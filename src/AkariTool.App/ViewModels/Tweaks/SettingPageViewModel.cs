using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUI.Framework.Mvvm;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Services;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Declarative replacement for TweakPageViewModel. Builds its sections from
/// <see cref="SettingGroup"/> records instead of TweakDefinition catalog arrays. No
/// TweakRegistry bracketing yet (Phase 3b-ii) — that wiring lands in a later phase.
/// </summary>
public abstract partial class SettingPageViewModel : ViewModelBase
{
    protected readonly ISettingStateReader _stateReader;
    protected readonly ISettingOperationExecutor _executor;
    protected readonly TweakDialogs _dialogs;
    protected readonly INewBadgeService? _newBadgeService;

    private volatile bool _built;

    protected SettingPageViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs,
        INewBadgeService? newBadgeService = null)
    {
        _stateReader = stateReader;
        _executor = executor;
        _dialogs = dialogs;
        _newBadgeService = newBadgeService;
    }

    public abstract string NavTag { get; }
    public abstract string NavLabel { get; }

    protected abstract IReadOnlyList<SettingGroup> BuildSettingGroups();

    /// <summary>
    /// Row factory. Virtual so a page (Power) can hand plan-special services to its
    /// LoadDynamicOptions row without touching the shared Build path.
    /// </summary>
    protected virtual SettingItemViewModel CreateItem(SettingDefinition s)
        => new(s, _stateReader, _executor, _dialogs, newBadgeService: _newBadgeService);

    /// <summary>
    /// A Power Plan row landed a plan change: re-read every sibling PowerCfg row
    /// (their values are scoped to the active plan). The plan row repopulates its
    /// own dropdown and is skipped here.
    /// </summary>
    private void OnPowerPlanChanged()
    {
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
                if (!item.IsPowerPlanSetting)
                    item.RefreshFromSystem();
        RefreshQuickActionCounts();
    }

    /// <summary>
    /// A gated row was permanently unlocked ("don't show again" checked): unlock
    /// every other RequiresAdvancedUnlock row on this page (Winhance's
    /// ParentFeatureViewModel sibling loop).
    /// </summary>
    private void OnAdvancedUnlockPersisted()
    {
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
                if (item.RequiresAdvancedUnlock)
                    item.IsLocked = false;
    }

    public ObservableCollection<SettingSectionViewModel> Sections { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplySearch(value);

    private void ApplySearch(string query)
    {
        foreach (var section in Sections)
            section.ApplySearch(query);
    }

    public void Build()
    {
        if (_built) return;

        lock (this)
        {
            if (_built) return;

            foreach (var group in BuildSettingGroups())
            {
                var items = group.Settings.Select(CreateItem).ToList();
                var section = new SettingSectionViewModel(group.Name, items);
                Sections.Add(section);

                // Power Plan rows repopulate their own dropdown; the rest of the
                // page must re-read after the active plan changes.
                // RequiresAdvancedUnlock rows unlock their siblings when the user
                // checks "don't show again" in the warning dialog (Winhance parity).
                foreach (var item in items)
                {
                    if (item.IsPowerPlanSetting)
                        item.PowerPlanChanged += OnPowerPlanChanged;
                    if (item.RequiresAdvancedUnlock)
                        item.AdvancedUnlockPersisted += OnAdvancedUnlockPersisted;
                }
            }

            _built = true;
            RefreshQuickActionCounts();
        }
    }

    // ── Quick Actions (tab scope) ─────────────────────────────────────────────

    [ObservableProperty]
    public partial int RecommendedPendingCount { get; set; }

    [ObservableProperty]
    public partial int DefaultPendingCount { get; set; }

    public string RecommendedPendingSubtitle => RecommendedPendingCount > 0 ? $"{RecommendedPendingCount} pending" : "All applied";
    public string DefaultPendingSubtitle => DefaultPendingCount > 0 ? $"{DefaultPendingCount} pending" : "All applied";

    partial void OnRecommendedPendingCountChanged(int value) => OnPropertyChanged(nameof(RecommendedPendingSubtitle));
    partial void OnDefaultPendingCountChanged(int value) => OnPropertyChanged(nameof(DefaultPendingSubtitle));

    /// <summary>
    /// Recomputes the pending counts: an item is "pending recommended" when it has a
    /// recommended target and its Recommended badge is not currently highlighted.
    /// </summary>
    public void RefreshQuickActionCounts()
    {
        int rec = 0, def = 0;
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
            {
                if (item.HasRecommendedQuickSet
                    && !item.Badges.Any(b => b.Kind == AkariTool.Core.Features.Common.Enums.SettingBadgeKind.Recommended && b.IsHighlighted))
                    rec++;
                if (item.HasDefaultQuickSet
                    && !item.Badges.Any(b => b.Kind == AkariTool.Core.Features.Common.Enums.SettingBadgeKind.Default && b.IsHighlighted))
                    def++;
            }
        RecommendedPendingCount = rec;
        DefaultPendingCount = def;
    }

    [RelayCommand]
    public async Task ApplyAllRecommendedAsync()
    {
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
                if (item.HasRecommendedQuickSet)
                    await item.ApplyRecommendedCommand.ExecuteAsync(null);
        RefreshQuickActionCounts();
    }

    [RelayCommand]
    public async Task RestoreDefaultsAsync()
    {
        foreach (var section in Sections)
            foreach (var item in section.Items.OfType<SettingItemViewModel>())
                if (item.HasDefaultQuickSet)
                    await item.ApplyDefaultCommand.ExecuteAsync(null);
        RefreshQuickActionCounts();
    }

    [RelayCommand]
    public async Task CreateRestorePointAsync()
    {
        // Stub — restore-point wiring lands in a later phase.
        await Task.CompletedTask;
    }
}
