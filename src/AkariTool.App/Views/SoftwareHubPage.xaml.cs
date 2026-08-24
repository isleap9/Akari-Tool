using System;
using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels.Common;

namespace AkariTool.Views;

/// <summary>
/// Software &amp; Apps hub page. Configures the shared <see cref="Controls.HubView"/> chrome
/// and supplies its cards (Windows Apps / External Apps / Debloat). These are app-catalog
/// pages with their own toolbars, so no pills, hub Quick Actions disabled, and the hub search
/// is hidden; each drills into its page inside the hub's inner frame. Folds the old SOFTWARE
/// rail group into one hub.
/// </summary>
public sealed partial class SoftwareHubPage : Page
{
    public SoftwareHubPage()
    {
        InitializeComponent();

        Hub.HeaderTitle = "Software & Apps";
        Hub.HeaderDescription = "Install apps and remove Windows bloatware.";
        Hub.HeaderGlyph = "";
        Hub.ShowSearch = false;   // each catalog page has its own search toolbar

        Hub.Cards.Add(new HubCardViewModel(
            "Windows Apps", "Remove built-in Windows apps & bloatware",
            "", typeof(WindowsAppsPage)));

        Hub.Cards.Add(new HubCardViewModel(
            "External Apps", "Install popular apps via winget",
            "", typeof(ExternalAppsPage)));

        Hub.Cards.Add(new HubCardViewModel(
            "Debloat", "Scripted debloat & cleanup presets",
            "", typeof(DebloatPage)));
    }

    /// <summary>Drills straight into a catalog page (used by search / Home card routing).</summary>
    public bool ShowDetailFor(Type detailPageType) => Hub.TryShowDetailByPageType(detailPageType);
}
