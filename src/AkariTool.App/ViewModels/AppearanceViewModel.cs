using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Customize;
using AkariTool.ViewModels.Tweaks;
using WinUI.Framework.Services;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Appearance sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Appearance group, reached via the Customize
/// landing hub or the Appearance sub-nav rail item. Section order and every
/// TweakDefinition Id are preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class AppearanceViewModel : SettingPageViewModel
{
    public AppearanceViewModel(
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
        Title = "Appearance";
        Subtitle = "Theme, transparency, color, and window style.";
    }

    public override string NavTag => "Appearance";
    public override string NavLabel => "Appearance";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => AppearanceOptimizations.Build();
}
