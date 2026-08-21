using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using WinUI.Framework.Services;
using AkariTool.Core.Features.Common.Events;
using AkariTool.Core.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Privacy;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Privacy &amp; Security page — ported to the declarative SettingDefinition model
/// (Track A Phase 4). Builds its sections from <see cref="PrivacyOptimizations.Build"/>.
///
/// ⚠ The "Security" section was audited for this rollout: all rows are pure
/// registry-key tweaks (UAC level, Workplace Join, BitLocker auto-encryption,
/// WiFi-Sense, Automatic Maintenance, Error Reporting, Remote Assistance, Smart
/// App Control, Developer Mode). NONE references DefenderService or arms/disables
/// Defender real-time, tamper, or antivirus protection. Nothing protective is touched.
/// </summary>
public sealed partial class PrivacyViewModel : SettingPageViewModel
{
    public PrivacyViewModel(
            ISettingStateReader stateReader,
            ISettingOperationExecutor executor,
            TweakDialogs dialogs,
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
        Title = "Privacy & Security";
        Subtitle = "Telemetry, data collection, app permissions, and AI opt-outs.";
    }

    public override string NavTag => "Privacy";
    public override string NavLabel => "Privacy & Security";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => PrivacyOptimizations.Build();

    /// <summary>
    /// Power's start-power-lock-option requires privacy-lock-screen: the resolver
    /// must see the Power catalog so disabling the lock screen cascades that row
    /// back to default cross-page (Winhance global-registry parity).
    /// </summary>
    protected override IReadOnlyList<SettingDefinition> AdditionalResolutionCatalogs()
        => AkariTool.Tabs.Power.PowerOptimizations.Build().SelectMany(g => g.Settings).ToList();
}
