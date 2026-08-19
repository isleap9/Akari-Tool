using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

public sealed class ProcessRestartManager : IProcessRestartManager
{
    private readonly IAkariLogService _log;

    public ProcessRestartManager(IAkariLogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting)
    {
        if (!string.IsNullOrWhiteSpace(setting.RestartProcess))
            await RestartProcessAsync(setting.RestartProcess).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(setting.RestartService))
            await RestartServiceAsync(setting.RestartService).ConfigureAwait(false);
    }

    private async Task RestartProcessAsync(string processName)
    {
        await Task.Run(() =>
        {
            try
            {
                var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                var procs = Process.GetProcessesByName(name);
                if (procs.Length == 0)
                {
                    _log.Log(LogLevel.Info, $"[ProcessRestart] No running process found: {processName}");
                    return;
                }
                foreach (var p in procs)
                {
                    try { p.Kill(); p.WaitForExit(3000); }
                    catch (Exception ex)
                    {
                        _log.Log(LogLevel.Warning, $"[ProcessRestart] Could not kill {processName}: {ex.Message}");
                    }
                    finally { p.Dispose(); }
                }
                if (name.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    _log.Log(LogLevel.Info, "[ProcessRestart] Explorer killed — Windows will relaunch it.");
                    return;
                }
                Process.Start(new ProcessStartInfo(processName) { UseShellExecute = true });
                _log.Log(LogLevel.Info, $"[ProcessRestart] Restarted: {processName}");
            }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Error, $"[ProcessRestart] Failed to restart {processName}: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    private async Task RestartServiceAsync(string serviceName)
    {
        await Task.Run(() =>
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    _log.Log(LogLevel.Info, $"[ServiceRestart] Stopped: {serviceName}");
                }
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                _log.Log(LogLevel.Info, $"[ServiceRestart] Started: {serviceName}");
            }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Warning, $"[ServiceRestart] Could not restart service '{serviceName}': {ex.Message}");
            }
        }).ConfigureAwait(false);
    }
}
