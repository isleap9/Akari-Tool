using System.Collections.Generic;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Update;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Windows Updates page. Now on the declarative SettingDefinition path
/// (Track A Phase 4) — the catalog is built from <see cref="UpdateOptimizations"/>
/// rather than the delegate-based UpdateTweaks.
///
/// updates-policy-mode (the Windows Update Policy dropdown) is deferred: its
/// detection requires a composite multi-value read (Paused/Disabled states
/// collide under single-value matching), which the SettingDefinition stack
/// cannot yet express. It stays out of the declarative catalog until a
/// composite-detection implementation lands.
/// </summary>
public sealed partial class UpdateViewModel : SettingPageViewModel
{
    public UpdateViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
        : base(stateReader, executor, dialogs)
    {
        Title = "Windows Updates";
        Subtitle = "Update policy, delivery optimisation, and update behaviour controls.";
    }

    public override string NavTag => "Update";
    public override string NavLabel => "Windows Updates";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => UpdateOptimizations.Build();
}
