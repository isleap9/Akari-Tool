using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Interfaces;
using WinUI.Framework.Services;
using AkariTool.Core.Features.Common.Events;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Power;
using AkariTool.Tabs.Privacy;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Power page — ported to the declarative SettingDefinition model (Track A
/// Phase 4 / Session C). Builds its sections from
/// <see cref="PowerOptimizations.Build"/>, gated by the shared Windows-version +
/// hardware pipeline (SettingPageViewModel, 4h) plus the Power-only PowerCfg
/// existence gate — matching Winhance's CompatibleSettingsRegistry treatment of
/// its Power feature.
///
/// The <c>power-plan-selection</c> row is the plan lifecycle row: its options are
/// loaded dynamically from the system's power plans
/// (<see cref="IPowerPlanComboBoxService"/>), activation runs through
/// <see cref="IPowerService"/>, and it renders via the bespoke PowerPlanComboBox
/// template (status dot, [Active] badge, per-plan delete) in the shared
/// TweakTemplates.xaml rendering layer.
///
/// RequiresAdvancedUnlock rows render locked behind Winhance's one-time warning
/// dialog (persisted via ISettingsService).
/// </summary>
public sealed partial class PowerViewModel : SettingPageViewModel
{
    private readonly IHardwareDetectionService _hardware;
    private readonly IPowerSettingsValidationService _validation;
    private readonly IPowerPlanComboBoxService _planComboBoxService;
    private readonly IPowerService _powerService;
    private readonly WinUI.Framework.Services.ISettingsService _settings;
    private readonly IReadOnlyList<SettingDefinition> _powerCatalog;
    private bool _hasBattery;

    public PowerViewModel(
            ISettingStateReader stateReader,
            ISettingOperationExecutor executor,
            TweakDialogs dialogs,
            IHardwareDetectionService hardware,
            IPowerSettingsValidationService validation,
            IPowerPlanComboBoxService planComboBoxService,
            IPowerService powerService,
            WinUI.Framework.Services.ISettingsService settings,
            ISettingDependencyResolver? dependencyResolver = null,
            ILocalizationService? localizationService = null,
            IDispatcherService? dispatcherService = null,
            IRegeditLauncher? regeditLauncher = null,
            IEventBus? eventBus = null)
            : base(stateReader, executor, dialogs,
                 newBadgeService: null,
                 dependencyResolver: dependencyResolver,
                 localizationService: localizationService,
                 dispatcherService: dispatcherService,
                 regeditLauncher: regeditLauncher,
                 eventBus: eventBus)
        {
        _hardware = hardware;
        _validation = validation;
        _planComboBoxService = planComboBoxService;
        _powerService = powerService;
        _settings = settings;

        _powerCatalog = PowerOptimizations.Build().SelectMany(g => g.Settings).ToList();

        Title = "Power";
        Subtitle = "Power plan management and advanced power configuration.";
    }

    public override string NavTag => "Power";
    public override string NavLabel => "Power";

    /// <summary>
    /// start-power-lock-option requires Privacy's privacy-lock-screen: the resolver
    /// must see the Privacy catalog to auto-enable it cross-page (Winhance resolves
    /// this via its global settings registry).
    /// </summary>
    protected override IReadOnlyList<SettingDefinition> AdditionalResolutionCatalogs()
        => PrivacyOptimizations.Build().SelectMany(g => g.Settings).ToList();

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => Gate(PowerOptimizations.Build());

    protected override SettingItemViewModel CreateItem(SettingDefinition s)
        {
            if (s.Recommendation?.LoadDynamicOptions == true)
                return new SettingItemViewModel(
                    s, _stateReader, _executor, _dialogs,
                    _hasBattery, _planComboBoxService, _powerService, _powerCatalog,
                    newBadgeService: _newBadgeService, settingsService: _settings,
                    dependencyResolver: _dependencyResolver,
                    localizationService: _localizationService,
                    dispatcherService: _dispatcherService,
                    regeditLauncher: _regeditLauncher,
                    eventBus: _eventBus,
                    discoveryService: WinUI.Framework.IoC.ServiceLocator
                        .GetService<AkariTool.Core.Features.Common.Interfaces.ISystemSettingsDiscoveryService>());
            // Numeric/Selection PowerCfg rows need HasBattery for the Dual/SingleAC
            // template split and per-mode badges.
            return new SettingItemViewModel(
                s, _stateReader, _executor, _dialogs, _hasBattery,
                newBadgeService: _newBadgeService, settingsService: _settings,
                dependencyResolver: _dependencyResolver,
                localizationService: _localizationService,
                dispatcherService: _dispatcherService,
                regeditLauncher: _regeditLauncher,
                eventBus: _eventBus,
                discoveryService: WinUI.Framework.IoC.ServiceLocator
                    .GetService<AkariTool.Core.Features.Common.Interfaces.ISystemSettingsDiscoveryService>());
        }

    /// <summary>
    /// 4h slim-down: hardware Requires* gating moved to the shared pipeline
    /// (SettingPageViewModel.ApplyCompatibilityGates → HardwareCompatibilityFilter,
    /// Winhance CompatibleSettingsRegistry order). What remains here is the
    /// Power-only existence gate (Winhance gates FilterSettingsByExistenceAsync to
    /// Power too) plus the battery probe that feeds CreateItem's Dual/SingleAC
    /// template split — a display concern, not gating.
    ///
    /// Ordering note: predicates are independent ANDs, so running existence here
    /// and Windows/hardware afterwards yields the same set as Winhance's
    /// windows → hardware → existence sequence.
    /// </summary>
    private IReadOnlyList<SettingGroup> Gate(IReadOnlyList<SettingGroup> groups)
    {
        // Battery state feeds CreateItem's Dual/SingleAC template split (not gating).
        // Blocking on the async probe is safe: the service ConfigureAwait(false)s
        // internally, and this runs once per Build (warm-up background thread or
        // the page ctor).
        _hasBattery = _hardware.HasBatteryAsync().GetAwaiter().GetResult();

        var valid = _validation.FilterSettingsByExistenceAsync(
                groups.SelectMany(g => g.Settings)).GetAwaiter().GetResult();
        var validIds = new HashSet<string>(valid.Select(s => s.Id), System.StringComparer.Ordinal);

        var result = new List<SettingGroup>();
        foreach (var group in groups)
        {
            var kept = group.Settings
                .Where(s => !s.ValidateExistence || validIds.Contains(s.Id))
                .ToList();
            if (kept.Count == 0) continue;
            result.Add(group with { Settings = kept });
        }
        return result;
    }
}