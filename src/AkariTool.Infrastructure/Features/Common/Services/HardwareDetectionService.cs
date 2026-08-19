using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Native;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Hardware capability probes used to gate hardware-dependent Power catalog
/// settings. Battery detection follows Akari's documented rule: use
/// GetSystemPowerStatus (BatteryFlag bit 128 = no battery), not powercfg probes.
/// Lid and hybrid-sleep come from GetPwrCapabilities (already bound in Core
/// PowerProf). Brightness support is proxied via battery+lid (a laptop) — a
/// deliberate simplification over Winhance's WMI fallback.
/// </summary>
public sealed class HardwareDetectionService : IHardwareDetectionService
{
    private readonly IAkariLogService _logService;

    public HardwareDetectionService(IAkariLogService logService)
    {
        _logService = logService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    /// <summary>
    /// True when the machine actually has a battery. BatteryFlag bit 128 means
    /// "No system battery"; 255 means the driver could not report a state, which
    /// is treated as absent so desktops never grow a Battery section.
    /// </summary>
    public Task<bool> HasBatteryAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!GetSystemPowerStatus(out var status)) return false;
                if (status.BatteryFlag == 255) return false; // unknown → assume none
                return (status.BatteryFlag & 128) == 0;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error detecting battery: {ex.Message}");
                return false;
            }
        });
    }

    public Task<bool> HasLidAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!PowerProf.GetPwrCapabilities(out var caps))
                {
                    _logService.Log(LogLevel.Warning, "GetPwrCapabilities call failed");
                    return false;
                }

                return caps.LidPresent;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error detecting lid support: {ex.Message}");
                return false;
            }
        });
    }

    public async Task<bool> SupportsBrightnessControlAsync()
    {
        var hasBattery = await HasBatteryAsync().ConfigureAwait(false);
        var hasLid = await HasLidAsync().ConfigureAwait(false);
        return hasBattery && hasLid;
    }

    public Task<bool> SupportsHybridSleepAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!PowerProf.GetPwrCapabilities(out var caps))
                {
                    _logService.Log(LogLevel.Warning, "GetPwrCapabilities call failed");
                    return false;
                }

                bool supported = caps.FastSystemS4;
                _logService.Log(LogLevel.Info, $"Hybrid sleep supported (FastSystemS4): {supported}");
                return supported;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error detecting hybrid sleep support: {ex.Message}");
                return false;
            }
        });
    }
}