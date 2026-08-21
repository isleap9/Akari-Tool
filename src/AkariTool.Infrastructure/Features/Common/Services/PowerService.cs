using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Native;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Utilities;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Plan-level power operations. Ported from Winhance's PowerService (Optimize),
/// trimmed to the plan lifecycle — the event-bus publish and config-import gating
/// are Session C concerns. The recommended-value apply to a freshly created Akari
/// Power Plan runs against a catalog passed in by the caller (the App layer owns
/// the catalog; Infrastructure cannot reference it).
/// </summary>
public sealed class PowerService(
    IPowerSettingsQueryService powerSettingsQueryService,
    IPowerSchemeOperations powerSchemeOperations,
    IProcessExecutor processExecutor,
    IAkariLogService logService) : IPowerService
{
    public async Task<PowerPlan?> GetActivePowerPlanAsync()
    {
        try
        {
            return await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting active power plan: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<PowerPlan>> GetAvailablePowerPlansAsync()
    {
        try
        {
            return await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error getting available power plans: {ex.Message}");
            return Array.Empty<PowerPlan>();
        }
    }

    public async Task<bool> ActivatePowerPlanAsync(string powerPlanGuid)
    {
        try
        {
            var currentActivePlan = await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
            if (currentActivePlan != null && string.Equals(currentActivePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Info, $"Power plan {powerPlanGuid} is already active, skipping application");
                return true;
            }

            var schemeGuid = Guid.Parse(powerPlanGuid);
            var result = powerSchemeOperations.SetActiveScheme(schemeGuid);

            if (result == PowerProf.ERROR_SUCCESS)
            {
                powerSettingsQueryService.InvalidateCache();
                return true;
            }

            logService.Log(LogLevel.Warning, $"PowerSetActiveScheme failed with code {result}");
            return false;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Error setting active power plan: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeletePowerPlanAsync(string powerPlanGuid)
    {
        try
        {
            logService.Log(LogLevel.Info, $"Attempting to delete power plan: {powerPlanGuid}");

            var activePlan = await GetActivePowerPlanAsync().ConfigureAwait(false);
            if (activePlan != null && string.Equals(activePlan.Guid, powerPlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Warning, "Cannot delete active power plan");
                return false;
            }

            var schemeGuid = Guid.Parse(powerPlanGuid);
            var result = powerSchemeOperations.DeleteScheme(schemeGuid);

            if (result == PowerProf.ERROR_SUCCESS)
            {
                powerSettingsQueryService.InvalidateCache();
                logService.Log(LogLevel.Info, $"Successfully deleted power plan: {powerPlanGuid}");
                return true;
            }
            else
            {
                logService.Log(LogLevel.Error, $"Failed to delete power plan: {powerPlanGuid}. Error code: {result}");
                return false;
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error deleting power plan: {ex.Message}");
            return false;
        }
    }

    public async Task<PowerPlanImportResult> ImportPowerPlanAsync(PredefinedPowerPlan predefinedPlan, IReadOnlyList<SettingDefinition>? powerCatalog = null)
    {
        try
        {
            if (PowerPlanDefinitions.IsAkariPowerPlan(predefinedPlan.Guid))
            {
                return await CreateAkariPowerPlanAsync(predefinedPlan, powerCatalog).ConfigureAwait(false);
            }

            if (predefinedPlan.Name == "Ultimate Performance")
            {
                var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                var existingPlan = systemPlans.FirstOrDefault(p => PowerPlanHelper.IsUltimatePerformancePlan(p.Name));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Ultimate Performance plan already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                var sourceGuid = Guid.Parse(predefinedPlan.Guid);
                var dupResult = powerSchemeOperations.DuplicateScheme(sourceGuid, out var newGuid);

                if (dupResult == PowerProf.ERROR_SUCCESS)
                {
                    powerSettingsQueryService.InvalidateCache();

                    var actualGuid = newGuid.ToString("D");

                    if (!string.IsNullOrEmpty(actualGuid))
                    {
                        SetPowerPlanNameAndDescription(newGuid, predefinedPlan.Name, predefinedPlan.Description);
                        return new PowerPlanImportResult(true, actualGuid);
                    }
                }

                return new PowerPlanImportResult(false, "", "Ultimate Performance creation failed");
            }
            else
            {
                var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
                var existingPlan = systemPlans.FirstOrDefault(p =>
                    string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

                if (existingPlan != null)
                {
                    logService.Log(LogLevel.Info, $"Power plan '{predefinedPlan.Name}' already exists with GUID: {existingPlan.Guid}");
                    return new PowerPlanImportResult(true, existingPlan.Guid);
                }

                logService.Log(LogLevel.Info, $"Attempting to duplicate power plan '{predefinedPlan.Name}' using GUID {predefinedPlan.Guid}");
                var srcGuid = Guid.Parse(predefinedPlan.Guid);
                var duplicateResult = powerSchemeOperations.DuplicateScheme(srcGuid, out var dupNewGuid);

                if (duplicateResult == PowerProf.ERROR_SUCCESS)
                {
                    powerSettingsQueryService.InvalidateCache();

                    var actualGuid = dupNewGuid.ToString("D");

                    if (!string.IsNullOrEmpty(actualGuid))
                    {
                        logService.Log(LogLevel.Info, $"Successfully duplicated power plan '{predefinedPlan.Name}' with GUID: {actualGuid}");
                        return new PowerPlanImportResult(true, actualGuid);
                    }
                }

                return new PowerPlanImportResult(false, "", $"Failed to duplicate plan '{predefinedPlan.Name}'");
            }
        }
        catch (Exception ex)
        {
            return new PowerPlanImportResult(false, "", ex.Message);
        }
    }

    private async Task<PowerPlanImportResult> CreateAkariPowerPlanAsync(PredefinedPowerPlan predefinedPlan, IReadOnlyList<SettingDefinition>? powerCatalog)
    {
        var ultimatePerformancePlan = PowerPlanDefinitions.BuiltInPowerPlans
            .FirstOrDefault(p => p.Name == "Ultimate Performance");

        if (ultimatePerformancePlan == null)
        {
            return new PowerPlanImportResult(false, "", "Ultimate Performance plan not found");
        }

        try
        {
            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var existingPlan = systemPlans.FirstOrDefault(p =>
                string.Equals(p.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

            // Check if plan exists AND is valid (not a ghost/corrupt entry)
            if (existingPlan != null &&
                string.Equals(existingPlan.Name?.Trim(), "Akari Power Plan", StringComparison.OrdinalIgnoreCase))
            {
                logService.Log(LogLevel.Info, $"Akari Power Plan already exists with GUID: {existingPlan.Guid}");
                return new PowerPlanImportResult(true, existingPlan.Guid);
            }

            // Clean up any ghost/corrupt plan entry (visible or invisible to enumeration)
            // that may block duplication with this GUID
            var akariGuid = Guid.Parse(predefinedPlan.Guid);
            var cleanupResult = powerSchemeOperations.DeleteScheme(akariGuid);
            if (cleanupResult == PowerProf.ERROR_SUCCESS)
            {
                logService.Log(LogLevel.Info, existingPlan != null
                    ? $"[PowerService] Deleted corrupt Akari plan (name was: '{existingPlan.Name}')"
                    : "[PowerService] Cleaned up ghost Akari power plan entry");
                powerSettingsQueryService.InvalidateCache();
            }

            logService.Log(LogLevel.Info, "Creating Akari Power Plan from Ultimate Performance");

            // Use powercfg for specific-GUID duplication (P/Invoke doesn't support destination GUID)
            var dupSuccess = await RunPowercfgAsync($"/duplicatescheme {ultimatePerformancePlan.Guid} {predefinedPlan.Guid}").ConfigureAwait(false);

            if (!dupSuccess)
            {
                logService.Log(LogLevel.Error, "Failed to duplicate plan for Akari Power Plan");
                return new PowerPlanImportResult(false, "", "Failed to create plan");
            }

            // Akari's ProcessResult does not capture standard output, so the actual GUID
            // is taken to be the requested one (powercfg honors an explicit destination GUID).
            var actualGuid = predefinedPlan.Guid;

            SetPowerPlanNameAndDescription(Guid.Parse(actualGuid), predefinedPlan.Name, predefinedPlan.Description);

            if (powerCatalog is { Count: > 0 })
            {
                await ApplyRecommendedSettingsToPlanAsync(actualGuid, powerCatalog).ConfigureAwait(false);
            }

            powerSettingsQueryService.InvalidateCache();

            logService.Log(LogLevel.Info, $"Successfully created Akari Power Plan: {actualGuid}");
            return new PowerPlanImportResult(true, actualGuid);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error creating Akari Power Plan: {ex.Message}");
            return new PowerPlanImportResult(false, "", ex.Message);
        }
    }

    private async Task ApplyRecommendedSettingsToPlanAsync(string planGuid, IReadOnlyList<SettingDefinition> allSettings)
    {
        logService.Log(LogLevel.Info, $"Applying recommended settings to plan: {planGuid}");

        try
        {
            int appliedCount = 0;

            foreach (var setting in allSettings)
            {
                try
                {
                    var powerCfgWithRecommended = setting.PowerCfgSettings?.FirstOrDefault(ps =>
                        ps.RecommendedValueAC.HasValue || ps.RecommendedValueDC.HasValue);

                    if (powerCfgWithRecommended != null)
                    {
                        var acValue = powerCfgWithRecommended.RecommendedValueAC ?? powerCfgWithRecommended.RecommendedValueDC ?? 0;
                        var dcValue = powerCfgWithRecommended.RecommendedValueDC ?? powerCfgWithRecommended.RecommendedValueAC ?? 0;

                        logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {acValue}, DC: {dcValue}");

                        var planSchemeGuid = Guid.Parse(planGuid);
                        var subgroupGuid = Guid.Parse(powerCfgWithRecommended.SubgroupGuid);
                        var settGuid = Guid.Parse(powerCfgWithRecommended.SettingGuid);

                        PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)acValue);
                        PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref planSchemeGuid, ref subgroupGuid, ref settGuid, (uint)dcValue);

                        appliedCount++;
                        continue;
                    }

                    if (setting.InputType == InputType.Selection &&
                        setting.Recommendation?.RecommendedOptionAC != null &&
                        setting.PowerCfgSettings?.Any() == true)
                    {
                        var recommendedOptionAC = setting.Recommendation.RecommendedOptionAC;
                        var recommendedOptionDC = setting.Recommendation.RecommendedOptionDC ?? recommendedOptionAC;

                        var options = setting.ComboBox?.Options;

                        if (options != null)
                        {
                            var indexAC = -1;
                            var indexDC = -1;
                            for (int oi = 0; oi < options.Count; oi++)
                            {
                                if (indexAC < 0 && string.Equals(options[oi].DisplayName, recommendedOptionAC, StringComparison.Ordinal))
                                    indexAC = oi;
                                if (indexDC < 0 && string.Equals(options[oi].DisplayName, recommendedOptionDC, StringComparison.Ordinal))
                                    indexDC = oi;
                            }

                            if (options.Any(o => o.ValueMappings != null))
                            {
                                int? acValue = null, dcValue = null;

                                if (indexAC >= 0 && options[indexAC].ValueMappings is { } valueDictAC &&
                                    valueDictAC.TryGetValue("PowerCfgValue", out var powerCfgValueAC) && powerCfgValueAC != null)
                                    acValue = Convert.ToInt32(powerCfgValueAC);

                                if (indexDC >= 0 && options[indexDC].ValueMappings is { } valueDictDC &&
                                    valueDictDC.TryGetValue("PowerCfgValue", out var powerCfgValueDC) && powerCfgValueDC != null)
                                    dcValue = Convert.ToInt32(powerCfgValueDC);

                                if (acValue.HasValue && dcValue.HasValue)
                                {
                                    var powerCfgSetting = setting.PowerCfgSettings[0];

                                    logService.Log(LogLevel.Debug, $"Applying {setting.Id} - AC: {recommendedOptionAC} ({acValue}), DC: {recommendedOptionDC} ({dcValue})");

                                    var recPlanGuid = Guid.Parse(planGuid);
                                    var recSubGuid = Guid.Parse(powerCfgSetting.SubgroupGuid);
                                    var recSettGuid = Guid.Parse(powerCfgSetting.SettingGuid);

                                    PowerProf.PowerWriteACValueIndex(IntPtr.Zero, ref recPlanGuid, ref recSubGuid, ref recSettGuid, (uint)acValue.Value);
                                    PowerProf.PowerWriteDCValueIndex(IntPtr.Zero, ref recPlanGuid, ref recSubGuid, ref recSettGuid, (uint)dcValue.Value);

                                    appliedCount++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to apply recommended setting '{setting.Id}': {ex.Message}");
                }
            }

            logService.Log(LogLevel.Info, $"Applied {appliedCount} PowerCfg settings to Akari Power Plan");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error applying recommended settings: {ex.Message}");
        }
    }

    private void SetPowerPlanNameAndDescription(Guid schemeGuid, string name, string description)
    {
        powerSchemeOperations.WriteFriendlyName(schemeGuid, name);

        if (!string.IsNullOrEmpty(description))
        {
            powerSchemeOperations.WriteDescription(schemeGuid, description);
        }
    }

    /// <summary>
    /// Runs powercfg for scheme duplication. Akari's ProcessResult only exposes the
    /// exit code, so success is ExitCode == 0.
    /// </summary>
    private async Task<bool> RunPowercfgAsync(string arguments)
    {
        try
        {
            var result = await processExecutor.ExecuteAsync("powercfg", arguments).ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"powercfg {arguments} failed: {ex.Message}");
            return false;
        }
    }
}