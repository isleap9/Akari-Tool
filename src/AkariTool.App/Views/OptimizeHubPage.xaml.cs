using Microsoft.UI.Xaml.Controls;
using WinUI.Framework.IoC;
using AkariTool.ViewModels;
using AkariTool.ViewModels.Common;

namespace AkariTool.Views;

/// <summary>
/// Optimize hub page. Configures the shared <see cref="Controls.HubView"/> chrome and
/// supplies its overview cards. AkariOS is pinned on top (bespoke page, no pills);
/// Gaming / Privacy / Power / Update / Notifications / Sound each drill into their bare
/// detail page inside the hub's inner frame, with live Recommended/Default pills bound to
/// the corresponding DI-singleton <c>SettingPageViewModel</c>.
///
/// Reimplemented from Winhance's OptimizePage (which hand-codes each card) as a thin
/// card-list supplier over the reusable HubView. Glyphs reuse the previous rail's Segoe
/// Fluent glyphs (proven-renderable); easy to retune after hardware review.
/// </summary>
public sealed partial class OptimizeHubPage : Page
{
    public OptimizeHubPage()
    {
        InitializeComponent();

        Hub.HeaderTitle = "Optimize";
        Hub.HeaderDescription = "Performance, privacy, power, and system tuning.";
        Hub.HeaderGlyph = "";
        Hub.SearchPlaceholder = "Find a setting…";

        // AkariOS — pinned, bespoke (no settings VM → renders no pills).
        Hub.Cards.Add(new HubCardViewModel(
            "AkariOS", "Service presets, Playbook, BCD, Competitive Mode & PostInstall",
            "", typeof(AkariOSPage), pageVm: null, isPinned: true));

        // Optimize detail pages — pills bind to the DI-singleton page VMs.
        Hub.Cards.Add(new HubCardViewModel(
            "Gaming & Performance", "GPU scheduling, Game Mode, latency & performance tweaks",
            "", typeof(GamingPage), ServiceLocator.GetService<GamingViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Privacy & Security", "Telemetry, tracking, permissions & data collection",
            "", typeof(PrivacyPage), ServiceLocator.GetService<PrivacyViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Power", "Power plans, sleep, USB & processor power management",
            "", typeof(PowerPage), ServiceLocator.GetService<PowerViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Windows Updates", "Update policy, delivery optimization & driver updates",
            "", typeof(UpdatePage), ServiceLocator.GetService<UpdateViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Notifications", "Notification toasts, tips, suggestions & lock-screen prompts",
            "", typeof(NotificationsPage), ServiceLocator.GetService<NotificationsViewModel>()));

        Hub.Cards.Add(new HubCardViewModel(
            "Sound", "Communication ducking, spatial audio & sound behavior",
            "", typeof(SoundPage), ServiceLocator.GetService<SoundViewModel>()));
    }
}
