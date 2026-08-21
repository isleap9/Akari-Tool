namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Computed badge state for one sidebar navigation entry (Winhance INavBadgeService parity,
/// adapted to Akari's recommended/default pending counts instead of Config Review Mode).
/// </summary>
/// <param name="Tag">NavigationViewItem Tag (= the page's NavTag).</param>
/// <param name="Count">Total pending rows; 0 means no badge.</param>
public record NavBadgeUpdate(string Tag, int Count);

/// <summary>
/// Computes per-page navigation badge state from the declarative SettingPageViewModel set.
/// </summary>
public interface INavBadgeService
{
    IReadOnlyList<NavBadgeUpdate> ComputeNavBadges();

    /// <summary>
    /// Invokes <paramref name="onChanged"/> (tag, total-pending) whenever a page's
    /// Recommended/Default pending counts change. Returns an unsubscribe handle.
    /// </summary>
    IDisposable Subscribe(Action<string, int> onChanged);
}
