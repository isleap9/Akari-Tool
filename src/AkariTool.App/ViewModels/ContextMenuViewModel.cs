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
/// Customize ▸ Context Menu sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Context Menu group, reached via the Customize
/// landing hub or the Context Menu sub-nav rail item. Every TweakDefinition Id is
/// preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class ContextMenuViewModel : SettingPageViewModel
{
    public ContextMenuViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
        : base(stateReader, executor, dialogs)
    {
        Title = "Context Menu";
        Subtitle = "Right-click menu entries.";
    }

    public override string NavTag => "ContextMenu";
    public override string NavLabel => "Context Menu";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => ContextMenuOptimizations.Build();
}
