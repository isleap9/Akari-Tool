using System.Threading.Tasks;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Hardware capability probes used to gate hardware-dependent Power catalog
/// settings (RequiresBattery, RequiresLid, RequiresBrightnessSupport,
/// RequiresHybridSleepCapable).
/// </summary>
public interface IHardwareDetectionService
{
    Task<bool> HasBatteryAsync();
    Task<bool> HasLidAsync();
    Task<bool> SupportsBrightnessControlAsync();
    Task<bool> SupportsHybridSleepAsync();
}