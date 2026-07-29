using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AkariTool.Services
{
    public enum CompetitiveStartOutcome
    {
        /// <summary>The game is running and the session owns the restore.</summary>
        Started,

        /// <summary>
        /// The game never appeared within the wait window. Everything applied has
        /// already been rolled back — there is nothing left for the caller to undo.
        /// </summary>
        GameNotFound,

        /// <summary>Cancelled before the game was confirmed; already rolled back.</summary>
        Cancelled,

        /// <summary>Threw before the game was confirmed; already rolled back.</summary>
        Error,
    }

    /// <summary>
    /// Outcome of a start attempt. <see cref="State"/> is non-null only when
    /// <see cref="Outcome"/> is <see cref="CompetitiveStartOutcome.Started"/> —
    /// every other outcome has already restored the machine.
    /// </summary>
    public sealed record CompetitiveStartResult(
        CompetitiveStartOutcome Outcome,
        CompetitiveSessionState? State,
        string? Error)
    {
        public bool Started => Outcome == CompetitiveStartOutcome.Started;
    }

    /// <summary>
    /// Orchestrates a Competitive Mode session: apply, launch, watch for exit,
    /// restore. Every mutation is recorded to <see cref="CompetitiveSessionStore"/>
    /// before it happens so a crash is recoverable from the next launch.
    /// </summary>
    public static class CompetitiveService
    {
        // ── Process lists ─────────────────────────────────────────────────────

        /// <summary>
        /// Background apps suspended by Game Focus. Hardcoded for v1; these are the
        /// memory- and CPU-hungry things that are almost always running during a
        /// match and almost never needed by it.
        /// </summary>
        private static readonly string[] SuspendableNames =
        {
            "chrome", "msedge", "firefox", "opera", "brave",
            "Discord", "Spotify", "OneDrive",
            "EpicGamesLauncher", "Battle.net", "EADesktop", "GalaxyClient",
            "Teams", "ms-teams", "slack",
            "Adobe Desktop Service", "CCXProcess",
        };

        /// <summary>
        /// Never suspend or stop these, whatever else the logic decides. Suspending
        /// csrss/wininit/lsass deadlocks the session; suspending dwm/explorer/audiodg
        /// freezes the desktop or kills game audio. This list is the last word — it
        /// is checked after every other filter, not before.
        /// </summary>
        private static readonly HashSet<string> HardExclusions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "csrss", "wininit", "winlogon", "services", "lsass", "smss",
                "System", "Idle", "dwm", "explorer", "audiodg", "svchost",
                "ctfmon", "RuntimeBroker", "SearchHost",
            };

        /// <summary>
        /// Services paused for the session. Deliberately short: ShellHWDetection,
        /// luafv, CDPSvc, CDPUserSvc, WpnService, WpnUserService and every Defender
        /// service are excluded — stopping those breaks removable media, UAC file
        /// virtualisation, notifications, or security, for no framerate gain.
        /// </summary>
        private static readonly string[] SessionServices = { "WSearch", "SysMain" };

        private const string ServicesKeyRoot = @"SYSTEM\CurrentControlSet\Services";
        private const int    ServiceDisabled = 4;

        // ── Session tracking ──────────────────────────────────────────────────

        private static CompetitiveSessionState? _current;
        private static CancellationTokenSource? _watcherCts;

        public static bool IsSessionActive => _current is not null;

        public static CompetitiveSessionState? CurrentState => _current;

        /// <summary>
        /// Raised on a background thread when the watcher ends a session because the
        /// game exited. The UI marshals to the dispatcher itself.
        /// </summary>
        public static event Action? SessionEndedByGameExit;

        // ── Start ─────────────────────────────────────────────────────────────

        public static async Task<CompetitiveStartResult> StartAsync(
            string gameExePath, CompetitiveOptions options,
            IProgress<string>? progress, CancellationToken ct)
        {
            string processName = Path.GetFileNameWithoutExtension(gameExePath);

            var state = new CompetitiveSessionState(
                GameExePath:             gameExePath,
                GameProcessName:         processName,
                StartedUtc:              DateTime.UtcNow,
                PreviousPowerSchemeGuid: null,
                SuspendedProcesses:      Array.Empty<SuspendedProcess>(),
                StoppedServices:         Array.Empty<StoppedService>(),
                TuningFailures:          Array.Empty<string>());

            // Written before the first mutation: from here on, anything that changes
            // system state is already covered by a recoverable record.
            CompetitiveSessionStore.Save(state);

            var failures = new List<string>();

            // Everything from the first mutation to a CONFIRMED running game lives
            // inside this try/finally. If the game never appears — or anything
            // throws on the way — the finally restores the machine. A half-applied
            // session with no process to watch would otherwise leave the power plan
            // switched and services stopped with nothing to ever undo them.
            bool confirmed = false;
            try
            {
            // ── 1. Power plan ─────────────────────────────────────────────────
            if (options.ConsistentPerformance)
            {
                progress?.Report("Switching power plan…");
                string? previous = await Task.Run(GetActiveSchemeGuid, ct);

                // Recorded BEFORE the switch — a crash between the two would
                // otherwise strand the user on Ultimate Performance with no record
                // of what they were on.
                state = state with { PreviousPowerSchemeGuid = previous };
                CompetitiveSessionStore.Save(state);

                string? target = await Task.Run(FindPerformanceSchemeGuid, ct);
                if (target is null || !await Task.Run(() => SetActiveScheme(target), ct))
                    failures.Add("power plan");
            }

            // ── 2. Game Focus: suspend background apps ────────────────────────
            if (options.GameFocus)
            {
                progress?.Report("Suspending background apps…");
                var suspended = await Task.Run(() => SuspendBackgroundApps(processName), ct);
                state = state with { SuspendedProcesses = suspended };
                CompetitiveSessionStore.Save(state);
            }

            // ── 3. Pause non-essential services ───────────────────────────────
            if (options.PauseNonEssentialServices)
            {
                progress?.Report("Pausing non-essential services…");
                var stopped = await Task.Run(StopSessionServices, ct);
                state = state with { StoppedServices = stopped };
                CompetitiveSessionStore.Save(state);
            }

            // ── 4. Launch the game ────────────────────────────────────────────
            var plan = ResolveLaunch(gameExePath, options);
            bool steamWasRunning = plan.ViaSteam && IsSteamClientRunning();

            progress?.Report(plan.ViaSteam
                ? $"Launching {processName} through Steam (AppID {plan.AppId})…"
                : $"Launching {processName}…");
            try
            {
                Process.Start(BuildLaunchStartInfo(gameExePath, plan));
            }
            catch (Exception ex)
            {
                failures.Add($"launch ({ex.Message})");
            }

            // ── 5. Tune the game process ──────────────────────────────────────
            // A steam:// launch returns immediately and the process it starts is
            // Steam, not the game, so the only usable signal is find-by-name. When
            // Steam has to cold-start it must also update itself and validate files
            // first, which is why that case gets double the budget.
            var timeout = plan.ViaSteam && !steamWasRunning
                ? TimeSpan.FromSeconds(240)
                : TimeSpan.FromSeconds(120);

            var game = await WaitForGameProcessAsync(processName, timeout, progress, ct);
            if (game is null)
            {
                // The game never started. Bail out — the finally below restores
                // everything, so the user is left exactly as they were.
                return new CompetitiveStartResult(
                    CompetitiveStartOutcome.GameNotFound, null, null);
            }
            else
            {
                if (options.BoostGamePriority)
                {
                    try
                    {
                        game.PriorityClass = options.PriorityLevel == GamePriorityLevel.High
                            ? ProcessPriorityClass.High
                            : ProcessPriorityClass.AboveNormal;
                    }
                    catch { failures.Add("priority class"); }

                    try
                    {
                        if (!ProcessTuning.SetIoPriority(game.Id, options.IoPriority))
                            failures.Add("I/O priority");
                    }
                    catch { failures.Add("I/O priority"); }

                    try
                    {
                        if (!ProcessTuning.SetDefaultCpuSets(game.Id, options.CpuSets))
                            failures.Add("CPU sets");
                    }
                    catch { failures.Add("CPU sets"); }
                }

                if (options.ConsistentPerformance)
                {
                    try
                    {
                        if (!ProcessTuning.DisablePowerThrottling(game.Id))
                            failures.Add("power throttling opt-out");
                    }
                    catch { failures.Add("power throttling opt-out"); }
                }
            }

            // ── 6. Standby memory ─────────────────────────────────────────────
            if (options.ClearStandbyMemory)
            {
                progress?.Report("Clearing standby memory…");
                try
                {
                    if (!await Task.Run(ProcessTuning.ClearStandbyList, ct))
                        failures.Add("standby list purge");
                }
                catch { failures.Add("standby list purge"); }
            }

            // ── 7. Persist the final state ────────────────────────────────────
            state = state with { TuningFailures = failures };
            CompetitiveSessionStore.Save(state);

            _current = state;
            StartExitWatcher(state);

            // Only now is the session real: the game is running and the watcher
            // owns the restore. Past this point the finally must not undo anything.
            confirmed = true;

            progress?.Report("Competitive Mode active.");
            return new CompetitiveStartResult(CompetitiveStartOutcome.Started, state, null);
            }
            catch (OperationCanceledException)
            {
                return new CompetitiveStartResult(CompetitiveStartOutcome.Cancelled, null, null);
            }
            catch (Exception ex)
            {
                return new CompetitiveStartResult(CompetitiveStartOutcome.Error, null, ex.Message);
            }
            finally
            {
                // Guaranteed restore for every path that did not reach a confirmed
                // running game — timeout, cancellation or exception alike.
                if (!confirmed)
                {
                    try { await EndAsync(state, progress); } catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Final undo failed: {ex.Message}"); /* the failure path must not throw a second time */ }

                }
            }
        }

        // ── Background app suspension ─────────────────────────────────────────

        private static IReadOnlyList<SuspendedProcess> SuspendBackgroundApps(string gameProcessName)
        {
            var suspended = new List<SuspendedProcess>();

            int ownPid       = Environment.ProcessId;
            int ownSessionId = GetOwnSessionId();

            foreach (string name in SuspendableNames)
            {
                Process[] matches;
                try { matches = Process.GetProcessesByName(name); }
                catch { continue; }

                foreach (var p in matches)
                {
                    try
                    {
                        if (!IsSuspendable(p, gameProcessName, ownPid, ownSessionId)) continue;

                        // Name captured before the suspend: reading it afterwards can
                        // throw once the process is frozen or has exited.
                        string procName = p.ProcessName;
                        if (ProcessSuspender.Suspend(p.Id))
                            suspended.Add(new SuspendedProcess(p.Id, procName));
                    }
                    catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Process vanished: {ex.Message}"); /* process vanished mid-loop — skip it */ }
                    finally { try { p.Dispose(); } catch { } }
                }
            }

            return suspended;
        }

        /// <summary>
        /// The hard-exclusion gate. Applied to every candidate regardless of how it
        /// got into the list.
        /// </summary>
        private static bool IsSuspendable(Process p, string gameProcessName, int ownPid, int ownSessionId)
        {
            try
            {
                if (p.Id == ownPid) return false;                                  // Akari Tool itself
                if (p.Id <= 4)      return false;                                  // System / Idle

                string name = p.ProcessName;
                if (HardExclusions.Contains(name)) return false;
                if (string.Equals(name, gameProcessName, StringComparison.OrdinalIgnoreCase))
                    return false;                                                  // the target game

                // A process in another session belongs to another logged-in user (or
                // to session 0's services); freezing it is never ours to do.
                    if (p.SessionId != ownSessionId) return false;

                return true;
            }
            catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] IsSuspendable check failed: {ex.Message}"); return false; }
        }

        private static int GetOwnSessionId()
        {
            try { using var self = Process.GetCurrentProcess(); return self.SessionId; }
            catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] GetOwnSessionId failed: {ex.Message}"); return -1; }
        }

        // ── Services ──────────────────────────────────────────────────────────

        private static IReadOnlyList<StoppedService> StopSessionServices()
        {
            var stopped = new List<StoppedService>();

            foreach (string name in SessionServices)
            {
                try
                {
                    string? priorStart = ReadServiceStartType(name);
                    if (priorStart is null)
                    {
                        ToolService.Current?.Log($"[CompetitiveService] Service {name} not found in registry");
                        continue;   // not present on this machine
                    }

                    bool wasRunning = false;
                    try
                    {
                        using var sc = new ServiceController(name);
                        wasRunning = sc.Status is ServiceControllerStatus.Running
                                                or ServiceControllerStatus.StartPending;
                    }
                    catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Service status check failed: {ex.Message}"); }

                    // Record before mutating.
                    stopped.Add(new StoppedService(name, priorStart, wasRunning));

                    // Disable first: WSearch and SysMain are both restarted
                    // automatically by triggers, so stopping alone does not keep
                    // them down for the length of a match.
                    try { WriteServiceStartType(name, ServiceDisabled); } catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Service disable failed: {ex.Message}"); }

                    if (wasRunning)
                    {
                        try
                        {
                            using var sc = new ServiceController(name);
                            if (sc.CanStop)
                            {
                                sc.Stop();
                                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                            }
                        }
                        catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Service stop failed: {ex.Message}"); /* still recorded — restore will put the config back */ }
                    }
                }
                catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Service iteration failed: {ex.Message}"); /* one bad service must not stop the rest */ }
            }

            return stopped;
        }

        private static string? ReadServiceStartType(string name)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesKeyRoot}\{name}");
                if (key?.GetValue("Start") is int start) return start.ToString();
                return null;
            }
            catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Registry read failed: {ex.Message}"); return null; }
        }

        private static void WriteServiceStartType(string name, int start)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"{ServicesKeyRoot}\{name}", writable: true);
                key?.SetValue("Start", start, RegistryValueKind.DWord);
            }
            catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Registry write failed: {ex.Message}"); }
        }

        // ── Power plan ────────────────────────────────────────────────────────
        // powercfg is invoked directly (never through cmd.exe, which mangles
        // /SWITCHES). PowerTab has similar helpers but they are private members of
        // a UserControl and are not reachable from a service.

        private static string RunPowerCfg(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", args)
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                using var p = Process.Start(psi)!;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10_000);
                return output;
            }
            catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] PowerCfg execution failed: {ex.Message}"); return ""; }
        }

        /// <summary>Active scheme GUID, parsed out of powercfg /GETACTIVESCHEME.</summary>
        private static string? GetActiveSchemeGuid()
        {
            string output = RunPowerCfg("/GETACTIVESCHEME");
            int i = output.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;

            string rest = output[(i + 5)..].TrimStart();
            string guid = rest.Split(' ', '\r', '\n')[0].Trim();
            return guid.Length == 36 ? guid : null;
        }

        private static List<(string Guid, string Name)> ListSchemes()
        {
            var result = new List<(string, string)>();
            foreach (string line in RunPowerCfg("/LIST").Split('\n'))
            {
                int gi = line.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
                if (gi < 0) continue;

                string rest = line[(gi + 5)..].Trim();
                string guid = rest.Split(' ')[0].Trim();
                if (guid.Length != 36) continue;

                string name = "";
                int p1 = rest.IndexOf('(');
                int p2 = rest.LastIndexOf(')');
                if (p1 >= 0 && p2 > p1) name = rest[(p1 + 1)..p2].Trim();

                result.Add((guid, name));
            }
            return result;
        }

        private const string HighPerfGuid     = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string UltimatePerfGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        /// <summary>
        /// Ultimate Performance if it exists on this machine, else High Performance.
        /// Never creates a plan — /duplicatescheme would leave a permanent artifact
        /// behind for what is meant to be a temporary session change.
        /// </summary>
        private static string? FindPerformanceSchemeGuid()
        {
            var schemes = ListSchemes();

            foreach (var (guid, name) in schemes)
                if (guid.Equals(UltimatePerfGuid, StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
                    return guid;

            foreach (var (guid, name) in schemes)
                if (guid.Equals(HighPerfGuid, StringComparison.OrdinalIgnoreCase)
                    || name.Contains("High performance", StringComparison.OrdinalIgnoreCase))
                    return guid;

            return null;
        }

        private static bool SetActiveScheme(string guid)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", $"/SETACTIVE {guid}")
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                };
                 using var p = Process.Start(psi)!;
                p.WaitForExit(10_000);
                return p.ExitCode == 0;
            }
            catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] PowerCfg set failed: {ex.Message}"); return false; }
        }

        /// <summary>Human-readable name of a scheme GUID, for the status list.</summary>
        public static string DescribeScheme(string? guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return "unchanged";
            foreach (var (g, name) in ListSchemes())
                if (g.Equals(guid, StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(name) ? guid : name;
            return guid;
        }

        // ── Game-exit watcher ─────────────────────────────────────────────────

        private static Process? FindGameProcess(string processName)
        {
            try { return Process.GetProcessesByName(processName).FirstOrDefault(); }
            catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Process retrieval failed: {ex.Message}"); return null; }
        }

        /// <summary>
        /// Polls for the game once a second up to <paramref name="timeout"/>,
        /// reporting the remaining budget so a long Steam cold-start does not look
        /// like a hang. Null on timeout.
        /// </summary>
        private static async Task<Process?> WaitForGameProcessAsync(
            string processName, TimeSpan timeout, IProgress<string>? progress, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                var found = FindGameProcess(processName);
                if (found is not null) return found;

                int remaining = (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds);
                progress?.Report($"Waiting for {processName}… ({remaining}s)");

                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

            return FindGameProcess(processName);   // one last look at the deadline
        }

        // ── Launch method ─────────────────────────────────────────────────────

        /// <summary>How a given exe will be started.</summary>
        public readonly record struct LaunchPlan(bool ViaSteam, uint AppId);

        /// <summary>
        /// Decides between steam://rungameid and a direct exe launch.
        ///
        /// Steam games started by their raw .exe come up with no app context — no
        /// SteamAppId handoff and no auth ticket — so ownership checks fail with
        /// "invalid or missing authentication token". Going through the protocol
        /// handler lets Steam set that up.
        /// </summary>
        public static LaunchPlan ResolveLaunch(string gameExePath, CompetitiveOptions options)
        {
            if (!options.LaunchThroughSteam) return new LaunchPlan(false, 0);

            try
            {
                if (SteamLibrary.TryGetSteamAppId(gameExePath, out uint appId))
                    return new LaunchPlan(true, appId);
            }
            catch { }

            return new LaunchPlan(false, 0);
        }

        private static ProcessStartInfo BuildLaunchStartInfo(string gameExePath, LaunchPlan plan)
        {
            if (plan.ViaSteam)
                // No WorkingDirectory: the protocol handler ignores it, and Steam
                // sets the game's own CWD correctly.
                return new ProcessStartInfo($"steam://rungameid/{plan.AppId}")
                {
                    UseShellExecute = true,
                };

            return new ProcessStartInfo
            {
                FileName         = gameExePath,
                UseShellExecute  = true,
                // Many games resolve data files relative to the CWD and will crash
                // on startup without this.
                WorkingDirectory = Path.GetDirectoryName(gameExePath) ?? "",
            };
        }

        private static bool IsSteamClientRunning()
        {
            try { return Process.GetProcessesByName("steam").Length > 0; }
            catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Steam client check failed: {ex.Message}"); return false; }
        }

        /// <summary>
        /// Polls for the game every 3s and ends the session once it has been gone
        /// for 30 consecutive seconds.
        ///
        /// The grace period exists because launcher-bootstrapped games (Steam,
        /// Battle.net, EA App) hand off to a different PID and the process we
        /// launched exits immediately — without it, every such game would end its
        /// own session seconds after starting. The clock only starts once the game
        /// has been seen at least once, so a slow-loading title is not cut off
        /// before it ever appears.
        /// </summary>
        private static void StartExitWatcher(CompetitiveSessionState state)
        {
            _watcherCts?.Cancel();
            var cts = new CancellationTokenSource();
            _watcherCts = cts;

            _ = Task.Run(async () =>
            {
                var ct = cts.Token;
                bool everSeen = false;
                DateTime? goneSince = null;

                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);

                        bool running;
                        try { running = Process.GetProcessesByName(state.GameProcessName).Length > 0; }
                        catch { running = true; }   // unreadable ≠ exited

                        if (running)
                        {
                            everSeen  = true;
                            goneSince = null;
                            continue;
                        }

                        if (!everSeen) continue;    // still loading — grace clock not started

                        goneSince ??= DateTime.UtcNow;
                        if (DateTime.UtcNow - goneSince.Value < TimeSpan.FromSeconds(30)) continue;

                        // Gone for 30s straight — the session is over.
                        var ending = _current;
                        if (ending is not null) await EndAsync(ending, null);
                        try { SessionEndedByGameExit?.Invoke(); } catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Game exit signal failed: {ex.Message}"); }
                        return;
                    }
                }
                catch (OperationCanceledException) { /* EndAsync called from the UI */ }
                catch { /* the watcher must never crash the app */ }
            });
        }

        // ── End ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Undoes everything the session recorded. Every step is independently
        /// try/caught: a failure resuming one process must not leave the power plan
        /// or the services unrestored.
        /// </summary>
        public static Task EndAsync(CompetitiveSessionState state, IProgress<string>? progress)
        {
            _watcherCts?.Cancel();
            _watcherCts = null;
            _current    = null;

            return Task.Run(() =>
            {
                // 1. Resume suspended processes.
                try
                {
                    progress?.Report("Resuming background apps…");
                    foreach (var sp in state.SuspendedProcesses)
                        try { ProcessSuspender.Resume(sp.Pid); } catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Process resume failed: {ex.Message}"); }
                }
                catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Resume phase failed: {ex.Message}"); }

                // 2. Restore services.
                try
                {
                    progress?.Report("Restoring services…");
                    foreach (var svc in state.StoppedServices)
                    {
                        try
                        {
                            if (int.TryParse(svc.PriorStartType, out int start))
                                WriteServiceStartType(svc.Name, start);

                            if (svc.WasRunning)
                            {
                                using var sc = new ServiceController(svc.Name);
                                if (sc.Status == ServiceControllerStatus.Stopped)
                                {
                                    sc.Start();
                                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                                }
                            }
                        }
                        catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Service restore failed: {ex.Message}"); /* next service */ }
                    }
                }
                catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Service phase failed: {ex.Message}"); }

                // 3. Restore the power plan.
                try
                {
                    if (!string.IsNullOrWhiteSpace(state.PreviousPowerSchemeGuid))
                    {
                        progress?.Report("Restoring power plan…");
                        SetActiveScheme(state.PreviousPowerSchemeGuid!);
                    }
                }
                catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Power plan restore failed: {ex.Message}"); }

                // 4. Drop the record last — only once the undo has been attempted.
                try { CompetitiveSessionStore.Clear(); } catch (Exception ex) { ToolService.Current?.Log($"[CompetitiveService] Session store clear failed: {ex.Message}"); }

                progress?.Report("Competitive Mode ended.");
            });
        }
    }
}
