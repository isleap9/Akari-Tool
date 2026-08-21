using Microsoft.Extensions.DependencyInjection;
using AkariTool.Core.Features.Common.Interfaces;
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
            var backup = services.GetRequiredService<SettingBackupService>();
            foreach (var page in pages)
            {
                page.Build();
                backup.Register(page);
            }

            InitializeNewBadges(services, pages);

            log.Info($"[WARMUP] Setting pages warmed: {pages.Count} page(s).");
        }
        catch (Exception ex)
        {
            log.Error("[WARMUP] Setting-page warm-up failed — some declarative tabs may be unbuilt.", ex);
        }
    }

    /// <summary>
    /// NEW-badge baseline (Winhance port): rows are constructed before the badge
    /// service is initialized, so after Build() we feed it every AddedInVersion
    /// tag and recompute each row's IsNew flag.
    /// </summary>
    private static void InitializeNewBadges(IServiceProvider services, List<SettingPageViewModel> pages)
    {
        var badges = services.GetService<INewBadgeService>();
        if (badges is null)
            return;

        var items = pages.SelectMany(p => p.Sections)
                         .SelectMany(s => s.Items)
                         .OfType<SettingItemViewModel>()
                         .ToList();

        badges.Initialize(items.Select(i => i.Definition.AddedInVersion));

        foreach (var item in items)
            item.IsNew = badges.IsSettingNew(item.Definition.AddedInVersion, item.Definition.Id);
    }
}
