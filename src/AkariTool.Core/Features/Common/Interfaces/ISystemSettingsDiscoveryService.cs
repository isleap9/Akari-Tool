using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Winhance ISystemSettingsDiscoveryService 1:1: two-phase state discovery.
/// Phase 1 batches raw value reads (registry, PowerCfg, scheduled tasks, special
/// handlers); phase 2 interprets each setting's raw values into a
/// <see cref="SettingStateResult"/> (enabled flag / selection index / numeric value).
/// </summary>
public interface ISystemSettingsDiscoveryService
{
    Task<Dictionary<string, Dictionary<string, object?>>> GetRawSettingsValuesAsync(IEnumerable<SettingDefinition> settings);
    Task<Dictionary<string, SettingStateResult>> GetSettingStatesAsync(IEnumerable<SettingDefinition> settings);
}
