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
using AkariTool.DI;
using AkariTool.Infrastructure.DI;

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

        // Startup orchestration AFTER the shell is up (Winhance StartupOrchestrator 1:1):
        // Phase 1 pre-filters every feature catalog into the CompatibleSettingsRegistry,
        // Phase 2 registers all bypassed settings into the GlobalSettingsRegistry, and
        // Phase 3 builds every SettingPageViewModel on a single background thread so
        // Backup export + global search see every tab even if the user never navigates
        // to it. Sequential (never parallel) — see SettingPageWarmUp for the threading
        // rationale. This is also the seam the future staged-progress splash hooks into.
        var shell = MainWindow as MainWindow;
        _ = Task.Run(async () =>
        {
            var orchestrator = new AkariTool.Services.StartupOrchestrator(
                Services.GetRequiredService<AkariTool.Core.Features.Common.Interfaces.ICompatibleSettingsRegistry>(),
                Services.GetRequiredService<AkariTool.Core.Features.Common.Interfaces.IGlobalSettingsPreloader>());
            await orchestrator.RunAsync(Services, log);

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

        // Framework base (must come first — subsequent lines override defaults)
        services.AddWinUIFrameworkCore();

        // Override framework defaults with app-specific implementations
        services.AddSingleton<IFileService, AkariFileService>();
        services.AddSingleton<FileLogService>();
        services.AddSingleton<AkariUiLogService>(sp =>
            new AkariUiLogService(sp.GetRequiredService<FileLogService>()));
        services.AddSingleton<ILogService>(sp =>
            sp.GetRequiredService<AkariUiLogService>());

        // Infrastructure + UI registrations
        services.AddAkariInfrastructure();
        services.AddAkariUI();

        var provider = services.BuildServiceProvider();
        ServiceLocator.Initialize(provider);
        return provider;
    }
}
