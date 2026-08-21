using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using WinUI.Framework.Services;
using AkariTool.Core.Features.Common.Events;
using AkariTool.Core.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Gaming;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Gaming &amp; Performance page — ported to the declarative SettingDefinition
/// model (Track A Phase 4). Builds its sections from
/// <see cref="GamingOptimizations.Build"/>.
///
/// The Windows Defender toggle is a bespoke <see cref="DefenderToggleViewModel"/>
/// row injected into the Security section via <see cref="AddDefenderRow"/> — a
/// reboot-based servicing-package removal that is not expressible as a plain
/// <see cref="SettingDefinition"/>.
/// </summary>
public sealed partial class GamingViewModel : SettingPageViewModel
{
    private readonly ToolService _tool;
    private DefenderToggleViewModel? _defenderRow;

    public GamingViewModel(
            ISettingStateReader stateReader,
            ISettingOperationExecutor executor,
            TweakDialogs dialogs,
            ToolService tool,
            INewBadgeService? newBadgeService = null,
            ISettingDependencyResolver? dependencyResolver = null,
            ILocalizationService? localizationService = null,
            IDispatcherService? dispatcherService = null,
            IRegeditLauncher? regeditLauncher = null,
            IEventBus? eventBus = null)
            : base(stateReader, executor, dialogs, newBadgeService, dependencyResolver,
                 localizationService, dispatcherService, regeditLauncher, eventBus)
        {
        _tool = tool;
        Title = "Gaming & Performance";
        Subtitle = "Game Mode, GPU, CPU, and network tweaks for maximum frame rates.";
    }

    public override string NavTag => "Gaming";
    public override string NavLabel => "Gaming & Performance";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => GamingOptimizations.Build();

    /// <summary>
    /// Injects the bespoke Windows Defender row into the Security section after the
    /// declarative sections have been built. Idempotent.
    /// </summary>
    public void AddDefenderRow()
    {
        var security = Sections.OfType<SettingSectionViewModel>().FirstOrDefault(s => s.Title == "Security");
        if (security == null) return;

        _defenderRow ??= new DefenderToggleViewModel(_tool, _newBadgeService);
        if (!security.Items.Contains(_defenderRow))
            security.Items.Add(_defenderRow);
    }
}
