using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Optimize.Services;

/// <summary>
/// Special-setting handler for the "updates-policy-mode" dropdown. Ported 1:1 from
/// Winhance's UpdateService — the composite multi-value read (Paused/Disabled states
/// collide under single-value matching) plus the service/scheduled-task/DLL work that
/// the generic SettingOperationExecutor cannot express.
/// </summary>
public sealed class WindowsUpdatePolicyHandler(
    IAkariLogService logService,
    IWindowsRegistryService registryService,
    IProcessExecutor processExecutor,
    IPowerShellRunner powerShellRunner,
    IFileSystemService fileSystemService) : ISpecialSettingHandler
{
    private const string AuHklm = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    private const string AuHkcu = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    private const string UxHklm = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private const string AuSub = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    private const string UxSub = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object? value)
    {
        if (setting.Id == "updates-policy-mode" && value is int index)
        {
            await ApplyUpdatesPolicyModeAsync(index).ConfigureAwait(false);
            return true;
        }
        return false;
    }

    public async Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(
        IEnumerable<SettingDefinition> settings)
    {
        if (settings.Any(s => s.Id == "updates-policy-mode"))
        {
            var idx = await GetCurrentUpdatePolicyIndexAsync().ConfigureAwait(false);
            return new Dictionary<string, Dictionary<string, object?>>
            {
                ["updates-policy-mode"] = new() { ["CurrentPolicyIndex"] = idx }
            };
        }
        return new Dictionary<string, Dictionary<string, object?>>();
    }

    // ── Detection ──────────────────────────────────────────────────────────

    private async Task<int> GetCurrentUpdatePolicyIndexAsync()
    {
        await Task.CompletedTask.ConfigureAwait(false);

        // Probe 1: critical DLLs renamed (Disabled mode).
        foreach (var dll in new[] { "WaaSMedicSvc.dll", "wuaueng.dll" })
        {
            var dllPath = $@"C:\Windows\System32\{dll}";
            var backupPath = $@"C:\Windows\System32\{System.IO.Path.GetFileNameWithoutExtension(dll)}_BAK.dll";
            if (fileSystemService.FileExists(backupPath) && !fileSystemService.FileExists(dllPath))
                return 3;
        }

        // Probe 2: pause timestamps present (Paused mode).
        foreach (var name in new[] { "PauseUpdatesStartTime", "PauseUpdatesExpiryTime", "PausedQualityDate", "PausedFeatureDate" })
        {
            if (registryService.GetValue(UxHklm, name) != null)
                return 2;
        }

        // Probe 3: feature updates deferred (Security only mode).
        if (registryService.GetValue(UxHklm, "DeferFeatureUpdates") is int d && d == 1)
            return 1;

        // Probe 4: Normal.
        return 0;
    }

    // ── Apply ──────────────────────────────────────────────────────────────

    private async Task ApplyUpdatesPolicyModeAsync(int index)
    {
        logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] Applying updates-policy-mode index {index}");

        switch (index)
        {
            case 0:
                await RestoreCriticalDllsAsync().ConfigureAwait(false);
                await EnableUpdateServicesAsync().ConfigureAwait(false);
                await EnableUpdateTasksAsync().ConfigureAwait(false);
                ApplyNormalRegistry();
                break;
            case 1:
                await RestoreCriticalDllsAsync().ConfigureAwait(false);
                await EnableUpdateServicesAsync().ConfigureAwait(false);
                ApplySecurityOnlyRegistry();
                break;
            case 2:
                await RestoreCriticalDllsAsync().ConfigureAwait(false);
                await SetUpdateServicesManualAsync().ConfigureAwait(false);
                ApplyPausedRegistry();
                break;
            case 3:
                await DisableUpdateServicesAsync().ConfigureAwait(false);
                await DisableUpdateTasksAsync().ConfigureAwait(false);
                await RenameCriticalDllsAsync().ConfigureAwait(false);
                await CleanupUpdateFilesAsync().ConfigureAwait(false);
                ApplyDisabledRegistry();
                break;
            default:
                throw new ArgumentException($"Invalid index: {index}");
        }

        logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] Successfully applied updates-policy-mode index {index}");
    }

    // ── Registry helpers (Microsoft.Win32 directly) ────────────────────────

    private void ApplyNormalRegistry()
    {
        var auValues = new[]
        {
            "NoAutoUpdate", "AUOptions", "NoAUShutdownOption",
            "AlwaysAutoRebootAtScheduledTime", "AutoInstallMinorUpdates", "UseWUServer"
        };
        foreach (var name in auValues)
        {
            DeleteValue(RegistryHive.LocalMachine, AuSub, name);
            DeleteValue(RegistryHive.CurrentUser, AuSub, name);
        }

        var uxValues = new[]
        {
            "BranchReadinessLevel", "DeferFeatureUpdates", "DeferFeatureUpdatesPeriodInDays",
            "DeferQualityUpdates", "DeferQualityUpdatesPeriodInDays",
            "PauseFeatureUpdatesStartTime", "PauseFeatureUpdatesEndTime",
            "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime",
            "PauseUpdatesStartTime", "PauseUpdatesExpiryTime",
            "PausedQualityDate", "PausedFeatureDate", "FlightSettingsMaxPauseDays",
            "PausedFeatureStatus", "PausedQualityStatus"
        };
        foreach (var name in uxValues)
            DeleteValue(RegistryHive.LocalMachine, UxSub, name);
    }

    private void ApplySecurityOnlyRegistry()
    {
        ApplyNormalRegistry();

        Registry.SetValue(AuHklm, "AUOptions", 2, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "AUOptions", 2, RegistryValueKind.DWord);

        Registry.SetValue(UxHklm, "BranchReadinessLevel", 20, RegistryValueKind.DWord);
        Registry.SetValue(UxHklm, "DeferFeatureUpdates", 1, RegistryValueKind.DWord);
        Registry.SetValue(UxHklm, "DeferFeatureUpdatesPeriodInDays", 365, RegistryValueKind.DWord);
        Registry.SetValue(UxHklm, "DeferQualityUpdates", 1, RegistryValueKind.DWord);
        Registry.SetValue(UxHklm, "DeferQualityUpdatesPeriodInDays", 7, RegistryValueKind.DWord);
    }

    private void ApplyPausedRegistry()
    {
        ApplyNormalRegistry();

        Registry.SetValue(AuHklm, "NoAutoUpdate", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "NoAutoUpdate", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHklm, "AUOptions", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "AUOptions", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHklm, "NoAUShutdownOption", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "NoAUShutdownOption", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHklm, "AlwaysAutoRebootAtScheduledTime", 0, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "AlwaysAutoRebootAtScheduledTime", 0, RegistryValueKind.DWord);
        Registry.SetValue(AuHklm, "AutoInstallMinorUpdates", 0, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "AutoInstallMinorUpdates", 0, RegistryValueKind.DWord);
        Registry.SetValue(AuHklm, "UseWUServer", 0, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "UseWUServer", 0, RegistryValueKind.DWord);

        var stringPairs = new (string Name, string Value)[]
        {
            ("PauseFeatureUpdatesStartTime", "2025-01-01T00:00:00Z"),
            ("PauseFeatureUpdatesEndTime",   "2051-12-31T00:00:00Z"),
            ("PauseQualityUpdatesStartTime", "2025-01-01T00:00:00Z"),
            ("PauseQualityUpdatesEndTime",   "2051-12-31T00:00:00Z"),
            ("PauseUpdatesStartTime",        "2025-01-01T00:00:00Z"),
            ("PauseUpdatesExpiryTime",       "2051-12-31T00:00:00Z"),
            ("PausedQualityDate",            "2025-01-01T00:00:00Z"),
            ("PausedFeatureDate",            "2025-01-01T00:00:00Z"),
        };
        foreach (var (name, value) in stringPairs)
            Registry.SetValue(UxHklm, name, value, RegistryValueKind.String);

        Registry.SetValue(UxHklm, "FlightSettingsMaxPauseDays", 10023, RegistryValueKind.DWord);
        Registry.SetValue(UxHklm, "PausedFeatureStatus", 1, RegistryValueKind.DWord);
        Registry.SetValue(UxHklm, "PausedQualityStatus", 1, RegistryValueKind.DWord);
    }

    private void ApplyDisabledRegistry()
    {
        ApplyNormalRegistry();

        Registry.SetValue(AuHklm, "NoAutoUpdate", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "NoAutoUpdate", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHklm, "AUOptions", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "AUOptions", 1, RegistryValueKind.DWord);
        Registry.SetValue(AuHklm, "UseWUServer", 0, RegistryValueKind.DWord);
        Registry.SetValue(AuHkcu, "UseWUServer", 0, RegistryValueKind.DWord);
    }

    private void DeleteValue(RegistryHive hive, string subPath, string name)
    {
        try
        {
            RegistryKey.OpenBaseKey(hive, RegistryView.Default)
                .OpenSubKey(subPath, writable: true)?
                .DeleteValue(name, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to delete {hive}\\{subPath}\\{name}: {ex.Message}");
        }
    }

    // ── Service / scheduled-task / DLL helpers (1:1 port of Winhance) ───────

    private async Task<bool> RunCommandAsync(string command)
    {
        try
        {
            var result = await processExecutor.ExecuteAsync("cmd.exe", $"/c chcp 65001 && {command}").ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Command failed '{command}': {ex.Message}");
            return false;
        }
    }

    private async Task DisableUpdateServicesAsync()
    {
        var services = new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" };
        foreach (var service in services)
        {
            try
            {
                await RunCommandAsync($"net stop {service}").ConfigureAwait(false);
                await RunCommandAsync($"sc config {service} start= disabled").ConfigureAwait(false);
                await RunCommandAsync($"sc failure {service} reset= 0 actions= \"\"").ConfigureAwait(false);
                logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] Disabled service: {service}");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to disable {service}: {ex.Message}");
            }
        }
    }

    private async Task EnableUpdateServicesAsync()
    {
        var services = new[]
        {
            ("wuauserv", "auto"),
            ("UsoSvc", "auto"),
            ("WaaSMedicSvc", "demand")
        };
        foreach (var (service, startType) in services)
        {
            try
            {
                await RunCommandAsync($"sc config {service} start= {startType}").ConfigureAwait(false);
                await RunCommandAsync($"net start {service}").ConfigureAwait(false);
                logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] Enabled service: {service}");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to enable {service}: {ex.Message}");
            }
        }
    }

    private async Task SetUpdateServicesManualAsync()
    {
        var services = new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" };
        foreach (var service in services)
        {
            try
            {
                await RunCommandAsync($"sc config {service} start= demand").ConfigureAwait(false);
                logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] Set {service} to manual");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to set {service} to manual: {ex.Message}");
            }
        }
    }

    private async Task DisableUpdateTasksAsync()
    {
        var folderPaths = new[]
        {
            @"\Microsoft\Windows\InstallService\",
            @"\Microsoft\Windows\UpdateOrchestrator\",
            @"\Microsoft\Windows\UpdateAssistant\",
            @"\Microsoft\Windows\WaaSMedic\",
            @"\Microsoft\Windows\WindowsUpdate\",
        };
        foreach (var folderPath in folderPaths)
        {
            try
            {
                var script = $"Get-ScheduledTask -TaskPath '{folderPath}' -ErrorAction SilentlyContinue | Disable-ScheduledTask -ErrorAction SilentlyContinue";
                await powerShellRunner.RunScriptInMemoryAsync(script).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to process tasks in {folderPath}: {ex.Message}");
            }
        }
    }

    private async Task EnableUpdateTasksAsync()
    {
        var folderPaths = new[]
        {
            @"\Microsoft\Windows\UpdateOrchestrator\",
            @"\Microsoft\Windows\WindowsUpdate\",
        };
        foreach (var folderPath in folderPaths)
        {
            try
            {
                var script = $"Get-ScheduledTask -TaskPath '{folderPath}' -ErrorAction SilentlyContinue | Enable-ScheduledTask -ErrorAction SilentlyContinue";
                await powerShellRunner.RunScriptInMemoryAsync(script).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to process tasks in {folderPath}: {ex.Message}");
            }
        }
    }

    private async Task RenameCriticalDllsAsync()
    {
        await Task.CompletedTask.ConfigureAwait(false);
        var dlls = new[] { "WaaSMedicSvc.dll", "wuaueng.dll" };
        foreach (var dll in dlls)
        {
            try
            {
                var dllPath = $@"C:\Windows\System32\{dll}";
                var backupPath = $@"C:\Windows\System32\{System.IO.Path.GetFileNameWithoutExtension(dll)}_BAK.dll";

                if (fileSystemService.FileExists(backupPath))
                {
                    if (fileSystemService.FileExists(dllPath))
                    {
                        logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] Conflict detected for {dll}. Deleting stale backup.");
                        await RunCommandAsync($"takeown /f \"{backupPath}\"").ConfigureAwait(false);
                        await RunCommandAsync($"icacls \"{backupPath}\" /grant *S-1-1-0:F").ConfigureAwait(false);
                        System.IO.File.Delete(backupPath);
                    }
                    else
                    {
                        continue;
                    }
                }

                if (!fileSystemService.FileExists(dllPath) || fileSystemService.FileExists(backupPath))
                    continue;

                await RunCommandAsync($"takeown /f \"{dllPath}\"").ConfigureAwait(false);
                await RunCommandAsync($"icacls \"{dllPath}\" /grant *S-1-1-0:F").ConfigureAwait(false);

                System.IO.File.Move(dllPath, backupPath);
                logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] Renamed {dll} to backup");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to rename {dll}: {ex.Message}");
            }
        }
    }

    private async Task RestoreCriticalDllsAsync()
    {
        var dlls = new[] { "WaaSMedicSvc.dll", "wuaueng.dll" };
        foreach (var dll in dlls)
        {
            try
            {
                var dllPath = $@"C:\Windows\System32\{dll}";
                var backupPath = $@"C:\Windows\System32\{System.IO.Path.GetFileNameWithoutExtension(dll)}_BAK.dll";

                if (fileSystemService.FileExists(backupPath))
                {
                    if (fileSystemService.FileExists(dllPath))
                    {
                        logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] System already restored {dll}. Removing backup.");
                        await RunCommandAsync($"takeown /f \"{backupPath}\"").ConfigureAwait(false);
                        await RunCommandAsync($"icacls \"{backupPath}\" /grant *S-1-1-0:F").ConfigureAwait(false);
                        System.IO.File.Delete(backupPath);
                    }
                    else
                    {
                        await RunCommandAsync($"takeown /f \"{backupPath}\"").ConfigureAwait(false);
                        await RunCommandAsync($"icacls \"{backupPath}\" /grant *S-1-1-0:F").ConfigureAwait(false);

                        System.IO.File.Move(backupPath, dllPath);
                        logService.Log(LogLevel.Info, $"[WindowsUpdatePolicyHandler] Restored {dll} from backup");
                    }
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to restore {dll}: {ex.Message}");
            }
        }
    }

    private async Task CleanupUpdateFilesAsync()
    {
        try
        {
            var script = "Remove-Item 'C:\\Windows\\SoftwareDistribution\\*' -Recurse -Force -ErrorAction SilentlyContinue";
            await powerShellRunner.RunScriptInMemoryAsync(script).ConfigureAwait(false);
            logService.Log(LogLevel.Info, "[WindowsUpdatePolicyHandler] Cleaned SoftwareDistribution folder");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[WindowsUpdatePolicyHandler] Failed to cleanup update files: {ex.Message}");
        }
    }
}
