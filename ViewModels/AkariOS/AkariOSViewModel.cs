using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Tabs;
using WinUI.Framework.IoC;
using WinUI.Framework.Services;
using AkariTool.Core.Competitive;

namespace AkariTool.ViewModels.AkariOS;

/// <summary>
/// AkariOS VM — MVVM port of net8 <c>AkariOSTab</c>'s cross-cutting state.
///
/// Most of the AkariOS tab is built imperatively in the page code-behind (faithful to
/// net8). This VM owns the shared <see cref="ToolService"/> AND — since E1 — the
/// <b>Competitive Mode session-control state + Start/End machine</b>, relocated off the
/// page so the headless <c>--competitive</c> command-line and shutdown/recovery paths
/// (E2/E3) can drive a session without a page instance. The page keeps all Competitive
/// UI (picker, options, status panel, timer) and delegates the actual start/end here,
/// rendering the <see cref="Status"/> / <see cref="StateChanged"/> events.
///
/// ⚠ Bespoke, registers NOTHING with TweakRegistry, so it stays OUT of the
/// TweakPageViewModel warm-up enumeration and the <c>[WARMUP]</c> total is unchanged.
///
/// The actual session (suspend/stop/power/standby/window) lives in the already-ported
/// <see cref="CompetitiveService"/> static singleton; this VM is only its driver.
/// <see cref="GetActiveSchemeForDisplay"/> is READ-ONLY (SystemStateReader.ReadActivePowerPlan)
/// — it never reactivates a scheme; the scheme write lives only inside CompetitiveService.StartAsync
/// (the CLAUDE.md power-plan invariant).
/// </summary>
public sealed class AkariOSViewModel
{
    /// <summary>The shared headless tool service — logging + process/script running.</summary>
    public ToolService Tool { get; }

    private readonly TweakDialogs _dialogs;
    // Framework dialog service for the crash-recovery prompt, which needs a custom "Ignore"
    // close label TweakDialogs can't express (E3). Same DI singleton TweakDialogs wraps.
    private readonly IDialogService _idlg;

    public AkariOSViewModel(ToolService tool, TweakDialogs dialogs, IDialogService idlg)
    {
        Tool = tool;
        _dialogs = dialogs;
        _idlg = idlg;

        // VM-lifetime subscription (E1 flag 2, isleap-approved): a game-exit ends the
        // session even if the page was never shown (CLI path) or was navigated away —
        // net8's page-scoped subscription could not cover the headless path.
        CompetitiveService.SessionEndedByGameExit += OnSessionEndedExternally;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Competitive session-control state (relocated from AkariOSPage, E1)
    // ══════════════════════════════════════════════════════════════════════

    private string? _selectedPath;
    private bool _busy;
    private CancellationTokenSource? _cts;
    private string? _activeSchemeName;

    /// <summary>The selected game exe (survives leaving/returning to the page — E1 flag 3).</summary>
    public string? SelectedPath { get => _selectedPath; set => _selectedPath = value; }
    public bool IsBusy => _busy;
    public string? ActiveSchemeName => _activeSchemeName;
    public bool IsSessionActive => CompetitiveService.IsSessionActive;

    /// <summary>Status headline text for the page to render.</summary>
    public event Action<string>? Status;
    /// <summary>Session/busy state changed — the page re-syncs controls, timer, status panel.</summary>
    public event Action? StateChanged;

    private void RaiseStatus(string text) => Status?.Invoke(text);
    private void RaiseStateChanged() => StateChanged?.Invoke();

    // UI dispatcher for window ops + marshaling the background game-exit watcher. The VM
    // has no inherent DispatcherQueue and the CLI path has no page, so it comes from the
    // MainWindow DI singleton (E1 flag 1, isleap-approved).
    private static DispatcherQueue? Ui => ServiceLocator.GetService<MainWindow>()?.DispatcherQueue;

    // ── Start / End ────────────────────────────────────────────────────────

    public async Task PrimaryClickAsync(CompetitiveOptions options)
    {
        if (IsSessionActive) { await EndSessionAsync(); return; }
        if (_busy) { CancelStart(); return; }
        await StartSessionAsync(options);
    }

    public void CancelStart()
    {
        _cts?.Cancel();
        RaiseStatus("Cancelling…");
    }

    public async Task StartSessionAsync(CompetitiveOptions options)
    {
        string? path = _selectedPath;
        if (string.IsNullOrWhiteSpace(path)) { RaiseStatus("Select a game before starting."); return; }
        if (!File.Exists(path)) { RaiseStatus($"That executable no longer exists: {path}"); return; }

        // One-time experimental disclaimer, before ANY mutation — including the power plan
        // switch, which is the first thing StartAsync touches.
        if (!await ConfirmDisclaimerAsync())
        {
            RaiseStatus("Cancelled — nothing was changed.");
            RaiseStateChanged();
            return;
        }

        _busy = true;
        _cts = new CancellationTokenSource();
        RaiseStateChanged();

        try
        {
            CompetitivePrefs.SaveOptions(options);

            var progress = new Progress<string>(RaiseStatus);
            var result = await CompetitiveService.StartAsync(path, options, progress, _cts.Token);

            if (!result.Started)
            {
                // StartAsync has already restored everything it applied.
                HandleStartFailure(result);
                return;
            }

            var state = result.State!;
            _activeSchemeName = state.PreviousPowerSchemeGuid is null
                ? null
                : await Task.Run(() => CompetitiveService.DescribeScheme(GetActiveSchemeForDisplay()));

            Tool?.Log($"Competitive Mode started for {state.GameProcessName}.");
            RaiseStateChanged();   // session now active — page starts its elapsed timer

            if (options.CloseAfterLaunch) ScheduleHide(state.GameProcessName);
        }
        catch (Exception ex)
        {
            RaiseStatus($"Could not start: {ex.Message}");
            Tool?.Log($"ERROR Competitive Mode start: {ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _busy = false;
            RaiseStateChanged();
        }
    }

    /// <summary>
    /// Reports a start that never reached a running game. StartAsync guarantees it has
    /// already rolled back, so this only explains what happened and leaves the section idle.
    /// (No StateChanged here: the StartSessionAsync finally raises it, which stops the
    /// page timer + repaints — matching net8's net behavior.)
    /// </summary>
    private void HandleStartFailure(CompetitiveStartResult result)
    {
        _activeSchemeName = null;

        string message = result.Outcome switch
        {
            CompetitiveStartOutcome.GameNotFound =>
                "The game did not start. Nothing was left changed — all settings have been " +
                "restored. If you picked the wrong executable, use Browse to select the " +
                "game's main .exe.",

            CompetitiveStartOutcome.Cancelled =>
                "Cancelled before the game started. All settings have been restored.",

            _ =>
                $"Could not start: {result.Error}. All settings have been restored.",
        };

        RaiseStatus(message);
        Tool?.Log($"Competitive Mode start failed ({result.Outcome}) — settings restored.");
    }

    private static string? GetActiveSchemeForDisplay()
    {
        var (_, guid) = SystemStateReader.ReadActivePowerPlan();
        return guid;
    }

    /// <summary>
    /// Starts a session from a Desktop <c>--competitive</c> shortcut (E2), using the
    /// persisted options with <c>CloseAfterLaunch</c> forced on. Relocated from net8's
    /// <c>StartCompetitiveFromCommandLineAsync</c> the same way <see cref="StartSessionAsync"/>
    /// was: UI calls → events. net8's UI-only calls on this path (BeginCompetitiveGameDetection,
    /// LoadCompetitiveOptionsIntoUi, StartCompetitiveTimer) are dropped — the window is hidden
    /// here; a page shown later re-syncs via the page's StateChanged handler. Called by
    /// <c>App.OnLaunched</c> directly on this VM (no MainWindow delegator).
    /// </summary>
    public async Task StartFromCommandLineAsync(string exePath)
    {
        if (!File.Exists(exePath)) { RaiseStatus($"That executable no longer exists: {exePath}"); return; }

        _selectedPath = exePath;
        CompetitivePrefs.SaveLastGamePath(exePath);

        var stored = CompetitivePrefs.LoadOptions();
        var options = stored with { CloseAfterLaunch = true };

        // The shortcut path is a first use like any other — the disclaimer gates it too,
        // before anything is touched.
        if (!await ConfirmDisclaimerAsync())
        {
            RaiseStatus("Cancelled — nothing was changed.");
            RaiseStateChanged();
            return;
        }

        _busy = true;
        _cts = new CancellationTokenSource();
        RaiseStateChanged();
        try
        {
            var progress = new Progress<string>(RaiseStatus);
            var result = await CompetitiveService.StartAsync(exePath, options, progress, _cts.Token);

            if (!result.Started)
            {
                HandleStartFailure(result);
                return;
            }

            var state = result.State!;
            Tool?.Log($"Competitive Mode started from shortcut for {state.GameProcessName}.");
            RaiseStateChanged();       // session active — a shown page starts its timer
            ScheduleHide(state.GameProcessName);   // net8 always hides on the shortcut path
        }
        catch (Exception ex)
        {
            RaiseStatus($"Could not start: {ex.Message}");
            Tool?.Log($"ERROR Competitive Mode (shortcut) start: {ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _busy = false;
            RaiseStateChanged();
        }
    }

    public async Task EndSessionAsync()
    {
        var state = CompetitiveService.CurrentState;
        if (state is null) { RaiseStateChanged(); return; }

        _busy = true;
        RaiseStateChanged();

        try
        {
            var progress = new Progress<string>(RaiseStatus);
            await CompetitiveService.EndAsync(state, progress);
            Tool?.Log("Competitive Mode ended — settings restored.");
        }
        catch (Exception ex)
        {
            RaiseStatus($"Restore reported a problem: {ex.Message}");
            Tool?.Log($"ERROR Competitive Mode end: {ex.Message}");
        }
        finally
        {
            _activeSchemeName = null;
            RestoreMainWindow();
            _busy = false;
            RaiseStateChanged();   // session now inactive — page stops its timer
        }
    }

    /// <summary>Watcher-driven end — marshal to the UI dispatcher before touching UI/window.</summary>
    private void OnSessionEndedExternally()
    {
        Ui?.TryEnqueue(() =>
        {
            _activeSchemeName = null;
            RestoreMainWindow();
            RaiseStatus("Game exited — settings restored.");
            RaiseStateChanged();
        });
    }

    // ── Crash recovery + shutdown (E3; net8 lived in MainWindow) ────────────

    private bool _recoveryChecked;

    /// <summary>
    /// Offers to undo a session that was never closed properly (crash, power loss,
    /// task-manager kill). net8 lived in MainWindow; relocated here (isleap decision 2) so
    /// both startup paths call it on the VM directly. TryLoad is read-only; returns early
    /// (no dialog) when there is no orphaned session on record. The prompt uses the framework
    /// IDialogService directly for its custom "Restore"/"Ignore" labels (4th dialog swap;
    /// TweakDialogs hardcodes "Cancel").
    /// </summary>
    public async Task CheckOrphanedSessionAsync()
    {
        if (_recoveryChecked) return;   // callable from both startup paths; run once
        _recoveryChecked = true;

        CompetitiveSessionState state;
        try { if (!CompetitiveSessionStore.TryLoad(out state)) return; }
        catch { return; }

        // XamlRoot may be unset on the --competitive path (window never shown). Take it from
        // MainWindow's content, matching net8's RootGrid.XamlRoot fallback; skip if still null
        // (truly headless — leave the record for the next normal launch rather than crash).
        if (_idlg.XamlRoot is null)
            _idlg.XamlRoot = (ServiceLocator.GetService<MainWindow>()?.Content as FrameworkElement)?.XamlRoot;
        if (_idlg.XamlRoot is null) return;

        try
        {
            var result = await _idlg.ShowAsync(
                "Competitive Mode",
                new TextBlock
                {
                    Text = $"A Competitive Mode session from {state.StartedUtc.ToLocalTime():g} was not " +
                           "closed properly. Some background apps may still be suspended and some " +
                           "services stopped. Restore normal settings now?",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                "Restore", "Ignore");

            if (result == ContentDialogResult.Primary)
            {
                await CompetitiveService.EndAsync(state, null);
                Tool?.Log("Competitive Mode: orphaned session restored.");
            }
            else
            {
                // Clear either way — otherwise the prompt reappears on every launch for a
                // session the user has chosen not to undo.
                CompetitiveSessionStore.Clear();
                Tool?.Log("Competitive Mode: orphaned session record discarded.");
            }
        }
        catch { /* recovery must never block startup */ }
    }

    /// <summary>
    /// net8 <c>EndCompetitiveSessionForShutdownAsync</c> — ported DORMANT/verbatim. net8's
    /// doc-comment claims a MainWindow Closing handler that does not actually exist, so it is
    /// uncalled there too. Kept uncalled here (isleap decision 3): NO AppWindow.Closing handler.
    /// </summary>
    public async Task EndForShutdownAsync()
    {
        _cts?.Cancel();
        await EndSessionAsync();
    }

    // ── Close-after-launch window handling (net8 MainWindow.Instance → DI singleton) ──

    /// <summary>
    /// Hides the window ~10s after the game is first seen. Never exits the app: the session
    /// watcher lives in this process, so exiting would strand every suspended app / service.
    /// </summary>
    private void ScheduleHide(string processName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 60; i++)
                {
                    if (Process.GetProcessesByName(processName).Length > 0) break;
                    await Task.Delay(1000);
                }
                await Task.Delay(TimeSpan.FromSeconds(10));

                Ui?.TryEnqueue(() =>
                {
                    var w = ServiceLocator.GetService<MainWindow>();
                    if (w is not null && CompetitiveService.IsSessionActive) w.AppWindow.Hide();
                });
            }
            catch { /* hiding is a convenience — never let it surface */ }
        });
    }

    private void RestoreMainWindow()
    {
        try
        {
            var w = ServiceLocator.GetService<MainWindow>();
            if (w is null) return;
            w.AppWindow.Show();
            if (w.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
                p.Restore();
            w.Activate();
        }
        catch { }
    }

    // ── One-time experimental disclaimer (relocated from AkariOSPage, E1) ──
    //  3rd AkariDialogs→TweakDialogs swap (arg order reversed to title, content, primaryText).
    //  On this machine the accepted flag is already 1, so ConfirmDisclaimerAsync short-circuits
    //  and the dialog does not show during normal verification.

    private const string DisclaimerPrefKey = @"HKEY_CURRENT_USER\Software\AkariTool";
    private const string DisclaimerPrefName = "CompetitiveDisclaimerAccepted";

    private static bool DisclaimerAccepted()
    {
        try { return Registry.GetValue(DisclaimerPrefKey, DisclaimerPrefName, null) is int i && i != 0; }
        catch { return false; }
    }

    private async Task<bool> ConfirmDisclaimerAsync()
    {
        if (DisclaimerAccepted()) return true;

        bool accepted = await _dialogs.ConfirmContentAsync(
            "Competitive Mode is experimental",
            new TextBlock
            {
                Text = "This feature suspends background apps, stops Windows services and changes " +
                       "the power plan for the duration of your game session, then restores them " +
                       "when the game exits.\n\n" +
                       "Most issues are recoverable — if something is left in a bad state, restarting " +
                       "your PC restores all of it. But this is new and not yet widely tested, so use " +
                       "it at your own risk.\n\n" +
                       "Continue?",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                MaxWidth = 440,
            },
            primaryText: "I understand, continue");

        if (!accepted) return false;

        try { Registry.SetValue(DisclaimerPrefKey, DisclaimerPrefName, 1, RegistryValueKind.DWord); }
        catch { /* a lost flag only means asking again — never block the start */ }

        return true;
    }
}
