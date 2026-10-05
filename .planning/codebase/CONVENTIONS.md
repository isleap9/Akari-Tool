# Coding Conventions

**Analysis Date:** 2026-10-05

> There is **no `.editorconfig`, no `Directory.Build.props`, no `global.json`, and no
> analyzer package** in the repo (verified). Every convention below is observed
> practice inferred from the source, not machine-enforced. Where the codebase is
> internally inconsistent, the inconsistency is documented rather than resolved —
> pick the dominant pattern and say so in your plan if you deviate.

## Naming Patterns

**Files:**
- One public type per file; **filename == type name**. No exceptions found.
- Partial classes are split by concern with a `.` separator, and the concern is a
  *noun phrase*, not a generic "Partial1":
  - `src/AkariTool.Infrastructure/Services/PlaybookTweaks.Registry.cs`
  - `src/AkariTool.Infrastructure/Services/SystemStateReader.Desktop.cs`
  - `src/AkariTool.App/Views/AkariOSPage.GpuTools.cs`
  - `src/AkariTool.Core/Features/Software/Catalogs/ExternalAppCatalog.Browsers.cs`
- UI pairing: `<Name>Page.xaml` + `<Name>Page.xaml.cs`. For XAML pages with multiple
  code-behinds, the primary is `<Name>Page.xaml.cs` and the rest are
  `<Name>Page.<Concern>.cs`.
- Service/behaviour suffixes: `Service`, `Wrapper`, `Registry`, `Manager`,
  `Controller`, `Provider`, `Resolver`, `Reader`, `Executor`, `Filter`, `Helper`,
  `Scanner`, `Baseline`, `Applier`.
- Catalog naming is `*Optimizations` for declarative tuning data
  (`GamingOptimizations`) and `*Catalog` / `*Catalogs.<Category>` for app catalogs
  (`ExternalAppCatalog.Browsers`).
- Tests: `<TypeUnderTest>Tests.cs`, one class per file, xUnit + FluentAssertions.

**Functions:**
- `PascalCase`, verbs first: `Build()`, `Apply()`, `Read()`, `Filter()`, `Get()`.
- Async methods carry the `Async` suffix consistently —
  `ApplyAllRecommendedAsync`, `CreateRestorePointAsync`,
  `GetSettingStatesAsync`, `PreloadAllSettingsAsync`, `FilterSettingsByExistenceAsync`.
- Predicate functions read as assertions: `IsNew`, `HasRecommendedQuickSet`,
  `IsUltimatePerformancePlan`, `IsCompatible`, `FilterSettingsByWindowsVersion`.
- **Never** name a boolean parameter `b` except in a tiny local scope. In catalogs,
  booleans are usually named after the semantic (`isOn`, `isRecommended`,
  `preference`), and named arguments are used at call sites where meaning matters.

**Variables:**
- `camelCase` for locals and parameters; `_camelCase` for private fields.
- Readonly dependencies are `private readonly ISettingStateReader _stateReader;`.
- **A leading underscore means private — but the codebase is inconsistent about
  `this.` qualification inside constructors.** Roughly half the ViewModel
  constructors assign with `this.` (e.g.
  `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs:77-88`) and half
  assign bare (e.g. `src/AkariTool.App/ViewModels/PowerViewModel.cs:65-71`).
  Follow the file you are editing.
- Boolean locals are prefixed `is`/`has`/`should` where it aids reading
  (`hasBattery`, `_hasBattery`, `_xmlAdded`, `_busy`, `_extractionDone`).
- `CTS` is always `cts`; `CancellationTokenSource` fields are `private CancellationTokenSource? _cts;`.

**Types:**
- Interfaces: `I` + PascalCase noun (`ISettingStateReader`, `IPowerCfgApplier`,
  `ISpecialDiscoveryRegistry`).
- The one `I`-less service is deliberate and legacy: `ToolService` (see
  `src/AkariTool.App/DI/UIServiceExtensions.cs:34`, which registers the concrete
  type and then aliases it to `IToolService`).
- Models are `sealed record` with `init` accessors — this is the dominant idiom
  for the declarative stack:
  ```csharp
  // src/AkariTool.Core/Features/Common/Models/SettingGroup.cs
  public sealed record SettingGroup
  {
      public required string Name { get; init; }
      public required string FeatureId { get; init; }
      public required IReadOnlyList<SettingDefinition> Settings { get; init; }
  }
  ```
- `required` is used for genuinely mandatory members
  (`BaseDefinition.Id/Name/Description`, `SettingGroup.Name/FeatureId/Settings`).
- `BaseDefinition` is the abstract base for anything renderable as a setting row.
- ViewModels are `sealed partial class X : ViewModelBase` (framework base) or
  `: ObservableObject` where a lighter base suffices
  (`SettingItemViewModel : ObservableObject, ISettingRowViewModel`).
- Primary-constructor classes appear in the newest Infrastructure code:
  `public class SettingOperationExecutor(IWindowsRegistryService registryService, …) : ISettingOperationExecutor`
  (`src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs:15`)
  and `public sealed class StartupOrchestrator(ICompatibleSettingsRegistry …)`
  (`src/AkariTool.App/Services/StartupOrchestrator.cs:21`). Both still copy the
  parameters into explicit `private readonly` fields. **Prefer the traditional
  ctor for ViewModels** (source generators need an unambiguous class shape) and
  primary constructors for plain services.

## Code Style

**Formatting:**
- No formatter config. Observed: 4-space indent, Allman braces, `(a, b)` tuple
  spacing, trailing commas in multi-line initialiser lists, and collection
  expressions `[]` / `[x, y]` for empty/small collections (C# 12+).
- Region-free. Large classes are delimited by `// ── Section name ─────` comment
  banners instead of `#region` — see `SettingPageViewModel.cs:272`,
  `SettingOperationExecutor.cs`, `SystemUtilities.cs:16`.
- `var` is used for locals whose type is obvious from the right-hand side
  (`var states = discovery.GetSettingStatesAsync(...)`) and avoided for
  dependency fields.
- Early-return guard clauses are preferred over nesting.

**Linting:**
- None configured. `Nullable` is `enable` in all four project files, and
  `ImplicitUsings` is `enable`, so `using System;` etc. are omitted.
- Nullable annotations are used honestly on optional dependencies
  (`IEventBus? _eventBus`) with the null-coalescing / `?.` fallbacks in place.
- `catch { }` with no body is **not** used; every swallow has an inline reason
  (see Error Handling).

## Import Organization

**Order (observed, not enforced):**
1. `System.*` / `System.*.*`
2. Third-party: `Microsoft.UI.*`, `Microsoft.Extensions.*`, `CommunityToolkit.*`,
   `WinUI.Framework.*`
3. First-party, most-specific-to-least:
   `AkariTool.Core.Features.<Domain>.Models` → `AkariTool.Core.Features.<Domain>.Interfaces`
   → `AkariTool.Core.*` → `AkariTool.Infrastructure.*` → `AkariTool.ViewModels.*`
   → `AkariTool.Services` → `AkariTool.Tabs.<Domain>`
4. `namespace AkariTool.*;` last.

**Within a group, do not alphabetise.** `SettingPageViewModel.cs:1-17` orders
`AkariTool.Core…` then `WinUI.Framework.Mvvm` then more `AkariTool.Core…` then
`AkariTool.Infrastructure…` — grouped by conceptual area, not sorted.

**Fully-qualified names are used for two specific escapes, and only these two:**
- Infrastructure utilities reached from App ViewModels:
  `AkariTool.Infrastructure.Features.Common.Utilities.NumericConversionHelper`
  (`src/AkariTool.App/ViewModels/Tweaks/SettingBadgeCalculator.cs:382`,
  `.../SettingItemViewModel.cs:758`)
- Services reached through the service locator inside a method body:
  `WinUI.Framework.IoC.ServiceLocator.GetService<AkariTool.Core.Features.Common.Interfaces.ISystemSettingsDiscoveryService>()`
  (`src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:91`)

**Path aliases:** none. No `<RootNamespace>`-relative resolution tricks; every
cross-project reference is a full namespace + a `ProjectReference`.

**Namespace does not match the folder.** This is the single biggest convention
hazard. Verified namespace distribution:

| Assembly | Namespace | Files | Folder |
|----------|-----------|-------|--------|
| Core | `AkariTool.Core.Features.*` | ~110 | `Features/` ✅ matches |
| Core | `AkariTool.Tabs` / `.Customize` / `.Gaming` / `.Notifications` / `.Power` / `.Privacy` / `.Sound` / `.Update` | 37 | `Features/*/Catalogs/` ❌ |
| Core | `AkariTool.Tabs` | 1 | `Features/Common/Constants/AkariPaths.cs` ❌ |
| Infrastructure | `AkariTool.Infrastructure.Features.*` | 52 | `Features/` ✅ |
| Infrastructure | `AkariTool.Tabs` | 18 | `Services/` ❌ |
| Infrastructure | `AkariTool.Services` | 19 | `Services/` ❌ |
| App | `AkariTool.Views` / `.ViewModels` / `.Services` | 69 | matches ✅ |
| App | `AkariTool.Tabs` | 2 | `Features/Software/`, `Features/Shared/` ❌ |
| App | `AkariTool.Features.Common.*` | 5 | `Features/Common/` ✅ |

Note the collision: `AkariTool.Tabs` is declared in **three** assemblies (Core,
Infrastructure, App). `AkariPaths` is defined **twice** in that namespace —
`src/AkariTool.Core/Features/Common/Constants/AkariPaths.cs:9` and
`src/AkariTool.App/Features/Software/SoftwareAppService.cs:21` — with
byte-identical members. See CONCERNS.

## File Organization

- One concern per file, except data-heavy catalogs which are intentionally one
  huge file (`GamingOptimizations.cs` is 3,427 lines).
- A feature folder groups by *role* (`Models/`, `Interfaces/`, `Services/`),
  not by use case. A new role needs a new folder; a new use case does not.
- Large legacy classes are split into `partial` files rather than extracted into
  new types. `PlaybookTweaks` = 6 partials / 76 KB. `SystemStateReader` = 7
  partials / 71 KB. `ServicesPreset` = 4 partials / 118 KB. `AkariOSPage` = 9
  partials / 2,631 lines.
- New Infrastructure code goes under `Features/<Domain>/Services/`; the flat
  `Infrastructure/Services/` directory is legacy and should not grow.

## How to Add a New Setting

This is the highest-frequency task in the project, and for a setting that is a
registry write or a PowerCfg write it is a **single-file change**.

1. **Author the definition.** Add a `SettingDefinition` to the matching
   `Build()` in `src/AkariTool.Core/Features/<Domain>/Catalogs/<Domain>Optimizations.cs`:
   ```csharp
   new SettingDefinition
   {
       Id          = "domain-setting-id",          // globally unique, hyphenated
       Name        = "Human Name",
       Description = "What it does.",
       GroupName   = "Section label",
       InputType   = InputType.Toggle,             // Toggle | NumericRange | Selection | CheckBox
       Icon        = "Palette",                    // Material path, resolved by IconConverter
       IconPack    = "Material",                   // default
       RegistrySettings = new[]
       {
           new RegistrySetting
           {
               KeyPath           = @"HKEY_CURRENT_USER\Software\Vendor\Feature",
               ValueName         = "Enabled",
               ValueType         = RegistryValueKind.DWord,
               RecommendedValue  = 1,
               DefaultValue      = 0,
           },
       },
   }
   ```
2. **Do not touch** the ViewModel, XAML, DI, or `MainWindow`. The row, its
   template, its badges, its Quick-Action participation, its search entry, and its
   backup-export entry are all derived.
3. **Gating is declarative, not code.** Use
   `RequiresBattery` / `RequiresLid` / `RequiresDesktop` /
   `RequiresBrightnessSupport` / `RequiresHybridSleepCapable` (hardware filter),
   `SupportedBuildRanges` / `MinimumBuildNumber` / `MaximumBuildNumber`
   (Windows filter), `IsWindows10Only` / `IsWindows11Only`, `ValidateExistence`
   (Power's existence gate), `AddedInVersion` (NEW badge).
4. **Recommendation / default shape matters** and is validated by
   `SettingCatalogValidator` — but *not in the build*. Enforce it by hand:
   - Standard `Selection`: **exactly one** option with `IsRecommended` and
     **exactly one** with `IsDefault`.
   - `IsSubjectivePreference = true`: at most one of each.
   - `PowerCfgSettings` present: **no** per-option flags — use
     `PowerRecommendation` and `PowerCfgSetting.DefaultValueAC/DC`.
   - `Recommendation.LoadDynamicOptions = true`: skipped entirely.
5. **If the generic executor cannot express it**, add
   `ISpecialSettingHandler` under
   `src/AkariTool.Infrastructure/Features/<Domain>/Services/` and register it in
   **both** `ISpecialSettingHandlerRegistry` and `ISpecialDiscoveryRegistry` in
   `src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs`. Template:
   `src/AkariTool.Infrastructure/Features/Optimize/Services/WindowsUpdatePolicyHandler.cs`.
6. **Verify uniqueness.** Ids are the backup-file key; a duplicate silently breaks
   profile import. `SettingCatalogValidator.Validate(IEnumerable<SettingGroup>)`
   checks this — call it from a test.

## How to Add a New Page

A new *declarative tuning page* is the only well-trodden path. Six touch points,
all listed in STRUCTURE.md § "Where to Add New Code". In order:

1. `src/AkariTool.Core/Features/<NewDomain>/Catalogs/<NewDomain>Optimizations.cs` —
   `public static class … { public static IReadOnlyList<SettingGroup> Build() => …; }`
2. `src/AkariTool.App/ViewModels/<NewDomain>ViewModel.cs` — subclass
   `SettingPageViewModel`, override `NavTag` / `NavLabel` / `BuildSettingGroups()`.
3. `src/AkariTool.App/Views/<NewDomain>Page.xaml` + `.xaml.cs` — thin code-behind
   (see `TaskbarPage.xaml.cs`, 28 lines: resolve from `ServiceLocator`, set
   `DataContext`, call `Build()`).
4. `src/AkariTool.App/DI/UIServiceExtensions.cs` — **two** registrations, both
   required: the concrete VM, then the `SettingPageViewModel` marker alias. The
   marker list is what warm-up enumerates; omitting it means the page is not
   built until first navigation, which breaks backup export and global search.
5. `src/AkariTool.App/MainWindow.xaml.cs` — a `PageMap` entry, plus membership in
   a `*DetailTags` set if it is a hub detail, plus a `HubCardViewModel` in the
   hub's code-behind.
6. Icons: add a `.png` to `src/AkariTool.App/Resource/NavIcons/` — but note that
   directory is **not** declared in the csproj, so verify how the existing icons
   are actually resolved before adding a 20th.

**Order is load-bearing.** In `UIServiceExtensions.cs`, registration order is the
warm-up order *and* the legacy `TweakRegistry` range order, and the comment at
`:85-89` records that this ordering was chosen to keep the flat Backup export
byte-identical to the old `CustomizeViewModel` order. Insert in the position your
page belongs in that sequence, not at the end.

## ViewModel Patterns

**Page ViewModel (the 11 declarative pages):**

```csharp
// src/AkariTool.App/ViewModels/SoundViewModel.cs — the minimal template
public sealed partial class SoundViewModel : SettingPageViewModel
{
    public SoundViewModel(
            ISettingStateReader stateReader,
            ISettingOperationExecutor executor,
            TweakDialogs dialogs,
            ISettingDependencyResolver? dependencyResolver = null,
            /* …optional: localizationService, dispatcherService,
                   regeditLauncher, eventBus… */)
            : base(stateReader, executor, dialogs, /* … */)
    {
        Title = "Sound";
        Subtitle = "Audio playback and communication settings.";
    }

    public override string NavTag => "Sound";
    public override string NavLabel => "Sound";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups()
        => SoundOptimizations.Build();
}
```

Rules that apply to all of them:
- `NavTag` must exactly match the `PageMap` key in `MainWindow.xaml.cs` and the
  rail tag in `MainWindow.xaml`. It is the join key for nav badges, global search,
  and backup restore.
- `Title` / `Subtitle` are set in the ctor, not as `[ObservableProperty]`.
- `newBadgeService: null` is passed explicitly at every call site even though the
  parameter is optional — a leftover from an earlier design; keep it for
  consistency with the 10 siblings.
- **Power is the only page that overrides anything structural**: it overrides
  `CreateItem` to inject plan-special services
  (`src/AkariTool.App/ViewModels/PowerViewModel.cs:90`) and
  `AdditionalResolutionCatalogs()` to declare its cross-catalog dependency on
  Privacy (`:85`).
- `GamingViewModel` is the only page that injects a bespoke row
  (`DefenderToggleViewModel`, `src/AkariTool.App/ViewModels/GamingViewModel.cs:29`).
  The comment explains why: a servicing-package removal is not expressible as a
  `SettingDefinition`.

**Row ViewModel:** `SettingItemViewModel` (979 lines) is constructed by
`SettingPageViewModel.CreateItem`, which is `virtual` precisely so Power can
specialise it. It is **not** DI-registered — it is created per `SettingDefinition`.

**Bespoke pages** (Software, AkariOS, Backup, Verify, Advanced Tools, Home,
Settings) do not follow a single pattern. `AkariOSViewModel` is a DI singleton
that owns nothing and orchestrates statics; `ImportReviewDialog` is a 19.6 KB
code-behind "view model"; `AdvancedToolsPage` puts its logic in
`.xaml.cs` partials and exposes `Wim` / `Xml` properties off the VM. Treat these
as precedent, not as a rule to follow.

## Async Patterns

- **`ConfigureAwait(false)` is the default in Infrastructure** (192 occurrences in
  `src/`). It is omitted in App-layer code so continuations return to the UI
  thread — `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:440`
  spells out `.ConfigureAwait(true)` explicitly because the author wanted the
  intent visible at a call site where the surrounding method is async.
- **`GetAwaiter().GetResult()` is confined to the synchronous warm-up path**, and
  every use carries a comment justifying it. The 10 sites are:
  - `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:151`
    (page refresh, via discovery service)
  - `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:188`
    (hardware compatibility filter inside `Build()`)
  - `src/AkariTool.App/ViewModels/PowerViewModel.cs:136` (battery probe)
  - `src/AkariTool.App/ViewModels/PowerViewModel.cs:139` (existence filter)
  - plus 6 in `src/AkariTool.App/Services/StartupOrchestrator.cs` / warm-up code.
  The rule: `Build()` is synchronous because startup runs it on a background
  thread (`App.xaml.cs:73`). **Do not introduce a new blocking bridge outside
  this path.**
- **Cancellation** flows as an explicit `CancellationToken` parameter, never
  ambient. `ITaskProgressService.StartTask(...)` returns a CTS; the caller checks
  `cts.IsCancellationRequested` in its loop
  (`src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:355`).
- **Fire-and-forget is expressed as `_ = ...`** to satisfy CS4014
  (`src/AkariTool.App/App.xaml.cs:62`, `:73`, `:84`). There is no
  central exception funnel for these — only the `UnhandledException` handler at
  `App.xaml.cs:48` and the log service.
- **Startup phases are sequential, never parallel.** The comment at
  `src/AkariTool.App/App.xaml.cs:70` says so explicitly, and
  `src/AkariTool.App/Services/SettingPageWarmUp.cs:20` iterates pages in
  registration order. Preserve that.
- **Bulk applies suppress side effects then flush once**:
  `using (restartManager?.SuppressRestarts() ?? NullScope.Instance) { … }` followed
  by a single `await restartManager.FlushCoalescedRestartsAsync(appliedDefs)`
  (`src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:350-369`).
  `SuppressRestarts()` returns a disposable, so the `using` shape is mandatory.
  Copy this rather than re-implementing the pattern.
- **Bulk applies are wrapped in one change-history batch:**
  `using var historyBatch = changeHistory?.BeginBatch("Apply Recommended");`
  (`:349`).

## Logging

- **One chain, three hops.** Infrastructure and App code log through
  `IAkariLogService` (a Core-adjacent interface) or the framework `ILogService`.
  The physical chain is:
  `logService.Log(...)` → `AkariLogService` (App) → `ToolService.Log` →
  `AkariUiLogService.Log` → `FileLogService`, with `AkariUiLogService.LineLogged`
  fanning out to the shell's log dock
  (`src/AkariTool.App/Services/AkariUiLogService.cs:42`).
- **The dominant call is `logService.Log(LogLevel.Info, $"[Component] message")`.**
  233 occurrences in `src/`. **Always prefix with a bracketed component tag**
  matching the emitting type: `[SettingOperationExecutor]`,
  `[PowerPlanComboBoxService]`, `[WARMUP]`, `[STARTUP]`, `[VERIFY]`, `[RESTORE]`,
  `[App]`. This is a strong, consistently applied convention.
- **`ToolService.Current?.Log(...)`** is the legacy static escape hatch, 35
  occurrences. Prefer injecting `IAkariLogService`. If you must use the static
  form, the null-conditional is mandatory — it is `null` in tests.
- **Views also log** through `ToolService` — e.g.
  `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:457`. Acceptable
  in a catch block where no logger is in scope.
- **Never log a secret.** No such pattern was found, and none should be added —
  `System.Management`/WMI queries and registry reads must be logged by key path
  and value only when the value is a setting's intended state.

## Error Handling

- **Prefer a result record over an exception** for anything the user can trigger
  and that has a recoverable failure. `OperationResult` for applies,
  `BackupResult` for restore points, `ProcessExecutionResult` / `ProcessResult`
  for process runs, `SettingStateResult` for reads.
- **Catch-all is the norm** (229 `catch (Exception …)` sites) and is used
  deliberately at process boundaries where one failure must not abort a phase —
  `src/AkariTool.App/Services/StartupOrchestrator.cs:32-36` swallows Phase 1 so
  Phases 2 and 3 still run.
- **A swallowed exception must carry an inline reason.** 35 sites use
  `catch { /* reason */ }`; **zero** bare `catch {}` were found. The reasons are
  specific and worth matching:
  - `src/AkariTool.Infrastructure/Services/DriftScanner.cs:184` —
    `/* diagnostics must never break startup */`
  - `src/AkariTool.App/Features/Shared/UiPreferences.cs:31` —
    `/* best-effort — a lost UI preference must never break the tab */`
  - `src/AkariTool.Infrastructure/Services/CompetitiveService.cs:681` —
    `/* the watcher must never crash the app */`
- **Never swallow silently in a loop.** Per-item failures are counted or
  collected, not dropped: `catch { failures.Add("CPU sets"); }`
  (`CompetitiveService.cs:231`), `catch { unreadable++; }`
  (`DriftScanner.cs:133`), `catch { failed++; }`
  (`src/AkariTool.App/ViewModels/Verify/VerifyViewModel.cs:170`).
- **Unreadable ≠ absent** is an explicit rule in this codebase; see
  `CompetitiveService.cs:659` — `catch { running = true; } // unreadable ≠ exited`.
- Log the exception (`log.Error(msg, ex)`) whenever a user-visible action fails.
  The framework's `ILogService.Error(string, Exception)` overload carries both.

## Comments and Docs

- **XML doc comments on public Core contracts and every non-obvious service.** Most
  `src/AkariTool.Core/Features/Common/Interfaces/*.cs` files have a one-line `<summary>`.
- **Long architectural comments are the house style.** The dominant pattern is a
  `///` block at the top of a class explaining *why* it exists and what it
  replaced — including migration history:
  > "Declarative replacement for TweakPageViewModel. Builds its sections from
  > SettingGroup records instead of TweakDefinition catalog arrays."
  > `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:19`
  These are load-bearing documentation. Preserve them when editing.
- **Cross-references use `<see cref="…"/>`** extensively, including to types in
  other assemblies. Keep these updated when renaming.
- **Winhance parity is annotated inline.** The codebase was ported from a
  reference implementation (Winhance), and every ported class says so:
  `/// Winhance 1:1 port`, `/// (Winhance SystemSettingsDiscoveryService 1:1)`,
  `/// 4h — Winhance PowerPlanComboBoxService parity`. Also annotates what was
  **not** ported and why — e.g. `SettingDefinition.cs:99-104`:
  > "Not in Winhance — Winhance carries only a bool RequiresConfirmation …
  > these give toggles a one-directional message."
  Continue this. A divergence from the reference is a design decision that
  belongs in a comment, not in someone's memory.
- **⚠ and 🔍 markers** are used to flag sharp edges to future readers:
  `⚠ SINGLETON, not transient` (`src/AkariTool.App/DI/UIServiceExtensions.cs:74`),
  `⚠ Bespoke, registers NOTHING with TweakRegistry`
  (`src/AkariTool.App/ViewModels/AkariOS/AkariOSViewModel.cs:29`),
  `⚠ SINGLETON: rows register with TweakRegistry on construction`
  (`src/AkariTool.App/Views/TaskbarPage.xaml.cs:20`). Use them for the same
  purpose.
- **Provenance comments on moved code.** Ported files state their origin:
  "MVVM PORT: carried over VERBATIM from the net8 factory partial
  `TweakHelpers.BulkActions.cs`" (`src/AkariTool.Core/Tweaks/TweakTargets.cs:6`),
  "MVVM PORT: ReadUiPref / WriteUiPref carried over verbatim … so a user's
  collapsed sections survive the move between builds"
  (`src/AkariTool.App/Features/Shared/UiPreferences.cs:8`). This is how
  byte-compatibility guarantees are documented. **Any change that breaks a
  documented byte-compatibility promise must update the comment.**
- Section banners use `// ── Name ─────` (box-drawing U+2500), not `#region`.
- **No `TODO` / `FIXME` / `HACK` markers anywhere in `src/`** — verified zero
  matches. Outstanding work is tracked in `.planning/`, not in code comments.

## Module Design

- **One public type per file; no barrel/`_` index files.** Namespaces are
  organised by folder, not by re-export.
- **Registries are explicit dictionaries, not reflection.** See
  `ISpecialSettingHandlerRegistry` wired with a literal
  `new Dictionary<string, ISpecialSettingHandler> { ["updates-policy-mode"] = … }`
  (`src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs:35-39`).
  Adding a handler means editing that dictionary.
- **Feature ids are `const string` in a single file**
  (`src/AkariTool.Core/Features/Common/Constants/FeatureIds.cs`), not enums, so
  persisted values stay stable across renames. Follow that for any new key space.
- **Utility classes are `internal static`** when Infrastructure-local
  (`PowerPlanHelper`, `ValueComparer`, `RegistryValueFormatter` in
  `src/AkariTool.Infrastructure/Features/Common/Utilities/`) and `public static`
  when they must be reached from App (`NumericConversionHelper`).
  `NumericConversionHelper` is `public` precisely because the App's
  `SettingBadgeCalculator` and `SettingItemViewModel` call it.
- **Interfaces are declared in Core, implemented in Infrastructure, except where a
  dependency forces otherwise** — and those exceptions are documented at the
  registration site. `UIServiceExtensions.cs:52-60` is the pattern:
  > "Impl lives App-side: TweakDialogs + vendored ISettingsService deps. Interface is Core."

  For `DispatcherService` the reason is different and worse: the same 154-line
  class exists in **both** layers (see CONCERNS).

---

*Convention analysis: 2026-10-05*
