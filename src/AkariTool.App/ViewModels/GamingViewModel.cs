using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
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
/// The Windows Defender toggle is implemented as a bespoke row driving
/// <see cref="DefenderService.SetAsync"/> — a reboot-based servicing-package
/// removal that is not expressible as a plain <see cref="SettingDefinition"/>.
/// </summary>
public sealed partial class GamingViewModel : SettingPageViewModel
{
    private readonly ToolService _tool;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefenderReady))]
    [NotifyPropertyChangedFor(nameof(DefenderButtonLabel))]
    public partial bool IsDefenderBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TamperWarningVisibility))]
    public partial bool IsTamperProtected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefenderButtonLabel))]
    public partial bool IsDefenderDisabled { get; set; }

    public bool IsDefenderReady => !IsDefenderBusy;

    public string DefenderButtonLabel => IsDefenderDisabled ? "Re-enable Windows Defender" : "Disable Windows Defender";

    public Visibility TamperWarningVisibility => IsTamperProtected ? Visibility.Visible : Visibility.Collapsed;

    public GamingViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs,
        ToolService tool)
        : base(stateReader, executor, dialogs)
    {
        _tool = tool;
        Title = "Gaming & Performance";
        Subtitle = "Game Mode, GPU, CPU, and network tweaks for maximum frame rates.";
        RefreshDefenderState();
    }

    public override string NavTag => "Gaming";
    public override string NavLabel => "Gaming & Performance";

    protected override IReadOnlyList<SettingGroup> BuildSettingGroups() => GamingOptimizations.Build();

    private void RefreshDefenderState()
    {
        IsTamperProtected = DefenderService.IsTamperProtectionEnabled();
        IsDefenderDisabled = !IsTamperProtected && TweakHelpers.HasState("DisableDefender");
    }

    [RelayCommand]
    private async Task ToggleDefenderAsync()
    {
        if (IsDefenderBusy) return;
        IsDefenderBusy = true;
        try
        {
            await DefenderService.SetAsync(!IsDefenderDisabled, _tool);
            RefreshDefenderState();
        }
        finally
        {
            IsDefenderBusy = false;
        }
    }
}
