# Akari Tool — MVVM Migration Log (continued, part 3)

Continuation of MIGRATION_LOG.md and MIGRATION_LOG2.md (Phases 1-29, archived there). New phases logged here going forward.

---

## MVVM Phase 1 — Backup & Restore (+ Defender review-banner, isleap Option 2) — **COMPLETE (isleap confirmed working; VM sign-off pending)**

First phase in LOG3. Build #2 read-only; **no Defender code touched** (proof below).
Closes the Backup & Restore wave except the destructive VM round-trip.

### Scope

UI-only, over the already-ported, format-identical `TweakRegistry` engine (Phase 29).
`TweakRegistry.cs` NOT modified — only its existing static entry points are called.

### New / edited files

- `ViewModels/Backup/BackupViewModel.cs` — the two-card panel VM. Export → `IFileService.SaveFileAsync` → `TweakRegistry.ExportToFile(file.Path)`; Import → `IFileService.PickSingleFileAsync` → `PreviewImport` → (zero-diff ⇒ status only) / (diffs ⇒ review dialog) → `ImportFromFile(path, SelectedIds)`. All copy verbatim from net8.
- `ViewModels/Backup/ImportReviewDialog.cs` — the per-entry checkbox review `ContentDialog` (default-checked, `current → imported`), **plus the new Defender banner**.
- `Views/BackupPage.xaml(.cs)`.
- `App.xaml.cs` — `AddSingleton<BackupViewModel>()` (bespoke; NOT under the `TweakPageViewModel` enumeration, registers nothing).
- `MainWindow.xaml.cs` — `["Backup"] = typeof(BackupPage)` + rail sync, **and** `_files.WindowHandle = WindowNative.GetWindowHandle(this)` (the system pickers are Win32 dialogs and throw without the app HWND).

**FilePickers — no new helper written.** The in-house framework already provides
`WinUI.Framework.Services.IFileService` (`SaveFileAsync` / `PickSingleFileAsync` +
`FileSavePicker`/`FileOpenPicker` with `InitializeWithWindow`), DI-registered by
`AddWinUIFrameworkCore()` but previously unused in build #3. `StorageFile.Path` feeds
`TweakRegistry` unchanged. Only gap was the uninitialized `WindowHandle`, now set in
MainWindow.

### THE NEW PART — Defender review banner (Option 2), presentation only

In `ImportReviewDialog`, when the previewed **differing** set contains
`gaming-disable-defender`, a distinct caution callout is inserted at the TOP of the
dialog (above the scrollable diff list):

- **Container:** rounded `Border`, `SystemFillColorCautionBackgroundBrush` fill +
  `SystemFillColorCautionBrush` 1px border (amber/caution), padding 14/12.
- **Header row:** caution warning glyph (`\uE7BA`) + **bold** lead-in
  `"This backup will change Windows Defender protection:"` (minimal, factual — no
  invented urgency).
- **Body:** the row's own `TweakDefinition.Warning` **verbatim** —
  `"This fully disables Windows Defender and removes its servicing package. Tamper
  Protection MUST be off first (…). A restart is required to complete, and re-enabling
  also requires a restart. Continue?"`

The Defender row **still appears in the list below with its ordinary checkbox** — checked
means it's applied, unchecked means kept as-is, identical to every other entry. **This is
presentation only:** no new confirmation gate, no second dialog, no change to the apply
path or to what Backup can do. The opposite of net8, where this row rendered flat like any
other tweak.

The Warning text is read via `TweakRegistry.TryGetDefinition("gaming-disable-defender",
out def)` → `def.Warning` — a generic, read-only registry lookup. **No `DefenderService`,
`DefenderPhase2Scheduler`, or their call sites are called, referenced, or imported.**

### ⛔ Defender no-touch — verified with the migration's usual rigor

- `Services/DefenderService.cs`, `Services/DefenderPhase2Scheduler.cs`,
  `Tabs/Gaming/Catalog/GamingTweaks.Security.cs` (the Defender call site) — **all three
  `diff` byte-identical build #2 ⟷ build #3**, i.e. untouched.
- The new Backup files reference **zero** Defender code (`grep DefenderService|Phase2|
  NoDefender|DisableDefender.ps1` → nothing).
- The only Defender-adjacent tokens in new code are the Id string
  `"gaming-disable-defender"` and the `def.Warning` readback — both in
  `ImportReviewDialog.cs`, both presentation.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```

Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### Verification

- **`[WARMUP]` guard — unchanged at 439** (Backup registers nothing): `7 tweak page(s),
  439 tweaks … OK tiling [0..439)`.
- **Export summary reads live count** — de-elevated + UIA on the Backup page showed
  `"439 tweaks are currently tracked and will be included in the export."` (= the warm-up
  total). Confirms the VM reads `TweakRegistry.Count`.
- **isleap confirmed the tab works in the real app** ("its working") during this phase —
  covers the Export/Import buttons + the review flow that automated UIA could not drive.
- **Automated file-picker driving could NOT be completed in the sandbox.** The Export/
  Import buttons open the native Win32 `FileSavePicker`/`FileOpenPicker`; driving them
  requires foregrounding the dialog + SendKeys, which does not land under this headless
  automation session (the dialog is detected via UIA but the filename never commits — a
  known environmental limit, same class as "Mica doesn't render in a VM"). So the
  round-trip zero-diff assertion and the banner-render assertion were **not** captured by
  UIA; they rest on: (a) the engine being byte-identical (Phase 29), (b) the banner code
  above, and (c) isleap's manual confirmation. **No Defender import was ever confirmed by
  automation** (the script only ever cancels).

### VM checklist (Phase 1 — for isleap)

- [ ] Export → a file `AkariTool-Settings-<date>.json` with `format:"akari-tool-settings"`,
      `version:1`, and a `tweaks` object of the tracked rows; status shows the count.
- [ ] Import that same just-exported file → **zero differences**, status
      "Everything already matches — nothing to apply.", no dialog (the `Mark`/`ClaimRange`
      round-trip proof).
- [ ] Hand-edit a copy so `gaming-disable-defender` flips → Import → the review dialog
      shows the **caution Defender banner** with the verbatim Warning above the list, and
      the Defender row still present as a normal checkbox. **Cancel** to verify no-apply.
- [ ] (isleap's explicit, deliberate step, disposable VM ONLY) confirm an import that
      includes the Defender change actually actuates — this is the one destructive path
      the banner exists to make visible.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — `DefenderService`/`Phase2`/Security call site byte-identical, zero
  references from new code. Banner is presentation of the existing Warning string only.
- **`TweakRegistry.cs`** — not modified; only existing static methods called.
- **Remaining waves:** Advanced Tools (has the waiting `GetSelectedWindowsApps()` hook),
  Home / About / Tools.

---

## MVVM Phase 2 — Advanced Tools (SHOW ONLY)

Recon of net8's Advanced Tools tab. **Nothing created or edited in build #3;
`AutounattendService.cs` not touched; build #2 read-only.** All bodies read from source
this phase. **No Defender references anywhere in this tab or its ported services** (point 5
— clean, nothing to gate).

### 1. Files + the two features

**Build #2 — `Tabs/AdvanceTools/` (7 files, 1109 lines):**
- `AdvancedToolsTab.xaml` (10) / `.xaml.cs` (208) — `BaseTab` shell; three sub-panels
  (landing / wizard / generator) toggled by `ShowLanding`/`ShowWizard`/`ShowGenerator`;
  `RunBusyAsync` shared busy/cancel/progress wrapper; **`SetSelectedAppsProvider`** (the
  hook, §2).
- `AdvancedToolsTab.Landing.cs` (121) — two entry cards.
- `AdvancedToolsTab.Wizard.cs` (243) — WIM wizard scaffolding (`MakeStepCard`, step
  chrome, `BuildStep1..4` glue). No features/downloads/system writes of its own.
- `AdvancedToolsTab.Steps12.cs` (236) — Step 1 (Select ISO) + Step 2 (Add XML).
- `AdvancedToolsTab.Steps34.cs` (112) — Step 3 (Add Drivers) + Step 4 (Create ISO).
- `AdvancedToolsTab.Generator.cs` (179) — the Autounattend XML generator (§2).

**The tab has exactly TWO features, both launched from the landing (`BuildLanding`):**
1. **Windows Installation Media Utility** — a 4-step WIM wizard (Select ISO → Add XML →
   Add Drivers → Create ISO), backed by `WimUtilService`.
2. **Create Autounattend XML** — the generator, backed by `AutounattendService`.

Both landing cards are `enabled: true` (net8 has a "COMING SOON" pill path in
`MakeEntryCard` but neither card uses it). Nav: `NavTag "Advanced"`, `NavLabel "Advanced
Tools"`.

### 2. Autounattend generator — flow, call, and the provider hook

**What the user actually configures (point 2's list corrected):** the generator UI collects
**only two things** — it does **NOT** collect computer name, user accounts, or locale:
- **Windows apps to remove** — NOT entered here. A read-only summary card shows the apps
  currently ticked in **Software ▸ Windows Apps**, pulled through the provider hook. Copy:
  "{N} app(s) ticked … will be removed during installation: {first 12 names}".
- **Tweak checklists** — two groups of checkboxes (`AddTweakGroup`): **System Tweaks**
  (`UnattendTweakCatalog.All.Where(!UserScoped)`) and **User Tweaks** (`.Where(UserScoped)`),
  each row `IsChecked = opt.DefaultOn`.

Computer name / local-account setup / locale / Win11 hardware bypasses / .NET 3.5 are
**baked into the template + scripts by `AutounattendService` itself** (per the generator's
hint text), not surfaced as UI inputs. So the UI order is simply: (apps summary shown) →
tick tweaks → Generate.

**Exact call** (`Generator.cs:82-95`): `Generate autounattend.xml` button →
`FilePickers.SaveFileAsync("XML files", ".xml", "autounattend.xml")` → inside `RunBusyAsync`:
```csharp
var apps   = GetSelectedApps();      // _selectedAppsProvider?.Invoke() ?? []
var tweaks = GetSelectedTweaks();    // ticked UnattendTweakOptions
await Task.Run(() => _xmlGen.GenerateToFile(pickedXml, apps, tweaks));
```
(The wizard's Step 2 "Generate Akari XML" makes the same call but writes to
`{workDir}\autounattend.xml` instead of a picked path.)

**Provider wiring — matches Phase 25 exactly.** net8 `MainWindow.xaml.cs:253-254`:
```csharp
if (_tabs.TryGetValue("Advanced", out var advTab) && advTab is AdvancedToolsTab adv && _software is not null)
    adv.SetSelectedAppsProvider(() => _software.GetSelectedWindowsApps());
```
`SetSelectedAppsProvider(Func<List<AppDefinition>>)` → the generator reads
`_selectedAppsProvider?.Invoke() ?? []`. **Confirmed still true in build #3:**
- `AutounattendService.GenerateToFile(string, IReadOnlyList<AppDefinition>,
  IReadOnlyList<UnattendTweakOption>)` is ported and has **NO caller** (only a doc-comment
  mention in `WindowsAppsViewModel`).
- `WindowsAppsViewModel.GetSelectedWindowsApps()` (Phase 25 hook) is present and returns
  `List<AppDefinition>` — satisfies the `Func<List<AppDefinition>>` provider AND the
  `IReadOnlyList<AppDefinition>` parameter directly. The contract Advanced Tools must
  satisfy is: resolve the `WindowsAppsViewModel` singleton and pass
  `() => vm.GetSelectedWindowsApps()` as the provider.

### 3. Data model of the non-catalog pieces

**`UnattendTweakOption` — bespoke, NOT `TweakDefinition`/`AppDefinition`, its own identity
space. Already ported in build #3** (`Services/AutounattendService.Tweaks.cs`,
`UnattendTweakCatalog.All`). Record shape (positional):
`new(Id, ScriptFile, Name, Description, UserScoped, DefaultOn)` — e.g.
`new("telemetry", "Telemetry.ps1", "Disable Telemetry", "…", false, true)`.
- **Ids** are kebab-case strings (`telemetry`, `consumer-features`, `windows-ai`, `widgets`,
  `edge-debloat`, `services`, `hibernation-off`, `utc-time`, `wpbt`, `clean-start-menu`,
  `right-click-menu`, `remove-home-gallery`, `bg-apps`, `folder-discovery`, `storage-sense`,
  `end-task`, `visual-effects`, …). These are the generator's OWN ids — **not** shared with
  `TweakRegistry` (no registration, no Backup participation, `[WARMUP]` unaffected).
- Most `ScriptFile`s map to the same embedded `Scripts/*.ps1` payloads the Debloat tab uses
  (Telemetry.ps1, WindowsAI.ps1, Widgets.ps1, EdgeDebloat.ps1, WPBT.ps1, …); `clean-start-menu`
  has a **null** script (baked into the XML template, not a ps1). `UserScoped` splits the two
  UI groups; `DefaultOn` sets the initial checkbox.
- Overlap note (same pattern as Debloat/Phase 27): these reference the Debloat scripts, but
  as unattend-time options, not live actions.

**Logic layers — already ported, verified:**
- `WimUtilService.cs` — **`diff` byte-identical build #2 ⟷ build #3.** Covers every wizard
  call (`ValidateIsoFile`, `LooksLikeExtractedMedia`, `ExtractIsoAsync`, `DetectImagesAsync`,
  `ConvertImageAsync`, `DeleteImageFileAsync`, `AddDriversAsync`, `EnsureOscdimgAvailableAsync`,
  `GetOscdimgPath`, `CreateIsoAsync`, `DownloadAkariAutounattendXmlAsync`, `AddXmlToImageAsync`).
- `AutounattendService` — ported across 5 files (`.cs`, `.ScriptPreamble`, `.ScriptSystem`,
  `.ScriptUser`, `.Xml`, `.Tweaks`).

So, like Software and Backup, **the logic layer is present; Advanced Tools is UI-only to
port.** Only the 7 `AdvancedToolsTab.*` files are missing.

### 4. Destructive / live-system actions

The WIM wizard works almost entirely on a **working-directory copy of extracted ISO media**,
not the running OS. Inventory:
- **Live-system modification — ONE:** Step 4 **"Install oscdimg"**
  (`EnsureOscdimgAvailableAsync` → winget `Microsoft.OSCDIMG`, fallback Windows ADK) installs
  a tool onto the running machine. That is the only action that changes live system state
  rather than producing an output/working file.
- **Reads live system (non-destructive):** Step 3 "Extract & Add System Drivers"
  (`AddDriversAsync(_, null, …)`) exports the current driver store into the media copy.
- **Working-dir file ops (not the live OS):** ISO extraction, WIM⇄ESD conversion, and
  **Delete install.wim / install.esd** — the deletes remove files inside the extracted-media
  working folder the user is actively managing (not system files). Unconfirmed but low-stakes.
- **Output files only:** Create ISO (writes the bootable ISO), generate/download/add XML.
- **No registry writes, no service changes, no removal from the live OS, no reboot.**
- **No confirmation dialogs anywhere in the tab** (consistent with every prior recon) — but
  nothing here is Defender/OS-destructive, so this is lower-stakes than Debloat's unguarded
  removals.

### 5. Defender — NONE

`grep -i defender` over `Tabs/AdvanceTools/` + build #3's `AutounattendService*.cs` +
`WimUtilService.cs` → **zero hits.** No Defender code is referenced by this tab or its ported
services. Nothing to gate; no isleap go-ahead needed on Defender grounds.

### 6. Build #3 current state — placeholder, UI un-started (logic present)

- **Rail tag EXISTS:** `MainWindow.xaml:146` `Content="Advanced Tools" Tag="Advanced"`.
  **Not** in `MainWindow.xaml.cs` `PageMap` → falls through to `PlaceholderPage`.
- **No `AdvancedToolsTab` UI un-logged:** no `Tabs/AdvanceTools/`, no `Views/AdvancedTools*`,
  no `ViewModels/AdvancedTools*`. (Checked directly, not trusting CLAUDE.md.)
- **What IS present (Services layer, as with Software/Backup):** `WimUtilService.cs`
  (byte-identical), the 5 `AutounattendService*.cs` files + `UnattendTweakCatalog`, and the
  `GetSelectedWindowsApps()` provider hook — all waiting for a UI + the one-line
  MainWindow wiring.

**Dependency for the extraction wave:** net8's file dialogs use
`FilePickers.SaveFileAsync/OpenFileAsync/OpenFolderAsync`. Build #3 has no `FilePickers`, but
the framework `IFileService` (now `WindowHandle`-initialized since Backup/Phase 1) covers all
three (`SaveFileAsync` / `PickSingleFileAsync` / `PickFolderAsync`) — a mechanical mapping,
no new picker helper needed.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Build #3** — recon only; no files created or edited except this log entry.
  `AutounattendService.cs` / `WimUtilService.cs` not touched.
- **Defender** — no references found; nothing gated.
- No extraction, no MainWindow wiring, no `AdvancedToolsViewModel` — awaiting isleap's
  go-ahead.

---

## MVVM Phase 3 — Advanced Tools (both features + provider hook) — **COMPLETE (VM sign-off pending)**

UI-only port over already-ported, byte-identical logic (`WimUtilService`,
`AutounattendService*`, `UnattendTweakCatalog`). Build #2 read-only; **no Defender code
referenced** (Phase 2 recon confirmed the tab has none). This is the last catalog/tool
tab; only the bespoke Home/About/Tools layouts remain.

### New / edited files

- `ViewModels/AdvancedTools/AdvancedToolsViewModel.cs` — thin VM: owns the two services
  (`Wim`/`Xml`, constructed from `ToolService`) + the selected-apps provider hook
  (`SetSelectedAppsProvider` / `GetSelectedApps`). Registers nothing.
- `Views/AdvancedToolsPage.xaml` — scroll host + three code-filled panels
  (landing / wizard / generator).
- `Views/AdvancedToolsPage.xaml.cs` — core: panel nav, step status/gating, `RunBusyAsync`,
  landing cards, element helpers, brush adapters.
- `Views/AdvancedToolsPage.Wizard.cs` — the 4 WIM steps + step-card factory + conversion/
  oscdimg refresh.
- `Views/AdvancedToolsPage.Generator.cs` — apps summary + tweak checklists + Generate.
- `App.xaml.cs` — `AddSingleton<AdvancedToolsViewModel>()` (bespoke; NOT in the
  `TweakPageViewModel` enumeration).
- `MainWindow.xaml.cs` — `["Advanced"] = typeof(AdvancedToolsPage)` + rail sync + **the
  provider hook** (below).

### Port approach

net8's Advanced Tools is heavily imperative (4 collapsible step cards, live per-step
status, busy/cancel, a dynamic conversion sub-card, oscdimg state) — far more stateful
than the card/list panels ported as DataTemplates. So it's ported as a **code-behind Page**
(3 partial files) that mirrors net8's builders almost line-for-line, with three mechanical
adaptations, all behaviour-preserving:
- `TweakHelpers` tokens → stock Fluent `ThemeResource` brushes (+ a small `Hex()` for the
  two literal accents `#3DDC84`/`#FF7A88`; glyphs via a `G("hex")` helper).
- `FilePickers.OpenFileAsync/OpenFolderAsync/SaveFileAsync` → framework
  `IFileService.PickSingleFileAsync/PickFolderAsync/SaveFileAsync` (`.Path` feeds the
  services unchanged; `WindowHandle` already wired since Backup/Phase 1).
- `Service` → the injected `ToolService`.

**No new picker helper, no `.ps1`/service changes, no confirmation dialogs** (net8 parity —
recon confirmed nothing here is OS-destructive; the one live action, oscdimg install via
winget, is dialog-less in net8 too, preserved).

### Feature 2 — the provider hook (Phase 25's payoff)

`MainWindow.xaml.cs` wires it in one place, matching net8's
`adv.SetSelectedAppsProvider(() => _software.GetSelectedWindowsApps())`:
```csharp
ServiceLocator.GetService<AdvancedToolsViewModel>()
    .SetSelectedAppsProvider(
        () => ServiceLocator.GetService<WindowsAppsViewModel>().GetSelectedWindowsApps());
```
Both are DI singletons, so the provider points at the SAME `WindowsAppsViewModel` the
Bloatware page uses; its selection state persists across navigation. The generator's
`GetSelectedApps()` → `ViewModel.GetSelectedApps()` → provider → `GetSelectedWindowsApps()`.
Generate calls `AutounattendService.GenerateToFile(pickedXml.Path, GetSelectedApps(),
GetSelectedTweaks())` — the previously-callerless entry point now has its caller.

Tweak checklists are `UnattendTweakCatalog.All` split by `UserScoped`; `UnattendTweakOption`
is bespoke (not `TweakDefinition`/`AppDefinition`), registers nothing.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### Registration — guard UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP]   Gaming [0..130) … Power [403..439) 36 rows
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```
Neither Advanced Tools feature registers a tweak — 439 holds, as expected.

### Verification (read-only, de-elevated + UIA/mouse-injection; NOTHING generated/installed)

Full click-through worked this time — the landing cards are `Tapped` Borders (not
UIA-invokable), driven here by foreground + mouse injection on the card rect, which lands
on the main app window (unlike SendKeys to the modal file dialogs in Backup/Phase 1):
- **Landing** — both entry cards present ("Windows Installation Media Utility", "Create
  Autounattend XML").
- **WIM wizard** — all 4 step cards render: Select ISO, Add XML File, Add Drivers, Create ISO.
- **Generator** — both checklist groups render (System Tweaks / User Tweaks); initial apps
  summary reads "No Windows apps are currently ticked in Software › Windows Apps…".
- **✅ PROVIDER HOOK PROVEN LIVE** — ticked **Cortana** in Windows Apps (Bloatware), returned
  to Advanced Tools ▸ Generator, and the summary updated to:
  `"1 app(s) ticked in Software › Windows Apps will be removed during installation: Cortana"`.
  The cross-VM selection provider correctly reflects live Bloatware selection state — the
  exact contract Phase 25 built the hook for.
- No `[ERROR]` in the log; app alive throughout.
- **ISO creation / oscdimg install / XML generation were NOT run** — those file/tool
  operations are isleap's VM step (neither is OS-destructive per the recon, but they're not
  automated here).

### VM checklist (Phase 3 — for isleap)

- [ ] WIM wizard end-to-end on a real ISO: Select ISO → Extract → (optional convert / delete
      image) → Add XML (Generate/Hosted/Custom) → Add Drivers (system export) → Install
      oscdimg (winget) → Create ISO. Confirm each step's status + the done check-mark.
- [ ] Generator: tick apps in Bloatware + some tweaks → Generate autounattend.xml → confirm
      the file contains the app-removal + selected-tweak sections.
- [ ] After both, the `[WARMUP]` guard still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no references (Phase 2 recon); none added.
- **`WimUtilService.cs` / `AutounattendService*.cs` / `.ps1` payloads** — unmodified; only
  called.
- **Remaining wave:** Home / About / Tools (bespoke, non-catalog, non-tool layouts).

---

## MVVM Phase 4 — file picker failure under elevation (DIAGNOSIS ONLY)

No code changed. Build #2 read-only; no Defender code touched. Root cause identified;
scope determined; **fix deferred** for isleap's decision.

### How this was diagnosed

This sandbox's automation session runs at **Medium integrity, non-elevated**
(`whoami /groups` → `Mandatory Label\Medium Mandatory Level`; `IsInRole(Administrator)` →
False). The app's manifest is `requireAdministrator`, so I can neither launch the real
elevated build from here (UAC is non-interactive) nor drive its High-IL window (UIPI blocks
cross-integrity input) — the same reason every prior phase verified on the de-elevated
copy. **But isleap's own elevated runs today already logged the complete failures** to
`%LOCALAPPDATA%\AkariTool\Logs\app-2026-08-10.log` (a 13:15 Backup run and a 17:41 WIM run),
so the literal exceptions below are real, not reconstructed.

### 1. Exact failure — WIM ▸ Select ISO (Step 1)

**The picker never opens.** The click handler calls the WinRT picker, which throws
immediately and — because the Select ISO handler has no try/catch — propagates as an
unhandled exception. Literal, from isleap's elevated run:

```
17:41:05.025 [ERROR  ] Unhandled exception.
System.Runtime.InteropServices.COMException (0x80004005)
   at WinUI.Framework.Services.FileService.PickSingleFileAsync(IReadOnlyList`1 fileTypeFilter) in C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\Services\FileService.cs:line 27
   at AkariTool.Views.AdvancedToolsPage.<BuildStep1>b__7_0(Object _, RoutedEventArgs _) in C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\Views\AdvancedToolsPage.Wizard.cs:line 195
   at System.Threading.Tasks.Task.<>c.<ThrowAsync>b__124_0(Object state)
   at Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext.<>c__DisplayClass2_0.<Post>b__0()
```

`AdvancedToolsPage.Wizard.cs:195` is the `Select ISO` button → `_files.PickSingleFileAsync(new[]{".iso"})`.
`0x80004005` = `E_FAIL` ("Unspecified error"). The same COMException repeats on every WIM
picker button (Select ISO, Select Folder, custom XML, custom driver folder, output
location).

**Downstream pipeline is fine.** Later in the SAME session (once isleap got past the
pickers) the WIM logic ran to completion elevated — mount, extract, download XML, and build
an 8.8 GB ISO:
```
17:43:37 [WIM] Mounting ISO: …_windows_11_…x64_dvd_….iso
17:43:57 [WIM] ISO extracted to C:\Users\isleap\Desktop\AkariWIM
17:44:52 [WIM] ISO created: C:\Users\isleap\Desktop\AkariWindows.iso (8,829,564,928 bytes)
```
So the defect is **isolated to the picker entry points**; `WimUtilService` itself works
under elevation.

### 2. Systemic, not WIM-specific — Backup ▸ Export/Import fail identically

**Yes — the same failure.** Backup's Export (`SaveFileAsync`) and Import
(`PickSingleFileAsync`) throw the identical COMException under elevation, from isleap's
13:15 run:

```
13:15:59.838 [ERROR  ] Unhandled exception.
System.Runtime.InteropServices.COMException (0x80004005)
   at WinUI.Framework.Services.FileService.SaveFileAsync(String suggestedFileName, IReadOnlyList`1 fileTypeFilter) in …\FileService.cs:line 77
   at AkariTool.ViewModels.Backup.BackupViewModel.ExportAsync() in …\BackupViewModel.cs:line 85
   …
13:16:02.872 [ERROR  ] Unhandled exception.
System.Runtime.InteropServices.COMException (0x80004005)
   at WinUI.Framework.Services.FileService.PickSingleFileAsync(IReadOnlyList`1 fileTypeFilter) in …\FileService.cs:line 27
   at AkariTool.ViewModels.Backup.BackupViewModel.ImportAsync() in …\BackupViewModel.cs:line 109
```

Three distinct throwing call sites across the log, all bottoming out in `FileService`
pickers: `BackupViewModel.ExportAsync:85`, `BackupViewModel.ImportAsync:109`,
`AdvancedToolsPage.Wizard.cs:195`. **This is a framework-level `IFileService` defect, not a
WimUtilService one.**

> Correction to the Phase 1 record: Backup's "isleap confirmed working" covered rendering +
> the de-elevated automation path; it did **not** exercise the pickers under elevation,
> which fail exactly like this. Backup Export/Import are non-functional in the app's normal
> (elevated) launch mode today.

### 3. IFileService implementation — WinRT pickers (the documented elevation trap)

`WinUI.Framework/Services/FileService.cs` uses `Windows.Storage.Pickers` WinRT pickers:
- `PickSingleFileAsync` (line 27): `new FileOpenPicker` → `await picker.PickSingleFileAsync()`
- `SaveFileAsync` (line 77): `new FileSavePicker` → `await picker.PickSaveFileAsync()`
- `PickFolderAsync`: `new FolderPicker` → `await picker.PickSingleFolderAsync()`

each preceded by `WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle)`.

This is exactly the class documented to fail in elevated processes. The WinRT pickers
activate through an **out-of-process broker**; that broker refuses to serve a **High-
integrity, unpackaged** caller, surfacing as `COMException 0x80004005 (E_FAIL)`.
`InitializeWithWindow` fixes only the *unpackaged "no owner HWND"* problem — it does **not**
address the integrity/broker mismatch, which is why the call still throws under elevation
even though the same code works fine de-elevated (medium IL) in every prior phase.

### 4. Framework fallback — none; genuinely unhandled

`FileService` has **no** elevation detection, **no** try/catch, and **no** Win32
`IFileDialog` fallback — it constructs the WinRT picker, calls `InitializeWithWindow`, and
awaits. `grep` for `elevat|integrity|IFileDialog|try|catch|fallback|comdlg32` over
`FileService.cs` → nothing. The framework assumes a medium-IL / packaged host and does not
handle the elevated-unpackaged case at all.

### Scope for the fix (decision for isleap — NOT implemented here)

This is a **framework-level defect affecting every file-dialog in the app** — Backup
(Export + Import) and Advanced Tools (Select ISO, Select Folder, Select Custom XML, Select
Custom Driver Folder, Select Output Location, Generate XML save) — and any future picker
use. The fix is a **picker-implementation swap**, not a per-feature change: replace the
WinRT `Windows.Storage.Pickers` with the Win32 COM common dialogs
(`IFileOpenDialog`/`IFileSaveDialog`, CLSID `FileOpenDialog`/`FileSaveDialog`), which work
at any integrity level. It belongs in `WinUI-3-framework`'s `FileService` (behind the
existing `IFileService` interface, so no call site changes) — **but that is a change to the
shared framework, which every consumer depends on; confirm with isleap whether to fix it
there vs. an app-local `IFileService` override before touching it.**

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Build #3 / framework** — DIAGNOSIS ONLY; no code changed (`IFileService`,
  `WimUtilService`, `BackupViewModel`, and all picker code untouched).
- **Defender** — not involved.
- **Fix** — deferred; scope (framework-level picker swap) reported for isleap's call.

---

## MVVM Phase 4 (continued) — file picker fix (app-local override) — **COMPLETE de-elevated; elevated confirmation is isleap's step (see honesty note)**

App-local `IFileService` implemented with Win32 COM dialogs. Build #2 read-only; **no
Defender code touched; no WinUI-3-framework file touched**; no tweak logic (`[WARMUP]`
unaffected).

### Change (app-local only — framework untouched)

- **New:** `Services/AkariFileService.cs` — implements the full `IFileService` surface
  (`WindowHandle`, `PickSingleFileAsync`, `PickMultipleFilesAsync`, `PickFolderAsync`,
  `SaveFileAsync`, `ReadTextAsync`, `WriteTextAsync`) using the classic Win32 COM common
  dialogs (`IFileOpenDialog`/`IFileSaveDialog` via `CoCreateInstance`), mapped:
  `PickSingleFileAsync` → `IFileOpenDialog` (single-select); `PickMultipleFilesAsync` →
  `IFileOpenDialog` + `FOS_ALLOWMULTISELECT`; `PickFolderAsync` → `IFileOpenDialog` +
  `FOS_PICKFOLDERS`; `SaveFileAsync` → `IFileSaveDialog` (+ `FOS_OVERWRITEPROMPT`). Filters
  from the interface's extension list are applied via `SetFileTypes` (COMDLG_FILTERSPEC),
  and `SaveFileAsync`'s `suggestedFileName` via `SetFileName`/`SetDefaultExtension`. Results
  convert path → `StorageFile`/`StorageFolder` via the **path-based** WinRT statics
  (`GetFileFromPathAsync`/`GetFolderFromPathAsync`), which are broker-free and
  elevation-safe. Callers read only `.Path`, so the surface is unchanged.
- **Edited:** `App.xaml.cs` — one line, `services.AddSingleton<IFileService,
  AkariFileService>();` placed **after** `AddWinUIFrameworkCore()`, so MS.DI's
  last-registration-wins resolves every `IFileService` consumer (MainWindow's
  `WindowHandle`, BackupViewModel, AdvancedToolsPage/WimUtilService) to this one. **Zero
  call sites changed.**

### Why this fixes the elevated bug (root cause → cure)

Phase 4 diagnosed the WinRT `Windows.Storage.Pickers` throwing `COMException 0x80004005`
under elevation because they activate through an **out-of-process broker** that refuses a
High-integrity, unpackaged caller. The classic `IFileOpenDialog`/`IFileSaveDialog` are
activated **in-process** (`CoCreateInstance` in the app's own apartment) — there is no
cross-integrity broker marshalling, so the 0x80004005 rejection cannot arise. This is the
standard, documented file-dialog approach for elevated Win32/WPF/WinUI apps.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — unchanged at 439

`[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439)` (from the test runs).
This touches no tweak logic.

### ⚠ HONESTY NOTE — what was and was NOT verified (given the Phase 1 lesson)

This sandbox's automation runs at **Medium integrity, non-elevated**, and cannot obtain a
truly elevated process (`Start-Process -Verb RunAs` here returns `IsInRole(Administrator) =
False`). So I **cannot self-verify the elevated path** — the very mode the fix targets.
What I verified is the **de-elevated** path (proves the new Win32 implementation is
functionally correct end-to-end and did not regress) plus the architectural certainty
above. **The elevated confirmation is isleap's step. I am NOT claiming elevated works.**

### Verification — DE-ELEVATED, literal (fixed build; all COMExceptions in the log predate it)

Every `Unhandled exception. / COMException 0x80004005` in `app-2026-08-10.log` is from the
OLD build (13:15 Backup, 17:40–17:43 WIM). **Count of `Unhandled exception.` headers at/after
18:00 (the fixed-build test window): 0.**

- **Backup Export** — the new `IFileSaveDialog` opened, returned a path, and a real file was
  written:
  ```
  18:03:45.897 [INFO   ] [Backup] Exported 409 tweak(s) → C:\Users\isleap\Documents\AkariTool-Settings-2026-08-10.json (27 skipped — state unreadable)
  ```
  File validated on disk: 60,303 bytes, `"format":"akari-tool-settings"`, `"version":1`,
  409 `tweaks` entries (sample id `gaming-game-mode`). (Test artifact deleted afterward.)
- **Backup Import** — the new `IFileOpenDialog` opened, a file was picked, and `PreviewImport`
  ran (round-trip, zero differences):
  ```
  17:59:21.801 [INFO   ] [Backup] Import preview: no differences (0 unknown).
  ```
- **WIM ▸ Select ISO** — clicking it opened the `IFileOpenDialog` with **no exception** (0
  new `Unhandled exception.` after launch) — the same button that logged `COMException
  0x80004005` on the old build at 17:41:05.
- **No regression:** de-elevated, every dialog opens and completes exactly as before —
  the swap did not break the previously-working path.

### VM checklist (Phase 4-cont — for isleap; the elevated confirmation I could not self-run)

Run the normal (elevated) build and confirm — expect success + **no `COMException
0x80004005` in the log**:
- [ ] **Backup Export** → a settings JSON is written to the chosen path (status "Exported N
      tweak(s)…").
- [ ] **Backup Import** → picks a file; `PreviewImport` runs (review dialog or "everything
      matches").
- [ ] **WIM ▸ Select ISO / Select Folder / Add XML (custom) / Add Drivers (custom) / Create
      ISO output** → each dialog opens and returns a path.
- [ ] Log shows no `Unhandled exception. / COMException 0x80004005` from `FileService`/
      `AkariFileService`.
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **WinUI-3-framework** — untouched (app-local override, per isleap's decision).
- **Defender** — not involved.
- **`TweakRegistry` / `WimUtilService` / `AutounattendService` / picker call sites** —
  unchanged; only the `IFileService` implementation behind them was swapped.
- **Elevated runtime verification** — not self-runnable in this Medium-IL sandbox; handed to
  isleap (checklist above).

---

## MVVM Phase 5 — elevated re-verification (Backup + Advanced Tools) — **BLOCKED: cannot obtain an elevated instance in this environment (no code changed)**

Verification could **not** be performed. This environment cannot produce a genuinely
elevated (High-IL) instance of the app, so Parts A/B/C — which require the real elevated
build — are not runnable here. Reporting the blocker with proof rather than passing off
Medium-IL results as elevated (the Phase 1 "it's working" lesson). **No code changed; build
#2 read-only; no Defender code touched.**

### Why elevated verification is not performable here (measured, not assumed)

- **My automation session is Medium IL, non-elevated** — token integrity `0x2000`;
  `IsInRole(Administrator)` → False.
- **The account's admin membership is a UAC split/filtered token** — `whoami /groups` shows
  `BUILTIN\Administrators … Group used for deny only`, with `EnableLUA = 1`. Getting the
  full High-IL token requires an interactive UAC consent, which cannot happen in this
  non-interactive session.
- **The built exe currently runs at Medium IL** — I launched
  `bin\x64\Debug\…\win-x64\AkariTool.exe` and measured its token integrity: **`0x2000`
  (Medium)**, same as my session. No High-IL instance was produced.
- **Even if a High-IL instance existed, a Medium-IL automation session cannot drive it** —
  Windows UIPI blocks input injection / UIA pattern invocation from a lower-IL process to a
  higher-IL window.

⇒ Running Parts A/C here would exercise only the **Medium-IL** path — the exact path where
even the OLD (broken) WinRT pickers worked. A "pass" at Medium IL would prove nothing about
the elevated fix. So I did not run them.

### ⚠ Separate finding worth isleap's attention — the built exe is `asInvoker`, not `requireAdministrator`

While confirming the above I checked the manifest actually embedded in the current build:
- **Source `app.manifest`** (authoritative intent): `<requestedExecutionLevel
  level="requireAdministrator" …/>`.
- **The built exe** (`bin\…\AkariTool.exe`): the only `requestedExecutionLevel` embedded is
  **`level="asInvoker"`**. That is why my direct launch ran at Medium IL without a UAC
  failure.

This is a **build/manifest issue independent of the picker fix**: the current normal build
is not carrying the `requireAdministrator` level into the exe, so it would not auto-elevate
as intended. (isleap's earlier elevated runs that threw the elevation-only `COMException`
— 13:15 and 17:41 in `app-2026-08-10.log` — were genuinely High-IL, so on isleap's machine
the app does run elevated, presumably via a correctly-manifested build or "Run as
administrator." But the exe now sitting in `bin\` is asInvoker.) **Flagging, not fixing —
this task changes no code.** It may warrant its own look before the elevated re-test, so the
re-test runs against a truly requireAdministrator build.

### What is already established (Phase 4-cont, Medium-IL / de-elevated — passes clean)

The fix's implementation was verified functionally at Medium IL and did not regress:
- Backup Export wrote a real 60,303-byte / 409-tweak file
  (`[Backup] Exported 409 tweak(s) → …AkariTool-Settings-2026-08-10.json`).
- Backup Import round-tripped (`[Backup] Import preview: no differences (0 unknown).`).
- WIM ▸ Select ISO opened with no exception.
- **Zero `COMException 0x80004005` from the fixed build** (all in the log predate it).

The architectural reason it will hold elevated is unchanged: `AkariFileService` uses the
**in-process** Win32 `IFileOpenDialog`/`IFileSaveDialog` (`CoCreateInstance`), which have no
out-of-process broker and thus no integrity mismatch. But **that is reasoning + a Medium-IL
functional pass — NOT an elevated runtime confirmation**, which only isleap can produce.

### Parts A / B / C — status

- **Part A (elevated picker confirmation):** NOT RUN — requires High IL (unavailable here).
- **Part B (Defender banner):** NOT RUN. It is IL-independent (pure review-dialog UI), so it
  *could* be checked at Medium IL — but it requires driving the Import open-dialog to a
  specific hand-edited file, which the native file dialog does not allow reliable automation
  of in this sandbox (Phase 1/4 established the dialogs aren't drivable to a chosen path
  here). Not faked.
- **Part C (generator XML save):** NOT RUN — same elevated-build requirement as A.

### Ready-to-run checklist for isleap (on the real elevated machine)

First ensure the build actually embeds `requireAdministrator` (per the finding above), then:

- [ ] **A · Backup Export** → file written; note byte size + tweak count. Compare to
      Medium-IL baseline (60,303 B / 409 tweaks); a *higher* exported count (fewer than 27
      "state unreadable") is expected & good under real admin rights.
- [ ] **A · Backup Import** → pick that same file → PreviewImport reports **zero
      differences**.
- [ ] **A · WIM** → Select ISO / Add XML / Add Drivers / Create-ISO output picker each open
      without error.
- [ ] **A · log** → search the session for `0x80004005`; expect **zero** hits.
- [ ] **B · Defender banner** → hand-edit a copy of the export so `gaming-disable-defender`
      differs → Import → the review dialog shows the caution Defender banner + verbatim
      Warning above the diff list → **Cancel** (do not confirm).
- [ ] **C · Generator** → tick a couple of tweaks → Generate → the `.xml` save dialog opens
      and a real file is written.

### Not touched / not done

- **No code changed** — the picker fix is exactly as shipped in Phase 4-cont.
- **Build #2 (net8)** — read-only.
- **Defender** — not touched; Part B not run.
- **Elevated verification** — blocked by environment (proof above); handed to isleap.

---

## MVVM Phase 6 — manifest embedding regression (DIAGNOSIS)

Cause found: **stale/incremental-build artifact.** A genuinely clean rebuild embeds the
correct `requireAdministrator` manifest; the defect does not survive a full `bin`/`obj`
wipe. No code or project-config change needed (guidance below). Build #2 read-only; no
Defender code touched.

### 1. Source is correct

`app.manifest` (verbatim, the relevant node):
```xml
<requestedPrivileges>
  <requestedExecutionLevel level="requireAdministrator" uiAccess="false"/>
</requestedPrivileges>
```
`AkariTool.csproj:7`: `<ApplicationManifest>app.manifest</ApplicationManifest>` — correctly
referenced. The intent and wiring are right; only the build OUTPUT was wrong.

### 2. Clean rebuild — the defect disappears

- **Before (dirty `bin\` from prior incremental builds):** exe embedded
  `requestedExecutionLevel level="asInvoker"` (the Phase 5 finding, re-confirmed).
- **Deleted `bin\` and `obj\`**, then rebuilt. (Note: a single `/t:Restore,Rebuild`
  invocation broke XAML codegen — `InitializeComponent` missing — a known SDK ordering
  issue; running `/t:Restore` and `/t:Rebuild` as **separate** MSBuild passes builds cleanly.
  Literal output of the clean build:)
  ```
    WinUI.Framework -> …\WinUI.Framework.dll
    AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
  ```
- **After (fresh exe):** `requireAdministrator` count = **1**, `asInvoker` count = **0**.
- **Runtime confirmation:** launching the fresh exe produced a process at **High integrity
  (0x3000) — genuinely ELEVATED** (vs the stale asInvoker build, which ran Medium 0x2000).

### 3. Verdict — stale artifact (point 3 applies)

**A clean rebuild fixes it. The `requireAdministrator` manifest is correct in clean output;
no further code/config fix is needed.** Point 4 (a real project/SDK misconfiguration) does
**not** apply — the source and csproj are correct and clean output is correct.

**Root cause of the staleness (how the `obj\` got poisoned):** the de-elevated test copies
built during Phases 4/5 used `/p:ApplicationManifest=asinvoker.manifest` but overrode only
`/p:OutputPath` (a separate `deelev\` folder) — **not** the intermediate directory. So those
builds shared the normal `obj\`, writing the *asInvoker* Win32-manifest intermediate there.
Standard `/t:Rebuild` (Clean+Build) did **not** purge that cached manifest resource, so the
next normal build re-embedded asInvoker. Only a physical `obj\` deletion cleared it — which
is exactly what this diagnosis did.

**Recommended practice (documenting, not changing anything):**
- **Always physically delete `bin\` + `obj\` before an elevation-sensitive release/test
  build** — `/t:Rebuild` alone is not sufficient to refresh the embedded manifest here.
- When building a de-elevated `asInvoker` test copy, give it its **own intermediate dir**
  (e.g. add `/p:BaseIntermediateOutputPath=obj-deelev\` alongside the `OutputPath` override)
  so it can never contaminate the normal `obj\`. That removes the poisoning at the source.

### Bonus — this also explains Phase 5's "can't obtain an elevated instance"

Phase 5 concluded the environment couldn't produce a High-IL app instance. That was itself a
**downstream symptom of this same regression**: the exe under test was the stale *asInvoker*
build, which runs Medium by design. The correctly-manifested clean build **does elevate to
High IL (0x3000) here**. So the environment is not the blocker — the wrong manifest was.
(Automated *driving* of an elevated window from this Medium-IL session is still UIPI-blocked,
so I still can't run Phase 5's Parts A/B/C myself — but they can now be run against a build
that genuinely requests elevation, which was the precondition isleap flagged.)

### Not touched / not done

- **No code or project-config changed.** The clean rebuild (requested in point 2) left a
  correct exe in `bin\` as a side effect; nothing was edited.
- **Build #2 (net8)** — read-only.
- **Defender** — not involved.
- **Picker fix** — untouched; unaffected by this build-pipeline issue.

---

## MVVM Phase 6 (continued) — prevent obj\ contamination — **COMPLETE**

Small build-config fix so the de-elevated `asInvoker` test build can never again write
into the normal (requireAdministrator) build's `obj\`. No functional/UI code touched;
normal builds are byte-for-byte unaffected (the change is a no-op unless the test flag is
set). Build #2 read-only; no Defender code.

### Change (two files, build-config only)

- **New `build-deelevated.ps1`** (repo root — not under `Scripts\`, so it is NOT embedded):
  the canonical, isolated way to build the de-elevated test copy. It locates VS MSBuild via
  vswhere, generates the `asInvoker` manifest from `app.manifest` **into** `obj\DeElevated\`,
  then builds with `/p:DeElevatedTest=true /p:OutputPath=bin\DeElevated\`. Restore runs as a
  separate pass (a combined `/t:Restore,Rebuild` breaks WinUI XAML codegen — Phase 6).
- **`AkariTool.csproj`** — one conditional PropertyGroup:
  ```xml
  <PropertyGroup Condition="'$(DeElevatedTest)' == 'true'">
    <IntermediateOutputPath>obj\DeElevated\</IntermediateOutputPath>
  </PropertyGroup>
  ```

### Why redirect the LEAF `IntermediateOutputPath`, not `BaseIntermediateOutputPath`

First attempt passed `/p:BaseIntermediateOutputPath=obj\DeElevated\`. That failed hard:
the global property **propagated to the referenced `WinUI.Framework` project** and, by
moving the *base* obj, **un-excluded the stale default `obj\` from the default compile
globs** — producing dozens of `CS0579 Duplicate 'AssemblyInfo'` / `CS0111` XAML-codegen
errors. The correct isolation keeps `BaseIntermediateOutputPath = obj\` (so `obj\**` stays
glob-excluded and restore/props stay shared) and redirects only the **leaf**
`IntermediateOutputPath` — the folder the Win32 manifest intermediate is actually written
to — and only in the app csproj (the condition is absent from the framework, so it is
untouched).

### Verification (literal)

**Step 1 — de-elevated build (`build-deelevated.ps1`):**
```
De-elevated build -> IntermediateOutputPath: …\obj\DeElevated\  OutputPath: …\bin\DeElevated\
  Determining projects to restore...
  All projects are up-to-date for restore.
  WinUI.Framework -> …\bin\DeElevated\WinUI.Framework.dll
  AkariTool -> …\bin\DeElevated\AkariTool.dll
```
- `bin\DeElevated\AkariTool.exe` embedded level = **`asInvoker`** ✓
- Intermediates isolated: `obj\DeElevated\` populated (**129 files**, incl.
  `app.asinvoker.manifest`) ✓

**Step 2 — NORMAL `/t:Rebuild` of the real app (NOT a clean; reuses the shared `obj\`):**
```
  WinUI.Framework -> …\WinUI.Framework\bin\x64\Debug\…\WinUI.Framework.dll
  AkariTool -> …\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
- `bin\x64\Debug\…\AkariTool.exe` embedded level = **`requireAdministrator`** ✓
- The normal `obj\x64\Debug` leaf's manifest intermediate reads **`requireAdministrator`**
  (no asInvoker anywhere in the normal obj) ✓

**Proof of structural prevention:** the de-elevated build ran first, then a plain
incremental `/t:Rebuild` (which reuses `obj\`) still produced `requireAdministrator`. Had
the de-elevated build touched the normal `obj\` manifest intermediate (the Phase-6
contamination), the incremental normal build would have re-embedded `asInvoker` — as it did
before this fix. It now embeds `requireAdministrator`. The contamination path is closed by
construction: the de-elevated build's intermediates live in a different leaf folder the
normal build never reads.

### Note (pre-existing, out of scope)

The repo has **no `.gitignore`**, so `bin\` / `obj\` (including `obj\DeElevated\`,
`bin\DeElevated\`) are not ignored. Pre-existing condition, unrelated to this fix — not
changed here.

### Not touched / not done

- **No functional/UI code changed** — only `build-deelevated.ps1` (new) + one conditional,
  no-op-by-default PropertyGroup in the csproj.
- **Normal builds unaffected** — `DeElevatedTest` is unset, so `IntermediateOutputPath`
  stays default; confirmed by Step 2 producing an unchanged `requireAdministrator` exe.
- **Build #2 (net8)** — read-only. **Defender** — not involved. **Picker fix** — untouched.

---

## MVVM Phase 8 — Home / About / Tools (SHOW ONLY)

Recon of net8's Home, About, Tools tabs. **Nothing created or edited in build #3; build #2
read-only.** **No Defender references anywhere in these three tabs** (point 5 — grep clean,
nothing to gate). No file pickers anywhere in these three (point 4 — none need the
Backup/Advanced elevated-picker treatment).

### ⚠ Completeness correction up front (point 6 / "don't assume the framing")

**Home/About/Tools is NOT "the last remaining wave."** Enumerating every build #3 rail tag
vs the `PageMap`, the tags still falling through to `PlaceholderPage` include three MORE
bespoke tabs with their own net8 implementations that this wave does not cover:
- **AkariOS** (`Tabs/AkariOS/`), **Verify** (`Tabs/Verify/`), **AppUpdate** (`Tabs/AppUpdate/`).

Plus six Customize **sub-nav** tags (`Appearance`, `ContextMenu`, `Desktop`, `Explorer`,
`StartMenu`, `Taskbar`) are in the rail but absent from `PageMap` — they route to
`PlaceholderPage` even though Customize itself is "fully rolled out." That's a separate
Customize sub-navigation gap, not this wave, but flagging since the Home cards deep-link to
some of them (e.g. card "Customize" → tag `Taskbar`).

So after Home/About/Tools, **AkariOS + Verify + AppUpdate remain** (and the Customize
sub-tag routing). The migration is not complete at the end of this wave.

---

### HOME

**1. Files / build #3 state.** net8: `Tabs/Home/HomeTab.xaml` (10) + `.xaml.cs` (589).
Build #3: `Views/HomePage.xaml` (240) + `.xaml.cs` + `ViewModels/HomeViewModel.cs` **EXIST
— but as a Phase-A DEMO STUB, not the net8 Home.** The stub has the real system-info banner
(`SystemInfoService.Gather` → Edition/Version/CPU/GPU/Memory, present in build #3 Services)
plus **demo** actions: `ShowAboutAsync` (a canned dialog), `LogSomething` ("demo log line"),
and `GoToTab` which navigates to **`PlaceholderPage`**. Routed (`PageMap["Home"]=HomePage`),
so it's not a placeholder — but it is not the net8 feature set.

**2. What net8 Home actually contains** (`Build()`):
- PageHeader "Akari Tool" + subtitle.
- **System information banner** (`BuildSystemBanner`, background WMI gather) — the one piece
  the stub already has.
- **Global search across ALL tabs** (`BuildGlobalSearchBar` + `RunGlobalSearch`) — walks
  `_searchSources` (per-tab label + root + navigate) and shows a results panel. Real
  interactive feature; the stub does NOT have it.
- **Quick-access card grid**, grouped SOFTWARE / OPTIMIZE / ADVANCED, ~14 cards
  (Windows Apps→`Bloatware`, External Apps→`AppInstaller`, Debloat, AkariOS, Gaming, Privacy,
  Update, Notifications, Power, Customize→`Taskbar`, Tools, Advanced, Backup, Verify) — each
  card navigates to a rail tag.

**3. Data model.** Cards = static `HomeCardDef` (title/glyph/desc/tag) → navigation. Global
search = read-only query over rendered tab roots; build #3's equivalent is the already-ported
`TweakRegistry.Search`. **Nothing TweakDefinition-backed; registers nothing.**

**4. File I/O / elevated-picker.** **None.** No pickers, no file writes.

---

### ABOUT

**1. Files / build #3 state.** net8: `Tabs/About/AboutTab.xaml` (10) + `.xaml.cs` (261).
Build #3: **no UI — rail tag `About` is not in `PageMap` → `PlaceholderPage`.** (Its one
dependency, `UpdateService.CurrentVersionDisplay`, IS ported in build #3 Services.)

**2. What it contains** (static/read-only):
- Title "Akari Tool" + **version pill** (`UpdateService.CurrentVersionDisplay`).
- Description paragraph ("A gaming-first Windows optimization utility…").
- **Credits/attribution**: "Registry tweak references from CTT WinUtil and Winhance. Advanced
  Tools ISO flow ported from Winhance. Sidebar icons by Icons8" — with an `Icons8` Hyperlink
  (`https://icons8.com`).
- **Two link buttons** → `Service.OpenUrl`: **Repository** (`https://github.com/isleap9/Akari-Tool`)
  and **".NET 8 Runtime"** (`https://dotnet.microsoft.com/.../dotnet/8.0`).

**3. Data model.** Entirely static display + external-URL buttons. **No update-check
action** (only the version string is shown — see point 6). Nothing interactive beyond
`OpenUrl`.

**4. File I/O / elevated-picker.** **None.**

**Copy note (not a defect):** the ".NET 8 Runtime" link + `RuntimeUrl` are **stale for
build #3**, which targets **net10**. Worth updating the label/URL if/when ported (flagging,
not fixing — recon only).

---

### TOOLS

**1. Files / build #3 state.** net8: `Tabs/Tools/ToolsTab.xaml` (10) + `.xaml.cs` (508).
Build #3: **no UI — rail tag `Tools` not in `PageMap` → `PlaceholderPage`.** All the ps1
payloads Tools runs are **already embedded** in build #3 `Scripts/` (ported earlier);
`SystemInfoService` exists but Tools uses its OWN richer readers (see below).

**2. What it contains** — five sections:
- **System Information** (`BuildSystemInfo`) — a detailed spec sheet read live from registry
  + WMI via bespoke `GetRegValue`/`GetWmiValue(s)` helpers (**20 calls**): Edition, build,
  Win10/11, CPU name/cores/logical/clock, RAM speed, GPU(s), motherboard, storage (type/size),
  display(s), network adapter, **activation status** (`SoftwareLicensingProduct.LicenseStatus`).
  Plus a **"Copy to Clipboard"** button. All read-only.
- **Repair & Health** (`BuildRepair`) — SFC Scan (`SfcScan.ps1`), DISM Repair (`DismRepair.ps1`),
  Create Restore Point (`RestorePoint.ps1`).
- **Network** (`BuildNetwork`) — Flush DNS (`FlushDns.ps1`), Reset Network Stack
  (`WinsockReset.ps1`), and a DNS-provider switch: Cloudflare/Google/Quad9/Auto
  (`SetDns*.ps1`).
- **Maintenance** (`BuildMaintenance`) — Clear Temp Files (`TempFiles.ps1`), Disk Cleanup
  (`DiskCleanup.ps1`), Rebuild Icon Cache (`IconCacheRebuild.ps1`).
- **Quick Shortcuts** (`BuildShortcuts`) — Task Manager, Startup Apps, Device Manager, Event
  Viewer, Windows Update, Disk Management, Services, Resource Monitor, Registry Editor — via
  `Process.Start(… UseShellExecute=true)` on an exe/`.msc` (`Launch`) or `ms-settings:` URI
  (`LaunchMs`).

**3. Data model.** System info = read-only WMI/registry (bespoke `ToolsTab` helpers, NOT the
5-field `SystemInfoService`). Repair/Network/Maintenance buttons → `Service.RunScript(script)`
— the same `ToolService.RunScript` (embedded ps1) the Debloat/Advanced ports use; **bespoke,
not TweakDefinition, registers nothing.** Shortcuts = `Process.Start` shell-execute.

**4. File I/O / elevated-picker.** **None** — no `SaveFileDialog`/`OpenFileDialog`/
`FilePickers` anywhere in Tools. "Copy to Clipboard" is clipboard, not file I/O. `RunScript`
extracts an embedded ps1 to `%TEMP%` internally (no dialog). `Process.Start` shell-execute
launches system tools/URIs. So **no elevated-picker treatment is needed for this wave** —
unlike Backup/Advanced. (The scripts run at the app's elevation, which is normal, not
picker-related.)

---

### 5. Defender — NONE

`grep -i defender` across `HomeTab.xaml.cs` + `AboutTab.xaml.cs` + `ToolsTab.xaml.cs` →
**zero hits.** Nothing to gate in any of the three.

### 6. Build #3 routing + the update-check flow

- **Home** → `PageMap["Home"] = HomePage` (routed; Phase-A stub, not the full port).
- **Tools** → not in `PageMap` → `PlaceholderPage`.
- **About** → not in `PageMap` → `PlaceholderPage`.
- **Startup update-check flow does NOT touch these three.** `UpdateService.CheckAsync` is
  ported (Services) but is **not called** from `App.xaml.cs`/`MainWindow` in build #3 — the
  startup check is not wired yet, and net8's flow targets the **AppUpdate** tab, not
  Home/About/Tools. About merely *displays* the version string; it performs no check.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Build #3** — recon only; no files created or edited except this log entry.
- **Defender** — no references found in these tabs; nothing gated.
- **Out of this wave but still unported (flagged for planning):** AkariOS, Verify, AppUpdate
  tabs; the Customize sub-tag routing; and Home's demo stub → full-Home replacement.

---

## MVVM Phase 9 — Home / About / Tools ported — **COMPLETE (VM sign-off pending)**

The three bespoke tabs from Phase 8's recon, ported. Build #2 read-only; **no Defender code
referenced** (Phase 8 confirmed none). No file pickers → no elevated-picker treatment
needed. None registers tweaks — `[WARMUP]` stays 439.

### New / edited files

- **Home** — `ViewModels/HomeViewModel.cs` trimmed to the banner ONLY (removed the Phase-A
  demo `ShowAbout`/`LogSomething`/`GoToTab→PlaceholderPage` + their `IDialogService`/
  `INavigationService` deps; ctor now takes just `ILogService`). `Views/HomePage.xaml`
  rewritten: header + bound system banner + search box + code-filled results/cards panels.
  `Views/HomePage.xaml.cs`: builds the 3-group / 14-card quick-nav grid and wires global
  search to `TweakRegistry.Search`.
- **About** — `Views/AboutPage.xaml(.cs)` (new): header (logo + version pill via
  `UpdateService.CurrentVersionDisplay` + tagline), Environment + Credits cards, Repository +
  runtime link buttons.
- **Tools** — `Views/ToolsPage.xaml(.cs)` (new): the five sections (System Information,
  Repair & Health, Network, Maintenance, Quick Shortcuts).
- **MainWindow.xaml.cs** — `PageMap` + rail-sync for `Tools`/`About`; new public
  `SelectRailTag(tag)` so Home cards route through the normal rail path (real page, or
  PlaceholderPage for still-unported tabs).

### Design notes (all behaviour-preserving)

- **Home global search** — net8 DOM-walked rendered tab controls for `search:` Tag markers;
  that rendering doesn't exist in the MVVM build, so it's rewired to the already-ported
  `TweakRegistry.Search(query)` (returns `SearchHit(Id,Name,Description,TabTag,TabLabel)`),
  grouped by `TabLabel`, each row navigating to `TabTag`. This is the intended build-#3
  mechanism (per the task) and is read-only — registers nothing.
- **Home cards** — use Segoe glyphs (net8's fallback path) rather than the `Resource/NavIcons`
  PNGs, avoiding an asset dependency. Titles/descriptions/tags verbatim.
- **About logo** — plain logo `Image` (Assets/AkariLogo.png) instead of net8's Composition
  glow + theme-swap brand ellipse (which used build-#2 `AkariShadow`/`ThemeService` helpers
  not in build #3). Content identical — same "shadows return in the cosmetic pass" deviation
  logged for every prior wave.
- **Tools system info** — reuses `SystemInfoService.GetRegValue/GetWmiValue/GetWmiValues`;
  the Tools-only WMI helpers (`GetRamGb`/`GetRamType`/`GetDriveInfo`/`GetActivationStatus`)
  ported verbatim into the page. **All reads are read-only** — nothing writes. Repair/Network/
  Maintenance → `ToolService.RunScript` against the already-embedded `Scripts/*.ps1` (**no ps1
  modified**). Shortcuts → `Process.Start(UseShellExecute)` verbatim.

### ABOUT — the .NET 8 → .NET 10 factual correction (exact before/after)

Two spots, both corrected because build #3 targets **net10** (a platform-migration fact, not
net8 behaviour to preserve):

| Location | Before (net8) | After (net10) |
|---|---|---|
| Environment card, "Framework" row | `.NET 8 Desktop` | `.NET 10 Desktop` |
| Link button label | `.NET 8 Runtime` | `.NET 10 Runtime` |
| Link button URL | `https://dotnet.microsoft.com/en-us/download/dotnet/8.0` | `https://dotnet.microsoft.com/en-us/download/dotnet/10.0` |

(Verified live: About shows ".NET 10 Desktop" + a ".NET 10 Runtime" button; **no ".NET 8"
text remains** on the page.)

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated copy built via `build-deelevated.ps1`
(isolated `obj\DeElevated\`, Phase 6) — clean.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (read-only, de-elevated + UIA/mouse-injection; no script/shortcut run)

**HOME**
- Banner renders (Edition/CPU labels present, bound to the VM).
- All 3 section headers (SOFTWARE / OPTIMIZE / ADVANCED) + cards (Windows Apps, Gaming,
  Verify System, …) render.
- **Global search works across tabs** — typing `game` returned a `GAMING & PERFORMANCE`
  group with real tweak rows: `Game Mode`, `System Responsiveness for Games`, … (live
  `TweakRegistry.Search` results).
- **Card routing works to a REAL page** — clicking the `Gaming` card navigated to the real
  GamingPage (found "Gaming & Performance" / "System Services"), **not** PlaceholderPage.

**TOOLS**
- All five section headers present (SYSTEM INFORMATION / REPAIR & HEALTH / NETWORK /
  MAINTENANCE / QUICK SHORTCUTS).
- **System Information shows real values**: `Windows 11 Professional 25H2 (Build 26200.8875)`,
  `AMD Ryzen 7 5800X 8-Core Processor @ 3,80 GHz (8C / 16T)`, `32 GB DDR4 3200 MHz`, plus
  Activation.
- Buttons present: 8 `Run` (Repair 3 + Network 2 + Maintenance 3), the 4 DNS buttons
  (Cloudflare…), 10 shortcuts (Task Manager, Registry Editor, …), Copy to Clipboard.
- **Nothing was clicked** — the Run/DNS/shortcut buttons execute real scripts/shell commands;
  that's isleap's VM step.

**ABOUT**
- Title "Akari Tool", version pill, Environment + Credits cards, credits text (CTT WinUtil,
  Winhance, Icons8), Repository + `.NET 10 Runtime` buttons — all present; **`.NET 8` absent.**

### VM checklist (Phase 9 — for isleap)

- [ ] Tools ▸ each Repair/Network/Maintenance `Run` executes its script; DNS buttons switch;
      shortcuts launch; Copy to Clipboard copies the spec sheet.
- [ ] About link buttons open the repo + the **.NET 10** runtime page.
- [ ] Home search + every quick-nav card lands on the right page.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no references in these tabs; none added.
- **`.ps1` payloads** — unmodified; only invoked.
- **STILL REMAINING (not this task):** AkariOS, Verify, AppUpdate tabs (rail tags →
  PlaceholderPage), and the six orphaned Customize sub-nav tags (Appearance / ContextMenu /
  Desktop / Explorer / StartMenu / Taskbar). The rebuild is not complete after this wave.

---

## MVVM Phase 10 — AkariOS tab (SHOW ONLY)

Recon of net8's AkariOS tab. **Nothing created or edited in build #3; build #2 read-only;
no Defender code touched.** All bodies read from source this phase. Defender: **two UI-copy
strings only** (the protected preset copy) — no Defender code, flagged in §5, nothing to
gate, but see the repositioning flag in §7.

### 0. Which build is which (confirmed, not assumed)

- **Build #2 = `C:\Users\isleap\Documents\GitHub\Akari-Tool`** — WinUI 3 factory build
  (`AkariTool.csproj`: `UseWinUI=true`, `net8.0-windows10.0.19041.0`, WinAppSDK
  `1.8.260710003`). This is the READ-ONLY net8 source reconned here. (Its working-dir
  CLAUDE.md still describes the WPF original and is stale on this point — the repo is the
  WinUI-3 factory build; git branch `winui3-migration`.)
- The pure-WPF original lives separately at `Akari-Tool-WPF+UI` (WPF-UI 4.1.0). Not used.
- **In build #2 the AkariOS tab is LIVE**, not dead: `MainWindow.xaml.cs:243` `_akariOS = new
  AkariOSTab(); Init("AkariOS", _akariOS);`, rail item `MainWindow.xaml:111`.

### 1. Files (build #2 — `Tabs/AkariOS/`, 9 files, 2633 lines)

`BaseTab` shell, one continuous page of collapsible cards. `NavTag/NavLabel = "AkariOS"`.
- `AkariOSTab.xaml` (10) / `.xaml.cs` (61) — shell + `Build()` order:
  `BuildPostInstallBanner` → `BuildGamingTweaksCard` → `BuildUtilitiesPanel` →
  `BuildUsefulToolsCard`. Plus shared `RunShellProcess` (shell-execute helper).
- `AkariOSTab.PostInstall.cs` (137) — the C:\PostInstall detect/download banner.
- `AkariOSTab.GamingTweaksCard.cs` (446) — the unified card (Service Preset + Playbook + BCD),
  the `BuildSectionCard` collapsible factory, and the Gaming Tweaks toggle rows.
- `AkariOSTab.Services.cs` (209) — Service Preset / Playbook / BCD section bodies + preset-label
  drift rendering.
- `AkariOSTab.Competitive.cs` (1004) — Competitive Mode (session-scoped game launcher).
- `AkariOSTab.ShaderCache.cs` (311) — Shader Cache Cleaner (scan + clean).
- `AkariOSTab.GpuTools.cs` (293) — NVIDIA + AMD button grids, NVIDIA "Miscellaneous" reg
  bundle, AMD shader-cache segmented control.
- `AkariOSTab.Account.cs` (162) — Utilities: Account (display name), Interface (Alt-Tab),
  System (Resync Time, DMA Remapping).

### 2. TweakDefinition-backed vs bespoke — **ENTIRELY BESPOKE**

**Not a single `TweakDefinition` in the whole tab. It registers NOTHING with
`TweakRegistry`.** Competitive.cs:20-24 says so explicitly ("nothing here is a
TweakDefinition, nothing is registered … excluded from Quick Actions and drift
verification"). Confirmed by grep: no `TweakDefinition` construction, no `Register`/`Mark`/
`ClaimRange`, no `AddItem`/`AddSectionTitle` (all `TweakHelpers.*` hits are **styling
tokens** — `CardBg`, `TextPrimary`, `WarnFg`, `Token("…")` — plus the collapse-state
`SaveState`/`HasState`/`ClearState` marker helpers, NOT tweak registration).

⇒ Like Software / Backup / Advanced Tools / Home-About-Tools, this is a **bespoke,
non-catalog tab.** Porting it leaves the `[WARMUP]` total at **439** — no Ids to preserve
because it defines none. (The Id-preservation hard constraint simply doesn't bite here.)

### 3. Data model + backing services — LOGIC LAYER ALREADY PRESENT IN BUILD #3

Every section is a thin UI wrapper over a static service. Verified each backing service
already exists in build #3 (only the UI is missing):

| Section | Backing call(s) | In build #3? |
|---|---|---|
| PostInstall banner | `PostInstallService.IsFullyInstalled/LocalRoot/EnsurePostInstallAsync` | ✅ `Tabs/Shared/PostInstallService.cs` |
| Service Preset | `ServicesPreset.ApplyAkariGaming/ApplyAkariDaily/ApplyStockDefault/ReadPresetStamp`, `SystemStateReader.DetectServicePresetDetailed` | ✅ `Tabs/Shared/ServicesPreset.AkariOs.cs`, `Tabs/Shared/SystemStateReader.cs` |
| Playbook | `RestorePointHelper.EnsureRestorePointAsync`, `PlaybookTweaks.ApplyAllAsync/UndoAllAsync` | ✅ `Tabs/Shared/RestorePointHelper.cs`, `PlaybookTweaks.*` |
| BCD | `BcdBackup.BackupAsync/ApplyAsync/RestoreAsync`, `BcdTweakOptions` | ✅ `Tabs/Shared/BcdBackup.cs` |
| Gaming Tweaks | `SetPreemption` (inline `Registry.SetValue`), `GpuTweaks.SetHdcpDisabled`, embedded `Scripts/Network/network-{apply,revert}.bat` via `Service.RunProcess`, `SystemStateReader.ReadPreemption/ReadHdcp` | ✅ `GpuTweaks.cs` (bat resources need port-check) |
| Competitive Mode | `CompetitiveService` (12 refs), `GameDetection`/`DetectedGame`, `CompetitivePrefs`, `FilePickers.OpenFileAsync(".exe")` | ✅ `CompetitiveService.cs`, `GameDetection.cs`, `CompetitivePrefs.cs` |
| Shader Cache | `ShaderCacheService.ScanAsync/CleanAsync`, `ShaderCacheRow/State/ScanResult/Target` | ✅ `ShaderCacheService.cs` |
| NVIDIA/AMD | `NvidiaProfileService.ApplyAkariProfileAsync`, `GpuTweaks.SetPState0/DisableEcc/…/ApplyAmdDwords`, `ToolFetchService.LaunchAsync`, `ElevationService.RunAsSystem` reg bundle | ✅ `NvidiaProfileService.cs`, `GpuTweaks.cs`, `ToolFetchService.cs` |
| Account/Interface/System | `AccountService.GetDisplayName/SetDisplayName`, `SystemUtilities.SetAltTabClassic/ResyncTimeAsync/SetDmaRemapping` | ✅ `AccountService.cs`, `SystemUtilities.cs` |

**Only genuinely-missing dependencies:** `AkariDialogs` (net8's ContentDialog helper — build
#3 uses `IDialogService`/`ContentDialog`; a mechanical swap, 10 refs) and `FilePickers` (→
the app-local `IFileService`/`AkariFileService`, §4). So, exactly like the prior bespoke
waves: **logic present, UI-only to port** — but this is by far the largest and most
system-invasive UI (2633 net8 lines, heavily imperative like Advanced Tools ⇒ likely a
code-behind Page, not DataTemplates).

### 4. File I/O + elevation-sensitive operations

- **File picker — ONE, and it's the elevation-sensitive kind:** Competitive Mode
  `BrowseForGameAsync` → `FilePickers.OpenFileAsync(".exe")` (`Competitive.cs:278`), to pick a
  game exe. **MUST route through the existing `IFileService.PickSingleFileAsync`
  (`AkariFileService`)** per the Phase 4 elevation lesson — never a raw WinRT picker (would
  throw `COMException 0x80004005` under the app's `requireAdministrator` launch). This is the
  only in-tab picker.
- **SYSTEM-elevation writes** (`ElevationService.RunAsSystem`): NVIDIA "Miscellaneous" reg
  bundle (`GpuTools.cs:38`, ~35 HKLM GraphicsDrivers/nvlddmkm values) and AMD shader-cache
  registry write (`GpuTools.cs:131`). Per-thread impersonation caveat applies (CLAUDE.md
  landmine) — the impersonated action is self-contained, preserve that.
- **Direct HKLM registry writes** (no explicit SYSTEM wrapper): `SetPreemption`
  (`GamingTweaksCard.cs:261-276`, nvlddmkm preemption values). HKCU write: Competitive
  disclaimer flag (`Registry.SetValue HKCU\Software\AkariTool "CompetitiveDisclaimerAccepted"`).
- **Embedded script extraction + reboot:** Network Optimization extracts embedded
  `Scripts/Network/network-{apply,revert}.bat` to `%TEMP%`, runs via `Service.RunProcess`,
  and **the batch reboots the machine ~1s after exit** — gated behind
  `ConfirmNetworkRebootAsync` ("This will restart your PC"). **Port-check:** confirm those two
  `.bat` are embedded in build #3's csproj (`Scripts/Network/*`); not yet verified this phase.
- **Destructive file deletes:** Shader Cache "Clean Now" → `ShaderCacheService.CleanAsync`
  deletes shader-cache files, **gated behind `ConfirmShaderCleanAsync`** (size-summary dialog).
- **Network download:** PostInstall banner "Download PostInstall" → `EnsurePostInstallAsync`
  (~30 MB from GitHub to `C:\PostInstall`). No file picker.
- **Restore point:** Playbook "Apply All" creates a system restore point first.
- **Shell-execute launches:** AMD "Driver Download" opens `https://www.amd.com/…/drivers.html`
  via `RunShellProcess`; NVCleanstall/RSS/DisableDx11Navi via `ToolFetchService.LaunchAsync`.
- **Confirmation dialogs DO exist here** (unlike Debloat/Advanced): network reboot, shader
  clean, and the one-time Competitive experimental disclaimer — all via `AkariDialogs`,
  which must map to `IDialogService`/`ContentDialog` in the port.

### 5. Defender — TWO UI-COPY STRINGS ONLY (no code; flagged per the standing rule)

`grep -i defender` over `Tabs/AkariOS/` → **exactly two hits, both preset UI copy:**
- `Services.cs:31` — "… Defender is always protected."
- `GamingTweaksCard.cs:58` — "… Defender and boot-critical services are never touched."

**No `DefenderService`, no `RunPhase2Native`, no `NoDefender`/`DisableDefender.ps1`, no
Defender registry/service code anywhere in the tab.** These are the exact
service-preset-copy strings CLAUDE.md marks as *must-stay-accurate and fine to keep* (the
preset path deliberately never touches Defender). **Per the standing rule I am flagging and
stopping short of any change** — when this tab is ported, that copy must be carried
**verbatim** and I will not alter or "improve" it without an explicit ask. No Defender
*code* is implicated, so there is no code path to gate; this is a copy-preservation note, not
a destructive-Defender finding.

### 6. Build #3 current state

- **Rail tag EXISTS** (`MainWindow.xaml`, "AkariOS"); **NOT in `PageMap` → `PlaceholderPage`.**
- **No AkariOS UI** in build #3 (no `Tabs/AkariOS`, no `Views/AkariOS*`, no
  `ViewModels/AkariOS*`).
- **Logic layer fully present** (table in §3); only `AkariDialogs` + `FilePickers` need the
  mechanical `IDialogService`/`IFileService` swaps.

### 7. ⚠ Flag for isleap BEFORE any extraction (decision, not mine to make)

Both CLAUDE.md files state the app is **"repositioned as a fully standalone product targeting
stock Windows 11; the AkariOS Playbook has been retired as a dependency."** Yet this entire
tab is **AkariOS-specific**: a `C:\PostInstall` GitHub download, "AkariOS Playbook Tweaks,"
service presets described in AkariOS terms, "Useful on AkariOS where w32time is disabled."
So there is a real question the recon cannot answer:

1. **Port the AkariOS tab as-is** (faithful, verbatim), or
2. **Port only the OS-agnostic subset** (Gaming Tweaks, Shader Cache, NVIDIA/AMD, Competitive,
   Account/Interface/System) and **drop the AkariOS-Playbook / PostInstall-dependent pieces**
   consistent with the standalone repositioning, or
3. **Skip AkariOS entirely** and move to Verify / AppUpdate.

This is exactly the "don't assume the framing" gate. **Awaiting isleap's pick — no
extraction, no UI, no MainWindow wiring until then.**

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Build #3** — recon only; no files created or edited except this log entry. No backing
  service touched.
- **Defender** — two preset-copy strings flagged (§5); no code involved; nothing changed.
- No `AkariOSViewModel`/`AkariOSPage`, no MainWindow wiring, no `FilePickers`→`IFileService`
  swap, no `AkariDialogs`→`IDialogService` swap — all awaiting the §7 decision.

---

## MVVM Phase 11 — AkariOS ▸ PostInstall banner + page scaffold — **COMPLETE (VM sign-off pending)**

**Decision:** isleap chose **Option 1 — port the AkariOS tab as-is, verbatim.** AkariOS is
the product's primary differentiator; the "standalone / Playbook retired as a dependency"
language in both CLAUDE.md files was stale and was corrected (not followed). First
sub-section extracted: the lowest-risk **PostInstall banner** + the page skeleton. Build #2
read-only; **no Defender code touched.** Registers NOTHING — `[WARMUP]` stays 439.

### Pre-extraction (done first, per isleap)

1. **Both CLAUDE.md "What this is" paragraphs rewritten** to state AkariOS is a first-class,
   actively-ported tab and the primary differentiator (stock Windows still supported via
   graceful degradation). Exact prior wording quoted to isleap before editing. Build #3's
   separate "MVVM rebuild status" list (which already names AkariOS among remaining tabs) was
   left intact.
2. **Defender preset-copy strings re-verified accurate** under Option-1 framing: presets carry
   an explicit `_defenderServices` guard (`ServicesPreset.cs:25-30`: WdBoot, WdFilter,
   WdNisDrv, WdNisSvc, WinDefend, SecurityHealthService, Sense, WdmCompanionFilter) and
   `Apply()` skips any member (`:121`, `:219`) — identical in both builds. (`mpssvc` = Windows
   **Firewall**, set to Auto `2`, not a Defender-AV change.) No copy edit; **no Defender code
   or copy touched.**
3. **Network `.bat` embedded-resource port-check (for the later Gaming Tweaks section) — PASS.**
   Build #3 has `Scripts/Network/network-apply.bat` + `network-revert.bat` on disk AND the
   csproj glob `<EmbeddedResource Include="Scripts\Network\*.bat" />` (`AkariTool.csproj:69`).
   Root namespace `AkariTool` is unchanged, so net8's `.Scripts.Network.{file}` suffix lookup
   holds. (Not consumed this phase — recorded ahead of Gaming Tweaks per isleap.)

### New / edited files (this sub-section only)

- **`ViewModels/AkariOS/AkariOSViewModel.cs`** (new) — thin bespoke VM; ctor injects
  `ToolService`, exposes it as `Tool`. **Deliberately NOT registered under
  `TweakPageViewModel`** (registers nothing → out of the warm-up enumeration).
- **`Views/AkariOSPage.xaml`** (new) — scroll host + page header ("AkariOS" / "AkariOS presets
  and system configuration.") + `RootPanel` StackPanel the code-behind fills. Mirrors the
  Advanced Tools code-behind-Page shell.
- **`Views/AkariOSPage.xaml.cs`** (new) — `Build()` preserves net8's four-call order:
  `BuildPostInstallBanner` (**ported near line-for-line**) + three **visible stub cards** for
  the unported calls. Token→ThemeResource mapping (per prior waves): `CardBg`→
  `CardBackgroundFillColorDefaultBrush`, `Hairline`→`CardStrokeColorDefaultBrush`, `SuccessBg`→
  `SystemFillColorSuccessBackgroundBrush`, `SuccessBorder`/`SuccessFg`→`SystemFillColorSuccessBrush`,
  `AccentText` (the ⚠ not-found header)→`SystemFillColorCautionBrush`, `TextSecondary`→
  `TextFillColorSecondaryBrush`, `CardRadius 8`→`new CornerRadius(8)`. **No `AkariDialogs` /
  `FilePickers` in this section**, so neither mechanical swap was needed yet.
- **`App.xaml.cs`** — `AddSingleton<…AkariOS.AkariOSViewModel>()`, beside the other bespoke
  non-catalog VMs (Backup/Advanced), NOT in the `TweakPageViewModel` block.
- **`MainWindow.xaml.cs`** — `PageMap["AkariOS"] = typeof(AkariOSPage)` + `AkariOSPage =>
  "AkariOS"` in `SyncSelectedItem` (rail sync). Replaces the prior PlaceholderPage
  fall-through. (Rail item already existed in `MainWindow.xaml`.)

### ⚠ Honest note — "four stubs" is THREE (net8 `Build()` has four calls total)

The scope said "the other four `Build()` calls left as stubs." net8 `AkariOSTab.Build()` makes
**four** calls total — `BuildPostInstallBanner` + `BuildGamingTweaksCard` +
`BuildUtilitiesPanel` + `BuildUsefulToolsCard`. PostInstall is now real, leaving **three**
stub cards (Gaming Tweaks / Utilities / Useful Tools). Each stub card is visibly labeled
"🔒 {name} — not yet ported" and lists the sub-sections it will contain (Gaming Tweaks lists
Service Preset, Playbook, BCD, Competitive Mode, Shader Cache, Gaming Tweaks, NVIDIA, AMD).
Nothing was silently dropped — the page composition equals net8's, one real + three stub.

### Build (VS MSBuild, literal)

First attempt failed — `PostInstallService` lives in namespace `AkariTool.Tabs`, missing
`using`. Added `using AkariTool.Tabs;`. Second attempt, clean:
```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors. De-elevated (`asInvoker`) copy built the same via
`build-deelevated.ps1` (isolated `obj\DeElevated\`).

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP]   Gaming [0..130) … Power [403..439) 36 rows
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```
`AkariOSViewModel` registers nothing and is out of the enumeration — total holds.

### Verification (de-elevated, launched + UIAutomation by window handle)

Launched `bin\DeElevated\AkariTool.exe` (Medium IL), attached UIA to the "Akari Tool" window,
selected the **AkariOS** rail item, read the page:
- **Page loads** — header "AkariOS" + "AkariOS presets and system configuration.".
- **PostInstall "not found" state rendered** (correct — `C:\PostInstall` is absent on this
  desktop, so `PostInstallService.IsFullyInstalled == false`): "⚠  PostInstall folder not
  found", the "…require C:\PostInstall / Click Download … (~30 MB)" body, and the **Download
  PostInstall** button present.
- **Three stub cards** all present and labeled "🔒 … — not yet ported" with their contents.
- No `[ERROR]`/`Unhandled` in the log for the run.
- **Not driven:** (a) the **"installed" success state** — code-symmetric on the `installed`
  bool but not exercisable without a real full `C:\PostInstall` on disk; (b) the **Download**
  action — deliberately NOT clicked (it performs a real ~30 MB GitHub download + creates
  `C:\PostInstall`). Both are isleap's VM steps.

### VM checklist (Phase 11 — for isleap)

- [ ] Elevated launch: AkariOS rail item opens the real page (not PlaceholderPage).
- [ ] With `C:\PostInstall` present → banner shows the green "✓ PostInstall folder detected"
      state (the branch automation couldn't exercise here).
- [ ] "Download PostInstall" → fetches to `C:\PostInstall`, banner flips to the success state
      (or "Retry Download" on failure).
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no code/copy touched; preset copy re-verified accurate (§ pre-extraction 2).
- **NOT extracted (awaiting per-section sign-off):** Service Preset, Playbook, BCD, Gaming
  Tweaks toggles, Competitive Mode, Shader Cache, NVIDIA/AMD, Account/Interface/System — all
  currently the three stub cards.
- `AkariDialogs`→`IDialogService` and `FilePickers`→`IFileService` swaps not needed yet (no
  dialogs/pickers in PostInstall); they land with the sections that use them (first picker:
  Competitive game-exe browse; first dialogs: network-reboot / shader-clean / Competitive
  disclaimer).

---

## MVVM Phase 12 — AkariOS ▸ Service Preset (Option A standalone card) — **COMPLETE (VM sign-off pending; buttons NOT driven — see below)**

Second AkariOS sub-section. **isleap picked Option A** (recon Phase 11 §3): Service Preset
rendered as its own standalone bordered card above the Gaming Tweaks stub; the presets-card
container + preset warning banner (the 2nd Defender string) **deferred** until Playbook + BCD
land. Build #2 read-only; **no Defender code touched**; registers nothing → `[WARMUP]` = 439.
**One file changed** (`Views/AkariOSPage.xaml.cs`), exactly as scoped — no App/MainWindow edits.

### Source ported (net8 `Tabs/AkariOS/AkariOSTab.Services.cs` lines 17–111)

`BuildServicePresetSection` + `SyncPresetLabel` + `ApplyPresetLabel` + the `_servicePresetLabel`
field — near line-for-line. Wrapped in a new `BuildServicePresetCard` (the Option-A card: net8's
presets Border had no padding, so the section grid keeps its 20/18 insets — identical layout).
Three buttons: **Apply Gaming** → `ServicesPreset.ApplyAkariGaming(Service!)`, **Apply Daily**
→ `ApplyAkariDaily`, **Restore Stock** → `ApplyStockDefault`, each `+ SyncPresetLabel()`. The
label reads `SystemStateReader.DetectServicePresetDetailed()` + `ServicesPreset.ReadPresetStamp()`
and renders Current: … with a per-service drift tooltip. All machine-wide writes stay inside the
already-ported `ServicesPreset` — this section only calls it + renders.

### Defender copy — ported byte-for-byte, unedited

The line-31 paragraph ends verbatim: *"…Daily keeps the same optimizations but leaves Windows
Update and ISO mounting working. **Defender is always protected.**"* No rewording, no cleanup —
UIA read it back identically (below). The 2nd string ("Defender and boot-critical services are
never touched") is in the warning banner, still deferred with the presets container.

### Token→ThemeResource mapping (per prior waves; two literal tints via new `Hex()`)

`TextPrimary`→`TextFillColorPrimaryBrush`, `TextSecondary`→`TextFillColorSecondaryBrush`,
`SuccessFg`→`SystemFillColorSuccessBrush`, `WarnFg`→`SystemFillColorCautionBrush`. Two net8
tints kept as literals so they stay distinct from the crimson accent: `InfoFg`→`Hex("#4CC2FF")`
(Daily blue), `Accent`→`Hex("#E0142A")` (brand crimson). `Hex()` helper added, matching
`AdvancedToolsPage`'s. Cosmetic-only, theme-awareness of those two tints deferred to the cosmetic
pass (same deviation logged for every prior wave).

### Backing services — match Phase 10/11 recon (one cosmetic naming note)

`ServicesPreset.ApplyAkariGaming/ApplyAkariDaily/ApplyStockDefault(ToolService, bool=false)`,
`ReadPresetStamp():string?`; `SystemStateReader.DetectServicePresetDetailed():ServicePresetResult
(Preset, Matched, Total, Drift)` with `enum ServicePreset {Stock,AkariGaming,AkariDaily,Mixed,
Unknown}`. Every field the port reads is present; the record's `Matched` field is unused by
`ApplyPresetLabel` (net8 reads `.Total`). No functional drift.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same via
`build-deelevated.ps1`.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated, launched + UIAutomation by handle)

- **Service Preset card renders** — title "Service Preset", the Defender paragraph **verbatim**,
  and the three buttons **Apply Gaming / Apply Daily / Restore Stock** all present.
- **Current-preset label read path PROVEN LIVE** — the label rendered
  **"Current: Daily (stock Windows)"**, i.e. `DetectServicePresetDetailed()` returned `AkariDaily`
  and `ReadPresetStamp()` returned the stock `"Daily"` stamp, and `stampAgrees`+the stamp switch
  produced the "(stock Windows)" wording in the Daily-blue tint. This exercises the entire
  `ApplyPresetLabel` read chain at build time (it runs on section build, not on click).
- **Stub contents line updated** — the Gaming Tweaks stub now reads "Playbook, BCD, Competitive
  Mode, Shader Cache Cleaner, Gaming Tweaks, NVIDIA, AMD" (**Service Preset removed**); Utilities
  + Useful Tools stubs unchanged.
- No `[ERROR]`/`Unhandled` in the run.

### ⚠ Buttons NOT driven — deliberate (destructive live-service writes)

**I did NOT click Apply Gaming / Apply Daily / Restore Stock.** They mutate live Windows service
startup types machine-wide. "Restore Stock as a reset" assumes this desktop began at stock — its
current detected state is **AkariOS/stock "Daily"**, not confirmed-stock, so a Restore Stock is
not a safe no-op reset here, and I had no explicit go-ahead to change this machine's services.
The **apply/restore click paths are therefore verified statically only** (byte-for-byte port of
net8's handlers over the byte-identical `ServicesPreset`), not driven. The button *rendering* and
the label *read path* were driven for real (above). Actuating the three presets on a disposable
elevated VM is isleap's step.

### VM checklist (Phase 12 — for isleap; disposable VM for the apply paths)

- [ ] AkariOS ▸ Service Preset card shows title + the Defender paragraph + current-preset label.
- [ ] **Apply Gaming** → services rewrite to the Gaming preset; label flips to "Current: … Gaming".
- [ ] **Apply Daily** → label flips to "Current: … Daily".
- [ ] **Restore Stock** → label flips to "Current: Windows Stock". (Run last as the reset.)
- [ ] Hover the label under a Mixed state → per-service drift tooltip ("svc: actual -> expected").
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — line-31 copy ported verbatim; no Defender code referenced; warning-banner 2nd
  string still deferred.
- **NOT extracted (await sign-off):** Playbook, BCD, the preset warning banner + presets
  container, Gaming Tweaks toggles, Competitive Mode, Shader Cache, NVIDIA/AMD, Account/
  Interface/System — all still the three stub cards.
- **Live preset apply/restore** — not driven (destructive); isleap's disposable-VM step.

---

## MVVM Phase 13 — AkariOS ▸ Playbook (Option A standalone card) — **COMPLETE (VM sign-off pending; Apply/Undo NOT driven)**

Third AkariOS sub-section, second of the presets trio. Option A: own standalone bordered card
between Service Preset and the Gaming Tweaks stub, preserving net8's order (Service Preset →
Playbook → BCD → …). Build #2 read-only; **no Defender code touched**; registers nothing →
`[WARMUP]` = 439. **One file changed** (`Views/AkariOSPage.xaml.cs`) — no App/MainWindow edits.

### Source ported (net8 `AkariOSTab.Services.cs` `BuildPlaybookSection`, lines 115–162)

Near line-for-line, wrapped in `BuildPlaybookCard` (Option-A card: no padding, section grid
keeps 20/18 insets). Title "AkariOS Playbook Tweaks", the descriptive paragraph, the amber ⓘ
caveat line, and two buttons: **Apply All** → logs `[RESTORE] Creating system restore point...`
→ `RestorePointHelper.EnsureRestorePointAsync(Service!)` → logs result → `PlaybookTweaks.
ApplyAllAsync(Service!)` (IsEnabled toggled in try/finally); **Undo All** → `PlaybookTweaks.
UndoAllAsync(Service!)`. Stateless UI — **no status label** (no read path, unlike Service
Preset). All restore-point + tweak writes stay inside the already-ported services.

### ⚠ NO CONFIRMATION DIALOG — verbatim net8 behavior (isleap Phase 13 decision)

Confirmed no dialog in the net8 section **and** none inside build #3's `PlaybookTweaks*.cs` /
`RestorePointHelper.cs`. Apply All is destructive and **partly irreversible** (the ⓘ line:
memory compression + DISM changes are NOT restore-point-recoverable) yet fires on a single
unguarded click — the same class as the Debloat tab. **isleap's decision: carry the no-dialog
behavior forward verbatim; a confirm-on-Apply, if ever wanted, is its own explicit change, not
this migration step.** No dialog was added. Flagged in the code comment too.

### Backing services — match Phase 10 recon, no drift

`RestorePointHelper.EnsureRestorePointAsync(ToolService, string = "Akari Tool - Pre-Tweak
Backup"):Task<bool>` (net8's `(Service!)` binds the default description); `PlaybookTweaks.
ApplyAllAsync(ToolService):Task` / `UndoAllAsync(ToolService):Task`. All present, compatible.

### Token→ThemeResource mapping

`TextPrimary`→`TextFillColorPrimaryBrush`, `TextSecondary`→`TextFillColorSecondaryBrush`,
`WarnFg` (the ⓘ line)→`SystemFillColorCautionBrush`. All already in use — **no new `Hex()`
tints, no new fields, no status label, no dialog/picker swaps.**

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated, launched + UIAutomation by handle)

- **Playbook card renders** — title "AkariOS Playbook Tweaks", the descriptive paragraph
  **verbatim** (UIA: "Applies 30 registry tweaks, 15 ETW autologger disables, 12 IFEO process
  priorities, 66 scheduled task disables…"), the ⓘ caveat line **verbatim** ("Registry/task
  changes are restore-point-recoverable. Memory compression and DISM changes are not."), and
  both buttons **Apply All / Undo All** present.
- **Stub contents updated** — Gaming Tweaks stub now reads "BCD, Competitive Mode, Shader Cache
  Cleaner, Gaming Tweaks, NVIDIA, AMD" (**Playbook removed**); Service Preset card + its "Current:
  Daily (stock Windows)" label still render above; Utilities + Useful Tools stubs unchanged.
- No `[ERROR]`/`Unhandled` in the run.

### ⚠ Apply All / Undo All NOT driven — deliberate (VM-checklist territory)

**I did NOT click Apply All or Undo All.** Apply All creates a system restore point and applies
30 registry tweaks + 15 ETW autologger disables + 12 IFEO priorities + 66 task disables +
filesystem/telemetry/memory-compression + DISM changes to the live machine (partly irreversible)
— not dev-desktop territory. There is no read path/status label to drive here, so the drivable
surface is **render-only** (done, above); the **Apply/Undo click paths are verified statically
only** (byte-for-byte port over the byte-identical `RestorePointHelper`/`PlaybookTweaks`).
Actuation is isleap's disposable-VM step.

### VM checklist (Phase 13 — for isleap; disposable VM only)

- [ ] AkariOS ▸ Playbook card shows title + paragraph + ⓘ caveat + Apply All / Undo All.
- [ ] **Apply All** → log shows `[RESTORE] Creating system restore point...` then the ✓/⚠ result,
      then the playbook applies; verify a restore point was actually created.
- [ ] **Undo All** → the playbook reverts (note: the recon's known net8 caveat that some changes,
      memory compression + DISM, are not fully reversible).
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — not referenced; nothing changed.
- **NOT extracted (await sign-off):** BCD, the preset warning banner + presets container,
  Gaming Tweaks toggles, Competitive Mode, Shader Cache, NVIDIA/AMD, Account/Interface/System.
- **Live Apply/Undo** — not driven (destructive + partly irreversible); isleap's disposable-VM step.

---

## MVVM Phase 14 — AkariOS ▸ BCD (Option A standalone card) — **COMPLETE (VM sign-off pending; Apply/Restore NOT driven)**

Fourth AkariOS sub-section, **completes the presets trio** (Service Preset → Playbook → BCD).
Option A: own standalone bordered card between Playbook and the Gaming Tweaks stub. Build #2
read-only; **no Defender code touched**; registers nothing → `[WARMUP]` = 439. **One file
changed** (`Views/AkariOSPage.xaml.cs`) — no App/MainWindow edits.

### Source ported (net8 `AkariOSTab.Services.cs` `BuildBcdSection`, lines 166–207)

Near line-for-line, wrapped in `BuildBcdCard` (Option-A card; section grid keeps 20/18 insets).
Title "BCD Tweaks", the descriptive paragraph, the ⚠ caveat line, and two buttons: **Apply** →
`BcdBackup.BackupAsync(Service!)` then `BcdBackup.ApplyAsync(Service!, new BcdTweakOptions())`;
**Restore** → `BcdBackup.RestoreAsync(Service!)`. Stateless UI — **no status label**. net8
constructs `new BcdTweakOptions()` with no args (all four tweaks default `true`); the UI exposes
**no per-toggle controls**, preserved exactly.

### `BcdBackup` semantics — confirmed, no drift

- Registry-backed undo: backup at `HKCU\Software\AkariTool\BcdBackup` (`BackupKeyPath`).
  `BackupAsync` captures current `bcdedit` values there **before** any write; `RestoreAsync`
  replays them (stock-default fallback if the key is absent).
- `ApplyAsync(ToolService, BcdTweakOptions)` gates four actions on the options: `LegacyBootMenu`
  → `bcdedit /set bootmenupolicy Legacy`; `DisableDynamicTick` → `/set disabledynamictick yes`;
  `DisableRecovery` → `/set {current} recoveryenabled no`; `DisableHibernation` → `powercfg -h
  off`. `BcdTweakOptions` = plain class, four `bool` props all default `true`. All signatures
  (`BackupAsync`/`ApplyAsync`/`RestoreAsync` : `Task`) match net8's calls. No dialog inside the
  service. No drift from Phase 10.

### Caveat line — ported byte-for-byte (net8 line 184)

"⚠  BCD changes are NOT covered by System Restore — values are backed up internally for undo."
UIA read it back identically.

### ⚠ NO CONFIRMATION DIALOG — verbatim net8 (matches Service Preset / Playbook)

Apply runs `bcdedit` + `powercfg -h off` against live boot config on a single click; no gate in
net8 or in `BcdBackup`. Carried forward verbatim per the standing isleap decision — none added.

### Token→ThemeResource mapping + the AccentText tint decision (isleap Phase 14)

`TextPrimary`→`TextFillColorPrimaryBrush`, `TextSecondary`→`TextFillColorSecondaryBrush`.
The ⚠ caveat uses net8's **`AccentText`** token (distinct from `Accent`): isleap chose to keep
them visually distinct rather than consolidate onto the crimson. **Flagged limitation:** the
page's `Hex()` helper is single-value, and a theme-aware two-value brush would need managed-brush
machinery or an `App.xaml` ThemeResource (out of this one-file scope). Per isleap's instruction,
**defaulted to the dark value alone `Hex("#FF8A94")`**; the light-theme `#B01020` is deferred to
the cosmetic pass (same as every prior wave). Kept distinct from Playbook's amber
`SystemFillColorCautionBrush` ⓘ.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated, launched + UIAutomation by handle)

- **BCD card renders** — title "BCD Tweaks", the descriptive paragraph **verbatim** ("Legacy boot
  menu (F8), disable dynamic tick … Current values are backed up to registry before applying."),
  the ⚠ caveat line **verbatim**, and both buttons **Apply / Restore** present.
- **Presets trio + order intact** — Service Preset → Playbook Tweaks → BCD Tweaks render in
  sequence above the Gaming Tweaks stub.
- **Stub contents updated** — Gaming Tweaks stub now reads "Competitive Mode, Shader Cache
  Cleaner, Gaming Tweaks, NVIDIA, AMD" (**BCD removed**).
- No `[ERROR]`/`Unhandled` in the run.

### ⚠ Apply / Restore NOT driven — deliberate (VM-checklist territory)

**Not clicked.** Apply issues `bcdedit` boot-config writes + `powercfg -h off` (removes
hiberfil.sys) on the live machine; Restore replays the registry backup. No read path/status to
drive, so the drivable surface is **render-only** (done); the **Apply/Restore click paths are
verified statically only** (byte-for-byte port over the byte-identical `BcdBackup`). Actuation is
isleap's disposable-VM step.

### VM checklist (Phase 14 — for isleap; disposable VM only)

- [ ] AkariOS ▸ BCD card shows title + paragraph + ⚠ caveat + Apply / Restore.
- [ ] **Apply** → log shows `[BCD] Applying BCD tweaks...` + per-tweak ✓/✗; a `HKCU\Software\
      AkariTool\BcdBackup` key is written first; `bcdedit /enum` reflects the changes; hiberfil
      removed.
- [ ] **Restore** → `[BCD] Restoring BCD values...` replays the backup (or stock fallback).
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — not referenced; nothing changed.
- **NEXT PICK (flagged, NOT started — needs its own sign-off):** reassemble the real **presets
  container** — one card wrapping Service Preset + Playbook + BCD under the shared **preset
  warning banner** (net8 `GamingTweaksCard.cs:39-65`), which carries the **deferred 2nd Defender
  string** ("Defender and boot-critical services are never touched"), ported verbatim then.
  Currently the three render as separate Option-A cards. **Awaiting confirmation before touching
  it.**
- **Still stubbed:** Competitive Mode, Shader Cache, Gaming Tweaks toggles, NVIDIA/AMD (Gaming
  Tweaks stub); Utilities; Useful Tools.
- **Live Apply/Restore** — not driven (destructive); isleap's disposable-VM step.

---

## MVVM Phase 15 — AkariOS ▸ presets container reassembly (+ 2nd Defender string) — **COMPLETE, fully driven (no NOT-driven flags)**

Closes out the presets trio. The three standalone Option-A cards (Service Preset / Playbook /
BCD) are collapsed into **one grouped card** under the shared preset **warning banner** —
mirroring net8's `BuildGamingTweaksCard` presets block. Build #2 read-only; **no Defender code
touched** (the banner's 2nd Defender string is presentation copy, ported verbatim). **Pure UI
restructure** — one file (`Views/AkariOSPage.xaml.cs`), registers nothing → `[WARMUP]` = 439.

### The change — structural wrap, NOT a rewrite

- **`Build()`** — the three calls `BuildServicePresetCard`/`BuildPlaybookCard`/`BuildBcdCard`
  replaced by a single **`BuildPresetsCard(RootPanel)`**.
- **New `BuildPresetsCard`** (ports net8 `GamingTweaksCard.cs:24-73`): outer card → inner
  StackPanel → the warning banner (both TextBlocks verbatim) → `BuildServicePresetSection(inner)`
  → hairline separator → `BuildPlaybookSection(inner)` → hairline separator →
  `BuildBcdSection(inner)`.
- **Removed** the three now-unused wrapper methods (`BuildServicePresetCard`/`BuildPlaybookCard`/
  `BuildBcdCard`). **The three `*Section` builders are UNTOUCHED** — only re-parented from their
  former standalone cards into `presetsInner`. Confirmed: Service Preset's label wiring
  (`_servicePresetLabel` field + `SyncPresetLabel`/`ApplyPresetLabel`) is self-contained and
  moved intact. (Also trued up the now-stale "own standalone card" comments on the Playbook/BCD
  section headers — comment-only.)

### 2nd Defender string — ported byte-for-byte (net8 `GamingTweaksCard.cs:58`)

Banner's 2nd TextBlock, verbatim: *"Gaming also disables Windows Update and Explorer ISO
mounting — choose Daily if you need either. **Defender and boot-critical services are never
touched.**"* Both Defender strings now present on the page (1st in Service Preset's paragraph,
2nd in the banner). Pure presentation — no Defender code referenced.

### Token→ThemeResource mapping (theme-aware Fluent, matches Phase 1 Backup banner)

`CardBg`→`CardBackgroundFillColorDefaultBrush`; `CardElevationBorder`→`CardStrokeColorDefaultBrush`
(flat stroke; elevation gradient + shadow deferred to cosmetic pass); `WarnBg`→
`SystemFillColorCautionBackgroundBrush`; `WarnBorder`/`WarnFg`→`SystemFillColorCautionBrush`;
`AkariOverlayStrong` separators→`DividerStrokeColorDefaultBrush`. Banner keeps net8's top-rounded
corners `(8,8,0,0)` + bottom-only 1px border + `20,10,20,10` padding.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors (both the functional build and the post-comment-cleanup rebuild).
De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation — FULLY DRIVEN, no NOT-driven flags)

UIA read the presets region in document order — exact sequence:
1. Banner line 1 — "⚠  Applies machine-wide changes to 166 service startup types. Restart required."
2. **Banner line 2 (2nd Defender string) — verbatim** "…Defender and boot-critical services are never touched."
3. "Service Preset"
4. Service Preset paragraph — **1st Defender string verbatim** "…Defender is always protected."
5. **"Current: Daily (stock Windows)"** — the live preset-label read path still works post-move.
6. "AkariOS Playbook Tweaks" + its paragraph.
7. "BCD Tweaks" + its ⚠ caveat.
8. "🔒 Gaming Tweaks — not yet ported" (the container ends before the stub).

All three sections render **inside the single grouped card in net8 order**, both Defender
strings verbatim, live label intact. No `[ERROR]`/`Unhandled`. **Nothing here was undrivable —
pure UI restructure, no destructive surface.**

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — 2nd string ported verbatim as presentation; no Defender code referenced.
- **The `*Section` builders** — unchanged; only re-parented.
- **PRESETS CARD NOW FULLY CLOSED OUT.** Still stubbed: Competitive Mode, Shader Cache, Gaming
  Tweaks toggles, NVIDIA/AMD (Gaming Tweaks stub); Utilities; Useful Tools — each its own
  future signed-off sub-section.

---

## MVVM Phase 16 — AkariOS ▸ Shader Cache Cleaner (+ first AkariDialogs→TweakDialogs swap) — **COMPLETE (Scan + dialog DRIVEN; Clean NOT driven)**

Fifth AkariOS sub-section — the smallest section carrying the **first dialog swap**, chosen to
validate that pattern on a contained surface before the bigger dialog/picker-carrying sections.
Card-wrapper **choice (1)**: plain Option-A card now; the collapsible `BuildSectionCard` factory
deferred to its own step for all five Gaming-card sections. Build #2 read-only; **no Defender
code touched**; registers nothing → `[WARMUP]` = 439. **One file** (`Views/AkariOSPage.xaml.cs`).

### Source ported (net8 `AkariOSTab.ShaderCache.cs`, lines 14–311)

`ShaderCacheRow` nested class + fields + `BuildShaderCacheContent`, `OnShaderCacheLoaded`,
`RunShaderScanAsync`, `RunShaderCleanAsync`, `ConfirmShaderCleanAsync`, `SetShaderBusy` — near
line-for-line, wrapped in `BuildShaderCacheCard` (plain card + a "Shader Cache Cleaner" title
header, standing in for net8's collapsible `BuildSectionCard`). Auto-scan deferred to `Loaded`.
Token map: `TextPrimary`→`TextFillColorPrimaryBrush`, `TextSecondary`→`TextFillColorSecondaryBrush`.

### The dialog swap (first of its kind) — `AkariDialogs` → `TweakDialogs`

`AkariDialogs` is absent in build #3; the app-local `TweakDialogs` (DI singleton) has a
same-named `ConfirmContentAsync` over the framework `IDialogService`, with `XamlRoot` wired at
startup (`MainWindow.xaml.cs:102`) and a fail-safe (returns false if XamlRoot unset). Mapping,
**arg order reversed** (net8 content-first → build-#3 title-first), copy verbatim:
```
// net8:      AkariDialogs.ConfirmContentAsync(new TextBlock{message}, "Clean Shader Caches", primaryText:"Clean")
// build #3:  _dialogs.ConfirmContentAsync("Clean Shader Caches", new TextBlock{message}, primaryText:"Clean")
```
`_dialogs` resolved via `ServiceLocator.GetService<TweakDialogs>()` in the page ctor — **no App/
MainWindow edits**. Confirmation copy (title "Clean Shader Caches", primary "Clean", close
"Cancel", the full dynamic body + the Steam-running addendum) ported byte-for-byte.

### Backing service — matches Phase 10, no drift

`ScanAsync(IEnumerable<ShaderCacheTarget>, CancellationToken=default)`, `CleanAsync(IEnumerable,
IProgress<string>?=null, CancellationToken=default)`, `GetTargets()`, `IsSteamInstalled()`,
`IsSteamRunning()`, `FormatBytes(long)`; records `ShaderCacheTarget(Id,DisplayName,Paths)` /
`ShaderCacheScanResult(TargetId,TotalBytes,FileCount,Exists)` /
`ShaderCacheCleanResult(TargetId,BytesFreed,FilesDeleted,FilesSkipped,Error)`. All fields read by
the port present. No drift.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation) — Scan + dialog DRIVEN, Clean NOT driven

- **Section renders** — "Shader Cache Cleaner" title + the description; checkbox rows per target;
  Rescan + Clean Now buttons; status line.
- **✅ Live Scan DRIVEN for real (read-only)** — the auto-scan produced real per-target sizes:
  `DirectX — 181,6 MB`, `NVIDIA — 8,4 GB`, `AMD — not found`, `Intel — not found`,
  `Steam — 200 KB`, and status `"8,6 GB of shader cache found."`. Rescan + Clean Now enabled
  post-scan. (Read-only enumeration; safe.)
- **✅ Confirm dialog DRIVEN — opened, copy verified, Cancelled safely.** Clicking Clean Now
  opened the dialog with the **verbatim** copy: "The following shader caches will be cleared:" +
  bulleted DirectX/NVIDIA/Steam + "About 8,6 GB will be freed. Games will rebuild their shaders
  on next launch." + **the Steam-running addendum** ("Steam is running. Close Steam and any games
  before cleaning to avoid errors.") — Steam is running on this desktop, so that branch rendered
  too. Both buttons present ("Clean", "Cancel"); **invoked strictly the button named exactly
  "Cancel"**, never "Clean". Post-Cancel the status was unchanged (`"8,6 GB of shader cache
  found."`, NOT "Freed…"), and the log shows **no** `Shader cache cleaned` line — confirming no
  delete occurred.
- **⚠ Clean/delete path NOT driven** — `CleanAsync` deletes real shader-cache files (8,6 GB
  here); verified statically only (byte-for-byte port over the byte-identical `ShaderCacheService`).
- No `[ERROR]`/`Unhandled` in the run.

### VM checklist (Phase 16 — for isleap; disposable VM for the actual clean)

- [ ] AkariOS ▸ Shader Cache card auto-scans; per-target sizes + total render.
- [ ] Rescan re-measures. Clean Now with a selection → dialog copy correct → **Clean** actually
      deletes; status shows "Freed … across N files"; a re-scan drops the sizes.
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — not referenced; nothing changed.
- **Collapsible `BuildSectionCard` factory** — deferred to its own step (all five Gaming-card
  sections at once), per isleap card-wrapper choice (1).
- **Still stubbed:** Competitive Mode, Gaming Tweaks toggles, NVIDIA/AMD (Gaming Tweaks stub);
  Utilities; Useful Tools.
- **Live Clean/delete** — not driven (destructive); isleap's disposable-VM step.

---

## MVVM Phase 17 — AkariOS ▸ Utilities (Account / Interface / System) — **COMPLETE (display-name read DRIVEN; all write-actions NOT driven)**

Sixth AkariOS sub-section — clears the **Utilities stub** entirely. Card-wrapper **choice (1)**:
three plain Option-A cards; collapsible factory still deferred. Build #2 read-only; **no
Defender code touched**; registers nothing → `[WARMUP]` = 439. **One file**
(`Views/AkariOSPage.xaml.cs`) — no App/MainWindow edits, no dialog/picker swaps.

### Source ported (net8 `AkariOSTab.Account.cs`, 162 lines)

`BuildUtilitiesPanel` → three cards; `BuildAccountContent` (Change Display Name TextBox + Apply),
`BuildInterfaceContent` (Alt-Tab Style: Classic/Immersive), `BuildSystemUtilContent` (Resync
System Time; DMA Remapping Enable/Disable), and the shared `AddActionRow` helper — all near
line-for-line. Wrapped by a new `BuildUtilityCard(title, contentBuilder)` (plain card + title
header, standing in for net8's collapsible `BuildSectionCard`). Token map:
`TextPrimary`→`TextFillColorPrimaryBrush`, `TextSecondary`→`TextFillColorSecondaryBrush`,
`AkariOverlayStrong` separator→`DividerStrokeColorDefaultBrush`.

**One faithful adaptation flagged:** net8's `AddActionRow` guards its inter-row separator with
`panel.Children.Count > 0` because net8's `BuildSectionCard` content panel started empty (title
lived in the separate card header). Here the title header shares the card's inner panel (child
0), so the guard is `> 1` — preserving net8's "separators only *between* rows, never above the
first" behavior. Purely structural; identical visual result.

### Backing services — match Phase 10, no drift

`AccountService.GetDisplayName():string?` / `SetDisplayName(string, Action<string>):bool`;
`SystemUtilities.SetAltTabClassic(bool, Action<string>)` / `ResyncTimeAsync(Action<string>):Task`
/ `SetDmaRemapping(bool, Action<string>)`. All in `AkariTool.Services`, signatures compatible.
No drift.

### Per-action effect classification (each individually)

- **GetDisplayName** (TextBox pre-fill) — **read-only; DRIVEN** (below).
- **Change Display Name → Apply** — **writes** the user's display name. **NOT driven.**
- **Alt-Tab Style (Classic/Immersive)** — writes registry + **restarts Explorer**. **NOT driven.**
- **Resync System Time → Resync Now** — confirmed NOT a read: `sc config w32time start=auto` →
  `net start w32time` → `w32tm /resync` (**adjusts the system clock**) → `net stop` →
  `sc config start=disabled`. Writes service config + system time. **NOT driven.**
- **DMA Remapping (Enable/Disable)** — writes HKLM `DmaRemappingCompatible` under every service
  subkey; restart required. **NOT driven.**

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation)

- **All three cards render** in order — **Account** (Change Display Name + description + TextBox +
  Apply), **Interface** (Alt-Tab Style + Classic/Immersive), **System** (Resync System Time +
  Resync Now; DMA Remapping + Enable/Disable). Useful Tools stub still follows. Utilities stub
  gone.
- **✅ Display-name read DRIVEN — correct against ground truth.** The TextBox was built from
  `AccountService.GetDisplayName()`; it came back **empty** with the placeholder "Enter a new
  display name" showing. Verified against ground truth: `net user isleap` Full Name is **empty**
  (`FullName=''`), so this account genuinely has no display name → `GetDisplayName()` returns
  null → empty box is the **correct** render (not a bug; a populated pre-fill simply can't be
  shown because there is none on this account). The read path executed without error.
- **Buttons present:** Apply (Account), Classic, Immersive, Resync Now, Enable, Disable. (A
  second "Apply" appeared in the UIA sweep but was **offscreen, rect=Empty** — a cached
  other-page element from `NavigationCacheMode=Required`, not a Utilities duplicate; the Account
  card has exactly one on-screen Apply.)
- No `[ERROR]`/`Unhandled` in the run.

### ⚠ Write-actions NOT driven — individually

Per the table above, **none** of Apply / Classic / Immersive / Resync Now / Enable / Disable
were clicked — each writes system state (display name / Explorer restart / service config +
system clock / HKLM registry). Verified statically only (byte-for-byte port over the
byte-identical `AccountService` / `SystemUtilities`). Actuation is isleap's step.

### VM checklist (Phase 17 — for isleap)

- [ ] Account: type a name → Apply → the account tile/lock-screen name updates; box re-reads it.
- [ ] Interface: Classic / Immersive → Alt-Tab switcher style changes (Explorer restarts).
- [ ] System: Resync Now → clock resyncs (log shows the w32tm steps, service left disabled).
- [ ] System: DMA Enable / Disable → `DmaRemappingCompatible` values flip (restart to apply).
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — not referenced; nothing changed.
- **Still stubbed:** Competitive Mode, Gaming Tweaks toggles, NVIDIA/AMD (Gaming Tweaks stub).
  Useful Tools stub remains. Collapsible `BuildSectionCard` factory still deferred.
- **All Utilities write-actions** — not driven; isleap's step.

---

## MVVM Phase 18 — AkariOS ▸ NVIDIA + AMD — **COMPLETE (AMD segmented READ driven; all writes/launches NOT driven)**

Seventh AkariOS sub-section — two of the five Gaming-card sections. Card-wrapper **choice (1)**:
two plain Option-A cards ("NVIDIA", "AMD"). Build #2 read-only; **no Defender code touched**;
registers nothing → `[WARMUP]` = 439. **One file** (`Views/AkariOSPage.xaml.cs`) — no App/
MainWindow/ViewModel edits, no dialog/picker swaps.

### Source ported (net8 `AkariOSTab.GpuTools.cs`)

`BuildNvidiaContent` (7-button grid) + `ApplyNvidiaMisc` (SYSTEM HKLM bundle); `BuildAmdContent`
(4-button grid) + the AMD shader-cache segmented control (`AmdShaderCacheKey`, `ShaderCacheState`,
`ReadAmdShaderCache`, `ApplyAmdShaderCache`, `BuildAmdShaderCacheControl`, `MakeSegment`); the
shared `AddButtonGrid` (3-col) and the `RunShellProcess` shell-execute helper (net8
`AkariOSTab.xaml.cs`) — all near line-for-line. `using Microsoft.Win32;` added for `Registry`.
Placed immediately after the Gaming Tweaks stub, preserving net8's Competitive → Shader Cache →
Gaming toggles → **NVIDIA → AMD** order. Gaming Tweaks stub shrunk to "Competitive Mode, Gaming
Tweaks". (`AddButtonGrid` will be reused by Useful Tools when it lands; `BuildToolsContent` not
ported this round.)

### Backing services — match Phase 10, no drift; elevation paths confirmed

`GpuTweaks.SetPState0/DisableEcc/DisableNvidiaTelemetry/UnrestrictClockPolicy/ApplyAmdDwords
(Action<string>)`; `NvidiaProfileService.ApplyAkariProfileAsync(ToolService)`;
`ToolFetchService.LaunchAsync(string, ToolService)` (confirmed: downloads-to-cache then launches
an external exe); `ElevationService.RunAsSystem(Action, Action<string>?):bool`. **Via RunAsSystem:**
`ApplyNvidiaMisc` + `ApplyAmdShaderCache`. **Direct read:** `ReadAmdShaderCache` = `Registry.GetValue`
only. No drift.

### Token→ThemeResource mapping (reused Phase-14 `Hex()`, no new values)

`TextSecondary`→`TextFillColorSecondaryBrush`; segment active fill `Accent`→`Hex("#E0142A")`,
active text `AccentText`→`Hex("#FF8A94")`; `CardBgHover`→`SubtleFillColorSecondaryBrush`;
`CardElevationBorder` (segment border)→`CardStrokeColorDefaultBrush`; inactive segment bg →
`Microsoft.UI.Colors.Transparent` (unchanged).

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors. (First patch of the Build() call site was malformed —
`BuildUtilityCard` returns a `Border`, needs `RootPanel.Children.Add(...)`; fixed before build.)
De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation)

- **Both cards render** — "NVIDIA" + all 7 buttons (Apply NVIDIA Profile, P-State 0, Disable ECC,
  Disable Telemetry, Unrestrict Clock Policy, NVCleanstall, Miscellaneous); "AMD" + all 4 buttons
  (DWORDS, RSS, Driver Download, Disable DXNAVI) + the "Shader Cache" segmented control with
  "AlwaysON" / "Default" segments.
- **✅ AMD segmented-control READ driven — correct against ground truth.** `BuildAmdShaderCacheControl`
  runs `Refresh()` → `ReadAmdShaderCache()` on build; it rendered without error and painted
  **neither** segment active (Unset). Verified against ground truth: the AMD `…\0000\UMD` key is
  **absent** on this NVIDIA-only desktop, so `ReadAmdShaderCache()` correctly returns `Unset`. No
  write was attempted (no `AMD Shader Cache …` log line — the read paints silently; that string
  only logs on write).
- No `[ERROR]`/`Unhandled` in the run.

### ⚠ NOT driven — individually

- **NVIDIA:** Apply NVIDIA Profile (`.nip` import), P-State 0, Disable ECC, Disable Telemetry,
  Unrestrict Clock Policy (all `GpuTweaks`/profile writes), NVCleanstall (external download+launch),
  **Miscellaneous** (`ElevationService.RunAsSystem` HKLM bundle) — none clicked.
- **AMD:** DWORDS (registry write), RSS + Disable DXNAVI (external download+launch), Driver
  Download (`RunShellProcess` → browser to amd.com, external launch), and **tapping either
  segment** (`ApplyAmdShaderCache` → `ElevationService.RunAsSystem` binary write) — none driven.
- All verified statically only (byte-for-byte port over the byte-identical services).

### VM checklist (Phase 18 — for isleap; AMD paths need AMD hardware)

- [ ] NVIDIA buttons apply their tweaks (log per action); Miscellaneous writes the HKLM bundle as
      SYSTEM; NVCleanstall downloads+launches.
- [ ] AMD (on an AMD box): DWORDS applies; RSS/DXNAVI launch; Driver Download opens amd.com;
      tapping AlwaysON/Default writes `ShaderCache` and the active segment repaints from the read.
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — not referenced; nothing changed.
- **Still stubbed:** Competitive Mode, Gaming Tweaks toggles (Gaming Tweaks stub); Useful Tools
  stub (its `BuildToolsContent` reuses the now-ported `AddButtonGrid`). Collapsible
  `BuildSectionCard` factory still deferred.
- **All NVIDIA/AMD writes + external launches** — not driven; isleap's step.

---

## MVVM Phase 19 — AkariOS ▸ Useful Tools — **COMPLETE, fully verified (render-only surface; nothing driven by design)**

Eighth AkariOS sub-section — the last bottom card. Plain Option-A card (choice 1), reusing the
Phase-18 `BuildUtilityCard` + `AddButtonGrid`. Build #2 read-only; **no Defender code touched**;
registers nothing → `[WARMUP]` = 439. **One file** (`Views/AkariOSPage.xaml.cs`) — no new
helpers, no swaps, no App/MainWindow/ViewModel edits.

### Source ported (net8 `AkariOSTab.GpuTools.cs` `BuildToolsContent`, lines 232–260)

Description line ("Tools are downloaded once and cached locally. First launch requires an
internet connection.") + a 14-button grid, each → `ToolFetchService.LaunchAsync(key, Service!)`:
Autoruns, Devmanview (`DevManView`), Serviwin (`ServiWin`), InSpectre, MouseTester, CRU, AUTO DSCP
(`AutoDSCP`), DISM++ (`DismPP`), Dev. Cleanup (`DeviceCleanup`), Interrupt AFPT
(`InterruptAffinity`), HIDUSB (`HidUsbF`), MeasureSleep, Process Explorer (`ProcessExplorer`),
ReservedCPUSets (`ReservedCpuSets`). Rendered as the **last** card (net8 Build() order). Token
map: `TextSecondary`→`TextFillColorSecondaryBrush`.

### All 14 = external-tool download+launch (confirmed, no in-app writes)

`LaunchAsync` resolves a cache path under `%LOCALAPPDATA%\AkariTool\Tools\`; cached → launch, else
download the bundle from the PostInstall GitHub raw repo, extract/copy, then launch. **No registry,
no `ElevationService`, no in-app system mutation.** So the entire drivable surface is render-only.
Two honest flags (still not-driven): **HIDUSB** launches a driver `Setup.exe`; **AUTO DSCP** is the
one non-zip entry (downloads + launches a `.bat`). Neither changes the classification.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation — render-only, as designed)

- **Card renders** — "Useful Tools" title + the description line verbatim.
- **All 14 buttons present** with correct labels (UIA matched 14/14: Autoruns, Devmanview,
  Serviwin, InSpectre, MouseTester, CRU, AUTO DSCP, DISM++, Dev. Cleanup, Interrupt AFPT, HIDUSB,
  MeasureSleep, Process Explorer, ReservedCPUSets).
- **✅ Nothing fired on load** — checked the log for `Downloading`/`Launching`/`Downloaded` after
  navigating to the page: **none**. Confirms every action is click-only (incl. HIDUSB and AUTO
  DSCP — neither downloads or launches on render). Nothing driven; render-only is the full
  surface, as designed.
- No `[ERROR]`/`Unhandled` in the run.

### VM checklist (Phase 19 — for isleap)

- [ ] Useful Tools: clicking a button downloads (first time) + launches the tool; second click is
      offline/cached. (HIDUSB launches a driver setup; AUTO DSCP runs a .bat — expected.)
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — not referenced; nothing changed.
- **Still stubbed:** Competitive Mode, Gaming Tweaks toggles (the single remaining Gaming Tweaks
  stub). Collapsible `BuildSectionCard` factory still deferred.
- **All 14 tool launches** — not driven (external download+launch); isleap's step.

---

## MVVM Phase 20 — AkariOS ▸ Gaming Tweaks toggles (+ 2nd dialog swap, ported BuildToggle) — **COMPLETE (restore-on-build reads DRIVEN; writes + reboot NOT driven; reboot dialog deliberately NOT driven — see abort note)**

Ninth AkariOS sub-section. Plain Option-A card "Gaming Tweaks" with three toggle rows. Build #2
read-only; **no Defender code touched**; registers nothing → `[WARMUP]` = 439. **One file**
(`Views/AkariOSPage.xaml.cs`) — no App/MainWindow/ViewModel edits.

### Source ported (net8 `AkariOSTab.GamingTweaksCard.cs:180–444`)

`_gamingSetters`, `BuildGamingToggleContent` (+ local `AddRow`), `SetPreemption`, `SetHdcp`,
`ConfirmNetworkRebootAsync`, `ExtractNetworkBatAsync`, the two bat-name consts,
`SetNetworkOptimization/Async` — near line-for-line. Rows: Disable Preemption (NVIDIA), Disable
HDCP, Network Optimization. Stub split: the old "Competitive Mode, Gaming Tweaks" stub → a
"Competitive Mode" stub + this real card (Competitive-before-GamingToggles preserved).

### Wrinkles resolved (shown before extraction)

- **`TweakHelpers.BuildToggle` absent in build #3** → ported net8's verbatim as a private
  `BuildToggle` helper: a `ToggleSwitch` with a `suppress` flag so the setter sets `IsOn`
  **without re-firing** `onToggle`. Load-bearing — it's why restore-on-build sets toggle state
  from a read without triggering a write.
- **State markers present** — reused `TweakHelpers.SaveState/HasState/ClearState` (build #3
  `TweakHelpers.State.cs`) directly.
- **2nd dialog swap** — `ConfirmNetworkRebootAsync`: `AkariDialogs.ConfirmContentAsync(content,
  title, primaryText)` → `_dialogs.ConfirmContentAsync(title, content, primaryText)` (arg order
  reversed, Phase-16 pattern). Copy verbatim: title "This will restart your PC", primary
  "Restart and apply"/"Restart and revert".
- **`ExtractNetworkBatAsync`** — `typeof(AkariOSTab)` → `typeof(AkariOSPage)` (app assembly);
  suffix `.Scripts.Network.{file}` unchanged (bats embedded + reachable — Phase-11 port-check).
- Reused `SystemStateReader.ReadPreemption/ReadHdcp`, `GpuTweaks.SetHdcpDisabled`,
  `Service.RunProcess(string,string,int?)` — all present, no drift.
- Token map: `TextPrimary/TextSecondary`→Fluent, `InfoFg`→`Hex("#4CC2FF")` (Phase-12),
  `AkariOverlayStrong` separator→`DividerStrokeColorDefaultBrush`. `AddRow`'s separator guard is
  `> 1` (title header is child 0), matching the Phase-17 adaptation.

### Reboot gate — confirmed no unguarded path

`SetNetworkOptimizationAsync` calls `ConfirmNetworkRebootAsync` **before** any
`ExtractNetworkBatAsync`/`RunProcess`. Cancel → revert toggle + return (no extraction, no run,
no reboot). No path reaches the reboot without the confirm returning true.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### `[WARMUP]` guard — UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation)

- **Card + all 3 rows render** — "Gaming Tweaks" title; Disable Preemption (NVIDIA) + desc;
  Disable HDCP + desc; Network Optimization + desc.
- **✅ Restore-on-build READS driven — correct vs ground truth, no write fired.** All three
  Gaming toggles rendered **Off**. Ground truth: `HKLM…\Scheduler\EnablePreemption` **absent** →
  `ReadPreemption()` returns **null** (it returns `val==0` only when the value exists) → toggle
  Off ✓; `RMHdcpKeyglobZero` absent → `ReadHdcp()` null → Off ✓; HKCU marker
  `NetworkOptimization` **absent** → `HasState` false → Off ✓. The restore reads executed on
  page build and set state through the **suppressing** setter — **no `SetPreemption`/`SetHdcp`
  write log line appeared on load** (verified). `[WARMUP]` 439.
- (The On/Off toggles UIA also surfaced #0–#4 are the Shader Cache **checkboxes** — DirectX/
  NVIDIA/Steam On, AMD/Intel Off — matching Phase 16, not Gaming toggles.)
- No `[ERROR]`/`Unhandled`.

### ⚠ Reboot dialog — DELIBERATELY NOT DRIVEN (abort per isleap's guardrail)

isleap authorized opening + Cancel-only, **but with**: "any ambiguity whatsoever in button
identification at runtime → abort to static-only immediately." The prerequisite to opening the
dialog is toggling the **Network Optimization** switch, and UIA reports all three Gaming
ToggleSwitches with **empty names** (`name=''`) — identifiable only by child index. That is
ambiguous, and misidentifying is worse than Shader Cache: toggling Preemption/HDCP by mistake
fires an **immediate HKLM registry write** (no dialog), and the right one leads to the reboot
path on isleap's **real desktop**. Per the stated abort condition, I did **not** drive it.
Confirmed post-session: **no** `Network Optimization` / `Restarting` / `shutdown` / temp-bat log
artifacts — nothing network/reboot ran. The **dialog copy + swap + Network apply/reboot path are
verified statically only** (byte-for-byte port; gate confirmed above).

### ⚠ NOT driven — individually

- **Disable Preemption toggle** → `SetPreemption` (6 HKLM writes + marker) — not driven.
- **Disable HDCP toggle** → `SetHdcp` → `GpuTweaks.SetHdcpDisabled` (adapter writes) — not driven.
- **Network Optimization toggle** → reboot dialog → `.bat` extract + `RunProcess` + **machine
  reboot** — not driven (dialog not opened, per abort note).

### VM checklist (Phase 20 — for isleap; disposable VM for the reboot path)

- [ ] Toggle Disable Preemption → 6 nvlddmkm/Scheduler values write; marker set; toggle persists.
- [ ] Toggle Disable HDCP → `RMHdcpKeyglobZero` writes across adapters; read-back keeps it On.
- [ ] Toggle Network Optimization → reboot confirm dialog shows the verbatim copy; **Cancel** =
      no change; **Restart and apply** = runs the bat and reboots (disposable VM only).
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — not referenced; nothing changed.
- **Ordering** — Shader Cache-vs-Competitive relative order left as-is (pre-existing Phase-16),
  flagged not fixed, per isleap.
- **Remaining:** Competitive Mode (finale); the deferred collapsible `BuildSectionCard` factory.
- **All Gaming-toggle writes + the reboot path** — not driven; isleap's step.

---

## DECISION — collapsible `BuildSectionCard` factory CANCELLED (not deferred)

isleap's call: **plain Option-A cards are now the PERMANENT pattern for AkariOS**, not a
placeholder awaiting a chrome upgrade. The previously-"deferred collapsible `BuildSectionCard`
factory step" is **cancelled** — do not implement it, and disregard the "deferred to the shared
factory step / later chrome pass" notes in Phases 16–20; those sections' plain cards are final.
Neither CLAUDE.md referenced the factory (grep `collapsible|BuildSectionCard|chrome|Option-A`
over both → clean), so no CLAUDE.md edit was needed. **Only remaining AkariOS work: Competitive
Mode.**

---

## MVVM Phase 21 - AkariOS > Competitive Mode, Sub-part A (scaffold + game picker + file picker) - COMPLETE (detection DRIVEN; picker/shortcut/start NOT driven)

First of five Competitive sub-parts (A->E). Plain Option-A card "Competitive Mode" with the picker; sub-parts B (anti-cheat notice), C (options), D (status + session machine), E (CLI/shutdown wiring) are STUBBED. Build #2 read-only; no Defender code; registers nothing -> [WARMUP] = 439. One file (Views/AkariOSPage.xaml.cs) - no App/MainWindow/ViewModel edits.

### Source ported (net8 AkariOSTab.Competitive.cs)

Shell BuildCompetitiveContent (B/C/D calls replaced by a visible stub note + minimal stubs), BuildCompetitivePicker, RefreshCompetitiveLaunchInfo, MakeCompetitiveButton, OnCompetitiveLoaded, BeginCompetitiveGameDetection, PopulateCompetitiveGames, BrowseForGame/BrowseForGameAsync, CreateCompetitiveShortcut, SanitizeFileName + picker fields - near line-for-line. Replaced the "Competitive Mode" stub, kept before Gaming Tweaks. Token map: TextSecondary->TextFillColorSecondaryBrush, TextMuted->TextFillColorTertiaryBrush.

### File-picker swap (elevation-safe) + _files resolution

_files = ServiceLocator.GetService<IFileService>() in the ctor (resolves to app-local AkariFileService). BrowseForGameAsync: net8 FilePickers.OpenFileAsync(".exe") (string?) -> _files.PickSingleFileAsync(new[]{".exe"}) (StorageFile?), .Path fed on. On record for this site: a raw WinRT FileOpenPicker throws COMException 0x80004005 under requireAdministrator (out-of-process broker refuses High-IL; InitializeWithWindow doesn't fix it) - so Browse must go through IFileService -> AkariFileService (in-process Win32 IFileOpenDialog), like Backup/WIM. Not driven (opens a native dialog).

### B/C/D stubs (each marked // STUB - sub-part ...)

Visible note "Options and session controls - pending (sub-parts B-D)"; plus minimal compile-enabling stubs: ReadCompetitiveOptionsFromUi() -> CompetitivePrefs.LoadOptions(); SyncCompetitiveControlStates() -> enable Start/Create-Shortcut when a game is selected + RefreshCompetitiveLaunchInfo(); SetCompetitiveStatus(text) -> Service.Log; OnCompetitivePrimaryClickAsync() -> status "pending sub-part D". SessionEndedByGameExit subscription deferred to D.

### Backing services - match Phase 10, no drift

GameDetection.DetectSteamGames(), DetectedGame(Name,ExePath); CompetitiveService.ResolveLaunch(string,CompetitiveOptions):LaunchPlan(bool ViaSteam,uint AppId); CompetitivePrefs.LoadOptions()/LoadLastGamePath()/SaveLastGamePath(string) (HKCU Software\AkariTool\Competitive*); IFileService.PickSingleFileAsync(IReadOnlyList<string>?). No drift.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (asInvoker) copy built the same.

### [WARMUP] guard - UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation)

- Card renders - "Competitive Mode" title, description, picker row (combo, Browse, Create Shortcut, Start Competitive Mode), and the "Options and session controls - pending (sub-parts B-D)" stub note.
- Game detection DRIVEN live (read-only) - DetectSteamGames() populated the combo with 4 real Steam games: Apex Legends, Dead by Daylight, MECCHA CHAMELEON, ProjectZomboid. (Interleaved Home/Windows Apps/... in the UIA sweep are NavigationView rail ListItems.)
- Button states correct - Browse enabled; Create Shortcut + Start disabled (no valid selection - see next).
- On-load SaveLastGamePath did NOT fire - honest nuance. Ground truth had a persisted game (D:\SteamLibrary\...\Half-Life\hl.exe), but it is not among the 4 detected and its exe isn't currently present, so PopulateCompetitiveGames never restored a selection (SelectedIndex stayed unset -> SelectionChanged never fired). HKCU CompetitiveLastGame unchanged after load - no write occurred. (Had the persisted game been present/detected, net8 would fire one idempotent re-save on restore - verbatim behavior; just not triggered here.)
- No [ERROR]/Unhandled.

### NOT driven - individually

- Combo selection change -> SaveLastGamePath (HKCU write; would overwrite isleap's persisted CompetitiveLastGame) - not driven; selection->enable->launch-info path is static-only.
- Browse -> IFileService.PickSingleFileAsync (native file dialog) - not driven.
- Create Shortcut -> writes a .lnk to the Desktop (WScript.Shell) - not driven.
- Start Competitive Mode -> stubbed (sub-part D) anyway - not driven.

### VM checklist (Sub-part A - for isleap)

- [ ] Competitive card: combo lists detected Steam games; selecting one enables Start + Create Shortcut and shows the "exe - via Steam (AppID n)/direct launch" line.
- [ ] Browse opens the file dialog (elevated) and adds the picked exe.
- [ ] Create Shortcut writes "<game> (Competitive).lnk" to the Desktop.
- [ ] [WARMUP] still tiles [0..439).

### Not touched / not done

- Build #2 (net8) - read-only; zero writes.
- Defender - not referenced; nothing changed.
- Sub-parts B-D - stubbed (visible note + minimal stubs); E (CLI/shutdown wiring) not started. No App/MainWindow edits yet.
- Picker write paths + shortcut + start - not driven; isleap's step.

---

## MVVM Phase 22 - AkariOS > Competitive Mode, Sub-part B (anti-cheat notice + disclaimer dialog) - COMPLETE (notice DRIVEN; disclaimer DORMANT/static-only)

Second of five Competitive sub-parts. Build #2 read-only; no Defender code; registers nothing -> [WARMUP] = 439. One file (Views/AkariOSPage.xaml.cs) - no App/MainWindow/ViewModel edits.

### Source ported (net8 AkariOSTab.Competitive.cs 300-369), copy byte-for-byte

- BuildCompetitiveAntiCheatNotice - static warning TextBlock; un-stubbed into BuildCompetitiveContent after BuildCompetitivePicker (net8 position); remaining stub note retitled "pending (sub-parts C-D)".
- DisclaimerPrefKey/DisclaimerPrefName consts, CompetitiveDisclaimerAccepted() (HKCU read), ConfirmCompetitiveDisclaimerAsync() (3rd AkariDialogs->TweakDialogs swap).
- Token: WarnFg -> SystemFillColorCautionBrush. Sub-part A's OnCompetitivePrimaryClickAsync stub untouched.

### DORMANCY (flagged) - disclaimer has no caller this sub-part

ConfirmCompetitiveDisclaimerAsync is called only from StartCompetitiveSessionAsync (D, line 695) and StartCompetitiveFromCommandLineAsync (E, line 956) - both stubbed. So the disclaimer dialog, its accept-write, and the accepted-read are DORMANT this round; they go live when D wires Start. Compiles clean (stock csc does not warn on an unused private method).

### Dialog swap (3rd) - same pattern + one extra

AkariDialogs.ConfirmContentAsync(content, title, primaryText) -> _dialogs.ConfirmContentAsync(title, content, primaryText) (arg order reversed, Phase 16/20 pattern). Copy verbatim: title "Competitive Mode is experimental", primary "I understand, continue". DIFFERENCE vs shader/reboot swaps: this gate persists an HKCU accept flag (CompetitiveDisclaimerAccepted = 1) after the dialog returns true - inside the gate, not part of the swap, only on primary.

### CARRY INTO D (explicit) - accepted flag already 1 on isleap's machine

Ground truth: HKCU Software\AkariTool\CompetitiveDisclaimerAccepted = 1. So when D wires Start -> ConfirmCompetitiveDisclaimerAsync, the gate SHORT-CIRCUITS (returns true immediately) and the dialog does NOT appear during normal verification. To actually exercise the live disclaimer-dialog-driving caution planned for D (enumerate buttons, dismiss via exact-match Cancel/Escape only, abort on ambiguity), the flag must first be deliberately CLEARED - a real (low-stakes) state change to isleap's machine that must be called out as its own decision when D is reached. Do NOT clear it without flagging.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (asInvoker) copy built the same.

### [WARMUP] guard - UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation)

- Anti-cheat notice DRIVEN (renders verbatim): "Experimental - use at your own risk. Competitive Mode temporarily suspends apps, stops services ... never modifies game memory. If Akari Tool is closed unexpectedly during a session, it will offer to restore your settings on next launch." Stub note now reads "pending (sub-parts C-D)".
- Disclaimer dialog NOT drivable this round - stated plainly, not implied as a pass. No caller exists until D. UIA confirmed the dialog is ABSENT (no "Competitive Mode is experimental" / "I understand, continue" text on screen) - correct. The swap, accept-write, and accepted-read are verified STATICALLY ONLY; accepted-flag ground truth = 1.
- No [ERROR]/Unhandled.

### Not touched / not done

- Build #2 (net8) - read-only; zero writes.
- Defender - not referenced; nothing changed.
- Sub-parts C-D stubbed; E not started. No App/MainWindow edits.
- Disclaimer dialog + accept-write - dormant (no caller until D); not driven.

---

## MVVM Phase 23 - AkariOS > Competitive Mode, Sub-part C (options groups) - COMPLETE (Load DRIVEN read-only; every option-write NOT driven)

Third of five Competitive sub-parts. Build #2 read-only; no Defender code; registers nothing -> [WARMUP] = 439. One file (Views/AkariOSPage.xaml.cs) - no App/MainWindow/ViewModel edits.

### Source ported (net8 AkariOSTab.Competitive.cs 373-498, 593-662)

BuildCompetitiveOptionGroups (3 groups: Game Process, Background Activity, System), AddCompetitiveGroup/Check/Dropdown, LoadCompetitiveOptionsIntoUi + option fields + _cmBusy - near line-for-line. Shell: replaced the "pending (C-D)" stub with BuildCompetitiveOptionGroups(panel) + a new "Session status - pending (sub-part D)" stub, then LoadCompetitiveOptionsIntoUi(CompetitivePrefs.LoadOptions()) before SyncCompetitiveControlStates() (net8 order). Token map: TextPrimary/TextSecondary -> Fluent.

### Two Sub-part A stubs REPLACED with real versions

- ReadCompetitiveOptionsFromUi: A-stub (returned LoadOptions()) -> real (reads the 10 checkbox/dropdown states). Added SaveCompetitiveOptions (CompetitivePrefs.SaveOptions(ReadOptionsFromUi())).
- SyncCompetitiveControlStates: A-stub -> real. BEHAVIOR CHANGE (net8-faithful, flagged): the real Sync sets _cmPrimaryBtn.IsEnabled = true UNCONDITIONALLY (Start always enabled; the D click-handler validates selection) - vs A's stub which disabled Start when no game selected. Create Shortcut stays selection-gated. Real Sync reads CompetitiveService.IsSessionActive + _cmBusy and calls RefreshCompetitiveStatus() (added as a sub-part-D no-op STUB).

### Backing services - match Phase 10, no drift

CompetitiveOptions record (10 fields: BoostGamePriority, PriorityLevel:GamePriorityLevel, IoPriority:GameIoPriority, CpuSets:CpuSetMode, GameFocus, PauseNonEssentialServices, ConsistentPerformance, CloseAfterLaunch, ClearStandbyMemory, LaunchThroughSteam); enums GamePriorityLevel{AboveNormal,High} / GameIoPriority{Normal,High} / CpuSetMode{AllCores}; CompetitivePrefs.LoadOptions()/SaveOptions(); CompetitiveService.IsSessionActive. No drift.

### SaveCompetitiveOptions - what/when

Writes the full 10-field record. Triggered on every option-control change: each of the 7 checkbox .Click handlers + each of the 3 dropdown .SelectionChanged handlers. No save button; not on session start. So any option interaction = one prefs write.

### Build (VS MSBuild, literal)

First build emitted CS0649 (_cmBusy never assigned - it's written in sub-part D). Fixed with an explicit `= false` initializer (interim). Second build:
```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors. De-elevated (asInvoker) copy built the same.

### [WARMUP] guard - UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation)

- 3 option groups render - Game Process, Background Activity, System - plus the Priority Level / I/O Priority / CPU Sets dropdowns and the "Session status - pending (sub-part D)" stub (old options stub gone).
- Load DRIVEN, read-only, matches ground truth. The 7 Competitive checkboxes (build order) read: Boost=On, LaunchThroughSteam=On, GameFocus=On, PauseServices=On, CloseAfterLaunch=Off, ConsistentPerf=On, ClearStandby=On - exactly matching HKCU (1/1/1/1/0/1/1). (First 5 toggles in the sweep are Shader Cache checkboxes.)
- No SaveCompetitiveOptions fired on load - registry snapshot IDENTICAL before/after navigation. (PriorityLevel/IoPriority both High = built default index 1 -> no SelectionChanged -> no save; checkbox IsChecked assignment doesn't fire Click.)
- No [ERROR]/Unhandled. [WARMUP] 439.

### NOT driven - per control type (not lumped)

- 7 checkbox .Click -> SaveCompetitiveOptions (full-record prefs write) - not driven.
- 3 dropdown .SelectionChanged -> SaveCompetitiveOptions (full-record prefs write) - not driven.
- (LoadCompetitiveOptionsIntoUi CAN fire an idempotent SaveOptions on load only if a persisted priority differs from the default index; on this machine High/High = default, so it did not - verified by the identical registry snapshot.)

### Not touched / not done

- Build #2 (net8) - read-only; zero writes.
- Defender - not referenced; nothing changed.
- Sub-part D (status panel + session state machine) - status stub in place; RefreshCompetitiveStatus/SetCompetitiveStatus/OnCompetitivePrimaryClickAsync still stubs. E not started. _cmBusy assigned in D. No App/MainWindow edits.
- All option write paths - not driven; isleap's step.

---

## MVVM Phase 24 - AkariOS > Competitive Mode, Sub-part D (status panel + session state machine) - COMPLETE (idle status DRIVEN; entire session lifecycle static-only)

Fourth of five Competitive sub-parts - the destructive core. Build #2 read-only; no Defender code; registers nothing -> [WARMUP] = 439. One file (Views/AkariOSPage.xaml.cs) - NO App/MainWindow/ViewModel edits (MainWindow reached via DI, below).

### Source ported (net8 AkariOSTab.Competitive.cs 500-589, 666-870)

BuildCompetitiveStatus, SetCompetitiveStatus, RefreshCompetitiveStatus, FriendlyServiceName, the DispatcherTimer trio, OnCompetitivePrimaryClickAsync, CancelCompetitiveStart, StartCompetitiveSessionAsync, HandleCompetitiveStartFailure, GetActiveSchemeForDisplay, EndCompetitiveSessionAsync, OnCompetitiveSessionEndedExternally, ScheduleHideForCompetitive, RestoreMainWindowAfterCompetitive - near line-for-line. Replaced the "Session status - pending D" shell stub with BuildCompetitiveStatus(panel); replaced the 3 D method-stubs (RefreshCompetitiveStatus/SetCompetitiveStatus/OnCompetitivePrimaryClickAsync) with real versions. Added status/session fields; dropped _cmBusy's interim `= false` initializer (D assigns it now). Wired SessionEndedByGameExit subscribe + Unloaded unsubscribe (deferred from A). Added `using System.Threading;`. Token map: TextSecondary -> Fluent; status separator AkariOverlayStrong -> DividerStrokeColorDefaultBrush.

### MainWindow reach - DI, no MainWindow edit

net8 MainWindow.Instance -> ServiceLocator.GetService<MainWindow>() in ScheduleHideForCompetitive/RestoreMainWindowAfterCompetitive (MainWindow is a DI singleton, App.xaml.cs AddSingleton<MainWindow>(); same instance). .AppWindow.Hide()/Show(), OverlappedPresenter.Restore(), .Activate() are standard Window members. Kept the change to one file - no App/MainWindow edit, as isleap approved.

### Backing services - match Phase 10, no drift

CompetitiveService.StartAsync(string, CompetitiveOptions, IProgress<string>?, CancellationToken):Task<CompetitiveStartResult>, EndAsync(CompetitiveSessionState, IProgress<string>?):Task, DescribeScheme(string?):string, CurrentState, IsSessionActive, SessionEndedByGameExit; CompetitiveStartResult(Outcome, State, Error) + Started; CompetitiveStartOutcome enum; CompetitiveSessionState(GameProcessName, StartedUtc, PreviousPowerSchemeGuid, SuspendedProcesses, StoppedServices, TuningFailures). SystemStateReader.ReadActivePowerPlan():(string? Name, string? Guid). No drift.

### POWER-PLAN INVARIANT - confirmed by CODE REVIEW (not implied)

grep of the ported file for SetPowerCfg|SETACTIVE|/setactive|EnsureAkariScheme|SetActive|powercfg -> the ONLY hit is inside the BCD section's COMMENT (line ~434, "Apply runs bcdedit + powercfg -h off") - NOT the Competitive read path. There is NO scheme-write call anywhere in AkariOSPage. RefreshCompetitiveStatus calls NEITHER ReadActivePowerPlan NOR DescribeScheme - it reads only CurrentState + the already-resolved _cmActiveSchemeName string. The scheme reads (DescribeScheme(GetActiveSchemeForDisplay()), and ReadActivePowerPlan which is a pure registry read) live only inside StartCompetitiveSessionAsync's success branch (the write/start path). => the read/refresh path never reactivates a scheme; the CLAUDE.md invariant holds.

### Disclaimer short-circuit (on record)

OnCompetitivePrimaryClickAsync -> StartCompetitiveSessionAsync -> ConfirmCompetitiveDisclaimerAsync, which begins `if (CompetitiveDisclaimerAccepted()) return true;`. Flag = 1 on this machine -> returns true immediately, dialog never constructed. Combined with Start not being driven, the disclaimer is doubly untouched by normal verification. Flag NOT cleared (unauthorized).

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (asInvoker) copy built the same.

### [WARMUP] guard - UNCHANGED at 439

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (de-elevated + UIAutomation) - heavier D-specific plan

- Idle RefreshCompetitiveStatus repaint DRIVEN (read-only) - status headline rendered "Idle. Select a game to begin." (correct: no game selected on this machine - the persisted Half-Life exe isn't detected, Phase 21). The old "Session status - pending D" stub is gone.
- Primary button shows "Start Competitive Mode", enabled = True (net8 always-enabled; the click handler validates selection).
- ZERO session/power activity on idle - no "Competitive Mode started/ended", no StartAsync, no power-plan/scheme log lines after navigation. Confirms the idle path never calls ReadActivePowerPlan or any session method.
- No [ERROR]/Unhandled. [WARMUP] 439.

### NOT driven - entire session lifecycle, static-only

OnCompetitivePrimaryClickAsync (Start/End/Cancel dispatch), StartCompetitiveSessionAsync, EndCompetitiveSessionAsync, CancelCompetitiveStart, OnCompetitiveSessionEndedExternally, CompetitiveService.StartAsync/EndAsync (app-suspend, service-stop, power-plan swap, standby clear), the DispatcherTimer, HandleCompetitiveStartFailure, ScheduleHideForCompetitive (hides the app window - would also break UIA mid-session), RestoreMainWindowAfterCompetitive. All verified statically over the byte-identical CompetitiveService. Disclaimer dialog untouched (flag=1 short-circuit + Start not driven).

### VM checklist (Sub-part D - for isleap; disposable/careful - real session)

- [ ] Select a game -> Start Competitive Mode -> disclaimer short-circuits (flag=1) -> session starts: status shows "Active - <proc> - hh:mm:ss" with bullets (N apps suspended, power plan, services paused); button flips to "End Session".
- [ ] End Session -> "settings restored"; apps resume, services restart, power plan restored, window restored.
- [ ] CloseAfterLaunch on -> window hides ~10s after the game appears; restored on game exit.
- [ ] Game exit (external) -> OnCompetitiveSessionEndedExternally restores + status "Game exited - settings restored".
- [ ] [WARMUP] still tiles [0..439).

### Not touched / not done

- Build #2 (net8) - read-only; zero writes.
- Defender - not referenced; nothing changed.
- Sub-part E (CLI/shutdown entry points: StartCompetitiveFromCommandLineAsync, EndCompetitiveSessionForShutdownAsync + App/MainWindow --competitive wiring) - LAST, not started; will show source + scope before touching App.xaml.cs/MainWindow.xaml.cs.
- Entire session lifecycle - not driven; isleap's disposable-VM step. Disclaimer flag NOT cleared (unauthorized).

---

## MVVM Phase 25 - AkariOS > Competitive Mode, Sub-part E1 (relocate session-owning state to AkariOSViewModel) - COMPLETE (A-D re-driven; session lifecycle static-only)

E1 of E1/E2/E3. The highest-risk sub-part - a real MVVM refactor, not a copy-port. The Competitive session-control state + Start/End machine move OFF the page INTO AkariOSViewModel (DI singleton) so E2/E3's headless CLI/shutdown/recovery paths can drive a session without a page. Build #2 read-only; no Defender code; registers nothing -> [WARMUP] = 439. Two files: ViewModels/AkariOS/AkariOSViewModel.cs + Views/AkariOSPage.xaml.cs. NO App.xaml.cs / MainWindow.xaml.cs edits (both flags resolved via DI, below).

### Relocated to AkariOSViewModel

- State: _selectedPath, _busy, _cts, _activeSchemeName (exposed as SelectedPath/IsBusy/ActiveSchemeName/IsSessionActive).
- Start/End machine (renamed, bodies near-verbatim; UI calls -> events): PrimaryClickAsync, StartSessionAsync, EndSessionAsync, CancelStart, HandleStartFailure, GetActiveSchemeForDisplay, ScheduleHide, RestoreMainWindow, OnSessionEndedExternally.
- Disclaimer gate (from B): ConfirmDisclaimerAsync + DisclaimerAccepted + the two consts (3rd AkariDialogs->TweakDialogs swap, verbatim copy).
- Ctor gains TweakDialogs (DI-resolved automatically - no App.xaml.cs edit). SetCompetitiveStatus -> raise Status event; SyncCompetitiveControlStates -> raise StateChanged event; Progress<string>(SetStatus) -> Progress<string>(RaiseStatus).

### Kept on AkariOSPage (UI), adapted to delegate

All builders (A-D), ReadCompetitiveOptionsFromUi/Load/Save (C), the elapsed timer + RefreshCompetitiveStatus + SetCompetitiveStatus + BuildCompetitiveStatus + FriendlyServiceName (D UI), SyncCompetitiveControlStates, game detection, file picker, shortcut. Field references adapted: _cmSelectedPath -> ViewModel.SelectedPath, _cmBusy -> ViewModel.IsBusy, _cmActiveSchemeName -> ViewModel.ActiveSchemeName. Start button -> ViewModel.PrimaryClickAsync(ReadCompetitiveOptionsFromUi()). New page method OnVmStateChanged() (Sync + timer start/stop) subscribed to ViewModel.StateChanged and called once on (re)build; ViewModel.Status subscribed to SetCompetitiveStatus; both unsubscribed on Unloaded.

### Flags (both isleap-approved) - implemented

- Flag 1 (UI dispatcher): VM has no DispatcherQueue and the CLI path no page -> `private static DispatcherQueue? Ui => ServiceLocator.GetService<MainWindow>()?.DispatcherQueue;`. ScheduleHide/OnSessionEndedExternally marshal through it.
- Flag 2 (SessionEndedByGameExit -> VM-lifetime): subscribed in the VM ctor (always alive), not per-page. The page no longer subscribes to CompetitiveService; it renders the VM's StateChanged/Status instead.

### POWER-PLAN INVARIANT - re-confirmed by CODE REVIEW across BOTH files

grep SetPowerCfg|SETACTIVE|/setactive|EnsureAkariScheme|SetActive|powercfg over AkariOSViewModel.cs + AkariOSPage.xaml.cs -> the ONLY hit is the BCD comment in the page. VM has ZERO scheme-write. The VM's only power calls are DescribeScheme(GetActiveSchemeForDisplay()) inside StartSessionAsync's success branch (write/start path; both reads) and GetActiveSchemeForDisplay = ReadActivePowerPlan (pure registry read). RefreshCompetitiveStatus (page) reads CurrentState + ViewModel.ActiveSchemeName only. Invariant holds after relocation.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt (incl. an intentional local-var fix to dodge a CS8604 nullable warning on the combo-selection SaveLastGamePath). De-elevated (asInvoker) copy built the same.

### [WARMUP] guard - UNCHANGED at 439

`[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).`

### Verification (de-elevated + UIAutomation) - full A-D re-drive

- A-D all render via the new VM-delegation path: Competitive card, anti-cheat notice (B), 3 option groups Game Process/Background Activity/System (C), and status "Idle. Select a game to begin." rendered via ViewModel.StateChanged -> page OnVmStateChanged -> RefreshCompetitiveStatus (D). Primary button "Start Competitive Mode", enabled.
- D-FLAG 5 (page rebuild) DRIVEN, no-session case: navigated AkariOS -> Home -> AkariOS. The rebuilt page re-renders correctly - card present, idle status re-rendered, primary button correct, NO crash. The Unloaded-unsubscribe + rebuild-resubscribe of the VM events works; the always-alive VM survives the page teardown. (The mid-session-ACTIVE branch of OnVmStateChanged - reads CompetitiveService.IsSessionActive on rebuild to show "End Session" + start the timer - is code-reasoned/static only; it cannot be driven without starting a real destructive session.)
- Options load read-only: prefs snapshot IDENTICAL before/after (no save-on-load). No session/power log lines. No [ERROR]/Unhandled. [WARMUP] 439.

### NOT driven - entire session lifecycle (now VM-owned), static-only

PrimaryClickAsync/StartSessionAsync/EndSessionAsync/CancelStart/HandleStartFailure/ScheduleHide/RestoreMainWindow/OnSessionEndedExternally + CompetitiveService.StartAsync/EndAsync. Disclaimer dialog untouched (flag=1 short-circuit; Start not driven; flag NOT cleared - unauthorized).

### Not touched / not done

- Build #2 (net8) - read-only; zero writes. Defender - not referenced.
- App.xaml.cs / MainWindow.xaml.cs - UNTOUCHED (E2/E3).
- E2 (App --competitive parse/dispatch, adds VM.StartFromCommandLineAsync) and E3 (MainWindow delegator + CheckOrphanedCompetitiveSessionAsync + EndForShutdownAsync dormant) - NEXT, not started. Will show source + proposed App/MainWindow wiring before touching those files.

---

## MVVM Phase 26 - AkariOS > Competitive Mode, Sub-part E2 (App.xaml.cs --competitive parse/dispatch) - COMPLETE (normal launch DRIVEN; --competitive path static-only)

E2 of E1/E2/E3. First App.xaml.cs edit of the whole migration. Build #2 read-only; no Defender code; registers nothing -> [WARMUP] = 439. Two files: ViewModels/AkariOS/AkariOSViewModel.cs (+ StartFromCommandLineAsync) + App.xaml.cs (parse/dispatch). No MainWindow.xaml.cs edit.

### VM: StartFromCommandLineAsync(exePath) - relocated, not new

Built from net8's StartCompetitiveFromCommandLineAsync, relocated the E1 way (UI calls -> events). File.Exists check -> _selectedPath + SaveLastGamePath -> options = LoadOptions() with { CloseAfterLaunch = true } -> ConfirmDisclaimerAsync() gate -> _busy/_cts + RaiseStateChanged -> CompetitiveService.StartAsync -> Started/HandleStartFailure -> log "...from shortcut for {proc}" -> RaiseStateChanged -> ScheduleHide (ALWAYS, per net8) -> catch/finally identical to StartSessionAsync. net8 UI-only calls dropped (flagged, headless path): BeginCompetitiveGameDetection, LoadCompetitiveOptionsIntoUi, StartCompetitiveTimer - the window is hidden; a page shown later re-syncs via OnVmStateChanged. Called by App directly on the VM (no MainWindow delegator).

### App.xaml.cs - additive; one existing line changed

- Added `using AkariTool.ViewModels.AkariOS;`.
- Added ParseCompetitiveArgument() (verbatim net8: scans Environment.GetCommandLineArgs() for --competitive <path>, returns the path iff File.Exists else null; ToolService.Current?.Log on failure).
- CHANGED one existing line: `MainWindow.Activate();` -> `if (competitiveExe is null) MainWindow.Activate();` (net8 does exactly this; a --competitive launch stays hidden, the un-activated window keeps the process alive).
- Added post-warm-up dispatch: `if (competitiveExe is not null) { /* E3: CheckOrphaned first */ _ = Services.GetRequiredService<AkariOSViewModel>().StartFromCommandLineAsync(competitiveExe); }` - fire-and-forget (OnLaunched is sync void; session runs in background).
- DispatcherQueue capture, unhandled-exception hook, warm-up Task.Run - UNTOUCHED.

### CheckOrphaned gap (E3) - documented, cleanly omitted

net8's --competitive path calls CheckOrphanedCompetitiveSessionAsync() then StartCompetitiveFromCommandLineAsync() as two sequential awaits - separable. E2 omits the recovery call; an explicit `// E3:` comment marks the gap. E3 adds CheckOrphanedSessionAsync to the VM (decision 2) and wires it here + on the normal path. No MainWindow delegator needed (App calls the VM directly).

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt (ToolService.Current resolves). De-elevated (asInvoker) copy built the same.

### [WARMUP] guard - UNCHANGED at 439

`[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).`

### Verification (de-elevated) - normal launch DRIVEN, common path undisturbed

- NORMAL launch (no --competitive): app launched, window ACTIVATED (title "Akari Tool") -> ParseCompetitiveArgument returned null -> MainWindow.Activate() ran (identical to before). Shell renders (SOFTWARE/OPTIMIZE/ADVANCED rail groups + the Home landing tab "Your control center for Windows..." - an existing tab rendering fully). 
- The --competitive dispatch did NOT fire on normal launch: no "from shortcut" / StartFromCommandLine / "Competitive Mode started" log lines. Correct - the if(competitiveExe is not null) block was skipped.
- No [ERROR]/Unhandled. [WARMUP] 439.
- (A Gaming-tab nav spot-check text did not surface in the UIA filter - a timing/filter artifact, not a defect; Home rendered fully and the app was error-free. E2 touches only startup, not any tab.)

### NOT driven - the --competitive path

Starts a real destructive session (VM -> CompetitiveService.StartAsync). Static-only, same as every session-lifecycle path (D/E1). Disclaimer flag still 1 (untouched, not cleared).

### Not touched / not done

- Build #2 (net8) - read-only. Defender - not referenced.
- MainWindow.xaml.cs - UNTOUCHED (E3).
- E3 (LAST): CheckOrphanedCompetitiveSessionAsync on the VM (4th AkariDialogs->TweakDialogs swap) + normal-path recovery call + EndForShutdownAsync dormant. The net8 MainWindow delegator is obviated (App calls the VM directly). Will show source + proposed wiring before touching MainWindow.xaml.cs.

---

## MVVM Phase 27 - AkariOS > Competitive Mode, Sub-part E3 (crash recovery + dormant shutdown) - COMPLETE. AkariOS FULLY PORTED.

Final sub-part of the final section of AkariOS. Build #2 read-only; no Defender code; registers nothing -> [WARMUP] = 439. Two files: ViewModels/AkariOS/AkariOSViewModel.cs + App.xaml.cs. MainWindow.xaml.cs NEVER TOUCHED (git diff confirms).

### VM additions (relocated from net8 MainWindow, decision 2)

- CheckOrphanedSessionAsync() - crash-recovery prompt. TryLoad (read-only) -> early return if no orphaned session; else the 4th dialog (see below) -> Restore = CompetitiveService.EndAsync; Ignore = CompetitiveSessionStore.Clear. XamlRoot fallback (net8 RootGrid.XamlRoot -> MainWindow.Content.XamlRoot) + null-skip. `_recoveryChecked` guard (run-once across both startup paths).
- EndForShutdownAsync() - ported DORMANT/verbatim (net8's doc-comment claims a Closing handler that never existed). Zero callers (grep-confirmed: only its own definition). No AppWindow.Closing wired (decision 3).
- Ctor gains IDialogService (DI-resolved; no App.xaml.cs edit).

### 4th dialog swap - the ONE deviation from the prior three (flagged)

The recovery prompt needs custom "Restore"/"Ignore" labels. TweakDialogs.ConfirmContentAsync hardcodes "Cancel", so this dialog calls the framework IDialogService.ShowAsync("Competitive Mode", content, "Restore", "Ignore") directly (result == ContentDialogResult.Primary = Restore) - preserving the verbatim labels. Same DI singleton TweakDialogs wraps (shared XamlRoot). isleap-approved (open Q4).

### App.xaml.cs wiring (isleap-approved open Q3: normal-path call in App, MainWindow untouched)

- Normal path: after MainWindow.Activate(), `_ = vm.CheckOrphanedSessionAsync();`.
- --competitive path: replaced the E2 fire-and-forget with a local async helper StartCompetitiveFromShortcutAsync(exe) that awaits CheckOrphanedSessionAsync() THEN StartFromCommandLineAsync(exe) (net8 order preserved; OnLaunched is void). Removed the E2 `// E3:` gap comment.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors, first attempt. De-elevated (asInvoker) copy built the same.

### [WARMUP] guard - UNCHANGED at 439

`[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).`

### Verification (de-elevated) - normal-path recovery "finds nothing" DRIVEN (real, not simulated)

Ground truth: %APPDATA%\AkariTool\competitive-session.json does NOT exist (no session ever started this migration).
- Normal launch: app launched + activated ("Akari Tool"); CheckOrphanedSessionAsync ran; TryLoad correctly found nothing.
- NO recovery dialog appeared (UIA: no "was not closed properly" / "Restore normal settings" text).
- NO "orphaned session restored/discarded" log lines (TryLoad false -> silent early return).
- Session file still absent after (nothing fabricated/created). Shell renders (Home landing tab). No [ERROR]/Unhandled. [WARMUP] 439.

### NOT driven / not done

- Recovery dialog, Restore (EndAsync), Clear - not triggered (no orphaned session; NOT fabricated - unauthorized state change). --competitive path - static-only. EndForShutdownAsync - dormant, zero callers.
- MainWindow.xaml.cs - NEVER touched across the entire AkariOS migration. Build #2 read-only. Defender - not referenced.

---

## ★ AkariOS MIGRATION COMPLETE (Phases 11-27) ★

Every AkariOS section is ported to build #3, each show-source -> sign-off -> extract -> verify:
- Phase 11 PostInstall banner; 12 Service Preset; 13 Playbook; 14 BCD; 15 presets-container reassembly (+ 2nd Defender copy string, verbatim); 16 Shader Cache (1st dialog swap); 17 Utilities (Account/Interface/System); 18 NVIDIA/AMD; 19 Useful Tools; 20 Gaming Tweaks toggles (2nd dialog swap, reboot-gated); 21-27 Competitive Mode A (picker+file-picker), B (anti-cheat notice + disclaimer, 3rd swap), C (options), D (status + session state machine), E1 (relocate session state to AkariOSViewModel), E2 (App --competitive parse/dispatch), E3 (crash recovery, 4th swap + dormant shutdown).
- [WARMUP] held at 439 the entire time (AkariOS registers nothing with TweakRegistry - it is action-based, not TweakDefinition-backed).
- Defender: never touched; the two service-preset copy strings ported verbatim; no Defender code referenced anywhere in AkariOS.
- MainWindow.xaml.cs: NEVER touched across all of AkariOS. Only App.xaml.cs was touched (E2/E3, Competitive command-line + recovery wiring). Collapsible BuildSectionCard factory: CANCELLED (plain Option-A cards are the permanent AkariOS pattern).
- Remaining build #3 rail-tag work (NOT AkariOS): Verify + AppUpdate tabs (still PlaceholderPage), the six Customize sub-nav tags. AkariOS itself is done.

---

## MVVM Phase 28 - BUGFIX: Network Optimization pause-hang (Option 2: decouple tweak-apply from reboot) - COMPLETE (static + runner verified; real apply/reboot is a VM-checklist item)

Not a porting step - a correctness fix for a flow that never worked (its reboot line has never executed; the apply path was static-only the whole migration). Build #2 read-only; no Defender code; registers nothing -> [WARMUP] = 439.

### Root cause (confirmed Phase-28 investigation)

network-apply.bat / network-revert.bat both ended with an interactive `pause` ("Press any key to reboot") then `shutdown -r -t 01`. ToolService.RunProcess launches them CreateNoWindow=true with StandardInput NOT redirected (default false) -> no console, no stdin EOF -> `pause` blocks forever -> the 120s Task.Delay branch fires -> process.Kill + Log("[TIMEOUT] Installation took too long") + return -1. The `shutdown` line never ran. SetNetworkOptimizationAsync only SaveState()s after the `if (exit != 0) return;` guard, so every retry (incl. post-reboot) re-ran the identical hang. The port's comment claiming "windowless runner hits EOF and reboots" was the wrong theory - stdin was never redirected. Pre-existing (net8 used the same RunProcess path); carried forward verbatim in Phase 20; never caught because the apply path was never driven.

### Fix (Option 2, isleap-approved: decouple apply from reboot)

- Scripts/Network/network-apply.bat: removed the 4-line tail (Echo "A reboot may be required" / Echo "Press any key to reboot" / pause / shutdown -r -t 01). Ends ...taskoffload=enabled / cls / exit. All 183 lines of reg/netsh tweak logic byte-identical.
- Scripts/Network/network-revert.bat: removed the same 4-line tail. Ends ...Reverted" / exit. winsock reset logic byte-identical.
- SetNetworkOptimizationAsync: after RunProcess returns 0 (now genuine completion), SaveState/ClearState fires on real success, then the app triggers the reboot itself: `RunShellProcess("shutdown", "/r /t 5")` - reusing the existing Phase-18 shell-execute helper + the standard shutdown command; NO new reboot abstraction (none exists elsewhere: the other shutdown -r hits are Customize context-menu registry verbs + the Autounattend embedded script). /t 5 (not /t 1) for state-write + temp-bat-cleanup margin. Two now-false comments updated. Single code path -> apply and revert both fixed symmetrically.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```
Zero warnings, zero errors. De-elevated copy built the same. [WARMUP] OK tiling [0..439).

### Verification (two distinct claims, both proven)

1. "pause is gone" (static): grep both .bat -> zero `pause`, zero reboot `shutdown` (the only shutdown hits are the WolShutdownLinkSpeed tweak value); tweak logic intact.
2. "runner success path works post-fix" (runner): a synthetic echo-only .bat run through RunProcess's exact ProcessStartInfo shape (UseShellExecute=false, CreateNoWindow=true, redirect stdout/err, stdin NOT redirected) exited with code 0 in 38ms - no 120s timeout. Isolates the runner claim from the pause claim.
3. No regression: de-elevated UIA - Gaming Tweaks card + all 3 toggle rows (Disable Preemption / Disable HDCP / Network Optimization) render; nothing fired on render; no errors; [WARMUP] 439.

### NOT driven (VM-checklist item, per every real-reboot/destructive path)

The real apply/revert runtime - applies real NIC driver + netsh global changes AND (post-fix) reboots - is destructive and reboot-triggering; NOT driven on any live desktop. isleap VM checklist: toggle Network Optimization -> the .bat applies the NIC/netsh tweaks -> RunProcess returns 0 -> marker persists -> app reboots via shutdown /r /t 5 (no more [TIMEOUT] -1). Same for revert.

### Not touched / not done

- Build #2 (net8) - read-only. Defender - not referenced. ConfirmNetworkRebootAsync copy left as-is (still accurate: a reboot happens after; not a UX change).
- Real NIC-change + reboot verification - isleap's disposable-VM step.
