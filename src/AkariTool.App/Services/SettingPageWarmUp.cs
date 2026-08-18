using Microsoft.Extensions.DependencyInjection;
using AkariTool.ViewModels.Tweaks;
using WinUI.Framework.Services;

namespace AkariTool.Services;

/// <summary>
/// Startup warm-up for the declarative SettingDefinition pages (Track A Phase 4).
///
/// Parallel to <see cref="TweakRegistryWarmUp"/> but for pages built on
/// <see cref="SettingPageViewModel"/>. These pages do NOT participate in
/// TweakRegistry (no Backup/search range attribution yet), so this simply calls
/// <c>Build()</c> once on each so a never-navigated tab is still populated.
/// </summary>
public static class SettingPageWarmUp
{
    public static void Run(IServiceProvider services, ILogService log)
    {
        try
        {
            var pages = services.GetServices<SettingPageViewModel>().ToList();
            foreach (var page in pages)
                page.Build();

            log.Info($"[WARMUP] Setting pages warmed: {pages.Count} page(s).");
        }
        catch (Exception ex)
        {
            log.Error("[WARMUP] Setting-page warm-up failed — some declarative tabs may be unbuilt.", ex);
        }
    }
}
