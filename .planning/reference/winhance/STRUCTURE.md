# Codebase Structure

**Analysis Date:** 2026-10-05
**Reference repository:** `C:/Users/isleap/Documents/GitHub/Winhance`

> Path references are relative to `C:/Users/isleap/Documents/GitHub/Winhance/`.
> `bin/`, `obj/`, `extras/`, and `vendor/` are excluded. Line counts are tracked source only.

---

## Directory Layout

```
Winhance/
├── Winhance.sln                       # 8 projects in 2 solution folders: src/ and tests/
├── global.json                        # SDK pinning
├── Winhance.ps1                       # Bootstrap/build script
├── Winhance-Beta.ps1                  # Beta-channel build script
├── LICENSE.txt
├── THIRD-PARTY-NOTICES.txt
├── README.md
│
├── .github/                           # CI workflows
│
├── extras/                            # EXCLUDED from analysis — installer (Inno Setup .iss),
│                                      #   app icons, release assets
│
├── src/
│   ├── Winhance.Core/                 # 240 .cs / 25,788 loc / 0 .xaml
│   │   ├── Winhance.Core.csproj       # net10.0-windows10.0.19041.0; no ProjectReference
│   │   ├── Properties/
│   │   │   └── PublishProfiles/
│   │   └── Features/
│   │       ├── Common/                # 168 files  ← shared base
│   │       │   ├── Constants/         #   FeatureIds, SettingIds, FeatureDefinitions,
│   │       │   │                      #   UserPreferenceKeys, ScriptPaths, ComboBoxConstants,
│   │       │   │                      #   ConfigFileConstants
│   │       │   ├── Converters/        #   StringOrStringArrayConverter
│   │       │   ├── Enums/             #   13 enums (InputType, LogLevel, WinhanceMode, …)
│   │       │   ├── Events/            #   IEventBus, IDomainEvent, ISubscriptionToken,
│   │       │   │   ├── Settings/      #     SettingAppliedEvent
│   │       │   │   └── UI/            #     FilterStateChanged, SettingsRefreshed, TooltipUpdated
│   │       │   ├── Exceptions/        #   ExecutionPolicyException, InsufficientDiskSpaceException
│   │       │   ├── Extensions/        #   TaskExtensions (FireAndForget), TaskProgress…
│   │       │   ├── Helpers/           #   BuildVersionGate, ZoomLevels
│   │       │   ├── Interfaces/        #   90 contracts  ← the contract wall
│   │       │   ├── Localization/      #   SettingLocalizationKeys (key-name builders only)
│   │       │   ├── Models/            #   40 records: BaseDefinition, SettingDefinition,
│   │       │   │                      #     SettingGroup, RegistrySetting, OperationResult, …
│   │       │   ├── Native/            #   8 P/Invoke surfaces (PowerProf, User32Api, DismApi, …)
│   │       │   ├── Services/          #   5 pure impls: LogService, StartupLogger,
│   │       │   │                      #     InitializationService, GlobalSettingsRegistry,
│   │       │   │                      #     DependencyManager
│   │       │   ├── Utils/             #   SearchHelper
│   │       │   └── Validation/        #   SettingCatalogValidator
│   │       ├── Optimize/              # 9 files / 10,558 loc
│   │       │   ├── Interfaces/        #   IPowerService
│   │       │   └── Models/            #   PrivacyOptimizations (2,918), GamingAndPerformance (4,151),
│   │       │                          #   PowerOptimizations (1,467), UpdateOptimizations,
│   │       │                          #   NotificationOptimizations, SoundOptimizations,
│   │       │                          #   PowerPlan, PowerTemplates
│   │       ├── Customize/             # 5 files / 5,025 loc
│   │       │   ├── Interfaces/        #   IWallpaperService
│   │       │   └── Models/            #   ExplorerCustomizations (3,338), TaskbarCustomizations,
│   │       │                          #   StartMenuCustomizations, WindowsThemeCustomizations
│   │       ├── AdvancedTools/         # 8 files / 124 loc  ← contracts only
│   │       │   ├── Interfaces/        #   IWimImageService, IWimCustomizationService, IIsoService,
│   │       │   │                      #     IOscdimgToolManager, IDriverCategorizer,
│   │       │   │                      #     IAutounattendXmlGeneratorService
│   │       │   └── Models/            #   ImageDetectionResult, ImageFormatInfo
│   │       └── SoftwareApps/          # 50 files / 5,363 loc
│   │           ├── Enums/             #   DetectionSource, RemovalOutcome
│   │           ├── Interfaces/        #   20 contracts
│   │           ├── Models/            #   ItemDefinition, ItemGroup, WindowsAppDefinitions,
│   │           │                      #     CapabilityDefinitions, OptionalFeatureDefinitions,
│   │           │                      #     ExternalAppDefinitions + 15 category partials,
│   │           │                      #     RemovalScript, EdgeRemovalScript, OneDriveRemovalScript,
│   │           │                      #     PackageInstallResult, RepoIconKey
│   │           └── Utilities/         #   BloatRemovalScriptGenerator
│   │
│   ├── Winhance.Infrastructure/        # 104 .cs / 24,005 loc
│   │   ├── Winhance.Infrastructure.csproj  # → Core + WindowsPackageManager.Interop
│   │   ├── Properties/PublishProfiles/
│   │   ├── Extensions/
│   │   │   └── DI/
│   │   │       └── InfrastructureServicesExtensions.cs   # AddInfrastructureServices()
│   │   └── Features/
│   │       ├── Common/                # 58 files / 10,927 loc
│   │       │   ├── EventHandlers/     #   TooltipRefreshEventHandler
│   │       │   ├── Events/            #   EventBus
│   │       │   ├── Helpers/           #   RecommendedSettingsResolver
│   │       │   ├── Services/          #   46 impls: WindowsRegistryService, ProcessExecutor,
│   │       │   │                      #     CompatibleSettingsRegistry, SettingApplicationService,
│   │       │   │                      #     SystemSettingsDiscoveryService, LocalizationService, …
│   │       │   └── Utilities/         #   PowerShellRunner, DismSessionManager, ValueComparer, …
│   │       ├── Optimize/Services/     # 2 files / 1,233 loc — PowerService, UpdateService
│   │       ├── Customize/Services/    # 2 files / 136 loc — WallpaperService, ThemeWallpaperApplier
│   │       ├── AdvancedTools/         # 13 files / 3,394 loc
│   │       │   ├── Helpers/           #   DriverCategorizer, PowerShellScriptUtilities,
│   │       │   │                      #     RegistryCommandEmitter
│   │       │   ├── ScriptSections/    #   ScriptPreambleSection, FeatureRegistryScriptSection,
│   │       │   │                      #     PowerSettingsScriptSection, AppRemovalScriptSection,
│   │       │   │                      #     SpecialFeatureScriptSection
│   │       │   └── Services/          #   AutounattendScriptBuilder, WimImageService,
│   │       │                          #     WimCustomizationService, IsoService, OscdimgToolManager
│   │       └── SoftwareApps/Services/ # 28 files / 8,181 loc
│   │           ├── AppIconResolver.cs, AppxIconSource.cs, AppxPackageSource.cs,
│   │           ├── AppInstallationService.cs, AppStatusDiscoveryService.cs,
│   │           ├── BloatRemovalService.cs, ChocolateyService.cs, DirectDownloadService.cs,
│   │           ├── ExternalAppsService.cs, ExternalAppUninstallService.cs,
│   │           ├── IconCacheMigration.cs, IconManifestService.cs,
│   │           ├── LegacyCapabilityService.cs, LightVariantSynthesizer.cs,
│   │           ├── OptionalFeatureService.cs, RepoIconSource.cs, StoreDownloadService.cs,
│   │           ├── WindowsAppsService.cs, WindowsAppUninstallService.cs
│   │           └── WinGet/
│   │               ├── WinGetBootstrapper.cs, WinGetComSession.cs,
│   │               │   WinGetDetectionService.cs, WinGetPackageInstaller.cs
│   │               ├── Utilities/     # ConPtyProcess, WinGetCliRunner, WinGetExitCodes,
│   │               │                  #   WinGetInstaller, WinGetProgressParser
│   │               └── winget-cli/    # vendored winget.exe + DLLs (copied to output)
│   │
│   ├── Winhance.UI/                   # 176 .cs / 26,458 loc / 35 .xaml
│   │   ├── Winhance.UI.csproj         # WinExe, UseWinUI, unpackaged, self-contained, x64
│   │   ├── Program.cs                 # custom entry (DISABLE_XAML_GENERATED_MAIN)
│   │   ├── App.xaml + App.xaml.cs     # DI host build, logging, localization, theme
│   │   ├── MainWindow.xaml + .cs      # shell: NavSidebar + ContentFrame
│   │   ├── NativeMethods.txt          # CsWin32 P/Invoke source list
│   │   ├── app.manifest
│   │   ├── Assets/
│   │   │   ├── AppIcons/  ModeIcons/  Sponsors/ (sponsors.json + logos/)
│   │   ├── Helpers/                   # 5 — shell-level, NOT in Features/
│   │   │   ├── NavigationRouter.cs          # tag → Page type map
│   │   │   ├── TitleBarManager.cs
│   │   │   ├── StartupUiCoordinator.cs
│   │   │   ├── TaskProgressCoordinator.cs
│   │   │   └── DialogAccessibilityHelper.cs
│   │   ├── ViewModels/                # 5 — shell-level, NOT in Features/
│   │   │   ├── MainWindowViewModel.cs, TaskProgressViewModel.cs,
│   │   │   ├── ReviewModeBarViewModel.cs, BuilderModeBarViewModel.cs,
│   │   │   └── UpdateCheckViewModel.cs
│   │   └── Features/
│   │       ├── Common/                # 98 files / 11,598 loc / 15 .xaml
│   │       │   ├── Controls/          # 11 custom controls (9 with .xaml): SettingsListView,
│   │       │   │                      #   SettingsCardItem, SettingDescriptionWithBadges,
│   │       │   │                      #   PowerPlanComboBox, NavSidebar, NavButton,
│   │       │   │                      #   TaskProgressControl, QuietInfoBar, UniformWrapPanel,
│   │       │   │                      #   WebsiteLinkButton, ComboBoxEx
│   │       │   ├── Converters/       # 12
│   │       │   ├── TemplateSelectors/ # 2 — SettingTemplateSelector, SettingItemTemplateSelector
│   │       │   ├── Dialogs/           # 3 builders: ConfigImport, Sponsors, TaskOutput
│   │       │   ├── Extensions/DI/     # ← THE COMPOSITION ROOT
│   │       │   │   ├── CompositionRoot.cs          # ConfigureWinhanceServices(), CreateWinhanceHost()
│   │       │   │   ├── SettingServicesExtensions.cs  # AddSettingServices() + per-domain methods
│   │       │   │   └── UIServicesExtensions.cs       # AddUIServices()
│   │       │   ├── Helpers/           # 6 — FeatureBadgeAggregator, ReviewModeFilter,
│   │       │   │                      #   PageScrollHelper, TextScaleHelper,
│   │       │   │                      #   Win32FileDialogHelper, AutoSuggestBoxExtensions
│   │       │   ├── Interfaces/        # 16 — ISettingsFeatureViewModel, ISectionInfo,
│   │       │   │                      #   ISettingViewModelFactory, ISettingPreparationPipeline, …
│   │       │   ├── Localization/      # 29 *.json (af … zh-Hant), 174–337 KB each
│   │       │   ├── Models/            # 6 — SearchSuggestionItem, SettingViewModelDependencies,
│   │       │   │                      #   TechnicalDetailRow/Section, SettingItemViewModelConfig, …
│   │       │   ├── Resources/         # 5 xaml dictionaries + subfolders
│   │       │   │   ├── BadgeStyles.xaml, Converters.xaml, FeatureIcons.xaml,
│   │       │   │   ├── SettingTemplates.xaml (+ .xaml.cs for x:Bind),
│   │       │   │   ├── TechnicalDetailsStyles.xaml
│   │       │   │   ├── AdvancedTools/  autounattend-template.xml (embedded)
│   │       │   │   └── Configs/        3 .winhance baseline configs (embedded)
│   │       │   ├── Services/          # 27 — DialogService, ThemeService, ResourceService,
│   │       │   │                      #   ConfigurationService, ConfigReviewService,
│   │       │   │                      #   SettingViewModelFactory, SettingPreparationPipeline,
│   │       │   │                      #   SettingsLoadingService, StartupOrchestrator, …
│   │       │   ├── Utilities/         # 7 — UiZoomManager, WindowSizeManager, RegeditLauncher,
│   │       │   │                      #   TerminalLineRenderer, ConfigRegistryInitializer, …
│   │       │   ├── ViewModels/        # 3 — BaseViewModel, SectionPageViewModel<T>,
│   │       │   │                      #   MoreMenuViewModel
│   │       │   └── Views/             # 1 window — ConfigImportOverlayWindow.xaml
│   │       ├── Optimize/              # 21 files / 4,691 loc / 7 .xaml
│   │       │   ├── OptimizePage.xaml(.cs)         # hub
│   │       │   ├── Pages/                          # 6 sub-pages + .xaml
│   │       │   ├── Interfaces/                     # IOptimizationFeatureViewModel
│   │       │   ├── Models/                         # OptimizeSectionInfo
│   │       │   └── ViewModels/                     # 13 — OptimizeViewModel,
│   │       │                                      #   BaseSettingsFeatureViewModel,
│   │       │                                      #   SettingItemViewModel, SettingsGroup,
│   │       │                                      #   SettingStatusBannerManager,
│   │       │                                      #   TechnicalDetailsManager,
│   │       │                                      #   6 × XxxOptimizationsViewModel
│   │       ├── Customize/             # 12 files / 1,200 loc / 5 .xaml
│   │       │   ├── CustomizePage.xaml(.cs)        # hub
│   │       │   ├── Pages/                          # 4 sub-pages + .xaml
│   │       │   ├── Interfaces/                     # ICustomizationFeatureViewModel
│   │       │   ├── Models/                         # CustomizeSectionInfo
│   │       │   └── ViewModels/                     # 6 — CustomizeViewModel + 4 feature VMs
│   │       ├── AdvancedTools/         # 15 files / 2,827 loc / 3 .xaml
│   │       │   ├── AdvancedToolsPage.xaml(.cs)    # hub
│   │       │   ├── WimUtilPage.xaml(.cs)
│   │       │   ├── AutounattendGeneratorPage.xaml(.cs)
│   │       │   ├── Models/             # AdvancedToolsSectionInfo, WizardActionCard, WizardStepState
│   │       │   ├── Services/           # AutounattendXmlGeneratorService (impls a Core interface)
│   │       │   └── ViewModels/         # 9 — AdvancedToolsViewModel, WimUtilViewModel,
│   │       │                            #   AutounattendGeneratorViewModel, WimStep1–4, WimImageFormat
│   │       ├── SoftwareApps/          # 15 files / 2,794 loc / 3 .xaml
│   │       │   ├── SoftwareAppsPage.xaml(.cs)     # single page, 2 grids, 3 view modes
│   │       │   ├── Views/                         # ExternalAppsHelpContent, WindowsAppsHelpContent
│   │       │   ├── Models/                         # AppSortMode, ISelectable, SoftwareAppsViewMode
│   │       │   ├── Services/                       # SelectedAppsProvider
│   │       │   ├── AppOperationConfirmation.cs
│   │       │   └── ViewModels/                     # 8 — SoftwareAppsViewModel, WindowsAppsViewModel,
│   │       │                                      #   ExternalAppsViewModel, AppItemViewModel,
│   │       │                                      #   AppSortHelper, RemovalStatus{,Container}ViewModel
│   │       └── Settings/              # 2 files / 314 loc / 1 .xaml   ← UI-ONLY SLICE
│   │           ├── SettingsPage.xaml(.cs)
│   │           └── ViewModels/SettingsViewModel.cs
│   │
│   └── WindowsPackageManager.Interop/ # satellite COM projection
│       ├── NativeMethods.txt
│       ├── WindowsPackageManager.Interop.csproj
│       ├── WindowsPackageManager.Interop.sln     # has its OWN solution file
│       └── WindowsPackageManager/
│           ├── ClassModel.cs, ClassesDefinition.cs, ClsidContext.cs,
│           └── WindowsPackageManagerFactory.cs, …ElevatedFactory.cs, …StandardFactory.cs
│
└── tests/
    ├── Winhance.Core.Tests/             # 17 files
    │   ├── Constants/  Helpers/  Models/  Services/  Utilities/  Utils/  Validation/  Assets/
    ├── Winhance.Infrastructure.Tests/   # 83 files
    │   ├── AdvancedTools/ (10) + AdvancedTools/WimServices/ (4) + Common/ + Events/
    │   ├── Services/ (68)  Utilities/  Helpers/
    ├── Winhance.UI.Tests/               # 81 files
    │   ├── Converters/  Helpers/  Services/  Utilities/  ViewModels/  Fixtures/
    └── Winhance.IntegrationTests/       # 12 files
        ├── DI/InfrastructureContainerSmokeTests.cs
        ├── Configuration/  FileSystem/  Localization/  Pipeline/
        ├── Registry/  ScriptGeneration/
        └── Fixtures/TempDirectoryFixture.cs  Helpers/{TestContext,TestSettingFactory}.cs
```

---

## Directory Purposes

**`src/Winhance.Core/Features/`**
- Purpose: the contract layer plus all declarative data. Contains no UI and no OS calls beyond
  P/Invoke *declarations*.
- Contains: one folder per feature domain, each with `Interfaces/` and `Models/`;
  `Common/` with 13 sub-folders.
- Key files: `Features/Common/Models/BaseDefinition.cs`,
  `Features/Common/Models/SettingDefinition.cs`, `Features/Common/Interfaces/ISettingItem.cs`,
  `Features/Common/Constants/FeatureIds.cs`, `Features/Common/Events/IEventBus.cs`.

**`src/Winhance.Infrastructure/Features/`**
- Purpose: implementations that touch Windows — registry, files, processes, COM, PowerShell.
- Contains: one folder per feature domain, each with `Services/`;
  `Common/` with `Services/`, `Events/`, `EventHandlers/`, `Helpers/`, `Utilities/`.
- Key files: `Features/Common/Services/CompatibleSettingsRegistry.cs` (the catalog aggregation
  point), `Features/Common/Services/SettingApplicationService.cs`,
  `Features/Common/Events/EventBus.cs`.

**`src/Winhance.Infrastructure/Extensions/DI/`**
- Purpose: the only DI registration file in Infrastructure.
- Key file: `InfrastructureServicesExtensions.cs`.

**`src/Winhance.UI/Features/`**
- Purpose: pages, controls, converters, ViewModels, dialogs, resources, localization, and the
  composition root.
- Contains: one folder per feature domain; `Common/` with 12 sub-folders.
- Key files: `Features/Common/Extensions/DI/CompositionRoot.cs`,
  `Features/Common/ViewModels/SectionPageViewModel.cs`,
  `Features/Common/Interfaces/ISettingsFeatureViewModel.cs`,
  `Features/Common/Resources/SettingTemplates.xaml`.

**`src/Winhance.UI/Helpers/` and `src/Winhance.UI/ViewModels/`**
- Purpose: shell-only code that belongs to no feature slice.
- Why it is outside `Features/`: `NavigationRouter` knows about all five domains; putting it in
  `Features/Common/` would make Common depend on every domain.
- Note: `MoreMenuViewModel` is in `Features/Common/ViewModels/` while `TaskProgressViewModel` is
  in `UI/ViewModels/` — inconsistent. Pick one convention in Akari.

**`src/WindowsPackageManager.Interop/`**
- Purpose: a hand-maintained CsWinRT projection of the `WindowsPackageManager` COM API, so
  Infrastructure can call `winget` in-process without an executable.
- It has its own `.sln` because it is developed independently of the main solution.

---

## Key File Locations

**Entry Points:**
- `src/Winhance.UI/Program.cs` — real process entry; single-instance + WinUI bootstrap
- `src/Winhance.UI/App.xaml.cs` — `OnLaunched`; builds DI host, logging, localization, theme
- `src/Winhance.UI/MainWindow.xaml` / `.xaml.cs` — shell chrome and startup kickoff

**Composition Root / DI:**
- `src/Winhance.UI/Features/Common/Extensions/DI/CompositionRoot.cs` — orchestrates all three
- `src/Winhance.Infrastructure/Extensions/DI/InfrastructureServicesExtensions.cs`
- `src/Winhance.UI/Features/Common/Extensions/DI/SettingServicesExtensions.cs`
- `src/Winhance.UI/Features/Common/Extensions/DI/UIServicesExtensions.cs`

**Core Logic:**
- `src/Winhance.Core/Features/Common/Models/SettingDefinition.cs` — the central data record
- `src/Winhance.Core/Features/Common/Models/BaseDefinition.cs` — shared base record
- `src/Winhance.Infrastructure/Features/Common/Services/CompatibleSettingsRegistry.cs` —
  where every domain catalog is registered (line 231, `GetKnownFeatureProviders`)
- `src/Winhance.Infrastructure/Features/Common/Services/SettingApplicationService.cs` — the
  generic apply engine
- `src/Winhance.Core/Features/Common/Validation/SettingCatalogValidator.cs` — catalog invariants
- `src/Winhance.UI/Features/Common/ViewModels/SectionPageViewModel.cs` — hub ViewModel base
- `src/Winhance.UI/Features/Common/Services/SettingViewModelFactory.cs` — definition → VM

**Pages (all 35 `.xaml`):**

| Slice | `.xaml` files |
|-------|--------------|
| Shell | `App.xaml`, `MainWindow.xaml` |
| `Common` | `Controls/NavButton.xaml`, `Controls/NavSidebar.xaml`, `Controls/PowerPlanComboBox.xaml`, `Controls/SettingDescriptionWithBadges.xaml`, `Controls/SettingsCardItem.xaml`, `Controls/SettingsListView.xaml`, `Controls/TaskProgressControl.xaml`, `Controls/WebsiteLinkButton.xaml`, `Resources/BadgeStyles.xaml`, `Resources/Converters.xaml`, `Resources/FeatureIcons.xaml`, `Resources/SettingTemplates.xaml`, `Resources/TechnicalDetailsStyles.xaml`, `Views/ConfigImportOverlayWindow.xaml` |
| `Optimize` | `OptimizePage.xaml`, `Pages/GamingOptimizePage.xaml`, `Pages/NotificationOptimizePage.xaml`, `Pages/PowerOptimizePage.xaml`, `Pages/PrivacyOptimizePage.xaml`, `Pages/SoundOptimizePage.xaml`, `Pages/UpdateOptimizePage.xaml` |
| `Customize` | `CustomizePage.xaml`, `Pages/ExplorerCustomizePage.xaml`, `Pages/StartMenuCustomizePage.xaml`, `Pages/TaskbarCustomizePage.xaml`, `Pages/WindowsThemeCustomizePage.xaml` |
| `AdvancedTools` | `AdvancedToolsPage.xaml`, `AutounattendGeneratorPage.xaml`, `WimUtilPage.xaml` |
| `SoftwareApps` | `SoftwareAppsPage.xaml`, `Views/ExternalAppsHelpContent.xaml`, `Views/WindowsAppsHelpContent.xaml` |
| `Settings` | `SettingsPage.xaml` |

**Testing:**
- `tests/Winhance.Core.Tests/` — 17 files; mirrors `Core/Features` by *concern*
  (`Models/`, `Services/`, `Validation/`, `Helpers/`, `Constants/`, `Utilities/`, `Utils/`)
- `tests/Winhance.Infrastructure.Tests/` — 83 files; flat `Services/`/`Utilities/`/`Events/`
  plus `AdvancedTools/` and `AdvancedTools/WimServices/` domain folders
- `tests/Winhance.UI.Tests/` — 81 files; `ViewModels/`, `Services/`, `Converters/`, `Helpers/`,
  `Utilities/`, `Fixtures/`
- `tests/Winhance.IntegrationTests/` — 12 files; `DI/`, `Configuration/`, `Localization/`,
  `Pipeline/`, `Registry/`, `ScriptGeneration/`, `FileSystem/`
- Stack: xunit 2.9.3 + Moq 4.20.72 + FluentAssertions 7.0.0 + coverlet.collector 6.0.2
  (`Winhance.Infrastructure.Tests.csproj:11`)

**Configuration:**
- `Winhance.sln` — 2 solution folders (`src`, `tests`), 8 projects
- `global.json` — SDK pin
- `Winhance.ps1`, `Winhance-Beta.ps1` — build/bootstrap scripts
- `src/Winhance.UI/Winhance.UI.csproj` — WinUI, unpackaged, self-contained, x64, embedded
  resources, content files
- `src/Winhance.UI/NativeMethods.txt` — CsWin32 P/Invoke source list
- `src/Winhance.UI/app.manifest` — app manifest

**Localization:**
- `src/Winhance.UI/Features/Common/Localization/*.json` — 29 locale files, copied to output as
  `Localization/<name>.json` by the csproj
- `src/Winhance.Core/Features/Common/Localization/SettingLocalizationKeys.cs` — key-name
  builders (`Setting_{LocalizationId}_Name`, etc.)

---

## Naming Conventions

**Files:**
- PascalCase, one type per file (partial classes allowed for category splits)
- `<TypeName>.cs` for the type, plus `<TypeName>.xaml` + `<TypeName>.xaml.cs` for controls/pages
- Test files: `<TypeName>Tests.cs`
- Test folders mirror the *type's role*, not always the source folder
  (`WimImageService.cs` → `tests/.../AdvancedTools/WimServices/WimImageServiceTests.cs`)

**Folders:**
- PascalCase, singular or plural as the content dictates
  (`Interface`+`s`, `Model`+`s`, `Service`+`s`, `Helper`+`s`, `Util`+`s` mixed:
  `Utilities/` and `Utils/` both exist in Core/Common)
- Feature domain folders are PascalCase and identical across layers: `Optimize`, `Customize`,
  `AdvancedTools`, `SoftwareApps`, `Settings`, `Common`
- Concern folders are also PascalCase: `Interfaces`, `Models`, `Services`, `ViewModels`,
  `Controls`, `Converters`, `Dialogs`, `Helpers`, `Utilities`, `Constants`, `Enums`, `Events`,
  `Extensions`, `Native`, `Validation`, `Localization`, `Resources`, `TemplateSelectors`,
  `Views`, `Pages`, `ScriptSections`

**Types:**
- `I` + PascalCase for interfaces (`ISettingApplicationService`, `IEventBus`)
- `*Service` for concrete services (`WallpaperService`, `LocalizationService`)
- `*ViewModel` for ViewModels; `*Page` for pages; `*SectionInfo` for section metadata records
- `*Definition` for declarative data (`SettingDefinition`, `ItemDefinition`, `FeatureDefinition`)
- `*Registry` for registries; `*Filter` / `*Resolver` / `*Builder` / `*Applier` for pure transforms
- Static catalog classes are plural noun-singular: `SoundOptimizations`, `TaskbarCustomizations`,
  `GamingAndPerformanceOptimizations` — **note the inconsistent plural**: `PowerTemplates`
  (plural) and `PowerPlan` (singular) sit side by side in `Optimize/Models/`

**Namespaces:** mirror the folder path exactly, rooted at the assembly:
`Winhance.Core.Features.Optimize.Models`, `Winhance.Infrastructure.Features.Customize.Services`,
`Winhance.UI.Features.Settings.ViewModels`, `Winhance.UI.Helpers`, `Winhance.UI.ViewModels`.

**XAML pages follow `<Feature><Section>Page` inside the slice:** `SoundOptimizePage` (Optimize
uses the infix `Optimize`), `TaskbarCustomizePage` (Customize also uses the infix), but
`WimUtilPage` and `AutounattendGeneratorPage` (AdvancedTools uses no infix) and `SettingsPage`.
The infix is what makes page names unique across slices — keep it.

**Namespace aliases** are used to disambiguate same-named types across `Common` and a domain
(`ISettingsLoadingService`, `IConfigReviewService`, `ILocalizationService`).

**Localization keys:** `Category_<Slice>_<Thing>` (`Category_Optimize_Title`),
`Nav_<Slice>` (`Nav_AdvancedTools`), `Setting_<Id>_<Field>`,
`Feature_<Id>_<Field>`, `Button_<Verb>`, `InfoBadge_<Kind>`, `Tooltip_<Subject>`.
The `<Domain>_` prefix is the reason `Common` localization keys are readable in isolation.

---

## Where to Add New Code

**New feature domain (full vertical slice):**
1. `src/<App>.Core/Features/<Domain>/Models/<X>Definitions.cs` — the static catalog class
   returning a `SettingGroup` (or `ItemGroup` for app-like domains).
2. `src/<App>.Core/Features/<Domain>/Models/<X>Definition.cs` — the per-item record, extending
   `BaseDefinition` if it is toggleable, or standing alone if it is not.
3. `src/<App>.Core/Features/<Domain>/Interfaces/I<X>Service.cs` — one contract per behaviour.
4. `src/<App>.Infrastructure/Features/<Domain>/Services/<X>Service.cs` — the implementation.
5. Add one line to `GetKnownFeatureProviders()` in
   `src/<App>.Infrastructure/Features/Common/Services/CompatibleSettingsRegistry.cs`.
6. Add a `Features/<Domain>/Models/<Domain>SectionInfo.cs` implementing `ISectionInfo`.
7. `src/<App>.UI/Features/<Domain>/ViewModels/<X>ViewModel.cs` extending
   `BaseSettingsFeatureViewModel`, plus a domain marker interface
   `Interfaces/I<Domain>FeatureViewModel.cs : ISettingsFeatureViewModel`.
8. `src/<App>.UI/Features/<Domain>/ViewModels/<Domain>ViewModel.cs` extending
   `SectionPageViewModel<<Domain>SectionInfo>`, with a `static readonly Sections` list.
9. `src/<App>.UI/Features/<Domain>/<Domain>Page.xaml(.cs)` — the hub with overview cards.
10. `src/<App>.UI/Features/<Domain>/Pages/<X>Page.xaml(.cs)` — one per section, ~34 lines each.
11. Register the feature VMs in `AddUIServices()` (against the marker interface) and the
    services in a new `Add<Domain>Services()` chained from `AddSettingServices()`.
12. Add the tag → page-type entry in `NavigationRouter.TagToPageType` (and the reverse map) and
    the button entry in `NavSidebar`.
13. Add localization keys prefixed `<Domain>_`.
14. Add tests in the matching `tests/<App>.<Layer>.Tests/` project.

**New setting catalog inside an existing domain (the common case):**
1. Add a `Get<X>() → SettingGroup` static method to the domain's catalog class
   (or a new `partial` file in the same folder — the `ExternalAppDefinitions.*` pattern).
2. Add the `FeatureId` constant to `Core/Features/Common/Constants/FeatureIds.cs`.
3. Add the `(Id, DefaultName, Category)` entry to
   `Core/Features/Common/Constants/FeatureDefinitions.cs`.
4. Add one line to `CompatibleSettingsRegistry.GetKnownFeatureProviders()`.
5. Create `UI/Features/<Domain>/ViewModels/<X>ViewModel.cs` and register it against the
   domain marker interface.
6. Add the section to the hub's `Sections` list and a `<X>Page.xaml` under `Pages/`.
7. Add the `sectionKey → pageType` arm in `<Domain>Page.NavigateToSection`.

**New page in an existing domain:** add the `.xaml` + `.xaml.cs` under
`UI/Features/<Domain>/Pages/` (or `Views/` for a non-page user control). Code-behind resolves the
**hub** ViewModel via `App.Services.GetRequiredService<T>()`, applies `e.Parameter` as a search
string, and calls `RefreshSettingStatesAsync()` on its named hub property. Do not inject
constructor arguments into pages.

**New shared service:**
- Pure in-process logic with no OS access → `Core/Features/Common/Services/`, with the interface
  in `Core/Features/Common/Interfaces/`. Copy the `LogService` precedent.
- Touches registry / disk / process / COM / PowerShell → `Infrastructure/Features/Common/Services/`,
  interface in `Core/Features/Common/Interfaces/`. Copy the `WindowsRegistryService` precedent.
- Touches WinUI (`XamlRoot`, `DispatcherQueue`, `ContentDialog`, resources) →
  `UI/Features/Common/Services/`, interface in `UI/Features/Common/Interfaces/`. Copy the
  `DialogService` precedent.
- Then add exactly one `AddSingleton<I<X>Service, <X>Service>()` line to the matching
  `*ServicesExtensions.cs`, under a `//` comment banner matching its concern.

**A setting that a registry write cannot express:** do **not** edit
`SettingApplicationService`. Implement `ISpecialSettingHandler` in the owning domain's
Infrastructure folder and add one entry to the id-keyed `SpecialSettingHandlerRegistry`
dictionary in `AddSettingServices()`. Add an `ISettingItem` id to
`Core/Features/Common/Constants/SettingIds.cs` rather than inlining the string.

**New WinUI control used by more than one page:** `UI/Features/Common/Controls/<Name>.xaml`
(+ code-behind if `x:Bind` is needed inside templates) and register any converters in
`UI/Features/Common/Resources/Converters.xaml`.

**New converter:** `UI/Features/Common/Converters/<Name>.converter.cs` → add to
`Converters.xaml`. Do **not** put a converter inside a domain folder; every slice would
duplicate it.

**New locale:** drop `<code>.json` into
`UI/Features/Common/Localization/`. It is picked up by the csproj glob
(`<Content Include="Features\Common\Localization\*.json">`) with no other change.

**New baseline config:** add `<name>.winhance` to
`UI/Features/Common/Resources/Configs/` **and** add an `<EmbeddedResource>` entry in
`Winhance.UI.csproj` with an explicit `<LogicalName>` — the glob is not implicit.

**New shell-level helper (belongs to no slice):** `UI/Helpers/`. If it must reference two or
more domains, it does not belong in `Features/Common/` — that would invert the dependency.

---

## Special Directories

**`src/Winhance.Infrastructure/Features/SoftwareApps/Services/WinGet/winget-cli/`**
- Purpose: vendored `winget.exe`, `WindowsPackageManagerServer.exe`, `WindowsPackageManager.dll`
  and 14 runtime DLLs, plus `winget-version.txt` pinning the vendored version.
- Generated: No — checked into the repo.
- Committed: Yes.
- Copied to output by `Winhance.Infrastructure.csproj:35-39` (`PreserveNewest`, linked to
  `winget-cli/%(RecursiveDir)%(Filename)%(Extension)`).
- Why: a reliable WinGet without depending on the host OS having a current `winget`.

**`src/Winhance.UI/Features/Common/Localization/`**
- Purpose: 29 locale JSON files.
- Committed: Yes.
- Copied to output (not embedded) by `Winhance.UI.csproj:122-126` so `LocalizationService` can
  enumerate the directory at runtime and offer a language picker without a resource lookup table.

**`src/Winhance.UI/Features/Common/Resources/Configs/`**
- Purpose: 3 baseline `.winhance` configuration files (recommended, Win11 25H2 default,
  Win10 22H2 default).
- Committed: Yes. **Embedded** as resources with explicit
  `<LogicalName>Winhance.UI.Resources.Configs.<file>` (`Winhance.UI.csproj:140-151`).

**`src/Winhance.UI/Features/Common/Resources/AdvancedTools/autounattend-template.xml`**
- Purpose: the autounattend skeleton that `AutounattendScriptBuilder` populates.
- Embedded: Yes, as `Winhance.UI.Resources.AdvancedTools.autounattend-template.xml`.

**`src/Winhance.UI/Assets/`**
- Purpose: `AppIcons/` (`.ico` for the apphost), `ModeIcons/`, `Sponsors/` (`sponsors.json` +
  `logos/*.png`, copied to output as an offline fallback for `SponsorsService`).
- Committed: Yes.

**`src/WindowsPackageManager.Interop/WindowsPackageManager.Interop.sln`**
- Purpose: an independent solution for the interop library.
- Note: the project *is* also in the main `Winhance.sln`, so the standalone `.sln` is a
  development convenience, not the canonical build entry.

**`extras/`**
- Purpose: the Inno Setup installer (`Winhance.Installer.iss`), release assets, and strip
  configuration for the ~38 MB of unused Windows ML DLLs that ship in the WinAppSDK metapackage.
- Excluded from this analysis; it is packaging, not architecture.

---

## Scale Reference

| Slice | Core | Infrastructure | UI | Total |
|-------|------|----------------|-----|-------|
| `Common` | 168 f / 4,718 loc | 58 f / 10,927 loc | 98 f / 11,598 loc | 324 f / 27,243 loc |
| `Optimize` | 9 f / 10,558 loc | 2 f / 1,233 loc | 21 f / 4,691 loc | 32 f / 16,482 loc |
| `Customize` | 5 f / 5,025 loc | 2 f / 136 loc | 12 f / 1,200 loc | 19 f / 6,361 loc |
| `AdvancedTools` | 8 f / 124 loc | 13 f / 3,394 loc | 15 f / 2,827 loc | 36 f / 6,345 loc |
| `SoftwareApps` | 50 f / 5,363 loc | 28 f / 8,181 loc | 15 f / 2,794 loc | 93 f / 16,338 loc |
| `Settings` | — | — | 2 f / 314 loc | 2 f / 314 loc |
| **Layer total** | **240 f / 25,788 loc** | **104 f / 24,005 loc** | **176 f / 26,458 loc** | **520 f / 76,251 loc** |

Tests: `Core.Tests` 17, `Infrastructure.Tests` 83, `UI.Tests` 81, `IntegrationTests` 12 = **193 files**.

---

*Structure analysis: 2026-10-05*
