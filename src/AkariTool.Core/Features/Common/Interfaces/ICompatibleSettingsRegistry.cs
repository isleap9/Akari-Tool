using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Winhance ICompatibleSettingsRegistry 1:1: central registry of every feature's
/// SettingDefinition catalog, pre-filtered once at startup. Two views are exposed:
/// filtered (incompatible rows removed — what pages render) and bypassed (all rows
/// kept but decorated — what backup/export/review need so config files round-trip
/// settings this machine can't show).
/// </summary>
public interface ICompatibleSettingsRegistry
{
    Task InitializeAsync();
    IEnumerable<SettingDefinition> GetFilteredSettings(string featureId);
    IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllFilteredSettings();
    IEnumerable<SettingDefinition> GetBypassedSettings(string featureId);
    IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllBypassedSettings();
    void SetFilterEnabled(bool enabled);
    bool IsInitialized { get; }

    /// <summary>
    /// Returns the SettingDefinition for the given id, or null if not registered.
    /// Respects the current filter mode (filtered vs bypassed).
    /// </summary>
    SettingDefinition? GetById(string settingId);

    /// <summary>
    /// Returns the feature id (e.g. "power") that owns the given setting,
    /// or null if not registered.
    /// </summary>
    string? GetFeatureIdForSetting(string settingId);
}
