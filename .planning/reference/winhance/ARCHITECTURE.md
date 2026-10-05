<!-- refreshed: 2026-10-05 -->
# Architecture

**Analysis Date:** 2026-10-05
**Reference repository:** `C:/Users/isleap/Documents/GitHub/Winhance`
**Scale:** 4 source projects, ~520 tracked `.cs` files in `src/` (240 Core / 104 Infrastructure / 176 UI), 35 `.xaml`, ~193 test files.

> This document describes the **target** architecture for Akari Tool. Everything stated here
> is observable in the Winhance repository today. Path references are relative to
> `C:/Users/isleap/Documents/GitHub/Winhance/`.

---

## System Overview

Winhance is a **3-layer Clean Architecture** application crossed with **vertical feature slices**.
The layering axis is enforced by project references. The feature axis is enforced purely by
folder convention inside each project. The defining trait is that **the same feature path is
repeated in all three layers**, so one feature's entire vertical extent is greppable and
navigable by a single path prefix.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                         Winhance.UI  (WinUI 3, net10.0-windows)              │
│  src/Winhance.UI/                                                          │
│                                                                             │
│  Features/                                                                  │
│  ┌──────────────┬──────────────┬───────────────┬──────────────┬───────────┐ │
│  │  Optimize/   │  Customize/  │ AdvancedTools/│ SoftwareApps/│ Settings/ │ │
│  │  Pages/ VM/  │  Pages/ VM/  │  VM/ Models/  │  VM/ Views/  │  VM/      │ │
│  │  Models/     │  Models/     │  Services/    │  Models/     │           │ │
│  │  Interfaces/ │  Interfaces/ │               │  Services/   │           │ │
│  ├──────────────┴──────────────┴───────────────┼──────────────┼───────────┤ │
│  │  Common/  Controls, Converters, Dialogs,    │              │           │ │
│  │           Services, ViewModels, Resources,   │              │           │ │
│  │           Extensions/DI (CompositionRoot)    │              │           │ │
│  └──────────────────────┬──────────────────────┴──────────────┴───────────┘ │
│                         │                                                  │
│  MainWindow.xaml.cs · Helpers/NavigationRouter.cs · ViewModels/MainWindowVM  │
└─────────────────────────┬──────────────────────────────────────────────────┘
                          │  project reference
┌─────────────────────────▼──────────────────────────────────────────────────┐
│                    Winhance.Infrastructure                                  │
│  src/Winhance.Infrastructure/                                               │
│  Features/                                                                  │
│  ┌──────────────┬──────────────┬───────────────┬──────────────────────────┐ │
│  │  Optimize/   │  Customize/  │ AdvancedTools/│  SoftwareApps/            │ │
│  │  Services/   │  Services/   │  Services/    │   Services/ + WinGet/     │ │
│  │              │              │  Helpers/     │   Services/WinGet/        │ │
│  │              │              │  ScriptSect.  │   Utilities/              │ │
│  ├──────────────┴──────────────┴───────────────┴──────────────────────────┤ │
│  │  Common/  Services, Events, EventHandlers, Helpers, Utilities          │ │
│  └──────────────────────┬──────────────────────┬──────────────────────────┘ │
│  Extensions/DI/InfrastructureServicesExtensions.cs                          │
└─────────────────────────┬──────────────────────────────────────────────────┘
                          │  project reference
┌─────────────────────────▼──────────────────────────────────────────────────┐
│                          Winhance.Core                                      │
│  src/Winhance.Core/Features/                                                 │
│  ┌──────────────┬──────────────┬───────────────┬──────────────────────────┐ │
│  │  Optimize/   │  Customize/  │ AdvancedTools/│  SoftwareApps/            │ │
│  │  Models/     │  Models/     │  Interfaces/  │   Interfaces/             │ │
│  │  Interfaces/ │  Interfaces/ │  Models/      │   Models/ + Enums/       │ │
│  │              │              │               │   Utilities/              │ │
│  ├──────────────┴──────────────┴───────────────┴──────────────────────────┤ │
│  │  Common/  Constants, Converters, Enums, Events, Exceptions,            │ │
│  │           Extensions, Helpers, Interfaces, Localization, Models,       │ │
│  │           Native (P/Invoke), Services, Utils, Validation              │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────────┘
```

A fourth project, `src/WindowsPackageManager.Interop/`, is a hand-maintained CsWinRT
projection of the `WindowsPackageManager` COM API (`WindowsPackageManagerFactory.cs`,
`WindowsPackageManagerElevatedFactory.cs`, `WindowsPackageManagerStandardFactory.cs`).
It is referenced **only** by `Winhance.Infrastructure` — never by Core, never by UI.

---

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Entry point | Single-instance enforcement via `AppLifecycle` before WinUI init | `src/Winhance.UI/Program.cs` |
| App | Builds the DI host, wires logging/localization/theme, creates `MainWindow` | `src/Winhance.UI/App.xaml.cs` |
| Shell window | Hosts `NavSidebar` + `ContentFrame`, owns startup kickoff | `src/Winhance.UI/MainWindow.xaml.cs` |
| Navigation router | Tag → Page-type map for the 5 top-level pages | `src/Winhance.UI/Helpers/NavigationRouter.cs` |
| Composition root | The single `IServiceCollection` wiring point | `src/Winhance.UI/Features/Common/Extensions/DI/CompositionRoot.cs` |
| Settings registry | Aggregates every domain's setting catalog, applies OS/hardware filters | `src/Winhance.Infrastructure/Features/Common/Services/CompatibleSettingsRegistry.cs` |
| Setting application | Generic apply engine driven by `SettingDefinition` payloads | `src/Winhance.Infrastructure/Features/Common/Services/SettingApplicationService.cs` |
| Event bus | Cross-slice notification (settings applied, mode exited, tooltips) | `src/Winhance.Infrastructure/Features/Common/Events/EventBus.cs` |
| Special-handler registry | Id-keyed plug-in point so domains can own non-registry settings | `src/Winhance.Core/Features/Common/Interfaces/ISpecialSettingHandlerRegistry.cs` |
| Settings VM factory | Builds `SettingItemViewModel` instances from `SettingDefinition` | `src/Winhance.UI/Features/Common/Services/SettingViewModelFactory.cs` |
| Startup orchestrator | Phased startup sequence, extracted from `MainWindow` for testability | `src/Winhance.UI/Features/Common/Services/StartupOrchestrator.cs` |

---

## Pattern Overview

**Overall:** Clean Architecture (3 assemblies, one-directional dependencies) with
**Vertical Feature Slices** as the organising principle inside every assembly.

**Key Characteristics:**

1. **One path, three layers.** Every feature domain exists at the identical relative path
   in all three projects:
   - `src/Winhance.Core/Features/<Domain>/`
   - `src/Winhance.Infrastructure/Features/<Domain>/`
   - `src/Winhance.UI/Features/<Domain>/`

   Only `Core` and `Infrastructure` use the `Features/` prefix in their root; the UI project
   has no other top-level source folders besides `Helpers/`, `ViewModels/`, `Assets/`, so
   `Features/` is likewise its single source root.

2. **Five domains + `Common`.** `Optimize`, `Customize`, `AdvancedTools`, `SoftwareApps`,
   `Settings`, plus `Common` as a shared base in each layer. A sixth, `WindowsPackageManager.Interop`,
   is a satellite support library, not a slice.

3. **`Settings` is a UI-only slice.** It has no `Core` and no `Infrastructure` counterpart.
   It is the one domain that demonstrates a slice may occupy fewer than three layers.

4. **Every domain folder carries `Interfaces/` + `Models/` in Core**, `Services/` in
   Infrastructure, and `ViewModels/` (plus `Pages/` or `Views/`) in UI. `Common` adds
   `Interfaces/`, `Services/`, `Helpers/`, `Utilities/`, `Events/`, `Models/`, `Enums/`,
   `Constants/`, `Extensions/`, `Validation/`, `Native/`.

5. **Domain → Common is the only permitted intra-layer direction.** Every domain imports
   `...Features.Common.*`; the reverse happens only at three sanctioned choke points
   (composition root, navigation router, review-mode coordinator).

6. **MVVM with CommunityToolkit.Mvvm source generators.** ViewModels are `partial class`
   deriving from `ObservableObject`, using `[ObservableProperty]`, `[RelayCommand]`, and
   `partial void On<Prop>Changed`. All registrations are singletons except a small
   allowlist (`SettingsViewModel`, `AutounattendGeneratorViewModel`).

---

## Layers

**Core** (`src/Winhance.Core/`)
- Purpose: data contracts, setting catalogs, enums, constants, event contracts, P/Invoke
  declarations, and a handful of dependency-free service implementations.
- Contains: `Features/<Domain>/{Interfaces,Models,...}`, `Features/Common/{Constants,Converters,Enums,Events,Exceptions,Extensions,Helpers,Interfaces,Localization,Models,Native,Services,Utils,Validation}`.
- Depends on: nothing project-local. Package references only
  (`Microsoft.Extensions.Hosting`, `CommunityToolkit.Mvvm`, `System.Data.SqlClient`,
  `System.IO.Packaging`, `System.DirectoryServices.Protocols`, `Microsoft.Windows.Compatibility`).
- Used by: Infrastructure and UI.
- Notable: `Features/Common/Native/*.cs` holds 8 `LibraryImport`-style P/Invoke surfaces
  (`PowerProf`, `DismApi`, `MsiApi`, `User32Api`, `ConPtyApi`, `Kernel32Api`, `SrClientApi`,
  `UserTokenApi`). `Features/Common/Services/` holds five real implementations —
  `LogService`, `StartupLogger`, `InitializationService`, `GlobalSettingsRegistry`,
  `DependencyManager` — all of which are pure in-process logic with no OS side effects.

**Infrastructure** (`src/Winhance.Infrastructure/`)
- Purpose: OS-touching implementations of Core interfaces, plus all DI registration for them.
- Contains: `Features/<Domain>/{Services,Helpers,ScriptSections}`, `Features/Common/{Services,Events,EventHandlers,Helpers,Utilities}`, `Extensions/DI/InfrastructureServicesExtensions.cs`.
- Depends on: `Winhance.Core`, `WindowsPackageManager.Interop`.
- Used by: UI only.
- Notable: carries vendored binaries at
  `Features/SoftwareApps/Services/WinGet/winget-cli/` (`winget.exe`,
  `WindowsPackageManagerServer.exe`, runtime DLLs, `winget-version.txt`) copied to output
  by `Winhance.Infrastructure.csproj`.

**UI** (`src/Winhance.UI/`)
- Purpose: WinUI 3 pages, controls, converters, ViewModels, and the composition root.
- Contains: `Features/<Domain>/...`, `Features/Common/...`, `Helpers/`, `ViewModels/`, `Assets/`, `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `Program.cs`, `NativeMethods.txt`.
- Depends on: `Winhance.Core`, `Winhance.Infrastructure`, WinAppSDK 1.8, CsWin32.
- Notable: `MainWindow.xaml.cs` is ~900 lines of shell + startup + mode-switching;
  feature pages keep their own code-behind thin and delegate to their slice's ViewModel.

**Tests** (`tests/`) — one xunit project per source project plus one integration project:
- `Winhance.Core.Tests` (17 files) — mirrors the *shape* of `Core/Features` by concern
  (`Models/`, `Services/`, `Validation/`, `Helpers/`, `Constants/`), not by domain.
- `Winhance.Infrastructure.Tests` (83 files) — mixes flat folders (`Services/`, `Utilities/`,
  `Events/`, `Helpers/`) with domain folders (`AdvancedTools/`, `AdvancedTools/WimServices/`).
- `Winhance.UI.Tests` (81 files) — `ViewModels/`, `Services/`, `Converters/`, `Helpers/`, `Utilities/`.
- `Winhance.IntegrationTests` (12 files) — `DI/InfrastructureContainerSmokeTests.cs` verifies
  `AddInfrastructureServices()` alone yields a resolvable container; `Configuration/`,
  `Registry/`, `ScriptGeneration/`, `Pipeline/`, `Localization/`, `FileSystem/`.

---

## The Vertical Slice Rule

### The rule

> **A feature lives at exactly one path per layer, and the path segment is identical across layers.**

To work on a feature you never leave the `<Domain>` directory prefix. To add a new feature
you create the folder in all three layers at once.

### The one documented exception

`Settings` exists **only** in the UI layer:

- `src/Winhance.UI/Features/Settings/SettingsPage.xaml` + `SettingsPage.xaml.cs`
- `src/Winhance.UI/Features/Settings/ViewModels/SettingsViewModel.cs`

There is no `src/Winhance.Core/Features/Settings/` and no
`src/Winhance.Infrastructure/Features/Settings/`. See the *Settings* section below.

### Verified per-domain presence

| Domain | Core | Infrastructure | UI |
|--------|------|----------------|-----|
| `Optimize` | 9 files / 10,558 loc | 2 files / 1,233 loc | 21 files / 4,691 loc |
| `Customize` | 5 files / 5,025 loc | 2 files / 136 loc | 12 files / 1,200 loc |
| `AdvancedTools` | 8 files / 124 loc | 13 files / 3,394 loc | 15 files / 2,827 loc |
| `SoftwareApps` | 50 files / 5,363 loc | 28 files / 8,181 loc | 15 files / 2,794 loc |
| `Settings` | **absent** | **absent** | 2 files / 314 loc |
| `Common` | 168 files / 4,718 loc | 58 files / 10,927 loc | 98 files / 11,598 loc |

Note the inversion: `Optimize` is a **Core-heavy** slice (its data catalogs are 4,151 + 2,918 +
1,467-line files), while `SoftwareApps` and `AdvancedTools` are **Infrastructure-heavy** slices.
A domain's shape follows what it does, not a template.

---

## Domain: `Optimize`

Windows performance/privacy/power tuning expressed as registry-driven settings.

**Core — `src/Winhance.Core/Features/Optimize/`**
- `Interfaces/IPowerService.cs` — `GetActivePowerPlanAsync`, `GetAvailablePowerPlansAsync`, `DeletePowerPlanAsync`. The only Core interface.
- `Models/PrivacyOptimizations.cs` (2,918 loc) — static class, `GetPrivacyAndSecurityOptimizations()` → `SettingGroup`.
- `Models/GamingAndPerformanceOptimizations.cs` (4,151 loc) — largest file in the repo.
- `Models/PowerOptimizations.cs` (1,467), `UpdateOptimizations.cs` (736),
  `NotificationOptimizations.cs` (463), `SoundOptimizations.cs` (212).
- `Models/PowerPlan.cs`, `Models/PowerTemplates.cs` (593 loc) — reusable power-plan catalogs
  consumed by AdvancedTools' autounattend builder.

**Infrastructure — `src/Winhance.Infrastructure/Features/Optimize/Services/`**
- `PowerService.cs` — implements `IPowerService`, also implements `ISpecialSettingHandler` for
  `SettingIds.PowerPlanSelection` and `DiscoverSpecialSettingsAsync`. Registered via a
  factory lambda that passes 9 dependencies explicitly.
- `UpdateService.cs` — implements `ISpecialSettingHandler` for `SettingIds.UpdatesPolicyMode`.
- Depends on Common only.

**UI — `src/Winhance.UI/Features/Optimize/`**
- Pages (`.xaml`): `OptimizePage.xaml` (hub, 6 overview cards), plus
  `Pages/GamingOptimizePage.xaml`, `Pages/NotificationOptimizePage.xaml`,
  `Pages/PowerOptimizePage.xaml`, `Pages/PrivacyOptimizePage.xaml`,
  `Pages/SoundOptimizePage.xaml`, `Pages/UpdateOptimizePage.xaml`.
- `Interfaces/IOptimizationFeatureViewModel.cs` — empty marker interface extending
  `ISettingsFeatureViewModel`, used so DI can inject `IEnumerable<IOptimizationFeatureViewModel>`.
- `Models/OptimizeSectionInfo.cs` — implements `ISectionInfo`; the 6 section definitions live
  as a `static readonly` list on `OptimizeViewModel.Sections`.
- `ViewModels/`: `OptimizeViewModel.cs` (hub, extends `SectionPageViewModel<OptimizeSectionInfo>`),
  `BaseSettingsFeatureViewModel.cs` (**shared base** — also used by Customize),
  `SettingItemViewModel.cs` (**shared base** — also used by Customize),
  `SettingsGroup.cs`, `SettingStatusBannerManager.cs`, `TechnicalDetailsManager.cs`, and one
  `XxxOptimizationsViewModel` per section.

**Depends on `Common`?** Yes, heavily — every file imports
`Winhance.Core.Features.Common.{Constants,Enums,Events,Interfaces,Models,Services}`.
`SettingsGroup.cs` and `SettingItemViewModel.cs` living under `Optimize/` and being used by
`Customize/` is the one place a "domain" folder acts as a shared base (see Anti-Patterns).

---

## Domain: `Customize`

Shell/explorer/taskbar/theme visual customization.

**Core — `src/Winhance.Core/Features/Customize/`**
- `Interfaces/IWallpaperService.cs` — `SetWallpaperAsync`, `GetDefaultWallpaperPath`.
- `Models/ExplorerCustomizations.cs` (3,338 loc), `TaskbarCustomizations.cs` (1,006),
  `StartMenuCustomizations.cs` (539), `WindowsThemeCustomizations.cs` (~400).
- Each is a static class exposing `GetXxxCustomizations()` → `SettingGroup`.

**Infrastructure — `src/Winhance.Infrastructure/Features/Customize/Services/`**
- `WallpaperService.cs` — implements `IWallpaperService`.
- `ThemeWallpaperApplier.cs` — a bare concrete class (no Core interface) registered as a
  `SpecialSettingHandlerRegistry` entry for `SettingIds.ThemeModeWindows`.
- Smallest Infrastructure slice: 136 loc across 2 files.

**UI — `src/Winhance.UI/Features/Customize/`**
- Pages: `CustomizePage.xaml` (hub), `Pages/ExplorerCustomizePage.xaml`,
  `Pages/StartMenuCustomizePage.xaml`, `Pages/TaskbarCustomizePage.xaml`,
  `Pages/WindowsThemeCustomizePage.xaml`.
- `Interfaces/ICustomizationFeatureViewModel.cs` — the Customize-side marker interface.
- `Models/CustomizeSectionInfo.cs`.
- `ViewModels/`: `CustomizeViewModel.cs` (extends `SectionPageViewModel<CustomizeSectionInfo>`,
  4 sections), plus 4 `XxxCustomizationsViewModel`.
- The 4 Customize VMs extend `BaseSettingsFeatureViewModel` from
  `Winhance.UI.Features.Optimize.ViewModels` — the clearest cross-slice base-class reuse.

**Depends on `Common`?** Yes. Also transitively on `Optimize` (UI layer only) for the base
ViewModel types.

---

## Domain: `AdvancedTools`

WIM/ISO image tooling and autounattend XML generation. The most tool-like slice.

**Core — `src/Winhance.Core/Features/AdvancedTools/`**
- `Interfaces/IAutounattendXmlGeneratorService.cs`, `IDriverCategorizer.cs`, `IIsoService.cs`,
  `IOscdimgToolManager.cs`, `IWimCustomizationService.cs`, `IWimImageService.cs`.
- `Models/ImageDetectionResult.cs`, `ImageFormatInfo.cs`.
- Only 124 loc total — the Core layer here is a pure contract layer.

**Infrastructure — `src/Winhance.Infrastructure/Features/AdvancedTools/`**
- `Services/AutounattendScriptBuilder.cs` — composite script generator; **imports both
  `Core.Features.Optimize.Models` and `Core.Features.SoftwareApps.Models`**.
- `Services/IsoService.cs`, `OscdimgToolManager.cs`, `WimCustomizationService.cs`, `WimImageService.cs`.
- `Helpers/DriverCategorizer.cs`, `PowerShellScriptUtilities.cs`, `RegistryCommandEmitter.cs`.
- `ScriptSections/` — a small strategy set: `AppRemovalScriptSection.cs`,
  `FeatureRegistryScriptSection.cs`, `PowerSettingsScriptSection.cs`, `ScriptPreambleSection.cs`,
  `SpecialFeatureScriptSection.cs`. `AppRemovalScriptSection` imports `SoftwareApps`;
  `PowerSettingsScriptSection` imports `Optimize`.

**UI — `src/Winhance.UI/Features/AdvancedTools/`**
- Pages: `AdvancedToolsPage.xaml` (hub, 2 cards), `WimUtilPage.xaml`, `AutounattendGeneratorPage.xaml`.
- `Models/AdvancedToolsSectionInfo.cs` (does **not** implement `ISectionInfo` — it uses
  `IconResourceKey` instead of `IconGlyphKey`), `WizardActionCard.cs`, `WizardStepState.cs`.
- `Services/AutounattendXmlGeneratorService.cs` — **implements the Core interface
  `IAutounattendXmlGeneratorService` from inside the UI layer** because it needs
  `ISelectedAppsProvider` (a UI service) and `AutounattendScriptBuilder` (Infrastructure).
- `ViewModels/`: `AdvancedToolsViewModel.cs` (plain `ObservableObject`, **not** a
  `SectionPageViewModel` — AdvancedTools has no `IEnumerable<IFeatureViewModel>` collection),
  `WimUtilViewModel.cs`, `AutounattendGeneratorViewModel.cs`, and a 4-step wizard
  (`WimStep1..4ViewModel.cs`, `WimImageFormatViewModel.cs`).

**Depends on `Common`?** Yes, for logging, task progress, localization, the settings registry.
Also on `Optimize` and `SoftwareApps` for script content — the only Infrastructure slice with
two-way domain coupling.

---

## Domain: `SoftwareApps`

App inventory, install, and removal. The widest slice by file count.

**Core — `src/Winhance.Core/Features/SoftwareApps/`**
- `Interfaces/` — 20 contracts: `IAppIconResolver`, `IAppInstallationService`,
  `IAppStatusDiscoveryService`, `IAppxIconSource`, `IAppxPackageSource`, `IBloatRemovalService`,
  `IChocolateyService`, `IDirectDownloadService`, `IExternalAppsService`,
  `IExternalAppUninstallService`, `IIconManifestService`, `ILegacyCapabilityService`,
  `IOptionalFeatureService`, `IRepoIconSource`, `IStoreDownloadService`, `IWindowsAppsService`,
  `IWindowsAppUninstallService`, `IWinGetBootstrapper`, `IWinGetDetectionService`,
  `IWinGetPackageInstaller`.
- `Models/ItemDefinition.cs` — the domain's central record. Extends `BaseDefinition` and adds
  `AppxPackageName`, `WinGetPackageId`, `MsStoreId`, `CapabilityName`, `OptionalFeatureName`,
  `ChocoPackageId`, `WinGetInstallerOverride`, `RegistryDisplayName`, `RegistrySubKeyName`,
  `DetectionPaths`, `ProcessesToStop`, `HasInstabilityWarning`, plus mutable runtime state
  (`IsInstalled`, `DetectedVia`, `IconPath`).
- `Models/ItemGroup.cs` — `Name`, `Icon`, `FeatureId`, `Items`.
- `Models/WindowsAppDefinitions.cs` (635), `CapabilityDefinitions.cs`, `OptionalFeatureDefinitions.cs`.
- `Models/ExternalAppDefinitions*.cs` — 15 partial-class files split by category
  (`Browsers`, `Compression`, `CustomizationUtilities`, `DevelopmentApps`, `DocumentViewers`,
  `FileDiskManagement`, `Gaming`, `Imaging`, `MessagingEmailCalendar`, `Multimedia`,
  `OnlineStorageBackup`, `OpticalDiscTools`, `OtherUtilities`, `PrivacySecurity`, `RemoteAccess`,
  `RuntimesAndDependencies`) plus the root `ExternalAppDefinitions.cs`.
- `Models/RemovalScript.cs`, `EdgeRemovalScript.cs` (601), `OneDriveRemovalScript.cs` (404),
  `PackageInstallResult.cs`, `RepoIconKey.cs`.
- `Enums/DetectionSource.cs` (`WinGet, Chocolatey, AppX, Registry, FileSystem`),
  `Enums/RemovalOutcome.cs`.
- `Utilities/BloatRemovalScriptGenerator.cs` (398 loc) — pure logic, no OS calls, so it lives in Core.

**Infrastructure — `src/Winhance.Infrastructure/Features/SoftwareApps/Services/`**
- App inventory: `WindowsAppsService.cs`, `ExternalAppsService.cs`, `AppStatusDiscoveryService.cs`.
- Install: `AppInstallationService.cs`.
- Uninstall: `WindowsAppUninstallService.cs`, `ExternalAppUninstallService.cs`, `BloatRemovalService.cs`.
- Package managers: `ChocolateyService.cs`, `StoreDownloadService.cs`, `DirectDownloadService.cs`.
- Icons: `AppIconResolver.cs` (cache-first), `AppxIconSource.cs`, `RepoIconSource.cs` (jsDelivr
  @main, sha256-verified), `IconManifestService.cs`, `IconCacheMigration.cs`,
  `LightVariantSynthesizer.cs`.
- Sources: `AppxPackageSource.cs` (PackageManager COM → WMI → PowerShell fallback chain).
- Capabilities: `LegacyCapabilityService.cs`, `OptionalFeatureService.cs`.
- `Services/WinGet/` — `WinGetBootstrapper.cs`, `WinGetComSession.cs`,
  `WinGetDetectionService.cs`, `WinGetPackageInstaller.cs`.
- `Services/WinGet/Utilities/` — `ConPtyProcess.cs`, `WinGetCliRunner.cs`, `WinGetExitCodes.cs`,
  `WinGetInstaller.cs`, `WinGetProgressParser.cs`.
- `Services/WinGet/winget-cli/` — vendored binaries.
- `WindowsAppsService.cs` imports `Core.Features.Optimize.Models` (one cross-domain import).

**UI — `src/Winhance.UI/Features/SoftwareApps/`**
- `SoftwareAppsPage.xaml` — single page hosting two `DataGrid` views plus a
  card/table/compact view toggle. No sub-pages.
- `Views/ExternalAppsHelpContent.xaml`, `Views/WindowsAppsHelpContent.xaml`.
- `Models/AppSortMode.cs`, `ISelectable.cs`, `SoftwareAppsViewMode.cs`.
- `Services/SelectedAppsProvider.cs` — bridges SoftwareApps state to the AdvancedTools WIM
  feature via the Common `ISelectedAppsProvider` interface.
- `ViewModels/`: `SoftwareAppsViewModel.cs` (hub over two tabs),
  `WindowsAppsViewModel.cs`, `ExternalAppsViewModel.cs`, `AppItemViewModel.cs`,
  `AppSortHelper.cs`, `RemovalStatusViewModel.cs`, `RemovalStatusContainerViewModel.cs`.
- `AppOperationConfirmation.cs` — confirmation payload record.

**Depends on `Common`?** Yes, plus a one-way import of `Optimize` in
`WindowsAppsService.cs`.

---

## Domain: `Settings` (UI-only slice)

`Settings` in Winhance is the **application's own preferences page** (language, theme,
backup/restore, create-restore-point) — not a domain of tunable Windows settings. That is
exactly why it has no Core and no Infrastructure counterpart: it is a thin page over
already-registered Common services.

**UI — `src/Winhance.UI/Features/Settings/`**
- `SettingsPage.xaml` + `SettingsPage.xaml.cs` (31 lines). Resolves its ViewModel from DI,
  sets `NavigationCacheMode.Disabled` (the only page that does — it is cheap to rebuild).
- `ViewModels/SettingsViewModel.cs` (299 lines + a `ThemeOption` record). Depends on
  `ILocalizationService`, `IThemeService`, `IUserPreferencesService`, `IDialogService`,
  `IConfigurationService`, `ILogService`, `ISystemBackupService`, `ITaskProgressService` —
  **all Common services, zero domain imports**. Registered `AddTransient<SettingsViewModel>()`;
  it is the only feature ViewModel that is transient, and it implements `IDisposable` to
  unsubscribe `LanguageChanged`.
- Commands: `ImportConfigAsync`, `ExportConfigAsync`, `CreateRestorePointAsync`.

**Why this matters for Akari:** Winhance proves a slice may occupy one layer. A slice that
only composes existing Common services needs no `Interfaces/` and no `Models/` — it needs
`ViewModels/` and a page. If Akari's settings surface is likewise app-preferences rather than
a tunable-setting catalog, keeping it UI-only is the faithful replication.

---

## `Common/` — the shared base, and the rule for what goes there

### What is in `Common`

**`src/Winhance.Core/Features/Common/`** (168 files, 4,718 loc — many files are 5–30 lines)
- `Constants/` — `FeatureIds.cs` (14 feature-id constants), `SettingIds.cs`, `FeatureDefinitions.cs`
  (the `(Id, DefaultName, Category)` master list with derived `OptimizeFeatures` / `CustomizeFeatures`
  sets), `UserPreferenceKeys.cs`, `ScriptPaths.cs`, `ComboBoxConstants.cs`, `ConfigFileConstants.cs`.
- `Enums/` — `InputType`, `DetectionType`, `LogLevel`, `WinhanceMode`, `SettingBadgeKind`,
  `SettingBadgeMode`, `BulkActionType`, `ScriptOption`, `RunContext`, `BuilderTarget`,
  `ImportOption`, `AppChangeKind`, `SponsorsDialogMode`.
- `Events/` — `IEventBus`, `IDomainEvent`, `ISubscriptionToken`, plus
  `SettingAppliedEvent`, `PowerPlanChangedEvent`, `BuilderModeExitedEvent`,
  `ReviewModeExitedEvent`, and `Events/UI/{FilterStateChangedEvent,SettingsRefreshedEvent,TooltipUpdatedEvent}`.
- `Exceptions/` — `ExecutionPolicyException`, `InsufficientDiskSpaceException`.
- `Extensions/` — `TaskExtensions.cs` (`FireAndForget(logService)`),
  `TaskProgressServiceExtensions.cs`.
- `Helpers/` — `BuildVersionGate.cs`, `ZoomLevels.cs`.
- `Interfaces/` — 90 files. The contracts every layer agrees on.
- `Localization/` — `SettingLocalizationKeys.cs` (key-name builders only; the actual JSON lives in UI).
- `Models/` — 40 files. `BaseDefinition`, `SettingDefinition`, `SettingGroup`, `RegistrySetting`,
  `PowerCfgSetting`, `NativePowerApiSetting`, `PowerShellScriptSetting`, `RegContentSetting`,
  `ScheduledTaskSetting`, `OperationResult`, `SettingDependency`, `SettingStateResult`,
  `SettingTooltipData`, `UnifiedConfigurationFile`, `ConfigurationItem`, `ComboBoxOption`,
  `ComboBoxMetadata`, `NumericRangeMetadata`, `PowerRecommendation`, `SystemInfo`, `VersionInfo`, …
- `Native/` — 8 P/Invoke surfaces.
- `Services/` — 5 concrete implementations: `LogService`, `StartupLogger`, `InitializationService`,
  `GlobalSettingsRegistry`, `DependencyManager`.
- `Utils/SearchHelper.cs`.
- `Validation/SettingCatalogValidator.cs` — enforces catalog invariants (e.g. at most one
  Recommended/Default option per selection setting).

**`src/Winhance.Infrastructure/Features/Common/`** (58 files, 10,927 loc)
- `Services/` — 46 implementations: `WindowsRegistryService`, `ProcessExecutor`,
  `SystemInfoProvider`, `WindowsVersionService`, `LocalizationService`, `UserPreferencesService`,
  `CompatibleSettingsRegistry`, `SystemSettingsDiscoveryService`, `SettingApplicationService`,
  `SettingOperationExecutor`, `SettingDependencyResolver`, `RecommendedSettingsApplier`,
  `BulkSettingsActionService`, `ScheduledTaskService`, `SystemBackupService`, `SystemRestoreService`,
  `PowerCfgApplier`, `ComboBoxResolver`, `ComboBoxSetupService`, `TaskProgressService`,
  `TooltipDataService`, `ConfigMigrationService`, `ConfigurationApplicationBridgeService`, …
- `Events/EventBus.cs` — the singleton dispatcher.
- `EventHandlers/TooltipRefreshEventHandler.cs`.
- `Helpers/RecommendedSettingsResolver.cs`.
- `Utilities/` — `ArchitectureHelper`, `DismSessionManager`, `NumericConversionHelper`,
  `PowerPlanHelper`, `PowerShellRunner`, `RegistryValueFormatter`, `ValueComparer`.

**`src/Winhance.UI/Features/Common/`** (98 files, 15 xaml, 11,598 loc)
- `Controls/` (9 custom controls + 8 `.xaml`): `SettingsListView`, `SettingsCardItem`,
  `SettingDescriptionWithBadges`, `PowerPlanComboBox`, `NavSidebar`, `NavButton`,
  `TaskProgressControl`, `QuietInfoBar`, `UniformWrapPanel`, `WebsiteLinkButton`, `ComboBoxEx`.
- `Converters/` (12) + `TemplateSelectors/` (2).
- `Dialogs/` — `ConfigImportDialogBuilder`, `SponsorsDialogBuilder`, `TaskOutputDialogBuilder`.
- `Services/` (27) — `DialogService`, `ThemeService`, `ResourceService`, `DispatcherService`,
  `MainWindowProvider`, `FilePickerService`, `ConfigurationService`, `ConfigExportService`,
  `ConfigLoadService`, `ConfigReviewService`, `SettingViewModelFactory`, `SettingViewModelEnricher`,
  `SettingPreparationPipeline`, `SettingsLoadingService`, `StartupOrchestrator`, …
- `Extensions/DI/` — **the composition root** (`CompositionRoot.cs`,
  `SettingServicesExtensions.cs`, `UIServicesExtensions.cs`).
- `Helpers/` — `FeatureBadgeAggregator`, `PageScrollHelper`, `ReviewModeFilter`,
  `TextScaleHelper`, `Win32FileDialogHelper`, `AutoSuggestBoxExtensions`.
- `Interfaces/` (16), `Models/` (6), `ViewModels/` (`BaseViewModel`, `SectionPageViewModel<T>`,
  `MoreMenuViewModel`), `Views/ConfigImportOverlayWindow.xaml`, `Resources/` (5 xaml dictionaries +
  `Configs/*.winhance` + `AdvancedTools/autounattend-template.xml`), `Localization/` (29 JSON files,
  174–337 KB each), `Utilities/` (7).

### The rule for Common vs. domain-specific

Apply in this order; the first match wins.

1. **Two or more domains need it → `Common`.** `FeatureIds`, `SettingIds`, `RegistrySetting`,
   `SettingDefinition`, `ILogService`, `ITaskProgressService`, `IDialogService`,
   `ISettingsFeatureViewModel`, `ISectionInfo` are all in `Common` because Optimize *and*
   Customize *and* SoftwareApps *and* AdvancedTools all touch them.
2. **Exactly one domain needs it → that domain's folder.** `PowerPlan` and `PowerTemplates` live
   in `Optimize/Models/` and are imported by AdvancedTools; the import is the exception, not
   the rule. `ItemDefinition` lives in `SoftwareApps/Models/`, not `Common/Models/`, even though
   it extends `BaseDefinition`.
3. **A domain needs it but so would every future domain → still `Common`, via an interface in
   `Common/Interfaces/` and a registry.** This is the `ISpecialSettingHandler` pattern:
   `Common/Interfaces/ISpecialSettingHandler.cs` declares the contract, the concrete handlers
   live in their domain (`PowerService`, `UpdateService`, `ThemeWallpaperApplier`), and
   `SettingServicesExtensions.cs` wires them into `ISpecialSettingHandlerRegistry` keyed by
   `SettingIds`. Adding a domain-specific apply path therefore requires **zero edits to Common**.
4. **An app-level concern, not a Windows-tuning concern → `Common`.** Language, theme, config
   import/export, backup/restore, startup sequencing, navigation, window chrome, and zoom all
   live in `Common` because none of them belong to a tuning domain.
5. **A view concern used by >1 page → `Common/Controls` + `Common/Resources`.** All
   `SettingTemplates.xaml` templates and the settings card/list controls are shared, so
   `SoundOptimizePage.xaml` and `TaskbarCustomizePage.xaml` render from the same vocabulary.
6. **Never put a page in `Common`.** The one exception is
   `Common/Views/ConfigImportOverlayWindow.xaml` — a *window*, not a page, because it is
   app-level chrome rather than a slice surface.

**Corollaries observed in the code:**
- `Common` never contains a `SettingDefinition` catalog. Catalogs only exist in domain `Models/`.
- Every domain imports `Common`. The only files importing two or more domains are
  `CompositionRoot`-adjacent (`UIServicesExtensions.cs`, `SettingServicesExtensions.cs`),
  `ReviewModeViewModelCoordinator.cs`, `ConfigExportService.cs`, and
  `NavigationRouter.cs`.
- `Core` has **zero** cross-domain imports (verified by grep across
  `src/Winhance.Core/Features/**`).

---

## Dependency Injection

### Where registrations live

| File | Responsibility |
|------|----------------|
| `src/Winhance.UI/Features/Common/Extensions/DI/CompositionRoot.cs` | Orchestrator. `ConfigureWinhanceServices()` calls the three below in order. `CreateWinhanceHost()` wraps them in `Host.CreateDefaultBuilder()`. |
| `src/Winhance.Infrastructure/Extensions/DI/InfrastructureServicesExtensions.cs` | `AddInfrastructureServices()` — 80+ `AddSingleton` calls, all Infrastructure implementations + AdvancedTools. |
| `src/Winhance.UI/Features/Common/Extensions/DI/SettingServicesExtensions.cs` | `AddSettingServices()` → `AddCustomizationServices()`, `AddOptimizationServices()`, `AddSoftwareAppServices()`. Also the two id-keyed handler registries. |
| `src/Winhance.UI/Features/Common/Extensions/DI/UIServicesExtensions.cs` | `AddUIServices()` — UI services, dialogs, config services, all feature ViewModels. |

The order in `CompositionRoot` is load-bearing and is commented as *"Register services in
dependency order"*:

```csharp
services
    .AddInfrastructureServices() // Infrastructure implementations
    .AddSettingServices()        // Setting services (Customization, Optimization, SoftwareApps)
    .AddUIServices();            // UI-specific services (ThemeService, etc.)
```

### How registrations are organized

1. **Grouped by concern with `//` comment banners**, never alphabetical:
   `// Core Infrastructure Services`, `// Windows Services`, `// Event Bus`,
   `// Settings Discovery and Application`, `// ComboBox Services`, `// Script Services`,
   `// System Services`, `// Task Progress Service`, `// Advanced Tools Services — …`.
2. **One method per feature domain** in `SettingServicesExtensions`:
   `AddCustomizationServices()`, `AddOptimizationServices()`, `AddSoftwareAppServices()`.
   `AddSettingServices()` is a three-call chain. Adding a domain adds one method + one chain link.
3. **`AddInfrastructureServices()` deliberately registers AdvancedTools only.** Optimize,
   Customize, and SoftwareApps Infrastructure services are registered from the UI layer's
   `AddSettingServices()`. This is a real, documented asymmetry —
   `AutounattendScriptBuilder` is registered inside `AddInfrastructureServices` (lines 147–159)
   because nothing in Infrastructure depends on it.
4. **Overwrite-by-later-registration is the documented mechanism.** `AddInfrastructureServices`
   `TryAddSingleton`s empty `ISpecialDiscoveryRegistry` / `ISpecialSettingHandlerRegistry`
   defaults so the Infrastructure container is self-contained for smoke tests; the UI layer then
   registers the real ones with `AddSingleton`, and *last registration wins*. The source comment
   says so explicitly (lines 81–93).
5. **Dual registration: concrete + interface, resolved from the same instance:**
   ```csharp
   services.AddSingleton<PowerService>(sp => new PowerService(/* 9 explicit deps */));
   services.AddSingleton<IPowerService>(sp => sp.GetRequiredService<PowerService>());
   ```
   The pattern repeats for `ConfigReviewService` → `IConfigReviewModeService` /
   `IConfigReviewDiffService` / `IConfigReviewBadgeService` / `IApplicationModeService`
   (4 aliases off one instance), `TaskProgressService` → `ITaskProgressService` /
   `IMultiScriptProgressService`, and `WindowsAppsViewModel` → `IWindowsAppsItemsProvider`.
6. **Everything is `AddSingleton` by default.** The exceptions, all deliberate and commented:
   - `AddTransient<SettingsViewModel>()` — "Settings page is lightweight - no need for caching".
   - `AddTransient<AutounattendGeneratorViewModel>()`.
   - Everything else, including every feature ViewModel, is a singleton "for state preservation
     during inner navigation".
7. **ViewModels registered against marker interfaces** so hubs receive
   `IEnumerable<IFeatureViewModel>`:
   ```csharp
   services.AddSingleton<IOptimizationFeatureViewModel, SoundOptimizationsViewModel>();
   // … 5 more
   services.AddSingleton<ICustomizationFeatureViewModel, ExplorerCustomizationsViewModel>();
   // … 3 more
   ```
   `OptimizeViewModel` then calls `GetFeatureByModuleId(FeatureIds.Sound)` to expose
   `SoundViewModel` for `x:Bind`.
8. **No assembly scanning, no `[Service]` attributes, no auto-registration.** Every binding is an
   explicit line. `AddInfrastructureServices()` is safe to call standalone in a test
   (`tests/Winhance.IntegrationTests/DI/InfrastructureContainerSmokeTests.cs`).

### Runtime resolution

`App.Services` (`App.xaml.cs:29`) exposes `IHost.Services`. Pages resolve their ViewModel in
the constructor via `App.Services.GetRequiredService<T>()` — no constructor injection into pages:

```csharp
public sealed partial class SoundOptimizePage : Page
{
    public OptimizeViewModel ViewModel { get; }
    public SoundOptimizePage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
    }
}
```

Sub-pages resolve the **hub** ViewModel, not their own feature VM, and reach into the hub's
named interface-typed property (`_ = ViewModel.SoundViewModel.RefreshSettingStatesAsync();`).

---

## Data Flow

### Primary Request Path — load and display settings

1. `Program.Main` → single-instance check → `Application.Start` → `new App()`
   (`src/Winhance.UI/Program.cs:20`).
2. `App.OnLaunched` builds the host: `CompositionRoot.CreateWinhanceHost().Build()`
   (`src/Winhance.UI/App.xaml.cs:110`).
3. `MainWindow` is created and activated; `StartStartupOperations()` fires
   (`src/Winhance.UI/App.xaml.cs:173`).
4. `StartupOrchestrator.RunStartupSequenceAsync` phase 1 calls
   `ICompatibleSettingsRegistry.InitializeAsync()`
   (`src/Winhance.UI/Features/Common/Services/StartupOrchestrator.cs:65`).
5. `CompatibleSettingsRegistry.PreFilterAllFeatureSettingsAsync` walks
   `GetKnownFeatureProviders()` — a `Dictionary<string, Func<IEnumerable<SettingDefinition>>>`
   that is the **single aggregation point for all domain catalogs**
   (`src/Winhance.Infrastructure/Features/Common/Services/CompatibleSettingsRegistry.cs:231`):
   ```csharp
   [FeatureIds.ExplorerCustomization] = () => ExplorerCustomizations.GetExplorerCustomizations().Settings,
   [FeatureIds.Power]                 = () => PowerOptimizations.GetPowerOptimizations().Settings,
   [FeatureIds.Sound]                 = () => SoundOptimizations.GetSoundOptimizations().Settings,
   ```
   Each provider's output passes `IWindowsCompatibilityFilter`, and for `Power` also
   `IHardwareCompatibilityFilter` and `IPowerSettingsValidationService`. A per-feature exception
   degrades to an empty list rather than failing startup.
6. `IGlobalSettingsPreloader.PreloadAllSettingsAsync()` then reads live registry state into
   `GlobalSettingsRegistry`.
7. User clicks a nav item → `NavSidebar_ItemClicked` → `NavigationRouter.NavigateToPage` maps
   `"Optimize"` → `typeof(OptimizePage)` (`src/Winhance.UI/Helpers/NavigationRouter.cs:31`).
8. `OptimizePage.OnNavigatedTo` calls `ViewModel.InitializeAsync()`, which loops the injected
   `IEnumerable<IOptimizationFeatureViewModel>` and awaits each `LoadSettingsAsync()`
   (`src/Winhance.UI/Features/Common/ViewModels/SectionPageViewModel.cs:141`).
9. Each feature VM calls `ISettingsLoadingService`, which uses `ISettingPreparationPipeline` to
   combine `ICompatibleSettingsRegistry.GetFilteredSettings(featureModuleId)` with
   `ISettingLocalizationService.LocalizeSetting(s)`.
10. `ISettingViewModelFactory` builds one `SettingItemViewModel` per `SettingDefinition`;
    `ISettingViewModelEnricher` attaches cross-group info and review diffs.
11. A click on a settings card calls `OptimizePage.NavigateToSection("Sound")` →
    `InnerContentFrame.Navigate(typeof(SoundOptimizePage))`. The overview is not navigated away
    from; visibility is toggled (`UpdateContentVisibility`), so the hub ViewModel stays alive.

### Apply Path

1. User flips a toggle → `SettingItemViewModel` command.
2. `ISettingApplicationService.ApplySettingAsync(ApplySettingRequest)` walks the
   `SettingDefinition` payload collections in order: `RegistrySettings`,
   `ScheduledTaskSettings`, `PowerShellScripts`, `RegContents`, `PowerCfgSettings`,
   `NativePowerApiSettings`, then `RestartProcess` / `RestartService` / `RequiresRestart`.
3. `ISettingDependencyResolver` gates on `Dependencies` / `AutoEnableSettingIds`;
   `IProcessRestartManager` handles `RestartProcess`.
4. For settings a plain registry write cannot express, the service consults
   `ISpecialSettingHandlerRegistry` by setting id and delegates to the owning domain's handler
   (`PowerService`, `UpdateService`, `ThemeWallpaperApplier`).
5. On success, `IEventBus.Publish(new SettingAppliedEvent(...))` notifies every subscribed
   overview card. `OptimizePage` recomputes its badge pills from that event.

### Secondary Flow — config import / review

`IConfigurationService` → `ConfigImportOverlayService` (a `Window` in `Common/Views/`) →
`ConfigReviewService` diffs the imported config against live state, stores
`ConfigReviewDiff` entries keyed by setting id and feature id, and flips
`IApplicationModeService` to review mode. `NavBadgeService` then pushes per-page counts onto
`NavSidebar` buttons. `ConfigApplicationExecutionService` applies the accepted set through the
same `ISettingApplicationService` path, so import reuses the manual-apply engine unchanged.

### Secondary Flow — WIM / autounattend

`WimUtilPage` → `WimUtilViewModel` → 4 step ViewModels → `IWimImageService.ConvertImageAsync` /
`IIsoService` / `IOscdimgToolManager`. `AutounattendGeneratorPage` → `AutounattendGeneratorViewModel`
→ `IAutounattendXmlGeneratorService` (UI impl) → `AutounattendScriptBuilder` (Infrastructure) →
composes `ScriptSections` by pulling `SettingDefinition`s from `ICompatibleSettingsRegistry`
(Optimize + Customize) and app lists from `ISelectedAppsProvider` (SoftwareApps) →
`IPowerShellRunner` writes `autounattend.xml` from the embedded
`Common/Resources/AdvancedTools/autounattend-template.xml`.

**State Management:**
- No navigation framework, no ViewModel locator, no state container. `App.Services` is the only
  global; the `Frame` is the navigation stack.
- ViewModel state is preserved by making everything a singleton and toggling visibility
  instead of navigating the hub away.
- Cross-slice state changes flow through `IEventBus` (`SettingAppliedEvent`,
  `SettingsRefreshedEvent`, `FilterStateChangedEvent`, `TooltipUpdatedEvent`,
  `PowerPlanChangedEvent`, `BuilderModeExitedEvent`, `ReviewModeExitedEvent`) — never through
  direct ViewModel references between domains.
- Settings that must survive a restart (language, theme, badge-visibility toggles) go through
  `IUserPreferencesService` with keys from `UserPreferenceKeys`.

---

## Key Abstractions

**`BaseDefinition`** (`src/Winhance.Core/Features/Common/Models/BaseDefinition.cs`)
- Purpose: the minimum shape of anything the user can toggle. `Id`, `Name`, `Description`,
  `GroupName`, `Icon`/`IconPack`, `InputType`, OS gates (`IsWindows11Only`, `MinimumBuildNumber`,
  `MaximumBuildRevision`, …), `RegistrySettings`, `RestartProcess`, `RequiresRestart`, and typed
  metadata (`ComboBox`, `NumericRange`, `Recommendation`, `SettingPresets`,
  `CrossGroupChildSettings`, `DetectionType`).
- Pattern: `abstract record` with `required`/`init` members only — definitions are immutable data.
- Subtypes: `SettingDefinition` (Core/Common), `ItemDefinition` (SoftwareApps).

**`SettingDefinition : BaseDefinition, ISettingItem`**
- Purpose: one Windows tuning knob, fully described as data. The engine applies it generically;
  the UI renders it generically. This is what makes one code path serve 1,000+ settings.
- Notable fields: `Dependencies`, `AutoEnableSettingIds`, hardware gates (`RequiresBattery`,
  `RequiresDesktop`, `RequiresBrightnessSupport`), `IsSubjectivePreference`,
  `RecommendedToggleState`, `DefaultToggleState`, `LocalizationId` (lets a Win10 and a Win11
  variant share one text), `ResolveUnmatchedToDefault`.

**`ISettingItem`** — `Id`, `Name`, `Description`, `GroupName`, `InputType`, `Dependencies`.
The data-model contract, deliberately free of UI state.

**`ISettingsFeatureViewModel`** (`src/Winhance.UI/Features/Common/Interfaces/`)
- Purpose: "a feature that owns a collection of settings". Members: `ModuleId`, `DisplayName`,
  `Settings`, `GroupedSettings`, `HasVisibleSettings`, `IsExpanded`, `IsLoading`, `SettingsCount`,
  `GroupDescriptionText`, `LoadSettingsAsync`, `RefreshSettingsAsync`,
  `RefreshSettingStatesAsync`, `ApplySearchFilter`.
- Narrowed per domain by an empty marker: `IOptimizationFeatureViewModel`,
  `ICustomizationFeatureViewModel`.

**`SectionPageViewModel<TSectionInfo>`** (`src/Winhance.UI/Features/Common/ViewModels/`)
- Purpose: base for a hub page that owns N feature ViewModels organized into sections.
  Handles `InitializeAsync`, search + suggestions (capped at 5), breadcrumb state,
  localization re-broadcast, and disposal. Abstract members: `PageTitleKey`,
  `PageDescriptionKey`, `BreadcrumbRootFallback`, `LogPrefix`, `SectionDefinitions`.
  Extended by `OptimizeViewModel` and `CustomizeViewModel`. **Not** used by AdvancedTools.

**`ISectionInfo`** — `Key`, `IconGlyphKey`, `DisplayName`, `ModuleId`. Implemented by
`OptimizeSectionInfo` and `CustomizeSectionInfo`. `AdvancedToolsSectionInfo` deliberately opts
out and uses `IconResourceKey`.

**`ISpecialSettingHandler`** (`src/Winhance.Core/Features/Common/Interfaces/`)
- Purpose: the domain extension point for settings a registry write cannot express.
  `TryApplySpecialSettingAsync` (required) + `DiscoverSpecialSettingsAsync` (default returns empty).
  Only handlers that override `DiscoverSpecialSettingsAsync` go into
  `ISpecialDiscoveryRegistry`; all handlers go into the id-keyed `ISpecialSettingHandlerRegistry`.

**`IEventBus` / `IDomainEvent` / `ISubscriptionToken`**
- Purpose: decouple cross-slice notification. `Subscribe<T>` returns a disposable token, which is
  the disposal discipline pages use in `OnNavigatedTo`/`OnNavigatedFrom`.

---

## Entry Points

**`Program.Main`** — `src/Winhance.UI/Program.cs:20`
- Triggers: process launch. `StartupObject` is set to `Winhance.UI.Program` in the csproj with
  `DISABLE_XAML_GENERATED_MAIN` so this runs instead of the generated one.
- Responsibilities: `AppInstance.FindOrRegisterForKey` single-instance check (redirects and
  foregrounds the incumbent before any WinUI init), COM wrapper init, dispatcher
  `SynchronizationContext` install, `Application.Start`.

**`App.OnLaunched`** — `src/Winhance.UI/App.xaml.cs:103`
- Triggers: WinUI launch.
- Responsibilities: register exception handlers (`AppDomain`, WinUI, `TaskScheduler`);
  build the DI host; start `ILogService` file logging to `C:\ProgramData\Winhance\Logs`;
  apply saved language then saved theme; create and activate `MainWindow`; kick off startup
  operations. Every step is individually try/caught so a failure logs instead of crashing.

**`MainWindow`** — `src/Winhance.UI/MainWindow.xaml.cs`
- Triggers: created by `App.OnLaunched`.
- Responsibilities: `NavSidebar` + `ContentFrame` chrome, `NavigationRouter` construction,
  `DialogService`/`DispatcherService` late XamlRoot injection, `StartStartupOperations()`,
  mode switching (Normal / Builder / ConfigReview), title bar and theme icon refresh.

**`AddInfrastructureServices()`** — `src/Winhance.Infrastructure/Extensions/DI/InfrastructureServicesExtensions.cs:21`
- Triggers: called by `CompositionRoot`; also called directly by
  `InfrastructureContainerSmokeTests`.
- Responsibilities: the entire Infrastructure lifetime map.

---

## Architectural Constraints

- **Project references are one-directional and enforced by MSBuild.** `Core` has zero project
  references. `Infrastructure` references `Core` + `WindowsPackageManager.Interop`. `UI`
  references `Core` + `Infrastructure`. There is no `InternalsVisibleTo` from `Core` to `UI`.
- **`InternalsVisibleTo` is per test project**: `Core` → `Winhance.Core.Tests`,
  `Infrastructure` → `Winhance.Infrastructure.Tests`, `UI` → `Winhance.UI.Tests`. Tests can see
  `internal` members of exactly the project they test.
- **All three projects target `net10.0-windows10.0.19041.0` with `Nullable=enable` and
  `ImplicitUsings=enable`.** Even Core is Windows-targeted because it holds P/Invoke.
- **x64 only.** `<Platforms>x64</Platforms>`, `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`,
  `TargetPlatformMinVersion 10.0.17763.0` (Windows 10 1809).
- **Unpackaged, self-contained WinUI.** `WindowsPackageType=None`, `SelfContained=true`,
  `WindowsAppSDKSelfContained=true`, `EnableMsixTooling=false`, `GenerateAppxPackageOnBuild=false`,
  `AssemblyName=Winhance`. No MSIX, no store identity.
- **Threading:** single-threaded UI (WinUI dispatcher). `SynchronizationContext` is pinned to the
  UI `DispatcherQueue` in `Program.Main`. Every service that touches the UI goes through
  `IDispatcherService`; pages use `DispatcherQueue.TryEnqueue`. `SemaphoreSlim` guards the
  registry, `BaseSettingsFeatureViewModel` loading, and `InitializationService`.
- **Global state (module-level, minimal):** `App.Services` and `App.MainWindow` are the only
  statics of consequence. `StartupLogger.Log(...)` is a static file-logger used from
  `Program`, `App`, `MainWindow`, `NavigationRouter`, and every page constructor — deliberately
  available before DI exists. `BaseSettingsFeatureViewModel` keeps
  `volatile Dictionary<string, SettingItemViewModel>` indexes for id/parent lookups.
- **Circular imports: none at the project level.** At the source level there are none between
  domains except the documented base-class reuse and the composition root.
- **Single-instance is mandatory.** Enforced before WinUI init; a second process redirects and exits.
- **Localization is data, not resources.** 29 JSON files under
  `Winhance.UI/Features/Common/Localization/` are copied to output and read by
  `ILocalizationService`. Code-behind sets text on controls directly because WinUI
  `DataGrid` column headers and InfoBar content do not re-evaluate `ThemeResource` bindings.
  Every `SettingDefinition` gets its text from `Setting_{LocalizationId}_*` keys.
- **The composition root is the only place that knows all five domains exist.** Any new domain
  requires exactly two edits outside its own folder: `AddSettingServices` (or
  `AddUIServices`) and, if it has setting catalogs, one entry in
  `CompatibleSettingsRegistry.GetKnownFeatureProviders()`.

---

## Anti-Patterns

### Shared base classes parked in a domain folder

**What happens:** `SettingItemViewModel`, `SettingsGroup`, `BaseSettingsFeatureViewModel`, and
`TechnicalDetailsManager` all live in `src/Winhance.UI/Features/Optimize/ViewModels/`, yet
`Customize`'s four ViewModels derive from `BaseSettingsFeatureViewModel` and
`src/Winhance.UI/Features/Common/Interfaces/ISettingsFeatureViewModel.cs` imports
`Winhance.UI.Features.Optimize.ViewModels`.

**Why it's wrong:** `Common` now depends on `Optimize`, inverting the intended direction, and the
slice boundary is a lie — deleting Optimize would break Customize's type system.

**Do this instead:** when replicating, put the shared setting-ViewModel base in
`UI/Features/Common/ViewModels/` and have `ISettingsFeatureViewModel` reference only
`Common` types. Winhance's own `SectionPageViewModel<T>` in `Common/ViewModels/` shows the
correct placement for a type shared by two hubs.

### Core as a behaviour layer

**What happens:** `src/Winhance.Core/Features/Common/Services/` contains five concrete
implementations (`LogService`, `StartupLogger`, `InitializationService`, `GlobalSettingsRegistry`,
`DependencyManager`), and `Core/Features/SoftwareApps/Utilities/BloatRemovalScriptGenerator.cs`
is a 398-line implementation.

**Why it's wrong:** it blurs the "Core = contracts, Infrastructure = implementations" rule, so a
reader cannot infer from the folder whether a type has side effects.

**Do this instead:** accept it deliberately. Winhance's justification is that these five have
**no OS dependency** — they are pure in-process logic, so putting them in Core keeps
`Infrastructure` from importing anything to satisfy a trivial type. Apply the same test when
replicating: *does it touch the registry, disk, process, or network?* If not, Core is fine.
`BloatRemovalScriptGenerator` passes because it emits PowerShell text, it does not run it.

### Interface in Core, implementation in UI

**What happens:** `IAutounattendXmlGeneratorService` is declared at
`src/Winhance.Core/Features/AdvancedTools/Interfaces/` but implemented at
`src/Winhance.UI/Features/AdvancedTools/Services/AutounattendXmlGeneratorService.cs`, because it
needs `ISelectedAppsProvider` (a UI-layer `Common` service) and `AutounattendScriptBuilder`
(Infrastructure) simultaneously.

**Why it's wrong:** the contract's location implies the wrong owner, and the layer that owns the
feature owns none of its logic.

**Do this instead:** when replicating, either move the interface to
`Winhance.Core/Features/Common/Interfaces/` (it is genuinely a cross-slice capability), or lift
`ISelectedAppsProvider` into Core so the whole chain fits in Infrastructure. The first option is
smaller and is the one to prefer.

### Id-keyed dispatcher registries instead of a virtual method or a plugin

**What happens:** `SpecialSettingHandlerRegistry` and `SpecialDiscoveryRegistry` map
`SettingIds.X → ISpecialSettingHandler`, and `ISystemSettingsDiscoveryService` /
`ISettingApplicationService` look handlers up by string id at runtime.

**Why it's wrong:** a `switch` on setting id would be a compile error when a domain is added;
this is stringly-typed, resolved at runtime, and split across two registries with subtly
different membership rules.

**Do this instead:** replicate the registry, but keep the two registries' membership rules
explicit in one file. `SettingServicesExtensions.cs` states them: *id-keyed registry gets
`ThemeModeWindows` too; discovery registry only gets handlers that override
`DiscoverSpecialSettingsAsync`.* Also replicate the `TryAddSingleton`-empty-default trick so
`AddInfrastructureServices()` remains independently resolvable.

### Overwrite-by-later-registration

**What happens:** `AddInfrastructureServices()` registers `ISpecialDiscoveryRegistry` and
`ISpecialSettingHandlerRegistry` with empty defaults, then `AddSettingServices()` registers them
again with the real handlers. The *last* registration wins, and the correctness of the app
depends on `CompositionRoot` calling them in that order.

**Why it's wrong:** the Infrastructure container resolves correctly but is useless, and nothing
in the type system records the ordering requirement.

**Do this instead:** replicate the `TryAddSingleton` defaults *and* the explanatory comment. The
comment is what stops the next person from "cleaning up" the ordering. Add an integration test
(`InfrastructureContainerSmokeTests` is the existing one) that resolves every registration from
`AddInfrastructureServices()` alone.

### Code-behind doing orchestration

**What happens:** `OptimizePage.xaml.cs` is 1,118 lines and performs badge aggregation, bulk
actions, review-mode approval, search, technical-details toggles, and breadcrumb state. The
per-section `XxxOptimizePage.xaml.cs` files are 34 lines.

**Why it's wrong:** the hub page mixes view, orchestration, and bulk-action policy; the
ViewModel it already owns is bypassed.

**Do this instead:** this is the pattern's *worst* instance, not a model to copy. What to copy
is the thin sub-page: resolve the hub ViewModel from DI, apply the navigation parameter, and
refresh state. Put bulk-action and review-mode logic in the hub ViewModel.

### Huge single-file data catalogs

**What happens:** `GamingAndPerformanceOptimizations.cs` is 4,151 lines;
`ExplorerCustomizations.cs` is 3,338; `PrivacyOptimizations.cs` is 2,918.

**Why it's wrong:** untenable to review or navigate; the files have no internal structure beyond
a flat `List<SettingDefinition>`.

**Do this instead:** note that SoftwareApps solved the same problem with a different pattern —
`ExternalAppDefinitions` is split into 15 `partial class` files by category, all contributing to
one static class. **That is the pattern to replicate:** one static class per feature, split into
`partial` files by natural sub-group, with one `GetX()` entry point. Never grow a single
4,000-line catalog file.

### 900-line shell window

**What happens:** `MainWindow.xaml.cs` is ~900 lines covering chrome, startup kickoff, mode
switching, dialogs, and badge plumbing.

**Why it's wrong:** two extractions were needed to stay testable —
`NavigationRouter` and `StartupOrchestrator` — and the residue stays in the window.

**Do this instead:** replicate the two extractions at their current locations
(`UI/Helpers/NavigationRouter.cs`, `UI/Features/Common/Services/StartupOrchestrator.cs`) and
start Akari's window thinner. Note the odd placement that resulted: `StartupUiCoordinator.cs`
and `TaskProgressCoordinator.cs` ended up in `UI/Helpers/` while `StartupOrchestrator` sits in
`UI/Features/Common/Services/` — pick one home for coordinators in Akari.

---

## Error Handling

**Strategy:** log-and-continue at the edges, fail-fast at the seams, never crash on a
per-feature failure.

**Patterns:**

- **Three-layer unhandled-exception net**, registered in `App` constructor before any UI:
  `AppDomain.CurrentDomain.UnhandledException` (fatal, log only),
  `Application.UnhandledException` (log, then `e.Handled = true`),
  `TaskScheduler.UnobservedTaskException` (log, then `e.SetObserved()`).
- **`StartupLogger.Log(component, message)`** is a static, dependency-free file logger called
  from `Program`, `App`, `MainWindow`, `NavigationRouter`, and page constructors — the escape
  hatch for the window where DI does not exist yet. `App.OnLaunched` then promotes it to
  `ILogService` with `StartLog()` writing to `C:\ProgramData\Winhance\Logs`.
- **`ILogService` has a `LogLevel` enum** (`Debug/Info/Warning/Error`) and every call site passes
  a component prefix: `_logService.LogWarning($"Failed to create restore point from Settings: {ex.Message}")`.
- **Per-feature isolation in the registry:** a `try/catch` inside
  `PreFilterAllFeatureSettingsAsync` logs the error and registers an **empty** list for that
  feature, so one broken catalog cannot prevent the other nine from loading.
- **Per-feature isolation in ViewModel init:** `SectionPageViewModel.InitializeAsync` wraps each
  `LoadSettingsAsync()` in its own `try/catch` and logs, so a section that fails to load still
  lets its siblings render.
- **Constructor guards.** `LogService` and friends throw `ArgumentNullException` from the ctor;
  `CompatibleSettingsRegistry.GetById` throws `InvalidOperationException("Registry not initialized.
  Call InitializeAsync first.")` if used too early.
- **Typed domain exceptions** in Core/Common/Exceptions: `ExecutionPolicyException`,
  `InsufficientDiskSpaceException`.
- **`FireAndForget(logService)` extension** — fire-and-forget is allowed, but only through this
  helper, which routes the fault to the log. Never a bare `_ = SomeAsync();` on a logging path
  without it. (Pages do use bare `_ =` for deliberate "lightweight refresh" calls such as
  `RefreshSettingStatesAsync()`.)
- **Swallowed-by-design exceptions are commented.** `Program.ActivateExistingWindow` catches
  `Exception` with a bare comment because the process may already have exited.
- **XML-config validation is enforced at build-relevant time**, not runtime:
  `Localization/LocalizationKeyReferenceTests.cs` and `LocalizationJsonValidityTests.cs` assert
  every referenced key exists in `en.json`.

---

## Cross-Cutting Concerns

**Logging** — `ILogService` (Core interface, Core `LogService` implementation, singleton).
`StartupLogger` static for pre-DI. Every service and ViewModel takes `ILogService` in its
constructor and prefixes messages. No `Console`/`Debug.WriteLine` in feature code.

**Validation** — two distinct kinds, both in Core:
- `Core/Features/Common/Validation/SettingCatalogValidator.cs` validates the *catalogs* at
  startup (e.g. a selection setting may declare at most one `IsRecommended` and one `IsDefault`
  `ComboBoxOption`). Tested in both `Winhance.Core.Tests/Validation/` and
  `Winhance.UI.Tests/Services/`.
- `WindowsCompatibilityFilter` / `HardwareCompatibilityFilter` / `PowerSettingsValidationService`
  *filter* the catalog per machine at registry-initialization time.
- `IHardwareDetectionService` supplies per-machine capability signals to `SettingDefinition`
  gates (`RequiresBattery`, `RequiresDesktop`, `RequiresBrightnessSupport`,
  `RequiresHybridSleepCapable`).

**Authentication** — Not applicable. The app runs as the interactive user and (optionally)
elevates; there is no identity concept. `IInteractiveUserService` resolves the session user for
token operations (`UserTokenApi`) used by scheduled-task creation.

**Localization** — `ILocalizationService` (Core interface / Infrastructure impl) reads 29 JSON
files from the output directory, exposes `GetString(key)`, `GetAvailableLanguages()`,
`SetLanguage(code)`, and a `LanguageChanged` event. ViewModels expose
`public string PageTitle => _localizationService.GetString("Category_Optimize_Title");` so
`x:Bind` re-evaluates on `OnPropertyChanged`. Text **not** driven by the service (e.g. page
toolbar toggles, DataGrid headers) is set imperatively from code-behind because those controls
do not re-evaluate bindings.

**Events** — `IEventBus` is the only sanctioned cross-slice channel. Domains never hold
references to each other's ViewModels. `ISubscriptionToken` from `Subscribe<T>` is disposed in
`OnNavigatedFrom` — this is the pattern for every page that listens.

**Configuration** — `IConfigurationService` (+ Import/Export/Review/Overlay/Bridge services) all
in `UI/Features/Common/Services/`. Backed by `UnifiedConfigurationFile` in Core, with three
embedded baseline configs (`Winhance_Recommended_Config.winhance`,
`Winhance_Default_Config_Windows11_25H2.winhance`,
`Winhance_Default_Config_Windows10_22H2.winhance`) under `UI/Features/Common/Resources/Configs/`
and `IConfigMigrationService` for backward compatibility.

**Resource dictionaries** — 5 merged XAML dictionaries in
`UI/Features/Common/Resources/` (`BadgeStyles.xaml`, `Converters.xaml`, `FeatureIcons.xaml`,
`SettingTemplates.xaml`, `TechnicalDetailsStyles.xaml`). `SettingTemplates.xaml` has a
code-behind (`SettingTemplates.xaml.cs`) specifically to enable `x:Bind` inside
`DataTemplate`s — the Microsoft-supported WinUI Gallery pattern. Icon resource keys follow a
naming convention the code depends on: `*IconPath` = Material Design SVG path for `PathIcon`,
`*IconSymbol` = `FluentIcons.Common.Icon` enum name. `OptimizePage` and `AdvancedToolsPage` both
branch on `resourceKey.EndsWith("Symbol")`.

---

## Replicating This in Akari — Checklist

1. Create 3 projects with one-directional references: `Akari.Core` ← `Akari.Infrastructure` ← `Akari.UI`.
2. In each, create `Features/` with the domain folders at identical paths. Add a sixth folder
   per layer for the shared base.
3. Decide per domain whether it needs `Interfaces/` + `Models/` (Core), `Services/`
   (Infrastructure), and `ViewModels/` + `Pages/` (UI) — or fewer. The `Settings` slice proves
   one layer is legal.
4. Put every contract in `Common/Interfaces/`, every pure record in `Common/Models/`, every
   enums/constant in `Common/Enums/` + `Common/Constants/`.
5. Create exactly one composition root (`UI/Features/Common/Extensions/DI/`) with one method per
   layer, and one method per feature domain inside the settings method.
6. Create the catalog aggregation point — an explicit
   `Dictionary<string, Func<IEnumerable<SettingDefinition>>>` in
   `Infrastructure/Features/Common/Services/` — and add one line per new setting catalog.
7. Create the domain extension point (`ISpecialSettingHandler` + id-keyed registry in
   `Common/Interfaces/`) so new domains never require edits to Common.
8. Create the event bus (`IEventBus`/`IDomainEvent`/`ISubscriptionToken` in `Common/Events/`)
   before the first cross-domain interaction.
9. Create the section hub base (`SectionPageViewModel<TSectionInfo>` in
   `UI/Features/Common/ViewModels/`) plus the per-domain marker interfaces and `*SectionInfo`
   records, so each hub is ~50 lines.
10. Mirror the tests: one xunit project per source project, plus an integration project with a
    container smoke test and a localization-key reference test.

---

*Architecture analysis: 2026-10-05*
