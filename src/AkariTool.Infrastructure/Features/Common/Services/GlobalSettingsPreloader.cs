using System;
using System.Linq;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Winhance GlobalSettingsPreloader 1:1: at startup, walks every feature in the
/// CompatibleSettingsRegistry and registers its bypassed (unfiltered) settings
/// into the GlobalSettingsRegistry, so cross-feature lookups and config export
/// work without any tab having been opened.
/// </summary>
public class GlobalSettingsPreloader : IGlobalSettingsPreloader
{
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly IGlobalSettingsRegistry _globalSettingsRegistry;
    private readonly IAkariLogService? _logService;
    private bool _isPreloaded;

    public bool IsPreloaded => _isPreloaded;

    public GlobalSettingsPreloader(
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IGlobalSettingsRegistry globalSettingsRegistry,
        IAkariLogService? logService = null)
    {
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _globalSettingsRegistry = globalSettingsRegistry;
        _logService = logService;
    }

    public async Task PreloadAllSettingsAsync()
    {
        if (_isPreloaded)
            return;

        if (!_compatibleSettingsRegistry.IsInitialized)
            await _compatibleSettingsRegistry.InitializeAsync().ConfigureAwait(false);

        var allBypassedSettings = _compatibleSettingsRegistry.GetAllBypassedSettings();

        foreach (var (featureId, settings) in allBypassedSettings)
        {
            try
            {
                _globalSettingsRegistry.RegisterSettings(featureId, settings.ToList());
            }
            catch (Exception ex)
            {
                _logService?.Log(LogLevel.Warning,
                    $"[Preloader] Failed to preload settings from {featureId}: {ex.Message}");
            }
        }

        _isPreloaded = true;
    }
}
