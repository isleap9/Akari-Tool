using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Core.Tweaks;

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
                    IsPreference     = true, RecommendedState = true, DefaultState = true,
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
                    Id               = "taskbar-thumbnails",
                    Name             = "Save taskbar thumbnail previews",
                    Description      = "Cache taskbar thumbnail previews. Disabling saves a small amount of memory",
                    IsPreference     = true,
                    RecommendedState = false, // Winhance recommends off
                    DefaultState     = false, // Windows default = off
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser,
                            @"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                            "AlwaysHibernateThumbnails", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Taskbar thumbnail previews {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "enable-peek",
                    Name             = "Enable Peek",
                    Description      = "Temporarily preview the desktop or a window when hovering over the Show Desktop button or a taskbar thumbnail",
                    IsPreference     = true, RecommendedState = true, DefaultState = true,
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
                        Log($"Peek {(on ? "enabled" : "disabled")}.");
                    }
                },
                UiPrefBitTweak(Log, "ui-effects", "Animate controls and elements inside windows",
                    "Fade/animate controls and elements inside windows", 4, 0x02),
                UiPrefBitTweak(Log, "menu-animation", "Fade or slide menus into view",
                    "Animate menus with a fade or slide when they open", 0, 0x02),
                UiPrefBitTweak(Log, "combo-box-animation", "Slide open combo boxes",
                    "Animate combo boxes with a sliding effect when opened", 0, 0x04),
                UiPrefBitTweak(Log, "smooth-scroll-listboxes", "Smooth-scroll list boxes",
                    "Smooth scrolling in list boxes instead of jumping", 0, 0x08),
                UiPrefBitTweak(Log, "fade-menu-items", "Fade out menu items after clicking",
                    "Fade menu items after selection before the menu closes", 1, 0x04),
                UiPrefBitTweak(Log, "fade-tooltip", "Fade or slide ToolTips into view",
                    "Animate tooltips with a fade or slide when they appear", 1, 0x08),
                UiPrefBitTweak(Log, "mouse-shadow", "Show shadows under mouse pointer",
                    "Display a shadow effect underneath the mouse cursor", 1, 0x20),
                UiPrefBitTweak(Log, "window-shadows", "Show shadows under windows",
                    "Display shadow effects underneath windows", 2, 0x04),
                new TweakDefinition
                {
                    Id               = "listview-alpha-select",
                    Name             = "Translucent selection rectangle",
                    Description      = "Show a semi-transparent rectangle when selecting multiple files in Explorer instead of a solid box",
                    IsPreference     = true, RecommendedState = true, DefaultState = true,
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
            };
    }
}
