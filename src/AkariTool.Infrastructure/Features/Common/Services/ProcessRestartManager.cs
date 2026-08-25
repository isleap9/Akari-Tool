using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

public sealed class ProcessRestartManager : IProcessRestartManager
{
    private readonly IAkariLogService _log;
    private int _suppressCount;

    public ProcessRestartManager(IAkariLogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public IDisposable SuppressRestarts()
    {
        Interlocked.Increment(ref _suppressCount);
        return new SuppressScope(this);
    }

    private sealed class SuppressScope(ProcessRestartManager owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Interlocked.Decrement(ref owner._suppressCount);
            }
        }
    }

    public async Task HandleProcessAndServiceRestartsAsync(SettingDefinition setting)
    {
        if (_suppressCount > 0)
        {
            if (!string.IsNullOrWhiteSpace(setting.RestartProcess))
                _log.Log(LogLevel.Debug, $"[ProcessRestart] Skipping process restart for '{setting.RestartProcess}' (restarts suppressed - parent will restart)");
            if (!string.IsNullOrWhiteSpace(setting.RestartService))
                _log.Log(LogLevel.Debug, $"[ProcessRestart] Skipping service restart for '{setting.RestartService}' (restarts suppressed - parent will restart)");
            return;
        }

        if (!string.IsNullOrWhiteSpace(setting.RestartProcess))
            await RestartProcessAsync(setting.RestartProcess).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(setting.RestartService))
            await RestartServiceAsync(setting.RestartService).ConfigureAwait(false);
    }

    /// <summary>
    /// Winhance FlushCoalescedRestartsAsync 1:1: restart each distinct process and
    /// service required by ANY applied setting — once. Called after a suppressed
    /// bulk apply so Explorer etc. restart a single time instead of per tweak.
    /// </summary>
    public async Task FlushCoalescedRestartsAsync(System.Collections.Generic.IReadOnlyCollection<SettingDefinition> appliedSettings)
    {
        var processes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var services = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var setting in appliedSettings)
        {
            if (!string.IsNullOrWhiteSpace(setting.RestartProcess))
                processes.Add(setting.RestartProcess);
            if (!string.IsNullOrWhiteSpace(setting.RestartService))
                services.Add(setting.RestartService);
        }

        _log.Log(LogLevel.Info, $"[ProcessRestart] Flushing coalesced restarts: {processes.Count} process(es), {services.Count} service(s)");

        foreach (var processName in processes)
            await RestartProcessAsync(processName).ConfigureAwait(false);

        foreach (var serviceName in services)
            await RestartServiceAsync(serviceName).ConfigureAwait(false);
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
