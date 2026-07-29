using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] Accessibility(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "gaming-narrator-hotkey",
                    Name             = "Narrator Win+Ctrl+Enter Hotkey",
                    Description      = "Enable the Win+Ctrl+Enter keyboard shortcut to quickly launch Windows Narrator screen reader",
                    IsPreference     = true,
                    // EnabledValue=[null], DisabledValue=[0], DefaultValue=null → enabled=key absent
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Narrator\NoRoam", "WinEnterLaunchEnabled");
                        return v.HasValue ? v != 0 : true; // absent = enabled (default)
                    },
                    Apply = on =>
                    {
                        if (on)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Narrator\NoRoam", true);
                            k?.DeleteValue("WinEnterLaunchEnabled", false);
                        }
                        else
                            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam",
                                "WinEnterLaunchEnabled", 0, RegistryValueKind.DWord);
                        Log($"Narrator Hotkey {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "accessibility-stickykeys-hotkey",
                    Name             = "StickyKeys Hotkey (Shift×5)",
                    Description      = "Enable the keyboard shortcut to activate StickyKeys by pressing the Shift key five times",
                    IsPreference     = true,
                    // EnabledValue=["510"], DisabledValue=["2"], DefaultValue="510"
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser,
                            @"Control Panel\Accessibility\StickyKeys", "Flags");
                        return v != null ? v == "510" : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys",
                            "Flags", on ? "510" : "2", RegistryValueKind.String);
                        Log($"StickyKeys Hotkey {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "accessibility-filterkeys-hotkey",
                    Name             = "FilterKeys Hotkey (Right Shift 8s)",
                    Description      = "Enable the keyboard shortcut to activate FilterKeys by holding the right Shift key for 8 seconds",
                    IsPreference     = true,
                    // EnabledValue=["126"], DisabledValue=["2"], DefaultValue="126"
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser,
                            @"Control Panel\Accessibility\Keyboard Response", "Flags");
                        return v != null ? v == "126" : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response",
                            "Flags", on ? "126" : "2", RegistryValueKind.String);
                        Log($"FilterKeys Hotkey {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "accessibility-togglekeys-hotkey",
                    Name             = "ToggleKeys Hotkey (Num Lock 5s)",
                    Description      = "Enable the keyboard shortcut to activate ToggleKeys by holding Num Lock for 5 seconds",
                    IsPreference     = true,
                    // EnabledValue=["62"], DisabledValue=["34"], DefaultValue="62"
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser,
                            @"Control Panel\Accessibility\ToggleKeys", "Flags");
                        return v != null ? v == "62" : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys",
                            "Flags", on ? "62" : "34", RegistryValueKind.String);
                        Log($"ToggleKeys Hotkey {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "accessibility-highcontrast-hotkey",
                    Name             = "High Contrast Hotkey (Alt+Shift+PrtScn)",
                    Description      = "Enable the keyboard shortcut to activate High Contrast mode by pressing Left Alt + Left Shift + Print Screen",
                    IsPreference     = true,
                    // Winhance: REG_SZ! EnabledValue=["126"], DisabledValue=["4194"], DefaultValue="126"
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser,
                            @"Control Panel\Accessibility\HighContrast", "Flags");
                        return v != null ? v == "126" : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast",
                            "Flags", on ? "126" : "4194", RegistryValueKind.String);
                        Log($"High Contrast Hotkey {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "accessibility-mousekeys-hotkey",
                    Name             = "MouseKeys Hotkey (Alt+Shift+NumLock)",
                    Description      = "Enable the keyboard shortcut to activate MouseKeys, which lets the numeric keypad control the mouse pointer",
                    IsPreference     = true,
                    // Winhance: REG_SZ! EnabledValue=["126"], DisabledValue=["130"], DefaultValue="126"
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser,
                            @"Control Panel\Accessibility\MouseKeys", "Flags");
                        return v != null ? v == "126" : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\MouseKeys",
                            "Flags", on ? "126" : "130", RegistryValueKind.String);
                        Log($"MouseKeys Hotkey {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
    }
}
