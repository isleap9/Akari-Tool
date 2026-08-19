using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Filters settings whose backing PowerCfg subgroup/setting does not exist on this
/// machine (ValidateExistence), and drops hardware-controlled settings
/// (CheckForHardwareControl: Min=0/Max=0 means the setting is not user-writable).
///
/// Ported from Winhance's PowerSettingsValidationService with one deliberate
/// deviation: Winhance WRITES the EnablementRegistrySetting (Attributes) to try to
/// reveal hidden-but-valid settings during this read-side pass. Akari never writes
/// from a read path (CLAUDE.md architectural rule), so hidden settings are simply
/// filtered out. isleap can revisit auto-reveal as a write-side action if needed.
/// </summary>
public sealed class PowerSettingsValidationService(
    IAkariLogService logService,
    IPowerSettingsQueryService powerSettingsQueryService) : IPowerSettingsValidationService
{
    public async Task<IEnumerable<SettingDefinition>> FilterSettingsByExistenceAsync(IEnumerable<SettingDefinition> settings)
    {
        var settingsList = settings.ToList();
        var originalCount = settingsList.Count;

        var bulkPowerValues = await powerSettingsQueryService.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT").ConfigureAwait(false);

        if (!bulkPowerValues.Any())
        {
            logService.Log(LogLevel.Warning, "Could not get bulk power settings, skipping validation");
            return settingsList;
        }

        var validatedSettings = new List<SettingDefinition>();

        foreach (var setting in settingsList)
        {
            if (!setting.ValidateExistence || setting.PowerCfgSettings?.Any() != true)
            {
                validatedSettings.Add(setting);
                continue;
            }

            var hasValidPowerCfgSetting = false;

            foreach (var powerCfgSetting in setting.PowerCfgSettings)
            {
                var settingKey = powerCfgSetting.SettingGuid;

                if (bulkPowerValues.ContainsKey(settingKey))
                {
                    hasValidPowerCfgSetting = true;
                    break;
                }

                logService.Log(LogLevel.Debug, $"Power setting not found on this system: {settingKey}");
            }

            if (hasValidPowerCfgSetting)
            {
                var shouldFilterOutDueToHardwareControl = false;

                foreach (var powerCfgSetting in setting.PowerCfgSettings.Where(p => p.CheckForHardwareControl))
                {
                    if (await powerSettingsQueryService.IsSettingHardwareControlledAsync(powerCfgSetting).ConfigureAwait(false))
                    {
                        logService.Log(LogLevel.Info,
                            $"Filtering out hardware-controlled setting: {setting.Id} ({powerCfgSetting.SettingGUIDAlias})");
                        shouldFilterOutDueToHardwareControl = true;
                        break;
                    }
                }

                if (!shouldFilterOutDueToHardwareControl)
                {
                    validatedSettings.Add(setting);
                }
            }
        }

        var filteredCount = originalCount - validatedSettings.Count;
        if (filteredCount > 0)
        {
            logService.Log(LogLevel.Debug, $"Filtered out {filteredCount} non-existent power settings");
        }

        return validatedSettings;
    }
}