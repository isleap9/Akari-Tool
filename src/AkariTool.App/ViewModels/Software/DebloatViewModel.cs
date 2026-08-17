using System.Collections.ObjectModel;
using AkariTool.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Software;

/// <summary>
/// The Software ▸ Debloat panel (rail tag "Debloat") — MVVM port of net8
/// <c>DebloatTab.xaml.cs</c>. Stage 4, the final Software-tab stage.
///
/// ⚠ BESPOKE, NOT A TWEAK PAGE. Rows are <c>(Title, Desc, RunScript, UndoScript)</c>
/// tuples (identity = run-script filename), NOT <c>TweakDefinition</c>/<c>AppDefinition</c>.
/// It never registers with <c>TweakRegistry</c>, is a plain DI singleton, and is absent
/// from the warm-up enumeration — the <c>[WARMUP]</c> total must stay 439.
///
/// ⚠ NO CONFIRMATION DIALOGS ANYWHERE — matches net8 exactly (Phase 27/28). Several rows
/// run destructive, irreversible actions with no gate; that is the deliberate net8
/// behaviour isleap chose to preserve verbatim. The embedded .ps1 scripts are run
/// unchanged (the two known net8 undo bugs — StoreSearch, Debloat — are left as-is).
///
/// Group titles, order, row order, and every Title/Description/script-name string are
/// verbatim from net8 <c>DebloatTab.Build()</c>.
/// </summary>
public sealed class DebloatViewModel
{
    private readonly ToolService _tool;

    /// <summary>net8 tracked applied titles in <c>BaseTab.AppliedTweaks</c>; same list, panel-local.</summary>
    private readonly List<string> _appliedTweaks = [];

    public string Title => "Debloat";

    public string Subtitle =>
        "One-click PowerShell scripts: disable telemetry components, remove bloatware, and clean up Edge/OneDrive.";

    public ObservableCollection<DebloatGroupViewModel> Groups { get; } = [];

    private bool _built;
    private readonly object _buildLock = new();

    public DebloatViewModel(ToolService tool) => _tool = tool;

    /// <summary>
    /// Builds the three groups verbatim from net8 <c>DebloatTab.Build()</c>. Idempotent +
    /// lock-guarded (DI singleton). No catalog, no registration — just tuple data.
    /// </summary>
    public void Build()
    {
        lock (_buildLock)
        {
            if (_built) return;
            _built = true;
        }

        AddGroup("Privacy & Telemetry",
            ("Telemetry — Disable",             "Disables Windows data collection and telemetry",                  "Telemetry.ps1",             "Telemetry-Undo.ps1"),
            ("Activity History — Disable",      "Erases recent docs, clipboard, and run history",                  "ActivityHistory.ps1",       "ActivityHistory-Undo.ps1"),
            ("Location Tracking — Disable",     "Disables Windows location services",                              "LocationTracking.ps1",      "LocationTracking-Undo.ps1"),
            ("PS7 Telemetry — Disable",         "Opts out of PowerShell 7 telemetry",                              "PS7Telemetry.ps1",          "PS7Telemetry-Undo.ps1"),
            ("Windows AI — Disable",            "Removes Copilot, Recall, and all AI features",                    "WindowsAI.ps1",             "WindowsAI-Undo.ps1"),
            ("Consumer Features — Disable",     "Disables suggested apps, tips, and Windows promotions",           "ConsumerFeatures.ps1",      "ConsumerFeatures-Undo.ps1"),
            ("Background Apps — Disable",       "Stops Microsoft Store apps running in the background",            "DisableBGApps.ps1",         "DisableBGApps-Undo.ps1"),
            ("Store Search — Disable",          "Hides Microsoft Store results from Start Menu search",            "StoreSearch.ps1",           "StoreSearch-Undo.ps1"),
            ("Delivery Optimization — Disable", "Stops Windows using your bandwidth to share updates",             "DeliveryOptimization.ps1",  "DeliveryOptimization-Undo.ps1"),
            ("Device Companion Apps — Block",   "Stops Windows fetching vendor apps/ads when you plug in a device", "DeviceMetadata.ps1",       "DeviceMetadata-Undo.ps1"),
            ("WPBT — Disable",                  "Blocks OEM firmware from executing vendor binaries at boot",       "WPBT.ps1",                  "WPBT-Undo.ps1"));

        AddGroup("Apps & Components",
            ("Unwanted Apps — Remove",       "Removes bloatware UWP apps (AkariOS AME Playbook list — 60+ packages)",   "Debloat.ps1",               "Debloat-Undo.ps1"),
            ("OneDrive — Remove",            "Completely removes OneDrive from the system",                     "RemoveOneDrive.ps1",        "RemoveOneDrive-Undo.ps1"),
            ("Microsoft Edge — Debloat",     "Disables telemetry, popups, and annoyances in Edge",             "EdgeDebloat.ps1",           "EdgeDebloat-Undo.ps1"),
            ("Microsoft Edge — Remove",      "Fully uninstalls Microsoft Edge from the system",                 "RemoveEdge.ps1",            ""),
            ("Widgets — Remove",             "Removes the Widgets button from the taskbar",                     "Widgets.ps1",               "Widgets-Undo.ps1"));

        AddGroup("Cleanup",
            ("Create Restore Point",         "Creates a Windows system restore point before making changes",    "RestorePoint.ps1",          ""),
            ("Disk Cleanup — Run",           "Runs cleanup on C: and removes old Windows updates",             "DiskCleanup.ps1",           ""),
            ("Temporary Files — Remove",     "Clears temp folders and prefetch files",                          "TempFiles.ps1",             ""),
            ("O&O ShutUp10++ — Run",         "Downloads and launches the O&O ShutUp10 privacy tool",           "OOSU.ps1",                  ""));
    }

    private void AddGroup(string title, params (string Title, string Desc, string Run, string Undo)[] rows)
    {
        var group = new DebloatGroupViewModel(title);
        foreach (var (t, d, run, undo) in rows)
            group.Rows.Add(new DebloatRowViewModel(t, d, run, undo, _tool, _appliedTweaks));
        Groups.Add(group);
    }
}

/// <summary>One titled group of Debloat rows (net8 <c>BuildGroup</c> card).</summary>
public sealed class DebloatGroupViewModel(string title)
{
    /// <summary>net8 rendered the group header upper-cased in the mono font.</summary>
    public string Header { get; } = title.ToUpperInvariant();

    public ObservableCollection<DebloatRowViewModel> Rows { get; } = [];
}
