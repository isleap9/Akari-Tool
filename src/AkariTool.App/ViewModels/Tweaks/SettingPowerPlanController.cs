using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Services;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Owns the dynamic Power Plan dropdown's options and activate/import/delete plumbing for the
/// power-plan-selection row (extracted from <see cref="SettingItemViewModel"/>). The owning row
/// stays the public surface for XAML/backup consumers (<c>PlanOptions</c>, <c>DeletePlanCommand</c>,
/// <c>ApplyPowerPlanByGuidAsync</c>, <c>PowerPlanChanged</c>) and delegates to this controller.
/// </summary>
public sealed class SettingPowerPlanController
{
    private readonly IPowerPlanComboBoxService? _powerPlanComboBoxService;
    private readonly IPowerService? _powerService;
    private readonly IReadOnlyList<SettingDefinition>? _powerCatalog;
    private readonly TweakDialogs _dialogs;
    private readonly Action<int> _setSelectedIndexSilently;
    private readonly Action<int> _setLastIndex;

    public SettingPowerPlanController(
        IPowerPlanComboBoxService? powerPlanComboBoxService,
        IPowerService? powerService,
        IReadOnlyList<SettingDefinition>? powerCatalog,
        TweakDialogs dialogs,
        Action<int> setSelectedIndexSilently,
        Action<int> setLastIndex)
    {
        _powerPlanComboBoxService = powerPlanComboBoxService;
        _powerService = powerService;
        _powerCatalog = powerCatalog;
        _dialogs = dialogs;
        _setSelectedIndexSilently = setSelectedIndexSilently;
        _setLastIndex = setLastIndex;
    }

    /// <summary>Dynamic Power Plan options backing the bespoke combo row.</summary>
    public ObservableCollection<PowerPlanComboBoxOption> PlanOptions { get; } = new();

    /// <summary>
    /// Raised after a plan activation / import / delete lands, so the page can
    /// re-read sibling PowerCfg rows (values differ per active plan).
    /// </summary>
    public event Action? PowerPlanChanged;

    /// <summary>
    /// Blocking repopulation of <see cref="PlanOptions"/> + active-index resolution.
    /// Runs in the row ctor (Build, on the warm-up background thread or the page
    /// ctor) and after every successful plan write. The internal services
    /// ConfigureAwait(false), so blocking here cannot deadlock a UI thread.
    /// </summary>
    public void RefreshPlanOptions()
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

        _setSelectedIndexSilently(idx);
        _setLastIndex(idx);
    }

    /// <summary>
    /// User picked a plan: activate it (importing the predefined plan first when it
    /// is not yet on the system), then repopulate the dropdown and ask the page to
    /// re-read sibling PowerCfg rows. Runs on the UI thread — no ConfigureAwait so
    /// the bound-collection mutations stay on the dispatcher.
    /// </summary>
    public async Task ApplyPowerPlanAsync(int newIndex)
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
    public async Task DeletePlanAsync(PowerPlanComboBoxOption? option)
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
}
