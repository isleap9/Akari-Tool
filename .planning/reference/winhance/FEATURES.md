# Winhance — User-Visible Feature Inventory

**Analysis Date:** 2026-10-05
**Reference repo:** `C:/Users/isleap/Documents/GitHub/Winhance` (read-only reference material)
**Purpose:** Exhaustive inventory of what Winhance *has*, so Akari Tool's rework can be defined by the gap.

---

## 0. Shell, Navigation & Cross-Cutting UX

### 0.1 Window / shell

- Single window: `src/Winhance.UI/MainWindow.xaml(.cs)`, `ExtendsContentIntoTitleBar=true`, `TitleBarHeightOption.Tall`.
- Backdrop: Mica on Windows 11, falls back to `DesktopAcrylic` on Windows 10 (`MainWindow.TrySetMicaBackdrop`).
- App-local **UI zoom**: Ctrl `+`/`-`/`0` and Ctrl+MouseWheel (`src/Winhance.UI/Features/Common/Utilities/UiZoomManager.cs`); wrapped in a `ZoomViewport`/`ZoomHost` grid so the whole window scales.
- Window size/position persistence (`Helpers/WindowSizeManager.cs`).
- App icon swaps light/dark variant on theme change (`MainWindowViewModel.UpdateAppIconForTheme`).
- RTL `FlowDirection` applied from `ILocalizationService.IsRightToLeft`.
- **Beta banner** text in the title bar: `"Release-Preview: Version is for Testing Only"`.

### 0.2 Title bar contents (left → right)

1. Pane-toggle button (collapse/expand nav)
2. App icon + `App_Title` + `App_By` ("by Memory")
3. Optional Beta banner
4. **Winhance Mode switcher** — 3 toggle buttons: `Normal` / `Builder` / `Config Review` (see §6)
5. **Windows Version Filter** button — toggles "show only settings compatible with this Windows build" vs "show all, marking incompatible ones" (`IWindowsVersionFilterService`)
6. **Donate** button (in-app sponsors dialog)
7. **Report a Bug** button → GitHub issues
8. **Docs** button → `https://winhance.net/docs/index.html`

### 0.3 Navigation

**Left sidebar (`Features/Common/Controls/NavSidebar.xaml`), 5 destinations + a More menu.** There is **no home page / dashboard**. Default landing page is **Software & Apps** (`src/Winhance.UI/Helpers/NavigationRouter.cs` — `TagToPageType`).

| Order | Tag | Page | Nav text key |
|---|---|---|---|
| 1 | `SoftwareApps` | `SoftwareAppsPage` | `Nav_SoftwareAndApps` |
| 2 | `Optimize` | `OptimizePage` | `Nav_Optimize` |
| 3 | `Customize` | `CustomizePage` | `Nav_Customize` |
| 4 | `AdvancedTools` | `AdvancedToolsPage` | `Nav_AdvancedTools` |
| 5 | `Settings` | `SettingsPage` | `Nav_Settings` |
| 6 | `More` | (flyout, not a page) | `Nav_More` |

**More flyout items** (`Features/Common/ViewModels/MoreMenuViewModel.cs`):
- Documentation (web)
- Report a Bug (web)
- **Check for Updates** (GitHub Releases API; downloads `Winhance.Installer.exe`; shows an in-window update InfoBar with Install Now / Relaunch)
- **Winhance Logs** (opens `C:\ProgramData\Winhance\Logs` in Explorer)
- **Change History** (opens `ChangeHistory.txt` — a running user-facing receipt of every app install/remove)
- **Winhance Scripts** (opens `C:\ProgramData\Winhance\Scripts` — the generated PowerShell removal scripts)
- **Support Winhance** (sponsors dialog with live-or-bundled sponsor data + "don't show again")
- **Close Winhance**

### 0.4 Content-frame pages show a standard layout

`Header (64px icon + title + description)` → `Nav row (breadcrumb | quick actions | view toggle)` → `Content`.
Every settings page also has an `AutoSuggestBox` search that (a) filters rows live and (b) offers cross-setting suggestions (`SearchSuggestionItem` = setting name + section display name); picking one jumps to the owning page with the search pre-filled.

### 0.5 Task progress

`Features/Common/Controls/TaskProgressControl.xaml` — three stacked progress bars in the bottom-right (up to 3 concurrent tasks), each with percentage, status text, terminal-output preview, and a cancel button. Backed by `ITaskProgressService` / `IMultiScriptProgressService` (`TaskProgressService`), which exposes `StartTask`, `UpdateProgress`, `CreateDetailedProgress()`, `GetCurrentCancellationToken()`, `CompleteTask()`. Output can also be opened in a scrollable dialog (`IDialogService.ShowTaskOutputDialogAsync`).

### 0.6 Setting card anatomy (what every setting row can show)

Renders through `Features/Common/Resources/SettingTemplates.xaml` (`SettingItemTemplate`, `SettingExpanderItemTemplate`) into `Features/Common/Controls/SettingsCardItem.xaml`:

- Icon (Material Design or Fluent, chosen by `SettingDefinition.IconPack`), Name, Description
- **Control by `InputType`**: `Toggle` (Switch), `Selection` (ComboBox, incl. an AC/DC "Dual" variant for power), `NumericRange` (NumberBox, incl. AC/DC Dual), `Action` (a run button), `CheckBox`
- **Badge pill row** (`SettingDescriptionWithBadges`, wrapped in a `WrapPanel` so AC/DC pills wrap on narrow windows) — kinds from `SettingBadgeKind`: **Recommended**, **Default**, **Custom**, **Preference**
- **Quick-set buttons** — per-card `Set to Recommended (X)` / `Set to Default (X)` buttons and tooltips, resolved from `RegistrySetting.RecommendedValue/DefaultValue`, `ComboBoxOption.IsRecommended/IsDefault`, or `PowerCfgSetting.RecommendedValueAC/DC` / `DefaultValueAC/DC`. Tooltips show *display* units, not raw system units (`UnitConversionHelper`).
- **Technical Details expander** (per-setting, toggled globally from Quick Actions) — a `TechnicalDetailSection`/`TechnicalDetailRow` list showing exactly which registry keys/values, scheduled tasks, powercfg GUIDs, or scripts the setting touches, plus an **"Open in Registry Editor"** button (`IRegeditLauncher`, jumps regedit to the key and pre-caches the regedit icon)
- **Status banner** (`SettingStatusBannerManager` — InfoBar: success / warning / error with message)
- **NEW badge** — data-driven from `SettingDefinition.AddedInVersion` vs the last-installed version (`INewBadgeService`)
- Parent/child dimming via `ParentSettingId`, and cross-setting dependency messages via `SettingDependency`
- Accessibility names are composed as `"<Setting name>: <action>"` and `"<Setting name> (Plugged In|On Battery): <action>"` via `x:Bind` function-call syntax

### 0.7 Quick Actions menu (Optimize + Customize pages)

`QuickActionsButton` flyout on both `OptimizePage` and `CustomizePage`:
- **Apply Recommended** (bulk) — `BulkActionType.ApplyRecommended`
- **Reset to Defaults** (bulk) — `BulkActionType.ResetToDefaults`
- separator
- **View Technical Details** (global toggle for the per-card expander)
- **View Info Badges** (global toggle for badge pills + quick-set buttons)
- **View New Badges** (global toggle for the NEW pill)
- *(Config Review mode only)* **Show Only Changes** (`ReviewModeFilter`, backed by `IConfigReviewDiffService`)

### 0.8 Global startup sequence

`StartupOrchestrator.RunStartupSequenceAsync` (4 phases, each individually try/caught so one failure never blocks startup):
1. **Settings registry** — `ICompatibleSettingsRegistry.InitializeAsync()` + `IGlobalSettingsPreloader.PreloadAllSettingsAsync()`; then NEW-badge init; tooltip event handler construction; regedit icon pre-cache.
2. **First-run backup config** — `IConfigurationService.CreateUserBackupConfigAsync()`, guarded by user preference `InitialConfigBackupCompleted`, 30 s timeout, retried next launch if it times out. Result sets `StartupResult.IsFirstLaunch`.
3. **Script migration** — `IScriptMigrationService.MigrateFromOldPathsAsync()`.
4. **Script updates** — `IRemovalScriptUpdateService.CheckAndUpdateScriptsAsync()`.

Plus `IStartupNotificationService` (post-startup backup notice), `UpdateCheckViewModel` (GitHub Releases update InfoBar), and a full-screen loading overlay (`LoadingLogo` / `LoadingTitleText` / `LoadingTaglineText` / `LoadingStatusText`).

### 0.9 Common preferences (all toggles persisted)

`IUserPreferencesService` → `%LOCALAPPDATA%\Winhance\Config\*.json`. Stored keys include: `Language`, `BuilderModeIntroDontShow`, `ConfigReviewModeIntroDontShow`, `InitialConfigBackupCompleted`, `StoreDownloadFallback_DontShowAgain`, plus the display toggles (technical details, info badges, new badges, window-version filter, sort mode, …).

---

## 1. OPTIMIZE (`OptimizePage` + 6 detail pages)

**Files:**
- Page: `src/Winhance.UI/Features/Optimize/OptimizePage.xaml(.cs)`
- Detail pages: `src/Winhance.UI/Features/Optimize/Pages/{Gaming,Notification,Power,Privacy,Sound,Update}OptimizePage.xaml(.cs)`
- ViewModels: `src/Winhance.UI/Features/Optimize/ViewModels/OptimizeViewModel.cs` + one `*OptimizationsViewModel.cs` per feature
- Catalogs: `src/Winhance.Core/Features/Optimize/Models/*Optimizations.cs`

**Shape:** `OptimizePage` is an **overview** listing one clickable `SettingsCard` per section (icon, display name, setting-count/group summary, NEW badge, Recommended/Default/Custom pills). Clicking pushes the section into an inner `Frame` and shows the breadcrumb dropdown. **6 sub-sections**, each ~288 `SettingDefinition` entries in total.

Section key → feature id → ViewModel (`OptimizeViewModel.Sections`):

| Section key | FeatureId | ViewModel | Detail page |
|---|---|---|---|
| `Privacy` | `Privacy` | `PrivacyOptimizationsViewModel` | `PrivacyOptimizePage` |
| `Power` | `Power` | `PowerOptimizationsViewModel` | `PowerOptimizePage` |
| `Gaming` | `GamingPerformance` | `GamingOptimizationsViewModel` | `GamingOptimizePage` |
| `Update` | `Update` | `UpdateOptimizationsViewModel` | `UpdateOptimizePage` |
| `Notification` | `Notifications` | `NotificationOptimizationsViewModel` | `NotificationOptimizePage` |
| `Sound` | `Sound` | `SoundOptimizationsViewModel` | `SoundOptimizePage` |

Every detail page is a thin shell: resolve the shared singleton `OptimizeViewModel`, apply the search parameter if present, then `RefreshSettingStatesAsync()` for that feature.

### 1.1 Privacy & Security — `PrivacyOptimizePage`
Catalog: `src/Winhance.Core/Features/Optimize/Models/PrivacyOptimizations.cs` (~90 definitions). Groups: **Security**, **Content Delivery & Advertising**, **Lock Screen**, **General**, **Speech**, **Inking and typing personalization**, **Diagnostics**, **App privacy / Windows permissions**, **Telematics**.

Capabilities include (non-exhaustive but representative):
- UAC level (Selection), workplace-join message prompts, BitLocker auto-encryption, WiFi-Sense, Automatic Maintenance, Windows Error Reporting, Remote Assistance, Smart App Control, Developer Mode, **PowerShell Execution Policy** (raises `ExecutionPolicyException` on block)
- Ads/suggestions/promotional content, Content Delivery, Subscribed content, Feature management, Soft-landing experiences, OEM pre-installed apps, Pre-installed suggested apps, Pre-installed apps history tracking, Silent app installation
- Lock screen (Spotlight, Fun Facts/tips)
- Advertising ID, website language access, Start/search app-launch tracking, Settings-app suggested content
- Online Speech Recognition, Narrator online services, Narrator scripting, custom inking/typing dictionary
- Send diagnostic data / tailored experiences with diagnostic data

### 1.2 Power — `PowerOptimizePage`
Catalog: `PowerOptimizations.cs` (~50 definitions). Groups: **Power Buttons and Lid**, **Sleep**, **Display**, **Hard Disk**, **Internet Explorer**, **Desktop Background Settings**, **Wireless Adapter Settings**, **USB settings**, **PCI Express**, **Processor Power Management**, **Intel(R) Graphics Settings**, **AMD Power Slider**, **ATI PowerPlay**, **Switchable Graphics**, **Multimedia Settings**, **Battery**, **Start Menu**.

Standout capability: **Power Plan selection** (`SettingIds.PowerPlanSelection`, a `Selection` whose options are populated at runtime from the machine's existing plans *plus* predefined templates; handled specially by `PowerService` via `ISpecialSettingHandlerRegistry`). Also:
- Turn off display / hard disk after, JavaScript timer frequency, desktop background slide show, power saving mode
- Put to sleep, hibernate after, allow wake timers, hybrid sleep, **fast startup**, show-hibernate-option
- USB selective suspend (timeout, setting), USB 3 link power management
- **Processor**: min/max state, cooling policy, performance boost mode, increase/decrease policy + thresholds, **core parking min/max cores**, EPP, power throttling
- Power/sleep/lid close button actions
- Multimedia: when sharing media, video playback quality bias, when playing video
- Battery: critical/low battery level, notification, action, reserve level (all hardware-gated via `RequiresBattery` / `RequiresLid` / `RequiresDesktop`)
- AMD overlay, ATI PowerPlay, GPU preference (`PowerPlanComboBox`), show-lock / show-sleep Start Menu options
- Many of these are **AC/DC "Dual"** NumericRange/Selections driven by `powercfg.exe` + the `PowerProf` P/Invoke surface (`src/Winhance.Core/Features/Common/Native/PowerProf.cs`)
- `HardwareDetectionService` / `HardwareCompatibilityFilter` hide or gate settings that don't apply to the machine (battery, lid, desktop, brightness, hybrid-sleep-capable)

### 1.3 Gaming & Performance — `GamingOptimizePage`
Catalog: `GamingAndPerformanceOptimizations.cs` (~115 definitions; the largest catalog). Groups: **Processor**, **Graphics**, **Network**, **Security**, **Xbox**, **System Services**, **Scheduled Tasks**, **Visual Effects**, **Accessibility**.

- Processor tuning (priority separation, scheduling quantum, MMCSS, timer resolution, …)
- Graphics: Game Mode, Game Bar, GPU scheduling, HAGS, VRR, fullscreen optimizations, mouse acceleration/trails, texture filtering quality overrides
- Network: TCP auto-tuning, Nagle's algorithm, RSC, RSS, QoS Packet Scheduler, DNS-over-HTTPS variants
- Security / exploit mitigations (ForceCompatDataQueries-style and similar)
- Xbox Game Bar / Game DVR toggles
- **~45 "System Services"** toggles (SysMain, Search, telemetry, Xbox Game Services, etc.)
- **~18 "Scheduled Tasks"** toggles (definition-time, telemetry, CEIP tasks, compatibility assistant, etc.) applied via `IScheduledTaskService`
- **~24 "Visual Effects"** toggles (animations, font smoothing, transparency, thumbnails, shadows, minimize animations, …)
- Accessibility (animation, text size, high contrast, …)

### 1.4 Windows Update — `UpdateOptimizePage`
Catalog: `UpdateOptimizations.cs` (12 definitions). Groups: **Update Policy**, **Update Behavior**, **Delivery & Store**.
- **Windows Update Policy** (`updates-policy-mode` — the win11resolve / policy-mode Selection; handled specially by `UpdateService`): never check / notify only / check but don't download / check, download and notify / allow interactive / automatic → with `windows-update-policy` + `wuauserv` policy keys
- Delivery Optimization, auto-update Microsoft Store apps
- Get latest updates as soon as available; receive updates for other Microsoft products; get me up to date
- Automatic restart after updates; update notifications; restart-required notification; metered-connection downloads; driver updates via Windows Update; driver co-installers
- Special-case: warns the user that disabling Windows Update blocks Microsoft Store app installs (`WindowsAppsService` checks the Update policy before Store installs)

### 1.5 Notifications — `NotificationOptimizePage`
Catalog: `NotificationOptimizations.cs` (15 definitions). Groups: **System Notifications**, **Additional Settings**, **Privacy Notifications**, **Security Notifications**.
- Focus assist / priority-only / do-not-disturb; per-app notifications for Calendar, Mail, Alarms, etc.
- Notification sounds / toasts toggles, Lock Screen notifications, action center
- Windows default notification apps (Mail & Calendar, To Do, etc.)
- Privacy/sensitive-content notification suppression; security & maintenance notifications

### 1.6 Sound — `SoundOptimizePage`
Catalog: `SoundOptimizations.cs` (7 definitions, one group: **System Sounds**).
- Startup sound during boot (dual registry key)
- Sound ducking preference (4-option Selection: mute / −80 % / −50 % / do nothing)
- Narrator audio ducking, voice activation for apps, last-used voice activation setting, accessibility activation sounds, accessibility warning sounds

---

## 2. CUSTOMIZE (`CustomizePage` + 4 detail pages)

**Files:**
- Page: `src/Winhance.UI/Features/Customize/CustomizePage.xaml(.cs)`
- Detail pages: `src/Winhance.UI/Features/Customize/Pages/{Explorer,StartMenu,Taskbar,WindowsTheme}CustomizePage.xaml(.cs)`
- ViewModels: `CustomizeViewModel.cs` + `Explorer/StartMenu/Taskbar/WindowsThemeCustomizationsViewModel.cs`
- Catalogs: `src/Winhance.Core/Features/Customize/Models/*.cs` — **130 `SettingDefinition` entries total**

Same overview→detail shape as Optimize.

| Section key | FeatureId | ViewModel | Detail page |
|---|---|---|---|
| `Explorer` | `ExplorerCustomization` | `ExplorerCustomizationsViewModel` | `ExplorerCustomizePage` |
| `StartMenu` | `StartMenu` | `StartMenuCustomizationsViewModel` | `StartMenuCustomizePage` |
| `Taskbar` | `Taskbar` | `TaskbarCustomizationsViewModel` | `TaskbarCustomizePage` |
| `WindowsTheme` | `WindowsTheme` | `WindowsThemeCustomizationsViewModel` | `WindowsThemeCustomizePage` |

### 2.1 Explorer — `ExplorerCustomizePage` (~87 definitions, the largest Customize catalog)

Groups: **Desktop**, **Context Menu**, **Devices and Peripherals**, **General**, **Files and Folders**, **File Associations**.

- Desktop icon visibility toggles: This PC, Recycle Bin, Users' Files, Control Panel, Network, shortcut suffix/arrow
- **Context Menu** additions (each is a *child* of the `explorer-customization-context-menu` master toggle and depends on it):
  - Take Ownership (`explorer-take-ownership`)
  - Windows Terminal
  - Open in PowerShell (run/edit `.ps1`)
  - Run SFC / DISM / CHKDSK / "Compress to" submenu
  - Toggle-extension-style file-type entries (`.ps1` Edit/Run)
- **File associations**: enable legacy Windows Photo Viewer, enable legacy Notepad (Win11)
- Files & Folders: launch-to (this PC / network drives only), click items to single-select, show recent/frequent folders, show Office files, show hidden files, hide empty drives, show file extensions, show `.lnk` extensions (child of show-file-ext), show file-operations UI, drive letter type (full paths), folder tips, compact mode, show status bar, show preview handlers, disable full path in title bar, Show SuperHidden/protected files, hide ZIP-compressed folder "compressed color", separate process for folders, persist browsers, popup descriptions, icon cache size, thumbnail cache cleanup, item spacing, thumbnail/extra-large-icon sizes, icon-overlays
- Desktop: dynamic lighting (ambient + foreground app), default printer management
- Hide merge-conflicts, hide protected files, separate process
- Parent/child and cross-group dependency graph is heavily used (e.g. `start-show-suggestions` requires `privacy-ads-promotional-master` in the Optimize catalog; `taskbar-transparent` requires `theme-transparency`)

### 2.2 Start Menu — `StartMenuCustomizePage` (13 definitions)

Groups: **Layout**, **Start Menu Settings**.
- **"Clean" start menu master toggles**: `start-menu-clean-10` (Win10) and `start-menu-clean-11` (Win11) — special-cased via `SettingIds`, apply the pinned-layout removal; `taskbar-clean` likewise
- Start menu layout (left/right/center), **All apps view layout** (grid/list), recommended section show/hide, recently-added apps, frequently used list, track program launches, show suggestions, show recommended files, recommended-files toggle depends on the recommended section
- Show account notifications, disable Bing web-search results in Start

### 2.3 Taskbar — `TaskbarCustomizePage` (28 definitions)

Groups: **Layout**, **Taskbar Icons**, **Taskbar Behavior**.
- Alignment (left/center/right), auto-hide, extended hover time, task-view button, badges ("Show taskbar badges"), **flash** taskbar when busy, show desktop button, end-task click behavior, "End task" on right-click, combine buttons (always/never/when full, plus "other" child for multi-display), taskbar button size
- **Multi-display taskbar** (`taskbar-multi-display`) with **child settings** `taskbar-multi-display-apps` and `taskbar-combine-buttons-other`
- Show lock option, show sleep option, meeting/meet-now button
- System-tray icons (Win10 + Win11 variants) and tray overflow customization (detection via `DetectionType.SystemTrayIcons`)
- **Taskbar icons on/off**: search box (Win10 & Win11 variants), Task View, Copilot, Copilot companion, Copilot PWA pin, Recall pin, Widgets, News & Interests
- Transparency (depends on `theme-transparency`), small taskbar

### 2.4 Windows Theme — `WindowsThemeCustomizePage` (2 definitions, but with special handling)

Groups: **Theme Mode**, **Transparency**.
- **`theme-mode-windows` "Choose your mode"** — `Selection` (Light / Dark) writing `AppsUseLightTheme` + `SystemUsesLightTheme`; `RequiresConfirmation=true`; `RestartProcess="Explorer"`. Special handler `ThemeWallpaperApplier` (`src/Winhance.Infrastructure/Features/Customize/Services/`) additionally swaps the desktop wallpaper to the matching Windows default (`C:\Windows\Web\Wallpaper\Windows\img0.jpg` / `img19.jpg` for Win11, `C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg` for Win10) when the "change wallpaper too" checkbox is ticked (surfaced in the config-import dialog and in the setting's confirmation).
- **`theme-transparency` "Transparency effects"** toggle (`EnableTransparency`)

---

## 3. ADVANCED TOOLS (`AdvancedToolsPage` + 2 detail pages) — **NO AKARI EQUIVALENT**

**Files:**
- `src/Winhance.UI/Features/AdvancedTools/AdvancedToolsPage.xaml(.cs)` — overview with two clickable `SettingsCard`s
- `src/Winhance.UI/Features/AdvancedTools/WimUtilPage.xaml(.cs)` + `ViewModels/WimUtilViewModel.cs`, `WimStep1ViewModel.cs`, `WimImageFormatViewModel.cs`, `WimStep2XmlViewModel.cs`, `WimStep3DriversViewModel.cs`, `WimStep4IsoViewModel.cs`, `Models/WizardStepState.cs`, `Models/WizardActionCard.cs`
- `src/Winhance.UI/Features/AdvancedTools/AutounattendGeneratorPage.xaml(.cs)` + `ViewModels/AutounattendGeneratorViewModel.cs`
- Infrastructure: `src/Winhance.Infrastructure/Features/AdvancedTools/Services/{WimImageService,WimCustomizationService,OscdimgToolManager,IsoService,AutounattendScriptBuilder}.cs`, `ScriptSections/*.cs`, `Helpers/{PowerShellScriptUtilities,DriverCategorizer,RegistryCommandEmitter}.cs`
- Core interfaces: `src/Winhance.Core/Features/AdvancedTools/Interfaces/{IWimImageService,IWimCustomizationService,IOscdimgToolManager,IIsoService,IDriverCategorizer,IAutounattendXmlGeneratorService}.cs`

### 3.1 `AdvancedToolsPage` — Overview

Two cards, breadcrumb navigation, no search:
1. **WIMUtil** — "Windows Installation Media Utility" / "Create Custom Windows Installation Media"
2. **Create Autounattend XML** — "Generate an autounattend.xml file based on your current Winhance selections to customize Windows during installation"

Section keys: `WimUtil`, `AutounattendXml`; `AdvancedToolsPage.NavigateToSection` maps them to `WimUtilPage` / `AutounattendGeneratorPage` in an inner `Frame`.

### 3.2 `WimUtilPage` — Build a customized Windows installation ISO

A 4-step accordion wizard (`WizardStepState`: number, icon, title, status text, `IsExpanded`, `IsAvailable`, `IsComplete`), each step driven by a `WizardStepCard` set. Steps 2–4 auto-expand once extraction completes (guarded by `_autoExpandApplied`). `WimUtilViewModel` is a thin orchestrator that forwards ~20 properties and ~14 commands to the sub-ViewModels so the existing XAML keeps binding.

**Step 1 — Select ISO & extract** (`WimStep1ViewModel`, `IIsoService`, `IDismProcessRunner`)
- Card: *Select ISO* → Win32 file picker
- Card: *Select working directory* (default `%TEMP%\WinhanceWIM`)
- Checkbox: "already extracted" — skips re-extraction
- Start extraction:
  - Pre-flight **disk-space check** → `InsufficientDiskSpaceException` (`src/Winhance.Core/Features/Common/Exceptions/`)
  - Mounts the ISO with `Mount-DiskImage -PassThru | Get-Volume`, copies `sources\install.wim` (or ESD), **unmounts on success, cancel, and error** (explicit try/finally + catch paths)
  - Disk-image detection determines whether the image is WIM or ESD and which index/build it is (`IWimImageService`, `ImageDetectionResult`, `ImageFormatInfo`)
- Card: download Windows 10 / Windows 11 ISO from Microsoft

**Optional Image-format conversion** (`WimImageFormatViewModel`)
- Detects the current format; "Optional convert" (WIM ⇄ ESD, using the ADK/`wimlib` `export` path via DISM process runner) with a live `IsConverting` progress state
- Shows both file sizes (`WimFileSize` / `EsdFileSize`); buttons to delete either; a card for "both images exist"

**Step 2 — Add XML file** (`WimStep2XmlViewModel`, `IAutounattendXmlGeneratorService`, `IWimCustomizationService`)
- Card: *Generate Winhance XML* — builds an `autounattend.xml` from current system state + selected Windows apps (see §3.3)
- Card: *Download XML* — fetches Schneegans' `unattendedwinstall` answer file from the web
- Card: *Select XML* — pick an existing `autounattend.xml`; a button opens Schneegans' unattended-winstall generator in the browser

**Step 3 — Add drivers** (`WimStep3DriversViewModel`, `IDriverCategorizer`)
- Card: *Extract drivers from this system* → runs `export` of the running image's drivers (`/recurse /all-drivers`) into `drivers` in the working tree, then injects them into the WIM
- Card: *Select custom drivers* → folder picker → recursive `.inf` injection into the WIM
- `DriverCategorizer` splits `pnputil` output into display/network/system/MediaKeying so only real drivers are imported

**Step 4 — Create ISO** (`WimStep4IsoViewModel`, `IOscdimgToolManager`)
- Card: *Download oscdimg* — `OscdimgToolManager` searches ADK install paths (`Windows Kits\10` and `\11`, amd64/x86) and WinGet package folders for `oscdimg.exe`; if absent, offers direct ADK download (`go.microsoft.com/fwlink/?linkid=2289980`, `adksetup.exe`) or `winget install Microsoft.OSCDIMG` via `IWinGetPackageInstaller`
- Card: *Select output ISO path*
- **Create ISO** button → `IsoService.CreateIsoAsync`: disk-space pre-flight, then `oscdimg.exe -m -o -u2 -udfver102` over the staged folder via `IDismProcessRunner.RunProcessWithProgressAsync`, with live terminal output
- Cleanup helper `CleanupWorkingDirectoryAsync`

### 3.3 `AutounattendGeneratorPage` — Autounattend XML generator

UI: one `SettingsCard` with an **accent "Generate"** button + spinner overlay + an informational InfoBar ("more generation options coming soon"). It is also reachable from the WIM wizard's Step 2.

Flow (`AutounattendGeneratorViewModel.GenerateAutounattendXmlAsync`):
1. Confirmation dialog
2. Save-file picker (**filename must be exactly `autounattend.xml`**, else a warning)
3. Pull the **currently selected Windows Apps** from `WindowsAppsViewModel` (lazily `LoadItemsAsync()`), mapped to `ConfigurationItem` (Appx package name / capability name / optional-feature name)
4. If nothing is selected, offer to continue anyway
5. `IAutounattendXmlGeneratorService.GenerateFromCurrentSelectionsAsync` → `AutounattendXmlGeneratorService` (`src/Winhance.UI/Features/AdvancedTools/Services/`):
   - Builds a `UnifiedConfigurationFile` v2.0 from the **live system state** of every Optimize + Customize setting (`ISystemSettingsDiscoveryService.GetSettingStatesAsync`) plus the selected apps
   - `AutounattendScriptBuilder.BuildWinhancementsScriptAsync` emits `Winhancements.ps1` from pluggable sections: `ScriptPreambleSection`, `FeatureRegistryScriptSection`, `PowerSettingsScriptSection`, `SpecialFeatureScriptSection`, `AppRemovalScriptSection` (registry + `reg.exe` command emission, powercfg commands, capability/DISM/feature removal, AppX removal)
   - Injects the script into the embedded `Resources/AdvancedTools/autounattend-template.xml` (`<unattend>` with a FirstLogon/RunOnce synchronous command pointing at `C:\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1`)
   - **Validates XML well-formedness** via `IPowerShellRunner.ValidateXmlSyntaxAsync`
   - Writes **UTF-8 without BOM** (Windows Setup requirement)
6. Success dialog offering to jump straight to **WIMUtil**

Also reachable in **Builder mode → Autounattend target** → *Save autounattend.xml* (`IConfigExportService.ExportBuilderAutounattendAsync`).

---

## 4. SOFTWARE & APPS (`SoftwareAppsPage`)

**Files:**
- `src/Winhance.UI/Features/SoftwareApps/SoftwareAppsPage.xaml(.cs)`
- ViewModels: `SoftwareAppsViewModel.cs`, `WindowsAppsViewModel.cs`, `ExternalAppsViewModel.cs`, `AppItemViewModel.cs`, `RemovalStatusViewModel.cs`, `RemovalStatusContainerViewModel.cs`, `AppSortHelper.cs`, `Services/SelectedAppsProvider.cs`, `Models/{SoftwareAppsViewMode,AppSortMode,ISelectable}.cs`, `AppOperationConfirmation.cs`
- Help content: `Views/WindowsAppsHelpContent.xaml`, `Views/ExternalAppsHelpContent.xaml`
- Catalogs: `src/Winhance.Core/Features/SoftwareApps/Models/` — **~266 `ItemDefinition` entries**
- Infrastructure: `src/Winhance.Infrastructure/Features/SoftwareApps/Services/` (22 services) + `Services/WinGet/` (COM + CLI + ConPTY + progress parser + exit codes) + `Services/WinGet/winget-cli/` (bundled CLI)
- Interop: `src/WindowsPackageManager.Interop/`

### 4.1 Page structure

- **2 tabs**: `Windows Apps` and `External Apps`, each with an `InfoBadge` (pending-change count in Config Review) and a **lock overlay** when locked.
- **3 view modes** (toggle group): **Card**, **Table** (`DataGrid` 7.1.2 with sortable columns Name / Description / Type / Status / Installable / Group), **Compact** (dense rows).
- **3 sort modes**: Name A-Z (Installed first) — default, Name A-Z, Name Z-A. In Table view the Sort button is disabled (sort via column headers) with a hint.
- **Search** with live filtering via `SearchHelper.MatchesSearchTerm` (name + description + id).
- **Select All** / **Select All Installed** / **Select All Not Installed**.
- **Help button** → in-app content dialog with a legend: Installed / Can be reinstalled / Not installed / **Cannot reinstall** / **Warning** (amber instability pill, e.g. Microsoft Edge).
- **Install Selected** button (bulk, with progress + per-item results).
- Per-item status pills: Installed / Not installed / Reinstallable / Non-reinstallable / Warning.
- **Removal status container**: a running list of removal outcomes with per-item status.
- **Refresh** button re-runs install-status discovery.

### 4.2 Windows Apps tab (3 sections in one list)

1. **Windows Apps** (`WindowsAppDefinitions.cs`, ~55 `ItemDefinition`s) — inbox/AppX bloat grouped by area (3D/Mixed Reality, Bing/Search, Camera/Media, Dev Tools, Entertainment, Gaming, Mail/Calendar, Microsoft 365, Mixed Reality, Office, OneDrive, Solitaire, Sports, Starter Apps, System/Store, Xbox…). Each carries `AppxPackageName[]` + `MsStoreId`.
2. **Windows Capabilities** (`CapabilityDefinitions.cs`, 10) — legacy DISM capabilities: Internet Explorer, PowerShell ISE, Quick Assist (Legacy), Steps Recorder, Windows Media Player, Windows PowerShell ISE, Work Folders, etc.
3. **Windows Optional Features** (`OptionalFeatureDefinitions.cs`, 7) — `Microsoft-Windows-Subsystem-Linux`, `Microsoft-Hyper-V-Hypervisor`, `Microsoft-Hyper-V-All`, `Microsoft-Hyper-V-Tools-All`, .NET 3.5, etc. (`RequiresReboot` where applicable)

**Install path** (`AppInstallationService.InstallSingleAppCoreAsync`):
- Capability → `ILegacyCapabilityService.EnableCapabilityAsync` (PowerShell DISM)
- Optional feature → `IOptionalFeatureService.EnableFeatureAsync`
- Store/AppX → `IWindowsAppsService.InstallAppAsync`: winget `winget install --id … --source msstore` (via WinGet COM or the bundled CLI); blocks with an explicit warning if the **Update policy** would prevent Store installs; offers a **Store download fallback** (`IStoreDownloadService` → `store.rg-adguard.net` API → download `.msixbundle` + dependencies → `Add-AppxPackage`) with a "don't show again" preference

**Removal path** (`WindowsAppUninstallService`, `BloatRemovalService`, `EdgeRemovalScript`, `OneDriveRemovalScript`):
- Generates/merges a `BloatRemoval.ps1` (script version `2.3`) listing selected packages, capabilities, optional features and special apps
- Executes it via `IPowerShellRunner` with `ExecutionPolicy Bypass`
- If the execution policy still blocks → **defers to a Scheduled Task** (`ScheduledTaskService`, Logon or Startup trigger, `LogonType=5` run-whether-logged-in-or-not) and reports `RemovalOutcome.DeferredToScheduledTask`
- **Edge** and **OneDrive** have **dedicated removal scripts** (`EdgeRemovalScript.cs`, `OneDriveRemovalScript.cs`) plus `OpenWebSearchRepair` task and hardlink cleanup; reinstalling them cleans up the obsolete script + task
- Removal confirmation offers a **"save removal scripts"** checkbox (`ShowRemovalSummaryAndConfirm`), and `PersistRemovalScriptsAsync` registers the deferred tasks
- Every install/remove is appended to `ChangeHistory.txt` (`IChangeHistoryService.LogAppChange(app.Name, AppChangeKind)`)

### 4.3 External Apps tab (~191 `ItemDefinition`s across 16 category partials)

`ExternalAppDefinitions.cs` concatenates: **Browsers** (~21), **Document Viewers** (12), **Messaging/Email/Calendar** (10), **Online Storage & Backup** (7), **Multimedia** (24), **Imaging** (13), **Customization Utilities** (14), **Gaming** (8), **Compression** (4), **File & Disk Management** (14), **Remote Access** (12), **Optical Disc Tools** (4), **Other Utilities** (11), **Privacy & Security** (7), **Development Apps** (11), **Runtimes & Dependencies** (22).

**Install fallback chain** (`ExternalAppsService.InstallAppAsync`):
1. If `ExternalApp.RequiresDirectDownload` → `IDirectDownloadService.DownloadAndInstallAsync`
2. Otherwise build an ordered source list **WinGet → MsStore**, trying each via `IWinGetPackageInstaller.InstallPackageAsync(pkgId, source, …)`; `IWinGetDetectionService.GetInstallerTypeAsync` detects **portable** installers and, on success, `CreateStartMenuShortcutForPortableAppAsync` creates a Start Menu shortcut
3. If `ChocoPackageId` is defined → `IChocolateyService`: `IsChocolateyInstalledAsync` (finds `choco.exe`) → else **bootstraps Chocolatey** via `community.chocolatey.org/install.ps1` → `choco install <id> -y --no-progress --ignore-checksums`. Includes **ghost-package recovery**: if choco reports already-installed but Winhance detected missing, `CleanupStalePackageRecordAsync` (`choco uninstall`) then retries
4. If `ExternalApp.DownloadUrl` is set → direct-download fallback

**Uninstall** (`IExternalAppUninstallService`) routes on `DetectionSource` (`WinGet`, `Chocolatey`, `AppX`, `Registry`, `FileSystem`) and uses `ItemDefinition.ProcessesToStop`, `RegistryDisplayName` / `RegistrySubKeyName` patterns (with `{version}`/`{arch}`/`{locale}` placeholders), and `DetectionPaths`.

### 4.4 `WindowsPackageManager.Interop` — the WinGet COM bridge

A standalone project that generates C# projections for `Microsoft.Management.Deployment` (the WinGet COM API) using CsWinRT over the `.winmd` shipped by `Microsoft.WindowsPackageManager.ComInterop`.

Hand-written files:
- `WindowsPackageManager/WindowsPackageManagerFactory.cs`, `WindowsPackageManagerStandardFactory.cs`, `WindowsPackageManagerElevatedFactory.cs`
- `WindowsPackageManager/ClsidContext.cs`, `ClassModel.cs`, `ClassesDefinition.cs`

Consumers in `Winhance.Infrastructure/Features/SoftwareApps/Services/WinGet/`:
- **`WinGetComSession`** — singleton owning the factory + `PackageManager`, double-checked locking, `EnsureComInitialized()`, `ResetFactory()`, `ComInitTimedOut`. Uses `WindowsPackageManagerStandardFactory(ClsidContext.Prod, allowLowerTrustRegistration: true)` because the app is admin + self-contained App SDK; `WindowsPackageManagerElevatedFactory` (via `winrtact.dll`) **hangs** in self-contained mode (microsoft/winget-cli#4377).
- **`WinGetBootstrapper`** — `EnsureWinGetReadyAsync` / `InstallWinGetAsync`: bootstraps App Installer from the **bundled `winget-cli`** payload, then retries COM init 10× at 3 s intervals; raises `WinGetInstalled`
- **`WinGetDetectionService`** — installer-type detection (portable vs installer), needed for shortcut creation
- **`WinGetPackageInstaller`** — install/uninstall/upgrade via COM, honoring `WinGetInstallerOverride` (`winget install --override`)
- **`Utilities/`** — `WinGetCliRunner` (bundled/system CLI detection + process run), `ConPtyProcess` (ConPTY so `winget.exe` progress renders as a live terminal stream), `WinGetProgressParser` (parses winget's progress output), `WinGetExitCodes` (exit-code → message mapping)

### 4.5 Icons

`IAppIconResolver` (cache-first) resolves an icon per item via, in order: AppX logo (`AppListEntry.DisplayInfo.GetLogo` — current-user / all-users / provisioned), then `IAppxIconSource`, then `IRepoIconSource` (jsDelivr `cdn.jsdelivr.net/gh/memstechtips/package-icons@main/…` with **sha256 verification** and a User-Agent header), then `IStoreDownloadService`/shell32 fallback. `IIconManifestService` + `IconCacheMigration` + `LightVariantSynthesizer` manage the local icon cache. `tests/Winhance.Core.Tests/Models/IconCoverageTests.cs` asserts every catalog entry has an icon (against a checked-in `package-icons-manifest.json` snapshot — tests never hit the network).

---

## 5. SETTINGS (`SettingsPage`)

**Files:** `src/Winhance.UI/Features/Settings/SettingsPage.xaml(.cs)`, `ViewModels/SettingsViewModel.cs`.

Four `SettingsCard` groups:

1. **Language** — `ComboBox` of every locale discovered by `ILocalizationService.GetAvailableLanguages()` (English first, then native display names). Changing it applies immediately (all ViewModels re-raise `PropertyChanged`) and persists via `IUserPreferencesService`.
2. **Theme (app UI theme)** — `System` / `Light` / `Dark` (`WinhanceTheme.System|LightNative|DarkNative`) via `IThemeService`. Applies immediately and re-colors the window caption buttons (`TitleBarManager`).
3. **Configuration — Backup & Restore** — **Import** and **Export** buttons, each with a keyboard accelerator. Both route through `IConfigurationService` (see §6.4).
4. **System Protection — System Restore Point** — "Create Restore Point" button. Runs `ISystemBackupService.CreateRestorePointAsync` (WMI `SystemRestore`), indeterminate task progress, success/failure dialogs (failure appends the service's `ErrorMessage`).

Plus the Settings page is `AddTransient` (fresh VM per navigation), unlike the singleton page VMs.

---

## 6. MODES: Normal / Builder / Config Review

**Files:** `src/Winhance.Core/Features/Common/Enums/WinhanceMode.cs`, `Enums/BuilderTarget.cs`, `Models/BuilderEdit.cs`, `Interfaces/IApplicationModeService.cs`, `src/Winhance.UI/Features/Common/Services/ConfigReviewService.cs`, `src/Winhance.UI/ViewModels/{MainWindowViewModel,BuilderModeBarViewModel,ReviewModeBarViewModel}.cs`.

Single source of truth: `ConfigReviewService` implements **six** interfaces at once (`IConfigReviewService`, `IConfigReviewModeService`, `IConfigReviewDiffService`, `IConfigReviewBadgeService`, `IApplicationModeService`), registered once and aliased. It owns `CurrentMode`; **no public method sets it directly** (private `SetMode` chokepoint) and raises `ModeChanged`.

### 6.1 Normal mode (default)
Toggling a setting applies immediately to the live system. No banners.

### 6.2 Builder mode
**Purpose:** author a `.winhance` config or an `autounattend.xml` from the UI **without touching the PC**.

- Entered from the title-bar mode switcher → first-run explainer dialog (`Dialog_BuilderIntro_*`) with a **"don't show again"** checkbox persisted as `BuilderModeIntroDontShow`.
- `BuilderModeBarViewModel` renders a persistent **Builder mode bar** under the title bar: title, description ("You're authoring a file from these settings — nothing here changes this PC"), a **target radio pair** (Config / Autounattend), **Save** (label changes to "Save autounattend.xml" when Autounattend is chosen) and **Cancel**.
- Toggling a setting **records** a `BuilderEdit` (`IApplicationModeService.RecordBuilderEdit`) — upserted by `SettingId`. Captures Toggle/CheckBox/Action `IsSelected`, Selection `SelectedIndex`, and Selection-Custom `CustomStateValues`. **NumericRange and AC/DC power edits are explicitly *not* recorded yet** (documented gap in `BuilderEdit`) — they fall back to the system-seeded value.
- `SetBuilderTarget` switches target **without** losing authored state; only the Save output changes.
- **Save** → `IConfigExportService.ExportBuilderConfigAsync()` or `ExportBuilderAutounattendAsync()`, merging recorded edits over the system-seeded base config.
- **Cancel** → confirm dialog → `EnterNormalMode()`. Leaving Builder with unsaved edits also prompts (from the mode switcher).

### 6.3 Config Review mode
**Purpose:** import a config, review every change accept/reject, then apply.

- Entered via the mode switcher → the **existing import-and-review flow** (`IConfigurationService.ImportConfigurationAsync`), preceded by a first-run explainer (`ConfigReviewModeIntroDontShow`).
- The **import dialog** (`Features/Common/Dialogs/ConfigImportDialogBuilder.cs`) offers four option cards (`ImportOption`):
  - `ImportOwn` — pick a `.winhance` file
  - `ImportRecommended` — the embedded `Winhance_Recommended_Config.winhance`
  - `ImportBackup` — the user's first-run backup config
  - `ImportWindowsDefaults` — "reset to Windows defaults"
- Additional import switches in the same dialog:
  - **"Skip review and apply immediately"** (`Review_Mode_Skip_Checkbox`)
  - Per-app action radios: **Windows Apps** → Install / Uninstall / Select-only; **External Apps** → Install / Uninstall / Select-only
  - Checkbox **change wallpaper with theme**, **clean taskbar**, **clean Start menu**
- `IConfigReviewService.EnterReviewModeAsync(config, isWindowsDefaults)` computes a `ConfigReviewDiff` per setting id.
- **`ReviewModeBarViewModel`** renders a **Review mode bar**: title, description, live status `"N of M reviewed (K will be applied)"` (or "All settings already match config" / "No configuration items to apply"), **Apply Config** and **Cancel**.
- **Apply is gated** until *everything* is reviewed: every Optimize/Customize diff accepted or rejected, `SoftwareAppsReviewed`, and `IsSectionFullyReviewed("Optimize"/"Customize")`.
- **Per-feature review affordances:** each setting card gets Approve / Reject controls (via `SettingReviewDiffApplier` + `SettingItemViewModel.ClearReviewState`); the SoftwareApps page shows a **review banner** per tab with Install / Remove action toggles.
- **Nav + breadcrumb badges:** `InfoBadge` counts per nav item and per section (total diffs / pending diffs), computed by `INavBadgeService` + `ConfigReviewService.GetNavBadgeCount`, plus `MarkFeatureVisited` for SoftwareApps so the page is "done" once visited.
- **Show Only Changes** filter (Quick Actions) — `ReviewModeFilter` predicates driven by the diff dictionary, not per-VM flags (deliberate fix for a drift bug, issue #665).
- **Windows Version Filter is force-on** during review and restored on exit (`MainWindowViewModel.HandleReviewModeFilterChange`).
- On exit: `BuilderModeExitedEvent` / `ReviewModeExitedEvent` on `IEventBus` tell every `BaseSettingsFeatureViewModel` to clear review state or re-read live system state.

### 6.4 Configuration file format

`UnifiedConfigurationFile` (`src/Winhance.Core/Features/Common/Models/UnifiedConfigurationFile.cs`), v2.0, extension `.winhance`:
```
{ Version, CreatedAt,
  WindowsApps: ConfigSection { IsIncluded, Items[] },
  ExternalApps: ConfigSection { IsIncluded, Items[] },
  Customize:    FeatureGroupSection { IsIncluded, Features: { <FeatureId>: ConfigSection } },
  Optimize:     FeatureGroupSection { IsIncluded, Features: { <FeatureId>: ConfigSection } } }
```
Serialized with `ConfigFileConstants.JsonOptions` (indented, case-insensitive, ignore nulls). `IConfigMigrationService` handles backward-compatible v1 imports. Embedded resources also include `Winhance_Default_Config_Windows10_22H2.winhance` and `Winhance_Default_Config_Windows11_25H2.winhance`.

---

## 7. COMMON / CROSS-CUTTING CAPABILITIES

| Capability | Where |
|---|---|
| **Localization, 29 locales, live switch** | `LocalizationService`, `Features/Common/Localization/*.json` |
| **Logging** to `C:\ProgramData\Winhance\Logs` (30-day / 50-file retention) + `StartupLogger` static early-boot trace | `Core/…/Services/LogService.cs`, `StartupLogger.cs` |
| **Event bus** (`IEventBus`) with `Subscribe`/`SubscribeAsync`/`Publish` + `ISubscriptionToken` | `Core/…/Events/`, `Infrastructure/…/Events/EventBus.cs` |
| **In-app dialogs**: information / warning / error / confirmation (with "don't show again" checkbox) / sponsors / config-import options / task output / custom content | `IDialogService`, `DialogService`, `Features/Common/Dialogs/*` |
| **Task progress** (3 concurrent, cancelable, terminal output) | `ITaskProgressService` / `IMultiScriptProgressService` |
| **Bulk actions**: Apply Recommended, Reset to Defaults | `IBulkSettingsActionService`, `IRecommendedSettingsApplier` |
| **Technical details** per setting + "Open in Registry Editor" | `TechnicalDetailsManager`, `IRegeditLauncher` |
| **Badge system**: Recommended / Default / Custom / Preference pills + per-card quick-set buttons + NEW pill + nav badges | `SettingBadgeKind`, `FeatureBadgeAggregator`, `SettingDescriptionWithBadges`, `BadgeStyles.xaml` |
| **Search + suggestions** across all features | `SearchHelper`, `SearchSuggestionItem`, `AutoSuggestBoxExtensions` |
| **Tooltips** (`ITooltipDataService` + `TooltipRefreshEventHandler`) | Infrastructure |
| **User preferences** persisted as JSON under `%LOCALAPPDATA%\Winhance\Config` | `UserPreferencesService` |
| **System info provider**, **version service** (+ auto-update + in-app installer download), **sponsors service** (live URL with bundled fallback) | `SystemInfoProvider`, `VersionService`, `SponsorsService` |
| **Process restart / service restart** after a setting applies, declared on the definition (`RestartProcess`, `RestartService`, `RequiresRestart`) | `IProcessRestartManager` |
| **Dependency management** between settings (`SettingDependency`, `AutoEnableSettingIds`, `CrossGroupChildSettings`) | `ISettingDependencyResolver` |
| **Windows-version + hardware compatibility filtering** | `IWindowsCompatibilityFilter`, `IHardwareCompatibilityFilter`, `IWindowsVersionFilterService` |
| **Change receipt** `ChangeHistory.txt` | `IChangeHistoryService` |
| **System Restore point creation** | `ISystemBackupService`, `ISystemRestoreService` |
| **Single-instance enforcement** via `AppInstance` + P/Invoke window activation, before WinUI init | `Program.cs` |
| **Custom file dialogs** via `IFileOpenDialog`/`IFileSaveDialog` P/Invoke (WinRT pickers break under elevation) | `Win32FileDialogHelper`, `IFilePickerService` |
| **PageUp/PageDown/Home/End fast scroll** on long pages | `PageScrollHelper` |

---

## 8. Gap checklist for Akari Tool (summary)

Winhance has, and Akari must decide about:

1. **6 Optimize sub-pages** with ~288 setting definitions (Gaming/Privacy dominate), including ~45 system-service toggles, ~18 scheduled-task toggles, ~24 visual-effects toggles.
2. **4 Customize sub-pages** with ~130 definitions, including the Windows 11 context-menu extensions (Terminal, SFC/DISM/CHKDSK, PS1 Edit/Run, Compress-to) and the "clean Start Menu / clean Taskbar" masters.
3. **Advanced Tools entirely** — the WIM/ISO installation-media builder (4-step wizard + WIM⇄ESD conversion + driver injection + oscdimg acquisition) and the autounattend.xml generator. **No Akari equivalent exists.**
4. **Software & Apps** — 266 catalog items across Windows Apps / Capabilities / Optional Features / 16 external-app categories, with WinGet COM interop, bundled WinGet CLI, Chocolatey bootstrap + ghost-package recovery, Store download fallback via `store.rg-adguard.net`, direct-download fallback, scheduled-task-deferred bloat removal, dedicated Edge/OneDrive removal scripts, and 3 view modes + 3 sort modes.
5. **Three-mode workflow** — Normal / Builder (author a `.winhance` or `autounattend.xml` without applying) / Config Review (import → per-setting accept/reject → gated Apply), including the four-way import-options dialog, nav badges, "Show Only Changes" filter, and Builder edit recording (with a known gap for NumericRange/AC-DC edits).
6. **Settings page** with Language, app Theme, Import/Export, and Create System Restore Point.
7. **29-locale JSON localization** with live language switch and per-setting auto-generated keys.
8. **Rich setting-card UX** — Technical Details panel with registry jump, badge pills, per-card quick-set-to-recommended/default, NEW badges, status banners, parent/child dimming, accessibility naming.
9. **29 locales + badges** are table stakes for parity, not optional polish.

---

*Feature inventory: 2026-10-05*