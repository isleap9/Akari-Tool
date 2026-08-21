using Microsoft.Extensions.DependencyInjection;
using AkariTool.Services;
using AkariTool.ViewModels;
using AkariTool.ViewModels.Software;
using AkariTool.ViewModels.Tweaks;
using WinUI.Framework.Services;
using AkariTool.Core.Tweaks;
using AkariTool.Core.Interfaces;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Events;
using AkariTool.Infrastructure.Features.Common.Services;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.DI;

/// <summary>
/// DI registrations for the UI layer: the shell window, view models, and the
/// tweak-page warm-up enumeration.
///
/// Also hosts ToolService, IToolService, and TweakDialogs. ToolService now lives
/// in the Infrastructure project (referenced via ProjectReference); it is wired up
/// here because its construction needs the UI-layer ILogService sink. TweakDialogs
/// still lives in the main app project, so it must be registered here regardless.
/// </summary>
public static class UIServiceExtensions
{
    public static IServiceCollection AddAkariUI(
        this IServiceCollection services)
    {
        // ToolService is the tweak layer's logger + process runner. Its sink feeds
        // the framework ILogService, which the shell's log dock already renders —
        // that wires the HEADLESS EVENT ToolService.LineLogged. ProgressStarted /
        // ProgressStopped are subscribed by MainWindow (status bar).
        services.AddSingleton<ToolService>(sp =>
        {
            var log = sp.GetRequiredService<ILogService>();
            return new ToolService(line => log.Info(line));
        });
        // Same instance resolves for both IToolService and the concrete ToolService.
        services.AddSingleton<IToolService>(sp => sp.GetRequiredService<ToolService>());

        services.AddSingleton<IAkariLogService>(sp =>
        {
            var tool = sp.GetRequiredService<ToolService>();
            return new AkariLogService(line => tool.Log(line));
        });

        // Dialog helper for tweak confirmations (serializes ContentDialogs).
        services.AddSingleton<TweakDialogs>();
        services.AddSingleton<SettingBackupService>();

        // NEW-badge baseline tracking (Winhance port). Impl lives App-side because
        // it needs the vendored ISettingsService; interface is Core.
        services.AddSingleton<INewBadgeService, NewBadgeService>();
        services.AddSingleton<INavBadgeService, NavBadgeService>();
        services.AddSingleton<ITaskProgressService, TaskProgressService>();

                // Winhance 1:1 port for TechnicalDetailsManager dependencies
                services.AddSingleton<ILocalizationService, WinUI.Framework.Services.LocalizationService>();
                services.AddSingleton<IDispatcherService, AkariTool.Features.Common.Services.DispatcherService>();
                services.AddSingleton<IEventBus, AkariTool.Infrastructure.Features.Common.Events.EventBus>();
                services.AddSingleton<SettingStatusBannerManager>();
                services.AddSingleton<TechnicalDetailsManager>();

                services.AddSingleton<MainWindow>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<PlaceholderViewModel>();
        services.AddTransient<SettingsViewModel>();

        // ⚠ SINGLETON, not transient: tweak rows register themselves with
        // TweakRegistry on construction and TweakRegistry has no unregister. A
        // transient page view model would re-register the whole tab on every
        // navigation — inflating the tweak count, duplicating Backup/Restore
        // entries, and breaking the contiguous index range ClaimRange depends on.
        // Tweak-page VMs — one DI SINGLETON each (see the lifetime note above).
        services.AddSingleton<GamingViewModel>();
        services.AddSingleton<SoundViewModel>();
        services.AddSingleton<NotificationsViewModel>();
        services.AddSingleton<UpdateViewModel>();
        services.AddSingleton<PrivacyViewModel>();
        // Customize is now a landing HUB (no tweaks of its own); its tweaks live on six
        // category sub-pages, each a tweak-page VM. Registration order here = warm-up
        // order = TweakRegistry range order, and matches the old CustomizeViewModel
        // section order (Taskbar → Explorer → Context Menu → Appearance → Start Menu →
        // Desktop) so the flat Backup export stays byte-identical.
        services.AddSingleton<TaskbarViewModel>();
        services.AddSingleton<ExplorerViewModel>();
        services.AddSingleton<AppearanceViewModel>();
        services.AddSingleton<StartMenuViewModel>();
        services.AddSingleton<DesktopViewModel>();
        services.AddSingleton<PowerViewModel>();

        // ⚠ BESPOKE PAGE — deliberately NOT registered under TweakPageViewModel below.
        // The Software tab has no TweakDefinition rows and never registers with
        // TweakRegistry, so it must stay out of the warm-up enumeration (adding it
        // there would break the ClaimRange tiling assertion). Singleton for the same
        // reason the tweak pages are: it owns the built catalog + selection state.
        services.AddSingleton<ExternalAppsViewModel>();
        services.AddSingleton<WindowsAppsViewModel>();
        services.AddSingleton<DebloatViewModel>();
        services.AddSingleton<AkariTool.ViewModels.Backup.BackupViewModel>();
        services.AddSingleton<AkariTool.ViewModels.Verify.VerifyViewModel>();
        services.AddSingleton<AkariTool.ViewModels.AdvancedTools.AdvancedToolsViewModel>();
        services.AddSingleton<AkariTool.ViewModels.AkariOS.AkariOSViewModel>();

        // Enumerable marker for the startup warm-up: every declarative page VM is
        // ALSO registered under the SettingPageViewModel base type, resolving to the
        // SAME singleton instance. SettingPageWarmUp does GetServices<SettingPageViewModel>()
        // and Build()s each one at startup so a never-visited tab is still populated
        // when it is navigated. Registration order = warm-up order.
        //
        // Power moved here (Session C): its rows are no longer TweakDefinitions, so
        // the TweakRegistry warm-up / Backup export / global search no longer carry
        // them — the TweakPageViewModel marker registration that used to warm Power
        // is gone and the legacy registry warm-up is now a no-op.
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<GamingViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<SoundViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<NotificationsViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<UpdateViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<PrivacyViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<TaskbarViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<ExplorerViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<AppearanceViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<StartMenuViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<DesktopViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<PowerViewModel>());

        return services;
    }
}
