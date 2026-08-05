using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace AkariTool;

// WinUI 3 application entry point (Phase 0 scaffold).
//
// Phase 0 deliberately keeps this minimal: WinUI lifecycle (OnLaunched -> create
// Window), the process-wide crash handlers, the splash + staged startup
// orchestration, and — approved by isleap — the load-bearing "--defender-phase2"
// headless post-reboot handoff (RunDefenderPhase2Headless below). That handoff
// calls the byte-identical DefenderService.RunPhase2Native +
// DefenderPhase2Scheduler.ClearRunOnce entry points; only Shutdown()→Exit()
// differs from WPF.
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();

        // WinUI's own UI-thread unhandled-exception hook (replaces WPF's
        // DispatcherUnhandledException). Log + crash-report; do not show UI here.
        this.UnhandledException += (_, e) =>
        {
            LogOrReport($"UI UNHANDLED EXCEPTION: {e.Exception}");
            CrashReport.Write(e.Exception, "App.UnhandledException");
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var msg = $"AppDomain UNHANDLED EXCEPTION: {e.ExceptionObject}";
            LogOrReport(msg);
            CrashReport.Write(e.ExceptionObject as Exception ?? new Exception(msg), "AppDomainUnhandledException");
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogOrReport($"Task UNOBSERVED EXCEPTION: {e.Exception}");
            CrashReport.Write(e.Exception, "UnobservedTaskException");
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Post-reboot Defender phase-2 handoff. Launched by the HKLM RunOnce entry
        // written in phase 1 (DefenderService → DefenderPhase2Scheduler.ScheduleRunOnce).
        // Must be checked BEFORE anything else: this path never shows UI.
        if (Environment.GetCommandLineArgs()
                .Contains("--defender-phase2", StringComparer.OrdinalIgnoreCase))
        {
            RunDefenderPhase2Headless();   // does its work, then Exit()
            return;                        // never touch splash/MainWindow
        }

        _ = ShowSplashAndLaunchAsync();
    }

    // ── Headless Defender phase-2 (post-reboot) ──
    // Launched via the HKLM RunOnce entry (see DefenderPhase2Scheduler) with the
    // --defender-phase2 flag. It must never show UI: it does its work, self-cleans the
    // RunOnce entry, and shuts the process down.
    //
    // MIGRATION: this is the WPF RunDefenderPhase2Headless verbatim except for two
    // framework-only changes — WPF's `ShutdownMode = OnExplicitShutdown` line is gone
    // (no WinUI equivalent, and unnecessary because no window is ever created on this
    // path), and `Shutdown()` became `this.Exit()`. DefenderService.cs and
    // DefenderPhase2Scheduler.cs are byte-identical to the WPF build.
    private void RunDefenderPhase2Headless()
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AkariTool", "defender-phase2.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            void Log(string m) => File.AppendAllText(logPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {m}{Environment.NewLine}");

            Log("[PHASE2] Headless phase-2 started.");
            Log($"[PHASE2] Identity: {System.Security.Principal.WindowsIdentity.GetCurrent().Name}");

            Log("[PHASE2] Native phase-2 starting.");
            Services.DefenderService.RunPhase2Native(Log);

            // Clear the RunOnce entry AFTER the work — a crash mid-work leaves it
            // scheduled to retry next login rather than silently half-disabling Defender.
            Services.DefenderPhase2Scheduler.ClearRunOnce();
            Log("[PHASE2] Native phase-2 complete.");
        }
        catch (Exception ex)
        {
            try
            {
                var p = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AkariTool", "defender-phase2.log");
                File.AppendAllText(p, $"[PHASE2] ERROR: {ex}{Environment.NewLine}");
            }
            catch { }
        }
        finally
        {
            this.Exit();   // headless — exit immediately, never show a window
        }
    }

    // ── Startup orchestration ──
    // The splash must paint before the heavy MainWindow constructor runs, so the user
    // never stares at a blank taskbar icon during cold start. We show it first, let it
    // render a frame, then build MainWindow while reporting the seven init stages.
    private async Task ShowSplashAndLaunchAsync()
    {
        // Theme FIRST, before any window paints, so the splash reads the correct
        // tokens (WPF applied it at the top of OnStartup for the same reason).
        // The splash's own root is attached as the theme root until MainWindow exists.
        var theme = Services.ThemeService.LoadPersisted();

        var splash = new SplashWindow();
        Services.ThemeService.AttachRoot(splash.Content as FrameworkElement);
        Services.ThemeService.Apply(theme);
        splash.Activate();

        // Let the splash paint its first (0%) frame before we do any work.
        await PaintAsync();

        // Each stage: report BEFORE its work so the label/percentage/pip render first,
        // then run the phase, holding the frame at least MinStepMs so fast phases don't
        // blink past. `completed` is the number of stages already finished, so the bar
        // starts at 0%.
        MainWindow? main = null;

        await RunStageAsync(splash, 0, "Checking administrator privileges", null);
        await RunStageAsync(splash, 1, "Loading system configuration", null);
        await RunStageAsync(splash, 2, "Reading current system state", null);

        // The bulk of cold start: the MainWindow constructor initialises every tab,
        // builds their catalogs and loads the WinGet app list. It must run on the UI
        // thread (it creates WinUI controls), so stages 4-6 report the phases that
        // happen inside this one call.
        await RunStageAsync(splash, 3, "Initializing optimization modules",
            () => { main = new MainWindow(); return Task.CompletedTask; });
        await RunStageAsync(splash, 4, "Building tweak catalog", null);
        await RunStageAsync(splash, 5, "Preparing app catalog (WinGet)", null);
        await RunStageAsync(splash, 6, "Finalizing interface", null);

        // All real init is done — light every pip at 100% and hold briefly before revealing.
        splash.Report(SplashWindow.TotalSteps, "Launching Akari Tool…");
        await PaintAsync();
        await Task.Delay(300);

        _window = main;

        // --competitive "<exe>": launched from a Desktop shortcut. The window stays
        // hidden (CloseAfterLaunch behaviour), but the process must keep running —
        // the session watcher lives here and is what restores the user's settings.
        // NOTE: in WinUI an un-Activated window still keeps the app alive, which is
        // what replaces WPF's ShutdownMode juggling.
        string? competitiveExe = ParseCompetitiveArgument();

        if (competitiveExe is null) main!.Activate();

        await splash.FadeOutAndCloseAsync();

        if (competitiveExe is not null)
        {
            // Orphan recovery still runs in this path — a crashed session must not
            // survive just because the next launch came from a shortcut. It shows the
            // window itself only if there is something to prompt about.
            await main!.CheckOrphanedCompetitiveSessionAsync();
            await main.StartCompetitiveFromCommandLineAsync(competitiveExe);
        }
    }

    /// <summary>
    /// The path following --competitive, or null. Returns null for a missing or
    /// non-existent path so a stale shortcut falls back to a normal launch rather
    /// than starting a session against nothing.
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
        catch (Exception ex) { Services.ToolService.Current?.Log($"[App] ParseCompetitiveArgument failed: {ex.Message}"); }
        return null;
    }

    // Minimum time each stage stays on screen so the bar fills deliberately rather than skipping.
    private const int MinStepMs = 320;

    // Reports a stage (label + pips + percentage), lets it paint, then runs its work
    // while enforcing a minimum visible duration. `work` may be null for phases with no
    // separable work (their real cost lives in the MainWindow constructor).
    private static async Task RunStageAsync(SplashWindow splash, int completed, string label, Func<Task>? work)
    {
        splash.Report(completed, label);
        await PaintAsync();

        var minVisible = Task.Delay(MinStepMs);
        if (work != null) await work();
        await minVisible;
    }

    // MIGRATION: WPF yielded with Dispatcher.Yield(DispatcherPriority.Background) to
    // flush a paint. WinUI has no Dispatcher.Yield, so this re-enqueues at Low
    // priority — which runs after the current render pass — and awaits that.
    private static Task PaintAsync()
    {
        var tcs = new TaskCompletionSource();
        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (queue is null || !queue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => tcs.TrySetResult()))
        {
            tcs.TrySetResult();
        }
        return tcs.Task;
    }

    private static void LogOrReport(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        try { Services.ToolService.Current?.Log(message); }
        catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"[App] LogOrReport failed: {logEx.Message}"); }
    }

    /// <summary>Writes a timestamped crash report file to %APPDATA%\AkariTool\.</summary>
    private static class CrashReport
    {
        public static void Write(Exception? ex, string source)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AkariTool");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"AkariTool_crash_{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex?.ToString() ?? "null"}{Environment.NewLine}" +
                    $"---{Environment.NewLine}");
            }
            catch (Exception writeEx) { System.Diagnostics.Debug.WriteLine($"[App] CrashReport.Write failed: {writeEx.Message}"); }
        }
    }
}
