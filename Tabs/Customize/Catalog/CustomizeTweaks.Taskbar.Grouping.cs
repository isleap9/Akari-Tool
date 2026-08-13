using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    // MVVM PORT (Phase 17): net8's Taskbar ▸ Button Grouping was three HAND-MADE
    // ComboBoxes (build #2 CustomizeTab.Taskbar.Grouping.cs) that were never
    // TweakDefinitions and never registered. Per the Phase-16 recon, they convert
    // cleanly to three standard dropdown TweakDefinitions (the System Services /
    // Scheduled Tasks catalog pattern — NOT bespoke ViewModel/DataTemplate work).
    //
    // Read/write logic is REUSED, not reimplemented: SystemStateReader.
    // ReadCombineTaskbarButtons() (dropdown 1), the already-ported ReadDwordCu /
    // SetHkcu statics, and ExplorerRestart. Only the TweakDefinition dropdown wrapper
    // is new. Apply still calls ExplorerRestart.Request() after writing (gated by
    // _suppressRestart during bulk), exactly as net8 did.
    //
    // NEW Ids minted (net8 assigned none — these settings were never in any
    // TweakRegistry/Backup export, so there is nothing to preserve byte-for-byte;
    // adding them is first-time Backup/search coverage). Names follow the existing
    // customize-taskbar-* convention; collision-checked against the full Customize
    // Id set (none).
    //
    // DEFAULT / PREFERENCE (isleap-resolved after Phase 17): all three are pure
    // cosmetic preferences with no objectively-correct answer. The Windows
    // clean-install default is index 0 for each (value absent → 0, matching net8's own
    // `HasValue ? Min(v,2) : 0` fallback: TaskbarGlomLevel 0 = Always combine,
    // MMTaskbarMode 0 = All taskbars, MMTaskbarGlomLevel 0 = Always combine).
    //
    // Each is marked `IsPreference = true` with the Windows-default option (index 0)
    // carrying `IsDefault: true` but NOT `IsRecommended` — matching the existing
    // Customize cosmetic-dropdown convention (Taskbar Transparency / Button Size).
    // Effect: bulk "Apply recommended" SKIPS these rows (no IsRecommended option →
    // TweakTargets.TryGetRecommendedTarget returns false), so a user's deliberate
    // taste choice is never silently overwritten by the Recommended bulk; bulk "Reset
    // to Windows defaults" still resets them to index 0.
    // NOTE: `IsPreference` itself is badge-only (it does NOT gate bulk); the actual
    // bulk-exemption comes from omitting `IsRecommended`.
    public static partial class CustomizeTweaks
    {
        private const string TaskbarAdvancedKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        // value written == SelectedIndex (0/1/2); absent reads back as index 0 (the
        // Windows default), clamped defensively — mirrors net8's read.
        private static int GroupingIndex(int? v) => v is null ? 0 : System.Math.Min(System.Math.Max(v.Value, 0), 2);

        public static TweakDefinition[] TaskbarButtonGrouping(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id          = "customize-taskbar-button-grouping",
                Name        = "Combine Taskbar Buttons",
                Description = "Controls whether taskbar buttons for the same app are grouped together",
                Group       = "Button Grouping",
                InputKind   = TweakInputKind.Dropdown,
                IsPreference = true,
                Options = new[]
                {
                    new TweakDropdownOption("Always combine",                0, IsDefault: true),
                    new TweakDropdownOption("Combine when taskbar is full",  1),
                    new TweakDropdownOption("Never combine",                 2),
                },
                // Reuse the already-ported reader (reads TaskbarGlomLevel).
                ReadCurrentIndex = () => GroupingIndex(SystemStateReader.ReadCombineTaskbarButtons()),
                ApplyIndex = idx =>
                {
                    if (idx < 0 || idx > 2) return;
                    SetHkcu(TaskbarAdvancedKey, "TaskbarGlomLevel", idx);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Log($"[TASKBAR] Combine taskbar buttons set to option {idx}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-taskbar-grouping-multimonitor",
                Name        = "Show Taskbar Apps On",
                Description = "Which monitors show open-window buttons when the taskbar spans multiple displays",
                Group       = "Button Grouping",
                InputKind   = TweakInputKind.Dropdown,
                IsPreference = true,
                Options = new[]
                {
                    new TweakDropdownOption("All taskbars",                                     0, IsDefault: true),
                    new TweakDropdownOption("Main taskbar and taskbar where window is open",    1),
                    new TweakDropdownOption("Taskbar where window is open",                     2),
                },
                ReadCurrentIndex = () => GroupingIndex(ReadDwordCu(TaskbarAdvancedKey, "MMTaskbarMode")),
                ApplyIndex = idx =>
                {
                    if (idx < 0 || idx > 2) return;
                    SetHkcu(TaskbarAdvancedKey, "MMTaskbarMode", idx);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Log($"[TASKBAR] Multi-monitor taskbar mode set to option {idx}.");
                },
            },
            new TweakDefinition
            {
                Id          = "customize-taskbar-grouping-other-taskbars",
                Name        = "Combine Buttons on Other Taskbars",
                Description = "Button grouping behaviour on secondary-monitor taskbars",
                Group       = "Button Grouping",
                InputKind   = TweakInputKind.Dropdown,
                IsPreference = true,
                Options = new[]
                {
                    new TweakDropdownOption("Always combine",                0, IsDefault: true),
                    new TweakDropdownOption("Combine when taskbar is full",  1),
                    new TweakDropdownOption("Never combine",                 2),
                },
                ReadCurrentIndex = () => GroupingIndex(ReadDwordCu(TaskbarAdvancedKey, "MMTaskbarGlomLevel")),
                ApplyIndex = idx =>
                {
                    if (idx < 0 || idx > 2) return;
                    SetHkcu(TaskbarAdvancedKey, "MMTaskbarGlomLevel", idx);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Log($"[TASKBAR] Secondary-taskbar grouping set to option {idx}.");
                },
            },
        };
    }
}
