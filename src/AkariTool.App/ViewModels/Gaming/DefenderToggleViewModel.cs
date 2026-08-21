using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Bespoke Security-section row driving <see cref="DefenderService.SetAsync"/> — a
/// reboot-based servicing-package removal that is not expressible as a plain
/// <see cref="SettingDefinition"/>. Injected into the Gaming page's Security section
/// by <see cref="GamingViewModel.AddDefenderRow"/> so it renders through the same
/// setting-row template as declarative rows.
/// </summary>
public sealed partial class DefenderToggleViewModel : ObservableObject, ISettingRowViewModel
{
    private readonly ToolService _tool;
    private readonly INewBadgeService? _newBadgeService;
    private bool _suppress;

    /// <summary>NEW-badge tag (Winhance AddedInVersion parity for bespoke rows).</summary>
    public string? AddedInVersion => "2.0.2";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Description))]
    public partial bool IsTamperProtected { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefenderReady))]
    public partial bool IsDefenderBusy { get; set; }

    public bool IsDefenderReady => !IsDefenderBusy;

    public string Name => "Windows Defender";

    public string Description => IsTamperProtected
        ? "Removes Windows Defender using the NoDefender CAB. ⚠ Tamper Protection is ON — disable it in Windows Security first."
        : "Removes Windows Defender using the NoDefender CAB. A reboot is required to complete.";

    public bool MatchesSearch(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        Description.Contains(query, StringComparison.OrdinalIgnoreCase);

    public bool HasBadges => true;
    public bool HasRecommendedQuickSet => false;
    public bool HasDefaultQuickSet => false;
    public string? RecommendedTooltip => null;
    public string? DefaultTooltip => null;

    public ObservableCollection<BadgePillState> Badges { get; } = new(new[]
    {
        new BadgePillState(SettingBadgeKind.Preference, true, "Preference", "Irreversible — requires reboot")
    });

    public IRelayCommand ApplyRecommendedCommand { get; } = new RelayCommand(() => { });
    public IRelayCommand ApplyDefaultCommand { get; } = new RelayCommand(() => { });

    // ── NEW badge (Winhance port) ──────────────────────────────────────────────
    private bool _isNew;
    public bool IsNew
    {
        get => _isNew;
        set
        {
            if (_isNew == value) return;
            _isNew = value;
            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(ShowNewBadge));
        }
    }

    private bool _isNewBadgeGloballyVisible = true;
    /// <summary>Global kill switch, mirrored from INewBadgeService.ShowNewBadges by the page layer.</summary>
    public bool IsNewBadgeGloballyVisible
    {
        get => _isNewBadgeGloballyVisible;
        set
        {
            if (_isNewBadgeGloballyVisible == value) return;
            _isNewBadgeGloballyVisible = value;
            OnPropertyChanged(nameof(IsNewBadgeGloballyVisible));
            OnPropertyChanged(nameof(ShowNewBadge));
        }
    }

    public bool ShowNewBadge => IsNew && IsNewBadgeGloballyVisible;

    public string NewBadgeText => "NEW";

    public DefenderToggleViewModel(ToolService tool, INewBadgeService? newBadgeService = null)
    {
        _tool = tool;
        _newBadgeService = newBadgeService;

        // Winhance parity: rows tagged AddedInVersion light up as NEW until the
        // user's baseline version passes. The row is constructed at Gaming-page
        // navigation, after SettingPageWarmUp has initialized the badge service.
        IsNew = _newBadgeService?.IsSettingNew(AddedInVersion, "windows-defender") == true;

        RefreshDefenderState();
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_suppress) return;
        _ = ToggleDefenderAsync(value);
    }

    private void SetIsOnSilently(bool v)
    {
        _suppress = true;
        IsOn = v;
        _suppress = false;
    }

    public void RefreshDefenderState()
    {
        IsTamperProtected = DefenderService.IsTamperProtectionEnabled();
        SetIsOnSilently(!IsTamperProtected && TweakHelpers.HasState("DisableDefender"));
    }

    private async Task ToggleDefenderAsync(bool newOn)
    {
        if (IsDefenderBusy) return;
        IsDefenderBusy = true;
        try
        {
            await DefenderService.SetAsync(newOn, _tool);
            RefreshDefenderState();
        }
        finally
        {
            IsDefenderBusy = false;
        }
    }
}
