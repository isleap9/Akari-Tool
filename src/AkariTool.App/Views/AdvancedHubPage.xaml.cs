using System;
using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels.Common;

namespace AkariTool.Views;

/// <summary>
/// Advanced Tools hub page. Configures the shared <see cref="Controls.HubView"/> chrome and
/// supplies its cards. All four targets are bespoke pages (no settings VM → no pills, hub
/// Quick Actions disabled); each drills into its page inside the hub's inner frame.
/// Folds the old Advanced Tools / Tools / Backup / Verify rail entries into one hub.
/// </summary>
public sealed partial class AdvancedHubPage : Page
{
    public AdvancedHubPage()
    {
        InitializeComponent();

        Hub.HeaderTitle = "Advanced Tools";
        Hub.HeaderDescription = "Imaging & deployment, system tools, backup and verification.";
        Hub.HeaderGlyph = "";
        Hub.ShowSearch = false;   // bespoke pages have no per-page search

        Hub.Cards.Add(new HubCardViewModel(
            "Image & Deployment", "WIM/ISO wizard & Autounattend generator",
            "", typeof(AdvancedToolsPage)));

        Hub.Cards.Add(new HubCardViewModel(
            "System Tools", "System info, repair, network & quick shortcuts",
            "", typeof(ToolsPage)));

        Hub.Cards.Add(new HubCardViewModel(
            "Backup & Restore", "Export & import your Akari settings",
            "", typeof(BackupPage)));

        Hub.Cards.Add(new HubCardViewModel(
            "Verify", "Detect settings Windows has reverted (drift)",
            "", typeof(VerifyPage)));
    }

    /// <summary>Drills straight into a tool page (used by the drift banner / search routing).</summary>
    public bool ShowDetailFor(Type detailPageType) => Hub.TryShowDetailByPageType(detailPageType);
}
