using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Utilities;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Builds and resolves the Power Plan combo box options. Ported 1:1 from
/// Winhance's PowerPlanComboBoxService (the UI-facing
/// <c>SetupPowerPlanComboBoxAsync</c> helper is omitted — Akari's SettingItemViewModel
/// consumes <see cref="GetPowerPlanOptionsAsync"/> and
/// <see cref="ResolveIndexFromRawValuesAsync"/> directly).
/// </summary>
public sealed class PowerPlanComboBoxService(
    IPowerSettingsQueryService powerSettingsQueryService,
    IAkariLogService logService) : IPowerPlanComboBoxService
{
    public async Task<List<PowerPlanComboBoxOption>> GetPowerPlanOptionsAsync()
    {
        logService.Log(LogLevel.Info, "[PowerPlanComboBoxService] Starting power plan options discovery");

        var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        var options = new List<PowerPlanComboBoxOption>();
        var processedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Processing {PowerPlanDefinitions.BuiltInPowerPlans.Count} predefined plans against {systemPlans.Count} system plans");

        int matchedPredefinedCount = 0;
        foreach (var predefinedPlan in PowerPlanDefinitions.BuiltInPowerPlans)
        {
            var matchingSystemPlan = systemPlans.FirstOrDefault(sp =>
                string.Equals(sp.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

            string matchMethod = "GUID";
            if (matchingSystemPlan == null)
            {
                if (predefinedPlan.Name == "Ultimate Performance")
                {
                    matchingSystemPlan = systemPlans.FirstOrDefault(sp => PowerPlanHelper.IsUltimatePerformancePlan(sp.Name));
                    matchMethod = "Ultimate Performance detection";
                }
                else
                {
                    matchingSystemPlan = systemPlans.FirstOrDefault(sp =>
                        string.Equals(PowerPlanHelper.CleanPlanName(sp.Name), predefinedPlan.Name, StringComparison.OrdinalIgnoreCase));
                    matchMethod = "name";
                }
            }

            if (matchingSystemPlan != null)
            {
                options.Add(new PowerPlanComboBoxOption
                {
                    DisplayName = predefinedPlan.LocalizationKey,
                    PredefinedPlan = predefinedPlan,
                    SystemPlan = matchingSystemPlan,
                    ExistsOnSystem = true,
                    IsActive = matchingSystemPlan.IsActive,
                    Index = options.Count
                });

                processedGuids.Add(matchingSystemPlan.Guid);
                processedNames.Add(PowerPlanHelper.CleanPlanName(matchingSystemPlan.Name));
                matchedPredefinedCount++;

                logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService]   ✓ Matched predefined '{predefinedPlan.Name}' with system '{PowerPlanHelper.CleanPlanName(matchingSystemPlan.Name)}' by {matchMethod}{(matchingSystemPlan.IsActive ? " *ACTIVE*" : "")}");
            }
            else
            {
                options.Add(new PowerPlanComboBoxOption
                {
                    DisplayName = predefinedPlan.LocalizationKey,
                    PredefinedPlan = predefinedPlan,
                    SystemPlan = null,
                    ExistsOnSystem = false,
                    IsActive = false,
                    Index = options.Count
                });

                logService.Log(LogLevel.Warning, $"[PowerPlanComboBoxService]   ✗ Predefined plan '{predefinedPlan.Name}' not found on system");
            }
        }

        var unmatchedSystemPlans = systemPlans.Where(sp =>
            !processedGuids.Contains(sp.Guid) &&
            !processedNames.Contains(PowerPlanHelper.CleanPlanName(sp.Name))).ToList();

        foreach (var systemPlan in unmatchedSystemPlans)
        {
            options.Add(new PowerPlanComboBoxOption
            {
                DisplayName = PowerPlanHelper.CleanPlanName(systemPlan.Name),
                PredefinedPlan = null,
                SystemPlan = systemPlan,
                ExistsOnSystem = true,
                IsActive = systemPlan.IsActive,
                Index = options.Count
            });

            logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService]   + Added unmatched system plan '{PowerPlanHelper.CleanPlanName(systemPlan.Name)}'{(systemPlan.IsActive ? " *ACTIVE*" : "")}");
        }

        var sortedOptions = options.OrderBy(o => o.DisplayName)
                                 .Select((o, index) => o with { Index = index })
                                 .ToList();

        logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Completed: {matchedPredefinedCount}/{PowerPlanDefinitions.BuiltInPowerPlans.Count} predefined matched, {unmatchedSystemPlans.Count} additional system plans, {sortedOptions.Count} total options");

        logService.Log(LogLevel.Debug, "[PowerPlanComboBoxService] Final sorted options:");
        for (int i = 0; i < sortedOptions.Count; i++)
        {
            var opt = sortedOptions[i];
            var status = opt.IsActive ? " *ACTIVE*" : "";
            var source = opt.PredefinedPlan != null ? " (predefined)" : " (system-only)";
            logService.Log(LogLevel.Debug, $"[PowerPlanComboBoxService]   {i}: {opt.DisplayName}{status}{source}");
        }

        return sortedOptions;
    }

    public async Task<int> ResolveIndexFromRawValuesAsync(SettingDefinition setting, Dictionary<string, object?> rawValues)
    {
        try
        {
            var options = await GetPowerPlanOptionsAsync().ConfigureAwait(false);

            // 1. Try to match by GUID (Best, independent of language)
            if (rawValues.TryGetValue("ActivePowerPlanGuid", out var activeGuidObj) && activeGuidObj != null)
            {
                var activeGuid = activeGuidObj.ToString();
                for (int i = 0; i < options.Count; i++)
                {
                    var opt = options[i];
                    var optGuid = opt.SystemPlan?.Guid ?? opt.PredefinedPlan?.Guid;

                    if (!string.IsNullOrEmpty(optGuid) &&
                        string.Equals(optGuid, activeGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            // 2. Fallback: Match by Name (Legacy/Unreliable if localized)
            if (rawValues.TryGetValue("ActivePowerPlan", out var activePlanName) && activePlanName != null)
            {
                var activeNameStr = activePlanName.ToString();

                for (int i = 0; i < options.Count; i++)
                {
                    // Check against DisplayName (LocalizationKey) or SystemPlan.Name
                    var displayMatch = string.Equals(PowerPlanHelper.CleanPlanName(options[i].DisplayName), activeNameStr, StringComparison.OrdinalIgnoreCase);
                    var systemMatch = options[i].SystemPlan != null &&
                                    string.Equals(PowerPlanHelper.CleanPlanName(options[i].SystemPlan!.Name), activeNameStr, StringComparison.OrdinalIgnoreCase);

                    if (displayMatch || systemMatch)
                    {
                        return i;
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Failed to resolve power plan index from raw values: {ex.Message}");
            return 0;
        }
    }

    public async Task<PowerPlanResolutionResult> ResolvePowerPlanByIndexAsync(int index)
    {
        try
        {
            logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Resolving power plan index {index} to GUID");

            var options = await GetPowerPlanOptionsAsync().ConfigureAwait(false);

            if (index < 0 || index >= options.Count)
            {
                var errorMsg = $"Invalid power plan index: {index} (available: 0-{options.Count - 1})";
                logService.Log(LogLevel.Error, $"[PowerPlanComboBoxService] {errorMsg}");
                return new PowerPlanResolutionResult { Success = false, ErrorMessage = errorMsg };
            }

            var selectedOption = options[index];
            var powerPlanGuid = selectedOption.SystemPlan?.Guid ?? selectedOption.PredefinedPlan?.Guid;

            if (string.IsNullOrEmpty(powerPlanGuid))
            {
                var errorMsg = $"Could not resolve GUID for power plan at index {index}";
                logService.Log(LogLevel.Error, $"[PowerPlanComboBoxService] {errorMsg}");
                return new PowerPlanResolutionResult { Success = false, ErrorMessage = errorMsg };
            }

            logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Resolved index {index} to: {selectedOption.DisplayName} ({powerPlanGuid})");

            return new PowerPlanResolutionResult
            {
                Success = true,
                Guid = powerPlanGuid,
                DisplayName = selectedOption.DisplayName
            };
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[PowerPlanComboBoxService] Error resolving power plan index {index}: {ex.Message}");
            return new PowerPlanResolutionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public void InvalidateCache()
    {
        powerSettingsQueryService.InvalidateCache();
    }
}