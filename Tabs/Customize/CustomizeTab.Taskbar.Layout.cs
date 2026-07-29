using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── TASKBAR ▸ LAYOUT ──
        private void BuildTaskbarLayout(StackPanel panel)
        {
            // ── Layout ────────────────────────────────────────────────────────
            var layoutSection = TweakHelpers.BuildSection(panel, "Layout");

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-align-left",
                Name        = "Align Taskbar Left",
                Description = "Moves taskbar icons to the left — restores Windows 10 layout",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance models alignment as a preference dropdown with no recommended
                // value; Windows 11 ships centered (TaskbarAl=1) so OFF is the default.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadTaskbarAlignLeft,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "TaskbarAl", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Alignment set to {(enable ? "left" : "center")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-search",
                Name        = "Hide Search Bar",
                Description = "Removes the search box/icon from the taskbar",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance models this as a 4-option preference dropdown with no
                // recommended value. Search is visible out of the box → OFF is default.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadSearchHidden,
                Apply       = enable =>
                {
                    // SearchboxTaskbarMode: 0 = hidden, 1 = icon only, 2 = icon + label,
                    // 3 = full search box. OFF restores 3 — the Windows 11 default — not 1,
                    // which would silently downgrade the taskbar to an icon-only search.
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Search",
                        "SearchboxTaskbarMode", enable ? 0 : 3);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Search bar {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-task-view",
                Name        = "Hide Task View Button",
                Description = "Removes the Task View (virtual desktops) button from the taskbar",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-task-view "Show Task View button": Recommended=0, Default=1.
                // This row inverts the polarity (Hide), so both states flip.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadTaskViewHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "ShowTaskViewButton", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Task View button {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-widgets",
                Name        = "Hide Widgets Button",
                Description = "Removes the Widgets (news/weather) button from the taskbar",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-widgets "Show Widgets": Recommended=0, Default=key-absent→shown.
                // Inverted here (Hide). NOTE: Winhance drives the AllowNewsAndInterests
                // policy; this row drives the per-user TaskbarDa value instead.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadWidgetsHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "TaskbarDa", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Widgets button {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-chat",
                Name        = "Hide Chat / Meet Now",
                Description = "Removes the Chat (Microsoft Teams) button from the taskbar",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-meet-now "Remove 'Meet Now'": Recommended=1 (remove),
                // Default=key-absent→shown. Same polarity as this row (both say "hide").
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadChatHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "TaskbarMn", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Chat button {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-copilot",
                Name        = "Hide Copilot Button",
                Description = "Removes the Copilot AI button from the taskbar",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-copilot: Recommended=0 (hidden), Default=1 (shown). Inverted here.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadCopilotHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "ShowCopilotButton", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Copilot button {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-copilot-companion",
                Name        = "Hide Copilot Companion Button",
                Description = "Removes the Copilot Companion button from the taskbar (newer builds)",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-copilot-companion: Recommended=0, Default=1. Inverted here.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadCopilotCompanionHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "TaskbarCompanion", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Copilot Companion button {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-copilot-pwa-pin",
                Name        = "Hide Copilot PWA Pin",
                Description = "Removes the pinned Copilot web app from the taskbar",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-copilot-pwa-pin: Recommended=0, Default=1. Inverted here.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadCopilotPwaPinHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "CopilotPWAPin", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Copilot PWA pin {(enable ? "hidden" : "shown")}.");
                },
            });

            TweakHelpers.AddTweakRow(layoutSection, new TweakDefinition
            {
                Id          = "customize-taskbar-hide-recall-pin",
                Name        = "Hide Recall Pin",
                Description = "Removes the pinned Recall shortcut from the taskbar",
                Group       = "Layout",
                InputKind   = TweakInputKind.Toggle,
                // Winhance taskbar-recall-pin: Recommended=0, Default=1. Inverted here.
                RecommendedState = true,
                DefaultState     = false,
                ReadState   = SystemStateReader.ReadRecallPinHidden,
                Apply       = enable =>
                {
                    SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        "RecallPin", enable ? 0 : 1);
                    if (!_suppressRestart) ExplorerRestart.Request();
                    Service?.Log($"[TASKBAR] Recall pin {(enable ? "hidden" : "shown")}.");
                },
            });
        }
    }
}
