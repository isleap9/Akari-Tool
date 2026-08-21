using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// 1:1 port of Winhance's SettingDependencyResolver (Infrastructure) +
/// DependencyManager (Core.Services). Winhance routes every apply through
/// ISettingApplicationService, which calls HandleValuePrerequisitesAsync +
/// HandleDependenciesAsync before the executor and SyncParentToMatchingPresetAsync
/// after; Akari's SettingItemViewModel apply pipeline mirrors that order.
///
/// Adaptations forced by Akari's thinner infrastructure (behavior preserved):
/// - IGlobalSettingsRegistry lookups → the caller-supplied allSettings universe
///   (pages append cross-catalog catalogs via AdditionalResolutionCatalogs).
/// - ISystemSettingsDiscoveryService state reads → ISettingStateReader.
/// - ApplySettingRequest{SettingId, Enable, Value, ResetToDefault} → direct
///   ISettingOperationExecutor.ApplySettingOperationsAsync on the resolved def.
/// - eventBus.SettingAppliedEvent → the SettingApplied event (UI refresh seam).
/// - Per-dependent catch is widened from ArgumentException("not found") to also
///   cover NotSupportedException: Winhance's PowerCfgApplier throws NSE when a
///   cascade resets a PowerCfg Selection with no value, which aborts the whole
///   parent apply. Akari logs and continues instead of replicating that abort.
/// </summary>
public sealed class SettingDependencyResolver(
    ISettingOperationExecutor executor,
    ISettingStateReader stateReader,
    IProcessRestartManager processRestartManager,
    IAkariLogService logService) : ISettingDependencyResolver
{
    public event Action<string>? SettingApplied;

    public async Task HandleDependenciesAsync(
        string settingId,
        IReadOnlyList<SettingDefinition> allSettings,
        bool enable,
        object? value)
    {
        if (enable)
        {
            var setting = FindSetting(settingId, allSettings);
            var directionalDependencies = setting?.Dependencies?
                .Where(d => d.DependencyType != SettingDependencyType.RequiresValueBeforeAnyChange)
                .ToList();

            if (directionalDependencies?.Any() == true)
            {
                logService.Log(LogLevel.Info, $"[SettingDependencyResolver] Handling dependencies for '{settingId}'");
                var dependencyResult = await HandleSettingEnabledAsync(settingId, allSettings).ConfigureAwait(false);
                if (!dependencyResult)
                    throw new InvalidOperationException($"Cannot enable '{settingId}' due to unsatisfied dependencies");
            }

            // Auto-enable associated settings when this setting is enabled.
            // Suppress process/service restarts during this loop — the parent's own
            // apply will trigger its restart, covering all children in a single restart.
            if (setting?.AutoEnableSettingIds?.Count > 0)
            {
                using (processRestartManager.SuppressRestarts())
                {
                    foreach (var autoEnableId in setting.AutoEnableSettingIds)
                    {
                        try
                        {
                            var autoEnableDef = FindSetting(autoEnableId, allSettings);
                            if (autoEnableDef != null)
                            {
                                // Always apply the auto-enable even if the child is already in the
                                // enabled registry state (e.g. value absent = enabled by default).
                                // This ensures the UI reflects the correct toggle state for children
                                // whose default is "enabled".
                                logService.Log(LogLevel.Info,
                                    $"[SettingDependencyResolver] Auto-enabling '{autoEnableId}' because '{settingId}' was enabled");
                                await ApplyAsync(autoEnableDef, true, null).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            logService.Log(LogLevel.Warning,
                                $"[SettingDependencyResolver] Failed to auto-enable '{autoEnableId}': {ex.Message}");
                        }
                    }
                }
            }
        }
        else
        {
            var hasDependentSettings = allSettings.Any(s => s.Dependencies?.Any(d =>
                d.RequiredSettingId == settingId &&
                d.DependencyType != SettingDependencyType.RequiresValueBeforeAnyChange) == true);
            if (hasDependentSettings)
            {
                logService.Log(LogLevel.Info, $"[SettingDependencyResolver] Handling dependent settings for disabled '{settingId}'");
                await HandleSettingDisabledAsync(settingId, allSettings).ConfigureAwait(false);
            }
        }

        if (enable && value != null)
        {
            await HandleSettingValueChangedAsync(settingId, allSettings).ConfigureAwait(false);
        }
    }

    public async Task<bool> HandleSettingEnabledAsync(string settingId, IReadOnlyList<SettingDefinition> allSettings)
    {
        var setting = FindSetting(settingId, allSettings);
        if (setting?.Dependencies == null || !setting.Dependencies.Any())
            return true;

        bool allSucceeded = true;
        foreach (var dependency in setting.Dependencies)
        {
            var requiredSetting = FindSetting(dependency.RequiredSettingId, allSettings);
            if (requiredSetting == null)
            {
                logService.Log(LogLevel.Error, $"Required dependency '{dependency.RequiredSettingId}' not found for '{settingId}'");
                allSucceeded = false;
                continue;
            }

            if (!await IsDependencySatisfiedAsync(dependency, requiredSetting).ConfigureAwait(false))
            {
                await ApplyDependencyAsync(dependency, requiredSetting).ConfigureAwait(false);
            }
        }

        return allSucceeded;
    }

    public async Task HandleSettingDisabledAsync(string settingId, IReadOnlyList<SettingDefinition> allSettings)
    {
        var dependentSettings = allSettings.Where(s =>
            s.Dependencies?.Any(d =>
                d.RequiredSettingId == settingId &&
                (d.DependencyType == SettingDependencyType.RequiresEnabled ||
                 d.DependencyType == SettingDependencyType.RequiresSpecificValue)) == true);

        foreach (var dependentSetting in dependentSettings)
        {
            if (!IsEnabled(dependentSetting))
                continue;

            try
            {
                logService.Log(LogLevel.Info,
                    $"[DependencyManager] Resetting dependent '{dependentSetting.Id}' to default values (parent '{settingId}' was disabled)");
                await ApplyAsync(dependentSetting, false, null, resetToDefault: true).ConfigureAwait(false);
                await HandleSettingDisabledAsync(dependentSetting.Id, allSettings).ConfigureAwait(false);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("not found"))
            {
                logService.Log(LogLevel.Warning,
                    $"Cannot disable dependent setting '{dependentSetting.Id}' - likely filtered due to OS/hardware compatibility. Skipping.");
            }
            catch (NotSupportedException ex)
            {
                // Winhance parity limitation: its PowerCfgApplier throws NSE when a
                // cascade resets a PowerCfg Selection with no value, aborting the whole
                // parent apply. Akari logs and keeps going so the parent still lands.
                logService.Log(LogLevel.Warning,
                    $"[DependencyManager] Could not cascade-reset '{dependentSetting.Id}' (unsupported input type for reset-to-default): {ex.Message}");
            }
        }
    }

    public async Task HandleSettingValueChangedAsync(string settingId, IReadOnlyList<SettingDefinition> allSettings)
    {
        var dependentSettings = allSettings.Where(s =>
            s.Dependencies?.Any(d =>
                d.RequiredSettingId == settingId &&
                d.DependencyType == SettingDependencyType.RequiresSpecificValue) == true);

        foreach (var dependentSetting in dependentSettings)
        {
            if (!IsEnabled(dependentSetting))
                continue;

            var dependency = dependentSetting.Dependencies!.First(d =>
                d.RequiredSettingId == settingId &&
                d.DependencyType == SettingDependencyType.RequiresSpecificValue);

            var requiredSetting = FindSetting(settingId, allSettings);
            if (requiredSetting == null)
                continue;

            if (!await IsDependencySatisfiedAsync(dependency, requiredSetting).ConfigureAwait(false))
            {
                try
                {
                    logService.Log(LogLevel.Info,
                        $"[DependencyManager] Resetting dependent '{dependentSetting.Id}' to default values (required value for '{settingId}' no longer satisfied)");
                    await ApplyAsync(dependentSetting, false, null, resetToDefault: true).ConfigureAwait(false);
                    await HandleSettingDisabledAsync(dependentSetting.Id, allSettings).ConfigureAwait(false);
                }
                catch (ArgumentException ex) when (ex.Message.Contains("not found"))
                {
                    logService.Log(LogLevel.Warning,
                        $"Cannot disable dependent setting '{dependentSetting.Id}' - likely filtered due to OS/hardware compatibility. Skipping.");
                }
                catch (NotSupportedException ex)
                {
                    logService.Log(LogLevel.Warning,
                        $"[DependencyManager] Could not cascade-reset '{dependentSetting.Id}' (unsupported input type for reset-to-default): {ex.Message}");
                }
            }
        }
    }

    public async Task HandleValuePrerequisitesAsync(
        SettingDefinition setting,
        string settingId,
        IReadOnlyList<SettingDefinition> allSettings)
    {
        if (setting.Dependencies?.Any() != true)
        {
            return;
        }

        var valuePrerequisites = setting.Dependencies
            .Where(d => d.DependencyType == SettingDependencyType.RequiresValueBeforeAnyChange)
            .ToList();

        if (!valuePrerequisites.Any())
        {
            return;
        }

        foreach (var dependency in valuePrerequisites)
        {
            logService.Log(LogLevel.Info,
                $"[ValuePrereq] Processing: '{settingId}' requires '{dependency.RequiredSettingId}' = '{dependency.RequiredValue}'");

            var requiredSetting = FindSetting(dependency.RequiredSettingId, allSettings);

            if (requiredSetting == null)
            {
                logService.Log(LogLevel.Warning,
                    $"[ValuePrereq] Required setting '{dependency.RequiredSettingId}' not found in current module or global registry");
                continue;
            }

            bool requirementMet = DoesCurrentValueMatchRequirement(requiredSetting, dependency.RequiredValue);

            if (!requirementMet)
            {
                logService.Log(LogLevel.Info,
                    $"[ValuePrereq] Auto-fixing '{dependency.RequiredSettingId}' to '{dependency.RequiredValue}' before applying '{settingId}'");

                var valueToApply = GetValueToApplyForRequirement(requiredSetting, dependency.RequiredValue);

                await ApplyAsync(requiredSetting, true, valueToApply).ConfigureAwait(false);

                logService.Log(LogLevel.Info,
                    $"[ValuePrereq] Successfully auto-fixed '{dependency.RequiredSettingId}', proceeding with '{settingId}'");
            }
        }
    }

    public async Task SyncParentToMatchingPresetAsync(
        SettingDefinition setting,
        string settingId,
        IReadOnlyList<SettingDefinition> allSettings)
    {
        var prerequisite = setting.Dependencies?
            .FirstOrDefault(d => d.DependencyType == SettingDependencyType.RequiresValueBeforeAnyChange);

        if (prerequisite == null)
        {
            return;
        }

        var parentSetting = FindSetting(prerequisite.RequiredSettingId, allSettings);
        if (parentSetting?.SettingPresets == null || parentSetting.SettingPresets.Count == 0)
        {
            return;
        }

        var presets = parentSetting.SettingPresets;

        logService.Log(LogLevel.Info,
            $"[PostChange] Checking if child settings now match a preset for parent '{prerequisite.RequiredSettingId}'");

        foreach (var (presetIndex, presetChildren) in presets)
        {
            var allMatch = DoAllChildrenMatchPreset(presetChildren, allSettings);

            if (allMatch)
            {
                logService.Log(LogLevel.Info,
                    $"[PostChange] All children match preset at index {presetIndex}, syncing parent '{prerequisite.RequiredSettingId}'");

                await ApplyAsync(parentSetting, true, presetIndex).ConfigureAwait(false);

                return;
            }
        }

        logService.Log(LogLevel.Debug,
            $"[PostChange] No preset match found for parent '{prerequisite.RequiredSettingId}', leaving at current value");
    }

    private bool DoAllChildrenMatchPreset(
        Dictionary<string, bool> preset,
        IReadOnlyList<SettingDefinition> allSettings)
    {
        var compatiblePresetEntries = new Dictionary<string, bool>();

        foreach (var (childId, expectedValue) in preset)
        {
            var childSetting = FindSetting(childId, allSettings);
            if (childSetting == null)
            {
                // Winhance skips registry-missing children ("likely OS-filtered"); Akari's
                // catalogs are pre-filtered at Build time, so a miss here means the child
                // lives outside the caller's resolution universe.
                logService.Log(LogLevel.Debug,
                    $"[PostChange] Skipping preset child '{childId}' from matching - not registered (likely OS-filtered)");
                continue;
            }

            compatiblePresetEntries[childId] = expectedValue;
        }

        if (compatiblePresetEntries.Count == 0)
            return false;

        foreach (var (childId, expectedValue) in compatiblePresetEntries)
        {
            var child = FindSetting(childId, allSettings)!;
            var actual = IsEnabled(child);
            if (actual != expectedValue)
            {
                logService.Log(LogLevel.Info,
                    $"[PostChange] Child '{childId}' mismatch - Expected: {expectedValue}, Actual: {actual}");
                return false;
            }

            logService.Log(LogLevel.Debug,
                $"[PostChange] Child '{childId}' matches - Value: {actual}");
        }

        return true;
    }

    private SettingDefinition? FindSetting(string settingId, IReadOnlyList<SettingDefinition> allSettings)
    {
        return allSettings.FirstOrDefault(s => s.Id == settingId);
    }

    private bool IsEnabled(SettingDefinition setting)
    {
        try
        {
            return stateReader.ReadToggleState(setting);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning,
                $"[DependencyManager] State read failed for '{setting.Id}': {ex.Message}");
            return false;
        }
    }

    private Task<bool> IsDependencySatisfiedAsync(SettingDependency dependency, SettingDefinition requiredSetting)
    {
        return Task.FromResult(IsDependencySatisfied(dependency, requiredSetting));
    }

    private bool IsDependencySatisfied(SettingDependency dependency, SettingDefinition requiredSetting)
    {
        return dependency.DependencyType switch
        {
            SettingDependencyType.RequiresEnabled => IsEnabled(requiredSetting),
            SettingDependencyType.RequiresDisabled => !IsEnabled(requiredSetting),
            SettingDependencyType.RequiresSpecificValue => DoesCurrentValueMatchRequirement(requiredSetting, dependency.RequiredValue),
            _ => false,
        };
    }

    private async Task ApplyDependencyAsync(SettingDependency dependency, SettingDefinition requiredSetting)
    {
        try
        {
            if (dependency.DependencyType == SettingDependencyType.RequiresSpecificValue)
            {
                if (requiredSetting.InputType == InputType.Selection && !string.IsNullOrEmpty(dependency.RequiredValue))
                {
                    await ApplyAsync(requiredSetting, true, dependency.RequiredValue).ConfigureAwait(false);
                }
                else
                {
                    await ApplyAsync(requiredSetting, true, null).ConfigureAwait(false);
                }
            }
            else
            {
                bool enableValue = dependency.DependencyType == SettingDependencyType.RequiresEnabled;
                await ApplyAsync(requiredSetting, enableValue, null).ConfigureAwait(false);
            }
        }
        catch (ArgumentException ex) when (ex.Message.Contains("not found"))
        {
            logService.Log(LogLevel.Warning,
                $"Cannot apply dependency '{dependency.RequiredSettingId}' - likely filtered due to OS/hardware compatibility. Skipping.");
        }
    }

    /// <summary>Resolver-initiated write; raises <see cref="SettingApplied"/> on success.</summary>
    private async Task ApplyAsync(SettingDefinition setting, bool enable, object? value, bool resetToDefault = false)
    {
        var result = await executor.ApplySettingOperationsAsync(setting, enable, value, resetToDefault).ConfigureAwait(false);
        if (result.Success)
            SettingApplied?.Invoke(setting.Id);
    }

    private bool DoesCurrentValueMatchRequirement(SettingDefinition setting, string? requiredValue)
    {
        if (string.IsNullOrEmpty(requiredValue))
        {
            return true;
        }

        if (setting.InputType == InputType.Selection &&
            setting.ComboBox?.Options is { } options)
        {
            int requiredIndex = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].DisplayName.Equals(requiredValue, StringComparison.OrdinalIgnoreCase))
                {
                    requiredIndex = i;
                    break;
                }
            }

            if (requiredIndex >= 0)
            {
                return stateReader.ReadSelectionIndex(setting) == requiredIndex;
            }
        }

        if (setting.InputType == InputType.Toggle)
        {
            bool requiredBool = requiredValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                               requiredValue.Equals("enabled", StringComparison.OrdinalIgnoreCase);
            bool currentBool = IsEnabled(setting);
            return currentBool == requiredBool;
        }

        return false;
    }

    private object? GetValueToApplyForRequirement(SettingDefinition setting, string? requiredValue)
    {
        if (string.IsNullOrEmpty(requiredValue))
        {
            return null;
        }

        if (setting.InputType == InputType.Selection &&
            setting.ComboBox?.Options is { } options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].DisplayName.Equals(requiredValue, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            logService.Log(LogLevel.Warning,
                $"[ValuePrereq] Could not find ComboBox option matching '{requiredValue}'");
            return null;
        }

        if (setting.InputType == InputType.Toggle)
        {
            return requiredValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   requiredValue.Equals("enabled", StringComparison.OrdinalIgnoreCase);
        }

        return null;
    }
        public async Task ApplyParentPresetsAsync(SettingDefinition setting, object? value, IReadOnlyList<SettingDefinition> allSettings)
        {
            if (setting.SettingPresets == null || setting.InputType != InputType.Selection || value is not int selectedIndex)
                return;
            if (!setting.SettingPresets.TryGetValue(selectedIndex, out var preset))
                return;

            logService.Log(LogLevel.Info, $"[SettingDependencyResolver] Applying preset for '{setting.Id}' at index {selectedIndex} ({preset.Count} children)");

            using (processRestartManager.SuppressRestarts())
            {
                foreach (var (childId, childValue) in preset)
                {
                    var child = FindSetting(childId, allSettings);
                    if (child == null)
                    {
                        logService.Log(LogLevel.Debug, $"[SettingDependencyResolver] Skipping preset child '{childId}' - not in resolution universe");
                        continue;
                    }

                    try
                    {
                        await ApplyAsync(child, childValue, null).ConfigureAwait(false);
                        SettingApplied?.Invoke(childId);
                    }
                    catch (Exception ex)
                    {
                        logService.Log(LogLevel.Warning, $"[SettingDependencyResolver] Preset child '{childId}' failed: {ex.Message}");
                    }
                }
            }
        }
}
