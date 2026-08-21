using System.ComponentModel;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.Services;

/// <summary>
/// Computes sidebar nav badges from the declarative pages' pending counts
/// (Winhance NavBadgeService parity, adapted to recommended/default pendings).
/// </summary>
public sealed class NavBadgeService(IEnumerable<SettingPageViewModel> pages) : INavBadgeService
{
    public IReadOnlyList<NavBadgeUpdate> ComputeNavBadges() =>
        pages.Select(p => new NavBadgeUpdate(p.NavTag, p.RecommendedPendingCount)).ToList();

    public IDisposable Subscribe(Action<string, int> onChanged)
    {
        void PagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (nameof(SettingPageViewModel.RecommendedPendingCount)
                or nameof(SettingPageViewModel.DefaultPendingCount))) return;

            if (sender is SettingPageViewModel p)
                onChanged(p.NavTag, p.RecommendedPendingCount);
        }

        foreach (var p in pages)
            p.PropertyChanged += PagePropertyChanged;

        return new Unsubscriber(pages, PagePropertyChanged);
    }

    private sealed class Unsubscriber(IEnumerable<SettingPageViewModel> pages, PropertyChangedEventHandler handler)
        : IDisposable
    {
        public void Dispose()
        {
            foreach (var p in pages)
                p.PropertyChanged -= handler;
        }
    }
}
