using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Filters settings whose backing PowerCfg subgroup/setting does not exist on this
/// machine (ValidateExistence), and drops settings that are hardware-controlled
/// (CheckForHardwareControl: Min=0/Max=0 means the setting is not user-writable).
/// </summary>
public interface IPowerSettingsValidationService
{
    Task<IEnumerable<SettingDefinition>> FilterSettingsByExistenceAsync(IEnumerable<SettingDefinition> settings);
}