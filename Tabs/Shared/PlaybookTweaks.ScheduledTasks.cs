using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class PlaybookTweaks
    {
        // ═════════════════════════════════════════════════════════════════════
        // SCHEDULED TASKS
        // ═════════════════════════════════════════════════════════════════════

        private static readonly string[] ScheduledTasks =
        {
            // .NET Framework
            @"\Microsoft\Windows\.NET Framework\.NET Framework NGEN v4.0.30319 64 Critical",
            @"\Microsoft\Windows\.NET Framework\.NET Framework NGEN v4.0.30319 64",
            @"\Microsoft\Windows\.NET Framework\.NET Framework NGEN v4.0.30319 Critical",
            @"\Microsoft\Windows\.NET Framework\.NET Framework NGEN v4.0.30319",
            // Application Experience
            @"\Microsoft\Windows\Application Experience\StartupAppTask",
            @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            @"\Microsoft\Windows\Application Experience\MareBackup",
            // Maintenance
            @"\Microsoft\Windows\Autochk\Proxy",
            @"\Microsoft\Windows\BrokerInfrastructure\BgTaskRegistrationMaintenanceTask",
            @"\Microsoft\Windows\Diagnosis\Scheduled",
            @"\Microsoft\Windows\DiskCleanup\SilentCleanup",
            @"\Microsoft\Windows\DiskFootprint\StorageSense",
            @"\Microsoft\Windows\DiskFootprint\Diagnostics",
            @"\Microsoft\Windows\Defrag\ScheduledDefrag",
            @"\Microsoft\Windows\Maintenance\WinSAT",
            @"\Microsoft\Windows\Shell\IndexerAutomaticMaintenance",
            @"\Microsoft\Windows\SystemRestore\SR",
            // Disk Diagnostics
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
            @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticResolver",
            // Internationalization
            @"\Microsoft\Windows\International\Synchronize Language Settings",
            // Software Protection
            @"\Microsoft\Windows\SoftwareProtectionPlatform\SvcRestartTaskLogon",
            @"\Microsoft\Windows\SoftwareProtectionPlatform\SvcRestartTaskNetwork",
            // Time Synchronization
            @"\Microsoft\Windows\Time Synchronization\ForceSynchronizeTime",
            @"\Microsoft\Windows\Time Synchronization\SynchronizeTime",
            // UPnP
            @"\Microsoft\Windows\UPnP\UPnPHostConfig",
            // Windows Filtering Platform
            @"\Microsoft\Windows\Windows Filtering Platform\BfeOnServiceStartTypeChange",
            // Certificate Services
            @"\Microsoft\Windows\CertificateServicesClient\AikCertEnrollTask",
            @"\Microsoft\Windows\CertificateServicesClient\KeyPreGenTask",
            // Clip / License
            @"\Microsoft\Windows\Clip\License Validation",
            // Device Setup
            @"\Microsoft\Windows\Device Setup\Metadata Refresh",
            // Registry
            @"\Microsoft\Windows\Registry\RegIdleBackup",
            // Security
            @"\Microsoft\Windows\Security\Pwdless\IntelligentPwdlessTask",
            // State Repository
            @"\Microsoft\Windows\StateRepository\MaintenanceTasks",
            // Subscription
            @"\Microsoft\Windows\Subscription\EnableLicenseAcquisition",
            @"\Microsoft\Windows\Subscription\LicenseAcquisition",
            // Sysmain
            @"\Microsoft\Windows\Sysmain\ResPriStaticDbSync",
            @"\Microsoft\Windows\Sysmain\WsSwapAssessmentTask",
            // WDI
            @"\Microsoft\Windows\WDI\ResolutionHost",
            // Windows Error Reporting
            @"\Microsoft\Windows\Windows Error Reporting\QueueReporting",
            // WinInet
            @"\Microsoft\Windows\Wininet\CacheTask",
            // Task Scheduler
            @"\Microsoft\Windows\TaskScheduler",
            // Application Data
            @"\Microsoft\Windows\ApplicationData\appuriverifierdaily",
            @"\Microsoft\Windows\ApplicationData\DsSvcCleanup",
            // CEIP / Telemetry
            @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            // Device Information
            @"\Microsoft\Windows\Device Information\Device User",
            @"\Microsoft\Windows\Device Information\Device",
            // Feedback
            @"\Microsoft\Windows\Feedback\Siuf\DmClient",
            @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
            // Flighting
            @"\Microsoft\Windows\Flighting\FeatureConfig\ReconcileFeatures",
            @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataFlushing",
            @"\Microsoft\Windows\Flighting\FeatureConfig\UsageDataReporting",
            // Input
            @"\Microsoft\Windows\Input\LocalUserSyncDataAvailable",
            @"\Microsoft\Windows\Input\MouseSyncDataAvailable",
            @"\Microsoft\Windows\Input\PenSyncDataAvailable",
            @"\Microsoft\Windows\Input\TouchpadSyncDataAvailable",
            // Location
            @"\Microsoft\Windows\Location\Notifications",
            @"\Microsoft\Windows\Location\WindowsActionDialog",
            // Cloud Experience Host
            @"\Microsoft\Windows\CloudExperienceHost\CreateObjectTask",
            // Power / PI
            @"\Microsoft\Windows\PI\Sqm-Tasks",
            @"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem",
            // Maps
            @"\Microsoft\Windows\Maps\MapsToastTask",
            @"\Microsoft\Windows\Maps\MapsUpdateTask",
            // Memory Diagnostics
            @"\Microsoft\Windows\MemoryDiagnostic\ProcessMemoryDiagnosticEvents",
            @"\Microsoft\Windows\MemoryDiagnostic\RunFullMemoryDiagnostic",
        };

        private static async Task ApplyScheduledTasksAsync(ToolService log)
        {
            log.Log($"[PLAYBOOK] Disabling {ScheduledTasks.Length} scheduled tasks...");
            int ok = 0, skip = 0;

            foreach (var task in ScheduledTasks)
            {
                var result = await RunSchtasksAsync($"/Change /TN \"{task}\" /Disable");
                if (result) ok++; else skip++;
            }

            log.Log($"[PLAYBOOK] Scheduled tasks: {ok} disabled, {skip} not found.");
        }

        private static async Task EnableScheduledTasksAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Re-enabling scheduled tasks...");
            foreach (var task in ScheduledTasks)
                await RunSchtasksAsync($"/Change /TN \"{task}\" /Enable");
            log.Log("[PLAYBOOK] Scheduled tasks re-enabled.");
        }

    }
}
