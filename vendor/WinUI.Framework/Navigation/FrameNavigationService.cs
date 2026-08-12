using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace WinUI.Framework.Navigation;

/// <summary>
/// Default <see cref="INavigationService"/> implementation backed by a
/// <see cref="Frame"/>. Pages are created by the Frame itself, so they must
/// have a parameterless constructor; resolve their view models from
/// <see cref="IoC.ServiceLocator"/> (or constructor-inject services into the
/// view models, which ARE created by the DI container).
///
/// Page caching: set <c>NavigationCacheMode</c> inside a page to keep its
/// state when navigating away, e.g.
/// <code>public DetailPage() { InitializeComponent(); NavigationCacheMode = NavigationCacheMode.Required; }</code>
/// </summary>
public class FrameNavigationService : INavigationService
{
    public Frame? Frame { get; set; }

    public bool CanGoBack => Frame?.CanGoBack ?? false;

    public void GoBack() => Frame?.GoBack();

    public void ClearHistory()
    {
        if (Frame is null)
        {
            return;
        }

        Frame.BackStack.Clear();
        Frame.ForwardStack.Clear();
    }

    public bool NavigateTo<T>() where T : Page => NavigateTo(typeof(T), null, null);

    public bool NavigateTo<T>(object? parameter) where T : Page => NavigateTo(typeof(T), parameter, null);

    public bool NavigateTo<T>(object? parameter, NavigationTransitionInfo? transition) where T : Page
        => NavigateTo(typeof(T), parameter, transition);

    public bool NavigateTo(Type pageType, object? parameter = null)
        => NavigateTo(pageType, parameter, null);

    public bool NavigateTo(Type pageType, object? parameter, NavigationTransitionInfo? transition)
    {
        if (Frame is null)
        {
            throw new InvalidOperationException(
                "No Frame is attached to the navigation service. Assign the Frame property before navigating.");
        }

        if (!typeof(Page).IsAssignableFrom(pageType))
        {
            throw new ArgumentException($"'{pageType.FullName}' is not a Page type.", nameof(pageType));
        }

        return transition is null
            ? Frame.Navigate(pageType, parameter)
            : Frame.Navigate(pageType, parameter, transition);
    }
}
