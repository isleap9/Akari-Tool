using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Native;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;

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

    private bool _suppress;
    private int _lastIndex = -1;

    public SettingItemViewModel(
        SettingDefinition definition,
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs,
        IPowerPlanComboBoxService? powerPlanComboBoxService = null,
        IPowerService? powerService = null,
        IReadOnlyList<SettingDefinition>? powerCatalog = null)
    {
        Definition = definition;
        _stateReader = stateReader;
        _executor = executor;
        _dialogs = dialogs;
        _powerPlanComboBoxService = powerPlanComboBoxService;
        _powerService = powerService;
        _powerCatalog = powerCatalog;

        Options = Definition.ComboBox?.Options?.Select(o => o.DisplayName).ToArray()
                  ?? Array.Empty<string>();

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

    // ── Observable state ───────────────────────────────────────────────────────
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    public ObservableCollection<BadgePillState> Badges { get; } = new();
    public bool HasBadges => Badges.Count > 0;

    public bool MatchesSearch(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Description.Contains(query, StringComparison.OrdinalIgnoreCase);

    // ── User-driven change plumbing (suppressible) ─────────────────────────────
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

    private async Task OnUserToggledAsync(bool newState)
    {
        if (!await _dialogs.ConfirmWarningAsync(Name, GetToggleWarning(newState)))
        {
            SetIsOnSilently(!newState);
            return;
        }

        await _executor.ApplySettingOperationsAsync(Definition, newState, null);
        RefreshBadges();
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
        await _executor.ApplySettingOperationsAsync(Definition, true, newIndex);
        RefreshBadges();
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

        RefreshBadges();
    }

    // ── Quick-set availability ─────────────────────────────────────────────────
    public bool HasRecommendedQuickSet => InputType switch
    {
        InputType.Toggle or InputType.CheckBox =>
            SettingDefinitionToggleState.GetRecommendedToggleState(Definition).HasValue,
        InputType.Selection =>
            Definition.ComboBox?.Options?.Any(o => o.IsRecommended) == true,
        _ => false,
    };

    public bool HasDefaultQuickSet => InputType switch
    {
        InputType.Toggle or InputType.CheckBox =>
            SettingDefinitionToggleState.GetDefaultToggleState(Definition).HasValue,
        InputType.Selection =>
            Definition.ComboBox?.Options?.Any(o => o.IsDefault) == true,
        _ => false,
    };

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
        }
        else if (InputType == InputType.Selection)
        {
            int idx = FindOptionIndex(o => o.IsRecommended);
            if (idx < 0) return;
            await _executor.ApplySettingOperationsAsync(Definition, true, idx);
            SetSelectedIndexSilently(idx);
            _lastIndex = idx;
            RefreshBadges();
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
        }
        else if (InputType == InputType.Selection)
        {
            int idx = FindOptionIndex(o => o.IsDefault);
            if (idx < 0) return;
            await _executor.ApplySettingOperationsAsync(Definition, true, idx);
            SetSelectedIndexSilently(idx);
            _lastIndex = idx;
            RefreshBadges();
        }
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
            || Definition.ComboBox?.Options?.Any(o => o.IsRecommended || o.IsDefault) == true;
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

        return result;
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
