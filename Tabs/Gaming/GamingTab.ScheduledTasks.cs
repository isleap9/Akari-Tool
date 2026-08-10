using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public partial class GamingTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // SCHEDULED TASKS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildScheduledTasks(StackPanel panel)
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

            var defs = tasks.Select(t => new TweakDefinition
            {
                Id               = t.id,
                Name             = t.name,
                Description      = t.desc,
                RecommendedState = false,
                DefaultState     = true,
                ReadState        = () => ReadTaskEnabled(t.taskPath),
                Apply            = on => SetTaskEnabled(t.taskPath, t.name, on)
            }).ToArray();

            AddSection(panel, "Scheduled Tasks", defs);
        }
        // ── Scheduled task helpers ────────────────────────────────────────────────

        private bool? ReadTaskEnabled(string taskPath)
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

        private void SetTaskEnabled(string taskPath, string name, bool enable)
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
