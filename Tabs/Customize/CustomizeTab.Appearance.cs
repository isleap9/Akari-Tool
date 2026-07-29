using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ─────────────────────────────────────────────────────────────────────
        // APPEARANCE
        // ─────────────────────────────────────────────────────────────────────

        private void BuildAppearance(StackPanel panel)
        {
            panel.Children.Add(PageHeader("Appearance", "Windows visual theme, color, transparency, and window style tweaks.",
                withActions: true, panel));

            // ── Theme ─────────────────────────────────────────────────────────
            var themeSection = TweakHelpers.BuildSection(panel, "Theme");

            TweakHelpers.AddTweakRow(themeSection, new TweakDefinition
            {
                Id          = "customize-appearance-dark-mode-apps",
                Name        = "Dark Mode (Apps)",
                Description = "Switches Windows apps to dark mode",
                Group       = "Theme",
                // Winhance AppsUseLightTheme carries no Recommended/Default value (it is a
                // Dark/Light preference dropdown). Windows ships Light → OFF is the default.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadDarkModeApps,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "AppsUseLightTheme", enable ? 0 : 1, RegistryValueKind.DWord);
                    Service?.Log($"[APPEARANCE] Apps dark mode {(enable ? "on" : "off")}.");
                },
            });

            TweakHelpers.AddTweakRow(themeSection, new TweakDefinition
            {
                Id          = "customize-appearance-dark-mode-system",
                Name        = "Dark Mode (System)",
                Description = "Switches the Windows shell, taskbar, and Start Menu to dark mode",
                Group       = "Theme",
                // Same preference treatment as Dark Mode (Apps). Windows ships Light → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadDarkModeSystem,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "SystemUsesLightTheme", enable ? 0 : 1, RegistryValueKind.DWord);
                    Service?.Log($"[APPEARANCE] System dark mode {(enable ? "on" : "off")}.");
                },
            });

            // ── Transparency & Effects ────────────────────────────────────────
            var fxSection = TweakHelpers.BuildSection(panel, "Transparency & Effects");

            TweakHelpers.AddTweakRow(fxSection, new TweakDefinition
            {
                Id          = "customize-appearance-transparency",
                Name        = "Transparency Effects",
                Description = "Enables transparency and blur effects on the taskbar, Start Menu, and Action Center",
                Group       = "Transparency & Effects",
                // Winhance EnableTransparency: Recommended=0 → OFF, Default=1 → ON. Same polarity.
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = SystemStateReader.ReadTransparency,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "EnableTransparency", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[APPEARANCE] Transparency {(enable ? "on" : "off")}.");
                },
            });

            TweakHelpers.AddTweakRow(fxSection, new TweakDefinition
            {
                Id          = "customize-appearance-oled-taskbar-transparency",
                Name        = "OLED Taskbar Transparency",
                Description = "Enables the deeper OLED-style taskbar transparency (requires Transparency Effects on)",
                Group       = "Transparency & Effects",
                // Not modelled by Winhance. Value absent out of the box → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadOledTaskbarTransparency,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "UseOLEDTaskbarTransparency", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[APPEARANCE] OLED taskbar transparency {(enable ? "on" : "off")}.");
                },
            });

            TweakHelpers.AddTweakRow(fxSection, new TweakDefinition
            {
                Id          = "customize-appearance-dwm-effect-mode",
                Name        = "Mica/Acrylic Effects (DWM)",
                Description = "Forces DWM effect mode — enables Mica and Acrylic material effects on supported apps",
                Group       = "Transparency & Effects",
                // Not modelled by Winhance. ForceEffectMode is absent/0 out of the box → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadDwmForceEffectMode,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm",
                        "ForceEffectMode", enable ? 2 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[APPEARANCE] DWM effect mode {(enable ? "forced (2)" : "default (0)")}.");
                },
            });

            // ── Color ─────────────────────────────────────────────────────────
            var colorSection = TweakHelpers.BuildSection(panel, "Color");

            TweakHelpers.AddTweakRow(colorSection, new TweakDefinition
            {
                Id          = "customize-appearance-accent-title-bars",
                Name        = "Accent Color on Title Bars & Borders",
                Description = "Shows your accent color on window title bars and borders instead of white/black",
                Group       = "Color",
                // Not modelled by Winhance. Accent on title bars is off out of the box → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadTitleBarAccentColor,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                        "ColorPrevalence", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[APPEARANCE] Title bar accent color {(enable ? "on" : "off")}.");
                },
            });

            TweakHelpers.AddTweakRow(colorSection, new TweakDefinition
            {
                Id          = "customize-appearance-accent-taskbar-start",
                Name        = "Accent Color on Taskbar & Start",
                Description = "Shows your accent color on the taskbar and Start Menu",
                Group       = "Color",
                // Not modelled by Winhance. Accent on taskbar is off out of the box → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadTaskbarAccentColor,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "ColorPrevalence", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[APPEARANCE] Taskbar accent color {(enable ? "on" : "off")}.");
                },
            });

            // ── Window Style ──────────────────────────────────────────────────
            var windowSection = TweakHelpers.BuildSection(panel, "Window Style");

            TweakHelpers.AddTweakRow(windowSection, new TweakDefinition
            {
                Id          = "customize-appearance-square-window-corners",
                Name        = "Square Window Corners",
                Description = "Disables rounded window corners — restores the sharp square corners from Windows 10",
                Group       = "Window Style",
                // Not modelled by Winhance. Windows 11 ships rounded → OFF.
                DefaultState = false,
                RequiresRestart = true,
                ReadState   = SystemStateReader.ReadSquareWindowCorners,
                Apply       = enable =>
                {
                    // Win11 uses DWM attribute; the registry path below is the known hack
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                        "UseWindowDarkMode", enable ? 0 : 0, RegistryValueKind.DWord); // placeholder — actual via dwmapi
                    // Real implementation: set WindowCornerPreference via registry policy
                    var path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DWM";
                    Registry.SetValue(path, "DisableRoundedCorners", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[APPEARANCE] Window corners set to {(enable ? "square" : "rounded (default)")}. Restart to apply.");
                },
            });

            TweakHelpers.AddTweakRow(windowSection, new TweakDefinition
            {
                Id          = "customize-appearance-drag-full-windows",
                Name        = "Show Window Contents While Dragging",
                Description = "Displays live window contents when dragging — disabling shows only an outline for lower CPU use",
                Group       = "Window Style",
                // Winhance DragFullWindows: Recommended="1" → ON, Default="1" → ON. Same polarity.
                RecommendedState = true,
                DefaultState     = true,
                ReadState   = SystemStateReader.ReadDragFullWindows,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop",
                        "DragFullWindows", enable ? "1" : "0", RegistryValueKind.String);
                    Service?.Log($"[APPEARANCE] Drag full windows {(enable ? "on" : "off")}.");
                },
            });

            TweakHelpers.AddTweakRow(windowSection, new TweakDefinition
            {
                Id          = "customize-appearance-animate-windows",
                Name        = "Animate Windows on Open/Close",
                Description = "Enables the open/minimize/maximize animations for windows",
                Group       = "Window Style",
                // Winhance MinAnimate: Recommended="0" → OFF, Default="1" → ON. Same polarity.
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = SystemStateReader.ReadMinAnimate,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics",
                        "MinAnimate", enable ? "1" : "0", RegistryValueKind.String);
                    Service?.Log($"[APPEARANCE] Window animations {(enable ? "on" : "off")}.");
                },
            });
        }
    }
}
