using System.Collections.Generic;
using WinUI.Framework.Mvvm;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs.Privacy;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Privacy &amp; Security page — ported to the declarative SettingDefinition model
/// (Track A Phase 4). Builds its sections from <see cref="PrivacyOptimizations.Build"/>.
///
/// ⚠ The "Security" section was audited for this rollout: all rows are pure
/// registry-key tweaks (UAC level, Workplace Join, BitLocker auto-encryption,
/// WiFi-Sense, Automatic Maintenance, Error Reporting, Remote Assistance, Smart
/// App Control, Developer Mode). NONE references DefenderService or arms/disables
/// Defender real-time, tamper, or antivirus protection. Nothing protective is touched.
/// </summary>
public sealed partial class PrivacyViewModel : SettingPageViewModel
{
    public PrivacyViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
        : base(stateReader, executor, dialogs)
    {
        Title = "Privacy & Security";
        Subtitle = "Telemetry, data collection, app permissions, and AI opt-outs.";
    }

    public override string NavTag => "Privacy";
    public override string NavLabel => "Privacy & Security";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => PrivacyOptimizations.Build();
}
