<!-- refreshed: 2026-10-05 -->
# Architecture

**Analysis Date:** 2026-10-05

## System Overview

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                        AkariTool.App  (WinUI 3 EXE)                     │
│  MainWindow.xaml.cs  ·  Views/*Page.xaml(.cs)  ·  ViewModels/**          │
│  Features/{Common,Shared,Software}  ·  Services/  ·  Defender/ Nvidia/  │
│  Scripts/  ·  DI/UIServiceExtensions.cs                                   │
│  WinUI.Framework (vendored) · Microsoft.Extensions.DependencyInjection    │
└───────────────┬─────────────────────────────────┬───────────────────────┘
                │ ProjectReference                 │ ProjectReference
                ▼                                 ▼
┌───────────────────────────────┐   ┌─────────────────────────────────────┐
│  AkariTool.Core  (net10 lib)  │◄──┤ AkariTool.Infrastructure (net10 lib)│
│  Features/Common/{Models,     │   │ Features/Common/{Services,          │
│    Interfaces,Enums,Constants,│   │   Interfaces,Utilities,Events,Models}│
│    Events,Helpers,Native,     │   │ Features/Apps/Services              │
│    Services,Validation}       │   │ Features/Optimize/Services          │
│  Features/<Domain>/Catalogs/  │   │ Services/   (flat legacy dir)       │
│  Interfaces/ Models/ Tweaks/  │   │ DI/InfrastructureServiceExtensions  │
│  Competitive/                 │   └─────────────────────────────────────┘
└───────────────────────────────┘
                ▲
                │ ProjectReference (AkariTool.Core ← AkariTool.Infrastructure ← App)
                └─ Infrastructure DOES reference Core. App references BOTH.
                   Core references NEITHER. This is the only enforced rule.
```

Compile-time enforcement is **only** the project-reference graph declared in the
three `.csproj` files. There is no analyzer, no `ArchitectureTests` project, and no
`.editorconfig` rule enforcing namespace or dependency direction beyond that.

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Shell / router | Rail tags → `Page` types, hub drill-down, nav badges, log dock, theme | `src/AkariTool.App/MainWindow.xaml.cs` |
| App bootstrap | DI container build, `ServiceLocator.Initialize`, startup orchestration, `--competitive` argv | `src/AkariTool.App/App.xaml.cs` |
| UI DI | ViewModel + App-side service registrations, `SettingPageViewModel` marker enumeration | `src/AkariTool.App/DI/UIServiceExtensions.cs` |
| Infra DI | All `Infrastructure/Features/**` service registrations | `src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs` |
| Declarative setting model | `SettingDefinition` / `SettingGroup` / `BaseDefinition` — the tuning data model | `src/AkariTool.Core/Features/Common/Models/SettingDefinition.cs` |
| Catalog validation | Cross-group id uniqueness, registry-path shape, ComboBox mapping integrity | `src/AkariTool.Core/Features/Common/Validation/SettingCatalogValidator.cs` |
| Tuning catalogs | 7 declarative `*Optimizations` catalogs + 2 helper catalogs + 24 Software catalogs | `src/AkariTool.Core/Features/<Domain>/Catalogs/` |
| Page ViewModel base | Section build, compatibility gates, quick actions, restore point, search | `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs` |
| Row ViewModel | Per-setting read/apply/badges/technical details/power-plan specialisation | `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` |
| Apply engine | Registry write, PowerCfg, scheduled task, PowerShell, restart coalescing, change history | `src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs` |
| Read engines | `SettingStateReader` (per-row) and `SystemSettingsDiscoveryService` (batched) | `.../Features/Common/Services/SettingStateReader.cs`, `.../SystemSettingsDiscoveryService.cs` |
| Backup / profile | Export, preview, import, global search over all `SettingPageViewModel`s | `src/AkariTool.App/Services/SettingBackupService.cs` |
| Legacy static OS services | 40+ `public static` classes (elevation, Defender, WIM, presets, GPU, BCD) | `src/AkariTool.Infrastructure/Services/` |

## Pattern Overview

**Overall:** Layered three-tier (Core / Infrastructure / App) with a declarative
vertical-slice *intent* that is only partially realised, plus a large surviving
`static`-service layer from the pre-architecture WPF/net8 codebase.

**Key Characteristics:**
- **Declarative tuning pipeline (Track A).** A setting is a `SettingDefinition`
  record in a Core catalog; the App builds rows from it; Infrastructure reads and
  writes it. This is the modern path and covers all 11 `SettingPageViewModel`
  pages.
- **Bespoke legacy path.** AkariOS, Software, Advanced Tools, Backup, Verify, Home,
  Settings do *not* use `SettingDefinition`. They call `static` OS services directly
  and keep significant logic in `.xaml.cs` code-behind.
- **Service locator as a second DI channel.** DI is a real container
  (`App.ConfigureServices`), but 48 call sites reach through
  `WinUI.Framework.IoC.ServiceLocator.GetService<T>()` inside VMs and pages,
  bypassing constructor injection.
- **Interface wrapping over statics.** `Infrastructure/Services/*Wrapper.cs` are
  thin adapters exposing legacy `static` classes through Core interfaces, so the
  DI graph looks interface-based while the implementation is not.

## Layers

### AkariTool.Core — "pure models, interfaces, compiler-enforced"

- Purpose: models, enums, interfaces, event contracts, catalog data, P/Invoke
  declarations, and catalog validation.
- Location: `src/AkariTool.Core/`
- Contains: `Features/Common/{Models,Interfaces,Enums,Constants,Events,Helpers,Native,Services,Validation}`,
  `Features/<Domain>/Catalogs/`, `Features/Apps/{Interfaces,Models}`,
  `Features/AkariOS/Models`, `Interfaces/`, `Models/`, `Tweaks/`, `Competitive/`.
- Depends on: nothing but the BCL. **No** `Infrastructure`, **no** `App`.
- Used by: `AkariTool.Infrastructure` and `AkariTool.App`, plus both test projects.

**Documented constraint vs. reality.** `AkariTool.Core.csproj` states the intent
"pure models, interfaces, compiler-enforced". Verified deviations:

| Deviation | Evidence |
|-----------|----------|
| P/Invoke in Core | `Features/Common/Native/PowerProf.cs` (23 `DllImport`s to `PowrProf.dll`/`Kernel32.dll`), `Features/Common/Native/SrClientApi.cs` (`SrClient.dll`) |
| `Microsoft.Win32` in Core models | `RegistryValueKind` in `Models/RegistrySetting.cs`, `Models/SettingDefinition.cs`, `Models/SettingDefinitionToggleState.cs` and all 11 tuning catalogs |
| File I/O in Core | `Competitive/CompetitiveSession.cs` reads/writes `%AppData%` JSON via `File.*` / `Directory.CreateDirectory` |
| Environment paths in Core | `Features/Common/Constants/AkariPaths.cs` calls `Environment.GetFolderPath` in a `static readonly` field |
| Concrete service impl in Core | `Features/Common/Services/GlobalSettingsRegistry.cs` — a real `ConcurrentDictionary` implementation, not a model |
| WindowsAppSDK package ref | `AkariTool.Core.csproj` references `Microsoft.WindowsAppSDK 2.3.1`; **no Core file uses `Microsoft.UI.*`** — verified zero matches. The reference is unnecessary weight. |

None of these are *enforced* violations of the reference graph (Core still has no
reference to Infrastructure or App) — they are violations of the stated "pure
models" intent, and they are what make `AkariTool.Core.Tests` a
Windows-only, P/Invoke-coupled test project.

### AkariTool.Infrastructure — OS services and wrappers

- Purpose: every `IWindowsRegistryService`, power, filesystem, process, WinGet,
  and compatibility-filter implementation; hosts the remaining legacy statics.
- Location: `src/AkariTool.Infrastructure/`
- Contains: `Features/Common/{Services,Interfaces,Utilities,Events,Models}` (the
  migrated declarative stack), `Features/Apps/Services` (4 WinGet files),
  `Features/Optimize/Services` (1 file), `Services/` (40 flat files, legacy),
  `DI/`.
- Depends on: `AkariTool.Core`, `vendor/WinGet.Interop`, `System.Management`,
  `System.ServiceProcess.ServiceController`, `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.WindowsAppSDK`.
- Used by: `AkariTool.App` only.

**Verified oddity:** `Infrastructure/Features/Common/Services/DispatcherService.cs`
declares `using Microsoft.UI.Dispatching;` and takes a `DispatcherQueue`. Verified:
`grep` for `using (Microsoft\.UI|WinUI)` across all of `src/AkariTool.Infrastructure`
returns **zero** matches other than this file's transitive use. That is, the only
UI-framework dependency in Infrastructure is a UI-thread dispatcher that is
duplicated verbatim in the App layer (see Anti-Patterns).

### AkariTool.App — WinUI 3 shell

- Purpose: pages (XAML + code-behind), ViewModels, dialogs, backup/profile,
  embedded script and payload resources, App-side service registration.
- Location: `src/AkariTool.App/`
- Contains: `MainWindow.xaml(.cs)`, `App.xaml(.cs)`, `Views/`, `ViewModels/`,
  `Features/{Common,Shared,Software}/`, `Services/`, `Defender/`, `Nvidia/`,
  `Scripts/`, `DI/`, `Resource/`, `Assets/`.
- Depends on: `AkariTool.Core`, `AkariTool.Infrastructure`,
  `vendor/WinUI.Framework`, `Microsoft.WindowsAppSDK`, `CommunityToolkit.Mvvm`,
  `Material.Icons.WinUI3`, `FluentIcons.WinUI`, `System.Management`,
  `System.ServiceProcess.ServiceController`.

## Data Flow

### Primary Request Path — a declarative tuning setting

1. **Author the setting.** A `SettingDefinition` record is added to a `Build()`
   method in a Core catalog, e.g.
   `src/AkariTool.Core/Features/Gaming/Catalogs/GamingOptimizations.cs`.
2. **Startup warm-up.** `App.OnLaunched` starts a background `Task` running
   `StartupOrchestrator.RunAsync` (`src/AkariTool.App/App.xaml.cs:73`). Phase 3
   calls `SettingPageWarmUp.Run` (`src/AkariTool.App/Services/SettingPageWarmUp.cs:16`),
   which does `GetServices<SettingPageViewModel>()` and `Build()`s each page.
3. **Build + gate.** `SettingPageViewModel.Build()`
   (`src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:222`) runs
   `ApplyCompatibilityGates` (Windows-version then hardware filter), materialises
   one `SettingItemViewModel` per `SettingDefinition`, and wires dependency resolution.
4. **Render.** The page's `DataTemplateSelector`
   (`src/AkariTool.App/Views/Selectors/TweakRowTemplateSelector.cs`) picks a row
   template from `src/AkariTool.App/Views/Templates/TweakTemplates.xaml` by
   `InputType`.
5. **Detect.** Row calls `ISettingStateReader` or, for a page refresh,
   `ISystemSettingsDiscoveryService.GetSettingStatesAsync` (batched).
6. **Apply.** Row calls `ISettingOperationExecutor.ApplySettingOperationsAsync`
   (`src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs:31`).
7. **Side effects.** `IProcessRestartManager` coalesces Explorer/service restarts;
   `IChangeHistoryService` writes `%ProgramData%\AkariTool\ChangeHistory.txt`;
   nav badges recompute via `INavBadgeService` → `MainWindow.RefreshNavBadges`.

### Secondary Flow — a bespoke AkariOS action

1. Click handler in `src/AkariTool.App/Views/AkariOSPage.GpuTools.cs` (e.g. line 57)
   writes `Registry.SetValue` directly.
2. `AkariOSViewModel` (`src/AkariTool.App/ViewModels/AkariOS/AkariOSViewModel.cs`)
   orchestrates `static` services: `ElevationService`, `DefenderService`,
   `PlaybookTweaks`, `ServicesPreset`, `CompetitiveService`, `WimUtilService`.
3. Result surfaced through `ToolService.Log` → `AkariUiLogService.LineLogged` →
   the shell's log dock.

**State Management:**
- ViewModels are `CommunityToolkit.Mvvm` source-generated (`[ObservableProperty]`,
  `[RelayCommand]`) on `WinUI.Framework.Mvvm.ViewModelBase`.
- Page ViewModels are DI **singletons** (see CONCERNS — the lifetime is load-bearing
  because of the legacy `TweakRegistry`).
- OS state is never cached in a store; every read goes to the registry via
  `IWindowsRegistryService`. The only caches are `CompatibleSettingsRegistry`
  (startup filter results) and `GlobalSettingsRegistry` (bypassed settings).
- Cross-feature notification is a hand-rolled event bus:
  `src/AkariTool.Infrastructure/Features/Common/Events/EventBus.cs` behind
  `IEventBus`, with contracts in `Core/Features/Common/Events/`.
- `DriftBaseline` (`src/AkariTool.Infrastructure/Services/DriftBaseline.cs`) is a
  global static that records applied-state snapshots for the Verify page.

## Key Abstractions

**`SettingDefinition` / `SettingGroup`**
- Purpose: the declarative description of every tuning row and its section.
- Examples: `src/AkariTool.Core/Features/Common/Models/SettingDefinition.cs`,
  `.../Models/SettingGroup.cs`, `.../Models/BaseDefinition.cs`
- Pattern: immutable `sealed record` with `init` properties; `SettingGroup` requires
  `Name`, `FeatureId`, `Settings`.

**`ISettingStateReader` / `ISettingOperationExecutor`**
- Purpose: the read/write seam between a row and Windows.
- Examples: `src/AkariTool.Core/Features/Common/Interfaces/ISettingStateReader.cs`,
  `.../ISettingOperationExecutor.cs`
- Pattern: one interface per direction; `ISettingOperationExecutor` takes 11
  constructor dependencies and is registered via an explicit factory lambda in
  `InfrastructureServiceExtensions.cs:64`.

**`ISpecialSettingHandler` / `ISpecialSettingHandlerRegistry`**
- Purpose: escape hatch for settings the generic executor cannot express
  (composite reads/writes, e.g. the Windows Update policy dropdown).
- Examples: `src/AkariTool.Core/Features/Common/Interfaces/ISpecialSettingHandler.cs`,
  `src/AkariTool.Infrastructure/Features/Optimize/Services/WindowsUpdatePolicyHandler.cs`
- Pattern: registry keyed by setting `Id`, consulted first in both the apply
  (`SettingOperationExecutor.cs:36`) and discovery paths. **This is the seam a new
  composite setting must use** — it is the only supported way to leave the
  registry-generic path.

**Interface wrapper over static**
- Purpose: make legacy statics injectable.
- Examples: `src/AkariTool.Infrastructure/Services/ToolFetchServiceWrapper.cs`
  (12 lines, forwards to `ToolFetchService.LaunchAsync`),
  `.../UpdateServiceWrapper.cs`, `.../SystemInfoServiceWrapper.cs`,
  `.../ShaderCacheServiceWrapper.cs`
- Pattern: 4 wrappers exist. The other ~40 static services are *not* wrapped and
  are called directly from VMs and code-behind.

## Entry Points

**`App`**
- Location: `src/AkariTool.App/App.xaml.cs`
- Triggers: process launch.
- Responsibilities: `ConfigureServices()` builds the container and calls
  `ServiceLocator.Initialize(provider)`; `OnLaunched` captures the
  `DispatcherQueue`, wires `UnhandledException` to the log, activates `MainWindow`,
  parses `--competitive`, and kicks off `StartupOrchestrator` on a background task.

**`MainWindow`**
- Location: `src/AkariTool.App/MainWindow.xaml.cs`
- Triggers: resolved from DI by `App`.
- Responsibilities: owns `PageMap` (tag → `Page` type), the four detail-tag sets,
  `SelectRailTag` routing, `TagForPage` reverse mapping for rail highlight, nav
  badge aggregation, theme toggle, log dock, `RunDriftCheck`.

**CLI argument surface**
- `--competitive <exe>` handled in `App.ParseCompetitiveArgument`
  (`src/AkariTool.App/App.xaml.cs:107`). This is the only command-line contract.

**Build-time entry points**
- `build-installer.ps1` passes `/p:AkariPublish=true`.
- `build-deelevated.ps1` passes `/p:DeElevatedTest=true`.
- Both are consumed by conditional `PropertyGroup`s in
  `src/AkariTool.App/AkariTool.App.csproj:35` and `:50`.

## Architectural Constraints

- **Threading:** WinUI 3 single-threaded UI apartment. All page construction is
  forced onto a background thread during warm-up (`App.xaml.cs:73`), and
  `SettingPageViewModel.Build()` is documented as synchronous by design. Two
  verified `GetAwaiter().GetResult()` bridges sit on that path:
  `SettingPageViewModel.ApplyCompatibilityGates` (`:188`) and
  `PowerViewModel.Gate` (`:136`, `:139`). `ConfigureAwait(false)` appears 192
  times in `src/`; the UI-facing VMs use the default (`ConfigureAwait(true)`).
- **Global state:** module-level mutable statics reachable from any layer —
  `TweakRegistry` (`src/AkariTool.Infrastructure/Services/TweakRegistry.cs`,
  `static List<(TweakDefinition, Action)> _entries`), `DriftScanner.Last`,
  `DriftBaseline`, `ToolService.Current`, `ExplorerRestart`. The Competitive Mode
  session watcher also runs in-process.
- **Circular imports:** no circular *project* references (Core ← Infra ← App is
  acyclic). Namespace-level near-cycles exist: `AkariTool.Tabs` is declared in
  **Core** (`Features/Software/Catalogs/*`, `Features/Common/Constants/AkariPaths.cs`)
  **and** in **Infrastructure** (`Services/TweakRegistry.cs`, `Services/DriftScanner.cs`,
  `Services/PlaybookTweaks*.cs`, `Services/ServicesPreset*.cs`,
  `Services/SystemStateReader*.cs`) — same namespace, two assemblies.
- **Self-contained publishing:** `WindowsAppSDKSelfContained` must never reach the
  two class libraries, so the publish flag is a custom property (`AkariPublish`)
  mapped only in the App csproj. See the comment at
  `src/AkariTool.App/AkariTool.App.csproj:27`.
- **No analyzer gate:** nothing prevents a new Core file from taking a WinUI or
  `System.Diagnostics` dependency beyond what already exists.

## Anti-Patterns

### Layer discipline leaks: WinUI and registry in the "pure" layers

**What happens:** `Core` carries P/Invoke declarations and `Microsoft.Win32` types;
`Infrastructure` carries a `DispatcherQueue`-based service; `App` calls
`Registry.SetValue` and `Process.Start` from 18 files.

**Why it's wrong here:** the three-layer split is the only structural guarantee
the project has, and it is already porous. Every new feature has an equal chance
of landing on the wrong side of it, and `Core`'s P/Invoke makes the Core test
project Windows-native-bound.

**Do this instead:** add a new composite behaviour in
`src/AkariTool.Infrastructure/Features/<Domain>/Services/` behind a Core
interface, and register it in `InfrastructureServiceExtensions.AddAkariInfrastructure`.
Copy the shape of `Features/Optimize/Services/WindowsUpdatePolicyHandler.cs` (a
single ~490-line file, one setting, registered as both an
`ISpecialSettingHandler` and an `ISpecialDiscovery`).

### `ServiceLocator` used as a second DI channel

**What happens:** 48 `ServiceLocator.GetService<T>()` call sites across 27 App
files, including inside `SettingPageViewModel` (`CreateItem`, `:89-91`;
`RefreshAllFromSystem`, `:146`; `ApplyCompatibilityGates`, `:177`;
`ApplyAllRecommendedAsync`, `:334`) and every `Views/*Page.xaml.cs`.

**Why it's wrong here:** a page cannot be constructed in a test or a preview host
without a live global container, and the constructor parameters of the page VMs
(`ISettingStateReader`, `ISettingOperationExecutor`, `TweakDialogs`) are then
half-real and half-resolved-at-call-time. It also hides the App→Infrastructure
dependency: `SettingPageViewModel.cs:336` reaches for
`AkariTool.Infrastructure.Features.Common.Interfaces.IProcessRestartManager`
through the locator, so the file's `using` list understates its coupling.

**Do this instead:** when adding a page VM, add the dependency to the
`SettingPageViewModel` constructor and pass it down through `CreateItem`; that is
the pattern the base class was designed for, and `PowerViewModel.CreateItem`
(`src/AkariTool.App/ViewModels/PowerViewModel.cs:90`) is the one place that
extends it correctly.

### Statics wrapped in interfaces, then injected as if they were real

**What happens:** `IToolFetchService` → `ToolFetchServiceWrapper` → static
`ToolFetchService`. Same for `IUpdateService`, `ISystemInfoService`,
`IShaderCacheService`. Meanwhile `IElevationService`, `IDefenderService`,
`IGameDetection`, `IGpuTweaks`, `INvidiaProfile` and ~34 more have no interface
and are called directly.

**Why it's wrong here:** the DI graph advertises a testable architecture that
only 4 of ~44 services actually deliver. A developer reading
`InfrastructureServiceExtensions.cs` reasonably concludes the OS layer is
interface-driven; it is not.

**Do this instead:** when touching a new OS capability, register the real
implementation directly (`services.AddSingleton<IFooService, FooService>()`) and
skip the wrapper class entirely. The wrappers exist only to bridge
already-committed statics.

## Error Handling

**Strategy:** three tiers, selected by blast radius.

**Patterns:**
- **Operation results over exceptions on the write path.**
  `OperationResult` (`src/AkariTool.Core/Features/Common/Models/OperationResult.cs`)
  is returned by `ISettingOperationExecutor`; the UI shows a dialog rather than
  propagating. `BackupResult` does the same for restore points.
- **Try/catch-all at process boundaries.** 229 `catch (Exception ...)` sites in
  `src/`. The dominant idiom is "log, then degrade", e.g.
  `src/AkariTool.App/Services/StartupOrchestrator.cs:32` swallows a Phase-1 failure
  so Phases 2 and 3 still run.
- **Best-effort with empty catch and a comment.** 35 sites match
  `catch { /* ... */ }` — always with an inline reason, e.g.
  `src/AkariTool.Infrastructure/Services/DriftScanner.cs:184`
  (`/* diagnostics must never break startup */`),
  `src/AkariTool.App/Features/Shared/UiPreferences.cs:31`
  (`/* best-effort — a lost UI preference must never break the tab */`).
  There is **no** bare `catch {}` without a comment.
- **Unobserved-task pattern.** Several fire-and-forget calls use `_ = ...` to
  silence CS4014, e.g. `src/AkariTool.App/App.xaml.cs:73`. Exceptions inside these
  are only observable through the log service.

## Cross-Cutting Concerns

**Logging:** two channels that converge on the same file.
`WinUI.Framework.Services.ILogService` is the framework contract; the App wraps
`FileLogService` in `AkariUiLogService`
(`src/AkariTool.App/Services/AkariUiLogService.cs`), which decorates and raises
`LineLogged` so the shell's log dock can render. `ToolService` is a DI singleton
built around an `Action<string>` sink wired to `ILogService.Info`
(`src/AkariTool.App/DI/UIServiceExtensions.cs:34`). Infrastructure logs through
`IAkariLogService`, whose App-side implementation forwards into `ToolService`
(`UIServiceExtensions.cs:42`). So: Infrastructure → `IAkariLogService` → `ToolService`
→ `ILogService` → `FileLogService`. There is also a static escape hatch,
`ToolService.Current?.Log(...)`, used 35 times.

**Validation:** three independent layers, none of them wired to the build.
1. `SettingCatalogValidator` (`src/AkariTool.Core/Features/Common/Validation/`)
   checks id uniqueness, registry hive/subkey shape, ComboBox→`ValueName`
   mapping integrity, and the Selection recommended/default shape. It is exercised
   by `tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs` but is
   **never called from production code** — verified zero non-test call sites. Nothing
   fails the build when a catalog violates an invariant.
2. `IWindowsCompatibilityFilter` / `IHardwareCompatibilityFilter` —
   `SettingPageViewModel.ApplyCompatibilityGates` (`src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs:175`).
3. `IPowerSettingsValidationService` — Power-only existence gate in
   `PowerViewModel.Gate` (`src/AkariTool.App/ViewModels/PowerViewModel.cs:130`).
   Plus `BuildVersionGate` (`src/AkariTool.Core/Features/Common/Helpers/`) and
   `SupportedBuildRanges` on `SettingDefinition`.

**Authentication:** not applicable. The app is a single-user, `requireAdministrator`
local desktop utility; there is no identity, no session, no licensing. The
elevation model is `app.manifest` + `src/AkariTool.Infrastructure/Services/ElevationService.cs`.

---

*Architecture analysis: 2026-10-05*
