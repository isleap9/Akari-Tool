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
/// Customize ▸ Desktop sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Desktop group, reached via the Customize landing
/// hub or the Desktop sub-nav rail item. Section order and every TweakDefinition
/// Id are preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class DesktopViewModel : SettingPageViewModel
{
    public DesktopViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
        : base(stateReader, executor, dialogs)
    {
        Title = "Desktop";
        Subtitle = "Desktop icons, shortcuts, startup, devices, lock screen, and regional settings.";
    }

    public override string NavTag => "Desktop";
    public override string NavLabel => "Desktop";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => DesktopOptimizations.Build();
}
