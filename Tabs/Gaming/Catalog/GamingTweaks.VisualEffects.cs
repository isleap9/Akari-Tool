using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] VisualEffects(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "visual-effects-mode",
                    Name             = "Visual Effects Mode",
                    Description      = "Control the overall level of Windows visual effects — trading appearance for performance",
                    IsPreference     = true,
                    InputKind        = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Let Windows choose (Default)", 0, IsDefault: true),
                        new TweakDropdownOption("Best appearance",              1),
                        new TweakDropdownOption("Best performance (Recommended)", 2, IsRecommended: true),
                        new TweakDropdownOption("Custom",                       3),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting");
                        return v switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, _ => 0 };
                    },
                    ApplyIndex = idx =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                            "VisualFXSetting", idx, RegistryValueKind.DWord);
                        const string ADV = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
                        const string CPD = @"HKEY_CURRENT_USER\Control Panel\Desktop";
                        if (idx == 2)
                        {
                            Registry.SetValue(ADV, "ListviewAlphaSelect", 0, RegistryValueKind.DWord);
                            Registry.SetValue(ADV, "ListviewShadow",      0, RegistryValueKind.DWord);
                            Registry.SetValue(ADV, "TaskbarAnimations",   0, RegistryValueKind.DWord);
                            Registry.SetValue(CPD, "DragFullWindows",     "0", RegistryValueKind.String);
                            Registry.SetValue(CPD + @"\WindowMetrics", "MinAnimate", "0", RegistryValueKind.String);
                        }
                        else if (idx == 1)
                        {
                            Registry.SetValue(ADV, "ListviewAlphaSelect", 1, RegistryValueKind.DWord);
                            Registry.SetValue(ADV, "ListviewShadow",      1, RegistryValueKind.DWord);
                            Registry.SetValue(ADV, "TaskbarAnimations",   1, RegistryValueKind.DWord);
                            Registry.SetValue(CPD, "DragFullWindows",     "1", RegistryValueKind.String);
                            Registry.SetValue(CPD + @"\WindowMetrics", "MinAnimate", "1", RegistryValueKind.String);
                        }
                        Log($"Visual Effects set to option {idx}. Sign out or restart to fully apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "drag-full-windows",
                    Name             = "Show window contents while dragging",
                    Description      = "Show the full window content while dragging instead of just an outline",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () => ReadString(RegistryHive.CurrentUser, @"Control Panel\Desktop", "DragFullWindows") != "0",
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "DragFullWindows", on ? "1" : "0", RegistryValueKind.String);
                        Log($"Show window contents while dragging {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "window-animation",
                    Name             = "Animate windows when minimizing and maximizing",
                    Description      = "Show smooth animation when windows are minimized or maximized",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () => ReadString(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate") != "0",
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", on ? "1" : "0", RegistryValueKind.String);
                        Log($"Window minimize/maximize animation {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "taskbar-animations",
                    Name             = "Taskbar animations",
                    Description      = "Show animations in the taskbar when apps open, close, or flash for attention",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "TaskbarAnimations", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Taskbar animations {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "font-smoothing",
                    Name             = "Smooth edges of screen fonts (ClearType)",
                    Description      = "Apply ClearType anti-aliasing to make text appear smoother on screen",
                    IsPreference     = true, RecommendedState = true, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"Control Panel\Desktop", "FontSmoothing");
                        return v.HasValue ? v == 2 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "FontSmoothing", on ? 2 : 0, RegistryValueKind.DWord);
                        Log($"Font smoothing {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "drop-shadows",
                    Name             = "Drop shadows under mouse pointer",
                    Description      = "Show a drop shadow beneath the mouse cursor for better visibility",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"Control Panel\Desktop", "CursorShadow");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "CursorShadow", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Cursor drop shadow {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "show-thumbnails",
                    Name             = "Show thumbnails instead of icons",
                    Description      = "Display thumbnail previews for image, video, and document files in File Explorer",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "IconsOnly");
                        return v.HasValue ? v == 0 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "IconsOnly", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Thumbnails {(on ? "enabled" : "disabled (icons only)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "enable-peek",
                    Name             = "Peek at the desktop",
                    Description      = "Temporarily view the desktop when hovering over the Show Desktop button at the taskbar corner",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisablePreviewDesktop");
                        return v.HasValue ? v != 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "DisablePreviewDesktop", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Peek at desktop {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "window-shadows",
                    Name             = "Show shadows under windows",
                    Description      = "Display drop shadows under windows to create a layered depth effect on the desktop",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "ListviewShadow", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Window shadows {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "smooth-scroll-listboxes",
                    Name             = "Smooth-scroll list boxes",
                    Description      = "Scroll list boxes smoothly instead of jumping one item at a time",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Control Panel\Desktop", "SmoothScroll");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "SmoothScroll", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Smooth scroll list boxes {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "listview-alpha-select",
                    Name             = "Translucent selection rectangle",
                    Description      = "Show a semi-transparent rectangle when selecting multiple files in Explorer instead of a solid box",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "ListviewAlphaSelect", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Translucent selection rectangle {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "menu-show-delay",
                    Name             = "Reduce menu animation delay",
                    Description      = "Reduces the delay before menus open from 400 ms to 200 ms for a snappier feel",
                    IsPreference     = true, RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadString(RegistryHive.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay");
                        return v == "200";
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "MenuShowDelay",
                            on ? "200" : "400", RegistryValueKind.String);
                        Log($"Menu animation delay set to {(on ? "200 ms (reduced)" : "400 ms (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "keyboard-delay",
                    Name             = "Reduce keyboard repeat delay",
                    Description      = "Sets keyboard initial repeat delay to its shortest value (0) for faster key response",
                    IsPreference     = true, RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, @"Control Panel\Keyboard", "KeyboardDelay");
                        return v.HasValue ? v == 0 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardDelay",
                            on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Keyboard repeat delay set to {(on ? "0 (shortest)" : "1 (default)")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "aero-peek-dwm",
                    Name             = "Aero Peek (DWM)",
                    Description      = "Enables the DWM-level Aero Peek glass preview effect — disable for a minor compositing performance gain",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\DWM", "EnableAeroPeek");
                        return v.HasValue ? v == 1 : true;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(
                            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                            "EnableAeroPeek", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Aero Peek (DWM) {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "user-preferences-mask",
                    Name             = "Windows animation effects (UserPreferencesMask)",
                    Description      = "Master binary flag controlling fade/slide menus, animate controls, smooth-scroll, tooltip fade, and cursor shadow — disabling matches Best Performance mode",
                    IsPreference     = true, RecommendedState = false, DefaultState = true,
                    ReadState = () =>
                    {
                        try
                        {
                            using var key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)
                                .OpenSubKey(@"Control Panel\Desktop");
                            if (key?.GetValue("UserPreferencesMask") is byte[] data && data.Length >= 1)
                                // Default mask has bit 0x02 set in byte[0] (animate controls).
                                // "Best performance" clears it. We report ON if default animations are active.
                                return (data[0] & 0x02) != 0;
                            return true; // key missing → assume default (on)
                        }
                        catch { return null; }
                    },
                    Apply = on =>
                    {
                        // Best-performance mask:  90 12 03 80 10 00 00 00
                        // Default (appearance):   9E 3E 07 80 12 00 00 00
                        var mask = on
                            ? new byte[] { 0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 }
                            : new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 };
                        using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
                        key?.SetValue("UserPreferencesMask", mask, RegistryValueKind.Binary);
                        Log($"Windows animation effects (UserPreferencesMask) {(on ? "restored to default" : "set to best performance")}.");
                    }
                },
            };
    }
}
