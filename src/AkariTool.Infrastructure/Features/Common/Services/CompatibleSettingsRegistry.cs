using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Constants;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Winhance 1:1: central registry of every feature's SettingDefinition catalog,
/// pre-filtered once at startup (Windows-version filter always; hardware +
/// power-existence validation for Power). Two views are maintained:
///   filtered  — incompatible rows removed (what pages render)
///   bypassed  — all rows kept but decorated (what backup/export/review need,
///               so config files round-trip settings this machine can't show)
/// </summary>
public class CompatibleSettingsRegistry : ICompatibleSettingsRegistry
{
    private readonly IWindowsCompatibilityFilter _windowsFilter;
    private readonly IHardwareCompatibilityFilter _hardwareFilter;
    private readonly IPowerSettingsValidationService _powerValidation;
    private readonly IAkariLogService? _logService;

    private bool _isInitialized;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly Dictionary<string, IEnumerable<SettingDefinition>> _preFilteredSettings = new();
    private readonly Dictionary<string, IEnumerable<SettingDefinition>> _windowsFilterBypassedSettings = new();
    private Dictionary<string, SettingDefinition> _filteredById = new();
    private Dictionary<string, SettingDefinition> _bypassedById = new();
    private Dictionary<string, string> _filteredSettingIdToFeatureId = new();
    private bool _filterEnabled = true;

    public bool IsInitialized => _isInitialized;

    public CompatibleSettingsRegistry(
        IWindowsCompatibilityFilter windowsFilter,
        IHardwareCompatibilityFilter hardwareFilter,
        IPowerSettingsValidationService powerValidation,
        IAkariLogService? logService = null)
    {
        _windowsFilter = windowsFilter ?? throw new ArgumentNullException(nameof(windowsFilter));
        _hardwareFilter = hardwareFilter ?? throw new ArgumentNullException(nameof(hardwareFilter));
        _powerValidation = powerValidation ?? throw new ArgumentNullException(nameof(powerValidation));
        _logService = logService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isInitialized) return;

            await PreFilterAllFeatureSettingsAsync().ConfigureAwait(false);

            RebuildIdIndexes();
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public SettingDefinition? GetById(string settingId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized. Call InitializeAsync first.");

        var index = _filterEnabled ? _filteredById : _bypassedById;
        return index.TryGetValue(settingId, out var s) ? s : null;
    }

    public string? GetFeatureIdForSetting(string settingId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized. Call InitializeAsync first.");

        return _filteredSettingIdToFeatureId.TryGetValue(settingId, out var f) ? f : null;
    }

    public IEnumerable<SettingDefinition> GetFilteredSettings(string featureId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized");

        return _preFilteredSettings.TryGetValue(featureId, out var settings)
            ? settings
            : Enumerable.Empty<SettingDefinition>();
    }

    public void SetFilterEnabled(bool enabled) => _filterEnabled = enabled;

    public IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllFilteredSettings()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized. Call InitializeAsync first.");

        return _preFilteredSettings;
    }

    public IEnumerable<SettingDefinition> GetBypassedSettings(string featureId)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized");

        return _windowsFilterBypassedSettings.TryGetValue(featureId, out var settings)
            ? settings
            : Enumerable.Empty<SettingDefinition>();
    }

    public IReadOnlyDictionary<string, IEnumerable<SettingDefinition>> GetAllBypassedSettings()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Registry not initialized");

        return _windowsFilterBypassedSettings;
    }

    private void RebuildIdIndexes()
    {
        _filteredById = new Dictionary<string, SettingDefinition>();
        _filteredSettingIdToFeatureId = new Dictionary<string, string>();
        foreach (var (featureId, settings) in _preFilteredSettings)
        {
            foreach (var s in settings)
            {
                if (_filteredSettingIdToFeatureId.TryGetValue(s.Id, out var prevFeature))
                    _logService?.Log(LogLevel.Warning,
                        $"Duplicate setting id '{s.Id}' — previously registered under feature '{prevFeature}', now overwritten by '{featureId}'");
                _filteredById[s.Id] = s;
                _filteredSettingIdToFeatureId[s.Id] = featureId;
            }
        }

        _bypassedById = new Dictionary<string, SettingDefinition>();
        foreach (var (featureId, settings) in _windowsFilterBypassedSettings)
            foreach (var s in settings)
                _bypassedById[s.Id] = s;
    }

    private async Task PreFilterAllFeatureSettingsAsync()
    {
        foreach (var (featureId, provider) in GetKnownFeatureProviders())
        {
            try
            {
                var rawSettings = provider().ToList();

                // Hardware gating first for Power (battery/lid rows), then Windows-version.
                if (featureId == FeatureIds.Power)
                    rawSettings = (await _hardwareFilter.FilterSettingsByHardwareAsync(rawSettings).ConfigureAwait(false)).ToList();

                IEnumerable<SettingDefinition> filteredSettings = rawSettings;
                if (featureId == FeatureIds.Power)
                    filteredSettings = await _powerValidation.FilterSettingsByExistenceAsync(filteredSettings).ConfigureAwait(false);

                _preFilteredSettings[featureId] =
                    featureId == FeatureIds.Power
                        ? _windowsFilter.FilterSettingsByWindowsVersion(filteredSettings)
                        : _windowsFilter.FilterSettingsByWindowsVersion(rawSettings);

                // Bypassed view: keep every row, but let the filter decorate it
                // (applyFilter:false marks unsupported rows instead of removing).
                _windowsFilterBypassedSettings[featureId] =
                    _windowsFilter.FilterSettingsByWindowsVersion(rawSettings, applyFilter: false);
            }
            catch
            {
                _preFilteredSettings[featureId] = Enumerable.Empty<SettingDefinition>();
                _windowsFilterBypassedSettings[featureId] = Enumerable.Empty<SettingDefinition>();
            }
        }
    }

    /// <summary>
    /// Explicit registry of all feature setting providers — direct static method
    /// calls, no reflection. To add a new feature, add a single entry here.
    /// Catalogs live in AkariTool.Core (moved from App for Winhance parity).
    /// </summary>
    private static Dictionary<string, Func<IEnumerable<SettingDefinition>>> GetKnownFeatureProviders() => new()
    {
        // Customize features
        [FeatureIds.Taskbar] = () => Tabs.Customize.TaskbarOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.StartMenu] = () => Tabs.Customize.StartMenuOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.ExplorerCustomization] = () => Tabs.Customize.ExplorerOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.Desktop] = () => Tabs.Customize.DesktopOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.Appearance] = () => Tabs.Customize.AppearanceOptimizations.Build().SelectMany(g => g.Settings),

        // Optimize features
        [FeatureIds.GamingPerformance] = () => Tabs.Gaming.GamingOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.Power] = () => Tabs.Power.PowerOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.Notifications] = () => Tabs.Notifications.NotificationsOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.Privacy] = () => Tabs.Privacy.PrivacyOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.Sound] = () => Tabs.Sound.SoundOptimizations.Build().SelectMany(g => g.Settings),
        [FeatureIds.Update] = () => Tabs.Update.UpdateOptimizations.Build().SelectMany(g => g.Settings),
    };
}
