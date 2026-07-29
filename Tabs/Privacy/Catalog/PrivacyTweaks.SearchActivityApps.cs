using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Privacy
{
    public static partial class PrivacyTweaks
    {
        // ══════════════════════════════════════════════════════════════════════
        // SEARCH PERMISSIONS
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] SearchPermissions(Action<string> Log)
        {
            const string SS  = @"Software\Microsoft\Windows\CurrentVersion\SearchSettings";
            const string SSH = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings";

            TweakDefinition SearchToggle(string id, string name, string desc, string valueName)
                => new TweakDefinition
                {
                    Id = id, Name = name, Description = desc,
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, SS, valueName); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(SS, true); k?.DeleteValue(valueName, false); }
                        else Registry.SetValue(SSH, valueName, 0, RegistryValueKind.DWord);
                        Log($"{name} {(on ? "enabled" : "disabled")}.");
                    }
                };

            return new[]
            {
                SearchToggle("privacy-search-history", "Search history on this device",
                    "Improves search results by allowing Windows Search to store your search history locally", "IsDeviceSearchHistoryEnabled"),
                SearchToggle("privacy-search-highlights", "Show search highlights",
                    "See content suggestions in search", "IsDynamicSearchBoxEnabled"),
                SearchToggle("privacy-search-msa-cloud", "Cloud Content Search (Microsoft account)",
                    "Allow Windows Search to show results from services signed in with your Microsoft account", "IsMSACloudSearchEnabled"),
                SearchToggle("privacy-search-aad-cloud", "Cloud Content Search (Work or School account)",
                    "Allow Windows Search to show results from services signed in with your work or school account", "IsAADCloudSearchEnabled"),
                new TweakDefinition
                {
                    Id = "privacy-allow-cortana", Name = "Allow Cortana",
                    Description = "Enables Microsoft's Cortana virtual assistant for voice commands and searches",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana"); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        if (on) { foreach (var h in new[] { Registry.LocalMachine, Registry.CurrentUser }) h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true)?.DeleteValue("AllowCortana", false); }
                        else { foreach (var h in new[] { "HKEY_LOCAL_MACHINE","HKEY_CURRENT_USER" }) Registry.SetValue($@"{h}\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, RegistryValueKind.DWord); }
                        Log($"Cortana {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // ACTIVITY HISTORY
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] ActivityHistory(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "privacy-activity-history", Name = "Activity History",
                    Description = "Allows Windows to jump back into what you were doing with apps, docs, or other activities on startup (Windows 10 Timeline feature)",
                    RecommendedState = false, DefaultState = true,
                    // EnabledValue=[null], DisabledValue=[0]
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities");
                        return v.HasValue ? v != 0 : true;
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System", true);
                            k?.DeleteValue("PublishUserActivities", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\System",
                                "PublishUserActivities", 0, RegistryValueKind.DWord);
                        Log($"Activity History {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-timeline-suggestions", Name = "Timeline Suggestions",
                    Description = "Shows suggestions in the Windows 10 Timeline feature based on your activity history",
                    RecommendedState = false, DefaultState = true,
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            "SubscribedContent-353698Enabled");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            "SubscribedContent-353698Enabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Timeline Suggestions {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // APP PERMISSIONS
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] AppPermissions(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "privacy-location-services", Name = "Location Services",
                    Description = "Allows Windows and apps to access your device location for location-based features",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => ReadConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location"),
                    Apply = on =>
                    {
                        WriteConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", on);
                        if (!on) { foreach (var h in new[] { Registry.CurrentUser, Registry.LocalMachine }) h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", true)?.DeleteValue("DisableLocation", false); }
                        Log($"Location Services {(on ? "allowed" : "denied")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-camera-access", Name = "Camera Access", IsPreference = true,
                    Description = "Allow apps to have camera access",
                    RecommendedState = true, DefaultState = true,
                    ReadState = () => ReadConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam"),
                    Apply = on => { WriteConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam", on); Log($"Camera Access {(on ? "allowed" : "denied")}."); }
                },
                new TweakDefinition
                {
                    Id = "privacy-microphone-access", Name = "Microphone Access", IsPreference = true,
                    Description = "Allow apps to have microphone access",
                    RecommendedState = true, DefaultState = true,
                    ReadState = () => ReadConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone"),
                    Apply = on => { WriteConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", on); Log($"Microphone Access {(on ? "allowed" : "denied")}."); }
                },
                new TweakDefinition
                {
                    Id = "privacy-account-info-access", Name = "Account Info Access",
                    Description = "Allow apps to have account info access",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => ReadConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation"),
                    Apply = on => { WriteConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation", on); Log($"Account Info Access {(on ? "allowed" : "denied")}."); }
                },
                new TweakDefinition
                {
                    Id = "privacy-app-diagnostic-access", Name = "App Diagnostic Access",
                    Description = "Allow apps to have app diagnostic access",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => ReadConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics"),
                    Apply = on => { WriteConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics", on); Log($"App Diagnostic Access {(on ? "allowed" : "denied")}."); }
                },
                new TweakDefinition
                {
                    Id = "privacy-onedrive-auto-backup", Name = "OneDrive Automatic Backups", IsPreference = true,
                    Description = "Controls whether OneDrive automatically backs up your Documents, Pictures, and Desktop folders",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\OneDrive", "KFMBlockOptIn"); return v.HasValue ? v != 1 : true; },
                    Apply = on =>
                    {
                        if (on) { foreach (var h in new[] { Registry.LocalMachine, Registry.CurrentUser }) h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\OneDrive", true)?.DeleteValue("KFMBlockOptIn", false); }
                        else { foreach (var h in new[] { "HKEY_LOCAL_MACHINE","HKEY_CURRENT_USER" }) Registry.SetValue($@"{h}\SOFTWARE\Policies\Microsoft\OneDrive", "KFMBlockOptIn", 1, RegistryValueKind.DWord); }
                        Log($"OneDrive Auto Backup {(on ? "allowed" : "blocked")}.");
                    }
                },
            };
        }

    }
}
