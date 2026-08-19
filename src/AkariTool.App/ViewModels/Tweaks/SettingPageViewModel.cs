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

    private volatile bool _built;

    protected SettingPageViewModel(
        ISettingStateReader stateReader,
        ISettingOperationExecutor executor,
        TweakDialogs dialogs)
    {
        _stateReader = stateReader;
        _executor = executor;
        _dialogs = dialogs;
    }

    public abstract string NavTag { get; }
    public abstract string NavLabel { get; }

    protected abstract IReadOnlyList<SettingGroup> BuildSettingGroups();

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
                IEnumerable<SettingItemViewModel> items =
                    group.Settings.Select(s => new SettingItemViewModel(s, _stateReader, _executor, _dialogs));
                Sections.Add(new SettingSectionViewModel(group.Name, items));
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
            foreach (var item in section.Items)
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
            foreach (var item in section.Items)
                if (item.HasRecommendedQuickSet)
                    await item.ApplyRecommendedCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task RestoreDefaultsAsync()
    {
        foreach (var section in Sections)
            foreach (var item in section.Items)
                if (item.HasDefaultQuickSet)
                    await item.ApplyDefaultCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task CreateRestorePointAsync()
    {
        // Stub — restore-point wiring lands in a later phase.
        await Task.CompletedTask;
    }
}
