using Microsoft.Extensions.DependencyInjection;
using AkariTool.Services;
using AkariTool.ViewModels;
using AkariTool.ViewModels.Software;
using AkariTool.ViewModels.Tweaks;
using WinUI.Framework.Services;
using AkariTool.Core.Tweaks;
using AkariTool.Core.Interfaces;

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

        // Dialog helper for tweak confirmations (serializes ContentDialogs).
        services.AddSingleton<TweakDialogs>();

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
        services.AddSingleton<ContextMenuViewModel>();
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

        // Enumerable marker for the startup warm-up: every tweak-page VM is ALSO
        // registered under the TweakPageViewModel base type, resolving to the SAME
        // singleton instance. TweakRegistryWarmUp does GetServices<TweakPageViewModel>()
        // and Build()s each one at startup so a never-visited tab is still present
        // in Backup exports and global search (rows register inside Build(), which
        // otherwise only runs on first navigation). Registration order = warm-up
        // order = TweakRegistry range order; add each new tweak page to BOTH lists.
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<GamingViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<SoundViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<NotificationsViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<UpdateViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<PrivacyViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<TaskbarViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<ExplorerViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<ContextMenuViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<AppearanceViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<StartMenuViewModel>());
        services.AddSingleton<SettingPageViewModel>(sp => sp.GetRequiredService<DesktopViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<PowerViewModel>());

        return services;
    }
}
