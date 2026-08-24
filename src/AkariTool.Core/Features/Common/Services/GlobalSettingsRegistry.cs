using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;

namespace AkariTool.Core.Features.Common.Services;

/// <summary>
/// Winhance GlobalSettingsRegistry 1:1: thread-safe module → settings store.
/// Lives in Core (no OS dependencies) exactly like Winhance's.
/// </summary>
public class GlobalSettingsRegistry : IGlobalSettingsRegistry
{
    private readonly ConcurrentDictionary<string, List<ISettingItem>> _moduleSettings = new();
    private readonly object _listLock = new();

    public void RegisterSettings(string moduleName, IEnumerable<ISettingItem> settings)
    {
        if (string.IsNullOrEmpty(moduleName))
            return;

        var settingsList = settings?.ToList() ?? [];
        _moduleSettings.AddOrUpdate(moduleName, settingsList, (_, _) => settingsList);
    }

    public ISettingItem? GetSetting(string settingId, string? moduleName = null)
    {
        if (string.IsNullOrEmpty(settingId))
            return null;

        if (!string.IsNullOrEmpty(moduleName))
        {
            if (_moduleSettings.TryGetValue(moduleName, out var moduleList))
            {
                lock (_listLock)
                {
                    return moduleList.FirstOrDefault(s => s.Id == settingId);
                }
            }
            return null;
        }

        foreach (var kvp in _moduleSettings)
        {
            lock (_listLock)
            {
                var found = kvp.Value.FirstOrDefault(s => s.Id == settingId);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    public IEnumerable<ISettingItem> GetAllSettings()
    {
        lock (_listLock)
        {
            return _moduleSettings.Values.SelectMany(s => s).ToList();
        }
    }

    public void RegisterSetting(string moduleName, ISettingItem setting)
    {
        if (string.IsNullOrEmpty(moduleName) || setting == null)
            return;

        lock (_listLock)
        {
            _moduleSettings.AddOrUpdate(
                moduleName,
                [setting],
                (_, existing) =>
                {
                    if (!existing.Any(s => s.Id == setting.Id))
                        existing.Add(setting);
                    return existing;
                });
        }
    }
}
