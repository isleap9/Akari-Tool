using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Services;
using WinUI.Framework.Services;

namespace AkariTool.Services;

/// <summary>
/// Winhance StartupOrchestrator 1:1 (phases adapted to Akari):
///   Phase 1 — CompatibleSettingsRegistry.InitializeAsync: pre-filter every
///             feature catalog once (Windows/hardware/power gating).
///   Phase 2 — GlobalSettingsPreloader.PreloadAllSettingsAsync: register every
///             bypassed setting into the global registry for cross-feature use.
///   Phase 3 — SettingPageWarmUp.Run: build each declarative page VM on a
///             background thread (existing behaviour preserved).
///
/// Extracted from App.OnLaunched for testability, matching Winhance's structure.
/// </summary>
public sealed class StartupOrchestrator(
    ICompatibleSettingsRegistry compatibleSettingsRegistry,
    IGlobalSettingsPreloader preloader)
{
    public async Task RunAsync(IServiceProvider services, ILogService log)
    {
        // Phase 1 — compatible settings registry.
        try
        {
            await compatibleSettingsRegistry.InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Error($"[STARTUP] CompatibleSettingsRegistry init failed: {ex.Message}", ex);
        }

        // Phase 2 — global registry preload.
        try
        {
            await preloader.PreloadAllSettingsAsync().ConfigureAwait(false);
            log.Info("[STARTUP] Global settings preload complete.");
        }
        catch (Exception ex)
        {
            log.Error($"[STARTUP] Global settings preload failed: {ex.Message}", ex);
        }

        // Phase 3 — page warm-up (background thread; existing Track A behaviour).
        try
        {
            await Task.Run(() => SettingPageWarmUp.Run(services, log)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Error($"[STARTUP] Setting-page warm-up failed: {ex.Message}", ex);
        }
    }
}
