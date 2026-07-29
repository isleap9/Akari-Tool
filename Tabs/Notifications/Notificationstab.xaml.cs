using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            BuildSounds(RootPanel);

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
                    RecommendedState = false,   // Windows default: Action Center present
                    DefaultState     = true,    // default = enabled (present)
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

        private static string? ReadString(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) as string; }
            catch { return null; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // SYSTEM SOUNDS (Winhance port)
        // ══════════════════════════════════════════════════════════════════════

        private void BuildSounds(StackPanel panel)
        {
            const string BootKey  = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation";
            const string BootSub  = @"Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation";
            const string EdKey    = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\EditionOverrides";
            const string EdSub    = @"Software\Microsoft\Windows\CurrentVersion\EditionOverrides";
            const string DuckKey  = @"HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio";
            const string DuckSub  = @"Software\Microsoft\Multimedia\Audio";
            const string NarrKey  = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam";
            const string NarrSub  = @"Software\Microsoft\Narrator\NoRoam";
            const string VoiceKey = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings";
            const string VoiceSub = @"Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings";
            const string AccKey   = @"HKEY_CURRENT_USER\Control Panel\Accessibility";
            const string AccSub   = @"Control Panel\Accessibility";

            AddSection(panel, "System Sounds", new[]
            {
                new TweakDefinition
                {
                    Id               = "sound-startup",
                    Name             = "Startup Sound During Boot",
                    Description      = "Play the Windows startup sound when the system boots",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    // Disabled when DisableStartupSound=1 (either key); absent/0 = enabled
                    ReadState = () =>
                    {
                        var a = ReadDword(RegistryHive.LocalMachine, BootSub, "DisableStartupSound");
                        var b = ReadDword(RegistryHive.LocalMachine, EdSub,   "UserSetting_DisableStartupSound");
                        return (a ?? 0) != 1 && (b ?? 0) != 1;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(BootKey, "DisableStartupSound",             on ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(EdKey,   "UserSetting_DisableStartupSound", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Startup sound {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id           = "sound-ducking",
                    Name         = "Sound Ducking Preference",
                    Description  = "How Windows adjusts other sounds when it detects communication activity",
                    IsPreference = true,
                    InputKind    = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Mute all other sounds",                     0),
                        new TweakDropdownOption("Reduce the volume of other sounds by 80%", 1, IsDefault: true),
                        new TweakDropdownOption("Reduce the volume of other sounds by 50%", 2),
                        new TweakDropdownOption("Do nothing",                               3, IsRecommended: true),
                    },
                    // Absent = Windows default (index 1)
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, DuckSub, "UserDuckingPreference") ?? 1;
                        return v is >= 0 and <= 3 ? v : 1;
                    },
                    ApplyIndex = idx =>
                    {
                        Registry.SetValue(DuckKey, "UserDuckingPreference", idx, RegistryValueKind.DWord);
                        Log($"Sound ducking preference set to option {idx}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "sound-narrator-ducking",
                    Name             = "Narrator Audio Ducking",
                    Description      = "Lower the volume of other apps while Narrator is speaking",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => (ReadDword(RegistryHive.CurrentUser, NarrSub, "DuckAudio") ?? 1) != 0,
                    Apply = on =>
                    {
                        Registry.SetValue(NarrKey, "DuckAudio", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Narrator audio ducking {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "sound-voice-activation",
                    Name             = "Voice Activation for Apps",
                    Description      = "Allow apps to listen for voice keywords and activate via speech",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => (ReadDword(RegistryHive.LocalMachine, VoiceSub, "AgentActivationEnabled") ?? 1) != 0,
                    Apply = on =>
                    {
                        Registry.SetValue(VoiceKey, "AgentActivationEnabled",  on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(VoiceKey, "AgentActivationLastUsed", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Voice activation for apps {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "sound-accessibility",
                    Name             = "Accessibility Activation & Warning Sounds",
                    Description      = "Play sounds when accessibility features like Sticky Keys are activated",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () =>
                    {
                        // REG_SZ "1"/"0" on some builds, DWORD on others — handle both
                        var s = ReadString(RegistryHive.CurrentUser, AccSub, "Sound on Activation");
                        if (s != null) return s != "0";
                        return (ReadDword(RegistryHive.CurrentUser, AccSub, "Sound on Activation") ?? 1) != 0;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(AccKey, "Sound on Activation", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(AccKey, "Warning Sounds",      on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Accessibility sounds {(on ? "enabled" : "disabled")}.");
                    }
                },
            });
        }

        private void Log(string msg) => Service?.Log(msg);
    }
}