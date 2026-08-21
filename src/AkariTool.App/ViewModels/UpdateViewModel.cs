using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using WinUI.Framework.Services;
using AkariTool.Core.Features.Common.Events;
using AkariTool.Core.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Update;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Windows Updates page. Fully on the declarative SettingDefinition path
/// (Track A Phase 4) — the catalog is built from <see cref="UpdateOptimizations"/>
/// rather than the delegate-based UpdateTweaks.
///
/// updates-policy-mode (the Windows Update Policy dropdown) is included in the
/// declarative catalog; its composite multi-value detection and apply path
/// (Paused/Disabled states collide under single-value matching) are handled by
/// WindowsUpdatePolicyHandler via the ISpecialSettingHandler registry.
/// </summary>
public sealed partial class UpdateViewModel : SettingPageViewModel
{
    public UpdateViewModel(
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
        Title = "Windows Updates";
        Subtitle = "Update policy, delivery optimisation, and update behaviour controls.";
    }

    public override string NavTag => "Update";
    public override string NavLabel => "Windows Updates";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => UpdateOptimizations.Build();
}
