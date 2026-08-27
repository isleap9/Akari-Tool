# Architecture Patterns

**Domain:** Windows desktop system-scan-and-act tools (uninstaller leftover cleanup, disk
cleanup/duplicate-file scanning, per-adapter NIC registry tuning) inside an existing WinUI 3
three-layer app
**Researched:** 2026-08-27

## Recommended Architecture

Build within the existing Core / Infrastructure / App layering — do not add a fourth layer, and
do not invent a new "scanning subsystem" namespace that sits outside it. Every one of the three
new tools decomposes the same way the app already decomposes its declarative tweak stack, just
with a bespoke (non-SettingDefinition) ViewModel per tool, matching Verify/Backup, not
SettingPageViewModel:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│ App (WinUI 3 shell)                                                     │
│                                                                          │
│  SystemToolsHubPage (HubView, 3 cards: Uninstaller Cleanup /            │
│  System Cleaner / Network Tuning) — mirrors AdvancedHubPage exactly     │
│                                                                          │
│  Per-tool: [Tool]Page.xaml (+ .cs) + [Tool]ViewModel (ObservableObject, │
│  RelayCommand) + [Tool]ResultRowViewModel (per-row, IsSelected bindable)│
│                                                                          │
│  ScanCommand: IProgress<ScanProgress> + CancellationTokenSource         │
│    → Task.Run(() => scanner.ScanAsync(progress, token)) → results       │
│    marshaled via ObservableCollection mutated ONLY inside               │
│    IProgress<T> callback (already runs on UI thread — see below)        │
│                                                                          │
│  ApplyCommand: TweakDialogs.ConfirmContentAsync(selected items) →       │
│    executor.DeleteSelectedAsync(selected) → ToolService.Log            │
└───────────────────────────────┬──────────────────────────────────────────┘
                                │ interfaces only (ILeftoverScanner, IJunkScanner,
                                │ IDuplicateFileScanner, INicTweakService, etc.)
┌───────────────────────────────▼──────────────────────────────────────────┐
│ Infrastructure (OS-touching)                                            │
│                                                                          │
│  Features/SystemTools/Services/                                        │
│    UninstallLeftoverScanner   — registry orphan scan + orphan folder/   │
│                                  scheduled-task scan (uses               │
│                                  IWindowsRegistryService, IFileSystemService)│
│    JunkFileScanner            — temp/cache enumeration                  │
│    DuplicateFileScanner       — hash-based large-drive walk             │
│    LargeFileScanner           — size-threshold walk                     │
│    NicTweakService            — per-adapter registry enumeration/apply  │
│                                  (IWindowsRegistryService,               │
│                                  IProcessExecutor for netsh/PS fallback) │
│                                                                          │
│  All scanners report progress via IProgress<T>, honor CancellationToken,│
│  return an immutable result list — no OS mutation during scan           │
└───────────────────────────────┬──────────────────────────────────────────┘
                                │
┌───────────────────────────────▼──────────────────────────────────────────┐
│ Core (pure C#, zero OS deps)                                            │
│                                                                          │
│  Features/SystemTools/Models/                                          │
│    LeftoverScanResult / LeftoverEntry (registry key | folder | task)    │
│    JunkFileEntry / DuplicateFileGroup / LargeFileEntry                  │
│    NicAdapterInfo / NicTweakDefinition / NicTweakValue                  │
│    ScanProgress (record: current path, items found, percent)           │
│                                                                          │
│  Features/SystemTools/Interfaces/                                      │
│    ILeftoverScanner, IJunkFileScanner, IDuplicateFileScanner,          │
│    ILargeFileScanner, INicTweakService — Task<T> scan signatures        │
│    taking IProgress<ScanProgress> + CancellationToken                   │
└─────────────────────────────────────────────────────────────────────────┘
```

This is the same shape as the existing `SettingOperationExecutor`/`SettingStateReader` split
(Infrastructure does the OS work, Core defines the contract and the data), just applied to
scan-then-review workflows instead of toggle/dropdown settings. **Scanning is a read (Infra
service, interface in Core); acting is a write (same Infra service, or a sibling apply method)
— never merge scan-and-delete into one non-cancellable method.**

### Component Boundaries

| Component | Responsibility | Communicates With |
|-----------|-----------------|--------------------|
| `SystemToolsHubPage` | Hosts `HubView`, supplies 3 `HubCardViewModel` entries | Drills into each tool's Page via `HubView.ShowDetail` |
| `[Tool]Page.xaml(.cs)` | XAML: results list (checkbox column), scan/cancel/apply buttons, progress bar | Binds to `[Tool]ViewModel`; no direct service calls |
| `[Tool]ViewModel` (App) | Orchestrates scan lifecycle, owns `ObservableCollection<[Tool]RowViewModel>`, selection state, apply command, dialog confirms | `I[Tool]Scanner` (Infrastructure, via DI), `TweakDialogs`, `ToolService`/`IAkariLogService`, `ITaskProgressService` (or a page-local progress bar for indeterminate long scans) |
| `I[Tool]Scanner` (Core interface) | Declares `Task<ScanResult> ScanAsync(IProgress<ScanProgress>, CancellationToken)` and `Task<ApplyResult> ApplyAsync(IReadOnlyList<Entry>, CancellationToken)` | Implemented by Infrastructure; consumed by App ViewModel only |
| `[Tool]Scanner` (Infrastructure impl) | Walks registry/filesystem/adapters, computes hashes, builds result entries; performs deletes/registry writes on Apply | `IWindowsRegistryService`, `IFileSystemService`, `IProcessExecutor` (netsh/PS where no native API exists), raw `System.IO`/`Microsoft.Win32.Registry` where the existing service interfaces don't cover the exact query needed (extend the interfaces first — don't reach past them) |
| `NicAdapterInfo`/`LeftoverEntry`/etc. (Core models) | Immutable scan-result records, pure data | Read by App for binding, produced by Infrastructure |

**Do not put scanning logic in the ViewModel.** The temptation with a "one-shot script button"
codebase (the current `ToolsPage.xaml.cs`) is to inline `Directory.EnumerateFiles` or
`Registry.OpenSubKey` directly in code-behind or the VM, the way `AdvancedToolsPage` currently
inlines `WimUtilService`/`AutounattendService` calls. For System Tools specifically, resist that:
duplicate-file hashing and full-registry-tree walks are exactly the kind of OS-touching,
long-running, testable-in-isolation logic the Infrastructure layer exists for, and NSubstitute
mocking of `I[Tool]Scanner` is how these get unit-tested (`Infrastructure.Tests`) without needing
a real disk full of test junk.

## Patterns to Follow

### Pattern 1: Background scan via `Task.Run` + `IProgress<T>` (no new machinery)

**What:** The app already has the exact primitive needed:
`TaskProgressService.CreateDetailedProgress()` returns `IProgress<TaskProgressDetail>`, and
`System.Progress<T>` captures the `SynchronizationContext` at construction time — since it's
constructed on the UI thread (inside a RelayCommand handler), every `Report()` call from the
background thread is automatically marshaled back to the UI thread. This is already the pattern
`HomePage.xaml.cs`, `SettingPageViewModel.ApplyAllRecommendedAsync`, and
`StartupNotificationService` use for bulk-apply progress. Scanning is the same shape as bulk-apply,
just read-only and producing a list instead of mutating settings.

**When:** Every scan operation (leftover scan, junk scan, large-file scan, duplicate-file scan).
Do NOT reuse `TaskProgressDetail` as-is for scans that need richer state (e.g. "3,412 files
scanned, 1.2 GB reclaimable so far") — define a `ScanProgress` record in
`Core/Features/SystemTools/Models/` shaped for scan semantics, and give each `[Tool]ViewModel`
its own `IProgress<ScanProgress>` via `new Progress<ScanProgress>(OnScanProgress)`, separate from
the global `ITaskProgressService` (which models one bottom-docked single-task bar — a scan wants
its own inline progress UI with a running count/size, not just a percent bar). Reserve
`ITaskProgressService` for a lightweight "scan running…" indicator in the shared bottom dock if
wanted; the tool page itself should render richer scan state locally.

**Example:**
```csharp
// App/ViewModels/SystemTools/DuplicateFileFinderViewModel.cs
[RelayCommand]
private async Task ScanAsync()
{
    _cts = new CancellationTokenSource();
    IsScanning = true;
    Results.Clear();

    var progress = new Progress<ScanProgress>(p =>
    {
        // Runs on the UI thread — Progress<T> captured the UI SynchronizationContext here.
        ScannedCount = p.ItemsScanned;
        CurrentPath = p.CurrentPath;
    });

    try
    {
        var groups = await _scanner.ScanAsync(progress, _cts.Token);
        foreach (var g in groups) Results.Add(new DuplicateGroupRowViewModel(g));
    }
    catch (OperationCanceledException)
    {
        _tool.Log("[SYSTEM-TOOLS] Duplicate scan cancelled by user.");
    }
    finally { IsScanning = false; _cts.Dispose(); _cts = null; }
}

[RelayCommand] private void CancelScan() => _cts?.Cancel();
```

**Infrastructure side — never touch UI types, report progress at a bounded rate:**
```csharp
// Infrastructure/Features/SystemTools/Services/DuplicateFileScanner.cs
public async Task<IReadOnlyList<DuplicateFileGroup>> ScanAsync(
    IProgress<ScanProgress> progress, CancellationToken ct)
{
    return await Task.Run(() =>
    {
        var bySize = new Dictionary<long, List<string>>();
        int scanned = 0;
        foreach (var file in EnumerateCandidateFiles(ct))
        {
            ct.ThrowIfCancellationRequested();
            // ... group by size first (cheap), hash only same-size groups (expensive) ...
            if (++scanned % 50 == 0)   // throttle — do NOT Report() per file, it floods the
                                       // UI-thread marshaling queue on a 500k-file drive
                progress.Report(new ScanProgress(scanned, file));
        }
        return HashAndGroup(bySize, progress, ct);
    }, ct);
}
```

### Pattern 2: Scan-then-review list with per-item selection (new UI pattern, model on Verify's structure — not its threading)

**What:** `VerifyViewModel` is the closest existing precedent for "render scan results, not
toggleable settings, in an `ObservableCollection<RowViewModel>` grouped by category, with
per-row and bulk actions" — reuse that shape (grouped `ObservableCollection`s, a row VM wrapping
an immutable Core record, `[RelayCommand]` per-row actions delegating to the parent VM). Do
**not** reuse Verify's threading choice: `VerifyViewModel.Scan()` runs `DriftScanner.Scan()`
synchronously on the UI thread "by design," because registry-only drift comparison is fast
(milliseconds). Duplicate-file hashing and full-drive enumeration are not — apply Pattern 1's
async scan instead, then land the results in the same kind of grouped `ObservableCollection`
Verify uses.

Every result row needs a bindable `IsSelected` (CheckBox-driven, not a bulk "select all" default
— PROJECT.md's safety constraint), a computed summary ("registry key" / "1.2 GB reclaimable" /
"folder, last modified 3 years ago"), and the group needs a running selected-count/selected-size
total bound to the Apply button's label ("Delete 12 items (340 MB)").

**When:** Uninstaller leftover review, System Cleaner (junk/large-file/duplicate results), and
optionally the NIC tweak page's "changed values" summary before a bulk apply.

**Example:**
```csharp
public sealed partial class LeftoverRowViewModel : ObservableObject
{
    public LeftoverEntry Entry { get; }
    [ObservableProperty] public partial bool IsSelected { get; set; }
    public string Kind => Entry.Kind switch
    {
        LeftoverKind.RegistryKey => "Registry key",
        LeftoverKind.Folder => "Folder",
        LeftoverKind.ScheduledTask => "Scheduled task",
        _ => "Unknown",
    };
    public string Path => Entry.Path;
    partial void OnIsSelectedChanged(bool value) => Parent.RecomputeSelection();
}
```

### Pattern 3: Apply-after-review through existing confirm + log seams

**What:** Once the user selects rows and hits Apply, route through the same
`TweakDialogs.ConfirmContentAsync` (custom content: item count + total size + a scrollable list
or "Show details") the bulk-apply confirm dialogs already use, then call the Infrastructure
scanner's `ApplyAsync(selectedEntries, ct)`, then log via `ToolService.Log` /
`IAkariLogService`. This keeps the destructive action behind the same dialog-serialization
(`SemaphoreSlim` gate) and fail-safe-on-no-XamlRoot behavior every other destructive action in
the app already has — do not hand-roll a new confirmation mechanism for these three tools.

**When:** Every delete/registry-write action across all three tools (leftover delete, junk/large-
file/duplicate delete, NIC value apply).

### Pattern 4: NIC tweak UI reuses the read/apply split, not a new registry abstraction

**What:** Per-adapter NIC tuning is fundamentally the same shape as the existing declarative
`SettingDefinition` stack (read current value → show current state → user changes it → write) —
but it is **not** a good fit for `SettingDefinition` itself, because SettingDefinition rows are
keyed to a single fixed registry path, and NIC tuning needs the same value written per-adapter
across a **dynamic, enumerated set of adapter registry subkeys**
(`HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-...}\00XX\`). Model it as its own small
Core interface (`INicTweakService`) with:
- `IReadOnlyList<NicAdapterInfo> GetAdapters()` (enumerate adapter subkeys)
- `IReadOnlyList<NicTweakValue> ReadCurrentValues(string adapterKey)` (per-value current state)
- `bool ApplyValue(string adapterKey, NicTweakDefinition def, object? value)` /
  `bool RevertValue(string adapterKey, NicTweakDefinition def)` (per-value, real revert — reads
  back the pre-change value and restores it, not a blanket script re-run)

Reuse `IWindowsRegistryService.GetValue`/`ApplySetting`/`KeyExists` for the actual reads/writes
(it already handles the DWord/String/Binary distinctions the rest of the app's registry tweaks
need); only add new Infrastructure methods where the existing interface doesn't cover an
operation (e.g., enumerating adapter subkeys under the network class GUID — extend
`IWindowsRegistryService.GetSubKeyNames` usage, don't bypass it with raw `Registry.OpenSubKey` in
the NIC service).

**When:** Building `NicTweakPage`/`NicTweakViewModel`. Per-adapter selection is a dropdown/list
at the top of the page (reuse `ComboBoxResolver`-style patterns where applicable), with the value
grid below re-reading on adapter change — architecturally the same "selection changes → re-read
sibling rows" flow `PowerPlanComboBox` already implements for Power Plans (`OnItemsSourceChanged`
→ re-push selection → dependent rows refresh). **Real per-value revert requires capturing the
pre-change value at apply time** (store it alongside the definition, e.g. in a small revert-log
model), not deriving revert from a fixed "Windows default" the way SettingDefinition's
`DefaultValue` does — NIC driver defaults vary per hardware/vendor and are not statically knowable
the way an OS registry default is.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Scanning synchronously on the UI thread because "it's just a read"

**What:** Copying `VerifyViewModel.Scan()`'s synchronous-on-UI-thread approach for the new scans,
reasoning that scans are read-only so blocking is "safe."
**Why bad:** Drift scanning reads a few hundred registry values (milliseconds). Duplicate-file
detection and large-file enumeration walk potentially millions of files across an entire drive and
compute hashes — this is minutes, not milliseconds, of blocking work. Running it on the UI thread
freezes the whole app (nav rail, log console, everything) for the duration, with no way to cancel.
**Instead:** Always wrap scanner calls in `Task.Run` from the ViewModel (Pattern 1), report
progress via `IProgress<T>`, and always create/expose a `CancellationTokenSource` the UI can
trigger from a visible Cancel button — the same `RunBusyAsync` shape `AdvancedToolsPage.cs` already
uses for its own long-running WIM operations (busy flag, cancel button visible, buttons disabled,
finally-block cleanup).

### Anti-Pattern 2: Deleting during the scan pass

**What:** Having the scanner delete/write as it discovers each leftover/junk file/duplicate,
instead of returning a result list first.
**Why bad:** Violates PROJECT.md's explicit safety constraint ("file/registry scans... are
read-only discovery; nothing deletes without explicit per-item user selection") and makes
cancellation dangerous — a scan cancelled mid-way could leave a partially-deleted, unreviewed set
of changes with no way to know what happened.
**Instead:** Strict two-phase split: `ScanAsync` returns an immutable result list and touches
nothing; `ApplyAsync(selectedEntries, ct)` is a separate call, only reachable after the user
reviews and selects, and itself supports cancellation between items (same "cancellation observed
only between items" constraint the app's existing bulk-apply loops already document, not
mid-item).

### Anti-Pattern 3: One flat `IReadOnlyList<T>` result with no incremental UI feedback

**What:** Scanning fully in the background and only updating the UI once at the very end with the
complete result list (`await scanner.ScanAsync()` with no progress reporting at all).
**Why bad:** A duplicate-file scan on a large drive can run for minutes; a static "Scanning…"
spinner with no counter gives the user no sense of progress and no confidence the app hasn't
hung — especially bad in an app whose whole premise is transparency ("no changes the user can't
see"). It also removes the only signal a user has for deciding whether to cancel.
**Instead:** Report incremental progress (files scanned, current path, running reclaimable-size
total) via `IProgress<ScanProgress>`, throttled to avoid flooding the UI-thread queue (batch every
N items, not every single item — see Pattern 1's `% 50` throttle).

### Anti-Pattern 4: Inlining OS scan logic directly in ViewModel or XAML code-behind

**What:** Following the precedent of `AdvancedToolsPage.xaml.cs` (which does call into
already-ported `WimUtilService`/`AutounattendService`, but constructs a lot of UI directly in
C#) or the very code being deleted (`ToolsPage.xaml.cs`, which runs `Directory.Delete`/registry
calls straight from button click handlers) and writing `Registry.OpenSubKey` or
`Directory.EnumerateFiles` calls directly inside a `[Tool]ViewModel` or page code-behind.
**Why bad:** Breaks the Core/Infrastructure/App boundary the rest of the app enforces
compiler-adjacently (Core has zero OS deps; only Infrastructure touches the OS); makes the scan
logic untestable without a real filesystem/registry (the existing `Infrastructure.Tests` project
mocks `IWindowsRegistryService`/`IFileSystemService` via NSubstitute specifically so OS logic can
be unit-tested); and risks missing the elevation-aware/ACL-exception handling
`WindowsRegistryService` already centralizes (e.g. `SecurityException` for ACL-locked keys).
**Instead:** New Infrastructure interfaces (`ILeftoverScanner`, `IJunkFileScanner`, etc.) in Core,
implementations in Infrastructure, DI-registered in `InfrastructureServiceExtensions.cs`,
consumed only through the interface from the App-layer ViewModel — exactly the existing
`ISettingOperationExecutor`/`SettingOperationExecutor` split.

## Scalability Considerations

| Concern | Small drive / few leftovers | Large drive (1TB+) / many apps uninstalled | Very large drive (multi-TB, 1M+ files) |
|---------|------------------------------|----------------------------------------------|------------------------------------------|
| Duplicate-file scan | Full hash of every candidate file is fine | Pre-group by file size before hashing (only hash files that share a size — avoids hashing unique files); consider a fast partial-hash (first 4KB) pre-filter before full hash | Same size/partial-hash funnel is mandatory, not optional; must remain cancellable mid-hash, and progress must reflect "groups being verified" not just "files walked" so a long hash pass doesn't look stalled |
| Large-file finder | Single enumeration pass, no special handling | `EnumerationOptions` with `IgnoreInaccessible = true` (skip ACL-denied dirs instead of throwing mid-walk) | Same; additionally consider excluding well-known noisy roots (WindowsApps, node_modules-style dirs) by default with an opt-in toggle, since a full C:\ walk otherwise dominates scan time |
| Leftover registry scan | Enumerate a handful of known uninstall-related hives directly | Still bounded — registry leftover scanning does not scale with drive size, only with install history, so this stays fast regardless of file count | Same — no special handling needed; this scan type never becomes the bottleneck |
| NIC tweak apply | Single adapter, immediate | Multiple adapters (VPN/virtual adapters included) — must let the user pick which adapters to include, not silently apply to every enumerated adapter (including virtual/loopback ones network-apply.bat currently blankets) | N/A — adapter count doesn't scale with drive size; the scaling concern here is correctness (virtual adapters shouldn't get physical-NIC tweaks), not performance |

## Build Order Implications

1. **Hub scaffold first, tools second.** `SystemToolsHubPage` (mirroring `AdvancedHubPage` +
   `HubView`) needs to exist and be wired into `MainWindow.xaml.cs`'s PageMap and
   `AdvancedHubPage`'s card list before any individual tool page has somewhere to navigate from —
   this is a small, low-risk phase (copy the existing hub pattern, 3 placeholder cards) that
   should land before tool-specific work starts, so each tool phase can be built and demoed
   independently inside a working hub shell rather than as an orphaned page.
2. **Shared scan/review scaffolding before the first tool's business logic.** The `ScanProgress`
   Core model, the `IProgress<T>` + `CancellationTokenSource` scan-lifecycle pattern (Pattern 1),
   and the reusable "grouped result list with per-row checkbox + running selected-count/size"
   row-VM shape (Pattern 2) are common to all three tools. Building the first tool (suggest
   Uninstaller leftover-cleanup — it's the smallest scan surface: known registry hives + a bounded
   folder/task check, no hashing) establishes this scaffolding; System Cleaner and NIC tweaks then
   reuse it rather than re-deriving it, so ordering leftover-cleanup before the disk-scanning tools
   pays down risk early on the cheapest tool.
3. **System Cleaner's three sub-scans can be sequenced by complexity, not necessarily built as one
   phase.** Junk/temp-file cleanup (simple enumeration, mirrors what the current one-shot
   "Clear Temp Files" button already does, just adding the review step) is lower-risk than large-file
   finder (needs the `IgnoreInaccessible` walk pattern) which is lower-risk than duplicate-file
   finder (needs the size/hash funnel and is the only one with real performance-tuning risk on
   large drives) — if the roadmap wants to split System Cleaner into sub-phases, this is the
   natural order.
4. **NIC tweak UI has no hard dependency on the other two tools** (different Infrastructure
   service, different Core models, no shared scan-review scaffolding beyond the general
   confirm/log/apply seams every tool already uses) — it can be built in parallel with or
   independent of Uninstaller/Cleaner ordering, but likely benefits from landing after at least
   one scan-review tool exists, since the "selection changes → re-read sibling values" flow
   should crib from whichever tool works out the grouped-row-VM pattern first, and its per-value
   revert-log model is genuinely new (no existing precedent to mirror, unlike the other two).
5. **Deletion of `ToolsPage.xaml(.cs)` and the nav-tag repoint happen last**, only once the new
   hub and all three tools are functional and cover the surviving sections (Repair & Health, Quick
   Shortcuts fold into the hub too, per PROJECT.md) — do not delete the old page and leave a
   partially-built hub as the only way to reach System Tools functionality mid-milestone.

## Sources

- `.planning/codebase/ARCHITECTURE.md` (2026-08-27 codebase mapping) — HIGH confidence, primary
  source for existing layering, DI, and threading constraints
- `.planning/codebase/STRUCTURE.md` (2026-08-27 codebase mapping) — HIGH confidence, folder/file
  conventions
- `.planning/PROJECT.md` — HIGH confidence, milestone scope and explicit safety/reversibility
  constraints
- Direct source reads (HIGH confidence, ground truth over any doc):
  `src/AkariTool.App/Services/TaskProgressService.cs`,
  `src/AkariTool.App/Views/Controls/TaskProgressControl.xaml.cs`,
  `src/AkariTool.App/Views/AdvancedToolsPage.xaml.cs` (RunBusyAsync busy/cancel/finally pattern),
  `src/AkariTool.App/Views/Controls/HubView.xaml.cs`,
  `src/AkariTool.App/ViewModels/Verify/VerifyViewModel.cs` (scan-result row VM shape; synchronous
  scan explicitly flagged as NOT to copy for slow scans),
  `src/AkariTool.App/Services/TweakDialogs.cs`,
  `src/AkariTool.Infrastructure/Features/Common/Interfaces/IFileSystemService.cs`,
  `src/AkariTool.Infrastructure/Features/Common/Interfaces/IWindowsRegistryService.cs`,
  `src/AkariTool.Core/Features/Common/Interfaces/IProcessExecutor.cs`
- General .NET async/UI patterns (`System.Progress<T>` `SynchronizationContext` capture,
  `CancellationToken` cooperative cancellation, `EnumerationOptions.IgnoreInaccessible` for
  resilient large-tree file walks) — MEDIUM confidence, standard .NET framework behavior, not
  independently re-verified against a live web source this pass since they match documented BCL
  semantics already relied on elsewhere in this codebase (`TaskProgressService` already uses
  `Progress<T>` this exact way)
