using System.Collections.Generic;
using WinUI.Framework.Mvvm;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Sound;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Sound page — first tab ported to the declarative SettingDefinition model
/// (Track A Phase 4). Builds its sections from <see cref="SoundOptimizations.Build"/>.
/// </summary>
public sealed partial class SoundViewModel : SettingPageViewModel
{
    public SoundViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs,
        ISettingDependencyResolver? dependencyResolver = null)
        : base(stateReader, executor, dialogs, dependencyResolver: dependencyResolver)
    {
        Title = "Sound";
        Subtitle = "Audio behavior and accessibility sound settings";
    }

    public override string NavTag => "Sound";
    public override string NavLabel => "Sound";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => SoundOptimizations.Build();
}
