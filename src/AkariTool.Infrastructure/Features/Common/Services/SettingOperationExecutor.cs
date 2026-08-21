using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Native;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Utilities;

namespace AkariTool.Infrastructure.Features.Common.Services;

public class SettingOperationExecutor(
    IWindowsRegistryService registryService,
    IComboBoxResolver comboBoxResolver,
    IProcessRestartManager processRestartManager,
    IPowerCfgApplier powerCfgApplier,
    IScheduledTaskService scheduledTaskService,
    IProcessExecutor processExecutor,
    IPowerShellRunner powerShellRunner,
    IFileSystemService fileSystemService,
    IAkariLogService logService,
    ISpecialSettingHandlerRegistry specialHandlerRegistry) : ISettingOperationExecutor
{
    private readonly ISpecialSettingHandlerRegistry _specialHandlerRegistry = specialHandlerRegistry;

    public async Task<OperationResult> ApplySettingOperationsAsync(SettingDefinition setting, bool enable, object? value, bool resetToDefault = false)
    {
        logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Processing operations for '{setting.Id}' - Type: {setting.InputType}{(resetToDefault ? " (resetting to default values)" : "")}");

        // Special-setting handlers own their full apply path (services/tasks/DLLs/registry).
        if (_specialHandlerRegistry.TryGet(setting.Id) is { } handler)
        {
            var handled = await handler.TryApplySpecialSettingAsync(setting, value).ConfigureAwait(false);
            if (handled)
            {
                await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);
                return OperationResult.Succeeded();
            }
        }

        bool allSucceeded = true;
        var failedOperations = new List<string>();

        if (setting.RegistrySettings?.Count > 0 && setting.RegContents?.Count == 0)
        {
            if (setting.InputType == InputType.Selection && value is Dictionary<string, object> customValues)
            {
                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with custom state values");

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    var valueName = registrySetting.ValueName ?? "KeyExists";

                    if (customValues.TryGetValue(valueName, out var specificValue))
                    {
                        bool ok = specificValue == null
                            ? registryService.ApplySetting(registrySetting, false)
                            : registryService.ApplySetting(registrySetting, true, specificValue);

                        if (!ok)
                        {
                            allSucceeded = false;
                            failedOperations.Add($"Registry: {registrySetting.KeyPath}\\{valueName}");
                            logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Failed to apply registry setting: {registrySetting.KeyPath}\\{valueName}");
                        }
                    }
                }
            }
            else if (setting.InputType == InputType.Selection && (value is int || (value is string stringValue && !string.IsNullOrEmpty(stringValue))))
            {
                int index = value switch
                {
                    int intValue => intValue,
                    string strValue => comboBoxResolver.GetIndexFromDisplayName(setting, strValue),
                    _ => 0
                };
                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with unified mapping for index: {index}");

                var specificValues = comboBoxResolver.ResolveIndexToRawValues(setting, index);

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    var valueName = registrySetting.ValueName ?? "KeyExists";
                    bool ok;

                    if (specificValues.TryGetValue(valueName, out var specificValue))
                    {
                        ok = specificValue == null
                            ? registryService.ApplySetting(registrySetting, false)
                            : registryService.ApplySetting(registrySetting, true, specificValue);
                    }
                    else
                    {
                        bool applyValue = comboBoxResolver.GetValueFromIndex(setting, index) != 0;
                        ok = registryService.ApplySetting(registrySetting, applyValue);
                    }

                    if (!ok)
                    {
                        allSucceeded = false;
                        failedOperations.Add($"Registry: {registrySetting.KeyPath}\\{valueName}");
                        logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Failed to apply registry setting: {registrySetting.KeyPath}\\{valueName}");
                    }
                }
            }
            else
            {
                bool applyValue = setting.InputType switch
                {
                    InputType.Toggle => enable,
                    InputType.NumericRange when value != null => NumericConversionHelper.ConvertNumericValue(value) != 0,
                    InputType.Selection => enable,
                    // Action settings are one-shot "apply"; the button click always means write EnabledValue.
                    // RunActionAsync hardcodes Enable=true, so this matches that semantic.
                    InputType.Action => enable,
                    _ => throw new NotSupportedException($"Input type '{setting.InputType}' not supported for registry operations")
                };

                logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.RegistrySettings.Count} registry settings for '{setting.Id}' with value: {applyValue}");

                foreach (var registrySetting in setting.RegistrySettings)
                {
                    bool ok;
                    if (resetToDefault && !applyValue)
                    {
                        // Parent cascading disable: write DefaultValue instead of DisabledValue
                        // so the child returns to a clean state without leaving behind policy-level
                        // overrides that could interfere with other independent settings.
                        var parentDisableValue = registrySetting.DisabledValue?.Length > 1 ? registrySetting.DisabledValue[1] : null;
                        logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Parent cascade disable for '{registrySetting.KeyPath}\\{registrySetting.ValueName}' - using DisabledValue[1]: {parentDisableValue?.ToString() ?? "null (delete)"}");
                        ok = registryService.ApplySetting(registrySetting, false, useDefaultValue: true);
                    }
                    else
                    {
                        ok = registryService.ApplySetting(registrySetting, applyValue);
                    }

                    if (!ok)
                    {
                        allSucceeded = false;
                        var valueName = registrySetting.ValueName ?? "KeyExists";
                        failedOperations.Add($"Registry: {registrySetting.KeyPath}\\{valueName}");
                        logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Failed to apply registry setting: {registrySetting.KeyPath}\\{valueName}");
                    }
                }
            }
        }

        if (setting.ScheduledTaskSettings?.Count > 0)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.ScheduledTaskSettings.Count} scheduled task settings for '{setting.Id}'");

            foreach (var taskSetting in setting.ScheduledTaskSettings)
            {
                var taskResult = enable
                    ? await scheduledTaskService.EnableTaskAsync(taskSetting.TaskPath).ConfigureAwait(false)
                    : await scheduledTaskService.DisableTaskAsync(taskSetting.TaskPath).ConfigureAwait(false);

                if (!taskResult.Success)
                {
                    allSucceeded = false;
                    failedOperations.Add($"ScheduledTask: {taskSetting.TaskPath}");
                    logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Failed to {(enable ? "enable" : "disable")} scheduled task: {taskSetting.TaskPath}");
                }
            }
        }

        if (setting.PowerShellScripts?.Count > 0)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Executing {setting.PowerShellScripts.Count} PowerShell scripts for '{setting.Id}'");

            foreach (var scriptSetting in setting.PowerShellScripts)
            {
                // For Selection types with Script on options, resolve which script to run from the selected index
                var useEnabled = enable;
                if (setting.InputType == InputType.Selection
                    && setting.ComboBox?.Options is { } scriptOptions
                    && value is int scriptIndex
                    && scriptIndex >= 0 && scriptIndex < scriptOptions.Count
                    && scriptOptions[scriptIndex].Script is { } scriptOption)
                {
                    // A "None" option applies no script at all (e.g. a Custom /
                    // leave-Windows-settings-alone dropdown choice).
                    if (scriptOption == ScriptOption.None)
                    {
                        continue;
                    }

                    useEnabled = scriptOption == ScriptOption.Enabled;
                }

                var script = useEnabled ? scriptSetting.EnabledScript : scriptSetting.DisabledScript;

                // Substitute ScriptVariables placeholders for the selected index
                if (!string.IsNullOrEmpty(script)
                    && setting.ComboBox?.Options is { } varOptions
                    && value is int varIndex
                    && varIndex >= 0 && varIndex < varOptions.Count
                    && varOptions[varIndex].ScriptVariables is { } variables)
                {
                    foreach (var kvp in variables)
                    {
                        script = script.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
                    }
                }

                if (!string.IsNullOrEmpty(script))
                {
                    await powerShellRunner.RunScriptInMemoryAsync(script).ConfigureAwait(false);
                }
            }
        }

        if (setting.RegContents?.Count > 0)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Importing {setting.RegContents.Count} registry contents for '{setting.Id}'");

            foreach (var regContentSetting in setting.RegContents)
            {
                var regContent = enable ? regContentSetting.EnabledContent : regContentSetting.DisabledContent;

                if (!string.IsNullOrEmpty(regContent))
                {
                    var tempDir = fileSystemService.GetTempPath();

                    var tempFile = fileSystemService.CombinePath(tempDir, $"akaritool_{Guid.NewGuid()}.reg");
                    try
                    {
                        await fileSystemService.WriteAllTextAsync(tempFile, regContent).ConfigureAwait(false);
                        logService.Log(LogLevel.Debug, $"[SettingOperationExecutor] Wrote registry content to temp file: {tempFile}");

                        await RunCommandAsync($"reg import \"{tempFile}\"").ConfigureAwait(false);

                        logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Registry import completed for '{setting.Id}'");
                    }
                    catch (Exception ex)
                    {
                        logService.Log(LogLevel.Error, $"[SettingOperationExecutor] Failed to import registry content for '{setting.Id}': {ex.Message}");
                        throw;
                    }
                    finally
                    {
                        if (fileSystemService.FileExists(tempFile))
                        {
                            fileSystemService.DeleteFile(tempFile);
                        }
                    }
                }
            }
        }

        if (setting.PowerCfgSettings?.Count > 0)
        {
            await powerCfgApplier.ApplyPowerCfgSettingsAsync(setting, enable, value).ConfigureAwait(false);
        }

        if (setting.NativePowerApiSettings?.Count > 0)
        {
            logService.Log(LogLevel.Info, $"[SettingOperationExecutor] Applying {setting.NativePowerApiSettings.Count} native power API settings for '{setting.Id}'");

            foreach (var apiSetting in setting.NativePowerApiSettings)
            {
                byte inputValue = enable ? apiSetting.EnabledValue : apiSetting.DisabledValue;
                var result = PowerProf.CallNtPowerInformation(
                    apiSetting.InformationLevel,
                    ref inputValue,
                    1,
                    IntPtr.Zero,
                    0);

                if (result == 0)
                {
                    logService.Log(LogLevel.Info, $"[SettingOperationExecutor] CallNtPowerInformation(level={apiSetting.InformationLevel}) succeeded for '{setting.Id}'");
                }
                else
                {
                    allSucceeded = false;
                    failedOperations.Add($"NativePowerApi: level={apiSetting.InformationLevel}");
                    logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] CallNtPowerInformation(level={apiSetting.InformationLevel}) failed with status {result} for '{setting.Id}'");
                }
            }
        }

        // AkariOS extension fields — stubbed in Phase 2b; wired to Akari's existing
        // BCD / Playbook / ServicePreset services in Phase 4.
        if (setting.BcdOperations?.Count > 0)
        {
            logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] BcdOperations not yet implemented for '{setting.Id}' - skipping {setting.BcdOperations.Count} operation(s).");
        }

        if (setting.PlaybookActions?.Count > 0)
        {
            logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] PlaybookActions not yet implemented for '{setting.Id}' - skipping {setting.PlaybookActions.Count} action(s).");
        }

        if (setting.ServicePreset != null)
        {
            logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] ServicePreset not yet implemented for '{setting.Id}' - skipping preset '{setting.ServicePreset}'.");
        }

        await processRestartManager.HandleProcessAndServiceRestartsAsync(setting).ConfigureAwait(false);

        if (!allSucceeded)
        {
            var message = $"One or more operations failed for '{setting.Id}': {string.Join(", ", failedOperations)}";
            logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] {message}");
            return OperationResult.Failed(message);
        }

        return OperationResult.Succeeded();
    }

    private async Task RunCommandAsync(string command)
    {
        try
        {
            var result = await processExecutor.ExecuteAsync("cmd.exe", $"/c {command}").ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                logService.Log(LogLevel.Warning, $"[SettingOperationExecutor] Command failed: {command} - {result.StandardError}");
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[SettingOperationExecutor] Command execution failed: {command} - {ex.Message}");
        }
    }
}
