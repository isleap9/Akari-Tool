using AkariTool.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs.Gaming
{
    // MVVM PORT (Phase 11): the Gaming ▸ Scheduled Tasks section, extracted VERBATIM
    // from build #2's Tabs/Gaming/GamingTab.ScheduledTasks.cs (BuildScheduledTasks +
    // the ReadTaskEnabled / SetTaskEnabled helpers). 18 TweakDefinition toggle rows,
    // each enabling/disabling one scheduled task via schtasks.exe. Same 18 Ids in the
    // same order, same task paths, same Name/Description, same
    // RecommendedState=false / DefaultState=true polarity (recommended OFF/disabled,
    // Windows default ON/enabled — NOT normalized or inverted), same read/apply logic.
    //
    // This is a SELF-CONTAINED implementation — it does NOT wrap
    // Tabs/Shared/PlaybookTweaks.ScheduledTasks.cs (a different, unrelated bulk
    // surface, untouched).
    //
    // Only framework-mechanical changes vs net8 (same pattern as ServiceDropdown in
    // Phase 9): the two helpers are `static` with the tab's `Log` threaded through as
    // a parameter (net8 captured the instance Log()); apply routes through the
    // already-present static TweakHelpers.RunCommand; BuildScheduledTasks(StackPanel)+
    // AddSection became the array-returning ScheduledTasks(Action<string>) method. No
    // task, Id, path, or text was reordered, retyped, or "cleaned up".
    public static partial class GamingTweaks
    {
        // ══════════════════════════════════════════════════════════════════════
        // SCHEDULED TASKS
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] ScheduledTasks(Action<string> Log)
        {
            // All tasks: recommended=OFF (false), default=ON (true)
            (string id, string name, string desc, string taskPath)[] tasks =
            {
                ("gaming-task-compatibility-appraiser", "Microsoft Compatibility Appraiser",
                 "Collects program compatibility telemetry for Windows upgrades. Disable to reduce telemetry",
                 @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser"),
                ("gaming-task-program-data-updater", "Program Data Updater",
                 "Updates the program compatibility database with information about installed applications",
                 @"\Microsoft\Windows\Application Experience\ProgramDataUpdater"),
                ("gaming-task-ceip-consolidator", "CEIP Consolidator",
                 "Consolidates and uploads usage data as part of the Customer Experience Improvement Program",
                 @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"),
                ("gaming-task-usb-ceip", "USB CEIP",
                 "Collects USB device-related telemetry for the Customer Experience Improvement Program",
                 @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"),
                ("gaming-task-disk-diagnostic", "Disk Diagnostic Data Collector",
                 "Collects disk diagnostic information and S.M.A.R.T. data for Microsoft",
                 @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"),
                ("gaming-task-feedback-dmclient", "Feedback DmClient",
                 "Collects feedback and diagnostic data for Microsoft",
                 @"\Microsoft\Windows\Feedback\Siuf\DmClient"),
                ("gaming-task-feedback-dmclient-download", "Feedback DmClient Scenario Download",
                 "Downloads feedback scenarios and configuration data from Microsoft",
                 @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"),
                ("gaming-task-error-reporting-queue", "Windows Error Reporting Queue",
                 "Queues crash reports and error data to send to Microsoft",
                 @"\Microsoft\Windows\Windows Error Reporting\QueueReporting"),
                ("gaming-task-sqm", "Software Quality Metrics",
                 "Collects software quality metrics and reliability data for Microsoft telemetry",
                 @"\Microsoft\Windows\PI\Sqm-Tasks"),
                ("gaming-task-mare-backup", "MAR Backup",
                 "Backs up Microsoft Assisted Recovery data. Disable to reduce background system activity",
                 @"\Microsoft\Windows\Application Experience\MareBackup"),
                ("gaming-task-startup-app", "Startup App Task",
                 "Tracks and monitors startup applications for telemetry and diagnostics",
                 @"\Microsoft\Windows\Application Experience\StartupAppTask"),
                ("gaming-task-maps-update", "Maps Update",
                 "Updates offline maps data for the Windows Maps app. Disable if you don't use the Maps app",
                 @"\Microsoft\Windows\Maps\MapsUpdateTask"),
                ("gaming-task-autochk-proxy", "AutoChk Proxy",
                 "Performs disk checking operations and collects diagnostic data",
                 @"\Microsoft\Windows\Autochk\Proxy"),
                ("gaming-task-power-efficiency", "Power Efficiency Diagnostics",
                 "Analyzes system power consumption and collects energy efficiency data",
                 @"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem"),
                ("gaming-task-windows-ai-recall-config", "Windows AI Recall Configuration",
                 "Windows AI Recall configuration task. Disable to prevent Recall from being configured in the background",
                 @"\Microsoft\Windows\WindowsAI\RecallConfiguration"),
                ("gaming-task-windows-ai-recall-pipeline", "Windows AI Recall Pipeline",
                 "Windows AI Recall pipeline task. Disable to prevent Recall snapshot pipeline from running in the background",
                 @"\Microsoft\Windows\WindowsAI\RecallPipeline"),
                ("gaming-task-office-actions-server", "Office Actions Server",
                 "Office AI Actions Server scheduled task. Disable to prevent Office AI from running in the background",
                 @"\Microsoft\Office\Office Actions Server"),
                ("gaming-task-family-safety", "Family Safety Monitor Task",
                 "Monitors family safety settings and usage. Disable if you don't use family safety features",
                 @"\Microsoft\Windows\Shell\FamilySafetyMonitor"),
            };

            return tasks.Select(t => new TweakDefinition
            {
                Id               = t.id,
                Name             = t.name,
                Description      = t.desc,
                RecommendedState = false,
                DefaultState     = true,
                ReadState        = () => ReadTaskEnabled(t.taskPath),
                Apply            = on => SetTaskEnabled(Log, t.taskPath, t.name, on)
            }).ToArray();
        }
        // ── Scheduled task helpers ────────────────────────────────────────────────

        private static bool? ReadTaskEnabled(string taskPath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe",
                    $"/Query /TN \"{taskPath}\" /FO CSV /NH")
                { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) return null;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) return null;
                return !output.Contains("Disabled");
            }
            catch { return null; }
        }

        private static void SetTaskEnabled(Action<string> Log, string taskPath, string name, bool enable)
        {
            try
            {
                TweakHelpers.RunCommand("schtasks.exe", $"/Change /TN \"{taskPath}\" /{(enable ? "Enable" : "Disable")}");
                Log($"{name}: {(enable ? "enabled" : "disabled")}.");
            }
            catch (Exception ex) { Log($"ERROR task {name}: {ex.Message}"); }
        }
    }
}
