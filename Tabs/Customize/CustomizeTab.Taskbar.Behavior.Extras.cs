using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── TASKBAR ▸ BEHAVIOR (Winhance parity vol.2 additions) ──
        private void BuildTaskbarBehaviorExtras(StackPanel behaviorSection)
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

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
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
                    Service?.Log($"[TASKBAR] Auto-hide hover delay set to {delayOptions[idx].Label}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-show-desktop-corner",
                Name        = "Show Desktop from Taskbar Corner",
                Description = "Click the far corner of the taskbar to minimize all windows and show the desktop",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                IsPreference = true,
                // Winhance taskbar-show-desktop (TaskbarSd): Recommended=1 → ON;
                // EnabledValue=[1,null] → absent = ON; Default=1 → ON. Applies live,
                // no Explorer restart in Winhance either.
                RecommendedState = true,
                DefaultState     = true,
                ReadState   = () => ReadDwordCu(advanced, "TaskbarSd") is int v ? v != 0 : true,
                Apply       = enable =>
                {
                    SetHkcu(advanced, "TaskbarSd", enable ? 1 : 0);
                    Service?.Log($"[TASKBAR] Show desktop from corner {(enable ? "enabled" : "disabled")}.");
                },
            });

            TweakHelpers.AddTweakRow(behaviorSection, new TweakDefinition
            {
                Id          = "customize-taskbar-share-any-window",
                Name        = "Share Any Window from Taskbar",
                Description = "Lets you share any open window directly from the taskbar during a Teams call",
                Group       = "Behavior",
                InputKind   = TweakInputKind.Toggle,
                IsPreference = true,
                // Winhance taskbar-share-window (TaskbarSn): Recommended=1 → ON;
                // EnabledValue=[1,null] → absent = ON; Default=1 → ON. Applies live.
                RecommendedState = true,
                DefaultState     = true,
                ReadState   = () => ReadDwordCu(advanced, "TaskbarSn") is int v ? v != 0 : true,
                Apply       = enable =>
                {
                    SetHkcu(advanced, "TaskbarSn", enable ? 1 : 0);
                    Service?.Log($"[TASKBAR] Share any window {(enable ? "enabled" : "disabled")}.");
                },
            });
        }
    }
}
