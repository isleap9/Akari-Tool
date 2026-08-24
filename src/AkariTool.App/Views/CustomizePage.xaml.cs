using System;
using Microsoft.UI.Xaml.Controls;
using WinUI.Framework.IoC;
using AkariTool.ViewModels;
using AkariTool.ViewModels.Common;

namespace AkariTool.Views;

/// <summary>
/// Customize hub page. Configures the shared <see cref="Controls.HubView"/> chrome and
/// supplies its overview cards; each drills into its bare detail page inside the hub's
/// inner frame, with live Recommended/Default pills bound to the DI-singleton page VM.
///
/// Replaces the previous code-filled card grid (which navigated the main frame). Glyphs
/// reuse the previous grid's Segoe Fluent glyphs.
/// </summary>
public sealed partial class CustomizePage : Page
{
    public CustomizePage()
    {
        InitializeComponent();

        Hub.HeaderTitle = "Customize";
        Hub.HeaderDescription = "Taskbar, Explorer, appearance, Start menu, and desktop.";
        Hub.HeaderGlyph = "";
        Hub.SearchPlaceholder = "Find a setting…";

        Hub.Cards.Add(new HubCardViewModel(
            "Taskbar", "Layout, behavior & button grouping",
            "", typeof(TaskbarPage), ServiceLocator.GetService<TaskbarViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Explorer", "View, behavior, associations & This PC",
            "", typeof(ExplorerPage), ServiceLocator.GetService<ExplorerViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Appearance", "Theme, transparency, color & window style",
            "", typeof(AppearancePage), ServiceLocator.GetService<AppearanceViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Start Menu", "Start menu layout & behavior",
            "", typeof(StartMenuPage), ServiceLocator.GetService<StartMenuViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Desktop", "Icons, shortcuts, startup, devices & region",
            "", typeof(DesktopPage), ServiceLocator.GetService<DesktopViewModel>()));
    }

    /// <summary>Drills straight into a section's detail page (used by global search / Home
    /// cards routing through the hub). Returns false if this hub owns no such card.</summary>
    public bool ShowDetailFor(Type detailPageType) => Hub.TryShowDetailByPageType(detailPageType);
}
