using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinUI.Framework.Services;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Native;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Core.Features.Common.Events;
using AkariTool.Core.Interfaces;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Declarative replacement for the delegate-based ToggleTweakViewModel/DropdownTweakViewModel.
/// Reads its state from the system via <see cref="ISettingStateReader"/>, applies changes via
/// <see cref="ISettingOperationExecutor"/>, and computes its own info-badge row from the
/// <see cref="SettingDefinition"/>. No TweakRegistry / page wiring yet (Phase 3b-ii).
/// </summary>
public sealed partial class SettingItemViewModel : ObservableObject, ISettingRowViewModel
{
    private readonly ISettingStateReader _stateReader;
    private readonly ISettingOperationExecutor _executor;
    private readonly TweakDialogs _dialogs;
    private readonly IPowerPlanComboBoxService? _powerPlanComboBoxService;
    private readonly IPowerService? _powerService;
    private readonly IReadOnlyList<SettingDefinition>? _powerCatalog;
    private readonly INewBadgeService? _newBadgeService;
    private readonly ISettingsService? _settingsService;
    private readonly ISettingDependencyResolver? _dependencyResolver;
        private IReadOnlyList<SettingDefinition>? _dependencyContext;

        // ─── Status Banner (Winhance 1:1 port) ──────────────────────────────────────
        private readonly SettingStatusBannerManager _statusBannerManager;

        // ─── Technical Details (Winhance 1:1 port) ──────────────────────────────────
        private readonly TechnicalDetailsManager _technicalDetailsManager;

        private bool _suppress;
        private int _lastIndex = -1;

    public SettingItemViewModel(
                SettingDefinition definition,
                ISettingStateReader stateReader,
                ISettingOperationExecutor executor,
                TweakDialogs dialogs,
                bool hasBattery = false,
                IPowerPlanComboBoxService? powerPlanComboBoxService = null,
                IPowerService? powerService = null,
                IReadOnlyList<SettingDefinition>? powerCatalog = null,
                INewBadgeService? newBadgeService = null,
                ISettingsService? settingsService = null,
                ISettingDependencyResolver? dependencyResolver = null,
                ILocalizationService? localizationService = null,
                IEventBus? eventBus = null,
                IRegeditLauncher? regeditLauncher = null,
                IDispatcherService? dispatcherService = null,
                ILogService? logService = null)
            {
                Definition = definition;
                _stateReader = stateReader;
                _executor = executor;
                _dialogs = dialogs;
                _powerPlanComboBoxService = powerPlanComboBoxService;
                _powerService = powerService;
                _powerCatalog = powerCatalog;
                _newBadgeService = newBadgeService;
                _settingsService = settingsService;
                _dependencyResolver = dependencyResolver;
                HasBattery = hasBattery;

            // Winhance parity: rows tagged AddedInVersion light up as NEW until the
            // user's baseline version passes. Recomputed after warm-up initializes
            // the badge service (see SettingPageWarmUp).
            IsNew = _newBadgeService?.IsSettingNew(Definition.AddedInVersion, Definition.Id) == true;

            // Winhance parity: RequiresAdvancedUnlock rows start locked until the
            // one-time warning dialog is accepted (persisted via the prefs store).
            if (Definition.RequiresAdvancedUnlock && _settingsService != null)
                IsLocked = !_settingsService.Get(AdvancedPowerSettingsUnlocked, false);

            UnlockCommand = new AsyncRelayCommand(HandleUnlockAsync);

            Options = Definition.ComboBox?.Options?.Select(o => o.DisplayName).ToArray()
                      ?? Array.Empty<string>();

            // ─── Status Banner Manager (Winhance 1:1 port) ──────────────────────────────
            _statusBannerManager = new SettingStatusBannerManager(localizationService);

            // ─── Technical Details Manager (Winhance 1:1 port) ──────────────────────────
                        _technicalDetailsManager = new TechnicalDetailsManager(
                            () => Definition.Id,
                            newSections =>
                            {
                                TechnicalDetailSections = newSections;
                                OnPropertyChanged(nameof(HasTechnicalDetails));
                                OnPropertyChanged(nameof(ShowTechnicalDetailsBar));
                            },
                            logService,
                            dispatcherService,
                            regeditLauncher,
                            eventBus,
                            localizationService,
                            new TechnicalDetailLabels
                {
                    Path = localizationService?.GetString("TechnicalDetails_Path") ?? "Path",
                    Value = localizationService?.GetString("TechnicalDetails_Value") ?? "Value",
                    Current = localizationService?.GetString("TechnicalDetails_Current") ?? "Current",
                    Recommended = localizationService?.GetString("TechnicalDetails_Recommended") ?? "Recommended",
                    Default = localizationService?.GetString("TechnicalDetails_DefaultValue") ?? "Default",
                    ValueNotExist = localizationService?.GetString("TechnicalDetails_ValueNotExist") ?? "doesn't exist",
                    On = localizationService?.GetString("Common_On") ?? "On",
                    Off = localizationService?.GetString("Common_Off") ?? "Off",
                    SectionRegistry = localizationService?.GetString("TechnicalDetails_Section_Registry") ?? "Registry Changes",
                    SectionScheduledTasks = localizationService?.GetString("TechnicalDetails_Section_ScheduledTasks") ?? "Scheduled Tasks",
                    SectionPowerSettings = localizationService?.GetString("TechnicalDetails_Section_PowerSettings") ?? "Power Settings",
                    SectionScripts = localizationService?.GetString("TechnicalDetails_Section_Scripts") ?? "PowerShell Scripts",
                    SectionRegContent = localizationService?.GetString("TechnicalDetails_Section_RegContent") ?? "Registry Content",
                    SectionDependencies = localizationService?.GetString("TechnicalDetails_Section_Dependencies") ?? "Depends On",
                    ScriptOnEnable = localizationService?.GetString("TechnicalDetails_Script_OnEnable") ?? "On Enable",
                    ScriptOnDisable = localizationService?.GetString("TechnicalDetails_Script_OnDisable") ?? "On Disable",
                    ScriptOnApply = localizationService?.GetString("TechnicalDetails_Script_OnApply") ?? "On Apply",
                    RegContentOnEnable = localizationService?.GetString("TechnicalDetails_RegContent_OnEnable") ?? "On Enable",
                    RegContentOnDisable = localizationService?.GetString("TechnicalDetails_RegContent_OnDisable") ?? "On Disable",
                    DependencyEquals = localizationService?.GetString("TechnicalDetails_Dependency_Equals") ?? "=",
                    DependencyNotEquals = localizationService?.GetString("TechnicalDetails_Dependency_NotEquals") ?? "≠",
                    PowerCfgSubgroup = localizationService?.GetString("TechnicalDetails_PowerCfg_Subgroup") ?? "Subgroup",
                    PowerCfgSetting = localizationService?.GetString("TechnicalDetails_PowerCfg_Setting") ?? "Setting"
                });

            OpenRegeditCommand = _technicalDetailsManager.OpenRegeditCommand;

            if (IsPowerPlanSetting)
            {
                DeletePlanCommand = new AsyncRelayCommand<PowerPlanComboBoxOption>(DeletePlanAsync);
                RefreshPlanOptions();
            }
            else
            {
                RefreshFromSystem();
            }
        }

    // ── Static surface ─────────────────────────────────────────────────────────
    public SettingDefinition Definition { get; }
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public InputType InputType => Definition.InputType;
    public string[] Options { get; }

    /// <summary>
    /// True for the Power Plan row (<c>power-plan-selection</c>): a Selection
    /// setting whose options are loaded at runtime from the system's power plans
    /// (Recommendation.LoadDynamicOptions), not from static ComboBox metadata.
    /// Rendered by a bespoke PowerPlanComboBox template, not the dropdown template.
    /// </summary>
    public bool IsPowerPlanSetting =>
        InputType == InputType.Selection && Definition.Recommendation?.LoadDynamicOptions == true;

    /// <summary>Dynamic Power Plan options backing the bespoke combo row.</summary>
        public ObservableCollection<PowerPlanComboBoxOption> PlanOptions { get; } = new();

        /// <summary>
        /// Delete command for a non-active, installed plan in the Power Plan dropdown.
        /// Null on every other row.
        /// </summary>
        public IAsyncRelayCommand<PowerPlanComboBoxOption>? DeletePlanCommand { get; }

        /// <summary>
        /// Raised after a plan activation / import / delete lands, so the page can
        /// re-read sibling PowerCfg rows (values differ per active plan).
        /// </summary>
        public event Action? PowerPlanChanged;

        // ─── Status Banner properties (Winhance 1:1 port) ───────────────────────────────
        [ObservableProperty]
        public partial string StatusBannerMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial Microsoft.UI.Xaml.Controls.InfoBarSeverity StatusBannerSeverity { get; set; } = Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;

        public bool ShowStatusBanner => !string.IsNullOrEmpty(StatusBannerMessage);

        // ─── Technical Details properties (Winhance 1:1 port) ────────────────────────
        [ObservableProperty]
        public partial bool IsTechnicalDetailsExpanded { get; set; }

        [ObservableProperty]
        public partial IReadOnlyList<TechnicalDetailSection> TechnicalDetailSections { get; set; } = new List<TechnicalDetailSection>();

        public bool HasTechnicalDetails => TechnicalDetailSections.Count > 0;

        public bool ShowTechnicalDetailsBar => HasTechnicalDetails;

        public string TechnicalDetailsLabel => "Technical Details";

        public string OpenRegeditTooltip => "Open in Registry Editor";

        public IAsyncRelayCommand OpenRegeditCommand { get; private set; }

        // ─── Advanced unlock (Winhance port) ────────────────────────────────────────

    private const string AdvancedPowerSettingsUnlocked = "AdvancedPowerSettingsUnlocked";

    // Verbatim Winhance copy (en.json Dialog_AdvancedPowerWarning_Message).
    private const string AdvancedPowerWarningText =
        "This setting is not normally exposed in Windows Power Options and requires registry modifications to access.\n\n" +
        "Modifying it incorrectly may cause:\n" +
        " System instability or unexpected behavior\n" +
        " Performance degradation\n" +
        " Thermal management problems\n" +
        " Settings may not work on all CPU types (modern HWP vs legacy)\n\n" +
        "Only change this if you understand processor power management.\n\n" +
        "Are you sure you want to modify this setting?";

    /// <summary>Raised when the user permanently unlocks advanced power settings,
    /// so the page can unlock every sibling gated row immediately.</summary>
    public event Action? AdvancedUnlockPersisted;

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    public bool RequiresAdvancedUnlock => Definition.RequiresAdvancedUnlock;
    public string ClickToUnlockText => "Click to unlock";
    public IAsyncRelayCommand UnlockCommand { get; }

    private async Task HandleUnlockAsync()
    {
        if (!IsLocked) return;

        var (confirmed, dontShowAgain) = await _dialogs.ConfirmWithCheckboxAsync(
            "Advanced Setting Warning",
            new Microsoft.UI.Xaml.Controls.TextBlock
                { Text = AdvancedPowerWarningText, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
            "Don't show this warning again for advanced power settings",
            "Unlock");
        if (!confirmed) return;

        IsLocked = false;

        if (dontShowAgain && _settingsService != null)
        {
            _settingsService.Set(AdvancedPowerSettingsUnlocked, true);
            AdvancedUnlockPersisted?.Invoke();
        }
    }

    // ── Observable state ───────────────────────────────────────────────────────
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int NumericValue { get; set; }

    [ObservableProperty]
    public partial int AcNumericValue { get; set; }

    [ObservableProperty]
    public partial int DcNumericValue { get; set; }

    [ObservableProperty]
    public partial bool HasBattery { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    public ObservableCollection<BadgePillState> Badges { get; } = new();
    public bool HasBadges => Badges.Count > 0;

    // ── NEW badge (Winhance port) ──────────────────────────────────────────────
    private bool _isNew;
    public bool IsNew
    {
        get => _isNew;
        set
        {
            if (_isNew == value) return;
            _isNew = value;
            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(ShowNewBadge));
        }
    }

    private bool _isNewBadgeGloballyVisible = true;
    /// <summary>Global kill switch, mirrored from INewBadgeService.ShowNewBadges by the page layer.</summary>
    public bool IsNewBadgeGloballyVisible
    {
        get => _isNewBadgeGloballyVisible;
        set
        {
            if (_isNewBadgeGloballyVisible == value) return;
            _isNewBadgeGloballyVisible = value;
            OnPropertyChanged(nameof(IsNewBadgeGloballyVisible));
            OnPropertyChanged(nameof(ShowNewBadge));
        }
    }

    public bool ShowNewBadge => IsNew && IsNewBadgeGloballyVisible;
    public string NewBadgeText => "NEW";

    /// <summary>NumericRange row: min/max/units from the catalog metadata.</summary>
    public int MinValue => Definition.NumericRange?.MinValue ?? 0;
    public int MaxValue => Definition.NumericRange?.MaxValue ?? 100;
    public string Units => Definition.NumericRange?.Units ?? Definition.PowerCfgSettings?.FirstOrDefault()?.Units ?? string.Empty;
    public bool HasUnits => !string.IsNullOrEmpty(Units);

    /// <summary>
    /// True when the setting is a PowerCfg numeric/selection with Separate AC/DC
    /// support (renders the Dual/SingleAC numeric templates instead of the single
    /// spinner). Ported 1:1 from Winhance's SupportsSeparateACDC.
    /// </summary>
    public bool SupportsSeparateACDC =>
        Definition.PowerCfgSettings?.Any(p => p.PowerModeSupport == PowerModeSupport.Separate) == true;

    public bool IsNumericType => InputType == InputType.NumericRange;

    public bool MatchesSearch(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Description.Contains(query, StringComparison.OrdinalIgnoreCase);

    // ── User-driven change plumbing (suppressible) ─────────────────────────────

    /// <summary>
    /// Winhance SettingApplicationService apply order (:107-111 / :124 / :205):
    /// value prerequisites, then dependencies, then the apply itself, then preset
    /// re-sync. The page assigns the resolution universe after Build via
    /// <see cref="SetDependencyContext"/>; without a resolver/context the row
    /// applies directly (pre-4c behavior).
    /// </summary>
    public void SetDependencyContext(IReadOnlyList<SettingDefinition> allSettings)
        => _dependencyContext = allSettings;

    private async Task ApplyWithDependencyPipelineAsync(bool enable, object? value, Func<Task> apply)
    {
        if (_dependencyResolver == null || _dependencyContext == null)
        {
            await apply();
            return;
        }

        await _dependencyResolver.HandleValuePrerequisitesAsync(Definition, Definition.Id, _dependencyContext);
        await _dependencyResolver.HandleDependenciesAsync(Definition.Id, _dependencyContext, enable, value);
        await apply();
        await _dependencyResolver.SyncParentToMatchingPresetAsync(Definition, Definition.Id, _dependencyContext);
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_suppress) return;
        _ = OnUserToggledAsync(value);
    }

    partial void OnSelectedIndexChanged(int value)
    {
        if (_suppress) return;
        _ = OnUserSelectedAsync(value);
    }

    partial void OnNumericValueChanged(int value)
    {
        if (_suppress) return;
        _ = OnUserNumericChangedAsync(value);
    }

    partial void OnAcNumericValueChanged(int value)
    {
        if (_suppress) return;
        _ = OnUserACDCNumericChangedAsync();
    }

    partial void OnDcNumericValueChanged(int value)
    {
        if (_suppress) return;
        _ = OnUserACDCNumericChangedAsync();
    }

    private void SetIsOnSilently(bool v)
    {
        _suppress = true;
        IsOn = v;
        _suppress = false;
    }

    private void SetSelectedIndexSilently(int v)
    {
        _suppress = true;
        SelectedIndex = v;
        _suppress = false;
    }

    private void SetNumericValueSilently(int v)
    {
        _suppress = true;
        NumericValue = v;
        _suppress = false;
    }

    private void SetAcNumericValueSilently(int v)
    {
        _suppress = true;
        AcNumericValue = v;
        _suppress = false;
    }

    private void SetDcNumericValueSilently(int v)
    {
        _suppress = true;
        DcNumericValue = v;
        _suppress = false;
    }

    private async Task OnUserToggledAsync(bool newState)
        {
            if (!await _dialogs.ConfirmWarningAsync(Name, GetToggleWarning(newState)))
            {
                SetIsOnSilently(!newState);
                return;
            }

            await ApplyWithDependencyPipelineAsync(newState, null, async () =>
            {
                await _executor.ApplySettingOperationsAsync(Definition, newState, null);
                RefreshBadges();
                await ApplyBannerAsync(true, isRecommended: newState == SettingDefinitionToggleState.GetRecommendedToggleState(Definition), isDefault: newState == SettingDefinitionToggleState.GetDefaultToggleState(Definition));
                await UpdateTechnicalDetailsAsync();
            });
        }

    private async Task OnUserSelectedAsync(int newIndex)
        {
            if (newIndex < 0) return;

            if (IsPowerPlanSetting)
            {
                await ApplyPowerPlanAsync(newIndex);
                return;
            }

            if (!await _dialogs.ConfirmWarningAsync(Name, null))
            {
                SetSelectedIndexSilently(_lastIndex);
                return;
            }

            _lastIndex = newIndex;
            await ApplyWithDependencyPipelineAsync(true, newIndex, async () =>
            {
                await _executor.ApplySettingOperationsAsync(Definition, true, newIndex);
                RefreshBadges();
                await ApplyBannerAsync(true);
                await UpdateTechnicalDetailsAsync();
            });
        }

    // ── Numeric rows (single spinner + Dual AC/DC spinners) ──────────────────

        /// <summary>
        /// Single NumericRange spinner changed: apply the display-unit int directly.
        /// PowerCfgApplier converts display → system units on the write path.
        /// </summary>
        private Task OnUserNumericChangedAsync(int newValue)
        {
            return ApplyWithDependencyPipelineAsync(true, newValue, async () =>
            {
                await _executor.ApplySettingOperationsAsync(Definition, true, newValue);
                RefreshBadges();
                await ApplyBannerAsync(true);
                await UpdateTechnicalDetailsAsync();
            });
        }

        /// <summary>
        /// AC/DC NumericRange spinner changed: apply both display-unit values as the
        /// {"ACValue","DCValue"} dictionary the PowerCfgApplier Separate branch expects.
        /// On battery-less systems the DC spinner is hidden, so DcNumericValue is
        /// whatever the AC value resolved to — the applier skips the DC write anyway.
        /// </summary>
        private Task OnUserACDCNumericChangedAsync()
        {
            var dict = new Dictionary<string, object?>
            {
                ["ACValue"] = AcNumericValue,
                ["DCValue"] = DcNumericValue,
            };
            return ApplyWithDependencyPipelineAsync(true, dict, async () =>
            {
                await _executor.ApplySettingOperationsAsync(Definition, true, dict);
                RefreshBadges();
                await ApplyBannerAsync(true);
                await UpdateTechnicalDetailsAsync();
            });
        }

    // ── Power Plan row (dynamic options) ─────────────────────────────────────

    /// <summary>
    /// Blocking repopulation of <see cref="PlanOptions"/> + active-index resolution.
    /// Runs in the row ctor (Build, on the warm-up background thread or the page
    /// ctor) and after every successful plan write. The internal services
    /// ConfigureAwait(false), so blocking here cannot deadlock a UI thread.
    /// </summary>
    private void RefreshPlanOptions()
    {
        if (_powerPlanComboBoxService == null || _powerService == null) return;

        var options = _powerPlanComboBoxService.GetPowerPlanOptionsAsync().GetAwaiter().GetResult();
        PlanOptions.Clear();
        foreach (var option in options)
            PlanOptions.Add(option);

        int idx = 0;
        var active = _powerService.GetActivePowerPlanAsync().GetAwaiter().GetResult();
        if (active != null)
        {
            var match = options.FirstOrDefault(o =>
                string.Equals(o.SystemPlan?.Guid, active.Guid, StringComparison.OrdinalIgnoreCase));
            if (match != null) idx = match.Index;
        }

        SetSelectedIndexSilently(idx);
        _lastIndex = idx;
    }

    /// <summary>
    /// User picked a plan: activate it (importing the predefined plan first when it
    /// is not yet on the system), then repopulate the dropdown and ask the page to
    /// re-read sibling PowerCfg rows. Runs on the UI thread — no ConfigureAwait so
    /// the bound-collection mutations stay on the dispatcher.
    /// </summary>
    private async Task ApplyPowerPlanAsync(int newIndex)
    {
        if (_powerService == null || _powerPlanComboBoxService == null) return;

        var options = await _powerPlanComboBoxService.GetPowerPlanOptionsAsync();
        if (newIndex < 0 || newIndex >= options.Count)
        {
            RefreshPlanOptions();
            return;
        }

        var option = options[newIndex];
        if (option.IsActive)
        {
            RefreshPlanOptions();
            return;
        }

        string? guid = option.SystemPlan?.Guid ?? option.PredefinedPlan?.Guid;
        if (string.IsNullOrEmpty(guid))
        {
            RefreshPlanOptions();
            return;
        }

        bool ok;
        if (option.ExistsOnSystem)
        {
            ok = await _powerService.ActivatePowerPlanAsync(guid);
        }
        else if (option.PredefinedPlan != null)
        {
            var import = await _powerService.ImportPowerPlanAsync(option.PredefinedPlan, _powerCatalog);
            ok = import.Success;
        }
        else
        {
            ok = false;
        }

        if (!ok)
        {
            await _dialogs.InfoAsync("Power Plan", $"Could not activate the power plan \"{option.DisplayName}\".");
            RefreshPlanOptions();
            return;
        }

        RefreshPlanOptions();
        PowerPlanChanged?.Invoke();
    }

    /// <summary>
    /// Backup-restore seam: activates/imports the plan matching <paramref name="guid"/>
    /// (by system plan GUID or predefined plan GUID), reusing the same apply path as a
    /// user pick. Returns false when no option carries that GUID, or the activation
    /// failed. Also raises <see cref="PowerPlanChanged"/> so sibling PowerCfg rows
    /// re-read under the newly active plan.
    /// </summary>
    public async Task<bool> ApplyPowerPlanByGuidAsync(string guid)
    {
        if (_powerPlanComboBoxService == null) return false;

        var options = await _powerPlanComboBoxService.GetPowerPlanOptionsAsync();
        int index = -1;
        for (int i = 0; i < options.Count; i++)
        {
            var o = options[i];
            if (string.Equals(o.SystemPlan?.Guid, guid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.PredefinedPlan?.Guid, guid, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }
        if (index < 0) return false;

        var active = options[index];
        if (active.IsActive) return true; // already active — nothing to do

        await ApplyPowerPlanAsync(index);
        return true;
    }

    /// <summary>
    /// Deletes an installed, non-active plan from the Power Plan dropdown. The
    /// active plan and not-installed predefined plans are guarded.
    /// </summary>
    private async Task DeletePlanAsync(PowerPlanComboBoxOption? option)
    {
        if (option == null || _powerService == null) return;

        if (!option.ExistsOnSystem || option.SystemPlan == null)
        {
            await _dialogs.InfoAsync("Power Plan", $"\"{option.DisplayName}\" is not installed on this system.");
            return;
        }

        if (option.IsActive)
        {
            await _dialogs.InfoAsync("Power Plan", "You cannot delete the active power plan.");
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete power plan", $"Delete the power plan \"{option.DisplayName}\"?", "Delete");
        if (!confirmed) return;

        bool ok = await _powerService.DeletePowerPlanAsync(option.SystemPlan.Guid);
        if (!ok)
        {
            await _dialogs.InfoAsync("Power Plan", $"Could not delete the power plan \"{option.DisplayName}\".");
            return;
        }

        RefreshPlanOptions();
        PowerPlanChanged?.Invoke();
    }

    // Per-state toggle warning copy (Akari extension). ON shows EnableWarning, OFF DisableWarning.
    private string? GetToggleWarning(bool newState) => newState ? Definition.EnableWarning : Definition.DisableWarning;

    // ── System-state read-back ─────────────────────────────────────────────────
    public void RefreshFromSystem()
    {
        // The Power Plan row's state is managed by its apply path (RefreshPlanOptions
        // repopulates + resolves the active index). _stateReader has no backing for it
        // and would wrongly reset the selection to -1.
        if (IsPowerPlanSetting) return;

        if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
        {
            SetIsOnSilently(_stateReader.ReadToggleState(Definition));
        }
        else if (InputType == InputType.Selection)
        {
            int idx = _stateReader.ReadSelectionIndex(Definition);
            SetSelectedIndexSilently(idx);
            _lastIndex = idx;
        }
        else if (InputType == InputType.NumericRange)
        {
            var (acValue, dcValue) = _stateReader.ReadNumericValue(Definition);
            if (SupportsSeparateACDC)
            {
                if (acValue.HasValue) SetAcNumericValueSilently(acValue.Value);
                if (dcValue.HasValue) SetDcNumericValueSilently(dcValue.Value);
            }
            else if (acValue.HasValue)
            {
                SetNumericValueSilently(acValue.Value);
            }
        }

        RefreshBadges();
    }

    // ── Quick-set availability ─────────────────────────────────────────────────
    public bool HasRecommendedQuickSet => InputType switch
    {
        InputType.Toggle or InputType.CheckBox =>
            SettingDefinitionToggleState.GetRecommendedToggleState(Definition).HasValue,
        InputType.Selection =>
            Definition.ComboBox?.Options?.Any(o => o.IsRecommended) == true,
        InputType.NumericRange =>
            NumericRecommendedValue.HasValue || AcRecommendedValue.HasValue,
        _ => false,
    };

    public bool HasDefaultQuickSet => InputType switch
    {
        InputType.Toggle or InputType.CheckBox =>
            SettingDefinitionToggleState.GetDefaultToggleState(Definition).HasValue,
        InputType.Selection =>
            Definition.ComboBox?.Options?.Any(o => o.IsDefault) == true,
        InputType.NumericRange =>
            NumericDefaultValue.HasValue || AcDefaultValue.HasValue,
        _ => false,
    };

    // ── Numeric quick-set targets (display units via ConvertFromSystemUnits) ──

    /// <summary>Single spinner recommended value (display units) or null.</summary>
    public int? NumericRecommendedValue
    {
        get
        {
            var pcfg = Definition.PowerCfgSettings?
                .FirstOrDefault(p => p.PowerModeSupport != PowerModeSupport.Separate);
            if (pcfg?.RecommendedValueAC is int rac) return ConvertFromSystemUnits(rac);
            return null;
        }
    }

    /// <summary>Single spinner default value (display units) or null.</summary>
    public int? NumericDefaultValue
    {
        get
        {
            var pcfg = Definition.PowerCfgSettings?
                .FirstOrDefault(p => p.PowerModeSupport != PowerModeSupport.Separate);
            if (pcfg?.DefaultValueAC is int dac) return ConvertFromSystemUnits(dac);
            return null;
        }
    }

    public int? AcRecommendedValue =>
        Definition.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueAC is int rac
            ? ConvertFromSystemUnits(rac) : null;

    public int? AcDefaultValue =>
        Definition.PowerCfgSettings?.FirstOrDefault()?.DefaultValueAC is int dac
            ? ConvertFromSystemUnits(dac) : null;

    public int? DcRecommendedValue =>
        Definition.PowerCfgSettings?.FirstOrDefault()?.RecommendedValueDC is int rdc
            ? ConvertFromSystemUnits(rdc) : null;

    public int? DcDefaultValue =>
        Definition.PowerCfgSettings?.FirstOrDefault()?.DefaultValueDC is int ddc
            ? ConvertFromSystemUnits(ddc) : null;

    /// <summary>Converts a system-unit PowerCfg value to the row's display units.</summary>
    private int ConvertFromSystemUnits(int systemValue) =>
        AkariTool.Infrastructure.Features.Common.Utilities.NumericConversionHelper
            .ConvertFromSystemUnits(systemValue, Definition.NumericRange?.Units);

    // Quick-set tooltips ("Set to Recommended (20)") for the button tooltips.
    public string RecommendedTooltip =>
        (SupportsSeparateACDC ? AcRecommendedValue : NumericRecommendedValue) is int rec
            ? $"Set to Recommended ({rec})" : string.Empty;
    public string DefaultTooltip =>
        (SupportsSeparateACDC ? AcDefaultValue : NumericDefaultValue) is int def
            ? $"Set to Default ({def})" : string.Empty;
    public string RecommendedAcTooltip => AcRecommendedValue is int rec
        ? $"Set to Recommended ({rec})" : string.Empty;
    public string DefaultAcTooltip => AcDefaultValue is int def
        ? $"Set to Default ({def})" : string.Empty;
    public string RecommendedDcTooltip => DcRecommendedValue is int rec
        ? $"Set to Recommended ({rec})" : string.Empty;
    public string DefaultDcTooltip => DcDefaultValue is int def
        ? $"Set to Default ({def})" : string.Empty;

    /// <summary>AC spinner quick-set visibility: needs data (and a battery for DC).</summary>
    public bool HasAcRecommendedQuickSet => AcRecommendedValue.HasValue;
    public bool HasAcDefaultQuickSet => AcDefaultValue.HasValue;
    public bool HasDcRecommendedQuickSet => HasBattery && DcRecommendedValue.HasValue;
    public bool HasDcDefaultQuickSet => HasBattery && DcDefaultValue.HasValue;

    [RelayCommand]
        private async Task ApplyRecommendedAsync()
        {
            if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
            {
                var state = SettingDefinitionToggleState.GetRecommendedToggleState(Definition);
                if (state is not bool value) return;
                await _executor.ApplySettingOperationsAsync(Definition, value, null);
                SetIsOnSilently(value);
                RefreshBadges();
                await ApplyBannerAsync(true, isRecommended: true);
                await UpdateTechnicalDetailsAsync();
            }
            else if (InputType == InputType.Selection)
            {
                int idx = FindOptionIndex(o => o.IsRecommended);
                if (idx < 0) return;
                await _executor.ApplySettingOperationsAsync(Definition, true, idx);
                SetSelectedIndexSilently(idx);
                _lastIndex = idx;
                RefreshBadges();
                await ApplyBannerAsync(true, isRecommended: true);
                await UpdateTechnicalDetailsAsync();
            }
            else if (InputType == InputType.NumericRange)
            {
                if (SupportsSeparateACDC)
                {
                    await ApplyAcNumericAsync(AcRecommendedValue, DcRecommendedValue);
                }
                else if (NumericRecommendedValue is int v)
                {
                    await _executor.ApplySettingOperationsAsync(Definition, true, v);
                    SetNumericValueSilently(v);
                    RefreshBadges();
                    await ApplyBannerAsync(true, isRecommended: true);
                    await UpdateTechnicalDetailsAsync();
                }
            }
        }

        [RelayCommand]
        private async Task ApplyDefaultAsync()
        {
            if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
            {
                var state = SettingDefinitionToggleState.GetDefaultToggleState(Definition);
                if (state is not bool value) return;
                await _executor.ApplySettingOperationsAsync(Definition, value, null);
                SetIsOnSilently(value);
                RefreshBadges();
                await ApplyBannerAsync(true, isDefault: true);
                await UpdateTechnicalDetailsAsync();
            }
            else if (InputType == InputType.Selection)
            {
                int idx = FindOptionIndex(o => o.IsDefault);
                if (idx < 0) return;
                await _executor.ApplySettingOperationsAsync(Definition, true, idx);
                SetSelectedIndexSilently(idx);
                _lastIndex = idx;
                RefreshBadges();
                await ApplyBannerAsync(true, isDefault: true);
                await UpdateTechnicalDetailsAsync();
            }
            else if (InputType == InputType.NumericRange)
            {
                if (SupportsSeparateACDC)
                {
                    await ApplyAcNumericAsync(AcDefaultValue, DcDefaultValue);
                }
                else if (NumericDefaultValue is int v)
                {
                    await _executor.ApplySettingOperationsAsync(Definition, true, v);
                    SetNumericValueSilently(v);
                    RefreshBadges();
                    await ApplyBannerAsync(true, isDefault: true);
                    await UpdateTechnicalDetailsAsync();
                }
            }
        }

    // ── Per-mode numeric quick-set commands (Dual/SingleAC templates) ────────

        [RelayCommand]
        private async Task ApplyAcRecommendedAsync()
        {
            await ApplyAcNumericAsync(AcRecommendedValue, null);
            await ApplyBannerAsync(true, isRecommended: true);
            await UpdateTechnicalDetailsAsync();
        }

        [RelayCommand]
        private async Task ApplyAcDefaultAsync()
        {
            await ApplyAcNumericAsync(AcDefaultValue, null);
            await ApplyBannerAsync(true, isDefault: true);
            await UpdateTechnicalDetailsAsync();
        }

        [RelayCommand]
        private async Task ApplyDcRecommendedAsync()
        {
            await ApplyAcNumericAsync(null, DcRecommendedValue);
            await ApplyBannerAsync(true, isRecommended: true);
            await UpdateTechnicalDetailsAsync();
        }

        [RelayCommand]
        private async Task ApplyDcDefaultAsync()
        {
            await ApplyAcNumericAsync(null, DcDefaultValue);
            await ApplyBannerAsync(true, isDefault: true);
            await UpdateTechnicalDetailsAsync();
        }

        /// <summary>
        /// Applies the given AC/DC display-unit targets (null = keep current) as the
        /// Separate {"ACValue","DCValue"} dictionary, then refreshes badges.
        /// </summary>
        private async Task ApplyAcNumericAsync(int? acTarget, int? dcTarget)
        {
            if (acTarget.HasValue) SetAcNumericValueSilently(acTarget.Value);
            if (dcTarget.HasValue) SetDcNumericValueSilently(dcTarget.Value);

            var dict = new Dictionary<string, object?>
            {
                ["ACValue"] = AcNumericValue,
                ["DCValue"] = DcNumericValue,
            };
            await _executor.ApplySettingOperationsAsync(Definition, true, dict);
            RefreshBadges();
        }

        // ─── Status Banner & Technical Details (Winhance 1:1 port) ────────────────────

        /// <summary>
        /// Updates the status banner based on current setting state.
        /// Called after any user-driven or quick-set apply.
        /// </summary>
        public async Task ApplyBannerAsync(bool isSuccess, string? customMessage = null, bool isRecommended = false, bool isDefault = false)
        {
            await _statusBannerManager.ApplyBannerAsync(
                this,
                Definition,
                isSuccess,
                customMessage,
                isRecommended,
                isDefault,
                _stateReader);
        }

        /// <summary>
        /// Updates the technical details panel by reading current system state.
        /// Called after any user-driven or quick-set apply.
        /// </summary>
        public async Task UpdateTechnicalDetailsAsync()
        {
            await _technicalDetailsManager.UpdateTechnicalDetailsAsync(
                this,
                Definition,
                _stateReader,
                _executor);
        }

        private int FindOptionIndex(Func<ComboBoxOption, bool> predicate)
    {
        var opts = Definition.ComboBox?.Options;
        if (opts == null) return -1;
        for (int i = 0; i < opts.Count; i++)
            if (predicate(opts[i])) return i;
        return -1;
    }

    // ── Badge computation ──────────────────────────────────────────────────────
    private void RefreshBadges()
    {
        var computed = ComputeBadgeState();
        Badges.Clear();
        foreach (var pill in computed)
            Badges.Add(pill);
        OnPropertyChanged(nameof(HasBadges));
    }

    private IReadOnlyList<BadgePillState> ComputeBadgeState()
    {
        var result = new List<BadgePillState>();

        if (Definition.InputType == InputType.Action)
            return result;

        bool hasBadgeData =
            Definition.RegistrySettings.Count > 0
            || Definition.ScheduledTaskSettings.Count > 0
            || Definition.ComboBox?.Options?.Any(o => o.IsRecommended || o.IsDefault) == true
            || (Definition.PowerCfgSettings?.Any(p =>
                p.RecommendedValueAC.HasValue || p.RecommendedValueDC.HasValue
                || p.DefaultValueAC.HasValue || p.DefaultValueDC.HasValue) == true);
        if (!hasBadgeData)
            return result;

        if (InputType == InputType.Toggle || InputType == InputType.CheckBox)
        {
            bool? recState = SettingDefinitionToggleState.GetRecommendedToggleState(Definition);
            bool? defState = SettingDefinitionToggleState.GetDefaultToggleState(Definition);

            // Start from the explicit toggle-level comparison when present; otherwise the
            // AND-identity (true) so the registry loop below can drive the flag. A literal
            // "recState.HasValue && IsOn==recState.Value" seed would pin the null case to
            // false and defeat the "let registry drive" intent stated in the spec.
            bool matchesRec = recState.HasValue ? IsOn == recState.Value : true;
            bool matchesDef = defState.HasValue ? IsOn == defState.Value : true;

            // Fold registry evaluation in. When the explicit toggle-level state is set we
            // keep it; when it is null we let the registry comparison drive the flag.
            foreach (var reg in Definition.RegistrySettings)
            {
                var (regRec, regDef) = EvaluateRegistrySetting(reg);
                if (!recState.HasValue) matchesRec = matchesRec && regRec;
                if (!defState.HasValue) matchesDef = matchesDef && regDef;
            }

            if (Definition.IsSubjectivePreference)
            {
                result.Add(new BadgePillState(SettingBadgeKind.Preference, true, "Preference", "This is a preference setting"));
            }
            else
            {
                if (recState.HasValue || Definition.RegistrySettings.Any(r => r.RecommendedValue != null))
                    result.Add(new BadgePillState(SettingBadgeKind.Recommended, matchesRec, "Recommended", "Akari's recommended value"));
                if (defState.HasValue || Definition.RegistrySettings.Any(r => r.DefaultValue != null))
                    result.Add(new BadgePillState(SettingBadgeKind.Default, matchesDef, "Windows Default", "Windows default value"));
            }
        }
        else if (InputType == InputType.Selection)
        {
            int optionCount = Definition.ComboBox?.Options?.Count ?? 0;

            if (Definition.IsSubjectivePreference)
            {
                result.Add(new BadgePillState(SettingBadgeKind.Preference, true, "Preference", "This is a preference setting"));
            }
            else
            {
                bool matchesRec = SelectedIndex >= 0 && SelectedIndex < optionCount
                    && Definition.ComboBox!.Options[SelectedIndex].IsRecommended;
                bool matchesDef = SelectedIndex >= 0 && SelectedIndex < optionCount
                    && Definition.ComboBox!.Options[SelectedIndex].IsDefault;
                bool isCustom = SelectedIndex >= 0 && !matchesRec && !matchesDef;

                if (Definition.ComboBox?.Options?.Any(o => o.IsRecommended) == true)
                    result.Add(new BadgePillState(SettingBadgeKind.Recommended, matchesRec, "Recommended", "Akari's recommended value"));
                if (Definition.ComboBox?.Options?.Any(o => o.IsDefault) == true)
                    result.Add(new BadgePillState(SettingBadgeKind.Default, matchesDef, "Windows Default", "Windows default value"));
                if (isCustom)
                    result.Add(new BadgePillState(SettingBadgeKind.Custom, true, "Custom", "Custom value"));
            }
        }
        else if (InputType == InputType.NumericRange)
        {
            var pcfg = Definition.PowerCfgSettings?.FirstOrDefault();
            if (pcfg == null) return result;

            // Separate AC/DC with a battery present: per-mode pills so the user can see
            // which mode matches recommended/default and which is custom. On battery-less
            // systems DC is hidden and not writable — keep single-pill behaviour (1:1
            // with Winhance).
            bool perModeBadges = SupportsSeparateACDC
                && HasBattery
                && pcfg.PowerModeSupport == PowerModeSupport.Separate;

            if (perModeBadges)
            {
                AddAcDcRecommendedPills(result, pcfg);
                AddAcDcDefaultPills(result, pcfg);
                AddAcDcCustomPills(result, pcfg);
            }
            else
            {
                // Compare display units; pcfg values are in system units.
                bool considerDc = HasBattery;
                bool matchesRec = true;
                bool matchesDef = true;

                if (SupportsSeparateACDC)
                {
                    if (pcfg.RecommendedValueAC.HasValue && AcNumericValue != ConvertFromSystemUnits(pcfg.RecommendedValueAC.Value))
                        matchesRec = false;
                    if (considerDc && pcfg.RecommendedValueDC.HasValue && DcNumericValue != ConvertFromSystemUnits(pcfg.RecommendedValueDC.Value))
                        matchesRec = false;
                    if (pcfg.DefaultValueAC.HasValue && AcNumericValue != ConvertFromSystemUnits(pcfg.DefaultValueAC.Value))
                        matchesDef = false;
                    if (considerDc && pcfg.DefaultValueDC.HasValue && DcNumericValue != ConvertFromSystemUnits(pcfg.DefaultValueDC.Value))
                        matchesDef = false;
                }
                else
                {
                    if (pcfg.RecommendedValueAC.HasValue && NumericValue != ConvertFromSystemUnits(pcfg.RecommendedValueAC.Value))
                        matchesRec = false;
                    if (pcfg.DefaultValueAC.HasValue && NumericValue != ConvertFromSystemUnits(pcfg.DefaultValueAC.Value))
                        matchesDef = false;
                }

                bool hasRecData = pcfg.RecommendedValueAC.HasValue || (considerDc && pcfg.RecommendedValueDC.HasValue);
                bool hasDefData = pcfg.DefaultValueAC.HasValue || (considerDc && pcfg.DefaultValueDC.HasValue);

                if (hasRecData)
                    result.Add(new BadgePillState(SettingBadgeKind.Recommended, matchesRec, "Recommended", "Akari's recommended value"));
                if (hasDefData)
                    result.Add(new BadgePillState(SettingBadgeKind.Default, matchesDef, "Windows Default", "Windows default value"));
                if (hasRecData || hasDefData)
                    result.Add(new BadgePillState(SettingBadgeKind.Custom, !matchesRec && !matchesDef, "Custom", "Custom value"));
            }
        }

        return result;
    }

    private void AddAcDcRecommendedPills(List<BadgePillState> row, PowerCfgSetting pcfg)
    {
        if (pcfg.RecommendedValueAC.HasValue)
        {
            bool match = AcNumericValue == ConvertFromSystemUnits(pcfg.RecommendedValueAC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, match, "Recommended", "Akari's recommended value (plugged in)", SettingBadgeMode.AC));
        }
        if (pcfg.RecommendedValueDC.HasValue)
        {
            bool match = DcNumericValue == ConvertFromSystemUnits(pcfg.RecommendedValueDC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Recommended, match, "Recommended", "Akari's recommended value (on battery)", SettingBadgeMode.DC));
        }
    }

    private void AddAcDcDefaultPills(List<BadgePillState> row, PowerCfgSetting pcfg)
    {
        if (pcfg.DefaultValueAC.HasValue)
        {
            bool match = AcNumericValue == ConvertFromSystemUnits(pcfg.DefaultValueAC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Default, match, "Windows Default", "Windows default value (plugged in)", SettingBadgeMode.AC));
        }
        if (pcfg.DefaultValueDC.HasValue)
        {
            bool match = DcNumericValue == ConvertFromSystemUnits(pcfg.DefaultValueDC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Default, match, "Windows Default", "Windows default value (on battery)", SettingBadgeMode.DC));
        }
    }

    private void AddAcDcCustomPills(List<BadgePillState> row, PowerCfgSetting pcfg)
    {
        if (pcfg.RecommendedValueAC.HasValue || pcfg.DefaultValueAC.HasValue)
        {
            bool acRec = pcfg.RecommendedValueAC.HasValue && AcNumericValue == ConvertFromSystemUnits(pcfg.RecommendedValueAC.Value);
            bool acDef = pcfg.DefaultValueAC.HasValue && AcNumericValue == ConvertFromSystemUnits(pcfg.DefaultValueAC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, !acRec && !acDef, "Custom", "Custom value (plugged in)", SettingBadgeMode.AC));
        }
        if (pcfg.RecommendedValueDC.HasValue || pcfg.DefaultValueDC.HasValue)
        {
            bool dcRec = pcfg.RecommendedValueDC.HasValue && DcNumericValue == ConvertFromSystemUnits(pcfg.RecommendedValueDC.Value);
            bool dcDef = pcfg.DefaultValueDC.HasValue && DcNumericValue == ConvertFromSystemUnits(pcfg.DefaultValueDC.Value);
            row.Add(new BadgePillState(SettingBadgeKind.Custom, !dcRec && !dcDef, "Custom", "Custom value (on battery)", SettingBadgeMode.DC));
        }
    }

    private (bool matchesRec, bool matchesDef) EvaluateRegistrySetting(RegistrySetting reg)
    {
        bool matchesRec;
        bool matchesDef;

        if (!TryOpenSubkey(reg.KeyPath, out var subkey))
            return (false, false);

        using (subkey)
        {
            if (subkey == null)
            {
                matchesRec = false;
                matchesDef = SettingDefinitionToggleState.IsKeyExistenceToggle(reg) ? false : false;
                return (matchesRec, matchesDef);
            }

            var currentValue = subkey.GetValue(reg.ValueName);
            if (currentValue == null)
            {
                matchesRec = reg.RecommendedValue == null ? ValuesEqual(null, reg.EnabledValue?[0]) : false;
                matchesDef = reg.DefaultValue == null;
                return (matchesRec, matchesDef);
            }

            matchesRec = reg.RecommendedValue != null && ValuesEqual(currentValue, reg.RecommendedValue);
            matchesDef = reg.DefaultValue != null && ValuesEqual(currentValue, reg.DefaultValue);
            return (matchesRec, matchesDef);
        }
    }

    private static bool TryOpenSubkey(string keyPath, out RegistryKey? subkey)
    {
        subkey = null;

        const string HklmPrefix = @"HKEY_LOCAL_MACHINE\";
        const string HkcuPrefix = @"HKEY_CURRENT_USER\";

        RegistryKey hive;
        string subPath;

        if (keyPath.StartsWith(HklmPrefix, StringComparison.Ordinal))
        {
            hive = Registry.LocalMachine;
            subPath = keyPath.Substring(HklmPrefix.Length);
        }
        else if (keyPath.StartsWith(HkcuPrefix, StringComparison.Ordinal))
        {
            hive = Registry.CurrentUser;
            subPath = keyPath.Substring(HkcuPrefix.Length);
        }
        else
        {
            return false;
        }

        subkey = hive.OpenSubKey(subPath, writable: false);
        return true;
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (Equals(a, b)) return true;

        try
        {
            return Convert.ToInt64(a) == Convert.ToInt64(b);
        }
        catch
        {
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
