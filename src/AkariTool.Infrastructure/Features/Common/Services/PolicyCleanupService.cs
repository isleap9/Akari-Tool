using System;
using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Winhance PolicyCleanupService 1:1: deletes Group Policy registry keys that
/// silently override user tweaks (LGPO/AD leftovers beat HKCU settings).
/// Key paths come from every RegistrySetting flagged IsGroupPolicy in the
/// compatible settings registry; parent paths win over children during dedup.
/// </summary>
public class PolicyCleanupService(
    ICompatibleSettingsRegistry compatibleSettingsRegistry,
    IWindowsRegistryService registryService,
    IAkariLogService logService) : IPolicyCleanupService
{
    public int CleanupPolicyKeys()
    {
        var policyKeyPaths = CollectPolicyKeyPaths();

        logService.Log(LogLevel.Info, $"[PolicyCleanup] Found {policyKeyPaths.Count} unique policy key paths to clean up");

        int deletedCount = 0;
        foreach (var keyPath in policyKeyPaths)
        {
            try
            {
                if (registryService.KeyExists(keyPath))
                {
                    if (registryService.DeleteKey(keyPath))
                    {
                        deletedCount++;
                        logService.Log(LogLevel.Info, $"[PolicyCleanup] Deleted policy key: {keyPath}");
                    }
                    else
                    {
                        logService.Log(LogLevel.Warning, $"[PolicyCleanup] Failed to delete policy key: {keyPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[PolicyCleanup] Error deleting policy key '{keyPath}': {ex.Message}");
            }
        }

        logService.Log(LogLevel.Info, $"[PolicyCleanup] Cleanup complete: {deletedCount} policy keys deleted");
        return deletedCount;
    }

    internal HashSet<string> CollectPolicyKeyPaths()
    {
        var policyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allSettings = compatibleSettingsRegistry.GetAllBypassedSettings();

        foreach (var featureSettings in allSettings.Values)
        {
            foreach (var setting in featureSettings)
            {
                if (setting.RegistrySettings == null)
                    continue;

                foreach (var regSetting in setting.RegistrySettings)
                {
                    if (!regSetting.IsGroupPolicy || string.IsNullOrEmpty(regSetting.KeyPath))
                        continue;

                    policyPaths.Add(regSetting.KeyPath);
                }
            }
        }

        // Deduplicate: keep the shortest parent path; drop any path nested under it.
        var deduplicatedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in policyPaths.OrderBy(p => p.Length))
        {
            bool isChildOfExisting = deduplicatedPaths.Any(existing =>
                path.StartsWith(existing + @"\", StringComparison.OrdinalIgnoreCase));

            if (!isChildOfExisting)
            {
                deduplicatedPaths.Add(path);
            }
        }

        return deduplicatedPaths;
    }
}
