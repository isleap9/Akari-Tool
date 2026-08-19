using System.Collections.Generic;
using WinUI.Framework.Mvvm;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Customize;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Taskbar sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Taskbar group (Layout → Behavior → Grouping),
/// reached via the Customize landing hub or the Taskbar sub-nav rail item.
///
/// "Behavior" = TaskbarBehavior + TaskbarBehaviorExtras concatenated into one
/// card (net8 BuildTaskbarBehavior calls BuildTaskbarBehaviorExtras on the same
/// section). "Button Grouping" is net8's 3 hand-made ComboBoxes as 3 dropdown
/// TweakDefinitions (Phase 17). Section order and every TweakDefinition Id are
/// preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class TaskbarViewModel : SettingPageViewModel
{
    public TaskbarViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
        : base(stateReader, executor, dialogs)
    {
        Title = "Taskbar";
        Subtitle = "Layout, behavior, and button grouping.";
    }

    public override string NavTag => "Taskbar";
    public override string NavLabel => "Taskbar";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => TaskbarOptimizations.Build();
}
