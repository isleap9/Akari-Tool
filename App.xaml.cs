using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using AkariTool.Services;
using AkariTool.ViewModels;
using AkariTool.ViewModels.AkariOS;
using AkariTool.ViewModels.Software;
using AkariTool.ViewModels.Tweaks;
using WinUI.Framework.IoC;
using WinUI.Framework.Services;
using AkariTool.Core.Tweaks;
using AkariTool.Core.Interfaces;
using AkariTool.Infrastructure.Services;

namespace AkariTool;

/// <summary>
/// Application entry point. Phase A keeps startup deliberately thin (no staged
/// splash): the framework's DI container is built once, services resolve from it,
/// and the shell window is created and activated. The migration branch's staged
/// splash + Defender phase-2 handoff are deferred to a later phase (see
/// docs/MIGRATION.md).
/// </summary>
public partial class App : Application
{
    /// <summary>The main application window.</summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>The UI thread dispatcher. Use to marshal calls to the UI thread.</summary>
    public static DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>The app-wide dependency injection container.</summary>
    public static IServiceProvider Services { get; } = ConfigureServices();

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Route unhandled exceptions into the file log so failures survive a crash.
        var log = Services.GetRequiredService<ILogService>();
        UnhandledException += (_, e) => log.Error("Unhandled exception.", e.Exception);

        MainWindow = Services.GetRequiredService<MainWindow>();

        // --competitive "<exe>": launched from a Desktop shortcut. The window stays
        // hidden (CloseAfterLaunch behaviour), but the process keeps running — the
        // session watcher lives in-process and is what restores the user's settings.
        // An un-Activated window still keeps the app alive (replaces WPF ShutdownMode).
        string? competitiveExe = ParseCompetitiveArgument();
        if (competitiveExe is null)
        {
            MainWindow.Activate();
            // Normal path: offer to restore a session left over from a crash/kill (E3).
            // No-op when there is no orphaned session on record.
            _ = Services.GetRequiredService<AkariOSViewModel>().CheckOrphanedSessionAsync();
        }

        // Warm up the tweak registry AFTER the shell is up: build every tweak-page
        // VM once, on a single background thread, so Backup export + global search
        // see every tab even if the user never navigates to it. Sequential (never
        // parallel) — see TweakRegistryWarmUp for the threading rationale. This is
        // also the seam the future staged-progress splash will hook into.
        var shell = MainWindow as MainWindow;
        _ = Task.Run(() =>
        {
            TweakRegistryWarmUp.Run(Services, log);

            // Drift check runs ONLY after warm-up: DriftScanner resolves each baseline
            // entry against TweakRegistry, so scanning before every page's Build() has
            // registered its rows would orphan (and silently miss) most tweaks. Marshal
            // back to the UI thread — it flips the title-bar drift banner's IsOpen.
            shell?.DispatcherQueue.TryEnqueue(() => shell.RunDriftCheck());
        });

        if (competitiveExe is not null)
        {
            // --competitive path: recover any orphaned session FIRST (net8 order), then start
            // the shortcut's session. Sequenced in a local async helper since OnLaunched is void.
            _ = StartCompetitiveFromShortcutAsync(competitiveExe);
        }
    }

    private static async Task StartCompetitiveFromShortcutAsync(string exePath)
    {
        var vm = Services.GetRequiredService<AkariOSViewModel>();
        await vm.CheckOrphanedSessionAsync();
        await vm.StartFromCommandLineAsync(exePath);
    }

    /// <summary>
    /// The path following <c>--competitive</c>, or null. Returns null for a missing or
    /// non-existent path so a stale shortcut falls back to a normal launch rather than
    /// starting a session against nothing.
    /// </summary>
    private static string? ParseCompetitiveArgument()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!args[i].Equals("--competitive", StringComparison.OrdinalIgnoreCase)) continue;

                string path = args[i + 1];
                return File.Exists(path) ? path : null;
            }
        }
        catch (Exception ex) { ToolService.Current?.Log($"[App] ParseCompetitiveArgument failed: {ex.Message}"); }
        return null;
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddWinUIFrameworkCore();

        // App-local IFileService override (Phase 4): the framework default uses WinRT
        // Windows.Storage.Pickers, which throw COMException 0x80004005 under elevation
        // (this app ships requireAdministrator). AkariFileService drives the Win32 COM
        // common dialogs instead, which work at any integrity level. Registered AFTER
        // AddWinUIFrameworkCore so MS.DI's last-registration-wins resolves to this one —
        // same IFileService interface, so no call sites change. Framework untouched.
        services.AddSingleton<IFileService, AkariFileService>();

        // UI log decorator: wraps the framework's FileLogService and raises
        // LineLogged so the shell's log dock renders live output. Registered after
        // AddWinUIFrameworkCore so the interface resolves to this instance.
        services.AddSingleton<FileLogService>();
        services.AddSingleton<AkariUiLogService>(sp => new AkariUiLogService(sp.GetRequiredService<FileLogService>()));
        services.AddSingleton<ILogService>(sp => sp.GetRequiredService<AkariUiLogService>());

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

        // Interface wrappers over the static services (Infrastructure).
        services.AddSingleton<IUpdateService, UpdateServiceWrapper>();
        services.AddSingleton<IToolFetchService, ToolFetchServiceWrapper>();
        services.AddSingleton<ISystemInfoService, SystemInfoServiceWrapper>();
        services.AddSingleton<IShaderCacheService, ShaderCacheServiceWrapper>();

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
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<GamingViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<SoundViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<NotificationsViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<UpdateViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<PrivacyViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<TaskbarViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<ExplorerViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<ContextMenuViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<AppearanceViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<StartMenuViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<DesktopViewModel>());
        services.AddSingleton<TweakPageViewModel>(sp => sp.GetRequiredService<PowerViewModel>());

        var provider = services.BuildServiceProvider();
        ServiceLocator.Initialize(provider);
        return provider;
    }
}
