using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Services;
using AkariTool.Tabs.Power;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Power page — ported to the declarative SettingDefinition model (Track A
/// Phase 4 / Session C). Builds its sections from
/// <see cref="PowerOptimizations.Build"/>, gated by hardware capability and
/// PowerCfg setting existence, exactly like Winhance's PowerOptimizationsViewModel.
///
/// The <c>power-plan-selection</c> row is the plan lifecycle row: its options are
/// loaded dynamically from the system's power plans
/// (<see cref="IPowerPlanComboBoxService"/>), activation runs through
/// <see cref="IPowerService"/>, and it renders via the bespoke PowerPlanComboBox
/// template (status dot, [Active] badge, per-plan delete) in the shared
/// TweakTemplates.xaml rendering layer.
///
/// Gating happens here (in BuildSettingGroups, blocking on the async probe
/// services — SettingPageViewModel.Build is synchronous) instead of the legacy
/// "return empty catalog on a battery-less machine" pattern: the catalog is kept
/// whole and rows are filtered. RequiresAdvancedUnlock rows stay visible without
/// a lock UI (Akari deviation — Winhance gates them behind a one-time unlock
/// dialog; the flag is preserved on the catalog for a later session).
/// </summary>
public sealed partial class PowerViewModel : SettingPageViewModel
{
    private readonly IHardwareDetectionService _hardware;
    private readonly IPowerSettingsValidationService _validation;
    private readonly IPowerPlanComboBoxService _planComboBoxService;
    private readonly IPowerService _powerService;
    private readonly IReadOnlyList<SettingDefinition> _powerCatalog;
    private bool _hasBattery;

    public PowerViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs,
        IHardwareDetectionService hardware,
        IPowerSettingsValidationService validation,
        IPowerPlanComboBoxService planComboBoxService,
        IPowerService powerService)
        : base(stateReader, executor, dialogs)
    {
        _hardware = hardware;
        _validation = validation;
        _planComboBoxService = planComboBoxService;
        _powerService = powerService;

        _powerCatalog = PowerOptimizations.Build().SelectMany(g => g.Settings).ToList();

        Title = "Power";
        Subtitle = "Power plan management and advanced power configuration.";
    }

    public override string NavTag => "Power";
    public override string NavLabel => "Power";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => Gate(PowerOptimizations.Build());

    protected override SettingItemViewModel CreateItem(SettingDefinition s)
    {
        if (s.Recommendation?.LoadDynamicOptions == true)
            return new SettingItemViewModel(
                s, _stateReader, _executor, _dialogs,
                _hasBattery, _planComboBoxService, _powerService, _powerCatalog);
        // Numeric/Selection PowerCfg rows need HasBattery for the Dual/SingleAC
        // template split and per-mode badges.
        return new SettingItemViewModel(s, _stateReader, _executor, _dialogs, _hasBattery);
    }

    /// <summary>
    /// Applies the hardware + existence gate. Hardware flags (RequiresBattery,
    /// RequiresLid, RequiresBrightnessSupport, RequiresHybridSleepCapable) filter
    /// whole rows; ValidateExistence keeps only settings whose backing PowerCfg
    /// subgroup/setting exists on this machine. Empty groups drop out (the shared
    /// section template hides nothing — the base has no empty-section guard).
    /// </summary>
    private IReadOnlyList<SettingGroup> Gate(IReadOnlyList<SettingGroup> groups)
    {
        // Blocking on the async probes is safe: the services ConfigureAwait(false)
        // internally, and this runs once per Build (warm-up background thread or
        // the page ctor).
        bool battery = _hardware.HasBatteryAsync().GetAwaiter().GetResult();
        bool lid = _hardware.HasLidAsync().GetAwaiter().GetResult();
        bool brightness = _hardware.SupportsBrightnessControlAsync().GetAwaiter().GetResult();
        bool hybrid = _hardware.SupportsHybridSleepAsync().GetAwaiter().GetResult();
        _hasBattery = battery;

        var valid = _validation.FilterSettingsByExistenceAsync(
                groups.SelectMany(g => g.Settings)).GetAwaiter().GetResult();
        var validIds = new HashSet<string>(valid.Select(s => s.Id), System.StringComparer.Ordinal);

        var result = new List<SettingGroup>();
        foreach (var group in groups)
        {
            var kept = group.Settings
                .Where(s => PassesGate(s, battery, lid, brightness, hybrid, validIds))
                .ToList();
            if (kept.Count == 0) continue;
            result.Add(group with { Settings = kept });
        }
        return result;
    }

    private static bool PassesGate(
        SettingDefinition s,
        bool battery,
        bool lid,
        bool brightness,
        bool hybrid,
        HashSet<string> validIds)
    {
        if (s.RequiresBattery && !battery) return false;
        if (s.RequiresLid && !lid) return false;
        if (s.RequiresBrightnessSupport && !brightness) return false;
        if (s.RequiresHybridSleepCapable && !hybrid) return false;
        if (s.ValidateExistence && !validIds.Contains(s.Id)) return false;
        return true;
    }
}