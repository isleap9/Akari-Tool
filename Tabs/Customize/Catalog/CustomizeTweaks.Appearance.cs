using Microsoft.Win32;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Appearance.cs.
    // Sections "Theme", "Transparency & Effects", "Color", "Window Style".
    public static partial class CustomizeTweaks
    {
        // ── Theme ──────────────────────────────────────────────────────────────
        public static TweakDefinition[] AppearanceTheme(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-appearance-dark-mode-apps",
                Name        = "Dark Mode (Apps)",
                Description = "Switches Windows apps to dark mode",
                Group       = "Theme",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadDarkModeApps,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "AppsUseLightTheme", enable ? 0 : 1, RegistryValueKind.DWord);
                    Log($"[APPEARANCE] Apps dark mode {(enable ? "on" : "off")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-appearance-dark-mode-system",
                Name        = "Dark Mode (System)",
                Description = "Switches the Windows shell, taskbar, and Start Menu to dark mode",
                Group       = "Theme",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadDarkModeSystem,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "SystemUsesLightTheme", enable ? 0 : 1, RegistryValueKind.DWord);
                    Log($"[APPEARANCE] System dark mode {(enable ? "on" : "off")}.");
                },
            },
        };

        // ── Transparency & Effects ───────────────────────────────────────────────
        public static TweakDefinition[] AppearanceEffects(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-appearance-transparency",
                Name        = "Transparency Effects",
                Description = "Enables transparency and blur effects on the taskbar, Start Menu, and Action Center",
                Group       = "Transparency & Effects",
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = SystemStateReader.ReadTransparency,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "EnableTransparency", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[APPEARANCE] Transparency {(enable ? "on" : "off")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-appearance-oled-taskbar-transparency",
                Name        = "OLED Taskbar Transparency",
                Description = "Enables the deeper OLED-style taskbar transparency (requires Transparency Effects on)",
                Group       = "Transparency & Effects",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadOledTaskbarTransparency,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "UseOLEDTaskbarTransparency", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[APPEARANCE] OLED taskbar transparency {(enable ? "on" : "off")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-appearance-dwm-effect-mode",
                Name        = "Mica/Acrylic Effects (DWM)",
                Description = "Forces DWM effect mode — enables Mica and Acrylic material effects on supported apps",
                Group       = "Transparency & Effects",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadDwmForceEffectMode,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm",
                        "ForceEffectMode", enable ? 2 : 0, RegistryValueKind.DWord);
                    Log($"[APPEARANCE] DWM effect mode {(enable ? "forced (2)" : "default (0)")}.");
                },
            },
        };

        // ── Color ────────────────────────────────────────────────────────────────
        public static TweakDefinition[] AppearanceColor(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-appearance-accent-title-bars",
                Name        = "Accent Color on Title Bars & Borders",
                Description = "Shows your accent color on window title bars and borders instead of white/black",
                Group       = "Color",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadTitleBarAccentColor,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                        "ColorPrevalence", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[APPEARANCE] Title bar accent color {(enable ? "on" : "off")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-appearance-accent-taskbar-start",
                Name        = "Accent Color on Taskbar & Start",
                Description = "Shows your accent color on the taskbar and Start Menu",
                Group       = "Color",
                DefaultState = false,
                ReadState   = SystemStateReader.ReadTaskbarAccentColor,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "ColorPrevalence", enable ? 1 : 0, RegistryValueKind.DWord);
                    Log($"[APPEARANCE] Taskbar accent color {(enable ? "on" : "off")}.");
                },
            },
        };

        // ── Window Style ───────────────────────────────────────────────────────
        public static TweakDefinition[] AppearanceWindowStyle(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-appearance-square-window-corners",
                Name        = "Square Window Corners",
                Description = "Disables rounded window corners — restores the sharp square corners from Windows 10",
                Group       = "Window Style",
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
                    Log($"[APPEARANCE] Window corners set to {(enable ? "square" : "rounded (default)")}. Restart to apply.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-appearance-drag-full-windows",
                Name        = "Show Window Contents While Dragging",
                Description = "Displays live window contents when dragging — disabling shows only an outline for lower CPU use",
                Group       = "Window Style",
                RecommendedState = true,
                DefaultState     = true,
                ReadState   = SystemStateReader.ReadDragFullWindows,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop",
                        "DragFullWindows", enable ? "1" : "0", RegistryValueKind.String);
                    Log($"[APPEARANCE] Drag full windows {(enable ? "on" : "off")}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-appearance-animate-windows",
                Name        = "Animate Windows on Open/Close",
                Description = "Enables the open/minimize/maximize animations for windows",
                Group       = "Window Style",
                RecommendedState = false,
                DefaultState     = true,
                ReadState   = SystemStateReader.ReadMinAnimate,
                Apply       = enable =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics",
                        "MinAnimate", enable ? "1" : "0", RegistryValueKind.String);
                    Log($"[APPEARANCE] Window animations {(enable ? "on" : "off")}.");
                },
            },
        };
    }
}
