# Codebase Structure

**Analysis Date:** 2026-08-27

## Directory Layout

```
Akari-Tool/
├── AkariTool.sln                  # Solution file (3 main projects + 2 tests + 2 vendor)
├── CLAUDE.md                      # Project context & rules
├── CHANGELOG.md                   # Release history
├── README.md                      # Landing documentation
│
├── src/
│   ├── AkariTool.Core/            # Pure C# models, zero OS dependencies
│   │   ├── AkariTool.Core.csproj
│   │   ├── Competitive/           # CompetitiveOptions, CompetitiveSession records
│   │   ├── Features/              # Feature-organized catalogs & models
│   │   │   ├── Common/            # Shared SettingDefinition stack, interfaces, enums
│   │   │   │   ├── Models/        # SettingDefinition, SettingGroup, badges, dependencies
│   │   │   │   ├── Enums/         # InputType, DetectionType, SettingBadgeKind, etc.
│   │   │   │   ├── Interfaces/    # ISettingOperationExecutor, ISettingStateReader
│   │   │   │   ├── Constants/
│   │   │   │   ├── Native/        # P/Invoke bindings (PowerProf.cs)
│   │   │   │   ├── Validation/    # SettingCatalogValidator
│   │   │   │   ├── Services/      # Core-only services (GlobalSettingsRegistry)
│   │   │   │   └── Helpers/       # BuildVersionGate helpers
│   │   │   ├── Gaming/            # Gaming & Performance feature
│   │   │   │   └── Catalogs/      # GamingOptimizations.cs (SettingGroup[] definitions)
│   │   │   ├── Privacy/           # Privacy feature
│   │   │   │   └── Catalogs/      # PrivacyOptimizations.cs
│   │   │   ├── Sound/             # Sound & Audio feature
│   │   │   │   └── Catalogs/      # SoundOptimizations.cs
│   │   │   ├── Notifications/     # Notifications feature
│   │   │   │   └── Catalogs/      # NotificationsOptimizations.cs
│   │   │   ├── Update/            # Windows Update feature
│   │   │   │   └── Catalogs/      # UpdateOptimizations.cs
│   │   │   ├── Power/             # Power Plans feature
│   │   │   │   └── Catalogs/      # PowerOptimizations.cs, PowerTemplates.cs
│   │   │   ├── Customize/         # Customize sub-features
│   │   │   │   └── Catalogs/      # Taskbar, Explorer, Appearance, StartMenu, Desktop
│   │   │   ├── AkariOS/           # AkariOS-specific models (BcdOperation, etc.)
│   │   │   └── Apps/              # Software/WinGet models
│   │   ├── Interfaces/            # Core-level service contracts
│   │   ├── Models/                # Common value objects (SystemInfo, UpdateCheckResult, etc.)
│   │   └── Tweaks/                # Legacy TweakDefinition (retained for Backup/Verify)
│   │
│   ├── AkariTool.Infrastructure/  # OS-touching implementations
│   │   ├── AkariTool.Infrastructure.csproj
│   │   ├── DI/                    # InfrastructureServiceExtensions.cs
│   │   ├── Features/              # Feature-organized services & executors
│   │   │   ├── Common/            # SettingOperationExecutor, SettingStateReader
│   │   │   │   ├── Services/      # Registry, PowerShell, tasks, files, processes
│   │   │   │   ├── Interfaces/    # Service contracts (IWindowsRegistryService, etc.)
│   │   │   │   ├── Events/        # EventBus
│   │   │   │   ├── Utilities/     # Value comparison, binary conversion
│   │   │   │   └── Models/        # ProcessResult, internal helpers
│   │   │   ├── Optimize/          # Windows Update policy + special handlers
│   │   │   │   └── Services/      # WindowsUpdatePolicyHandler
│   │   │   └── Apps/              # WinGet COM stack (detection, installation)
│   │   └── Services/              # Static OS services (legacy namespace)
│   │
│   └── AkariTool.App/             # WinUI 3 shell, ViewModels, XAML pages
│       ├── AkariTool.App.csproj
│       ├── App.xaml(.cs)          # Application entry point, DI setup
│       ├── MainWindow.xaml(.cs)   # Shell window, navigation routing
│       ├── DI/                    # UIServiceExtensions.cs (all VM + service registrations)
│       ├── ViewModels/            # MVVM ViewModels
│       │   ├── Tweaks/            # SettingPageViewModel, SettingItemViewModel, SettingSectionViewModel
│       │   ├── GamingViewModel.cs # Concrete SettingPageViewModel (Gaming feature)
│       │   ├── SoundViewModel.cs  # Concrete SettingPageViewModel (Sound feature)
│       │   ├── NotificationsViewModel.cs
│       │   ├── UpdateViewModel.cs
│       │   ├── PrivacyViewModel.cs
│       │   ├── PowerViewModel.cs  # Power Plans tab (Session C)
│       │   ├── TaskbarViewModel.cs, ExplorerViewModel.cs, etc. # Customize sub-pages
│       │   ├── GameViewModel.cs   # Bespoke DefenderToggleViewModel
│       │   ├── Software/          # SoftwareVM (Apps management)
│       │   ├── AkariOS/           # AkariOSViewModel (bespoke, non-declarative)
│       │   ├── Backup/            # BackupViewModel
│       │   ├── Verify/            # VerifyViewModel
│       │   ├── AdvancedTools/     # AdvancedToolsViewModel
│       │   └── Common/            # Shared ViewModel utilities
│       ├── Views/                 # XAML pages & controls
│       │   ├── HomePage.xaml      # Landing page
│       │   ├── GamingPage.xaml    # Gaming page (DataContext = GamingViewModel)
│       │   ├── SoundPage.xaml, NotificationsPage.xaml, etc. # Feature pages
│       │   ├── PowerPage.xaml     # Power Plans page (with dynamic plan dropdown)
│       │   ├── OptimizeHubPage.xaml # Hub for Optimize section (nav to detail pages)
│       │   ├── CustomizePage.xaml # Hub for Customize categories
│       │   ├── SoftwareAppsPage.xaml # Hub for Software section
│       │   ├── AdvancedHubPage.xaml # Hub for Advanced section
│       │   ├── BackupPage.xaml, VerifyPage.xaml, AdvancedToolsPage.xaml, etc.
│       │   ├── AkariOSPage.xaml   # AkariOS page (bespoke)
│       │   ├── SettingsPage.xaml  # App settings (theme, etc.)
│       │   ├── PlaceholderPage.xaml # Fallback for unmapped nav tags
│       │   ├── Controls/          # Reusable XAML controls
│       │   │   ├── NavButton.xaml # Navigation rail buttons
│       │   │   ├── NavSidebar.xaml # Sidebar component
│       │   │   ├── TaskProgressControl.xaml # Bulk task progress UI
│       │   │   ├── PowerPlanComboBox.xaml # Dynamic power plan selector
│       │   │   └── HubView.xaml   # Hub content area
│       │   ├── Templates/         # Data templates & selectors
│       │   │   ├── TweakTemplates.xaml # Row templates (Toggle, Selection, etc.)
│       │   │   ├── TweakRowTemplateSelector.cs # Selector logic
│       │   │   ├── SoftwareViewTemplates.xaml
│       │   │   └── TechnicalDetailsStyles.xaml
│       │   ├── Converters/        # Value converters (BoolToDim, Icon, etc.)
│       │   └── Selectors/         # XAML value converters
│       ├── Services/              # App-layer services
│       │   ├── ToolService.cs     # App-wide logger (entry point for all logging)
│       │   ├── TweakDialogs.cs    # ContentDialog serializer (confirmations)
│       │   ├── AkariFileService.cs # Win32 file picker (override framework default)
│       │   ├── AkariUiLogService.cs # UI-layer log sink
│       │   ├── DefenderService.cs # Windows Defender toggle (MUST stay App-side)
│       │   ├── DefenderPhase2Scheduler.cs # Defender post-reboot handler
│       │   ├── SettingBackupService.cs # Export/import settings + global search
│       │   ├── SettingPageWarmUp.cs # Startup warm-up for all SettingPageVMs
│       │   ├── NavBadgeService.cs # Navigation badge counts
│       │   ├── NewBadgeService.cs # New setting tracking
│       │   ├── TaskProgressService.cs # Bulk operation progress
│       │   ├── StartupNotificationService.cs # First-launch restore point offer
│       │   ├── AutounattendService.cs # AkariOS autounattend.xml generation
│       │   ├── StartupOrchestrator.cs # Compatibility filtering + warm-up orchestration
│       │   └── SettingStatusBannerManager.cs # Technical details banner
│       ├── Features/              # Feature-specific UI helpers
│       │   ├── Common/            # Shared feature code
│       │   │   ├── Converters/    # IconConverter (Material.Icons + FluentIcons)
│       │   │   ├── Models/        # TechnicalDetailRow, TechnicalDetailSection
│       │   │   └── Services/      # DispatcherService (UI thread marshaling)
│       │   ├── Shared/            # Cross-feature utilities (UiPreferences)
│       │   └── Software/          # Software page helpers (AppIconService, etc.)
│       ├── Assets/                # Images, fonts, resources
│       ├── Scripts/               # Embedded PowerShell scripts
│       │   └── Network/           # Network-related scripts
│       ├── Resource/              # Resource files (icons, etc.)
│       ├── Defender/              # Windows Defender resources (embedded CAB, scripts)
│       ├── Nvidia/                # NVIDIA GPU tweaks
│       └── XAML-related root files (App.xaml, MainWindow.xaml)
│
├── tests/
│   ├── AkariTool.Core.Tests/      # Core model & logic tests
│   │   ├── AkariTool.Core.Tests.csproj
│   │   ├── Features/              # SettingDefinition, badges, validators
│   │   │   └── *.cs               # SettingDefinitionToggleStateTests, etc.
│   │   ├── Helpers/               # BuildVersionGateTests
│   │   └── Models/                # UpdateModelsTests
│   │
│   └── AkariTool.Infrastructure.Tests/ # Infrastructure service & state reader tests
│       ├── AkariTool.Infrastructure.Tests.csproj
│       ├── Features/              # Reader, executor, resolver, filters
│       │   ├── SettingStateReaderTests.cs
│       │   ├── SettingOperationExecutorTests.cs
│       │   ├── SettingDependencyResolverTests.cs
│       │   ├── PowerPlanComboBoxServiceTests.cs
│       │   ├── PowerPlanHelperTests.cs
│       │   ├── ComboBoxResolverTests.cs
│       │   ├── WindowsCompatibilityFilterTests.cs
│       │   ├── HardwareCompatibilityFilterTests.cs
│       │   ├── Optimize/WindowsUpdatePolicyHandlerTests.cs
│       │   └── SystemBackupServiceParsingTests.cs
│       └── Services/
│           └── UpdateServiceTests.cs
│
├── vendor/
│   ├── WinUI.Framework/           # Local vendored WinUI 3 MVVM framework (ProjectReference)
│   │   └── WinUI.Framework.csproj # Provides: ViewModelBase, INavigationService, IDispatcherService, etc.
│   ├── WinGet.Interop/            # WinGet COM interop bindings
│   └── (legacy) WPF+WinUI hybrid build at Akari-Tool-MVVM/ — fallback only, not in active development
│
├── installer/                     # MSI installer project
├── docs/                          # Documentation
└── build-*.ps1 scripts            # Build automation

```

## Directory Purposes

**src/AkariTool.Core/**
- Purpose: Immutable models, catalogs, and contracts; zero OS dependencies
- Contains: SettingDefinition records, SettingGroup, feature catalogs (GamingOptimizations, etc.), enums (InputType, DetectionType, SettingBadgeKind), service interfaces
- Key files:
  - `Features/Common/Models/SettingDefinition.cs` — Core row model
  - `Features/Gaming/Catalogs/GamingOptimizations.cs` — Gaming settings catalog
  - `Features/Common/Enums/` — All enum definitions

**src/AkariTool.Infrastructure/**
- Purpose: OS interactions (registry, PowerShell, tasks, files, power settings, WinGet)
- Contains: SettingOperationExecutor, SettingStateReader, service implementations, compatibility filters
- Key files:
  - `Features/Common/Services/SettingOperationExecutor.cs` — Apply settings to OS
  - `Features/Common/Services/SettingStateReader.cs` — Read OS state
  - `Features/Common/Services/WindowsRegistryService.cs` — Registry I/O
  - `DI/InfrastructureServiceExtensions.cs` — DI registration

**src/AkariTool.App/**
- Purpose: WinUI 3 shell, pages, ViewModels, dialogs, and presentation logic
- Contains: App.xaml(.cs), MainWindow, Pages (XAML), ViewModels (SettingPageViewModel + concrete impls), DI setup, services
- Key files:
  - `App.xaml.cs` — Entry point, DI container, OnLaunched
  - `MainWindow.xaml(.cs)` — Shell window, nav rail, page Frame
  - `ViewModels/Tweaks/SettingPageViewModel.cs` — Base VM for all tweak pages
  - `ViewModels/GamingViewModel.cs` — Gaming page implementation
  - `DI/UIServiceExtensions.cs` — ViewModel & App service registration

**tests/**
- Purpose: Unit tests for models, services, and logic (NO WinUI, NO real registry)
- Core.Tests: SettingDefinition model tests, badge computation, catalog validation, version gates
- Infrastructure.Tests: State reader, executor, resolver, power plans, compatibility filters, WinGet, update service

**vendor/WinUI.Framework/**
- Purpose: Local MVVM framework (ProjectReference, not NuGet)
- Provides: ViewModelBase, INavigationService, IDispatcherService, FileLogService, LocalizationService, ServiceLocator

## Key File Locations

**Entry Points:**
- `src/AkariTool.App/App.xaml.cs` — Application startup, DI configuration, OnLaunched lifecycle
- `src/AkariTool.App/MainWindow.xaml.cs` — Shell window, nav routing, frame hosting
- `src/AkariTool.App/App.xaml` — WinUI 3 resource dictionaries (theme colors, etc.)

**Configuration:**
- `AkariTool.sln` — Solution definition (3 main projects + 2 tests + 2 vendor)
- `src/AkariTool.App/DI/UIServiceExtensions.cs` — ViewModel + service registration
- `src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs` — Infrastructure service registration
- `.claude/settings.json` — Claude Code preferences (if present)

**Core Logic:**
- `src/AkariTool.Core/Features/Common/Models/SettingDefinition.cs` — Core row model
- `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs` — Base SettingPageViewModel
- `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` — Per-row ViewModel
- `src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs` — Apply operations to OS
- `src/AkariTool.Infrastructure/Features/Common/Services/SettingStateReader.cs` — Read OS state for badges

**Catalogs (Feature Definitions):**
- `src/AkariTool.Core/Features/Gaming/Catalogs/GamingOptimizations.cs` — Gaming settings
- `src/AkariTool.Core/Features/Privacy/Catalogs/PrivacyOptimizations.cs` — Privacy settings
- `src/AkariTool.Core/Features/Power/Catalogs/PowerOptimizations.cs` — Power settings
- `src/AkariTool.Core/Features/Customize/Catalogs/*.cs` — Taskbar, Explorer, Appearance, StartMenu, Desktop

**Pages (XAML + CodeBehind):**
- `src/AkariTool.App/Views/GamingPage.xaml(.cs)` — Gaming page UI
- `src/AkariTool.App/Views/HomePage.xaml(.cs)` — Landing page
- `src/AkariTool.App/Views/Templates/TweakTemplates.xaml` — Row data templates (Toggle, Selection, etc.)

**Testing:**
- `tests/AkariTool.Core.Tests/` — Model & logic tests (no OS calls)
- `tests/AkariTool.Infrastructure.Tests/Features/SettingStateReaderTests.cs` — State reader tests
- `tests/AkariTool.Infrastructure.Tests/Features/SettingOperationExecutorTests.cs` — Executor tests (mocked)

**Build Scripts:**
- `build-installer.ps1` — MSI package automation
- `build-deelevated.ps1` — Deelevated process runner

## Naming Conventions

**Files:**
- `[FeatureName]ViewModel.cs` — Concrete SettingPageViewModel (e.g., GamingViewModel.cs)
- `[FeatureName]Page.xaml` — XAML page for a feature (e.g., GamingPage.xaml)
- `[FeatureName]Optimizations.cs` — Catalog method in Core (e.g., GamingOptimizations.cs)
- `[Service]Tests.cs` — Test file for a service (e.g., SettingStateReaderTests.cs)
- Interfaces: `I[ServiceName].cs` (e.g., ISettingOperationExecutor.cs)

**Directories:**
- `Features/[FeatureName]/Catalogs/` — Catalog files for a feature
- `Features/Common/` — Shared models, enums, interfaces (every project has this)
- `ViewModels/[FeatureName]/` — Feature-specific ViewModels
- `Views/` → Top-level pages; `Views/Controls/` → Reusable components; `Views/Templates/` → Data templates
- `Services/` → App-layer services (Context-dependent, WinUI, etc.); Infrastructure/Features/*/Services/ → OS services

**Naming Patterns:**
- **Catalog classes:** PascalCase method `static Build()` returning `IReadOnlyList<SettingGroup>`
- **ViewModel properties:** `ObservableProperty` attributes via MVVM Toolkit (auto-generates OnXChanged)
- **View bindings:** `{x:Bind ViewModel.PropertyName}` (compile-time safe)
- **DI registration:** SingletonVM instances; services wired by type
- **Record models:** All immutable; init-only properties

## Where to Add New Code

**New Feature (Full Stack):**
1. **Core Catalog:** `src/AkariTool.Core/Features/[FeatureName]/Catalogs/[FeatureName]Optimizations.cs` — Define SettingGroup[] + SettingDefinitions
2. **Concrete ViewModel:** `src/AkariTool.App/ViewModels/[FeatureName]ViewModel.cs` — Extend SettingPageViewModel, implement BuildSettingGroups()
3. **XAML Page:** `src/AkariTool.App/Views/[FeatureName]Page.xaml` — Bind to ViewModel, use row templates from TweakTemplates.xaml
4. **DI Registration:** `src/AkariTool.App/DI/UIServiceExtensions.cs` — AddSingleton([FeatureName]ViewModel) + register as SettingPageViewModel marker
5. **NavTag Routing:** `src/AkariTool.App/MainWindow.xaml.cs` — Add [FeatureName] to PageMap

**New SettingDefinition Row:**
1. Open the corresponding catalog file in `src/AkariTool.Core/Features/[FeatureName]/Catalogs/`
2. Add a new SettingDefinition record to the appropriate SettingGroup array
3. Specify: Id, Name, Description, InputType, RegistrySettings (or Tasks/Scripts/PowerCfg), RecommendedValue, DefaultValue
4. Tests automatically pick up the row; validate at startup via SettingCatalogValidator

**New Infrastructure Service:**
1. **Interface:** Define in `src/AkariTool.Infrastructure/Features/[Scope]/Interfaces/I[ServiceName].cs`
2. **Implementation:** `src/AkariTool.Infrastructure/Features/[Scope]/Services/[ServiceName].cs`
3. **Registration:** Add to `src/AkariTool.Infrastructure/DI/InfrastructureServiceExtensions.cs`
4. **Usage:** Inject via constructor into SettingPageViewModel or SettingItemViewModel

**New Page (Non-Declarative):**
1. **ViewModel:** `src/AkariTool.App/ViewModels/[PageName]/[PageName]ViewModel.cs` — Extend ViewModelBase
2. **XAML Page:** `src/AkariTool.App/Views/[PageName]Page.xaml`
3. **DI Registration:** `src/AkariTool.App/DI/UIServiceExtensions.cs` — AddSingleton (if stateful) or AddTransient
4. **Navigation:** `MainWindow.xaml.cs` PageMap + rail item in MainWindow.xaml

**Tests:**
- **Core logic tests:** `tests/AkariTool.Core.Tests/Features/` — No OS calls, no WinUI
- **Infrastructure tests:** `tests/AkariTool.Infrastructure.Tests/Features/` — Mock all OS services via NSubstitute
- **Run with:** `dotnet test` (not MSBuild; tests are pure .NET)

## Special Directories

**vendor/WinUI.Framework/**
- Purpose: Local vendored MVVM framework (ProjectReference, not NuGet package)
- Generated: No
- Committed: Yes (part of repo)
- Why: Framework source is controlled in-tree; easier to patch/tweak without waiting for releases

**vendor/WinGet.Interop/**
- Purpose: COM interop bindings for Windows Package Manager (WinGet)
- Generated: No (hand-written, but tied to WinGet SDK)
- Committed: Yes

**(Legacy) Akari-Tool-MVVM/**
- Purpose: Fallback WPF+WinUI 3 hybrid build (kept as shipping fallback)
- Generated: No
- Committed: Yes
- Status: Not in active development; maintained only for backward compatibility on older test VMs

**.claude/** directory
- Purpose: Local Claude Code configuration (not committed to repo)
- Generated: Yes (by Claude Code harness)
- Committed: No (.gitignore)

**scripts/ folder (if present)**
- Embedded PowerShell + batch scripts for system operations
- Located: `src/AkariTool.App/Scripts/`
- Usage: Embedded in the executable; extracted by ToolService at runtime

---

*Structure analysis: 2026-08-27*
