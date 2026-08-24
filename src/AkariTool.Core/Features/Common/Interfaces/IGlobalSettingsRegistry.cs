using System.Collections.Generic;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Winhance IGlobalSettingsRegistry 1:1: manages settings across modules for
/// cross-module lookups (config export/import, review diff, cross-feature
/// dependencies, recommended appliers).
/// </summary>
public interface IGlobalSettingsRegistry
{
    /// <summary>
    /// Registers settings from a module (e.g. "Taskbar", "Power").
    /// </summary>
    void RegisterSettings(string moduleName, IEnumerable<ISettingItem> settings);

    /// <summary>
    /// Gets a setting by ID from any module.
    /// </summary>
    /// <param name="settingId">The ID of the setting</param>
    /// <param name="moduleName">Optional module name to search in. If null, searches all modules.</param>
    /// <returns>The setting if found, null otherwise</returns>
    ISettingItem? GetSetting(string settingId, string? moduleName = null);

    /// <summary>
    /// Gets all settings from all modules.
    /// </summary>
    IEnumerable<ISettingItem> GetAllSettings();

    /// <summary>
    /// Registers a single setting from a module, preserving existing settings.
    /// Used to register settings on-demand during application.
    /// </summary>
    void RegisterSetting(string moduleName, ISettingItem setting);
}
