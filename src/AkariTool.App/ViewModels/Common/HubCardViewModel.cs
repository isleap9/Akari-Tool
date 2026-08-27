using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels.Common;

/// <summary>
/// One overview card in a hub (Optimize / Customize / Advanced Tools). Carries the
/// card's display data plus a live pending-count pair forwarded from the associated
/// <see cref="SettingPageViewModel"/> (Recommended / Default). Cards without a settings
/// page (AkariOS, Backup, Verify) pass a null <c>pageVm</c> and render no pills.
///
/// Reimplemented from Winhance's overview-card behavior against Akari's own
/// infrastructure: the pill counts bind to the existing RecommendedPendingCount /
/// DefaultPendingCount observables rather than a per-page hand-coded pill set.
/// </summary>
public sealed partial class HubCardViewModel : ObservableObject, IDisposable
{
    private readonly SettingPageViewModel? _pageVm;

    public HubCardViewModel(
        string title,
        string description,
        string glyph,
        Type detailPageType,
        SettingPageViewModel? pageVm = null,
        bool isPinned = false)
    {
        Title = title;
        Description = description;
        Glyph = glyph;
        DetailPageType = detailPageType;
        IsPinned = isPinned;
        _pageVm = pageVm;

        if (_pageVm is not null)
        {
            _pageVm.PropertyChanged += OnPageVmPropertyChanged;
        }
    }

    /// <summary>Card heading.</summary>
    public string Title { get; }

    /// <summary>Sub-heading line.</summary>
    public string Description { get; }

    /// <summary>Segoe Fluent Icons glyph string (single char), matching Akari's card style.</summary>
    public string Glyph { get; }

    /// <summary>Detail page navigated into the hub's inner frame on click.</summary>
    public Type DetailPageType { get; }

    /// <summary>Pinned cards (e.g. AkariOS) sort to the top of the overview.</summary>
    public bool IsPinned { get; }

    /// <summary>True when this card has an associated settings page (renders pills).</summary>
    public bool HasCounts => _pageVm is not null;

    public int RecommendedCount => _pageVm?.RecommendedPendingCount ?? 0;
    public int DefaultCount => _pageVm?.DefaultPendingCount ?? 0;
    public int RecommendedTotalCount => _pageVm?.RecommendedTotalCount ?? 0;
    public int DefaultTotalCount => _pageVm?.DefaultTotalCount ?? 0;

    // Pills show only when there is a settings page AND that bucket has pending items.
    public Visibility RecommendedPillVisibility =>
        HasCounts && RecommendedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DefaultPillVisibility =>
        HasCounts && DefaultCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public string RecommendedPillText => $"{RecommendedCount}/{RecommendedTotalCount} Recommended";
    public string DefaultPillText => $"{DefaultCount}/{DefaultTotalCount} Default";

    private void OnPageVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingPageViewModel.RecommendedPendingCount)
            or nameof(SettingPageViewModel.DefaultPendingCount)
            or nameof(SettingPageViewModel.RecommendedTotalCount)
            or nameof(SettingPageViewModel.DefaultTotalCount))
        {
            OnPropertyChanged(nameof(RecommendedCount));
            OnPropertyChanged(nameof(DefaultCount));
            OnPropertyChanged(nameof(RecommendedTotalCount));
            OnPropertyChanged(nameof(DefaultTotalCount));
            OnPropertyChanged(nameof(RecommendedPillVisibility));
            OnPropertyChanged(nameof(DefaultPillVisibility));
            OnPropertyChanged(nameof(RecommendedPillText));
            OnPropertyChanged(nameof(DefaultPillText));
        }
    }

    public void Dispose()
    {
        if (_pageVm is not null)
        {
            _pageVm.PropertyChanged -= OnPageVmPropertyChanged;
        }
    }
}
