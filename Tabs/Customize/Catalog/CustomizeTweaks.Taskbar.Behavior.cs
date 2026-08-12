using Microsoft.Win32;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Taskbar.Behavior.cs.
    // Section "Behavior" (+ some "System Tray") — 15 rows. The Winhance-parity vol.2 rows
    // live in TaskbarBehaviorExtras (separate partial). The SetTaskbarAutoHide StuckRects3
    // blob helper (net8 lived in Taskbar.cs) moved here as a static helper.
    public static partial class CustomizeTweaks
    {
        /// <summary>
        /// Auto-hide lives in the StuckRects3 settings blob rather than a plain
        /// DWORD, so it needs a read-modify-write of bit 0 in byte 8.
        /// </summary>
        private static void SetTaskbarAutoHide(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3", writable: true);
            if (key?.GetValue("Settings") is not byte[] data) return;
            if (enable)
                data[8] |= 0x01;   // set auto-hide bit
            else
                data[8] &= 0xFE;   // clear auto-hide bit
            key.SetValue("Settings", data, RegistryValueKind.Binary);
            if (!_suppressRestart) ExplorerRestart.Request();
        }

        public static TweakDefinition[] TaskbarBehavior(Action<string> Log)
        {
            var result = new List<TweakDefinition>
            {
                new()
                {
                    Id          = "customize-taskbar-auto-hide",
                    Name        = "Auto-Hide Taskbar",
                    Description = "Hides the taskbar until you hover over it",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadTaskbarAutoHide,
                    Apply       = enable =>
                    {
                        SetTaskbarAutoHide(enable);
                        Log($"[TASKBAR] Auto-hide {(enable ? "enabled" : "disabled")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-end-task",
                    Name        = "End Task on Taskbar",
                    Description = "Adds 'End Task' to the right-click menu for taskbar app buttons",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadEndTask,
                    Apply       = enable =>
                    {
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                            "TaskbarEndTask", enable ? 1 : 0);
                        if (!_suppressRestart) ExplorerRestart.Request();
                        Log($"[TASKBAR] End Task {(enable ? "enabled" : "disabled")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-clock-seconds",
                    Name        = "Show Seconds in Clock",
                    Description = "Displays seconds in the system tray clock",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadClockSeconds,
                    Apply       = enable =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "ShowSecondsInSystemClock", enable ? 1 : 0, RegistryValueKind.DWord);
                        if (!_suppressRestart) ExplorerRestart.Request();
                        Log($"[TASKBAR] Clock seconds {(enable ? "shown" : "hidden")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-hide-badges",
                    Name        = "Hide Taskbar Badges",
                    Description = "Removes notification badge counters from taskbar app icons",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadTaskbarBadgesHidden,
                    Apply       = enable =>
                    {
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "TaskbarBadges", enable ? 0 : 1);
                        if (!_suppressRestart) ExplorerRestart.Request();
                        Log($"[TASKBAR] Badges {(enable ? "hidden" : "shown")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-disable-thumbnails",
                    Name        = "Disable Taskbar Thumbnails",
                    Description = "Stops thumbnail previews appearing when hovering taskbar icons — reduces RAM usage",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadTaskbarThumbnailsDisabled,
                    Apply       = enable =>
                    {
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "ExtendedUIHoverTime", enable ? 30000 : 400);
                        if (!_suppressRestart) ExplorerRestart.Request();
                        Log($"[TASKBAR] Thumbnails {(enable ? "disabled" : "enabled")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-disable-aero-peek",
                    Name        = "Disable Aero Peek",
                    Description = "Disables the Show Desktop peek when hovering the bottom-right corner",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadAeroPeekDisabled,
                    Apply       = enable =>
                    {
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "DisablePreviewDesktop", enable ? 1 : 0);
                        if (!_suppressRestart) ExplorerRestart.Request();
                        Log($"[TASKBAR] Aero Peek {(enable ? "disabled" : "enabled")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-disable-snap-assist",
                    Name        = "Disable Snap Assist",
                    Description = "Disables the snap layout overlay that appears when dragging windows",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadSnapAssistDisabled,
                    Apply       = enable =>
                    {
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "SnapAssist", enable ? 0 : 1);
                        Log($"[TASKBAR] Snap Assist {(enable ? "disabled" : "enabled")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-verbose-status",
                    Name        = "Verbose Boot/Shutdown Messages",
                    Description = "Shows detailed status messages during Windows startup and shutdown",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadVerboseStatus,
                    Apply       = enable =>
                    {
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                            "verbosestatus", enable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"[TASKBAR] Verbose status {(enable ? "enabled" : "disabled")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-show-all-tray-icons",
                    Name        = "Always Show All Tray Icons",
                    Description = "Shows every system tray icon instead of collapsing them into the overflow flyout",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadAllTrayIconsShown,
                    Apply       = enable =>
                    {
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                            "EnableAutoTray", enable ? 0 : 1);
                        if (!_suppressRestart) ExplorerRestart.Request();
                        Log($"[TASKBAR] Show all tray icons {(enable ? "enabled" : "disabled")}.");
                    },
                },
                new()
                {
                    Id          = "customize-taskbar-hide-on-other-displays",
                    Name        = "Hide Taskbar on Other Displays",
                    Description = "Shows the taskbar only on the main monitor in multi-display setups",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadMultiMonitorTaskbarHidden,
                    Apply       = enable =>
                    {
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "MMTaskbarEnabled", enable ? 0 : 1);
                        if (!_suppressRestart) ExplorerRestart.Request();
                        Log($"[TASKBAR] Multi-monitor taskbar {(enable ? "hidden" : "shown")}.");
                    },
                },
            };

            // Parity vol.2 rows (net8 rendered these into the same behaviorSection).
            result.AddRange(TaskbarBehaviorExtras(Log));

            // ── Winhance-parity Behavior / System Tray rows ──────────────────
            result.Add(new TweakDefinition
            {
                Id          = "customize-taskbar-flashing",
                Name        = "Show Flashing on Taskbar Apps",
                Group       = "Behavior",
                Description = "Flash taskbar buttons when an app needs attention",
                RecommendedState = true,
                DefaultState     = true,
                ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarFlashing") is int v ? v != 0 : true,
                Apply       = on =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "TaskbarFlashing", on ? 1 : 0, RegistryValueKind.DWord);
                    Log($"Taskbar flashing {(on ? "on" : "off")}.");
                },
            });

            result.Add(new TweakDefinition
            {
                Id          = "customize-taskbar-small-icons",
                Name        = "Use Small Taskbar Buttons",
                Group       = "Behavior",
                Description = "Use smaller taskbar buttons (Windows 10 only; ignored on Windows 11)",
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarSmallIcons") is int v ? v != 0 : false,
                Apply       = on =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "TaskbarSmallIcons", on ? 1 : 0, RegistryValueKind.DWord);
                    Log($"Small taskbar buttons {(on ? "on" : "off")}.");
                },
            });

            result.Add(new TweakDefinition
            {
                Id          = "customize-taskbar-meet-now",
                Name        = "Remove Meet Now Button",
                Group       = "System Tray",
                Description = "Hide the Meet Now button from the system tray",
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = () => ReadDwordCu(@"Software\Policies\Microsoft\Windows\Explorer", "HideSCAMeetNow") is int v ? v == 1 : false,
                Apply       = on =>
                {
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer",
                        "HideSCAMeetNow", on ? 1 : 0, RegistryValueKind.DWord);
                    Log($"Meet Now {(on ? "removed" : "shown")}.");
                },
            });

            result.Add(new TweakDefinition
            {
                Id          = "customize-taskbar-button-size",
                Name        = "Combine Taskbar Buttons / Small Buttons",
                Group       = "Behavior",
                Description = "When to use smaller taskbar buttons",
                IsPreference = true,
                InputKind   = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Always",               0),
                    new TweakDropdownOption("When taskbar is full", 2, IsDefault: true),
                    new TweakDropdownOption("Never",                1),
                },
                ReadCurrentIndex = () =>
                {
                    var v = ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "IconSizePreference");
                    return v switch { 0 => 0, 2 => 1, 1 => 2, _ => 1 };
                },
                ApplyIndex = idx =>
                {
                    if (idx < 0 || idx > 2) return;
                    int[] vals = { 0, 2, 1 };
                    Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "IconSizePreference", vals[idx], RegistryValueKind.DWord);
                    Log($"Taskbar button size set to option {idx}.");
                },
            });

            result.Add(new TweakDefinition
            {
                Id          = "customize-taskbar-transparency",
                Name        = "Taskbar Transparency",
                Group       = "Behavior",
                Description = "Force the taskbar fully transparent or fully opaque",
                IsPreference = true,
                InputKind   = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Windows default", -1, IsDefault: true),
                    new TweakDropdownOption("Transparent",      0),
                    new TweakDropdownOption("Opaque",           255),
                },
                ReadCurrentIndex = () =>
                {
                    var v = ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAcrylicOpacity");
                    return v switch { 0 => 1, 255 => 2, _ => 0 };
                },
                ApplyIndex = idx =>
                {
                    const string sub = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
                    if (idx == 0) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("TaskbarAcrylicOpacity", false); }
                    else Registry.SetValue(@"HKEY_CURRENT_USER\" + sub, "TaskbarAcrylicOpacity", idx == 1 ? 0 : 255, RegistryValueKind.DWord);
                    Log($"Taskbar transparency set to option {idx}.");
                },
            });

            return result.ToArray();
        }
    }
}
