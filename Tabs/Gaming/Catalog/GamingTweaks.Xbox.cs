using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] Xbox(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "gaming-xbox-game-dvr",
                    Name             = "Xbox Game DVR",
                    Description      = "Record gameplay clips and take screenshots using the Xbox Game Bar overlay. Disabling reduces CPU/GPU usage and can improve frame rates",
                    // EnabledValue=[1], DisabledValue=[0], DefaultValue=1
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"System\GameConfigStore", "GameDVR_Enabled");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore",
                            "GameDVR_Enabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                            "AppCaptureEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR",
                            "AllowGameDVR", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Xbox Game DVR {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-game-bar-controller",
                    Name             = "Game Bar Controller Access",
                    Description      = "Allow your Xbox/compatible controller to open Game Bar by pressing the Xbox button. Disable to prevent accidental Game Bar activation during gaming",
                    // EnabledValue=[null], DisabledValue=[0], DefaultValue=null → enabled=key absent
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled");
                        return v.HasValue ? v != 0 : true; // absent = enabled
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar", true);
                            k?.DeleteValue("UseNexusForGameBarEnabled", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                                "UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord);
                        Log($"Game Bar Controller Access {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-game-bar-tips",
                    Name             = "Game Bar Tips and Hints",
                    Description      = "Show tips and hints about Game Bar features when opening the overlay. Disabling reduces distractions during gameplay",
                    // EnabledValue=[null], DisabledValue=[0], DefaultValue=null → enabled=key absent
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\GameBar", "ShowStartupPanel");
                        return v.HasValue ? v != 0 : true; // absent = enabled
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar", true);
                            k?.DeleteValue("ShowStartupPanel", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                                "ShowStartupPanel", 0, RegistryValueKind.DWord);
                        Log($"Game Bar Tips {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
    }
}
