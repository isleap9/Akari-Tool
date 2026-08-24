using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace AkariTool.Views;

/// <summary>
/// Software &amp; Apps page (Winhance-style tabbed layout). A hub-style header + a two-segment
/// tab header switching an inner frame between the existing catalog pages
/// (<see cref="WindowsAppsPage"/> / <see cref="ExternalAppsPage"/>). Stage A of the Software
/// parity work; the shared toolbar and view modes build on this shell.
/// </summary>
public sealed partial class SoftwareAppsPage : Page
{
    public SoftwareAppsPage()
    {
        InitializeComponent();
        SelectTab(typeof(WindowsAppsPage));   // default tab
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn) return;
        var target = (btn.Tag as string) == "External" ? typeof(ExternalAppsPage) : typeof(WindowsAppsPage);
        SelectTab(target);
    }

    private void SelectTab(Type pageType)
    {
        bool windows = pageType == typeof(WindowsAppsPage);
        TabWindows.IsChecked = windows;
        TabExternal.IsChecked = !windows;
        if (TabFrame.CurrentSourcePageType != pageType)
            TabFrame.Navigate(pageType);
    }

    /// <summary>Selects the tab whose catalog page matches <paramref name="detailPageType"/>
    /// (used by rail/search routing). Returns false for anything that isn't a Software tab.</summary>
    public bool ShowDetailFor(Type detailPageType)
    {
        if (detailPageType == typeof(WindowsAppsPage) || detailPageType == typeof(ExternalAppsPage))
        {
            SelectTab(detailPageType);
            return true;
        }
        return false;
    }
}
