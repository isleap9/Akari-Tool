<!-- refreshed: 2026-10-05 -->
# Winhance ↔ Akari Parity Gap Analysis

**Analysis date:** 2026-10-05
**System under rework:** Akari Tool — `C:/Users/isleap/Documents/GitHub/Akari-Tool-OLD`
**Reference target:** Winhance — `C:/Users/isleap/Documents/GitHub/Winhance` (read-only)
**Sources:** `.planning/codebase/{ARCHITECTURE,STRUCTURE,CONVENTIONS,CONCERNS}.md` + `.planning/reference/winhance/{ARCHITECTURE,STRUCTURE,STACK,FEATURES,CONVENTIONS}.md`, plus targeted source verification of both repos (grep/read only — **no build was run**).

> **Clean-room discipline.** Winhance is PolyForm Shield 1.0.0 with a noncompete clause. Parity
> here means Akari's own implementation matching Winhance's *architecture, conventions, feature
> set and UX patterns*. No file listed in this document should be copied; the mapping tables tell
> you where Akari's own code must **live** and what shape it must **take**. The already-adopted
> 1:1 model types (`SettingDefinition`, `RegistrySetting`, `BadgePillState`,
> `SettingDefinitionToggleState`, `PowerCfgSetting`, `ComboBoxOption`, …) are out of scope and
> stay as they are.

---

## 0. Headline findings — five things that change the plan

**H1 — The *data* is already at parity. The *structure and machinery* are not.**
This is the single most important finding and it validates the user's "restructure first"
choice. Measured `SettingDefinition.Id` counts, Akari vs Winhance:

| Slice | Akari | Winhance | Delta |
|---|---|---|---|
| Optimize — Gaming & Performance | 129 | ~115 | **+14** |
| Optimize — Privacy & Security | 89 | ~90 | −1 |
| Optimize — Power | 48 | ~50 | −2 |
| Optimize — Notifications | 16 | 15 | +1 |
| Optimize — Windows Update | 12 | 12 | 0 |
| Optimize — Sound | 7 | 7 | 0 |
| **Optimize total** | **301** | **~288** | **+13** |
| Customize — Explorer (+ Akari's separate Desktop 12) | 67 | ~87 (Desktop is a group inside Explorer) | −20 |
| Customize — Start Menu | 12 | 13 | −1 |
| Customize — Taskbar | 32 | 28 | +4 |
| Customize — Appearance (Winhance: Windows Theme) | 10 | 2 (both special-handled) | +8 |
| **Customize total** | **121** | **~130** | **−9** |
| SoftwareApps — Windows Apps | 56 | ~55 | +1 |
| SoftwareApps — External Apps (16 categories) | **191** | **~191** | **0 (per-category identical)** |
| SoftwareApps — Capabilities + Optional Features | ~17 | 17 | ~0 |

Akari's external-app category counts match Winhance's *exactly* (Browsers 21, Compression 4,
CustomizationUtilities 14, DevelopmentApps 11, DocumentViewers 12, FileDiskManagement 14,
Gaming 8, Imaging 13, MessagingEmailCalendar 10, Multimedia 24, OnlineStorageBackup 7,
OpticalDiscTools 4, OtherUtilities 11, PrivacySecurity 7, RemoteAccess 12,
RuntimesAndDependencies 21). Akari's Infrastructure `Features/Common/Services/` holds ~33
services + 2 registries, against Winhance's 46 in the same slot.

**Consequence:** do *not* size this project as "port ~400 missing settings". Size it as
"re-shape ~95 %-complete data into Winhance's structure, then build the missing machinery
(contracts, services, fallbacks, modes, localization, row-rendering primitives)". Catalog
content work is small and mostly mechanical; structural and machinery work is the bulk.

**H2 — Two of the four "known-Absent" items are not absent. They are mis-shaped.**
`AdvancedToolsPage.Wizard.cs` (22 KB) already implements Winhance's 4-step WIM/ISO wizard
(Step 1 Select ISO → Step 2 Add XML → Step 3 Add Drivers → Step 4 Create ISO) against
`WimUtilService` (851 lines: 31 oscdimg references, ESD⇄WIM conversion, 4 disk-space checks,
`Mount-DiskImage` with unmount-on-success/cancel/error, `pnputil`/`export` driver injection,
ISO extraction/creation). `AdvancedToolsPage.Generator.cs` already implements the autounattend
generator against `AutounattendService` (860 lines across 5 partials, including the
Schneegans-generator link). Winhance ships these as `WimUtilPage` + `AutounattendGeneratorPage`
with 6 Core contracts, 5 Infrastructure services, 5 `ScriptSections`, 3 helpers and 9
ViewModels.
**The gap is therefore structural (one 2,200-line code-behind page with two panels and no VM
slice), not functional.** Estimated effort drops from L to **M**. See §3.

**H3 — Akari already has an always-elevated manifest and already has Winhance's file-picker
approach.** `src/AkariTool.App/app.manifest` is `requestedExecutionLevel
level="requireAdministrator" uiAccess="false"` — identical to Winhance. `AkariFileService`
(335 lines) already drives COM `IFileOpenDialog`/`IFileSaveDialog` via `CoCreateInstance`,
with a documented rationale identical to Winhance's (`WinRT pickers throw COMException
0x80004005` under elevation). Both of these were flagged in the brief as divergences; they are
**not**. The real elevation divergence is the *opposite* one: Akari has a capability Winhance
lacks (in-process SYSTEM/TrustedInstaller impersonation) plus a `build-deelevated.ps1`
`asInvoker` test build. See §4.1–4.2.

**H4 — Akari already has the catalog aggregation point.** `CompatibleSettingsRegistry.
GetKnownFeatureProviders()` exists in `src/AkariTool.Infrastructure/Features/Common/Services/
CompatibleSettingsRegistry.cs:151+`, with the same explicit-dictionary/no-reflection shape, the
same per-feature try/catch-to-empty degradation, and the same Power-specific hardware +
existence filtering. **The restructure must not move or rewrite this file** — it is the one
structural anchor already correct. See §2.4.

**H5 — Two orphans are worse than the brief stated, and one is a whole mode system.** Verified
directly: `TweakRegistry.Register` has **zero** call sites across all of `src/` and `tests/`
(independently re-verified). Additionally, `BuilderModeExitedEvent` and
`ReviewModeExitedEvent` exist as event contracts in `Core/Features/Common/Events/` with **no
producer, no `IApplicationModeService`, no `BuilderEdit`, no `ConfigReviewService`** anywhere in
the codebase — verified by grep. These are the residue of a modes port that shipped its
contracts and never shipped its engine. See §5.

---

## 1. Verdict table

Severity = gap size, not risk. **None** = parity. **Minor** = cosmetic/small. **Major** = real
work. **Absent** = capability does not exist in Akari in any form.

| Domain | Akari today | Winhance | Gap |
|---|---|---|---|
| **Optimize** | 6 detail pages + hub. **301 settings** (ahead of Winhance by 13). Infrastructure slice = **1 file** (`WindowsUpdatePolicyHandler`); `PowerService`/`PowerPlanComboBoxService`/registry-adjacent services live in `Common`. Hubs build cards in code-behind; no `SectionPageViewModel<T>`, no `ISectionInfo`, no `IOptimizationFeatureViewModel` marker. | 6 detail pages + hub, ~288 settings. Infrastructure slice = 2 files (`PowerService`, `UpdateService`). Hub base `SectionPageViewModel<TSectionInfo>` in `Common`, per-domain marker interface, `BaseSettingsFeatureViewModel` + `SettingItemViewModel`. | **Minor** (catalog) / **Major** (UI-layer hub + marker infrastructure) |
| **Customize** | 5 detail pages + hub — one more than Winhance, because `Appearance` and `Desktop` are standalone sections. **121 settings** (−9). **Zero** Infrastructure files in the slice. No `IWallpaperService`, no wallpaper applier. No `WindowsTheme` special handler. | 4 detail pages + hub, ~130 settings. 2 Infrastructure files (`WallpaperService`, `ThemeWallpaperApplier`), 1 Core contract (`IWallpaperService`). `theme-mode-windows` writes `AppsUseLightTheme`+`SystemUsesLightTheme`, requires confirmation, restarts Explorer, and can swap the desktop wallpaper to the OS default. | **Major** |
| **AdvancedTools** | Capability **present but monolithic**: `AdvancedToolsPage` (4 partials, ~2,200 lines) with a `WizardPanel` and a `GeneratorPanel`; logic lives in code-behind calling static `WimUtilService` (851) and `AutounattendService` (860). **0 Core contracts, 0 interfaces, 1 page, 0 ViewModels.** No `ScriptSections` strategy set, no embedded `autounattend-template.xml`. | `AdvancedToolsPage` hub + **`WimUtilPage`** + **`AutounattendGeneratorPage`**. 6 Core contracts, 5 Infrastructure services, 3 helpers, 5 `ScriptSections`, 9 ViewModels, 1 embedded XML template. | **Major (structural)** / **Minor (functional)** |
| **SoftwareApps** | **254+ item definitions at parity**, in 24 catalogs split as 16 `ExternalAppCatalog.<Category>` partials (the correct pattern). **4 Core WinGet contracts** (`IWingetDetectionService`, `IWingetInstalledDetectionService`, `IWingetBootstrapper`, `IWingetPackageInstaller`) + 3 models. **4 Infrastructure WinGet services** + `vendor/WinGet.Interop`. All app logic in **one 646-line App-layer `SoftwareAppService`**. 4 pages (SoftwareApps hub, WindowsApps, ExternalApps, Debloat). No view/sort modes, no help-content views, no `SelectedAppsProvider`. | 266 items, **20 Core contracts**, **28 Infrastructure services** (incl. `Services/WinGet/Utilities/` = CLI runner, ConPTY, progress parser, exit codes, + vendored `winget-cli/`), 8 ViewModels, 3 view modes (Card/Table/DataGrid/Compact), 3 sort modes, 2 help-content views, `SelectedAppsProvider` bridging to AdvancedTools. Chocolatey bootstrap + ghost-package recovery. Store-download fallback via `store.rg-adguard.net`. Direct-download fallback. Scheduled-task-deferred bloat removal. | **Major** |
| **Settings** | `SettingsPage` (10.6 KB code-behind) + `SettingsViewModel` (thin, uses the vendored framework's `IThemeService`). Theme + restore point + backup page. Registered singleton. | `SettingsPage` (31-line code-behind) + `SettingsViewModel` (299 lines) over 8 Common services, `AddTransient`, `IDisposable`. Language + Theme + Import/Export + Create Restore Point. | **Minor** structurally |
| **Common** | Infra `Features/Common/Services/` ≈33 services + 2 registries (≈ parity with Winhance's 46). Core `Features/Common/Interfaces/` = **32** contracts (Winhance: 90). App `Services/` = 20 flat files. **No composition root** (one 134-line `DI/UIServiceExtensions.cs`). **`ServiceLocator` as a second DI channel — 48 call sites.** No localization. `vendor/WinUI.Framework` not in the solution. No `AkariTool.App.Tests`, no integration test project. | 90 Core contracts, 58 Infra, 98 UI. `CompositionRoot.cs` + `SettingServicesExtensions.cs` + `UIServicesExtensions.cs` under `UI/Features/Common/Extensions/DI/`. 29-locale JSON localization. `Win32FileDialogHelper` + `IFilePickerService`. 193 test files incl. a container smoke test. | **Major** |

### Cross-cutting verdicts

| Capability | Akari | Winhance | Gap |
|---|---|---|---|
| **Localization** | **Zero.** No `ILocalizationService`, no locale JSON, no `CultureInfo`-based UI language, no `LanguageChanged`. Every user-facing string is an inline literal. | 29 locales, live switch, per-setting auto-derived keys, `LocalizationId` sharing, RTL flow direction, 2 guardrail tests. | **Absent** |
| **Modes: Builder / Config Review** | Event contracts only (`BuilderModeExitedEvent`, `ReviewModeExitedEvent`) — no engine. | 3-mode system, single-source-of-truth `ConfigReviewService` implementing 6 interfaces, builder edit recording, gated apply, nav badges, `Show Only Changes`. | **Absent** |
| **Chocolatey** | **No service.** One `ToolService.RunPackageCommand(command, appName)` shell-out that runs an arbitrary PowerShell package-manager command and creates a desktop shortcut; one catalog entry mentions Chocolatey as a *description*. No detection, no bootstrap, no ghost-package recovery, no contract. | `IChocolateyService`: `IsChocolateyInstalledAsync` → bootstrap from `community.chocolatey.org/install.ps1` → `choco install -y --no-progress --ignore-checksums` → `CleanupStalePackageRecordAsync` ghost recovery. Part of the install fallback chain. | **Absent** (Akari's position: WinGet-only install path) |
| **WinGet COM interop** | `vendor/WinGet.Interop` — hand-written `WindowsPackageManagerFactory` / `…StandardFactory` / `…ElevatedFactory` / `ClsidContext` / `ClassModel` / `ClassesDefinition` + CsWinRT projection from `Microsoft.Management.Deployment.winmd`. Structurally equivalent to Winhance's `src/WindowsPackageManager.Interop/`. | Same shape, under `src/`, its own `.sln`. | **None** (placement differs) |
| **WinGet Utilities layer** | **Absent.** No `WinGetCliRunner`, `ConPtyProcess`, `WinGetProgressParser`, `WinGetExitCodes`, no vendored `winget-cli/`. | 5 utility classes + vendored `winget.exe` payload. | **Absent** |
| **Setting-card row rendering** | `Views/Templates/TweakTemplates.xaml` (80 KB) + 5 controls (`HubView`, `NavButton`, `NavSidebar`, `PowerPlanComboBox`, `TaskProgressControl`). **No `CommunityToolkit.WinUI` at all** — no `SettingsCard`, no `WrapPanel`, no `DataGrid`. | 11 controls incl. `SettingsCardItem`, `SettingsListView`, `SettingDescriptionWithBadges`, `QuietInfoBar`, `UniformWrapPanel`, `ComboBoxEx`; 5 resource dictionaries; `DataGrid 7.1.2` for table view. | **Major** |
| **Drift / Verify** | **Akari-only extension.** `DriftScanner`/`DriftBaseline` exist but are wired to an empty registry → permanently dead (see §5). Winhance has no equivalent. | n/a | Akari extension — must survive **and** be fixed |
| **Elevation** | `requireAdministrator` + `ElevationService` in-process SYSTEM/TrustedInstaller impersonation + `build-deelevated.ps1` `asInvoker` test copy. | `requireAdministrator` + OTS `IInteractiveUserService` + `UserTokenApi`. **No SYSTEM impersonation, no de-elevated build.** | **Minor** (both sides have extensions the other lacks) |
| **Tests** | 20 files / 2 projects. No App test project, no integration project. Catalog validator tested against hand-built fixtures, never against the real catalogs. | 193 files / 4 projects, incl. DI container smoke test + localization key/JSON integrity tests. | **Major** |

---

## 2. Structural gap — moving to Winhance's vertical slices

### 2.1 Akari's three-layer problem, stated precisely

| Layer | Winhance rule | Akari reality |
|---|---|---|
| `Core/Features/` | 5 domains + `Common`; every domain has `Interfaces/` + `Models/` | **11 domains**, of which **8 contain only a `Catalogs/` folder** (`Customize`, `Gaming`, `Notifications`, `Power`, `Privacy`, `Software`, `Sound`, `Update`) — data with no contracts and no services. Plus `AkariOS/Models` (3 files) and `Apps/{Interfaces,Models}` (2 files). Plus **non-`Features/` legacy roots**: `Competitive/`, `Interfaces/`, `Models/`, `Tweaks/`. |
| `Infrastructure/Features/` | 5 domains + `Common`; every domain has `Services/` | **3** (`Common` 47, `Apps` 4, `Optimize` 1) **plus a 40-file flat `Infrastructure/Services/` outside `Features/`** holding two unrelated namespaces. |
| `App/Features/` | 5 domains + `Common`; `Pages/` + `ViewModels/` | **3** (`Common` 5, `Shared` 1, `Software` 2) **plus 8 legacy top-level dirs** — `Views/` (28 pages, 36 `.xaml`), `ViewModels/` (13 flat + 6 subdirs), `Services/` (20 flat), `Defender/`, `Nvidia/`, `Scripts/`, `DI/`, `Resource/` — all outside any `Features/` boundary. |
| Composition root | `UI/Features/Common/Extensions/DI/` — 3 files, one method per layer, one method per domain | `App/DI/UIServiceExtensions.cs` (134 lines, singular "Service") + `Infrastructure/DI/InfrastructureServiceExtensions.cs`. No `CompositionRoot`, no `SettingServicesExtensions`, no per-domain methods. |

### 2.2 Domain consolidation: 11 → 5 (+1 decision)

Winhance's five domains absorb Akari's eleven. The mapping is mechanical for eight of them:

**Core**

| Akari | → | Winhance | Action |
|---|---|---|---|
| `Core/Features/Gaming/Catalogs/` | → | `Core/Features/Optimize/Models/GamingAndPerformanceOptimizations.cs` | move + rename class |
| `Core/Features/Privacy/Catalogs/` | → | `…/Optimize/Models/PrivacyOptimizations.cs` | move + rename |
| `Core/Features/Power/Catalogs/PowerOptimizations.cs` | → | `…/Optimize/Models/PowerOptimizations.cs` | move + rename |
| `Core/Features/Power/Catalogs/PowerTemplates.cs` | → | `…/Optimize/Models/PowerTemplates.cs` | move (already parity-named) |
| `Core/Features/Update/Catalogs/UpdateOptimizations.cs` | → | `…/Optimize/Models/UpdateOptimizations.cs` | move + rename |
| `Core/Features/Update/Catalogs/UpdateTweaks.cs` | → | — | **DELETE** (dead, §5) |
| `Core/Features/Notifications/Catalogs/` | → | `…/Optimize/Models/NotificationOptimizations.cs` | move + rename |
| `Core/Features/Sound/Catalogs/` | → | `…/Optimize/Models/SoundOptimizations.cs` | move + rename |
| `Core/Features/Customize/Catalogs/ExplorerOptimizations.cs` | → | `…/Customize/Models/ExplorerCustomizations.cs` | move + rename |
| `Core/Features/Customize/Catalogs/TaskbarOptimizations.cs` | → | `…/Customize/Models/TaskbarCustomizations.cs` | move + rename |
| `Core/Features/Customize/Catalogs/StartMenuOptimizations.cs` | → | `…/Customize/Models/StartMenuCustomizations.cs` | move + rename |
| `Core/Features/Customize/Catalogs/AppearanceOptimizations.cs` | → | `…/Customize/Models/WindowsThemeCustomizations.cs` | move + merge down to the 2 parity settings; surplus becomes a Theme group or is dropped |
| `Core/Features/Customize/Catalogs/DesktopOptimizations.cs` | → | `…/Customize/Models/ExplorerCustomizations.cs` | **merge as the `Desktop` group inside the Explorer page** — Winhance has no Desktop section. *Decision D3.* |
| `Core/Features/Software/Catalogs/WindowsAppCatalog.cs` | → | `…/SoftwareApps/Models/WindowsAppDefinitions.cs` | move + rename |
| `Core/Features/Software/Catalogs/ExternalAppCatalog*.cs` (17) | → | `…/SoftwareApps/Models/ExternalAppDefinitions*.cs` (16 + root) | move + rename — **the split-by-category shape is already correct** |
| `Core/Features/Software/Catalogs/{CapabilityCatalog,OptionalFeatureCatalog}.cs` | → | `…/SoftwareApps/Models/{Capability,OptionalFeature}Definitions.cs` | move + rename |
| `Core/Features/Software/Catalogs/{BloatRemovalScriptGenerator,EdgeRemovalScript,OneDriveRemovalScript}.cs` | → | `…/SoftwareApps/{Models,Utilities}/` (Winhance keeps the generator in Core `Utilities/`, the two removal scripts in `Models/`) | move + split by role |
| `Core/Features/Apps/{Interfaces,Models}` (4 contracts + 3 models) | → | `…/SoftwareApps/Interfaces/` + `…/SoftwareApps/Models/` | **fuse `Apps` into `SoftwareApps`** — this is the `Apps` → `SoftwareApps` rename that removes one Core domain |
| `Core/Features/AkariOS/Models/` (3) | → | **DECISION D1** | see §2.5 |
| `Core/Competitive/` (2) | → | **DECISION D1** | file I/O in Core is a layer violation regardless of destination |
| `Core/Interfaces/` (5) | → | `Core/Features/Common/Interfaces/` | move — all 5 are cross-domain |
| `Core/Models/**` | → | `Core/Features/Common/Models/` + `…/AdvancedTools/Models/` | move + split (SystemInfo, ShaderCache\*, Update\ReleaseInfo, `AkariProfile`, Actions\RunActions) |
| `Core/Tweaks/` | → | — | **DELETE** (§5) |

**Result:** `Core/Features/` = `Optimize/`, `Customize/`, `AdvancedTools/`, `SoftwareApps/`, `Common/` (+`AkariOS/` pending D1). Core drops from 11 declared domains + 4 legacy roots to 5 (+1).

**Infrastructure**

| Akari | → | Winhance | Action |
|---|---|---|---|
| `Infra/Features/Common/**` (47 files, ~33 services + `Events/EventBus` + `Utilities/`) | → | `Infra/Features/Common/{Services,Events,Utilities}` | **already at parity — leave alone** |
| `Infra/Features/Apps/Services/` (4 WinGet files) | → | `Infra/Features/SoftwareApps/Services/WinGet/` | move |
| `Infra/Features/Optimize/Services/WindowsUpdatePolicyHandler.cs` | → | `Infra/Features/Optimize/Services/UpdateService.cs` (Winhance's slot) | move + rename |
| `Infra/Features/Common/Services/PowerService.cs` | → | `Infra/Features/Optimize/Services/PowerService.cs` | move (Winhance puts it in the Optimize slice, not Common) |
| `Infra/Features/Common/Services/PowerPlanComboBoxService.cs`, `PowerSchemeOperations`, `PowerSettingsQueryService` | → | stay `Common` (Winhance has no equivalents; they are power-domain but shared with the Power page) | keep, document |
| `Infra/Features/Common/Services/DispatcherService.cs` | → | — | **DELETE** (§5) |
| `Infra/Services/SystemInfoService.cs`, `UpdateService.cs`, `ToolFetchService.cs`, `ShaderCacheService.cs` | → | `Infra/Features/Common/Services/` | move (+4 `*Wrapper.cs` **DELETE**; register the real types) |
| `Infra/Services/WimUtilService.cs` (851) | → | `Infra/Features/AdvancedTools/Services/{WimImageService,WimCustomizationService,OscdimgToolManager,IsoService}` | **split** into 4 services + extract `IDismProcessRunner` disk-space check |
| `Infra/Services/{ToolService,ExplorerRestart,RestorePointHelper,DriftScanner,DriftBaseline}.cs` | → | `Infra/Features/Common/Services/` | move; `ExplorerRestart` folds into the existing `ProcessRestartManager` (**DELETE**); `RestorePointHelper` folds into `SystemRestoreService` (**DELETE**) |
| `Infra/Services/PlaybookTweaks.*` (6), `ServicesPreset.*` (4), `SystemStateReader.*` (7), `PostInstallService`, `BcdBackup`, `ElevationService`, `DefenderService`, `CompetitiveService`, `CompetitivePrefs`, `GameDetection`, `GpuTweaks`, `NvidiaProfileService`, `ProcessSuspender`, `ProcessTuning` (35 files) | → | **DECISION D1** (`AkariOS/`) | move |
| `Infra/Services/AccountService.cs`, `SteamLibrary.cs` | → | `Infra/Features/{AdvancedTools,SoftwareApps}/Services/` | move |
| `Infra/Services/TweakRegistry.cs`, `TweakHelpers.*` | → | — | **DELETE** (§5) |
| `Infra/DI/InfrastructureServiceExtensions.cs` | → | `Infra/Extensions/DI/InfrastructureServicesExtensions.cs` | move + rename to `AddInfrastructureServices()` + remove 3 duplicate registration pairs |

**Result:** `Infrastructure/Features/` = `Optimize/` (2), `Customize/` (2, to be created), `AdvancedTools/` (13, to be created by splitting `WimUtilService` + adding the autounattend builder), `SoftwareApps/` (28, to be created), `Common/` (parity), (+`AkariOS/` pending D1). The flat `Infrastructure/Services/` directory **disappears**.

**App**

| Akari | → | Winhance | Action |
|---|---|---|---|
| `Views/OptimizeHubPage.xaml` | → | `Features/Optimize/OptimizePage.xaml` | move + rename |
| `Views/{Gaming,Privacy,Power,Update,Notifications,Sound}Page.xaml` | → | `Features/Optimize/Pages/*OptimizePage.xaml` | move + rename (`GamingPage` → `GamingOptimizePage`, etc.) |
| `Views/CustomizePage.xaml` | → | `Features/Customize/CustomizePage.xaml` | move |
| `Views/{Taskbar,Explorer,StartMenu,Appearance}Page.xaml` | → | `Features/Customize/Pages/*CustomizePage.xaml` | move + rename; `AppearancePage` → `WindowsThemeCustomizePage` |
| `Views/DesktopPage.xaml` | → | — | **merge into the Explorer page** (D3) |
| `Views/AdvancedHubPage.xaml` + `Views/AdvancedToolsPage.xaml(+Wizard,+Generator)` | → | `Features/AdvancedTools/AdvancedToolsPage.xaml` (hub, 2 cards) + `Features/AdvancedTools/Pages/WimUtilPage.xaml` + `Pages/AutounattendGeneratorPage.xaml` | **split the monolith into 3 pages + 9 ViewModels** (§3) |
| `Views/SoftwareAppsPage.xaml`, `WindowsAppsPage.xaml`, `ExternalAppsPage.xaml`, `DebloatPage.xaml` | → | `Features/SoftwareApps/SoftwareAppsPage.xaml` (1 page, 2 tabs, 3 view modes) + `Views/{WindowsApps,ExternalApps}HelpContent.xaml` | consolidate; *Decision D4* on whether Debloat survives as a tab or is absorbed into the removal flow |
| `Views/SettingsPage.xaml` | → | `Features/Settings/SettingsPage.xaml` | move — **exact Winhance match already** (UI-only slice) |
| `Views/VerifyPage.xaml`, `BackupPage.xaml`, `HomePage.xaml`, `ToolsPage.xaml`, `PlaceholderPage.xaml` | → | Akari-only. No Winhance counterpart (Winhance has no Home, no Backup page, no Verify, no Tools) | place per D1/D5 |
| `Views/Controls/` (5) | → | `Features/Common/Controls/` | move; **7 controls to create** (see §3.5) |
| `Views/Templates/` (3 `.xaml`) | → | `Features/Common/Resources/` | move + add `BadgeStyles.xaml`, `Converters.xaml`, `FeatureIcons.xaml` |
| `Views/Selectors/` (2) | → | `Features/Common/TemplateSelectors/` | move + rename (`SettingTemplateSelector`, `SettingItemTemplateSelector`) |
| `Views/Converters/` (1) | → | `Features/Common/Converters/` | move — **fixes the misplaced-converter defect** (this converter is the one `TweakTemplates.xaml` consumes) |
| `ViewModels/{11 page VMs}.cs` (flat) | → | `Features/{Optimize,Customize}/ViewModels/*OptimizationsViewModel.cs` | move + rename to the `…ViewModel` + marker-interface shape |
| `ViewModels/Tweaks/` (9 files: `SettingPageViewModel`, `SettingItemViewModel`, `TechnicalDetailsManager`, `SettingBadgeCalculator`, `SettingPowerPlanController`, `SettingStatusBannerManager`, `SettingSectionViewModel`, …) | → | **`Features/Common/ViewModels/`** — NOT Winhance's `Features/Optimize/ViewModels/` | move. See §4.3: Winhance's placement is a documented anti-pattern; Akari must not recreate it |
| `ViewModels/{Software,Backup,Verify,AdvancedTools,AkariOS,Common,Gaming}/` | → | `Features/<Domain>/ViewModels/` | move |
| `ViewModels/{Home,Settings,Placeholder}ViewModel.cs` | → | `Features/Settings/ViewModels/SettingsViewModel.cs`; Home → D5 | move |
| `App/Services/AutounattendService.*` (5 partials, 860) | → | `Features/AdvancedTools/Services/AutounattendScriptBuilder.cs` + `ScriptSections/*.cs` | move + split into the 5-section strategy set |
| `App/Services/SettingBackupService.cs` (658) | → | `Features/Common/Services/` (Akari's own; Winhance's equivalents are `ConfigExport/ConfigLoad/ConfigReview/ConfigImportOverlay` + `UnifiedConfigurationFile`) | move; *schema fork decision D6* |
| `App/Services/AkariFileService.cs` (335) | → | `Features/Common/Helpers/Win32FileDialogHelper.cs` + `Features/Common/Services/FilePickerService.cs` (`IFilePickerService`, resolved via `IMainWindowProvider`) | move + split. Behaviour already correct |
| `App/Services/{NavBadgeService,NewBadgeService,SettingPageWarmUp,StartupOrchestrator,TaskProgressService,TweakDialogs,AkariUiLogService,SystemUtilities,StartupNotificationService}` | → | `Features/Common/{Services,Dialogs,Utilities}` | move + rename (`TweakDialogs` → `DialogService` + `Dialogs/*Builder`) |
| `App/Services/{DefenderService,DefenderPhase2Scheduler}` | → | `Features/AkariOS/Services/` | move |
| `App/Features/Common/Services/DispatcherService.cs` | → | `Features/Common/Services/DispatcherService.cs` | move (keep this one) |
| `App/Features/Shared/UiPreferences.cs` | → | `Features/Common/Services/UserPreferencesService.cs` | move + rename. **Note: Akari's store is registry-backed section-collapse; Winhance's is JSON under `%LOCALAPPDATA%` holding language/theme/display toggles. Two different stores — D7.** |
| `App/Features/Software/SoftwareAppService.cs` (646) + `AppIconService.cs` | → | `Features/SoftwareApps/Services/` split into `WindowsAppsService`, `ExternalAppsService`, `AppStatusDiscoveryService`, `AppIconResolver`, … | move + **split (the single largest decomposition in the App layer)** |
| `App/Features/Common/{Models,Utilities,Converters}` | → | `Features/Common/{Models,Utilities,Converters}` | move (no change) |
| `App/Scripts/` (47 `.ps1` + 2 `.bat`) | → | split: autounattend → `Features/AdvancedTools/Resources/Scripts/`; bloat removal → `Features/SoftwareApps/Resources/Scripts/`; remainder → `Features/Common/Resources/Scripts/` | move. **Winhance *generates* scripts to `%ProgramData%\…`; Akari *embeds* 47.** Different, not inferior — D8 |
| `App/Defender/`, `App/Nvidia/` | → | `Features/AkariOS/Resources/` | move (embedded payloads; keep the `Condition="Exists(...)"` guard or make it hard-fail) |
| `App/DI/UIServiceExtensions.cs` | → | `Features/Common/Extensions/DI/{UIServicesExtensions,SettingServicesExtensions,CompositionRoot}.cs` | **split into 3** (see §2.4) |
| `App/Resource/` + `App/Assets/` | → | `Assets/{AppIcons,ModeIcons,Sponsors}` | collapse the duplication; declare `NavIcons/` in the csproj or delete it |
| `MainWindow.xaml.cs` (557) | → | `Helpers/NavigationRouter.cs` (tag→page map) + `Helpers/{TitleBarManager,StartupUiCoordinator,TaskProgressCoordinator}.cs` + `ViewModels/MainWindowViewModel.cs` | **extract** — the `PageMap` must leave `MainWindow` |
| **no `App/Helpers/`** | → | `Helpers/` (5 files) | **CREATE** |
| **no shell `App/ViewModels/`** | → | `ViewModels/MainWindowViewModel` + 4 children | **CREATE** |

**Result:** `App/Features/` = `Optimize/`, `Customize/`, `AdvancedTools/`, `SoftwareApps/`, `Settings/`, `Common/` (+`AkariOS/`). All 8 legacy top-level dirs disappear. `MainWindow.xaml.cs` drops from 557 to a thin shell.

### 2.3 The namespace/folder disagreement — cost assessment

**Measured disagreement:**

| Assembly | Namespace | Files | Folder matches? |
|---|---|---|---|
| Core | `AkariTool.Core.Features.*` | ~110 | yes |
| Core | `AkariTool.Tabs{,.Customize,.Gaming,.Notifications,.Power,.Privacy,.Sound,.Update}` | **37** (all catalog files) | **no** |
| Core | `AkariTool.Tabs` | 1 (`AkariPaths.cs`) | no |
| Infrastructure | `AkariTool.Infrastructure.Features.*` | 52 | yes |
| Infrastructure | `AkariTool.Tabs` | **18** (`Services/` legacy flat) | no |
| Infrastructure | `AkariTool.Services` | **19** (same flat dir) | no |
| App | `AkariTool.Views/.ViewModels/.Services` | 69 | yes |
| App | `AkariTool.Tabs` | **2** (`Features/Software/`, `Features/Shared/`) | no |

`AkariTool.Tabs` is declared in **three** assemblies. Grepping `AkariTool.Core.Features.Gaming`
finds **nothing**; grepping `AkariTool.Tabs.Gaming` finds the 3,427-line catalog. Eight of
eleven Core domains have **no namespace that matches their folder** — for a catalog in
`Core/Features/Gaming/Catalogs/`, the namespace you'd expect from the path does not exist.
`AkariPaths` is declared **twice** in that same namespace, in Core and in App, with
byte-identical members — a `CS0433` ambiguity for any file with `using AkariTool.Tabs;` that
references both assemblies (both copies are live: `BloatRemovalScriptGenerator` uses the Core
one, `SoftwareAppService` uses all four members of the App one).

**Cost assessment — this is the largest *file-count* item in the restructure and the smallest
*risk* item.** Concretely:

- **57 files** need a namespace rename (37 Core + 18 Infra + 2 App), plus ~19 more
  (`AkariTool.Services` in Infra) if you normalize that scheme too. Call it **~76 files, ~40 % of
  the codebase's first-party `.cs` count.**
- It is **mechanically verifiable**: rename, build, repeat. There is no behavioural logic in a
  namespace declaration, and the compiler catches every missed reference *at the use site*.
- **The real hazard is partial completion, not the rename itself.** Because `AkariTool.Tabs`
  is one namespace spread across three assemblies, a half-done rename leaves a *mixed* state
  where folder reasoning and namespace reasoning still disagree — and the compiler will **not**
  complain about types nobody references. That makes a half-rename actively worse than no
  rename: it destroys the ability to grep for "everything in the legacy scheme" and thereby
  hides leftovers.
- **Mitigation: one assembly per commit, in dependency order (Core → Infrastructure → App).**
  Each commit is independently verifiable because Core compiles alone. Akari's own CONCERNS
  item 12 already prescribes exactly this ordering — adopt it.
- **Do it *before* the moves, not with them.** If you move `Core/Features/Gaming/Catalogs/` and
  rename its namespace in the same commit, a failure in a 3,427-line file is ambiguous between
  "the move broke it" and "the rename broke it". Separate them and each failure has one cause.
- **Do the `AkariPaths` duplicate first, as a standalone 2-line deletion**, before any rename —
  otherwise you have two `AkariPaths` in the namespace you are actively rewriting.
- **The `AkariOSPage`/`AdvancedToolsPage` XAML moves are higher-risk than the namespace
  renames.** 37 `.xaml` files carry `x:Class` in a code-behind namespace; moving a page means
  moving `.xaml` + `.xaml.cs` atomically, and `MainWindow.xaml.cs`'s `PageMap` uses `typeof()`
  per page plus 4 `*DetailTags` sets, so a hub move invalidates its entire detail set. Treat
  the XAML moves as the critical path, and the namespace renames as the cheap warm-up.

**Verdict:** the namespace disagreement raises the *cost* of the restructure by roughly one
extra pass over ~76 files (~1–2 working days of edit-and-build) and raises the *risk* by
almost nothing, provided it is done assembly-by-assembly before any file moves. It is **not**
a reason to reorder the plan.

### 2.4 Composition root and DI registration

**Target layout (Winhance parity, Akari's own code):**

```
src/AkariTool.Infrastructure/Extensions/DI/InfrastructureServicesExtensions.cs
    → public static IServiceCollection AddInfrastructureServices(this IServiceCollection)

src/AkariTool.App/Features/Common/Extensions/DI/CompositionRoot.cs
    → public static IServiceCollection ConfigureAkariServices(this IServiceCollection)
    → public static IHost CreateAkariHost()                       // optional; Akari has no Hosting pkg today

src/AkariTool.App/Features/Common/Extensions/DI/SettingServicesExtensions.cs
    → AddSettingServices()
        → AddOptimizationServices()      // Core/Features/Optimize + Infra/Features/Optimize/Services
        → AddCustomizationServices()    // Core/Features/Customize + Infra/Features/Customize/Services
        → AddSoftwareAppServices()      // + WinGet/ + Chocolatey + icon services
        → AddAdvancedToolsServices()    // WimUtil, oscdimg, autounattend builder
        → AddAkariOsServices()          // pending D1
        → the two id-keyed registries (ISpecialSettingHandlerRegistry, ISpecialDiscoveryRegistry)

src/AkariTool.App/Features/Common/Extensions/DI/UIServicesExtensions.cs
    → AddUIServices()   // dialogs, theme, file picker, config, localization, all VMs, MainWindow
```

`CompositionRoot` calls them in load-bearing order, with a comment saying so:
`.AddInfrastructureServices().AddSettingServices().AddUIServices();`

**Three deliberate decisions here, stated explicitly:**

1. **Adopt the `TryAddSingleton`-empty-default trick + its explanatory comment.** Akari's
   `InfrastructureServiceExtensions` already registers empty `ISpecialDiscoveryRegistry` /
   `ISpecialSettingHandlerRegistry` defaults, and `UIServiceExtensions` re-registers the real
   ones. Winhance's discipline is *documented in a comment* because the correctness depends on
   registration order. Copy that discipline, and add the missing `InfrastructureContainerSmokeTests`
   that resolves every registration from `AddInfrastructureServices()` alone — the test is what
   stops the next person from "cleaning up" the ordering.
2. **Do NOT replicate Winhance's asymmetry.** Winhance deliberately registers Optimize,
   Customize and SoftwareApps *Infrastructure* services from the **UI** layer's
   `AddSettingServices()`. That is documented as a real asymmetry, and adopting it would be a
   **regression**: it makes Infrastructure services unreachable from an Infrastructure-only
   container and exists only to work around Winhance's own interface placement. Akari should
   keep domain services registered in Infrastructure and register only ViewModels/pages in the
   UI layer. Record this as a deliberate non-replication in the composition-root comment.
3. **Single catalog aggregation point stays put.** `CompatibleSettingsRegistry.GetKnownFeatureProviders()`
   is already the explicit, reflection-free, one-entry-per-catalog dictionary with per-feature
   error isolation. **The restructure must not move, rename or rewrite it.** Its catalogue
   *arguments* change (the static method names change on the move), but its location, shape and
   role do not. This is the one structural anchor that is already correct.

**`ServiceLocator` must die.** 48 `ServiceLocator.GetService<T>()` call sites across 27 App
files, including inside `SettingPageViewModel` itself (`:89-91`, `:146`, `:177`, `:334`) and in
every `Views/*Page.xaml.cs`. Two of those resolve an **Infrastructure** interface
(`IProcessRestartManager`) by fully-qualified name inside the method body, so the file's `using`
list understates its coupling. The `AkariPaths` CS0433 hazard and the `DispatcherService`
last-wins hazard are both downstream of the same root cause. Winhance has no locator at all —
pages resolve their ViewModel via `App.Services.GetRequiredService<T>()` in the constructor.

**`vendor/WinUI.Framework` decision (D9).** The vendored framework supplies `IoC.ServiceLocator`,
`Mvvm.ViewModelBase`, `Services.ILogService`, `Services.IThemeService`, `Services.IFileService`,
and `SettingsService` — which overlap conceptually with Winhance's `Common` slice. It is also
**not registered in `AkariTool.sln`** despite being a `ProjectReference` from 67 call sites. Two
options: (a) keep the framework for its non-DI types and delete only `IoC` usage; (b) fold the
needed pieces into `Features/Common/` and delete the framework. (b) is the parity-faithful
answer and removes the invisible-project problem, but it is a **large** change touching every
ViewModel base class. **Recommendation: (a) now, (b) only if the roadmap has budget for it.**
Note also that `IThemeService`/`IFileService` currently come from the framework, not from
`Core/Features/Common/Interfaces/` — so Akari's "every service is interface-first" story is
already partly false at the framework boundary.

---

### 2.5 Where Akari-specific capability lands (constraint: it must survive)

Per the project constraint, every Akari-only capability must survive the restructure. Here is
where each one lands. **D1 is the one decision the restructure cannot proceed without.**

| Akari capability | Lands in | Notes |
|---|---|---|
| **AkariOS service presets** (`ServicesPreset.*` 4 partials / 118 KB; `Core/Features/AkariOS/Models/ServicePresetKind`) | `Features/AkariOS/Services/` + `Core/Features/AkariOS/Models/` | Keeps `ServicesPreset.Stock.cs` / `.AkariOs.cs` / `.Playbook.cs` split |
| **AkariOS Playbook tweaks** (`PlaybookTweaks.*` 6 partials / 76 KB) | `Features/AkariOS/Services/` | Registry/IFEO/scheduled-task/FSUtil tweaks |
| **BCD** (`Core/AkariOS/Models/BcdOperation`, `Infra/Services/BcdBackup.cs`) | `Features/AkariOS/Services/` (or `AdvancedTools/` — it is boot tooling like the WIM path) | small; pick one and be consistent |
| **Competitive Mode** (`CompetitiveService` 12 refs, `CompetitivePrefs`, `CompetitiveSession` + `AkariOSPage.Competitive.cs` 680 lines, `--competitive` argv) | `Features/AkariOS/{Pages,ViewModels,Services}` | `CompetitiveSession` currently does `%AppData%` file I/O **in Core** — move to Infrastructure on the way |
| **GPU tooling** (`GpuTweaks` 20 refs, `AkariOSPage.GpuTools.cs` with ~40 direct `Registry.SetValue` calls) | `Features/AkariOS/Services/` | **Also** re-route through `IWindowsRegistryService` so it appears in change history and backup/restore (currently it does not — a separate, larger task; do not bundle with the restructure) |
| **PostInstall** (`PostInstallService` 13 refs, `AkariOSPage.PostInstall.cs`) | `Features/AkariOS/Services/` | |
| **Defender** (`Infra/Services/DefenderService` 25 refs, `App/Services/DefenderService` 329 lines, `DefenderPhase2Scheduler`, `Defender/NoDefender.cab` + `DisableDefender.ps1`, `DefenderToggleViewModel`) | `Features/AkariOS/{Services,Resources}` + the bespoke row stays in `GamingViewModel` | **Note the duplication**: there are two `DefenderService` classes (App-layer 329 lines with 19 registry calls, Infra-layer static). Consolidate to one during the move. |
| **Nvidia** (`NvidiaProfileService` 7 refs, `Nvidia/Settings.nip` 22 KB) | `Features/AkariOS/Resources/Settings.nip` | The csproj's `Condition="Exists(...)"` means deleting the file **silently drops the feature instead of failing the build**. Make it a hard requirement. |
| **Home** (`HomePage` 86 xaml-refs, `HomeViewModel` 17 refs, `DriftedCount`) | **D5** | Winhance has **no home page** and lands on `SoftwareApps` by default. |
| **Backup/Restore** (`BackupPage` 74 xaml-refs, `BackupViewModel`, `SettingBackupService` 658 lines / 33 refs, `ImportReviewDialog` 19.6 KB) | `Features/Common/Services/` (engine) + **D5** (surface) | *Schema fork: D6.* |
| **Verify / drift** (`VerifyPage` 168 xaml-refs, `VerifyViewModel`, `DriftScanner`, `DriftBaseline`) | `Features/Common/Services/{DriftScanner,DriftBaseline}` + `Features/Common/ViewModels/VerifyViewModel` | Akari-only, and currently **broken** (§5). Fix, keep. |
| **Tools page** (`ToolsPage` 25 refs: system info, repair, network, quick shortcuts) | `Features/AdvancedTools/` (it shells out to the embedded `Scripts/*.ps1`) or `Features/Common/` | Akari-only |
| **Shader cache** (`IShaderCacheService`, `ShaderCacheService`, `AkariOSPage.ShaderCache.cs` 349 lines) | `Features/Common/Services/` (interface is already in Core `Interfaces/`) | |
| **Steam library** (`Infra/Services/SteamLibrary.cs`) | `Features/SoftwareApps/Services/` | Akari-only |
| **Account / OTS** (`Infra/Services/AccountService.cs`, `TweakHelpers.GetRealUserSid`) | `Features/AkariOS/Services/` or `Features/Common/Services/` | Akari's analogue of Winhance's `IInteractiveUserService` |
| **De-elevated test build** (`build-deelevated.ps1` → `/p:DeElevatedTest=true` → `obj/DeElevated/` + generated `asInvoker` manifest) | Repo root, survives untouched | Akari-only, used for UIA/automation testing. **Do not delete during the restructure** — it is the only way to drive the UI from an automation harness. |
| **47 embedded `.ps1`** | split across `Features/*/Resources/Scripts/` | D8 |

#### D1 — the AkariOS domain question (**blocks the restructure**)

Winhance has exactly 5 domains mirrored across 3 layers. AkariOS is 35 Infrastructure files,
9 XAML partials / 2,631 lines of page, 1 `AkariOSViewModel`, plus 2 `Core` model files — i.e. a
full slice with no name in Winhance's taxonomy. Three options:

- **(A) Add a 6th domain `AkariOS` mirrored across all three layers → 5 + 1.** AkariOS keeps its
  own `Interfaces/`, `Models/`, `Services/`, `Pages/`, `ViewModels/`. Cost: one domain that has
  no Winhance counterpart, so the "5-across-all-layers" rule becomes "6-across-all-layers".
  Benefit: 35 files and 2,631 lines land in one obvious place; nothing is scattered; the
  vertical-slice rule still holds *identically*, just with a different domain count.
- **(B) Fold into existing domains.** Registry tweaks (`PlaybookTweaks`, `SystemStateReader`,
  `GpuTweaks`) → `Optimize`; service presets + BCD + PostInstall → `AdvancedTools`;
  Competitive Mode → `AdvancedTools`; Nvidia/Defender → `AdvancedTools`. Cost: one 9-partial
  page becomes four different pages in three different domains; a single user-facing feature
  ("AkariOS") becomes 3 nav destinations; **2,631 lines of cohesive code-behind get cut up**.
  This is the worst option and it is what "5-across-all-layers" naively implies.
- **(C) Hybrid.** Keep `AkariOS` as a **UI-only + Infrastructure** slice (no `Core` contracts —
  mirror Winhance's `Settings` slice, which is the documented precedent for a slice that needs
  no contract layer), and keep `Core/Features/AkariOS/Models/{ServicePresetKind, BcdOperation,
  PlaybookTweakAction}` inside… `Core/Features/AdvancedTools/Models/`. Cost: three model records
  live under a domain their consumers are not in.

**Recommendation: (A).** The parity constraint is about *architecture, conventions, feature set
and UX patterns* — not about the literal number five. Option (A) replicates the vertical-slice
rule exactly and keeps one cohesive Akari feature cohesive. Winhance's own doc pre-authorises a
domain count that isn't "the obvious four": `Settings` exists only in the UI layer. Also choose
the **landing page**: Winhance defaults to `SoftwareApps`; Akari defaults to Home. Pick one
deliberately (recommendation: keep Home — it is Akari value per constraint 3 — but then Home is
Akari's one intentional navigation divergence and should be documented as such).

---

## 3. Feature gap — every Winhance capability Akari lacks

Every item below is **in scope** (constraint 4: take everything from Winhance). Effort is
S ≤ ½ day, M = 1–3 days, L = > 3 days, **at parity quality, clean-room**. "Depends on" names
the phase-0/1 structural work that must land first.

### 3.1 `Optimize`

| # | Missing capability | Effort | Depends on |
|---|---|---|---|
| O1 | `SectionPageViewModel<TSectionInfo>` + `ISectionInfo` + `OptimizeViewModel.Sections` + 6 `…OptimizePage.xaml` renames + `IOptimizationFeatureViewModel` marker + `OptimizePage.NavigateToSection` (replacing 6 hard-coded flyout buttons) | **M** | Phase 2 (domain move), D1 |
| O2 | `BaseSettingsFeatureViewModel` — Akari's `SettingPageViewModel` is the equivalent; needs `ISettingsFeatureViewModel` (`ModuleId`, `Settings`, `GroupedSettings`, `LoadSettingsAsync`, `ApplySearchFilter`), `SemaphoreSlim` load gate, debounced search, deferred event subscriptions | **M** | O1 |
| O3 | `PowerService` into `Infra/Features/Optimize/Services/` + register as `ISpecialSettingHandler` for power-plan selection | **S** | Phase 2 |
| O4 | `PowerPlanComboBox` AC/DC "Dual" control parity (Akari has `PowerPlanComboBox` in `Views/Controls`; Winhance has it in `Common/Controls` and drives `NumericRange` Dual variants) | **S** | §3.5 |
| O5 | Windows-Version-Filter button in the title bar (show-only-compatible vs show-all-marked) — Akari has the *filter* (`IWindowsCompatibilityFilter` + `_windowsFilterBypassedSettings`) but no title-bar toggle | **S** | — |
| O6 | Per-section **NEW badge** / **Recommended·Default·Custom·Preference** pill + per-card quick-set buttons + `FeatureBadgeAggregator` (Akari has `SettingBadgeCalculator` and `NewBadgeService`; needs the hub aggregator + the `Common/Controls/SettingDescriptionWithBadges` + `Resources/BadgeStyles.xaml` home) | **M** | §3.5 |
| O7 | Per-setting technical-details expander with **"Open in Registry Editor"** — Akari has `TechnicalDetailsManager` (523 lines) + `RegeditLauncher`; needs the shared UI vocabulary | **S** | §3.5 |
| O8 | **Catalog-per-file parity** for the 4 huge tuning catalogs: split `GamingOptimizations` (3,427), `PrivacyOptimizations` (2,934), `ExplorerOptimizations` (2,026), `PowerOptimizations` (1,624) into `<Name>.<Group>.cs` partials, replicating `ExternalAppDefinitions.*`. Winhance explicitly says *do this*, despite its own files being large. | **M** | Phase 2 (pure file moves, zero behaviour change) |
| O9 | `ISettingsLoadingService` + `ISettingPreparationPipeline` + `ISettingViewModelFactory` + `ISettingViewModelEnricher` — Winhance's row-VM construction pipeline. Akari builds rows inline in `SettingPageViewModel.CreateItem` | **M** | O2 |

**Content delta:** ~+13 Optimize settings net. Not the bottleneck.

### 3.2 `Customize`

| # | Missing capability | Effort | Depends on |
|---|---|---|---|
| C1 | `IWallpaperService` (Core `Customize/Interfaces/`) + `WallpaperService` (Infra `Customize/Services/`) — `SetWallpaperAsync`, `GetDefaultWallpaperPath` | **S** | — |
| C2 | `ThemeWallpaperApplier` — the `theme-mode-windows` special handler that additionally swaps the desktop wallpaper to the OS default (Win11 `img0.jpg`/`img19.jpg`, Win10 `img0_3840x2160.jpg`) when the "change wallpaper too" checkbox is ticked | **M** | C1 |
| C3 | `ICustomizationFeatureViewModel` marker + `CustomizeSectionInfo` + 4 `…CustomizePage.xaml` renames + `CustomizePage.NavigateToSection` | **M** | Phase 2 |
| C4 | `AppearanceOptimizations` (10 defs) → `WindowsThemeCustomizations` (2 defs): decide the fate of the 8 surplus definitions | **S** | D3 |
| C5 | `Explorer` −20 settings: Winhance's Explorer catalog has ~87 defs including the full Context Menu child-set (Take Ownership, Windows Terminal, Open in PowerShell, SFC/DISM/CHKDSK, Compress-to, `.ps1` Edit/Run), File Associations (legacy Photo Viewer, legacy Notepad), Desktop dynamic lighting, icon cache/thumbnail sizes. Akari has 55 + 12 Desktop | **M** | Phase 2 |
| C6 | The `Desktop` ↔ `Explorer` merge (D3) | **S** | D3 |

### 3.3 `AdvancedTools` — the "Absent" items, re-verified

**`AutounattendGeneratorPage` — page-level Absent, capability PRESENT.**
Akari has `AdvancedToolsPage.Generator.cs` + `App/Services/AutounattendService.{,Xml,ScriptPreamble,ScriptSystem,ScriptUser,Tweaks}.cs` (860 lines). Winhance has a dedicated page + `IAutounattendXmlGeneratorService` + `AutounattendScriptBuilder` + 5 `ScriptSections`.

| # | Missing | Effort | Depends on |
|---|---|---|---|
| A1 | Split `AdvancedToolsPage` into hub + `Pages/AutounattendGeneratorPage.xaml` + `Pages/WimUtilPage.xaml`, with the ~2,200 lines of code-behind logic moved into ViewModels | **L** | Phase 2 |
| A2 | 6 Core contracts extracted: `IWimImageService`, `IWimCustomizationService`, `IIsoService`, `IOscdimgToolManager`, `IDriverCategorizer`, `IAutounattendXmlGeneratorService` | **M** | A1 |
| A3 | 4-step wizard decomposed into `WimStep1..4ViewModel` + `WimImageFormatViewModel` + `WizardStepState`/`WizardActionCard` models, with auto-expand-once-extraction-completed | **M** | A1 |
| A4 | Split `WimUtilService` (851, one class) into `WimImageService` / `WimCustomizationService` / `OscdimgToolManager` / `IsoService` + extract `IDismProcessRunner` (`RunProcessWithProgressAsync` + `CheckDiskSpaceAsync`) and add `InsufficientDiskSpaceException` to `Core/Features/Common/Exceptions/` | **M** | A2 |
| A5 | Split `AutounattendService` into `AutounattendScriptBuilder` + the 5 `ScriptSections` (preamble, feature-registry, power-settings, special-feature, app-removal) | **M** | A2 |
| A6 | Embedded `Common/Resources/AdvancedTools/autounattend-template.xml` + XML well-formedness validation + **UTF-8 without BOM** write (a Windows Setup hard requirement) | **S** | A5 |
| A7 | `DriverCategorizer` splitting `pnputil` output into display/network/system/MediaKeying; `PowerShellScriptUtilities`; `RegistryCommandEmitter` | **M** | A4 |
| A8 | Cross-slice wiring Winhance has and Akari doesn't: `ISelectedAppsProvider` (SoftwareApps selection → autounattend), pulling selected `SettingDefinition`s from `ICompatibleSettingsRegistry`, and the "generated Winhancements.ps1 injected into the template's FirstLogon/RunOnce" contract | **M** | A5, §3.4 |
| A9 | Save-filename enforcement (`autounattend.xml` exactly, else warn), "more generation options coming soon" InfoBar, success dialog offering to jump to WIMUtil | **S** | A1 |

**`WimUtilPage` (WIM/ISO builder) — page-level Absent, capability PRESENT.** All four steps exist; the documented Winhance steps 1–4 map onto Akari's `BuildStep1..BuildStep4`. Missing relative to Winhance: disk-image **detection result** modelling (`ImageDetectionResult`, `ImageFormatInfo` in Core), explicit WIM⇄ESD size display/delete cards for the "both images exist" state, `IDismProcessRunner` progress, and the ADK-`oscdimg` discovery across `Windows Kits\10` and `\11` amd64/x86 (Akari has 31 oscdimg references but the *discovery* strategy needs confirming).

**`WindowsPackageManager.Interop` project — PRESENT, mis-placed.** Not a gap. Akari's `vendor/WinGet.Interop` has the same hand-written factory trio plus `ClsidContext`/`ClassModel`/`ClassesDefinition` and a CsWinRT projection from `Microsoft.Management.Deployment.winmd`. Actions: (a) move to `src/WindowsPackageManager.Interop/` for layout parity, (b) confirm `WinGetComSession` uses `WindowsPackageManagerStandardFactory` with `allowLowerTrustRegistration: true` — Winhance documents that `WindowsPackageManagerElevatedFactory` **hangs** in self-contained mode (microsoft/winget-cli#4377) and that this forced their choice, (c) keep it in its own `.sln` if desired (Winhance does).

**Localization — genuinely Absent.** Highest-leverage item in the whole analysis; see §6.4.

**Builder / Config Review mode — genuinely Absent.**

| # | Missing | Effort | Depends on |
|---|---|---|---|
| M1 | `AkariMode` enum (Winhance: `WinhanceMode`) + `BuilderTarget` enum + `BuilderEdit` model + `IApplicationModeService` | **S** | — |
| M2 | `ConfigReviewService` implementing 6 interfaces from one singleton, private `SetMode` chokepoint, `ModeChanged` event | **M** | M1 |
| M3 | Builder-mode bar (`BuilderModeBarViewModel`): title, "nothing here changes this pc", Config/Autounattend target radio pair, Save (label swaps), Cancel, first-run explainer with don't-show-again | **M** | M1, A1 |
| M4 | `RecordBuilderEdit` wired into every apply handler with the **Builder-mode short-circuit** (record intent, never apply) — the second invariant in Winhance's canonical apply flow. Akari's `SettingItemViewModel` needs the `if (IsBuilderMode) { …; return; }` branch | **M** | M1, O2 |
| M5 | Config Review: import dialog with 4 option cards (`ImportOwn` / `ImportRecommended` / `ImportBackup` / `ImportWindowsDefaults`), skip-review checkbox, per-app Install/Uninstall/Select radios, clean-taskbar / clean-start-menu / change-wallpaper checkboxes | **M** | M2, D6 |
| M6 | Review-mode bar with `"N of M reviewed (K will be applied)"`, **Apply gated until fully reviewed**, per-card Approve/Reject, nav + breadcrumb badges, `Show Only Changes` filter driven by a diff dictionary (not per-VM flags), Windows-Version-Filter forced on during review | **L** | M2 |
| M7 | 3 embedded baseline configs + `IConfigMigrationService` | **S** | D6 |

**Winhance's own documented Builder-mode gap, carried forward as a known limitation, not a
target:** `BuilderEdit` does **not** record `NumericRange` edits or AC/DC power edits; they fall
back to the system-seeded value. Akari's implementation should replicate the *architecture*
(mode service, short-circuit, save/cancel) and — being clean-room — may optionally fix the
recording gap. Flagged because replicating the gap silently would produce a mode that appears
to work and silently drops numeric edits. **Do not replicate the bug; do not let it surprise
anyone.** Also: the Autounattend generator in Akari today is reachable from Builder mode in
Winhance (`ExportBuilderAutounattendAsync`) — Akari has no such path.

### 3.4 `SoftwareApps`

| # | Missing capability | Effort | Depends on |
|---|---|---|---|
| S1 | **18 of Winhance's 20 Core contracts** — `IWindowsAppsService`, `IExternalAppsService`, `IAppStatusDiscoveryService`, `IAppInstallationService`, `IWindowsAppUninstallService`, `IExternalAppUninstallService`, `IBloatRemovalService`, `ILegacyCapabilityService`, `IOptionalFeatureService`, `IChocolateyService`, `IDirectDownloadService`, `IStoreDownloadService`, `IAppIconResolver`, `IAppxIconSource`, `IRepoIconSource`, `IIconManifestService`, `IAppxPackageSource`, `ISelectedAppsProvider`. Akari has 4 WinGet contracts and one 646-line App-layer `SoftwareAppService` | **M** | Phase 2 |
| S2 | Split `SoftwareAppService` (646) into `WindowsAppsService` / `ExternalAppsService` / `AppStatusDiscoveryService` / `AppInstallationService` / `AppIconResolver` / `LightVariantSynthesizer` under `Infra/Features/SoftwareApps/Services/` | **L** | S1 |
| S3 | Install fallback chain: WinGet → MsStore → Chocolatey → direct download, with `WinGetInstallerOverride`, portable-installer detection + Start-Menu shortcut creation | **M** | S2 |
| S4 | **`IChocolateyService`** — `IsChocolateyInstalledAsync` → bootstrap from `community.chocolatey.org/install.ps1` → `choco install -y --no-progress --ignore-checksums` → **`CleanupStalePackageRecordAsync` ghost-package recovery** (choco says installed, Winhance detected missing → `choco uninstall` then retry). *Akari's position today: no Chocolatey service at all — WinGet-only, plus a generic `ToolService.RunPackageCommand` PowerShell shell-out.* | **M** | S3 |
| S5 | `Services/WinGet/Utilities/`: `WinGetCliRunner` (bundled/system CLI), `ConPtyProcess` (ConPTY so `winget.exe` renders a live progress stream), `WinGetProgressParser`, `WinGetExitCodes` | **M** | — |
| S6 | Vendored `winget-cli/` payload (`winget.exe` + server + runtime DLLs + `winget-version.txt`) copied to output + a refresh script | **S** | S5 |
| S7 | `IStoreDownloadService` fallback via the `store.rg-adguard.net` API (download `.msixbundle` + deps → `Add-AppxPackage`) with a "don't show again" preference | **M** | S3 |
| S8 | `IRepoIconSource` (jsDelivr + **sha256 verification** + User-Agent) + `IconCacheMigration` + `LightVariantSynthesizer` + `IIconManifestService` | **M** | S1 |
| S9 | Scheduled-task-deferred bloat removal: catch `ExecutionPolicyException` → `RemovalOutcome.DeferredToScheduledTask` with `LogonType=5` and `deleteAfterRun`; plus a "save removal scripts" option on the removal confirmation | **M** | S1 |
| S10 | 3 view modes (Card / **Table via `DataGrid` 7.1.2** / Compact) + 3 sort modes + `SoftwareAppsViewMode`/`AppSortMode`/`ISelectable` models + `DataGrid` column headers | **L** | Stack spike (§7.2), S1 |
| S11 | `Views/{WindowsAppsHelpContent,ExternalAppsHelpContent}.xaml` + the legend dialog (Installed / Can be reinstalled / Not installed / **Cannot reinstall** / Warning) + `HasInstabilityWarning` amber pill | **S** | — |
| S12 | `RemovalStatusViewModel` / `RemovalStatusContainerViewModel` running per-item outcome list; `AppChangeKind` + `IChangeHistoryService.LogAppChange` for every install/remove (Akari has `ChangeHistoryService`) | **M** | S1 |
| S13 | `IconCoverageTests`-style invariant test: every catalog entry has an icon, against a **checked-in** manifest snapshot (never hits the network) | **S** | — |
| S14 | 4 pages → 1 page + 2 tabbed grids (D4), and `DebloatPage` absorbed into the removal flow | **M** | D4 |
| S15 | `WindowsAppsService` warns before a Store install when the Windows Update policy would block it (Akari already special-cases `updates-policy-mode`) | **S** | — |

### 3.5 `Common` / cross-cutting

| # | Missing capability | Effort | Depends on |
|---|---|---|---|
| N1 | **Localization**: `ILocalizationService` (Core interface / Infra impl), 29-locale JSON in `Features/Common/Localization/` copied to output (not embedded, so a language picker needs no resource table), `en` fallback + `"[key]"` sentinel, `SetLanguage` holding `CurrentCulture` at Invariant (so `NumberBox` stays locale-independent), `_Meta_LanguageDisplayName`, `ISettingLocalizationService` rewriting definitions non-destructively via `setting with { … }`, `SettingLocalizationKeys` key derivation, RTL `FlowDirection`, live re-render on `LanguageChanged`, 2 guardrail tests | **L** (plumbing **M**, content per-locale **L**) | Phase 2 (before bulk feature work — §6.4) |
| N2 | **Composition root** (3 files) + `InfrastructureContainerSmokeTests` | **M** | §2.4 |
| N3 | `ServiceLocator` elimination — 48 call sites, 27 files; pages resolve ViewModels from DI in their constructors | **M** | N2 |
| N4 | `Common/Controls` additions: `SettingsCardItem`, `SettingsListView`, `SettingDescriptionWithBadges`, `QuietInfoBar`, `UniformWrapPanel`, `WebsiteLinkButton`, `ComboBoxEx` — **requires adopting `CommunityToolkit.WinUI`** | **L** | Stack spike (§7.2) |
| N5 | `Common/Dialogs/`: `ConfigImportDialogBuilder`, `SponsorsDialogBuilder`, `TaskOutputDialogBuilder`; `IDialogService` with information/warning/error/confirmation(+don't-show-again)/custom-content; replaces the serialized `ContentDialog` helper `TweakDialogs` | **M** | N2 |
| N6 | `Win32FileDialogHelper` + `IFilePickerService` + `IMainWindowProvider` (Akari's behaviour is already right; it needs a Winhance-shaped home and no `Window` leaking into ViewModels) | **S** | Phase 2 |
| N7 | `UiZoomManager` (Ctrl +/−/0, Ctrl+wheel, `ZoomViewport`/`ZoomHost`), `PageScrollHelper` (PageUp/Down/Home/End), `WindowSizeManager` | **M** | — |
| N8 | `TaskProgressService` → `IMultiScriptProgressService` + `CreateDetailedProgress()` + "open output in a scrollable dialog" | **S** | — |
| N9 | `IDismProcessRunner` (progress + `CheckDiskSpaceAsync`), `IProcessExecutor.ExecuteWithStreamingAsync`, `InsufficientDiskSpaceException`, `ExecutionPolicyException`, `FireAndForget(logService)` extension | **M** | A4 |
| N10 | `StartupLogger` static pre-DI file logger; `AppDomain`/`Application`/`TaskScheduler` three-layer exception net; `ILogService` with 30-day/50-file retention | **S** | — |
| N11 | Single-instance enforcement via `AppInstance.FindOrRegisterForKey` **before WinUI init** (redirect + foreground the incumbent), plus `Program.cs` as the real entry point (`DISABLE_XAML_GENERATED_MAIN`). Akari has no `Program.cs` — WinUI's generated `Main` runs. This is also what makes the title-bar More-menu "Close Winhance" and the update-relaunch safe | **M** | N2 |
| N12 | Title-bar chrome: pane toggle, beta banner, **3-way mode switcher**, Windows-version-filter button, Docs / Report-a-Bug / Donate; More-flyout (Docs, Bug, **Check for Updates** w/ in-window install, **Logs folder**, **ChangeHistory.txt**, **Scripts folder**, Sponsors, Close); Mica/Acrylic backdrop with Win10 fallback | **M** | N2, M3 |
| N13 | `IUpdateCheckViewModel` / GitHub-Releases update check + installer download + "Install Now / Relaunch" InfoBar (Akari has a static `Infra/Services/UpdateService` self-updater but no in-window UX) | **M** | N11 |
| N14 | `SponsorsService` with bundled-fallback assets | **S** | N12 |
| N15 | 4th + 5th test project: `AkariTool.App.Tests` + `AkariTool.IntegrationTests` (container smoke, config round-trip, localization integrity, temp-dir filesystem) | **M** | N2 |
| N16 | `AkariTool.sln` hygiene: add `vendor/WinUI.Framework` (67 call sites, currently invisible to tooling) | **S** | — |
| N17 | `Resource/` vs `Assets/` de-duplication (~3.6 MB unreferenced binaries) + resolve whether `NavIcons/` is consumed | **S** | — |
| N18 | Catalog split for `SettingItemViewModel` (979 lines, 13 `[ObservableProperty]` + 9 `[RelayCommand]` + Power specialisation) — **only with a characterisation test in hand** | **L** | O2, N15 |
| N19 | Two `UpdateService` classes in Infrastructure (`AkariTool.Services.UpdateService` = self-updater; `…Features.Optimize.Services.WindowsUpdatePolicyHandler` = the Windows Update policy setting). Winhance has one name for the second. **Rename on the move or the collision will bite.** | **S** | Phase 2 |

### 3.6 Effort roll-up

| Group | S | M | L | Notes |
|---|---|---|---|---|
| Optimize | 3 | 5 | 0 | content ≈ parity |
| Customize | 3 | 4 | 0 | −9 settings, 2 services |
| AdvancedTools | 2 | 6 | 1 | **capability present, shape absent** |
| SoftwareApps | 4 | 9 | 3 | catalog at parity, 18 contracts + 24 services missing |
| Modes | 1 | 5 | 1 | genuinely absent |
| Common / shell | 6 | 8 | 3 | localization is the big L |
| **Total** | **19** | **37** | **8** | **plus the restructure itself (§6) and AkariOS (D1)** |

---

## 4. Convention and pattern deltas

### 4.1 Elevation — **not the divergence the brief assumed**

| | Akari | Winhance |
|---|---|---|
| Manifest | `app.manifest` → `requestedExecutionLevel level="requireAdministrator" uiAccess="false"` | **identical** |
| Second build | `build-deelevated.ps1` rewrites the manifest to `asInvoker` into an isolated `obj/DeElevated/` + `bin/DeElevated/`, passed via `/p:DeElevatedTest=true`, with a 5-line csproj comment explaining why `BaseIntermediateOutputPath` must *not* be redirected (it re-un-excludes stale `obj` from the compile globs) | none |
| Beyond admin | **`ElevationService` — in-process impersonation to SYSTEM / TrustedInstaller.** Borrows a token, duplicates it into an impersonation token, attaches it to the calling thread so every `Registry.*` call inside the supplied action performs its access check against the elevated identity. Enables `SeBackupPrivilege`/`SeRestorePrivilege` for DACL-bypass writes. Documented hazard: *"impersonation is a per-thread state; the action must be fully synchronous; awaiting inside it can resume on another thread."* | **OTS elevation** — `IInteractiveUserService` detects when the user consented with different credentials, exposes `IsOtsElevation`, `InteractiveUserSid/Name`, `GetInteractiveUserFolderPath`, `HasInteractiveUserToken`, `RunProcessAsInteractiveUserAsync` (token stolen from `explorer.exe` via `UserTokenApi` P/Invoke). An InfoBar tells the user. |
| Per-user settings | `TweakHelpers.GetRealUserSid` (OpenProcessToken + WindowsIdentity) | `IInteractiveUserService` |

**Assessment.** The elevation *model* already matches: both apps are elevated from launch and
neither has a runtime `runas` relaunch. The divergence is that **each side has an elevation
extension the other lacks** — Akari has SYSTEM/TrustedInstaller impersonation and a
de-elevated automation build; Winhance has OTS detection and interactive-user token theft.

**Parity consequences:**
1. **Adopt OTS detection.** Winhance's OTS path exists because an always-elevated app can write
   HKCU to the *admin's* profile instead of the *user's*. Akari's app has the identical exposure
   and no equivalent guard. Port `IInteractiveUserService` + `UserTokenApi` as
   `Features/Common/` (cross-domain: Power, SoftwareApps, and Customize all need it).
   **Effort M.**
2. **Keep `ElevationService` and keep `build-deelevated.ps1`.** Both are Akari value. Explicitly
   protect them in the restructure.
3. **`ElevationService`'s thread-affinity hazard is a live restructure risk.** Its own doc
   comment says awaiting inside an impersonated action silently loses the elevated identity. If
   the restructure makes any `ElevationService.Run*` call site's path async (e.g. routing AkariOS
   GPU tweaks behind `ISettingOperationExecutor`, which is async), the impersonation silently
   stops applying — no exception, no log. **Audit every `ElevationService` call site for
   synchronicity as part of the move, and leave a comment at each one.**

**One correction to the brief:** `IProcessRestartManager` is **not** an elevation mechanism. It
is Winhance's restart-coalescing service, and Akari's is a 1:1 port with the same name and role
(`SuppressRestarts()` → `FlushCoalescedRestartsAsync()`, honoured by `SettingDefinition.
RestartProcess`). Nothing to decide; nothing to change.

### 4.2 File pickers — **already at parity**

The brief asked what Akari uses. Answer: `src/AkariTool.App/Services/AkariFileService.cs`,
335 lines, which **already drives the classic Win32 COM common dialogs** — `IFileOpenDialog` /
`IFileSaveDialog` via `CoCreateInstance`, with private RCW interfaces for `IFileDialog`,
`IFileOpenDialog`, `IFileSaveDialog` and a private `ApplyFilters` helper. Its header comment
records the identical diagnosis Winhance's does:

> the framework uses WinRT `Windows.Storage.Pickers` (FileOpenPicker/FileSavePicker/FolderPicker),
> which throw `COMException 0x80004005` … FIX: drive the classic Win32 COM common dialogs …
> These work at ANY integrity level.

Call sites already route through it (`AkariOSPage.Competitive.cs` Browse button,
`AdvancedToolsPage`, `BackupViewModel`) and each carries a migration note pointing at
`IFileService.PickSingleFileAsync`.

**So this is a zero-gap capability.** The only parity work is **placement and shape** (N6):
move `AkariFileService` → `Features/Common/Helpers/Win32FileDialogHelper.cs`, extract
`IFilePickerService` + `FilePickerService` + `IMainWindowProvider` so no `Window` leaks into a
ViewModel, and declare the COM GUIDs in `NativeMethods.txt` / use CsWin32 instead of
hand-rolled RCW interfaces if the stack spike (§7.2) permits. **Effort S.**

### 4.3 `ISettingsFeatureViewModel` → `Optimize.ViewModels` — **must NOT be replicated**

Winhance parks its shared setting-ViewModel bases — `SettingItemViewModel`,
`BaseSettingsFeatureViewModel`, `SettingsGroup`, `TechnicalDetailsManager` — in
`src/Winhance.UI/Features/Optimize/ViewModels/`, and then
`Features/Common/Interfaces/ISettingsFeatureViewModel.cs` imports
`Winhance.UI.Features.Optimize.ViewModels`. Winhance's own anti-patterns section calls this out:
*"`Common` now depends on `Optimize`, inverting the intended direction, and the slice boundary is
a lie — deleting Optimize would break Customize's type system."*

**Akari's equivalent is already in the right place**: `ViewModels/Tweaks/` (9 files) holds the
shared machinery and `Core/Features/Common/Interfaces/` imports nothing from a page VM. **The
restructure must move these to `Features/Common/ViewModels/` and keep `ISettingsFeatureViewModel`
(when introduced, O2) referencing only `Common` types.** Two specifics to preserve while moving:
`SettingPageViewModel.CreateItem` is `virtual` solely so `PowerViewModel` can specialise it, and
`PowerViewModel.AdditionalResolutionCatalogs()` is Akari's only intentional cross-domain catalog
reference (Power → Privacy's `privacy-lock-screen`). Keep both extension points; do not
"clean them up".

Winhance's stated correction is the target: *put the shared base in `UI/Features/Common/
ViewModels/` and have `ISettingsFeatureViewModel` reference only `Common` types.*

### 4.4 Badge computation, `SettingDefinitionToggleState`, catalog-per-file

| Area | Akari | Winhance | Verdict |
|---|---|---|---|
| **Badge computation** | `SettingBadgeCalculator.cs` — 21 KB, `public static class`, doc: *"Recommended/Default/Custom/Preference pills to display — no I/O, no side effects."* Handles `RecommendedToggleState`/`DefaultToggleState`, scheduled-task `RecommendedState`/`DefaultState`, and ComboBox/PowerCfg recommendation presence. Used by `SettingItemViewModel` (5 sites) and `SettingBadgeCalculator` self (7). Plus `NavBadgeService` + `NewBadgeService` | `FeatureBadgeAggregator` (UI `Common/Helpers`) + `SettingItemViewModel.ComputeBadgeState()` + `SettingBadgeKind`/`SettingBadgeMode` + `SettingDescriptionWithBadges` + `BadgeStyles.xaml` | **Logic ≈ parity. Presentation missing.** Needs the `Common/Controls` + `Common/Resources` home and a hub aggregator (O6) |
| **`SettingDefinitionToggleState`** | Present in Core (`SettingDefinitionToggleState.cs`), consumed by `SettingBadgeCalculator` (7), `SettingItemViewModel` (5), `SettingStateReader` (2), `SystemSettingsDiscoveryService` (2) — a **read-path** type representing "toggle ON = key absent" | Winhance's equivalent shape lives on `SettingDefinition` (`RecommendedToggleState`/`DefaultToggleState`) and Winhance's own note in `SettingDefinition.cs:99-104` records that these **are not in Winhance** — they give Akari toggles a one-directional message | **Akari is ahead and must not be regressed.** Out of scope (adopted 1:1 model). Keep the divergence and keep its provenance comment |
| **Catalog-per-file** | **Mixed.** Tuning catalogs: 4 monolithic files (3,427 / 2,934 / 2,026 / 1,624). Software catalogs: **16 `ExternalAppCatalog.<Category>.cs` partials — already exactly Winhance's recommended pattern.** | The same mixed state — `GamingAndPerformanceOptimizations.cs` is 4,151 lines, but `ExternalAppDefinitions` is split into 15 category partials. Winhance's own verdict: *"That is the pattern to replicate … Never grow a single 4,000-line catalog file"* | **Akari must split the 4 tuning catalogs (O8).** It must **not** un-split the software catalogs, and it must **not** copy Winhance's file sizes. Winhance flags its own 4,151-line catalog as an anti-pattern — replicate the advice, not the file |

### 4.5 Other deltas worth naming

| Delta | Akari | Winhance | Recommendation |
|---|---|---|---|
| Package stack | `Microsoft.WindowsAppSDK **2.3.1**`, `CommunityToolkit.Mvvm 8.4.2`, `Material.Icons.WinUI3 3.0.2`, `FluentIcons.WinUI 2.1.326`, `Microsoft.Extensions.DependencyInjection 10.0.10`, vendored `WinUI.Framework`. **No `CommunityToolkit.WinUI`, no `DataGrid`, no CsWin32, no `Microsoft.Extensions.Hosting`, no `Microsoft.Xaml.Behaviors`.** | WinAppSDK **1.8.260416003** + `CommunityToolkit.WinUI` 8.2.x (SettingsControls, Primitives, Behaviors, Collections, Triggers, Extensions) + `UI.Controls.DataGrid` 7.1.2 + `Microsoft.Xaml.Behaviors.WinUI.Managed` 3.0.1 + CsWin32 0.3.183 + `Microsoft.Extensions.Hosting` 10.0.7 | Akari is **ahead** on WinAppSDK. Do **not** downgrade. Adopting `CommunityToolkit.WinUI` on 2.3.1 is the single biggest technical risk in this plan → spike first (§7.2). Winhance's own note: the metapackage is mandatory, and WinAppSDK version races cause MSB4011 + MSIX `CustomBeforeMicrosoftCommonTargets` errors |
| Shared VMs | `ToolService` is the one `I`-less service; registered concrete-then-aliased | all services `I`-prefixed | keep `ToolService` as the documented legacy exception |
| `BuilderTarget` | absent | Core enum | M1 |
| Typed domain exceptions | none | `ExecutionPolicyException`, `InsufficientDiskSpaceException` | N9 |
| Structured-catalog comments | absent | `unattend-template.xml` with `<LogicalName>` | A6 |
| Registry ("`Registry` dir") | absent | `RegistryCommandEmitter` emits `reg.exe` commands | A7 |
| Localization key naming | n/a | `Area_Subject` (`Category_*`, `Nav_*`, `Setting_*`, `Feature_*`, `Button_*`, `InfoBadge_*`, `Tooltip_*`) | N1 |
| Code-behind discipline | mixed: 11 declarative pages have 28-line thin code-behind; `AkariOSPage` (9 partials, 2,631), `AdvancedToolsPage` (4 partials, ~2,200), `MainWindow` (557) carry logic in code-behind | thin sub-pages (34 lines), but `OptimizePage.xaml.cs` is 1,118 and `MainWindow.xaml.cs` ~900 — Winhance calls both its own worst instances | Replicate the **thin sub-page**. Akari is already better than Winhance on `AkariOSPage`'s partial split and worse on `AdvancedToolsPage` |
| Catalog validators | `SettingCatalogValidator` exists, **never called from production** | same validator, exercised by tests against the data | Both have the defect. Fix in Akari (see §5) — do not replicate the "tested against hand-built fixtures" shape |
| Nav router | `PageMap` + 4 `*DetailTags` sets inside `MainWindow.xaml.cs` | `Helpers/NavigationRouter.cs` with two symmetric dictionaries | Extract it (Phase 2) |

---

## 5. Bugs and dead code to resolve during the restructure

These are first-class work items, not cleanup chores. **Recommendation: do 5.1–5.4 *before*
the file moves** — they are behaviour fixes, they are cheap while the files are where they are,
and doing them afterwards means finding the same files in two different directories.

### 5.1 `TweakRegistry.Register` has zero call sites → the whole drift/Verify subsystem is dead 🔴 **High**

**Independently re-verified:** `TweakRegistry.Register` — **0 matches** across all `src/` and
`tests/`. `_entries` (`private static readonly List<(TweakDefinition, Action)> _entries`)
is therefore permanently empty. Consequences, all confirmed by reading the consumers:

- `TweakRegistry.Count == 0` → `TryGetDefinition(id, …)` always `false`.
- `DriftScanner.Scan()` increments `orphaned` and `continue`s for every baseline entry → **every
  drift check reports 100 % orphaned**. The title-bar drift banner
  (`MainWindow.RunDriftCheck`) and `HomeViewModel.DriftedCount` therefore read zero.
- `DriftBaseline.Record` is called only from `TweakHelpers.ApplyToggle`/`ApplyOption`, whose only
  live callers are `VerifyViewModel.ReapplyRows` (which itself fails every row at `:159`) and
  `TweakRegistry.ImportFromFile` (which matches against the empty list). **The baseline is never
  written**, so `VerifyPage` shows its "Nothing tracked yet" empty state **permanently**
  (`Views/VerifyPage.xaml:136`).
- The `⚠ SINGLETON, not transient` comments at `DI/UIServiceExtensions.cs:74-79` still justify a
  DI lifetime by a registration side-effect that no longer happens.

**Root cause:** the `SettingDefinition` migration replaced `TweakDefinition` + `TweakRegistry` as
the source of truth and nobody rewired the drift path.

**Why it is dangerous:** it is **silent**. No crash, no log entry, no red badge — a false
"everything is fine". Worse than an error.

**Fix (clean-room, Akari's own design):** port drift detection onto the declarative model.
`ISettingStateReader` already answers "what is this setting's current value", so the baseline
should record `(settingId, value, osBuild)` from `SettingOperationExecutor` and the scanner
should read back through `ISettingStateReader` instead of a `TweakDefinition` lookup. Then
delete `TweakRegistry`, `TweakHelpers.*`, `TweakTargets`, and the singleton-lifetime comments.
**Effort M. Do it first — it unblocks 5.2 and 5.3.**

### 5.2 `SettingCatalogValidator` is never called → the setting-ID uniqueness invariant is unenforced 🟠 **Medium-high**

**Re-verified:** `SettingCatalogValidator` appears in `SettingDefinition.cs:36` (a doc comment),
its own definition file, and `SettingCatalogValidatorTests.cs` × 28. **Zero production call
sites.** There is no CI in the repo (no `.github/`, no pipeline), so nothing else runs it either.

**Why it matters more than "Medium":** setting `Id` is the **backup-file key**. A duplicate ID
silently breaks profile import with no compile error and no test failure. The validator exists
to prevent exactly that and is not run. Meanwhile `SettingBackupService` still *references*
`TweakRegistry` types, so this invariant and the dead registry are entangled.

**Fix:** add a test that enumerates **all 11 `Build()` methods** (statically enumerable — they
are `public static`) and validates every `SettingGroup`, then wire the same call into
`SettingPageWarmUp`. Assert on the **real catalogs**, not hand-built fixtures (the current test
does the latter — so the validator is tested and the data it guards is not). **Effort S, and it
must land in Phase 0** because it is the safety net for every later phase.

### 5.3 The modes system is a pair of orphan event contracts 🔴 **High — new finding**

`Core/Features/Common/Events/BuilderModeExitedEvent.cs` and `ReviewModeExitedEvent.cs` exist.
A codebase-wide grep for `IApplicationModeService`, `BuilderEdit`, `ConfigReviewService`,
`CurrentMode`, `ModeChanged`, `RecordBuilderEdit` returns **only two incidental hits** (one each
in `ExternalAppsViewModel.cs` and `WindowsAppsViewModel.cs`, unrelated). **No mode enum, no mode
service, no producer for either event.** This is the residue of a modes port that shipped its
Core contracts and never shipped its engine — the same failure shape as 5.1, one layer over.

**Consequence for the plan:** the events are dead code *today*, but §3.3's mode work (M1–M7) is
what makes them live. **Do not delete them — they are the correct seam.** Flag them so nobody
"cleans them up" between now and the modes phase. If the modes phase slips past the restructure,
they become deletion candidates.

### 5.4 Dead code to delete 🟡 **Medium**

| Item | Size | Status |
|---|---|---|
| `Core/Features/Update/Catalogs/UpdateTweaks.cs` | **329 lines**, 11 definitions | **Zero call sites** (mapper-verified; the only mentions are 4 comments in `UpdateOptimizations.cs` and 1 in `UpdateViewModel.cs` — the latter says outright "rather than the delegate-based UpdateTweaks", i.e. the replacement shipped and the original was never deleted). It contains 19 inline `Registry.SetValue` calls, sits in `Catalogs/` **next to the live `UpdateOptimizations.cs`**, and its comments call it "the ground truth". **A developer adding an Update setting will read the wrong file.** It is also the last thing pinning the dead `Core.Tweaks` namespace alive. **Delete** — move any still-valuable inverted-detection knowledge into `UpdateOptimizations.cs` as a comment first |
| `Core/Tweaks/TweakDefinition.cs` | ~10 KB | Survives only in dead code + 14 `///` **comment** references |
| `Core/Tweaks/TweakTargets.cs` | 4.4 KB | **0 call sites** (`TryGetRecommendedTarget`, `IsMismatched`, `CollectPending` all unreferenced) |
| `Infra/Services/*Wrapper.cs` × 4 | 12 lines each | `IToolFetchService`→`ToolFetchServiceWrapper`→static `ToolFetchService`, same for `IUpdateService`, `ISystemInfoService`, `IShaderCacheService`. `IToolService` exists "solely to be passed one object". Register the real implementations; delete the wrappers |
| `Infra/Services/ExplorerRestart.cs` | small | Duplicate of the existing `ProcessRestartManager` |
| `Infra/Services/RestorePointHelper.cs` | 15 refs | Duplicate of `SystemRestoreService` |
| **Two `DefenderService` classes** | App 329 lines (19 registry calls) + Infra static | Consolidate to one |
| `App/Resource/` vs `App/Assets/` | ~3.6 MB | `Assets/` is the only half declared in the csproj; `NavIcons/` is referenced by neither csproj nor code — **unresolved whether it is used at runtime** |
| `Core` → `Microsoft.WindowsAppSDK 2.3.1` + `CommunityToolkit.Mvvm 8.4.2` | 2 `PackageReference` lines | **Zero usage** of `Microsoft.UI`/`WinUI`/`ObservableProperty`/`[RelayCommand]` anywhere in Core. On the layer whose stated purpose is to be dependency-free. Remove and build |
| `views/WinUI.Framework` not in the sln | — | 67 call sites, invisible to tooling (N16) |

### 5.5 Duplicated `DispatcherService` 🟠 **Medium**

`Infra/Features/Common/Services/DispatcherService.cs` and
`App/Features/Common/Services/DispatcherService.cs` are **byte-identical except the `namespace`
line**. Both are registered for `IDispatcherService`; `AddAkariInfrastructure()` runs before
`AddAkariUI()`, so with last-registration-wins the **App** copy is the one injected and the
Infrastructure copy is dead — and its `using Microsoft.UI.Dispatching;` is the *only*
UI-framework dependency in the entire Infrastructure project.

**The layering is inverted by accident of ordering, and the file that breaks the rule is the one
that looks authoritative.** Fix: delete the Infrastructure copy and its registration (2 lines).
The interface is already in Core, so the one UI-thread implementation belongs in the App layer.

### 5.6 `AkariPaths` declared twice in namespace `AkariTool.Tabs`, two assemblies 🟠 **Medium**

`Core/Features/Common/Constants/AkariPaths.cs:9` and `App/Features/Software/SoftwareAppService.cs:21`,
byte-identical members (`ScriptsDirectory`, `ScriptsDirectoryLiteral`, `LogsDirectoryLiteral`,
`PowerShellExePath`). Both live in `namespace AkariTool.Tabs` but in different assemblies, so
neither shadows the other: a file with `using AkariTool.Tabs;` referencing both assemblies gets
**CS0433**, and a file with neither `using` picks one arbitrarily. **Both are live** —
`BloatRemovalScriptGenerator.cs:111` uses the Core copy, `SoftwareAppService` uses all four
members of the App copy at `:73,349,350,385,404,455,465,532,588`.

**Dangerous rather than untidy:** `ScriptsDirectory` is a `static readonly` computed from
`Environment.GetFolderPath` at type-init time. If one copy is edited and not the other,
generated scripts reference a different path than the runtime — and there is no test. Fix: keep
the Core copy, delete the App one, add the `using` (`SoftwareAppService` already has it). **Do
this first**, before any namespace rename — it is 2 lines and it removes a CS0433 landmine from
the 76-file rename.

### 5.7 Test coverage cannot validate a restructure 🔴 **High as a planning risk**

20 test files / 2 projects. **No test project for `AkariTool.App`** — no `.csproj` references it,
so every ViewModel, page, `SettingBackupService`, `SettingPageWarmUp` and all 11 catalogs are
unverified by automation. Largely a consequence of `ServiceLocator` (48 sites) making the VMs
unconstructable without a live global container. **No test asserts the real catalogs are valid**
(5.2). **No test for `IEventBus`** despite the hand-rolled pub/sub with subscription tokens and
a `PowerPlanChangedEvent` that gates sibling-row refreshes.

**A restructure of ~76 renamed files + ~200 moved files + a deleted namespace cannot be validated
by this suite.** Three additions, **all before the restructure** (Akari's own CONCERNS 16 says
the same): (1) catalog-validity test enumerating all 11 `Build()` methods, (2) a
`SettingPageWarmUp` integration test against a hand-built `ServiceCollection`, (3) a ViewModel
test project.

### 5.8 Mapper caveat — **carried forward, applies to this document too**

The codebase mapper **did not run `dotnet build`**, and neither did this analysis. Every finding
below is code-reading only:

- **Whether the app currently builds.** `AkariPaths` CS0433, the 16-space stray indentation block
  at `UI/UIServiceExtensions.cs:62-69`, and the `DispatcherService` last-wins resolution order are
  all *inferences*. The compiler may already be failing on one, or may be fine because of
  `using` placement.
- **Whether the duplicate `DispatcherService` registrations really resolve to the App copy**
  (last-wins + `AddAkariUI()` called second — documented MS DI behaviour, but unrun).
- **Whether `Resource/NavIcons/` is consumed at runtime.**
- **Whether `SettingCatalogValidator` was ever invoked by a build script or CI** (no CI exists in
  the repo).
- **Whether the 47 embedded `.ps1` payloads behave correctly.** Behaviour cannot be assessed
  statically; `Nvidia/Settings.nip` deleting itself out of the build under
  `Condition="Exists(...)"` is the concrete hazard.

**A baseline `dotnet build` is a hard prerequisite for Phase 0. Do not plan the restructure
without it.**

---

## 6. Sequencing recommendation

### 6.1 Shape

Six stages. Stages 0–3 are the restructure; 4–5 are features. **The user's "restructure first,
then features" ordering is correct and this analysis strengthens it** — with one amendment:
*localization plumbing* belongs at the front of the feature stage, not the back (§6.4).

```
0  Baseline & safety net          ── no moving parts; makes everything after verifiable
1  Bug fixes while the map is known ── behaviour fixes, cheap today, expensive later
2  Namespace normalization          ── ~76 files, 3 commits, one per assembly, compiler-verified
3  Vertical slices + DI            ── the big move; then kill ServiceLocator
4  High-value feature work          ── modes, AdvancedTools split, SoftwareApps machinery
5  Long tail                        ── localization content, shell polish, catalog splits
```

### 6.2 Stage detail

**Stage 0 — Baseline and safety net.** *Effort S.* No file moves.
`dotnet build` on the solution + the `/p:DeElevatedTest=true` variant to establish a green (or
known-red) baseline. Add `vendor/WinUI.Framework` to the sln. Delete Core's two dead package
references. Add the **catalog-validity test over all 11 real `Build()` methods** (§5.2). Add the
`SettingPageWarmUp` integration test. **Rationale:** everything downstream is validated by "does it
still build + do the 11 catalogs still validate", and today neither answer is available.

**Stage 1 — Fix the bugs while the map is still known.** *Effort M.* In this order:
1. `AkariPaths` duplicate (§5.6) — 2 lines, removes a CS0433 landmine from the Stage-2 rename.
2. Duplicate `DispatcherService` (§5.5) — 2 lines, removes a UI dependency from Infrastructure.
3. Drift/Verify onto the declarative model (§5.1) — **M**, and it deletes `TweakRegistry`,
   `TweakHelpers.*`, `TweakTargets`, and the bogus singleton-lifetime comments. This is a
   *behaviour* fix, not a move; doing it after the restructure means re-finding the same files
   under new paths.
4. Delete `UpdateTweaks.cs` (329 lines) after salvaging its comments (§5.4).
5. Three duplicate registration pairs + the 16-space indentation block in the two DI files.

**Rationale for doing these first, not "during":** each is 1–5 files, each is verified by the
Stage-0 safety net, and each removes ambiguity that would otherwise be carried through ~300 file
moves. Deferring them converts a 2-line deletion into a hunt.

**Stage 2 — Namespace normalization.** *Effort M; ~76 files; the cheap warm-up for Stage 3.*
Three commits, in dependency order, each independently verifiable because Core compiles alone:
1. **Core**: `AkariTool.Tabs{,.Customize,.Gaming,.Notifications,.Power,.Privacy,.Sound,.Update}`
   (37 catalog files) → `AkariTool.Core.Features.<Domain>`; `AkariTool.Tabs` (`AkariPaths`) →
   `AkariTool.Core.Features.Common.Constants`. **38 files, 1 commit, build-verified.**
2. **Infrastructure**: `AkariTool.Tabs` (18) + `AkariTool.Services` (19) →
   `AkariTool.Infrastructure.Features.<Domain>.Services`. **37 files, 1 commit.**
3. **App**: `AkariTool.Tabs` (2) → correct `AkariTool.*` namespaces. **2 files, 1 commit.**

Then move the **XAML** surface: 37 `.xaml` + code-behind pairs, atomically per page, plus
`MainWindow.xaml.cs`'s `PageMap` and 4 `*DetailTags` sets. **This is the highest-risk single
step in the whole plan** — treat it as its own stage or its own commit series, never bundled.

**Rationale:** namespace-before-move means every later failure has one cause. Namespace-after-
move means every failure is ambiguous between "wrong folder" and "wrong namespace".

**Stage 3 — Vertical slices, then DI.** *Effort L; the bulk of the work.*

3a. **Decide D1 (AkariOS domain), D3 (Desktop↔Explorer), D4 (SoftwareApps pages), D5 (Home/
Backup/Tools surfaces), D6 (config schema), D7 (preferences store), D8 (scripts), D9 (vendored
framework).** D1 and D6 block the most work. **Rationale:** these are the forks where two answers
mean redoing a whole stage.

3b. **Create the missing layer folders** for all 5(+1) domains × 3 layers, per §2.2. Move Core
catalogs first (data only, zero behaviour), then Infrastructure, then App.
**Rationale:** Core-first means each move is verifiable by the Stage-0 catalog test, because the
catalogs keep producing valid settings while their files change location.

3c. **Composition root**: create `CompositionRoot` + `SettingServicesExtensions` +
`UIServicesExtensions` under `Features/Common/Extensions/DI/`, move `Infrastructure/DI/` →
`Infrastructure/Extensions/DI/`, add `InfrastructureContainerSmokeTests`. **Do not touch
`CompatibleSettingsRegistry`** (§2.4 decision 3). Rationale: the composition root must exist
*before* the DI removals, and the container smoke test is what keeps the
`TryAddSingleton`-ordering trick from being "cleaned up".

3d. **Kill `ServiceLocator`**: 48 sites / 27 files; add the 5 resolved-at-call-time dependencies to
the `SettingPageViewModel` constructor and pass them through `CreateItem`, exactly as
`PowerViewModel.CreateItem` already does correctly. Rationale: after this, ViewModels are
constructable in tests, which is the precondition for Stage 5's ViewModel work and for N15.

3e. **Extract the shell**: `Helpers/NavigationRouter.cs` out of `MainWindow.xaml.cs`, plus
`MainWindowViewModel` + the coordinator helpers. Rationale: `MainWindow` is the file every
later UI change touches; shrink it before it becomes the bottleneck.

**Stage 4 — High-value feature work.** *Effort L.* In this order, because each unlocks the next:
1. **Localization plumbing** (N1 service + key derivation + `en.json` + guardrail tests) —
   see §6.4. **This is the top of Stage 4, not the bottom.**
2. **AdvancedTools split** (A1–A9) — it is mostly a move, the capability already exists, and it
   unlocks `ISelectedAppsProvider` for the autounattend generator.
3. **Modes** (M1–M7) — needs the final catalog shape and a settled config schema (D6), which is
   why it follows the AdvancedTools split but precedes heavy new-catalog work.
4. **SoftwareApps machinery** (S1–S4, S7–S9, S12, S15) — contracts first, then the
   `SoftwareAppService` split, then the fallback chain including **Chocolatey** (S4).
5. **Customize services** (C1, C2) + content delta (C5).
6. **`Common/Controls` + DataGrid** (N4, S10) — gated on the Stage-0 stack spike.

**Stage 5 — Long tail.** Winhance's 29-locale content, `UiZoomManager`/`PageScrollHelper`/
`WindowSizeManager` (N7), title-bar chrome (N12), single-instance + update UX (N11, N13),
`SettingItemViewModel` split (N18 — **only with a characterisation test in hand**), the 4 tuning
catalogs' partial split (O8), `Resource/`/`Assets/` de-dup (N17).

### 6.3 Critical path and parallelism

**Critical path (strictly serial):**

```
dotnet build baseline
  → catalog-validity test green
  → AkariPaths + DispatcherService deletions
  → drift/Verify fix
  → Core namespace commit
  → Infra namespace commit
  → XAML + PageMap move
  → D1 decision
  → Core catalogs → Infra services → App pages (per domain)
  → composition root + container smoke test
  → ServiceLocator removal
```

Everything on that list is serial because each step either changes what the next step's compiler
can see, or must be verified by the previous step's test.

**Genuinely parallelisable:**

| Parallel track | When it unblocks | Constraint |
|---|---|---|
| Per-domain moves (Optimize / Customize / SoftwareApps / AdvancedTools / AkariOS) as separate commits | after the namespace commits | each domain must be a **self-contained commit**; the shared files (`Common`, `MainWindow`, `PageMap`, DI) are the serialization point |
| **Adding new Winhance settings to an existing catalog** | **immediately after Stage 2** | a new setting is a pure data addition — it survives file moves. **Catalog content work does not have to wait for the restructure to finish.** This is the biggest available parallelism win |
| Locale content translation | after N1 plumbing | per-locale, per-feature |
| AdvancedTools split ‖ SoftwareApps consolidation | after Stage 3c | disjoint file sets |
| O8 catalog partial split ‖ N18 `SettingItemViewModel` split | Stage 5 | pure file moves; O8 is zero-risk, N18 needs a characterisation test |

### 6.4 Risky-at-the-end items — deferring these makes them much harder

**R1 — Localization. Highest risk of deferral in the entire plan.** Retrofitting 29 locales
across ~420 settings after several hundred more settings and a dozen more pages have landed is
dramatically more expensive than doing the *plumbing* first. Winhance's key names are **derived**
from `SettingDefinition` (`Setting_{LocalizationId ?? Id}_Name`, `_Description`, `_Option_{n}`,
`SettingGroup_{compacted}`), which means the key surface grows automatically with the catalog —
so every setting added after localization ships un-localized and needs a second pass. Worse,
`LocalizationKeyReferenceTests` only fails for keys that *are* requested, so an un-localized
setting is invisible rather than red.
**Recommendation:** N1's plumbing (service, key derivation, `en.json` seed, the 2 guardrail tests,
live re-render, RTL) lands at the **top of Stage 4**, before any bulk new-catalog or new-page work.
Content translation is Stage 5 and parallelisable per locale.

**R2 — The config-format fork (D6).** Akari's `SettingBackupService` writes its **own** flat JSON
derived from `SettingPageViewModel`s. Winhance's modes + autounattend generator are all built on
`UnifiedConfigurationFile` v2.0 with a nested `{WindowsApps, ExternalApps, Customize, Optimize}`
shape and `IConfigMigrationService`. Akari's setting IDs are largely **shared with Winhance's**
(both were ported from the same lineage), which means a user could plausibly try to import a
`.winhance` file into Akari and get a **partial, silently wrong** import rather than an error.
**Decision needed before Stage 3**: does Akari adopt `UnifiedConfigurationFile` (→ `.winhance`
import compatibility, but a rewrite of `SettingBackupService` and a format migration for existing
Akari users), or does it deliberately fork with a distinct extension and an explicit
"not a `.winhance` file" rejection? **This must be decided before more format-specific code is
written**, because retrofitting a schema is far more expensive than choosing one.

**R3 — OTS elevation detection.** Retrofitting this after more per-user (HKCU) settings land means
finding every affected write site. It should land with the Customize/Power content work, not after.

**R4 — `ElevationService` thread-affinity.** Not a sequencing item so much as a *vigilance* item:
any Stage-3/4 change that makes an impersonated path async silently breaks elevated writes.
Audit at every touch (§4.1).

**R5 — The `ServiceLocator` removal.** If it slips past Stage 3, every new feature written in the
meantime acquires the pattern, and N15 (an App test project) stays blocked. This is why 3d sits
inside Stage 3 rather than Stage 4.

**R6 — The `AkariPaths`-style static path constants.** `static readonly` computed at type-init in
two assemblies. If localization, WIM, or the autounattend generator add new path consumers before
this is consolidated, a third copy appears.

### 6.5 Things Winhance itself flags as uncertain — do not treat as settled

| Item | Winhance's own caveat |
|---|---|
| **Builder-mode edit recording** | **Numerically incomplete, by their own documentation.** `BuilderEdit` records Toggle/CheckBox/Action `IsSelected`, Selection `SelectedIndex`, and Selection-Custom `CustomStateValues` — but **not** `NumericRange` edits and **not** AC/DC power edits; those fall back to the system-seeded value. Replicate the architecture; **do not replicate the gap silently**. Either implement the missing recording or surface the limitation in the Builder UI. Flag for explicit user decision |
| **WinGet COM under self-contained** | `WindowsPackageManagerElevatedFactory` (via `winrtact.dll`) **hangs** in self-contained mode — microsoft/winget-cli#4377. Winhance is forced onto `WindowsPackageManagerStandardFactory` with `allowLowerTrustRegistration: true`. Verify Akari's `WinGetComSession` made the same choice |
| **`Microsoft.WindowsAppSDK` version** | Winhance pins **1.8.260416003** and warns the metapackage is mandatory (transitive dep of Material.Icons / CommunityToolkit.WinUI / FluentIcons) and that switching to the `.WinUI` component package causes MSB4011 + MSIX `CustomBeforeMicrosoftCommonTargets`. **Akari is on 2.3.1** — ahead. There is no documented "2.3.1 + CommunityToolkit.WinUI 8.2.x" compatibility statement. **Spike required** (§7.2) |
| **Shared bases in a domain folder** | Winhance's own anti-pattern. Do not replicate (§4.3) |
| **Single-file catalogs** | Winhance's own anti-pattern, despite its own 4,151-line file. Replicate the *advice* (partial split), not the file |
| **Overwrite-by-later-registration** | Winhance's own anti-pattern that it nevertheless instructs replicators to keep — with the comment and the container smoke test. Do both |
| **`IAutounattendXmlGeneratorService` in the UI layer** | Winhance's own anti-pattern (interface in Core, implementation in UI). Its stated preferred fix is to move the interface to `Common/Interfaces` or lift the dependency. Akari: **declare it in `Core/Features/AdvancedTools/Interfaces/` as Winhance does, then implement in Infrastructure** and lift the selected-apps dependency behind an interface — do not repeat the placement problem |
| **`OptimizePage.xaml.cs` 1,118 lines, `MainWindow.xaml.cs` ~900** | Winhance's own worst instances, explicitly not to be copied. Replicate the 34-line thin sub-page |
| **`OptimizePage.xaml.cs` 1,118 lines vs Akari's `AkariOSPage` 2,631 / 9 partials** | Akari's partial split is the better pattern; keep it. Winhance's remark that coordinators ended up in two different homes (`UI/Helpers/StartupUiCoordinator` vs `UI/Features/Common/Services/StartupOrchestrator`) — pick one home in Akari |

---

## 7. Confidence and gaps

### 7.1 Confidence by section

| Section | Confidence | Basis |
|---|---|---|
| §1 Verdict table | **High** | Directory/file/line counts re-verified directly in both repos |
| §2 Structural gap | **High** | Full `src/` trees of both repos read; the mapper's file counts and namespace distribution independently re-verified |
| §3 Feature gap | **Medium-High** | Capability presence/absence verified by grep + file read. Effort estimates are judgement. **Setting counts are measured, not estimated** — that is what makes this section trustworthy |
| §4 Convention deltas | **High** for elevation/pickers/badges/toggle-state/catalog-per-file (each verified by reading the actual source); **Medium** for the stack row (no restore performed) |
| §5 Bugs & dead code | **High** for §5.1–5.3, 5.5, 5.6 (independently re-verified by grep in this session); **Medium** for §5.4's mapper-derived rows; **High** that §5.8's build caveat applies to this document too |
| §6 Sequencing | **Medium** — opinionated, grounded in dependency order and in the measured counts, but no plan has been attempted |

**Overall: MEDIUM-HIGH on structure and feature inventory; MEDIUM on effort sizing and on
anything that would require running the build.**

### 7.2 What could not be determined from the two codebases alone

| # | Gap | How to close it | Blocks |
|---|---|---|---|
| G1 | **Whether Akari currently builds.** No build was run (mapper) and none was run here. §5.8 lists five specific code-reading inferences that could be invalidated by one compile. | `dotnet build` on the solution + `/p:DeElevatedTest=true` | **Phase 0, everything** |
| G2 | **Does `CommunityToolkit.WinUI` 8.2.x (SettingsControls / Primitives / DataGrid 7.1.2) resolve and render on `Microsoft.WindowsAppSDK 2.3.1`?** Akari is a full major version ahead of Winhance and has never used the toolkit. This gates `SettingsCardItem`, `UniformWrapPanel`, `DataGrid` table view — i.e. most of the UX-parity work. | A throwaway spike: one project, the packages, one `SettingsCard` + one `DataGrid` + one `WrapPanel`, self-contained x64 build | **N4, S10, and the whole "rich setting-card UX" column** |
| G3 | **`Resource/NavIcons/` — consumed or dead?** 19 PNGs, no csproj reference, no code reference; `MainWindow.xaml` must be setting glyphs some other way, or the icons are dead too. | Trace every `MainWindow.xaml` glyph binding, or run the app | N17 |
| G4 | **Does the duplicated `DispatcherService` registration really resolve to the App copy?** Documented last-wins + `AddAkariUI()` second, but unrun. | Assert `IDispatcherService`'s concrete type in the container smoke test | 5.5 verification |
| G5 | **Per-catalog `SettingId` diff between Akari and Winhance.** I measured *counts* (which show near-parity) but not *identity*. Since Akari's IDs came from the same lineage, they may largely match — which makes the D6 `.winhance`-import question sharper than this document can answer. | A throwaway script extracting every `Id =` from all catalogs, both repos, diffed | **D6** |
| G6 | **Behaviour of the 47 embedded `.ps1` payloads** and whether `Nvidia/Settings.nip`'s `Condition="Exists(...)"` has ever silently dropped the feature. | Runtime test; make the guard a hard requirement | A9, AkariOS |
| G7 | **Are there pre-migration DriftBaseline entries on real user machines?** A build from before the `SettingDefinition` migration may have written baselines in the old shape. That would change the §5.1 fix from "start recording" to "migrate or discard". | Inspect a real `%ProgramData%` install, or ask the user | 5.1 |
| G8 | **Whether `SetLocalized`-style content is required for parity or optional.** FEATURES §8 item 9 calls 29 locales "table stakes for parity, not optional polish", but 29 locales is a large content investment. | **User decision** | N1 sizing |
| G9 | **CommunityToolkit.Mvvm version skew:** Winhance uses 8.4.0 in UI and 8.2.2 in Core/Infra; Akari uses 8.4.2 in App and (dead) 8.4.2 in Core. Whether `LangVersion=preview` is still required on .NET 10 / WinAppSDK 2.3.1. | Build spike | trivial once G2 runs |

### 7.3 Decisions needed from the user

| ID | Decision | Why it blocks | Recommendation |
|---|---|---|---|
| **D1** | AkariOS as a 6th mirrored domain, or folded into Optimize/AdvancedTools | 35 Infra files + 2,631 lines of page; blocks Stage 3b | **(A) 6th domain `AkariOS` mirrored across all 3 layers.** Option (B) cuts one cohesive feature into three nav destinations |
| **D6** | Adopt `UnifiedConfigurationFile` v2.0 / `.winhance` interop, or deliberately fork? | Blocks modes, autounattend, and any further format code | Fork deliberately with a distinct extension + explicit rejection of `.winhance`, **unless** cross-tool import is a product goal. Decide **before** Stage 4 |
| **D3** | Akari's standalone `Desktop` section: merge into Winhance's Explorer `Desktop` group, or keep as a 5th Customize section? | 12 settings; low cost either way | Merge into Explorer — it is Winhance's shape and the settings already claim to be Explorer-family |
| **D4** | Akari's 4 SoftwareApps pages → Winhance's 1 page + 2 tabs, or keep the page split? | 4 page moves | Consolidate to 1 page + 2 tabs, absorb `DebloatPage` into the removal flow |
| **D5** | Where do Home / Backup / Verify / Tools live? Winhance has none of them | 4 surfaces | `Verify` → `Features/Common`; `Tools` → `Features/AdvancedTools`; `Backup` → engine in `Features/Common/Services`, surface as an Akari-only UI-only slice beside `Settings` (Winhance's `Settings` proves a UI-only slice is legal); `Home` → its own Akari-only UI-only slice, and **keep it as the landing page** (Akari value) |
| **D7** | `UiPreferences` (registry-backed section collapse) vs Winhance's JSON `UserPreferencesService` (language/theme/display toggles)? | N12 + N1 need a single preference store | Converge on **one JSON store** with keys from `UserPreferenceKeys`; migrate the section-collapse state into it. Localization depends on this |
| **D8** | Embedded `.ps1` payloads vs Winhance's generate-to-`%ProgramData%` scripts? | N12 "Scripts folder" menu item; Akari has no such folder | Keep embedded (47 hand-authored scripts are real Akari value), **and** add a Scripts-folder menu item that exposes a copy of them so the UX is not lost |
| **D9** | `vendor/WinUI.Framework`: keep for non-DI types, or fold into `Features/Common` and delete? | Affects every ViewModel base class | **Keep now**, delete `IoC` usage only. Full removal is a later, separately-budgeted phase |
| **D10** | Replicate Winhance's Builder-mode `NumericRange`/AC-DC recording gap, or fix it? | M4 | **Fix it.** Do not ship a mode that silently drops numeric edits |
| **D11** | Landing page: keep Home (Winhance lands on `SoftwareApps`)? | Navigation | Keep Home; document as Akari's one intentional navigation divergence |

---

*Parity gap analysis: 2026-10-05. Sources: `.planning/codebase/{ARCHITECTURE,STRUCTURE,CONVENTIONS,CONCERNS}.md` and `.planning/reference/winhance/{ARCHITECTURE,STRUCTURE,STACK,FEATURES,CONVENTIONS}.md`, plus direct grep/read verification of both repositories. No build, restore, or test run was performed. Winhance was read only; no file in either repository was modified.*

