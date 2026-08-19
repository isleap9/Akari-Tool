using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Native (PowrProf) reads for power settings and power plans, with a short-lived
/// plan cache. Implementation lives in Infrastructure; this interface mirrors the
/// Winhance <c>IPowerSettingsQueryService</c> 1:1.
/// </summary>
public interface IPowerSettingsQueryService
{
    Task<List<PowerPlan>> GetAvailablePowerPlansAsync();
    Task<PowerPlan> GetActivePowerPlanAsync();
    Task<(int? acValue, int? dcValue)> GetPowerSettingACDCValuesAsync(PowerCfgSetting powerCfgSetting);
    Task<Dictionary<string, (int? acValue, int? dcValue)>> GetAllPowerSettingsACDCAsync(string powerPlanGuid = "SCHEME_CURRENT");
    Task<bool> IsSettingHardwareControlledAsync(PowerCfgSetting powerCfgSetting);
    void InvalidateCache();
}