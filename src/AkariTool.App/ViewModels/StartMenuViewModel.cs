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
/// Customize ▸ Start Menu sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Start Menu group, reached via the Customize
/// landing hub or the Start Menu sub-nav rail item. Section order and every
/// TweakDefinition Id are preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class StartMenuViewModel : SettingPageViewModel
{
    public StartMenuViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
        : base(stateReader, executor, dialogs)
    {
        Title = "Start Menu";
        Subtitle = "Start menu layout and behavior.";
    }

    public override string NavTag => "StartMenu";
    public override string NavLabel => "Start Menu";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => StartMenuOptimizations.Build();
}
