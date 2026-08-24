using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace WinUI.Framework.Navigation;

/// <summary>
/// Encapsulates Frame-based page navigation for the app.
/// </summary>
public interface INavigationService
{
    /// <summary>The <see cref="Frame"/> that hosts the pages. Attach once at startup.</summary>
    Frame? Frame { get; set; }

    /// <summary>Whether the navigation stack can go back.</summary>
    bool CanGoBack { get; }

    /// <summary>Navigates to the previous page in the navigation stack.</summary>
    void GoBack();

    /// <summary>Clears the forward/back navigation history.</summary>
    void ClearHistory();

    /// <summary>Navigates to the specified page type.</summary>
    bool NavigateTo<T>() where T : Page;

    /// <summary>Navigates to the specified page type, passing a navigation parameter.</summary>
    bool NavigateTo<T>(object? parameter) where T : Page;

    /// <summary>
    /// Navigates to the specified page type, passing a navigation parameter and
    /// an optional transition animation.
    /// </summary>
    bool NavigateTo<T>(object? parameter, NavigationTransitionInfo? transition) where T : Page;

    /// <summary>Navigates to the specified page type with an optional parameter.</summary>
    bool NavigateTo(Type pageType, object? parameter = null);

    /// <summary>
    /// Navigates to the specified page type with a parameter and transition animation.
    /// </summary>
    bool NavigateTo(Type pageType, object? parameter, NavigationTransitionInfo? transition);
}
