using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Notifications
{
    public partial class NotificationsTab : BaseTab
    {
        private readonly List<Action> _refreshActions = new();

        public NotificationsTab() => InitializeComponent();

        public override string NavTag   => "Notifications";
        public override string NavLabel => "Notifications";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Notifications",
                "Control system, app, and lock-screen notification behaviour.",
                withActions: true, RootPanel));

            BuildGeneral(RootPanel);
            BuildAdditional(RootPanel);
            BuildSystem(RootPanel);
            BuildPrivacy(RootPanel);
            BuildSecurity(RootPanel);

            foreach (var r in _refreshActions)
                try { r(); } catch { }
        }

        private void AddSection(StackPanel panel, string title, TweakDefinition[] tweaks)
        {
            var section = TweakHelpers.BuildSection(panel, title);
            foreach (var def in tweaks)
                _refreshActions.Add(TweakHelpers.AddTweakRow(section, def));
            TweakHelpers.AttachBulkActions(section);
        }

        // helpers
        private static bool? ReadAbsent(RegistryHive hive, string sub, string val)
        {
            var v = ReadDword(hive, sub, val);
            return v.HasValue ? v != 0 : true; // absent = enabled
        }

        private static bool? ReadDisable(RegistryHive hive, string sub, string val)
        {
            var v = ReadDword(hive, sub, val);
            return v.HasValue ? v != 1 : true; // 1 = disabled, absent = enabled
        }

        // ══════════════════════════════════════════════════════════════════════
        // GENERAL
        // ══════════════════════════════════════════════════════════════════════

        private void BuildGeneral(StackPanel panel)
        {
            const string PushSub  = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
            const string PushKey  = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\PushNotifications";
            const string NotiSub  = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";
            const string NotiKey  = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";
            const string ExpSub   = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            const string ExpKey   = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

            AddSection(panel, "General", new[]
            {
                new TweakDefinition
                {
                    Id               = "windows-pushnotifications",
                    Name             = "Show Notifications",
                    Description      = "Get notifications from apps and other senders in Windows",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[1,null], DisabledValue=[0], DefaultValue=1
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser, PushSub, "ToastEnabled"),
                    Apply = on =>
                    {
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(PushSub, true); k?.DeleteValue("ToastEnabled", false); }
                        else Registry.SetValue(PushKey, "ToastEnabled", 0, RegistryValueKind.DWord);
                        Log($"Notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-sound",
                    Name             = "Allow notifications to play sounds",
                    Description      = "Play audio alerts when notifications arrive from apps and system senders",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser, NotiSub, "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"),
                    Apply = on =>
                    {
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(NotiSub, true); k?.DeleteValue("NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", false); }
                        else Registry.SetValue(NotiKey, "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", 0, RegistryValueKind.DWord);
                        Log($"Notification sounds {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-toast-above-lock",
                    Name             = "Show notifications on the lock screen",
                    Description      = "Display toast notifications on the lock screen when your device is locked",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser, NotiSub, "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK"),
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(NotiSub, true);
                            k?.DeleteValue("NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", false);
                            using var k2 = Registry.CurrentUser.OpenSubKey(PushSub, true);
                            k2?.DeleteValue("LockScreenToastEnabled", false);
                        }
                        else
                        {
                            Registry.SetValue(NotiKey, "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", 0, RegistryValueKind.DWord);
                            Registry.SetValue(PushKey, "LockScreenToastEnabled", 0, RegistryValueKind.DWord);
                        }
                        Log($"Lock screen notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-critical-toast-above-lock",
                    Name             = "Show reminders and incoming VoIP calls on the lock screen",
                    Description      = "Display critical notifications like reminders and VoIP calls when your device is locked",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser, NotiSub, "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK"),
                    Apply = on =>
                    {
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(NotiSub, true); k?.DeleteValue("NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK", false); }
                        else Registry.SetValue(NotiKey, "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK", 0, RegistryValueKind.DWord);
                        Log($"Critical lock screen notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-show-bell-icon",
                    Name             = "Show notification bell icon",
                    Description      = "Display the notification bell icon in the system tray",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, ExpSub, "ShowNotificationIcon");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(ExpKey, "ShowNotificationIcon", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Notification bell icon {(on ? "shown" : "hidden")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "explorer-action-center",
                    Name             = "Disable Action Center",
                    Description      = "Remove the Action Center / notification flyout from the taskbar entirely via Explorer policy",
                    IsPreference     = true,    // personal preference; not recommended by default
                    DefaultState     = true,    // default = enabled (present)
                    Warning          = "Disabling Action Center can stop some apps from opening — the NVIDIA App and other software that relies on Windows notifications may fail to launch until it's re-enabled. Continue?",
                    WarningState     = false,   // warn only when switching OFF (disabling Action Center)
                    ReadState = () =>
                    {
                        var disabled = SystemStateReader.ReadActionCenter(); // true = disabled
                        return disabled.HasValue ? !disabled.Value : true;   // row ON = Action Center present
                    },
                    Apply = on =>
                    {
                        const string sub = @"Software\Policies\Microsoft\Windows\Explorer";
                        const string key = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("DisableNotificationCenter", false); }
                        else    { Registry.SetValue(key, "DisableNotificationCenter", 1, RegistryValueKind.DWord); }
                        Log($"Action Center {(on ? "enabled" : "disabled")}.");
                    }
                },
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // ADDITIONAL SETTINGS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildAdditional(StackPanel panel)
        {
            const string CDMSub = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            const string CDMKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

            AddSection(panel, "Additional Settings", new[]
            {
                new TweakDefinition
                {
                    Id               = "notifications-welcome-experience",
                    Name             = "Show the Windows welcome experience after updates",
                    Description      = "Show what's new and suggested after updates and when signed in",
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[1,null], DisabledValue=[0], DefaultValue=1
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser, CDMSub, "SubscribedContent-310093Enabled"),
                    Apply = on =>
                    {
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(CDMSub, true); k?.DeleteValue("SubscribedContent-310093Enabled", false); }
                        else Registry.SetValue(CDMKey, "SubscribedContent-310093Enabled", 0, RegistryValueKind.DWord);
                        Log($"Windows welcome experience {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-system-setting-engagement",
                    Name             = "Suggest ways to get the most out of Windows",
                    Description      = "Show suggestions to help you complete device setup and optimize Windows features",
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[1,null], DisabledValue=[0], DefaultValue=1
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled"),
                    Apply = on =>
                    {
                        const string sub = @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement";
                        const string key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("ScoobeSystemSettingEnabled", false); }
                        else Registry.SetValue(key, "ScoobeSystemSettingEnabled", 0, RegistryValueKind.DWord);
                        Log($"Setup/finish suggestions {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-tips-suggestions",
                    Name             = "Get tips and suggestions when using Windows",
                    Description      = "Show helpful tips and suggestions while using Windows",
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, CDMSub, "SubscribedContent-338389Enabled");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(CDMKey, "SubscribedContent-338389Enabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Windows tips and suggestions {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-system-pane-suggestions",
                    Name             = "Show suggestions in Notification Center",
                    Description      = "Display helpful suggestions in the Action Center and Notification Center",
                    RecommendedState = false,
                    DefaultState     = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, CDMSub, "SystemPaneSuggestionsEnabled");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(CDMKey, "SystemPaneSuggestionsEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Notification Center suggestions {(on ? "enabled" : "disabled")}.");
                    }
                },
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // SYSTEM NOTIFICATIONS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildSystem(StackPanel panel)
        {
            AddSection(panel, "System Notifications", new[]
            {
                new TweakDefinition
                {
                    Id               = "notifications-capability-access",
                    Name             = "Capability Access Notifications",
                    Description      = "Show notifications when apps request access to system capabilities and permissions",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.CapabilityAccess", "Enabled"),
                    Apply = on =>
                    {
                        const string key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.CapabilityAccess";
                        const string sub = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.CapabilityAccess";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("Enabled", false); }
                        else Registry.SetValue(key, "Enabled", 0, RegistryValueKind.DWord);
                        Log($"Capability access notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-startup-app",
                    Name             = "Startup App Notifications",
                    Description      = "Show notifications when apps are added to your Windows startup list",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.StartupApp", "Enabled"),
                    Apply = on =>
                    {
                        const string key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.StartupApp";
                        const string sub = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.StartupApp";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("Enabled", false); }
                        else Registry.SetValue(key, "Enabled", 0, RegistryValueKind.DWord);
                        Log($"Startup app notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-clock-change",
                    Name             = "Clock Change Notifications",
                    Description      = "Show notifications when daylight saving time changes occur",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser, @"Control Panel\Desktop", "DstNotification"),
                    Apply = on =>
                    {
                        const string key = @"HKEY_CURRENT_USER\Control Panel\Desktop";
                        const string sub = @"Control Panel\Desktop";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("DstNotification", false); }
                        else Registry.SetValue(key, "DstNotification", 0, RegistryValueKind.DWord);
                        Log($"Clock change notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // PRIVACY NOTIFICATIONS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildPrivacy(StackPanel panel)
        {
            AddSection(panel, "Privacy Notifications", new[]
            {
                new TweakDefinition
                {
                    Id               = "notifications-app-location-request",
                    Name             = "Notify when apps request location",
                    Description      = "Show notifications when apps attempt to access your location information",
                    RecommendedState = true,
                    DefaultState     = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "ShowGlobalPrompts");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                            "ShowGlobalPrompts", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Location request notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        // SECURITY NOTIFICATIONS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildSecurity(StackPanel panel)
        {
            const string WDSCSub  = @"Software\Microsoft\Windows Defender Security Center\Notifications";
            const string WDSCKey  = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows Defender Security Center\Notifications";
            const string WDSCPolSub = @"Software\Policies\Microsoft\Windows Defender Security Center\Notifications";

            AddSection(panel, "Security Notifications", new[]
            {
                new TweakDefinition
                {
                    Id               = "notifications-windows-security",
                    Name             = "Windows Security Notifications",
                    Description      = "Show all notifications from Windows Security about threats, scans, and protection status",
                    RecommendedState = true,
                    DefaultState     = true,
                    // EnabledValue=[0,null], DisabledValue=[1] → ON=key absent, OFF=1
                    ReadState = () => ReadDisable(RegistryHive.LocalMachine, WDSCSub, "DisableNotifications"),
                    Apply = on =>
                    {
                        if (on)
                        {
                            Registry.LocalMachine.OpenSubKey(WDSCSub, true)?.DeleteValue("DisableNotifications", false);
                            foreach (var h in new[] { Registry.CurrentUser, Registry.LocalMachine })
                            {
                                var k = h.OpenSubKey(WDSCPolSub, true);
                                k?.DeleteValue("DisableNotifications", false);
                                k?.DeleteValue("DisableEnhancedNotifications", false);
                            }
                        }
                        else
                        {
                            Registry.SetValue(WDSCKey, "DisableNotifications", 1, RegistryValueKind.DWord);
                            foreach (var h in new[] { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE" })
                            {
                                var path = $@"{h}\Software\Policies\Microsoft\Windows Defender Security Center\Notifications";
                                Registry.SetValue(path, "DisableNotifications",         1, RegistryValueKind.DWord);
                                Registry.SetValue(path, "DisableEnhancedNotifications", 1, RegistryValueKind.DWord);
                            }
                        }
                        Log($"Windows Security notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "notifications-security-maintenance",
                    Name             = "Security and Maintenance Notifications",
                    Description      = "Show notifications from the Security and Maintenance Action Center",
                    RecommendedState = true,
                    DefaultState     = true,
                    // EnabledValue=[1,null], DisabledValue=[0], DefaultValue=1
                    ReadState = () => ReadAbsent(RegistryHive.CurrentUser,
                        @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance", "Enabled"),
                    Apply = on =>
                    {
                        const string key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance";
                        const string sub = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("Enabled", false); }
                        else Registry.SetValue(key, "Enabled", 0, RegistryValueKind.DWord);
                        Log($"Security and Maintenance notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
            });
        }

        // ── Registry helpers ──────────────────────────────────────────────────

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) is int i ? i : (int?)null; }
            catch { return null; }
        }

        private void Log(string msg) => Service?.Log(msg);
    }
}