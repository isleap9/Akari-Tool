using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Privacy
{
    public static partial class PrivacyTweaks
    {
        // ══════════════════════════════════════════════════════════════════════
        // CONTENT DELIVERY & ADVERTISING
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] ContentDelivery(Action<string> Log)
        {
            const string CDM = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            const string CDMSub = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

            TweakDefinition Toggle(string id, string name, string desc, string valueName, bool recommended = false, bool defState = true, bool isAbsent = false)
                => new TweakDefinition
                {
                    Id = id, Name = name, Description = desc,
                    RecommendedState = recommended, DefaultState = defState,
                    ReadState = isAbsent
                        ? () => { var v = ReadDword(RegistryHive.CurrentUser, CDMSub, valueName); return v.HasValue ? v != 0 : true; }
                        : () => { var v = ReadDword(RegistryHive.CurrentUser, CDMSub, valueName); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        if (isAbsent && on) { using var k = Registry.CurrentUser.OpenSubKey(CDMSub, true); k?.DeleteValue(valueName, false); }
                        else Registry.SetValue(CDM, valueName, on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"{name} {(on ? "enabled" : "disabled")}.");
                    }
                };

            return new[]
            {
                Toggle("privacy-content-delivery-allowed", "Content Delivery",
                    "Allows Windows to deliver promotional content and automatically install suggested apps", "ContentDeliveryAllowed"),
                Toggle("privacy-subscribed-content", "Subscribed Content",
                    "Enables promotional content subscriptions from Microsoft and partners throughout Windows",
                    "SubscribedContentEnabled", isAbsent: true),
                Toggle("privacy-feature-management", "Feature Management",
                    "Enables Windows feature management functionality for promotional features and automatic app installations",
                    "FeatureManagementEnabled"),
                Toggle("privacy-soft-landing", "Soft Landing Experiences",
                    "Displays tips and notifications about Windows features as you use the operating system",
                    "SoftLandingEnabled"),
                Toggle("privacy-oem-preinstalled-apps", "OEM Pre-installed Apps",
                    "Prevents OEM manufacturers from automatically installing bloatware apps", "OemPreInstalledAppsEnabled"),
                Toggle("privacy-preinstalled-apps", "Pre-installed Suggested Apps",
                    "Prevents Microsoft from automatically installing suggested apps", "PreInstalledAppsEnabled"),
                Toggle("privacy-preinstalled-apps-ever", "Pre-installed Apps History Tracking",
                    "Disables tracking of whether pre-installed apps were ever enabled",
                    "PreInstalledAppsEverEnabled", isAbsent: true),
                Toggle("privacy-silent-installed-apps", "Silent App Installation",
                    "Prevents apps from being silently installed in the background", "SilentInstalledAppsEnabled"),
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // LOCK SCREEN
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] LockScreen(Action<string> Log)
        {
            const string CDM = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            const string CDMSub = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

            return new[]
            {
                new TweakDefinition
                {
                    Id = "privacy-lock-screen", Name = "Lock Screen",
                    Description = "Allows users to lock their computer using Windows+L, Start menu, or Ctrl+Alt+Del. Disabling prevents locking the workstation",
                    IsPreference = true, RecommendedState = true, DefaultState = true,
                    // EnabledValue=[0], DisabledValue=[1], DefaultValue=0
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "DisableLockWorkstation");
                        return v.HasValue ? v != 1 : true; // absent/0 = enabled
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon",
                            "DisableLockWorkstation", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Lock Screen {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-rotating-lock-screen", Name = "Windows Spotlight on Lock Screen",
                    Description = "Displays rotating Windows Spotlight images on your lock screen instead of a static background",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, CDMSub, "RotatingLockScreenEnabled"); return v.HasValue ? v == 1 : true; },
                    Apply = on => { Registry.SetValue(CDM, "RotatingLockScreenEnabled", on ? 1 : 0, RegistryValueKind.DWord); Log($"Windows Spotlight {(on ? "enabled" : "disabled")}."); }
                },
                new TweakDefinition
                {
                    Id = "privacy-lock-screen-overlay", Name = "Lock Screen Fun Facts and Tips",
                    Description = "Displays fun facts, tips, and tricks as an overlay on your lock screen",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, CDMSub, "RotatingLockScreenOverlayEnabled"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(CDM, "RotatingLockScreenOverlayEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(CDM, "SubscribedContent-338387Enabled",  on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Lock Screen Fun Facts {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // GENERAL
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] General(Action<string> Log)
        {
            const string CDM = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            const string CDMSub = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

            return new[]
            {
                new TweakDefinition
                {
                    Id = "privacy-advertising-id", Name = "Let apps show personalized ads by using my advertising ID",
                    Description = "Windows generates a unique advertising ID that apps use to track your activity and deliver personalized ads",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\AdvertisingInfo", "Value", on ? 1 : 0, RegistryValueKind.DWord);
                        if (on) { foreach (var h in new[] { Registry.CurrentUser, Registry.LocalMachine }) h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", true)?.DeleteValue("DisabledByGroupPolicy", false); }
                        else { foreach (var h in new[] { "HKEY_CURRENT_USER","HKEY_LOCAL_MACHINE" }) Registry.SetValue($@"{h}\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1, RegistryValueKind.DWord); }
                        Log($"Advertising ID {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-language-list", Name = "Let websites show locally relevant content by accessing my language list",
                    Description = "Allows websites to access your language preferences so they can display content in your preferred language",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut"); return v.HasValue ? v != 1 : true; },
                    Apply = on =>
                    {
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\International\User Profile", true); k?.DeleteValue("HttpAcceptLanguageOptOut", false); }
                        else Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1, RegistryValueKind.DWord);
                        Log($"Language list website access {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-app-launch-tracking", Name = "Let Windows improve Start and search results by tracking app launches",
                    Description = "Windows records which apps you use most frequently to personalize your Start menu and improve search results",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs"); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true); k?.DeleteValue("Start_TrackProgs", false); }
                        else Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0, RegistryValueKind.DWord);
                        Log($"App launch tracking {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-settings-content", Name = "Show me suggested content in the Settings app",
                    Description = "Displays promotional content, tips, and feature suggestions within the Windows Settings app",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, CDMSub, "SubscribedContent-338393Enabled"); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        foreach (var val in new[] { "SubscribedContent-338393Enabled","SubscribedContent-353694Enabled","SubscribedContent-353696Enabled" })
                        { if (on) { using var k = Registry.CurrentUser.OpenSubKey(CDMSub, true); k?.DeleteValue(val, false); } else Registry.SetValue(CDM, val, 0, RegistryValueKind.DWord); }
                        Log($"Settings app suggested content {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-settings-notifications", Name = "Settings App Notifications",
                    Description = "Shows account notifications in the Settings app, including prompts to reauthenticate and backup your device",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications", "EnableAccountNotifications"); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        const string sub = @"Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("EnableAccountNotifications", false); }
                        else Registry.SetValue(@"HKEY_CURRENT_USER\" + sub, "EnableAccountNotifications", 0, RegistryValueKind.DWord);
                        Log($"Settings app notifications {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

    }
}
