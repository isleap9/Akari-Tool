using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Taskbar.Behavior.Extras.cs.
    // Winhance-parity vol.2 additions to the "Behavior" section — 3 rows. net8 added these
    // into the same behaviorSection; here TaskbarBehavior() AddRanges them.
    public static partial class CustomizeTweaks
    {
        public static TweakDefinition[] TaskbarBehaviorExtras(Action<string> Log)
        {
            const string advanced = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

            // Winhance taskbar-extended-hover-time (ExtendedUIHoverTime): preference
            // dropdown, no recommended registry value; option "1ms (Instant)" is
            // IsRecommended and "400ms (Default)" is IsDefault. RestartProcess=Explorer.
            // NOTE: customize-taskbar-disable-thumbnails writes the same value
            // (30000/400) — the two rows can fight each other; see review.
            var delayOptions = new[]
            {
                new TweakDropdownOption("1ms (Instant)",     1, IsRecommended: true),
                new TweakDropdownOption("10ms (Very Fast)", 10),
                new TweakDropdownOption("50ms (Fast)",      50),
                new TweakDropdownOption("100ms (Moderate)", 100),
                new TweakDropdownOption("200ms",            200),
                new TweakDropdownOption("400ms (Default)",  400, IsDefault: true),
            };

            return new[]
            {
                new TweakDefinition
                {
                    Id           = "customize-taskbar-autohide-hover-delay",
                    Name         = "Taskbar Auto-Hide Hover Delay",
                    Description  = "How long you must hover at the screen edge before the auto-hidden taskbar appears. Lower values reveal the taskbar faster",
                    Group        = "Behavior",
                    InputKind    = TweakInputKind.Dropdown,
                    IsPreference = true,
                    Options      = delayOptions,
                    ReadCurrentIndex = () =>
                    {
                        // Absent = Windows default 400ms. A value matching no option
                        // (e.g. 30000 from the thumbnail-disable row) renders unselected.
                        var cur = ReadDwordCu(advanced, "ExtendedUIHoverTime") ?? 400;
                        return Array.FindIndex(delayOptions, o => (int)o.Value == cur);
                    },
                    ApplyIndex = idx =>
                    {
                        SetHkcu(advanced, "ExtendedUIHoverTime", (int)delayOptions[idx].Value);
                        if (!_suppressRestart) ExplorerRestart.Request();
                        Log($"[TASKBAR] Auto-hide hover delay set to {delayOptions[idx].Label}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-taskbar-show-desktop-corner",
                    Name        = "Show Desktop from Taskbar Corner",
                    Description = "Click the far corner of the taskbar to minimize all windows and show the desktop",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    IsPreference = true,
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(advanced, "TaskbarSd") is int v ? v != 0 : true,
                    Apply       = enable =>
                    {
                        SetHkcu(advanced, "TaskbarSd", enable ? 1 : 0);
                        Log($"[TASKBAR] Show desktop from corner {(enable ? "enabled" : "disabled")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-taskbar-share-any-window",
                    Name        = "Share Any Window from Taskbar",
                    Description = "Lets you share any open window directly from the taskbar during a Teams call",
                    Group       = "Behavior",
                    InputKind   = TweakInputKind.Toggle,
                    IsPreference = true,
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(advanced, "TaskbarSn") is int v ? v != 0 : true,
                    Apply       = enable =>
                    {
                        SetHkcu(advanced, "TaskbarSn", enable ? 1 : 0);
                        Log($"[TASKBAR] Share any window {(enable ? "enabled" : "disabled")}.");
                    },
                },
            };
        }
    }
}
