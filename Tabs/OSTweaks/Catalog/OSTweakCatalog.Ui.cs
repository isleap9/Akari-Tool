using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.OSTweaks
{
    public static partial class OSTweakCatalog
    {
        // ══════════════════════════════════════════════════════════════════════
        // UI & VISUAL
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] Ui(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "os-disable-notifications", Name = "Disable Notifications",
                    Description = "Disables Windows push notifications service and toast notification sounds",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WpnService", "Start");
                        return v.HasValue ? v == 4 : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WpnService", "Start", disable ? 4 : 2, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", disable ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\PushNotifications", "NoCloudApplicationNotification", disable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Notifications {(disable ? "disabled" : "enabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-action-center", Name = "Disable Action Center",
                    Description = "Hides the Action Center / notification panel in the taskbar",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableNotificationCenter");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableNotificationCenter", disable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Action Center {(disable ? "disabled" : "enabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-lock-screen", Name = "Disable Lock Screen",
                    Description = "Removes the lock screen on startup and sleep wake, going directly to the login prompt",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreen");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreen", disable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Lock screen {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-animations", Name = "Disable Animations",
                    Description = "Disables window animations and visual effects for a snappier desktop response",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting");
                        return v.HasValue ? v == 3 : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\DWM", "EnableAeroPeek", disable ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", disable ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "IconsOnly", disable ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", disable ? 3 : 1, RegistryValueKind.DWord);
                        Log($"Animations {(disable ? "disabled" : "enabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-transparency-effects", Name = "Transparency Effects",
                    Description = "Enables/disables Fluent Design transparency effects on the taskbar and other surfaces",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = enable =>
                    {
                        using var key = TweakHelpers.CreateRealHkcuSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                        key.SetValue("EnableTransparency", enable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Transparency effects {(enable ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-wallpaper-quality", Name = "Disable Wallpaper Quality Reduction",
                    Description = "Prevents Windows from compressing your wallpaper to JPEG — keeps it at full quality",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"Control Panel\Desktop", "JPEGImportQuality");
                        return v.HasValue ? v == 100 : false;
                    },
                    Apply = disable =>
                    {
                        if (disable)
                            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "JPEGImportQuality", 100, RegistryValueKind.DWord);
                        else
                            Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true)?.DeleteValue("JPEGImportQuality", throwOnMissingValue: false);
                        Log($"Wallpaper quality reduction {(disable ? "disabled (max quality)" : "restored to default")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-startmenu-bing", Name = "Disable Start Menu Bing Search",
                    Description = "Disables Bing web search results in the Start Menu search box",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled");
                        return v.HasValue ? v == 0 : false;
                    },
                    Apply = disable =>
                    {
                        using var key = TweakHelpers.CreateRealHkcuSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Search");
                        key?.SetValue("BingSearchEnabled",    disable ? 0 : 1, RegistryValueKind.DWord);
                        key?.SetValue("SearchBoxTaskbarMode", disable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Start Menu Bing {(disable ? "disabled" : "enabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-fso-gamebar", Name = "Disable FSO and Gamebar",
                    Description = "Disables Fullscreen Optimizations and Xbox Game Bar — can improve frame pacing in some games",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode");
                        return v.HasValue ? v == 2 : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "ShowStartupPanel",    disable ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode",   disable ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled",         disable ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_FSEBehaviorMode", disable ? 2 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_FSEBehavior",     disable ? 2 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", disable ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\BcastDVRUserService", "Start", disable ? 4 : 3, RegistryValueKind.DWord);
                        Log($"FSO and Gamebar {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
            };
        }

    }
}
