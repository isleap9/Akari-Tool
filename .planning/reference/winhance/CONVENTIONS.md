# Winhance — Working Conventions

**Analysis Date:** 2026-10-05
**Reference repo:** `C:/Users/isleap/Documents/GitHub/Winhance` (read-only reference material)

These are the patterns to copy when bringing Akari Tool to parity. Every path is relative to `C:/Users/isleap/Documents/GitHub/Winhance`.

---

## 1. Layering Rule

```
src/Winhance.UI               → Winhance.Core + Winhance.Infrastructure
src/Winhance.Infrastructure   → Winhance.Core + WindowsPackageManager.Interop
src/Winhance.Core             → nothing (Contracts: Models, Interfaces, Enums, Events, Native, Localization keys)
```

**Hard rule:** every interface lives in `Winhance.Core/Features/<Domain>/Interfaces/`. Every implementation lives in `Winhance.Infrastructure/Features/<Domain>/Services/` or `Winhance.UI/Features/<Domain>/Services/`. The UI project never contains an interface that Infrastructure implements — it may contain UI-only interfaces (`ISettingsFeatureViewModel`, `IDispatcherService`, `IFilePickerService`, `IThemeService`, `IResourceService`, `IMainWindowProvider`) that only UI code consumes.

Data (settings, app items, enums, constants) lives in Core as **records** (`sealed record SettingDefinition`, `record ItemDefinition`, `abstract record BaseDefinition`). Records + `with` expressions are how localization rewrites a definition non-destructively (`SettingLocalizationService`).

---

## 2. How to add a new SETTING

The catalog pattern. A setting is **data**, not code.

### Step 1 — Add the `SettingDefinition` to the feature catalog

Pick the right file under `src/Winhance.Core/Features/Optimize/Models/` (e.g. `SoundOptimizations.cs`) or `src/Winhance.Core/Features/Customize/Models/`. Each file exposes a single static `SettingGroup Get<Feature>Optimizations()` returning a `SettingGroup { Name, FeatureId, Settings = new List<SettingDefinition> { … } }`.

Canonical example — `src/Winhance.Core/Features/Optimize/Models/SoundOptimizations.cs:18`:

```csharp
new SettingDefinition
{
    Id = "sound-startup",                    // stable, kebab-case; NEVER reused
    IsSubjectivePreference = true,           // no objectively-better answer → "Preference" badge
    Name = "Startup Sound During Boot",      // English fallback; localization overrides at runtime
    Description = "Play the Windows startup sound when your computer boots up",
    GroupName = "System Sounds",             // becomes the section header on the detail page
    Icon = "MonitorSpeaker",                 // Material Design glyph (IconPack="Fluent" for FluentIcon)
    InputType = InputType.Toggle,            // Toggle | Selection | NumericRange | Action | CheckBox
    RegistrySettings = new List<RegistrySetting>
    {
        new RegistrySetting
        {
            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation",
            ValueName = "DisableStartupSound",
            RecommendedValue = 1,             // "Recommended" pill + Set-to-Recommended target
            EnabledValue  = [0, null],        // what to write when ON; `null` = delete the value
            DisabledValue = [1],              // what to write when OFF
            DefaultValue = 0,                 // "Default" pill + Set-to-Default target
            ValueType = RegistryValueKind.DWord,
        },
    },
},
```

The whole declarative surface (all on `BaseDefinition` / `SettingDefinition`):

**Identity & presentation** — `Id`, `Name`, `Description`, `GroupName`, `Icon`, `IconPack` (`"Material"` default, or `"Fluent"`), `DisableTooltip`, `AddedInVersion` (drives the NEW badge), `LocalizationId` (lets a Win10 and a Win11 variant share text keys).

**OS gating** — `IsWindows11Only`, `IsWindows10Only`, `MinimumBuildNumber`/`MinimumBuildRevision`/`MaximumBuildNumber`/`MaximumBuildRevision`, `SupportedBuildRanges` (list of `(MinBuild, MaxBuild)` tuples), `VersionCompatibilityMessage` (may embed `Key|arg1|arg2`).

**Hardware gating** — `RequiresBattery`, `RequiresLid`, `RequiresDesktop`, `RequiresBrightnessSupport`, `RequiresHybridSleepCapable`.

**Mechanisms** — `RegistrySettings[]`, `PowerCfgSettings[]`, `PowerShellScripts[]`, `RegContents[]`, `ScheduledTaskSettings[]`, `NativePowerApiSettings[]`, `DetectionType` (`Registry|PowerCfg|ScheduledTask|PowerPlan|DnsServer|SystemRestore|SystemTrayIcons`).

**Behaviour** — `RequiresConfirmation`, `RestartProcess` (e.g. `"Explorer"`), `RestartService`, `RequiresRestart`, `ValidateExistence`.

**Composition** — `ParentSettingId` (parent/child dimming), `Dependencies[]` (`SettingDependency`), `AutoEnableSettingIds`, `CrossGroupChildSettings`, `SettingPresets`.

**Recommendations** — `RecommendedToggleState` / `DefaultToggleState` (for Toggles whose default is "key absent"), `IsSubjectivePreference`, per-`RegistrySetting.RecommendedValue`/`DefaultValue`, per-`ComboBoxOption.IsRecommended`/`IsDefault`, `PowerCfgSetting.RecommendedValueAC/DC` + `DefaultValueAC/DC`, `PowerRecommendation` (incl. `LoadDynamicOptions`).

**Numeric/Selection metadata** — `ComboBox` (`ComboBoxMetadata.Options[]` where each `ComboBoxOption` has `DisplayName`, `ValueMappings` (registry value name → value), `SimpleValue`, `CommandValue`, `Script`, `Tooltip`, `Warning`, `Confirmation`, `ScriptVariables`, `IsDefault`, `IsRecommended`), `NumericRange` (min/max/units), `ResolveUnmatchedToDefault`.

### Step 2 — Register the setting id in `SettingIds` **only if** it needs special apply/discovery handling

`src/Winhance.Core/Features/Common/Constants/SettingIds.cs`:
```csharp
public const string PowerPlanSelection = "power-plan-selection";
public const string ThemeModeWindows   = "theme-mode-windows";
public const string UpdatesPolicyMode  = "updates-policy-mode";
public const string StartMenuCleanWin10 = "start-menu-clean-10";
public const string StartMenuCleanWin11 = "start-menu-clean-11";
public const string TaskbarClean        = "taskbar-clean";
```
Never hardcode these strings in service code — compare against the constant.

### Step 3 — Register the *feature* (only when adding a whole new feature, not a new setting)

`src/Winhance.Core/Features/Common/Constants/FeatureIds.cs` — add the id constant.
`src/Winhance.Core/Features/Common/Constants/FeatureDefinitions.cs` — add a `FeatureDefinition(Id, "Display Name", "Category")` entry to `All`; the `OptimizeFeatures` / `CustomizeFeatures` `HashSet<string>`s are derived from it. This is what makes the feature appear in Config Review, config export, and autounattend generation.
`src/Winhance.Infrastructure/Features/Common/Services/CompatibleSettingsRegistry.cs:231` `GetKnownFeatureProviders()` — **add exactly one entry**:
```csharp
[FeatureIds.Sound] = () => SoundOptimizations.GetSoundOptimizations().Settings,
```
This dictionary is the **explicit registry** — no reflection, no naming conventions. The comment in the file says so verbatim: *"To add a new feature, add a single entry here."*

### Step 4 — If it needs a custom apply/discovery path, write an `ISpecialSettingHandler`

`src/Winhance.Core/Features/Common/Interfaces/ISpecialSettingHandler.cs`:
```csharp
public interface ISpecialSettingHandler
{
    Task<bool> TryApplySpecialSettingAsync(
        SettingDefinition setting, object value,
        bool additionalContext = false,
        ISettingApplicationService? settingApplicationService = null);

    // Default impl returns empty — override ONLY if you self-filter and return raw values.
    Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(
        IEnumerable<SettingDefinition> settings)
        => Task.FromResult(new Dictionary<string, Dictionary<string, object?>>());
}
```

Register in **two** places in `src/Winhance.UI/Features/Common/Extensions/DI/SettingServicesExtensions.cs`:

```csharp
// 1. Apply-time dispatcher: settingId → handler
services.AddSingleton<ISpecialSettingHandlerRegistry>(sp =>
    new SpecialSettingHandlerRegistry(new Dictionary<string, ISpecialSettingHandler>
    {
        [SettingIds.PowerPlanSelection] = sp.GetRequiredService<PowerService>(),
        [SettingIds.UpdatesPolicyMode]  = sp.GetRequiredService<UpdateService>(),
        [SettingIds.ThemeModeWindows]   = sp.GetRequiredService<ThemeWallpaperApplier>(),
    }));

// 2. Discovery registry — ONLY handlers that override DiscoverSpecialSettingsAsync
services.AddSingleton<ISpecialDiscoveryRegistry>(sp =>
    new SpecialDiscoveryRegistry(new List<ISpecialSettingHandler>
    {
        sp.GetRequiredService<PowerService>(),
        sp.GetRequiredService<UpdateService>(),
    }));
```

Current examples: `src/Winhance.Infrastructure/Features/Optimize/Services/PowerService.cs`, `.../UpdateService.cs`, `src/Winhance.Infrastructure/Features/Customize/Services/ThemeWallpaperApplier.cs`.

### Step 5 — Add localization keys to `en.json`

Keys are **auto-derived** (`src/Winhance.Core/Features/Common/Localization/SettingLocalizationKeys.cs`), so for a new setting you only add:
```
"Setting_<Id>_Name", "Setting_<Id>_Description",
"Setting_<Id>_Option_0" … "Setting_<Id>_Option_<n>"   (Selection only)
"Setting_<Id>_OptionTooltip_<n>", "Setting_<Id>_OptionWarning_<n>"   (optional)
"SettingGroup_<GroupName compacted>"  and/or "SettingGroup_<GroupName snake_case>"
```
`LocalizationKeyReferenceTests` (integration) fails the build if a key a setting requests is missing from `en.json`.

### Step 6 — Satisfy the catalog invariant

`src/Winhance.Core/Features/Common/Validation/SettingCatalogValidator.cs` (tested by `tests/Winhance.Core.Tests/Validation/SettingCatalogValidatorTests.cs`). For every `InputType.Selection`:

| Category | Trigger | Rule |
|---|---|---|
| **Dynamic** | `Recommendation.LoadDynamicOptions == true` | no static check |
| **PowerCfg** | `PowerCfgSettings?.Count > 0` | must **not** set `ComboBoxOption.IsRecommended`/`IsDefault` — recommendation lives on `PowerRecommendation.RecommendedOptionAC/DC` |
| **Subjective** | `IsSubjectivePreference == true` | at most one `IsRecommended` and at most one `IsDefault` |
| **Standard** | everything else | **exactly one** `IsRecommended` **and exactly one** `IsDefault` |

**Do this and the setting is done.** No view code, no wiring, no page changes — it appears in the feature's detail page automatically.

---

## 3. How to add a new PAGE and wire it into navigation

### 3.1 A top-level page (nav destination)

1. **Create the Page + code-behind** in `src/Winhance.UI/Features/<Domain>/`:
   `Features/<Domain>/<Domain>Page.xaml` + `.xaml.cs`.
2. **Set `x:DefaultBindMode="OneWay"` and `NavigationCacheMode="Enabled"`** on the `Page` root (Standard).
3. **Resolve the ViewModel from DI in the constructor** — pages never `new` up a ViewModel:
   ```csharp
   public sealed partial class PrivacyOptimizePage : Page
   {
       public OptimizeViewModel ViewModel { get; }

       public PrivacyOptimizePage()
       {
           this.InitializeComponent();
           ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
       }

       protected override void OnNavigatedTo(NavigationEventArgs e)
       {
           base.OnNavigatedTo(e);
           if (e.Parameter is string searchText && !string.IsNullOrWhiteSpace(searchText))
               ViewModel.SearchText = searchText;          // cross-page search handoff
           _ = ViewModel.PrivacyViewModel.RefreshSettingStatesAsync();
       }
   }
   ```
   (`src/Winhance.UI/Features/Optimize/Pages/PrivacyOptimizePage.xaml.cs` — the reference implementation.)
4. **Register in the nav router** — `src/Winhance.UI/Helpers/NavigationRouter.cs`. Add the tag **to both** dictionaries (they must stay symmetric):
   ```csharp
   private static readonly Dictionary<string, Type> TagToPageType = new()
   {
       ["Settings"] = typeof(SettingsPage),
       ["Optimize"] = typeof(OptimizePage),
       …
       ["YourNewTag"] = typeof(YourNewPage),      // ← add
   };

   private static readonly Dictionary<string, string> PageTypeNameToTag = new()
   {
       [nameof(SettingsPage)] = "Settings",
       …
       [nameof(YourNewPage)] = "YourNewTag",      // ← add (keyed by TYPE NAME)
   };
   ```
5. **Add the sidebar button** — `src/Winhance.UI/Features/Common/Controls/NavSidebar.xaml`:
   ```xml
   <local:NavButton x:Name="YourNewButton"
                    Header="{…}"  Icon="…"
                    NavigationTag="YourNewTag"
                    Clicked="NavButton_Clicked"/>
   ```
   The `NavSidebar.SelectedTag` DependencyProperty (defined in `NavSidebar.xaml.cs`) drives selection highlighting generically.
6. **Add the localized nav label** — `NavViewModel.NavYourNewText` in `MainWindowViewModel` plus `"Nav_YourNew"` in `en.json`, and an `OnPropertyChanged(nameof(NavYourNewText))` inside `MainWindowViewModel.OnLanguageChanged`.

### 3.2 A sub-page inside an existing section page (Optimize / Customize / AdvancedTools)

Sub-pages are **not** in the router. They are pushed into a private inner `Frame` by the host page.

- **Optimize / Customize:** add a `SectionInfo` entry to the `Sections` static list and a named property on the host VM, and add an overview `SettingsCard` + a breadcrumb flyout `MenuFlyoutItem` in the page XAML. See `OptimizeViewModel.Sections` (`src/Winhance.UI/Features/Optimize/ViewModels/OptimizeViewModel.cs:24`) and the six hard-coded flyout buttons in `OptimizePage.xaml:133-200`.
- **AdvancedTools:** `AdvancedToolsPage.NavigateToSection(sectionKey)` maps a string key to a `Page` type and calls `InnerContentFrame.Navigate(pageType)`; `InnerContentFrame_Navigated` maps the resulting `e.SourcePageType.Name` back to a section key; `UpdateContentVisibility()` swaps `OverviewContent` vs `InnerContentFrame`. See `src/Winhance.UI/Features/AdvancedTools/AdvancedToolsPage.xaml.cs`.

### 3.3 A settings section inside an existing feature

Almost free:
1. Add the `FeatureId` constant + `FeatureDefinitions` entry.
2. Add the static `Get<Feature>Customizations()` catalog returning a `SettingGroup`.
3. Add one entry to `CompatibleSettingsRegistry.GetKnownFeatureProviders()`.
4. Create a ViewModel: `public partial class XOptimizationsViewModel : BaseSettingsFeatureViewModel, IOptimizationFeatureViewModel` with `public override string ModuleId => FeatureIds.X;` and `protected override string GetDisplayNameKey() => "Feature_X_Name";` — the **entire body** is the constructor, which just forwards the six base dependencies (`src/Winhance.UI/Features/Optimize/ViewModels/SoundOptimizationsViewModel.cs` is 24 lines total).
5. Register in `UIServicesExtensions.AddUIServices()`:
   ```csharp
   services.AddSingleton<IOptimizationFeatureViewModel, XOptimizationsViewModel>();
   ```
6. Add the `SectionInfo` + named property + overview card + breadcrumb item.

---

## 4. How to add a new Infrastructure service

1. **Interface** in `src/Winhance.Core/Features/<Domain>/Interfaces/I<Name>Service.cs`. Keep it async-first, `Task`-returning, and pass `CancellationToken cancellationToken = default` on long operations plus `IProgress<TaskProgressDetail>? progress = null` for anything the user waits on.
2. **Implementation** in `src/Winhance.Infrastructure/Features/<Domain>/Services/<Name>Service.cs`.
3. **Register it** in `src/Winhance.Infrastructure/Extensions/DI/InfrastructureServicesExtensions.cs` — `services.AddSingleton<INameService, NameService>();`. **Everything is Singleton.** Use a factory lambda only when the concrete type must be shared with another interface (`TaskProgressService` → `ITaskProgressService` + `IMultiScriptProgressService`; `PowerService` → `IPowerService`).
4. **Domain-specific services** go in the feature-specific extension instead: `AddCustomizationServices()`, `AddOptimizationServices()`, `AddSoftwareAppServices()` — all in `src/Winhance.UI/Features/Common/Extensions/DI/SettingServicesExtensions.cs`. UI-layer services go in `AddUIServices()` (same folder, `UIServicesExtensions.cs`).
5. **Composition root** — `src/Winhance.UI/Features/Common/Extensions/DI/CompositionRoot.cs`:
   ```csharp
   services.AddInfrastructureServices()   // order matters
           .AddSettingServices()
           .AddUIServices();
   ```
   The re-registration trick: `AddInfrastructureServices` `TryAddSingleton`s *empty* `ISpecialDiscoveryRegistry` / `ISpecialSettingHandlerRegistry` so the Infrastructure container can stand alone in integration tests; `AddSettingServices` (running later) replaces them with the real handler set. **Document this whenever you touch those two.**

### Constructor style — both styles are present, pick by era

- **Primary-constructor DI** for most new services:
  ```csharp
  public class ExternalAppsService(
      ILogService logService,
      IWinGetPackageInstaller winGetPackageInstaller,
      /* … */ IChangeHistoryService changeHistory) : IExternalAppsService
  ```
  (`src/Winhance.Infrastructure/Features/SoftwareApps/Services/ExternalAppsService.cs`)
- **Classic field-assignment constructor** for older/UI-adjacent types:
  ```csharp
  public class XService : IXService { private readonly ILogService _logService;
      public XService(ILogService logService) { _logService = logService; … } }
  ```

### Null-guarding convention in ViewModels

Base-class dependencies throw; optional ones are `?.`:
```csharp
_settingsLoadingService = settingsLoadingService ?? throw new ArgumentNullException(nameof(settingsLoadingService));
_privateUserPreferencesService = preferencesService;      // optional — no throw
```

---

## 5. ViewModel patterns

### 5.1 Toolkit MVVM source generators

- `public partial class Foo : ObservableObject, IDisposable` — **always `partial`** (needed by the generators).
- Properties: `[ObservableProperty] public partial string Name { get; set; }` — note the **`partial` on the property too**; that is .NET 10 / Mvvm 8.4 syntax.
- Cross-property notification: `[NotifyPropertyChangedFor(nameof(Other))]`.
- Derived notifications: `partial void On<Prop>Changed(<T> value) { OnPropertyChanged(nameof(Computed)); }`.
- Initialise generated-property defaults **in the constructor body** (before subscriptions) — a repeated pattern with an explanatory comment:
  ```csharp
  SearchText = string.Empty;
  IsWindowsAppsTabSelected = true;
  ```
- Commands: `[RelayCommand] private async Task DoThingAsync()` → generates `DoThingCommand`.
- Backward-compatible commands are exposed as **forwarding expression-bodied properties** when XAML hasn't migrated yet:
  ```csharp
  public IAsyncRelayCommand SelectIsoFileCommand => Step1.SelectIsoFileCommand;
  ```

### 5.2 ViewModel lifetime

| Lifetime | Which |
|---|---|
| **Singleton** | `OptimizeViewModel` + the 6 `*OptimizationsViewModel`, `CustomizeViewModel` + the 4 `*CustomizationsViewModel`, `AdvancedToolsViewModel`, `WimUtilViewModel`, `SoftwareAppsViewModel`, `WindowsAppsViewModel`, `ExternalAppsViewModel`, `MainWindowViewModel` + its 4 child VMs, `SettingsViewModel`'s page siblings. **Singletons preserve state across inner navigation.** |
| **Transient** | `SettingsViewModel`, `AutounattendGeneratorViewModel` |
| **Interface aliases for the same singleton** | `services.AddSingleton<WindowsAppsViewModel>(); services.AddSingleton<IWindowsAppsItemsProvider>(sp => sp.GetRequiredService<WindowsAppsViewModel>());` |

### 5.3 Subscribing / disposing

- **Event handlers**: subscribe in the constructor, unsubscribe in `Dispose()` behind an `if (_disposed) return; _disposed = true;` guard.
- **EventBus**: store the token and dispose it — `_settingAppliedSubscription = _eventBus.Subscribe<SettingAppliedEvent>(OnSettingApplied);`.
- **Defer subscriptions out of the constructor.** `BaseSettingsFeatureViewModel.SubscribeToEvents()` is called from `LoadSettingsAsync` on first load, guarded by `_isSubscribed`, precisely so DI construction has no side effects.
- `ILocalizationService.LanguageChanged` → `OnLanguageChanged` → re-raise `PropertyChanged` for every localized string property. Every ViewModel does this.

### 5.4 The settings-feature VM skeleton

`BaseSettingsFeatureViewModel` (`src/Winhance.UI/Features/Optimize/ViewModels/BaseSettingsFeatureViewModel.cs`) owns:
- `ObservableCollection<SettingItemViewModel> Settings`, `ObservableCollection<SettingsGroup> GroupedSettings`
- `SemaphoreSlim _loadingSemaphore = new(1, 1)` guarding `LoadSettingsAsync` against concurrent loads
- `volatile Dictionary<string, SettingItemViewModel> _settingsById` + `_childrenByParentId` for event routing
- **Debounced search**: `Interlocked.Exchange(ref _searchDebounceTokenSource, newCts)` + `token.ThrowIfCancellationRequested()` inside a `Task.Run`
- `EventBus` subscriptions: `SettingAppliedEvent`, `FilterStateChangedEvent`, `ReviewModeExitedEvent`, `BuilderModeExitedEvent` — each triggers `_dispatcherService.RunOnUIThread(...)` and then publishes `SettingsRefreshedEvent(DisplayName)` so pages can re-apply badge/quick-set state
- `RefreshSettingsForFilterChangeAsync()` disposes every `IDisposable` SettingItemViewModel, clears the collection, and reloads

### 5.5 The canonical apply flow (this is the pattern to copy)

`SettingItemViewModel.HandleToggleAsync` (`src/Winhance.UI/Features/Optimize/ViewModels/SettingItemViewModel.cs:1348`):

```csharp
private async Task HandleToggleAsync(bool newValue, bool resetToDefault = false)
{
    if (IsApplying || _isUpdatingFromEvent || SettingDefinition == null) return;   // re-entrancy guard
    if (newValue == IsSelected) return;                                          // no-op guard

    if (IsBuilderMode)
    {
        // Builder mode: record the desired state only — never apply to the system,
        // never confirm, never show a restart banner.
        IsSelected = newValue;
        ComputeBadgeState();
        _applicationModeService?.RecordBuilderEdit(new BuilderEdit { SettingId = SettingId, InputType = InputType, IsSelected = newValue });
        return;
    }

    try
    {
        var (confirmed, checkboxChecked) = await HandleConfirmationIfNeededAsync(newValue);
        if (!confirmed) { OnPropertyChanged(nameof(IsSelected)); return; }        // revert

        IsApplying = true;
        _logService.Log(LogLevel.Info, $"Toggling setting: {SettingId} to {newValue}");

        var result = await _settingApplicationService.ApplySettingAsync(
            new ApplySettingRequest { SettingId = SettingId, Enable = newValue,
                                      ResetToDefault = resetToDefault, CheckboxResult = checkboxChecked });

        if (!result.Success)
        {
            _logService.Log(LogLevel.Warning, $"Setting '{SettingId}' apply failed: {result.ErrorMessage}. Reverting UI state.");
            OnPropertyChanged(nameof(IsSelected));
            return;
        }

        IsSelected = newValue;
        ComputeBadgeState();
        ShowRestartBannerIfNeeded();
    }
    catch (Exception ex)
    {
        _logService.Log(LogLevel.Error, $"Error toggling setting {SettingId}: {ex.Message}");
        OnPropertyChanged(nameof(IsSelected));                                   // revert
    }
    finally { IsApplying = false; }
}
```

Five invariants in every apply handler:
1. **Re-entrancy guard** (`IsApplying || _isUpdatingFromEvent`)
2. **Builder-mode short-circuit** that records intent instead of applying
3. **Confirmation before apply** (driven by `SettingDefinition.RequiresConfirmation`), revert on decline
4. **`OperationResult` check → revert on failure** (never assume success)
5. **`IsApplying = true … finally { IsApplying = false; }`**

`HandleValueChangedAsync` additionally **queues** into `_pendingValue` when an apply is already in flight rather than dropping the user's newest input.

### 5.6 Threading

`IDispatcherService` (UI) is the **only** way to touch the UI from background code:
- `RunOnUIThread(Action)` — inline if already on the UI thread
- `RunOnUIThreadAsync(Func<Task>)`
- `RunOnUIThreadWithContextAsync(Func<Task>)` — installs a `DispatcherQueueSynchronizationContext` for the duration so **every** `await` resumes UI-thread-affine. Use for multi-stage UI work triggered from a bare `TryEnqueue` callback. `WindowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync` uses it plus a `lock (_loadGate)` + `_loadTask ??=` to make the load exactly-once under a two-path startup race.
- `Initialize(DispatcherQueue)` **must** be called from the `MainWindow` constructor after window creation, before anything uses it.

Infrastructure code is UI-agnostic and uses `.ConfigureAwait(false)` throughout.

---

## 6. Error handling

### 6.1 The three mechanisms, all of them required

| Mechanism | Where | Purpose |
|---|---|---|
| **`OperationResult` / `OperationResult<T>`** (`src/Winhance.Core/Features/Common/Models/OperationResult.cs`) | All Infrastructure services | Return data-carrying failures. `Succeeded(result)`, `Failed(msg[, ex])`, `Cancelled(msg)`, `ConfirmationRequired(msg)`, `DeferredSuccess(result, infoMessage)` (Success=true + an informational message — used for scheduled-task-deferred removals). |
| **Domain exceptions** | `src/Winhance.Core/Features/Common/Exceptions/` | Only two: `InsufficientDiskSpaceException`, `ExecutionPolicyException`. Both are *caught and converted* to user-facing outcomes by their callers, never surfaced raw. |
| **try/catch with `[Type]Log*` + a user dialog** | All ViewModels | Catch → log → `IDialogService.Show*Async` → revert UI state. |

### 6.2 Cancellation is a first-class path

Always `catch (OperationCanceledException)` **separately**, before the general `catch`, and translate it into `OperationResult<T>.Cancelled(...)`. Example: `AppInstallationService.InstallAppsAsync`.

### 6.3 Long-running shell-outs

- `IProcessExecutor.ExecuteWithStreamingAsync(file, args, onOutputLine, onErrorLine, ct)` — line-by-line stdout/stderr for long tools (DISM, Chocolatey, winget); `CreateNoWindow=true`, `UseShellExecute=false`, UTF-8.
- `IPowerShellRunner.RunScriptInMemoryAsync` — `powershell.exe -EncodedCommand <UTF-16-LE base64>`, no temp file; use for toggle scripts under ~24 KB encoded.
- `IPowerShellRunner.RunScriptAsync` / `RunScriptFileAsync` — temp/pre-written file for anything larger.
- `IDismProcessRunner` — wraps DISM with progress, plus `CheckDiskSpaceAsync(path, requiredBytes, operationName)`.
- `WinGet/Utilities/ConPtyProcess.cs` — ConPTY so `winget.exe` renders a live progress stream.
- `WinGetExitCodes.cs` — exit code → human message mapping.

### 6.4 Resource cleanup on failure

`IsoService.ExtractIsoAsync` is the reference: it tracks `isoMounted`, and dismounts in **all three** paths — success, `OperationCanceledException`, and generic `catch` — each with its own `LogWarning` on dismount failure.

---

## 7. Logging

`ILogService` (`src/Winhance.Core/Features/Common/Interfaces/ILogService.cs`) with `LogInformation` / `LogWarning` / `LogError(msg, ex?)` / `LogDebug` / `Log(level, msg, ex?)` / `GetLogPath()` / `StartLog()`.

**Implementation:** `src/Winhance.Core/Features/Common/Services/LogService.cs` — writes to `%ProgramData%\Winhance\Logs\`, with `CleanupOldLogs(dir, maxAgeDays: 30, maxFiles: 50)` on startup.

**Two loggers, two phases:**
- **`StartupLogger`** (static, `src/Winhance.Core/Features/Common/Services/StartupLogger.cs`) — a plain file trace used by `Program.Main`, `MainWindow`, `NavigationRouter`, `StartupOrchestrator` **before/instead of** DI-backed logging. Call as `StartupLogger.Log("ComponentName", "message")`.
- **`ILogService`** — everything after the container is built.

**Message conventions:**
- Infrastructure: free-form, but informative — `$"Successfully installed app '{app.Id}'"`, `$"Install failed for '{item.Name}' via {src}/{pkgId}: {lastResult.FailureReason}"`.
- ViewModels: **prefix with the type in brackets** — `$"[SettingItemViewModel] …"`, `$"[MainWindowViewModel] …"`, `$"[ThemeWallpaperApplier] …"`.
- `BaseSettingsFeatureViewModel` uses `LogPrefix`-style strings: `$"Refreshing settings for {DisplayName} due to filter change"`.
- External tools are line-logged verbatim: `$"[choco] {line}"`, `$"[choco-bootstrap] {line}"`, `$"[FireAndForget] Unobserved exception in {callerName}: {ex.Message}"`.

**Never log secrets.** Nothing sensitive exists in this app; preserve that.

---

## 8. Elevation / admin patterns

The app **always** runs elevated (`app.manifest` → `requireAdministrator`). Implications encoded in the code:

1. **WinRT file pickers break under elevation** → use the COM `IFileOpenDialog`/`IFileSaveDialog` P/Invokes in `src/Winhance.UI/Features/Common/Helpers/Win32FileDialogHelper.cs`, surfaced as `IFilePickerService` (which resolves the window via `IMainWindowProvider` so no `Window` leaks into ViewModels).
2. **Over-the-Shoulder (OTS) elevation** — the user may have consented with *different* credentials. `IInteractiveUserService` detects this and exposes `IsOtsElevation`, `InteractiveUserSid`, `InteractiveUserName`, `GetInteractiveUserFolderPath(SpecialFolder)`, `HasInteractiveUserToken`, `RunProcessAsInteractiveUserAsync(...)`, `LaunchProcessAsInteractiveUser(...)` (token stolen from `explorer.exe` via `src/Winhance.Core/Features/Common/Native/UserTokenApi.cs`). Settings that must apply to the *logged-in* user (HKCU writes, Explorer, per-user AppX) use this. The shell shows an informational InfoBar when OTS is detected (`MainWindowViewModel.InitializeOtsInfoBar`).
3. **WinGet COM** needs the **StandardFactory** with `allowLowerTrustRegistration: true` — `WindowsPackageManagerElevatedFactory` (via `winrtact.dll`) hangs in self-contained mode (microsoft/winget-cli#4377). See the comment block in `WinGetComSession.EnsureComInitialized`.
4. **Scheduled tasks** are created with `LogonType = 5` (run whether user is logged on or not) so deferred removals actually run; `ScheduledTaskService.CreateUserLogonTaskAsync` targets a specific username with `deleteAfterRun`.
5. **Execution-policy blocks** are expected, not exceptional: `BloatRemovalService` catches `ExecutionPolicyException` and returns `RemovalOutcome.DeferredToScheduledTask` with the terminal text *"Execution policy blocked script — removal deferred to scheduled task"*.
6. **No runtime UAC elevation path** — the process is elevated from launch. Never add a `runas` relaunch.

---

## 9. Navigation, events, and cross-cutting plumbing

**Event bus** — `IEventBus` (`src/Winhance.Infrastructure/Features/Common/Events/EventBus.cs`):
```csharp
_eventBus.Publish(new SettingAppliedEvent(settingId, isEnabled, value));
_eventBus.Subscribe<SettingAppliedEvent>(OnSettingApplied);              // sync
await _eventBus.SubscribeAsync<FilterStateChangedEvent>(OnFilterStateChangedAsync);  // async
```
Token-based (`ISubscriptionToken`), dispose to unsubscribe. Declared events:
`SettingAppliedEvent`; UI events `SettingsRefreshedEvent`, `FilterStateChangedEvent`, `TooltipUpdatedEvent`; `PowerPlanChangedEvent`; and the two mode-exit events `BuilderModeExitedEvent`, `ReviewModeExitedEvent`.

**Prefer the EventBus over direct service references** for anything that crosses a ViewModel boundary — this is what keeps the feature ViewModels from depending on each other.

**Single source of truth for mode** — `ConfigReviewService` implements six interfaces from one singleton; `SetMode` is private; nothing else assigns `CurrentMode`.

---

## 10. Style / formatting rules

- **No `.editorconfig`, no StyleCop, no Roslyn analyzer package, no `dotnet format` config, no central package management.** Formatting is whatever VS produces: 4 spaces, Allman braces, `_camelCase` private fields, **`PascalCase` private readonly fields** in Infrastructure services (`private readonly ILogService logService;` — primary-constructor style), file-scoped namespaces.
- **Always add a header comment to non-obvious csproj lines.** The Winhance csproj files are heavily commented with the *reason* for each pin (CVE floors, WinUI version races, SDK workarounds). Reproduce that discipline.
- **Comments explain *why*, not *what*.** Long, specific rationale comments are the house style — e.g. the 12-line block justifying `CETCompat=false`, the block on `LocalizationService` explaining why `CurrentCulture` is held at `InvariantCulture`, the `ReviewModeFilter` doc-comment explaining the drift bug (issue #665) the design avoids.
- **XML doc comments on Core interfaces and models**, `/// <summary>` with `<param>`/`<returns>`; `<remarks>` for the non-obvious invariants. UI-layer types get summaries where the intent isn't obvious.
- **`sealed`** on concrete classes; `static` on catalogs and pure helpers; `partial` on anything the MVVM generators touch.
- **`ConfigureAwait(false)`** in every Infrastructure `await`.
- **Private fields exposed to XAML via public expression-bodied properties** is an accepted transitional pattern (heavily used in `WimUtilViewModel` with an explicit "backward-compatible XAML bindings" comment).

---

## 11. Test conventions

- xUnit `[Fact]`, `FluentAssertions`, `Moq`. **No integration-test framework beyond the separate `Winhance.IntegrationTests` project** (which is still xUnit — just a different project with real-I/O tests).
- Class = `<Type>Tests`; method = `Member_Condition_ExpectedResult`; `// Arrange` / `// Act` / `// Assert` markers.
- Standard mock setup (copy this):
  ```csharp
  _mockLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
                           .Returns((string key) => key);          // key passthrough
  _mockDispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
                         .Callback<Action>(action => action());    // synchronous
  ```
- Private `CreateViewModel()` factory method per test class.
- **One test file per production type**, mirroring the folder layout.
- Invariant tests are first-class: `SettingCatalogValidatorTests`, `LocalizationJsonValidityTests`, `LocalizationKeyReferenceTests`, `IconCoverageTests`, `SettingIdsTests`, `ConfigSchemaValidationTests`.
- Offline-safe: network-dependent tests use checked-in snapshots (`tests/Winhance.Core.Tests/Assets/package-icons-manifest.json`).

---

*Convention analysis: 2026-10-05*