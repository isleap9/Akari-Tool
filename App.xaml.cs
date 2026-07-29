using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;

namespace AkariTool;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("--defender-phase2", StringComparer.OrdinalIgnoreCase))
        {
            RunDefenderPhase2Headless();   // does its work, then Shutdown — no UI
            return;                        // never touch splash/MainWindow
        }

        base.OnStartup(e);
        // Apply the persisted theme (default Dark) BEFORE the splash is shown so it
        // reads the correct tokens. ThemeService also re-asserts the Akari red accent
        // (WPF-UI otherwise derives accent brushes from the Windows personalisation colour).
        Services.ThemeService.Apply(Services.ThemeService.LoadPersisted());

        // Reliable click-to-open for the custom implicit ComboBox template (App.xaml).
        // The template's ToggleButton alone doesn't consistently open the popup, so open
        // on header press and mark handled (suppresses the toggle that would close it).
        // Item clicks live in the popup's own window, so they don't route through here;
        // header clicks while already open fall through to close normally.
        EventManager.RegisterClassHandler(typeof(ComboBox),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnComboBoxPreviewMouseDown), handledEventsToo: true);

        await ShowSplashAndLaunchAsync();
    }

    // ── Headless Defender phase-2 (post-reboot) ──
    // Launched via the HKLM RunOnce entry (see DefenderPhase2Scheduler) with the
    // --defender-phase2 flag. It must never show UI: it does its work, self-cleans the
    // RunOnce entry, and shuts the process down. Stage 2 keeps the payload a no-op — it
    // only proves the reboot→RunOnce→headless→self-clean loop before any destructive
    // Defender logic exists (Stage 3 will call the real phase-2 work here).
    private void RunDefenderPhase2Headless()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AkariTool", "defender-phase2.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
            void Log(string m) => System.IO.File.AppendAllText(logPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {m}{Environment.NewLine}");

            Log("[PHASE2] Headless phase-2 started.");
            Log($"[PHASE2] Identity: {System.Security.Principal.WindowsIdentity.GetCurrent().Name}");

            Log("[PHASE2] Native phase-2 starting.");
            Services.DefenderService.RunPhase2Native(Log);

            // Clear the RunOnce entry AFTER the work — a crash mid-work leaves it scheduled
            // to retry next login rather than silently half-disabling Defender.
            Services.DefenderPhase2Scheduler.ClearRunOnce();
            Log("[PHASE2] Native phase-2 complete.");
        }
        catch (Exception ex)
        {
            try {
                var p = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AkariTool", "defender-phase2.log");
                System.IO.File.AppendAllText(p, $"[PHASE2] ERROR: {ex}{Environment.NewLine}");
            } catch { }
        }
        finally
        {
            Shutdown();   // headless — exit immediately, never show a window
        }
    }

    // ── Startup orchestration ──
    // The splash must paint before the heavy MainWindow constructor runs, so the user
    // never stares at a blank taskbar icon during cold start. We show it first, let it
    // render a frame, then build MainWindow while reporting the seven init stages.
    private async Task ShowSplashAndLaunchAsync()
    {
        // Closing the splash mustn't shut the app down before MainWindow is assigned.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var splash = new SplashWindow();
        splash.Show();

        // Let the splash paint its first (0%) frame before we do any work.
        await PaintAsync();

        // Each stage: report BEFORE its work so the label/percentage/pip render first, then
        // run the phase, and hold the frame at least MinStepMs so fast phases don't blink past.
        // `completed` is the number of stages already finished, so the bar starts at 0%.
        MainWindow? main = null;

        await RunStageAsync(splash, 0, "Checking administrator privileges", null);
        await RunStageAsync(splash, 1, "Loading system configuration", null);
        await RunStageAsync(splash, 2, "Reading current system state", null);

        // The bulk of cold start: the MainWindow constructor initialises the ten tabs, builds
        // their catalogs and loads the WinGet app list. It must run on the UI thread (it creates
        // WPF controls), so stages 4–6 report the phases that happen inside this one call.
        await RunStageAsync(splash, 3, "Initializing optimization modules",
            () => { main = new MainWindow(); return Task.CompletedTask; });
        await RunStageAsync(splash, 4, "Building tweak catalog", null);
        await RunStageAsync(splash, 5, "Preparing app catalog (WinGet)", null);
        await RunStageAsync(splash, 6, "Finalizing interface", null);

        // All real init is done — light every pip at 100% and hold briefly before revealing.
        splash.Report(SplashWindow.TotalSteps, "Launching Akari Tool…");
        await PaintAsync();
        await Task.Delay(300);

        MainWindow = main;

        // --competitive "<exe>": launched from a Desktop shortcut. The window stays
        // hidden (CloseAfterLaunch behaviour), but the process must keep running —
        // the session watcher lives here and is what restores the user's settings.
        string? competitiveExe = ParseCompetitiveArgument();

        if (competitiveExe is null) main!.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        await splash.FadeOutAndCloseAsync();

        if (competitiveExe is not null)
        {
            // Orphan recovery still runs in this path — a crashed session must not
            // survive just because the next launch came from a shortcut. It shows
            // the window itself only if there is something to prompt about.
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
                return System.IO.File.Exists(path) ? path : null;
            }
        }
        catch (Exception ex) { Services.ToolService.Current?.Log($"[App] ParseCompetitiveArgument failed: {ex.Message}"); }
        return null;
    }

    // Minimum time each stage stays on screen so the bar fills deliberately rather than skipping.
    private const int MinStepMs = 320;

    // Reports a stage (label + pips + percentage), lets it paint, then runs its work while
    // enforcing a minimum visible duration. `work` may be null for phases with no separable
    // work (their real cost lives in the MainWindow constructor).
    private static async Task RunStageAsync(SplashWindow splash, int completed, string label, Func<Task>? work)
    {
        splash.Report(completed, label);
        await PaintAsync();

        var minVisible = Task.Delay(MinStepMs);
        if (work != null) await work();
        await minVisible;
    }

    // Yields to the dispatcher at Background priority — below Render — so a full paint of the
    // splash (status text / pips / percentage) is flushed before control returns.
    private static async Task PaintAsync() =>
        await Dispatcher.Yield(DispatcherPriority.Background);

    private static void OnComboBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ComboBox { IsDropDownOpen: false, IsEditable: false } cb)
        {
            cb.IsDropDownOpen = true;
            e.Handled = true;
        }
    }

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            LogOrReport($"UI UNHANDLED EXCEPTION: {e.Exception}");
            CrashReport.Write(e.Exception, "DispatcherUnhandledException");
            MessageBox.Show(e.Exception.ToString(), "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var msg = $"AppDomain UNHANDLED EXCEPTION: {e.ExceptionObject}";
            LogOrReport(msg);
            CrashReport.Write(e.ExceptionObject as Exception ?? new Exception(msg), "AppDomainUnhandledException");
            MessageBox.Show(e.ExceptionObject.ToString(), "AppDomain Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogOrReport($"Task UNOBSERVED EXCEPTION: {e.Exception}");
            CrashReport.Write(e.Exception, "UnobservedTaskException");
            MessageBox.Show(e.Exception.ToString(), "Task Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.SetObserved();
        };
    }

    private static void LogOrReport(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        try { Services.ToolService.Current?.Log(message); } catch { }
    }

    /// <summary>Writes a timestamped crash report file to %APPDATA%\AkariTool\.</summary>
    private static class CrashReport
    {
        public static void Write(Exception? ex, string source)
        {
            try
            {
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AkariTool");
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir,
                    $"AkariTool_crash_{DateTime.Now:yyyy-MM-dd}.log");
                System.IO.File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex?.ToString() ?? "null"}{Environment.NewLine}" +
                    $"---{Environment.NewLine}");
            }
            catch { }
        }
    }
}
