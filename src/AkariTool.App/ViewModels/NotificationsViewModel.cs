using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Notifications;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Notifications page. First-wave rollout tab — a single-catalog, standard
/// tweak-row tab with no bespoke sections, reusing the Gaming rendering layer.
///
/// Section order matches net8's NotificationsTab.Build() exactly: General →
/// Additional Settings → System Notifications → Privacy Notifications → Security
/// Notifications.
///
/// ⚠ The Security Notifications section contains Windows Defender *notification*
/// toggles (`notifications-windows-security`). These only touch notification
/// registry keys — they do NOT disable, arm, or otherwise alter Defender
/// protection. They are the same rows the net8 build ships and are fine to render
/// unchanged; nothing Defender-protective is touched here.
/// </summary>
public sealed partial class NotificationsViewModel : TweakPageViewModel
{
    public NotificationsViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Notifications";
        Subtitle = "Control system, app, and lock-screen notification behaviour.";
    }

    public override string NavTag => "Notifications";
    public override string NavLabel => "Notifications";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("General", NotificationsTweaks.General(log));
        yield return ("Additional Settings", NotificationsTweaks.AdditionalSettings(log));
        yield return ("System Notifications", NotificationsTweaks.SystemNotifications(log));
        yield return ("Privacy Notifications", NotificationsTweaks.PrivacyNotifications(log));
        yield return ("Security Notifications", NotificationsTweaks.SecurityNotifications(log));
    }
}
