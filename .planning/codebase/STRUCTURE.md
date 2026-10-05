# Codebase Structure

**Analysis Date:** 2026-10-05

## Directory Layout

```text
Akari-Tool-OLD/
├── AkariTool.sln                 # 5 projects + 2 solution folders
├── build-installer.ps1           # passes /p:AkariPublish=true
├── build-deelevated.ps1          # passes /p:DeElevatedTest=true
├── AKARI_ARCHITECTURE_PLAN.md    # net8 → 3-layer migration plan (Session 0-6)
├── AKARI_NEXT_PHASE.md
├── docs/                         # static HTML site (404/index/features/changelog/…)
├── installer/                    # installer payload
├── vendor/
│   ├── WinUI.Framework/          # vendored WinUI 3 framework (source ProjectReference)
│   └── WinGet.Interop/           # WinGet COM interop
├── src/
│   ├── AkariTool.Core/           # 0 project refs; ~155 .cs
│   ├── AkariTool.Infrastructure/ # → Core; ~85 .cs
│   └── AkariTool.App/            # → Core, → Infrastructure; ~164 .cs + 37 .xaml
└── tests/
    ├── AkariTool.Core.Tests/           # 8 test files
    └── AkariTool.Infrastructure.Tests/ # 12 test files
```

Full `src/` tree (excluding `bin/`, `obj/`, `vendor/`):

```text
src/AkariTool.Core/
├── Competitive/                        CompetitiveOptions.cs, CompetitiveSession.cs
├── Interfaces/                         IShaderCacheService, ISystemInfoService,
│                                       IToolFetchService, IToolService, IUpdateService
├── Models/
│   ├── Actions/RunActions.cs
│   ├── ShaderCache/                    ShaderCacheTarget, ShaderCacheScanResult,
│   │                                   ShaderCacheCleanResult
│   ├── Update/                         ReleaseInfo, UpdateCheckResult, UpdateStatus
│   └── SystemInfo.cs
├── Tweaks/                             TweakDefinition.cs, TweakTargets.cs
└── Features/
    ├── Common/                         ← the shared kernel
    │   ├── Constants/                  AkariPaths, ComboBoxConstants, FeatureIds,
    │   │                               UserPreferenceKeys
    │   ├── Enums/                      InputType, DetectionType, LogLevel, RunContext,
    │   │                               ScriptOption, SettingBadgeKind, SettingBadgeMode
    │   ├── Events/
    │   │   ├── Settings/               SettingAppliedEvent
    │   │   ├── UI/                    FilterStateChangedEvent, SettingsRefreshedEvent,
    │   │   │                           TooltipUpdatedEvent
    │   │   ├── IDomainEvent, IEventBus, ISubscriptionToken,
    │   │   ├── BuilderModeExitedEvent, ReviewModeExitedEvent, PowerPlanChangedEvent
    │   ├── Helpers/BuildVersionGate.cs
    │   ├── Interfaces/                 32 files: ISettingItem, ISettingStateReader,
    │   │                               ISettingOperationExecutor, ISettingDependencyResolver,
    │   │                               IChangeHistoryService, ITooltipDataService,
    │   │                               IGlobalSettingsRegistry, IPowerService, …
    │   ├── Models/                     27 files: SettingDefinition, BaseDefinition,
    │   │                               SettingGroup, RegistrySetting, ComboBoxOption,
    │   │                               SettingStateResult, OperationResult, …
    │   ├── Native/                     PowerProf.cs (P/Invoke), SrClientApi.cs (P/Invoke)
    │   ├── Services/GlobalSettingsRegistry.cs
    │   └── Validation/SettingCatalogValidator.cs
    ├── AkariOS/Models/                 BcdOperation, PlaybookTweakAction, ServicePresetKind
    ├── Apps/
    │   ├── Interfaces/IWingetServices.cs
    │   └── Models/WingetModels.cs
    ├── Customize/Catalogs/             5 files  ← 5 backing pages
    ├── Gaming/Catalogs/                1 file   ← 1 backing page
    ├── Notifications/Catalogs/         1 file   ← 1 backing page
    ├── Power/Catalogs/                 2 files  ← 1 backing page
    ├── Privacy/Catalogs/               1 file   ← 1 backing page
    ├── Software/Catalogs/              24 files ← no declarative page
    ├── Sound/Catalogs/                 1 file   ← 1 backing page
    └── Update/Catalogs/                2 files  ← 1 backing page

src/AkariTool.Infrastructure/
├── DI/InfrastructureServiceExtensions.cs
├── Features/
│   ├── Apps/Services/                  WinGetComSession, WingetBootstrapper,
│   │                                   WingetDetectionService, WingetPackageInstaller
│   ├── Common/
│   │   ├── Events/EventBus.cs
│   │   ├── Interfaces/                 8 files: IWindowsRegistryService, IProcessExecutor,
│   │   │                               IPowerShellRunner, IFileSystemService, IAkariLogService,
│   │   │                               IPowerCfgApplier, IScheduledTaskService,
│   │   │                               IComboBoxResolver, IProcessRestartManager
│   │   ├── Models/ProcessResult.cs
│   │   ├── Services/                   33 files: SettingOperationExecutor, SettingStateReader,
│   │   │                               SystemSettingsDiscoveryService, SettingDependencyResolver,
│   │   │                               WindowsRegistryService, PowerService, PowerCfgApplier,
│   │   │                               SystemBackupService, SystemRestoreService,
│   │   │                               WindowsCompatibilityFilter, HardwareCompatibilityFilter,
│   │   │                               CompatibleSettingsRegistry, GlobalSettingsPreloader,
│   │   │                               TooltipDataService, DispatcherService, …
│   │   └── Utilities/                  ValueComparer, PowerPlanHelper,
│   │                                   RegistryValueFormatter, NumericConversionHelper
│   └── Optimize/Services/WindowsUpdatePolicyHandler.cs
└── Services/                           ← 40 flat legacy files, NOT under Features/

src/AkariTool.App/
├── App.xaml(.cs)                       bootstrap + ConfigureServices
├── MainWindow.xaml(.cs)                shell, PageMap, rail routing
├── app.manifest
├── AkariTool.App.csproj
├── Views/                              ← 36 .xaml
│   ├── Controls/                       HubView, NavButton, NavSidebar,
│   │                                   PowerPlanComboBox, TaskProgressControl
│   ├── Converters/InverseBoolToVisibilityConverter.cs
│   ├── Selectors/                      ChangelogLineTemplateSelector,
│   │                                   TweakRowTemplateSelector
│   ├── Templates/                      TweakTemplates.xaml (80 KB),
│   │                                   SoftwareViewTemplates.xaml, TechnicalDetailsStyles.xaml
│   └── <28 top-level pages>            see page table below
├── ViewModels/                         ← 13 flat files + 6 subdirs
│   ├── <11 SettingPageViewModel pages> SoundViewModel, UpdateViewModel, …
│   ├── HomeViewModel, SettingsViewModel, PlaceholderViewModel
│   ├── AdvancedTools/ Verify/ Backup/ Software/ AkariOS/ Common/ Tweaks/ Gaming/
├── Features/                           ← only 3 domains
│   ├── Common/{Converters,Models,Services,Utilities}
│   ├── Shared/UiPreferences.cs
│   └── Software/{AppIconService.cs, SoftwareAppService.cs}
├── Services/                           ← 20 flat files
├── Defender/                           DisableDefender.ps1, NoDefender.cab
├── Nvidia/Settings.nip
├── Scripts/                            47 .ps1 + Network/{network-apply,network-revert}.bat
├── DI/UIServiceExtensions.cs
├── Resource/                           NavIcons/ (19 .png), logos
└── Assets/                             AkariLogo.*, AkariOSWallpaper.jpg
```

## Directory Purposes

### `src/AkariTool.Core/Features/Common/`
The shared kernel every domain uses. `Models/` holds the declarative tuning
vocabulary (`SettingDefinition`, `BaseDefinition`, `SettingGroup`,
`RegistrySetting`, `ComboBoxOption`, `NumericRangeMetadata`, `PowerPlan`);
`Interfaces/` holds all 32 service contracts; `Enums/` the 7 enum types;
`Constants/` the string-key tables (`FeatureIds`, `ComboBoxConstants`,
`AkariPaths`, `UserPreferenceKeys`); `Events/` the hand-rolled pub/sub
contracts; `Native/` P/Invoke; `Validation/` the catalog linter; `Services/`
one concrete implementation (`GlobalSettingsRegistry`).

### `src/AkariTool.Core/Features/<Domain>/Catalogs/`
The declarative tuning data. `Build()` returns `IReadOnlyList<SettingGroup>`.
Two distinct catalog families live here:
- **Tuning catalogs** (7 files with a `Build()`): declarative `SettingDefinition`
  rows consumed by a `SettingPageViewModel`.
- **Software catalogs** (24 files): app cards, capability lists, and
  PowerShell *script text generators* (`BloatRemovalScriptGenerator.cs`,
  `EdgeRemovalScript.cs`, `OneDriveRemovalScript.cs`). No `SettingDefinition`
  involved.

### `src/AkariTool.Infrastructure/Features/`
The migrated OS-service slice. `Common/Services/` is where every
`ISetting*`/`IPower*`/`IWindows*` implementation lives. `Apps/Services/` is
the WinGet COM stack. `Optimize/Services/` holds the single
`WindowsUpdatePolicyHandler` special handler.

### `src/AkariTool.Infrastructure/Services/` (legacy flat dir)
40 files, two namespace families, and **not** under `Features/`:
- `namespace AkariTool.Tabs` (18 files) — `TweakRegistry`, `DriftScanner`,
  `DriftBaseline`, `ExplorerRestart`, `PlaybookTweaks.*` (6 partials),
  `ServicesPreset.*` (4 partials), `SystemStateReader.*` (7 partials),
  `PostInstallService`, `RestorePointHelper`, `TweakHelpers.*` (2 partials).
- `namespace AkariTool.Services` (19 files) — `ElevationService`,
  `DefenderService`, `CompetitiveService`, `CompetitivePrefs`, `GameDetection`,
  `GpuTweaks`, `NvidiaProfileService`, `WimUtilService`, `AccountService`,
  `SteamLibrary`, `ProcessSuspender`, `ProcessTuning`, `ToolService`,
  `UpdateService`, `ToolFetchService`, `ShaderCacheService`,
  `SystemInfoService`.
- `namespace AkariTool.Infrastructure.Services` (4 files) — the `*Wrapper.cs`
  adapters.

### `src/AkariTool.App/Views/`
28 top-level pages plus `Controls/`, `Templates/`, `Selectors/`,
`Converters/`. All 28 are `namespace AkariTool.Views`. `TweakTemplates.xaml`
(80 KB) is the shared row-rendering layer every declarative page binds to.

### `src/AkariTool.App/ViewModels/`
11 flat `SettingPageViewModel` subclasses (one per tuning page) plus
`HomeViewModel`, `SettingsViewModel`, `PlaceholderViewModel`, and six
subdirectories for the non-declarative pages (`Tweaks/` holds the shared base
and row machinery; `Software/`, `Backup/`, `Verify/`, `AdvancedTools/`,
`AkariOS/`, `Common/`, `Gaming/`).

### `src/AkariTool.App/Features/`
Only 3 domains — the incomplete part of the vertical slice.
- `Common/` — `Converters/` (IconConverter, BoolToDimOpacityConverter),
  `Models/` (TechnicalDetailRow/Section), `Services/DispatcherService.cs`,
  `Utilities/RegeditIconProvider.cs`.
- `Shared/UiPreferences.cs` — registry-backed section-collapse store.
- `Software/` — `SoftwareAppService.cs` (646 lines, the entire Software-tab
  service) and `AppIconService.cs`.

### `src/AkariTool.App/Scripts/`, `Defender/`, `Nvidia/`
Embedded payloads, not source. Declared as `EmbeddedResource` in
`src/AkariTool.App/AkariTool.App.csproj:90-96` and loaded at runtime via
`GetManifestResourceStream`. 47 `.ps1` + 2 `.bat`, a Defender CAB, and an
NVIDIA profile `.nip`. These are data files with executable content — editing
them changes app behaviour without a recompile.

## Key File Locations

**Entry Points:**
- `src/AkariTool.App/App.xaml.cs` — DI container build, startup orchestration, argv.
- `src/AkariTool.App/MainWindow.xaml.cs` — `PageMap` at `:34`, detail-tag sets at `:80-93`, `SelectRailTag` at `:355`, `TagForPage` at `:320-348`.
- `src/AkariTool.App/Services/StartupOrchestrator.cs` — the 3-phase startup seam.

**Configuration:**
- `AkariTool.sln` — project list. Note `vendor/WinUI.Framework` is **not** in the solution even though `AkariTool.App.csproj:75` ProjectReferences it (verified: zero `WinUI` matches in the sln).
- `src/AkariTool.App/AkariTool.App.csproj` — WinUI settings, `AkariPublish`/`DeElevatedTest` conditionals, EmbeddedResource list.
- `src/AkariTool.Core/AkariTool.Core.csproj` — `InternalsVisibleTo AkariTool.Core.Tests`.
- `src/AkariTool.Infrastructure/AkariTool.Infrastructure.csproj` — OS packages, `InternalsVisibleTo AkariTool.Infrastructure.Tests`.
- No `.editorconfig`, no `Directory.Build.props`, no `global.json`, no analyzers.

**Core Logic:**
- `src/AkariTool.Core/Features/Common/Models/SettingDefinition.cs` — the tuning data model.
- `src/AkariTool.Core/Features/Common/Validation/SettingCatalogValidator.cs` — catalog invariants.
- `src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs` — the write path (11 injected deps).
- `src/AkariTool.Infrastructure/Features/Common/Services/SystemSettingsDiscoveryService.cs` — the batched read path.
- `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs` — section build + gates + quick actions.
- `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` — the row (979 lines, the largest App file).

**Testing:**
- `tests/AkariTool.Core.Tests/` — model, validator, `BuildVersionGate` tests.
- `tests/AkariTool.Infrastructure.Tests/` — the filtering, reading, writing, and
  power-plan services. `Features/Optimize/WindowsUpdatePolicyHandlerTests.cs` is
  the only subdirectory.
- No test project for `AkariTool.App` (nothing references it — the ViewModels are
  not unit-testable as written, largely because of the `ServiceLocator` usage).

## The 7 Tuning Catalogs and the Page Each Backs

Each catalog is a `public static class` with a single `Build()` returning
`IReadOnlyList<SettingGroup>`. Every one is in `AkariTool.Core/Features/<Domain>/Catalogs/`
but declares a **`AkariTool.Tabs*`** namespace, not a `AkariTool.Core.*` one.

| # | Catalog file | Lines | `SettingGroup`s | Namespace | Page (`Views/`) | ViewModel | NavTag / NavLabel |
|---|--------------|-------|----------------|-----------|----------------|-----------|-------------------|
| 1 | `Customize/Catalogs/TaskbarOptimizations.cs` | 942 | 3 | `AkariTool.Tabs.Customize` | `TaskbarPage` | `ViewModels/TaskbarViewModel` | `Taskbar` / Taskbar |
| 2 | `Customize/Catalogs/ExplorerOptimizations.cs` | 2026 | 6 | `AkariTool.Tabs.Customize` | `ExplorerPage` | `ViewModels/ExplorerViewModel` | `Explorer` / Explorer |
| 3 | `Customize/Catalogs/AppearanceOptimizations.cs` | 315 | 4 | `AkariTool.Tabs.Customize` | `AppearancePage` | `ViewModels/AppearanceViewModel` | `Appearance` / Appearance |
| 4 | `Customize/Catalogs/StartMenuOptimizations.cs` | 420 | 2 | `AkariTool.Tabs.Customize` | `StartMenuPage` | `ViewModels/StartMenuViewModel` | `StartMenu` / Start Menu |
| 5 | `Customize/Catalogs/DesktopOptimizations.cs` | 456 | 6 | `AkariTool.Tabs.Customize` | `DesktopPage` | `ViewModels/DesktopViewModel` | `Desktop` / Desktop |
| 6 | `Gaming/Catalogs/GamingOptimizations.cs` | 3427 | 12 | `AkariTool.Tabs.Gaming` | `GamingPage` | `ViewModels/GamingViewModel` | `Gaming` / Gaming & Performance |
| 7 | `Notifications/Catalogs/NotificationsOptimizations.cs` | 515 | 5 | `AkariTool.Tabs.Notifications` | `NotificationsPage` | `ViewModels/NotificationsViewModel` | `Notifications` / Notifications |
| 8 | `Power/Catalogs/PowerOptimizations.cs` | 1624 | 18 | `AkariTool.Tabs.Power` | `PowerPage` | `ViewModels/PowerViewModel` | `Power` / Power |
| 9 | `Privacy/Catalogs/PrivacyOptimizations.cs` | 2934 | 13 | `AkariTool.Tabs.Privacy` | `PrivacyPage` | `ViewModels/PrivacyViewModel` | `Privacy` / Privacy & Security |
| 10 | `Sound/Catalogs/SoundOptimizations.cs` | 244 | 1 | `AkariTool.Tabs.Sound` | `SoundPage` | `ViewModels/SoundViewModel` | `Sound` / Sound |
| 11 | `Update/Catalogs/UpdateOptimizations.cs` | 415 | 3 | `AkariTool.Tabs.Update` | `UpdatePage` | `ViewModels/UpdateViewModel` | `Update` / Windows Updates |

**11 tuning catalogs, not 7** — `Customize` contributes 5 (one per sub-page) and
`Gaming`, `Notifications`, `Power`, `Privacy`, `Sound`, `Update` contribute 6.
The "7" reading that does hold is at the *nav-hub* level: the **Optimize hub**
owns 6 detail pages (Gaming, Privacy, Power, Update, Notifications, Sound) and the
**Customize hub** owns 5 (Taskbar, Explorer, Appearance, StartMenu, Desktop);
`AkariOSPage` is a 7th Optimize-hub card with no catalog and no settings VM.

**Two more catalog files, neither with a `Build()`:**
- `Power/Catalogs/PowerTemplates.cs` (620 lines) — a shared library of
  `ComboBoxMetadata` / `NumericRangeMetadata` factories (`TimeIntervals`,
  `LidActions`, `ProcessorBoostMode`, `CreateNumericRange(...)`, …). Heavily used
  by `PowerOptimizations.cs` (≈35 references). **Live.**
- `Update/Catalogs/UpdateTweaks.cs` (329 lines) — a `public static partial class`
  of legacy `TweakDefinition` factories with inline `Registry.SetValue` lambdas.
  Verified: **zero call sites** anywhere in `src/` or `tests/`; the only mentions
  are four comments in `UpdateOptimizations.cs` and one in `UpdateViewModel.cs`.
  **Dead code** (see CONCERNS).

**Cross-catalog dependency:** `PowerViewModel.AdditionalResolutionCatalogs()`
(`src/AkariTool.App/ViewModels/PowerViewModel.cs:85`) returns
`PrivacyOptimizations.Build()…` so the dependency resolver can auto-enable
Privacy's `privacy-lock-screen` when Power's `start-power-lock-option` is applied.
This is the **only** intentional cross-domain catalog reference in the codebase,
and it is declared through the base class's extension point rather than by a
shared registry.

## The 24 Software Catalogs

`src/AkariTool.Core/Features/Software/Catalogs/` — all `namespace AkariTool.Tabs`.
None back a `SettingPageViewModel`.

| File | Kind | Backing UI |
|------|------|-----------|
| `WindowsAppCatalog.cs` (653 lines) | App card list | `WindowsAppsPage` / `ViewModels/Software/WindowsAppsViewModel.cs` |
| `ExternalAppCatalog.cs` + 16 `ExternalAppCatalog.<Category>.cs` partials | App card lists (Browsers, Compression, CustomizationUtilities, DevelopmentApps, DocumentViewers, FileDiskManagement, Gaming, Imaging, MessagingEmailCalendar, Multimedia, OnlineStorageBackup, OpticalDiscTools, OtherUtilities, PrivacySecurity, RemoteAccess, RuntimesAndDependencies) | `ExternalAppsPage` / `ViewModels/Software/ExternalAppsViewModel.cs` |
| `AppModels.cs` | Shared app-card record | both |
| `CapabilityCatalog.cs` | DISM capability ids | `WindowsAppsViewModel` |
| `OptionalFeatureCatalog.cs` | Windows optional-feature ids | `WindowsAppsViewModel` |
| `BloatRemovalScriptGenerator.cs` | PowerShell text generator | `DebloatPage` / `ViewModels/Software/DebloatViewModel.cs` |
| `EdgeRemovalScript.cs` (685 lines) | PowerShell text generator | `DebloatPage` |
| `OneDriveRemovalScript.cs` | PowerShell text generator | `DebloatPage` |

## The Legacy App Top-Level Dirs and What Still Lives in Them

`src/AkariTool.App/Features/` has only `Common`, `Shared`, `Software` — while these
eight top-level directories sit beside it, outside any `Features/` boundary.

### `Views/` — 28 pages, all UI still lives here
Every page is `namespace AkariTool.Views` with a code-behind. Sub-dirs:
`Controls/` (5 reusable controls incl. the 20 KB `HubView.xaml` used by all four
hubs), `Templates/` (3 shared template dictionaries, `TweakTemplates.xaml` is
80 KB), `Selectors/` (2 `DataTemplateSelector`s), `Converters/` (1 file).

**Four hub pages** own the rail and host detail pages in an inner frame:
`OptimizeHubPage`, `CustomizePage`, `AdvancedHubPage`, `SoftwareAppsPage`. All four
configure a shared `HubView` and add `HubCardViewModel` cards in code
(`Views/OptimizeHubPage.xaml.cs:32`, `Views/CustomizePage.xaml.cs:28`).

**One page is 7 partials totaling 2631 lines:** `AkariOSPage` —
`AkariOSPage.xaml.cs` (141), `.Tools` (123), `.PostInstall` (142),
`.Utilities` (178), `.GpuTools` (285), `.GamingTweaks` (327), `.ShaderCache` (349),
`.ServicePresets` (506), `.Competitive` (680). `AdvancedToolsPage` is 4 partials
(1,222 lines incl. `.Wizard.cs` at 22 KB). `SettingsPage` is a single
10.6 KB code-behind.

**A converter lives in the wrong place:** `Views/Converters/InverseBoolToVisibilityConverter.cs`
is the only converter outside `Features/Common/Converters/`, and it is the one
consumed by the shared `TweakTemplates.xaml` (registered as `InvBoolToVis` at
`Views/Templates/TweakTemplates.xaml:27`).

### `ViewModels/` — 13 flat files + 6 sub-dirs
The 11 declarative page VMs are **flat at the top level** (`TaskbarViewModel.cs`
sits beside `HomeViewModel.cs`), while every non-declarative page VM is in a
sub-directory (`ViewModels/Software/`, `Backup/`, `Verify/`, `AdvancedTools/`,
`AkariOS/`). `ViewModels/Tweaks/` holds the shared machinery:
`SettingPageViewModel` (465), `SettingItemViewModel` (979),
`TechnicalDetailsManager` (523), `SettingBadgeCalculator` (21 KB),
`SettingPowerPlanController`, `SettingStatusBannerManager`,
`SettingSectionViewModel`, `ISettingRowViewModel`.

### `Services/` — 20 flat files, UI-layer services not under `Features/`
- Declarative-stack support: `SettingBackupService.cs` (658 lines — export/import/
  search engine), `TweakDialogs.cs` (serialized `ContentDialog` helper),
  `TaskProgressService.cs`, `SettingPageWarmUp.cs`, `StartupOrchestrator.cs`.
- Preferences/badges: `NewBadgeService.cs`, `NavBadgeService.cs`,
  `StartupNotificationService.cs`.
- OS-touching despite being in `App`: `DefenderService.cs` (329 lines, 19 registry
  calls), `DefenderPhase2Scheduler.cs`, `SystemUtilities.cs` (194 lines, 7 registry
  calls), `AkariFileService.cs` (335 lines, an `IFileService` override).
- Payload writers: `AutounattendService` + 4 partials (`.Xml`, `.ScriptPreamble`,
  `.ScriptSystem`, `.ScriptUser`, `.Tweaks`) — 860 lines total.
- `AkariUiLogService.cs` — the `ILogService` decorator.

### `Defender/`, `Nvidia/`, `Scripts/`
Embedded binary/script payloads (see `Special Directories` below). No C# in any
of them; they are declared as `EmbeddedResource` and read at runtime.

### `DI/UIServiceExtensions.cs`
The only file in `DI/`. 134 lines. Registers 5 non-VM services, 3 decorators,
`MainWindow`, 3 transient VMs, 19 singleton VMs, and the 11-entry
`SettingPageViewModel` marker enumeration that drives warm-up order.

### `Resource/` vs `Assets/` — duplicated brand assets
`Resource/NavIcons/` (19 `.png`) plus `Resource/AkariLogo.png`, `.ico`,
`AkariLogoLight.png`, `Akari.png`. `Assets/` holds a **second** copy of
`AkariLogo.png` / `.ico` / `AkariLogoLight.png` (identical byte sizes for the `.ico`
at 64,199) plus `AkariOSWallpaper.jpg` (98 KB). Only the `Assets/` copies are
declared as `Content` (`AkariTool.App.csproj:81-85`); the `Resource/` copies are
unreferenced by the csproj. `Resource/NavIcons/` is not declared in the csproj
either — verified no `NavIcons` reference outside the directory itself.

## Naming Conventions

**Files:**
- One public type per file, filename == type name. Partial classes split by concern
  with a `.` suffix: `PlaybookTweaks.Registry.cs`, `SystemStateReader.Desktop.cs`,
  `AkariOSPage.GpuTools.cs`, `ExternalAppCatalog.Browsers.cs`.
- `*Page.xaml` + `*Page.xaml.cs` for pages; `*ViewModel.cs` for VMs;
  `*Service.cs`, `*Registry.cs`, `*Helper.cs`, `*Filters`/`*Resolver` for behaviour.
- `I*` prefix for every interface. `*Wrapper` for a static→interface adapter.
- `*Optimizations.cs` for tuning catalogs; `*Optimizations` reads as
  "optimizations we apply", not "optimizations of the catalog".

**Directories:**
- Domain-first: `Features/<Domain>/{Models,Interfaces,Services,Catalogs,Constants,Enums,Events,Utilities,Validation,Native,Helpers}`.
- Layer-first in `Core`/`Infrastructure`, but `AkariTool.App` is the outlier —
  it is `Views/`, `ViewModels/`, `Services/` with `Features/` as a newcomer.

**Namespaces:** three incompatible schemes coexist (see CONCERNS).
Folder path does **not** reliably predict namespace.

## Where to Add New Code

**A new declarative setting on an existing page:**
- Primary code: the matching `src/AkariTool.Core/Features/<Domain>/Catalogs/<Domain>Optimizations.cs`, inside the relevant `Build()` `new SettingGroup(...)` (or a new `SettingGroup`).
- No ViewModel change, no XAML change, no DI change. The row, its template, badges, and Quick Actions all derive from the `SettingDefinition`.
- If the setting needs a composite read/write the generic executor cannot express: add a handler in `src/AkariTool.Infrastructure/Features/<Domain>/Services/`, register it in `ISpecialSettingHandlerRegistry` **and** `ISpecialDiscoveryRegistry` in `src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs` (mirror `updates-policy-mode` at `:38`).
- Validate against `SettingCatalogValidator` — but note it does not run in the build, so run `tests/AkariTool.Core.Tests/Features/SettingCatalogValidatorTests.cs`-style assertions yourself.

**A new tuning page (new domain):**
- Catalog: `src/AkariTool.Core/Features/<NewDomain>/Catalogs/<NewDomain>Optimizations.cs` with `public static IReadOnlyList<SettingGroup> Build()`.
- ViewModel: `src/AkariTool.App/ViewModels/<NewDomain>ViewModel.cs` — `sealed partial class X : SettingPageViewModel`, override `NavTag`, `NavLabel`, `BuildSettingGroups()`; set `Title`/`Subtitle` in the ctor. Copy `src/AkariTool.App/ViewModels/SoundViewModel.cs` (45 lines) as the minimal template, or `TaskbarViewModel.cs` for the full 9-optional-parameter ctor.
- Page: `src/AkariTool.App/Views/<NewDomain>Page.xaml(.cs)` — ~28 lines of code-behind, resolve the VM from `ServiceLocator`, set `DataContext`, call `Build()`.
- DI: `src/AkariTool.App/DI/UIServiceExtensions.cs` — two registrations required, in matching positions: `AddSingleton<XViewModel>()` and `AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<XViewModel>())`. **Order in that file is the warm-up order and the `TweakRegistry` range order** (see the comment at `:85-89`).
- Navigation: `src/AkariTool.App/MainWindow.xaml.cs` — a `PageMap` entry (`:34`) and, if it is a hub detail, membership in one of the four `*DetailTags` sets (`:80-93`) plus a `HubCardViewModel` in the hub's `.xaml.cs`.
- Templates: the row renders from `src/AkariTool.App/Views/Templates/TweakTemplates.xaml` via `TweakRowTemplateSelector` — no per-page template work.

**A new non-declarative feature:** there is no pattern. Decide explicitly
between adding it as a bespoke page (follow `AkariOSPage` / `WindowsAppsPage`)
or, preferably, as a new `SettingDefinition` catalog so it inherits the whole
declarative pipeline.

**A new OS service:**
- Interface in `src/AkariTool.Core/Features/Common/Interfaces/`.
- Implementation in `src/AkariTool.Infrastructure/Features/Common/Services/`.
- Registration in `src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs`.
- Do **not** create a `*Wrapper` over a new static class — that pattern is
  legacy and only 4 instances of it exist.

**Shared helpers:**
- Cross-layer pure logic → `src/AkariTool.Core/Features/Common/Helpers/`.
- Infrastructure-only helper → `src/AkariTool.Infrastructure/Features/Common/Utilities/` (declare `internal static` — all 4 existing files do).
- App-only helper → `src/AkariTool.App/Features/Common/Utilities/`.

## Special Directories

**`src/AkariTool.App/Scripts/`:**
- Purpose: 47 PowerShell scripts + 2 batch files, embedded and executed at runtime by `ToolService` and `AdvancedToolsPage`.
- Generated: No — hand-authored, but functionally code.
- Committed: Yes.
- Caveat: declared as `EmbeddedResource Include="Scripts\*.ps1"` (`AkariTool.App.csproj:91`). Changes ship without a recompile of the ported logic.

**`src/AkariTool.App/Defender/` and `src/AkariTool.App/Nvidia/`:**
- Purpose: `NoDefender.cab` (43 KB) + `DisableDefender.ps1`, and `Settings.nip` (22 KB, NVIDIA profile).
- Generated: No.
- Committed: Yes.
- Caveat: `Nvidia\Settings.nip` is guarded by `Condition="Exists(...)"` (`csproj:95`) — deleting it silently drops the resource rather than failing the build.

**`src/AkariTool.App/Resource/`:**
- Purpose: nav icons and brand images.
- Generated: Partly (the 2 MB `AkariLogoLight.png`).
- Committed: Yes.
- Caveat: **duplicates `Assets/`** and is not declared in the csproj. `NavIcons/` is likewise unreferenced by the project file.

**`vendor/WinUI.Framework/`:**
- Purpose: the WinUI 3 framework consumed as a source `ProjectReference` (`AkariTool.App.csproj:75`).
- Generated: No.
- Committed: Yes.
- Caveat: **not registered in `AkariTool.sln`** (verified zero `WinUI` matches) — so `dotnet build AkariTool.sln` will still pull it transitively via MSBuild, but it is invisible in the IDE solution explorer. A comment at `csproj:70-74` flags this as temporary ("Switch to a local NuGet feed before releasing").

**`vendor/WinGet.Interop/`:**
- Purpose: WinGet COM interop, referenced by `AkariTool.Infrastructure.csproj:19`.
- Generated: No. Committed: Yes. In the sln.

**`bin/` and `obj/`:**
- Purpose: build output.
- Generated: Yes. Committed: No (`.gitignore`).
- Caveat: `AkariTool.App.csproj:50-52` deliberately redirects `IntermediateOutputPath` to `obj/DeElevated/` under `/p:DeElevatedTest=true` so the `asInvoker` manifest intermediate cannot overwrite the normal `requireAdministrator` one. The comment explains `BaseIntermediateOutputPath` is left alone on purpose, because redirecting it un-excludes stale `obj` files from the default compile globs.

---

*Structure analysis: 2026-10-05*
