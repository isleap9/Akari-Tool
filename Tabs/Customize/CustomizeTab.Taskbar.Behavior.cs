using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── TASKBAR ▸ BEHAVIOR ──
        private void BuildTaskbarBehavior(StackPanel panel)
        {
            // ── Behavior ──────────────────────────────────────────────────────
            var behaviorSection = TweakHelpers.BuildSection(panel, "Behavior");

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-auto-hide",
                Name        = "Auto-Hide Taskbar",
                Description = "Hides the taskbar until you hover over it",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-auto-hide: Recommended=2 and Default=2, both of which
                // are the DisabledValue → OFF. Same polarity as this row.
                RecommendedState = false,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadTaskbarAutoHide,
                Apply       = enable =>
                {
                    SetTaskbarAutoHide(enable);
                    Service?.Log($"[TASKBAR] Auto-hide {(enable ? "enabled" : "disabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-end-task",
                Name        = "End Task on Taskbar",
                Description = "Adds 'End Task' to the right-click menu for taskbar app buttons",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Restored: row was lost in a pre-pass-A session; reader survived.
                // Windows ships without End Task → OFF is default.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadEndTask,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                        "TaskbarEndTask", enable ? 1 : 0);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] End Task {(enable ? "enabled" : "disabled")}.");
                },
            });;

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-clock-seconds",
                Name        = "Show Seconds in Clock",
                Description = "Displays seconds in the system tray clock",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Not modelled by Winhance. Windows ships with seconds hidden → OFF is default.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadClockSeconds,
                Apply       = enable =>
                {
                    Registry.SetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "ShowSecondsInSystemClock", enable ? 1 : 0, RegistryValueKind.DWord);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Clock seconds {(enable ? "shown" : "hidden")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-badges",
                Name        = "Hide Taskbar Badges",
                Description = "Removes notification badge counters from taskbar app icons",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-badges "Show badges": Recommended=1, Default=1 → both ON
                // (badges shown). Inverted here (Hide), so both become OFF.
                RecommendedState = false,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadTaskbarBadgesHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "TaskbarBadges", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Badges {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-disable-thumbnails",
                Name        = "Disable Taskbar Thumbnails",
                Description = "Stops thumbnail previews appearing when hovering taskbar icons — reduces RAM usage",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Winhance models ExtendedUIHoverTime as a preference dropdown with no
                // recommended value; its 400ms option is labelled "(Default)" → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadTaskbarThumbnailsDisabled,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "ExtendedUIHoverTime", enable ? 30000 : 400);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Thumbnails {(enable ? "disabled" : "enabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-disable-aero-peek",
                Name        = "Disable Aero Peek",
                Description = "Disables the Show Desktop peek when hovering the bottom-right corner",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Not modelled by Winhance. DisablePreviewDesktop is absent/0 out of the box → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadAeroPeekDisabled,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "DisablePreviewDesktop", enable ? 1 : 0);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Aero Peek {(enable ? "disabled" : "enabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-disable-snap-assist",
                Name        = "Disable Snap Assist",
                Description = "Disables the snap layout overlay that appears when dragging windows",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Not modelled by Winhance. SnapAssist ships enabled (1) → OFF is default.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadSnapAssistDisabled,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "SnapAssist", enable ? 0 : 1);
                    Service?.Log($"[TASKBAR] Snap Assist {(enable ? "disabled" : "enabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-verbose-status",
                Name        = "Verbose Boot/Shutdown Messages",
                Description = "Shows detailed status messages during Windows startup and shutdown",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Not modelled by Winhance. verbosestatus is absent out of the box → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadVerboseStatus,
                Apply       = enable =>
                {
                    Registry.SetValue(
                        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                        "verbosestatus", enable ? 1 : 0, RegistryValueKind.DWord);
                    Service?.Log($"[TASKBAR] Verbose status {(enable ? "enabled" : "disabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-show-all-tray-icons",
                Name        = "Always Show All Tray Icons",
                Description = "Shows every system tray icon instead of collapsing them into the overflow flyout",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-system-tray-icons: Recommended=0 → EnabledValue → ON;
                // Default=1 → DisabledValue → OFF. Same polarity as this row.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadAllTrayIconsShown,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                        "EnableAutoTray", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Show all tray icons {(enable ? "enabled" : "disabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-on-other-displays",
                Name        = "Hide Taskbar on Other Displays",
                Description = "Shows the taskbar only on the main monitor in multi-display setups",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-multi-display "Show my taskbar on all displays":
                // Recommended=1, Default=1 → both ON (shown everywhere). Inverted here (Hide).
                RecommendedState = false,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadMultiMonitorTaskbarHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "MMTaskbarEnabled", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Multi-monitor taskbar {(enable ? "hidden" : "shown")}.");
                },
            });

            // Parity vol.2 rows live in their own partial to keep this file
            // within the size conventions.
            BuildTaskbarBehaviorExtras(behaviorSection);

            // ── Winhance-parity Behavior / System Tray rows ──────────────────
            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
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

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
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

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
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

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
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

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
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
        }
    }
}
