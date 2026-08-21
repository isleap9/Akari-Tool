using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using WinUI.Framework.Services;
using AkariTool.Core.Features.Common.Events;
using AkariTool.Core.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Customize;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Explorer sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Explorer group, reached via the Customize
/// landing hub or the Explorer sub-nav rail item.
///
/// "View" = ExplorerView + ExplorerViewFolderOptions concatenated into one card
/// (net8 BuildExplorerView calls BuildExplorerViewFolderOptions on the same
/// section). Section order and every TweakDefinition Id are preserved
/// byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class ExplorerViewModel : SettingPageViewModel
{
    public ExplorerViewModel(
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
        Title = "Explorer";
        Subtitle = "File Explorer view, behavior, associations, and This PC folders.";
    }

    public override string NavTag => "Explorer";
    public override string NavLabel => "Explorer";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => ExplorerOptimizations.Build();
}
