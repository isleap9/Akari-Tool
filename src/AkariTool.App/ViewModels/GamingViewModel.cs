using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Gaming;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Gaming &amp; Performance page — ported to the declarative SettingDefinition
/// model (Track A Phase 4). Builds its sections from
/// <see cref="GamingOptimizations.Build"/>.
///
/// gaming-disable-defender is deferred — DefenderService.SetAsync is not
/// expressible as a plain registry write; dedicated follow-up session needed.
/// </summary>
public sealed partial class GamingViewModel : SettingPageViewModel
{
    public GamingViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
        : base(stateReader, executor, dialogs)
    {
        Title = "Gaming & Performance";
        Subtitle = "Game Mode, GPU, CPU, and network tweaks for maximum frame rates.";
    }

    public override string NavTag => "Gaming";
    public override string NavLabel => "Gaming & Performance";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups()
        => GamingOptimizations.Build();
}
