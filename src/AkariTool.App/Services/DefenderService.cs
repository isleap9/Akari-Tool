using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using AkariTool.Tabs;   // TweakHelpers

namespace AkariTool.Services
{
    /// <summary>
    /// Two-phase Windows Defender disable/enable engine, extracted from the AkariOS tab.
    ///
    /// Phase 1 (interactive, admin): checks Tamper Protection, drops the embedded
    /// NoDefender.cab to C:\Windows, runs the embedded DisableDefender.ps1 (which removes
    /// the Defender servicing package via DISM), and schedules a headless post-reboot
    /// phase-2 run through <see cref="DefenderPhase2Scheduler"/>.
    ///
    /// Phase 2 (headless, post-reboot — <see cref="RunPhase2Native"/>): the Defender
    /// service keys are ELAM-protected at runtime and cannot be written while Defender is
    /// loaded. After phase-1's package removal + reboot ELAM no longer locks them, so
    /// phase-2 writes them natively as SYSTEM via <see cref="ElevationService.RunAsSystem"/>
    /// — no PowerRun, no MinSudo, no generated .bat.
    ///
    /// The payload (cab + ps1) is embedded in the assembly, so this path has no dependency
    /// on PostInstallService / the downloaded PostInstall folder.
    /// </summary>
    public static class DefenderService
    {
        private static readonly string WinNoDefenderCab = @"C:\Windows\NoDefender.cab";

        private static readonly string[] DefenderServices =
        {
            "MsSecCore","MsSecFlt","MsSecWfp","SecurityHealthService",
            "Sense","WdBoot","WdFilter","WdNisDrv","WdNisSvc",
            "WinDefend","wscsvc","MDCoreSvc","SgrmAgent","SgrmBroker",
            "webthreatdefsvc","webthreatdefusersvc",
        };

        private static readonly string[] DefenderScheduledTasks =
        {
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Verification",
        };

        // ═════════════════════════════════════════════════════════════════════════
        // PUBLIC ENTRY — phase-1 orchestration (disable) / full restore (enable)
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Disables (phase 1 + schedules phase 2) or re-enables Windows Defender.
        /// Returns true when the requested phase completed without an exception.
        /// </summary>
        public static async Task<bool> SetAsync(bool disable, ToolService log)
        {
            try
            {
                if (disable)
                {
                    if (TweakHelpers.HasState("DisableDefender")) return true;
                    log.Log("[DEFENDER] Disabling Windows Defender...");
                    log.Log("[DEFENDER] Checking Tamper Protection status...");
                    if (IsTamperProtectionEnabled())
                    {
                        log.Log("[DEFENDER] ERROR: Tamper Protection is ON.");
                        log.Log("[DEFENDER] Go to: Windows Security → Virus & threat protection");
                        log.Log("[DEFENDER]   → Manage settings → Tamper Protection → Off");
                        log.Log("[DEFENDER] Then try again.");
                        return false;
                    }
                    log.Log("[DEFENDER] Tamper Protection is off — proceeding.");

                    log.Log("[DEFENDER] Preparing NoDefender package...");
                    var cabTemp = await ExtractEmbeddedAsync(".NoDefender.cab", "NoDefender.cab");
                    var ps1Temp = await ExtractEmbeddedAsync(".DisableDefender.ps1", "DisableDefender.ps1");
                    try
                    {
                        File.Copy(cabTemp, WinNoDefenderCab, overwrite: true);
                        log.Log("[DEFENDER] Installing NoDefender (30–60s)...");
                        await RunElevatedPsFileAsync(ps1Temp);
                    }
                    finally
                    {
                        try { File.Delete(cabTemp); } catch { }
                        try { File.Delete(ps1Temp); } catch { }
                    }

                    log.Log("[DEFENDER] Scheduling native post-reboot phase 2...");
                    DefenderPhase2Scheduler.ScheduleRunOnce();
                    TweakHelpers.SaveState("DisableDefender");
                    log.Log("[DEFENDER] Phase 1 complete. Please restart now.");
                    log.Log("[DEFENDER] On next login, Phase 2 will run automatically to finish disabling Defender.");
                    return true;
                }
                else
                {
                    log.Log("[DEFENDER] Re-enabling Windows Defender...");
                    log.Log("[DEFENDER] Restoring Defender package (30–60s)...");
                    await RunElevatedPsAsync(
                        $"if (Test-Path '{WinNoDefenderCab}') {{ Remove-WindowsPackage -Online -PackagePath '{WinNoDefenderCab}' -NoRestart }}");

                    log.Log("[DEFENDER] Restoring Defender services (native SYSTEM writes)...");
                    ElevationService.RunAsSystem(() =>
                    {
                        foreach (var svc in DefenderServices)
                        {
                            try
                            {
                                Registry.SetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\{svc}",
                                    "Start", 2, RegistryValueKind.DWord);
                                log.Log($"[DEFENDER]   {svc} Start=2 OK");
                            }
                            catch (Exception ex) { log.Log($"[DEFENDER]   {svc} FAILED — {ex.Message}"); }
                        }
                    }, log.Log);

                    log.Log("[DEFENDER] Restoring Security notification icon and scheduled tasks...");
                    // SecurityHealth Run entry: REG_EXPAND_SZ with %windir%, matching stock Windows 11.
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        "SecurityHealth", @"%windir%\system32\SecurityHealthSystray.exe", RegistryValueKind.ExpandString);
                    foreach (var task in DefenderScheduledTasks)
                        RunProcess("schtasks.exe", $"/change /enable /TN \"{task}\"", log.Log);

                    TryStartSecurityHealthSystray(log);
                    TweakHelpers.ClearState("DisableDefender");
                    log.Log("[DEFENDER] Defender re-enabled. Restart required.");
                    return true;
                }
            }
            catch (Exception ex) { log.Log($"[DEFENDER] ERROR: {ex.Message}"); return false; }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // PHASE 2 — native SYSTEM writes (called by the headless post-reboot runner)
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The real post-reboot work: with ELAM lifted, write every Defender service key to
        /// Start=4, disable real-time monitoring, strip SmartScreen (registry + binary
        /// takeover), set CI/policy + DeviceGuard keys, and disable the Defender scheduled
        /// tasks — all inside a single SYSTEM impersonation. Every step is logged; a failure
        /// on one service does not abort the loop. Headless-safe (no UI).
        /// </summary>
        public static void RunPhase2Native(Action<string> log)
        {
            bool ok = ElevationService.RunAsSystem(() =>
            {
                log($"[PHASE2] Identity inside RunAsSystem: {System.Security.Principal.WindowsIdentity.GetCurrent().Name}");

                // 1. ELAM service keys → Start=4 (disabled). Post-reboot these are writable.
                log("[PHASE2] Setting Defender service keys to Start=4...");
                foreach (var svc in DefenderServices)
                {
                    try
                    {
                        Registry.SetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\{svc}",
                            "Start", 4, RegistryValueKind.DWord);
                        log($"[PHASE2]   {svc} Start=4 OK");
                    }
                    catch (Exception ex) { log($"[PHASE2]   {svc} Start=4 FAILED — {ex.Message}"); }
                }

                // 2. Disable real-time monitoring (admin PS; harmless inside SYSTEM context).
                log("[PHASE2] Disabling real-time monitoring...");
                RunProcess("powershell.exe",
                    "-NonInteractive -NoLogo -NoProfile -C \"Set-MpPreference -DisableRealtimeMonitoring 1\"", log);

                // 3. Remove SecurityHealth systray Run entry.
                log("[PHASE2] Removing SecurityHealth Run entry...");
                try
                {
                    using var run = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
                    run?.DeleteValue("SecurityHealth", false);
                }
                catch (Exception ex) { log($"[PHASE2]   SecurityHealth delete FAILED — {ex.Message}"); }

                // 4. SmartScreen takeover — registry (native) + binary rename (stock tools).
                log("[PHASE2] Disabling SmartScreen...");
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                    "SmartScreenEnabled", "Off", RegistryValueKind.String);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\System",
                    "EnableSmartScreen", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows Defender\SmartScreen",
                    "ConfigureAppInstallControlEnabled", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows Defender\SmartScreen",
                    "EnableSmartScreen", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AppHost",
                    "EnableWebContentEvaluation", 0, RegistryValueKind.DWord);

                var sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System));
                var smartScreen    = Path.Combine(sys32, "smartscreen.exe");
                var smartScreenOld  = Path.Combine(sys32, "smartscreen.exe.old");
                RunProcess("taskkill.exe", "/f /im smartscreen.exe", log);
                if (!File.Exists(smartScreenOld) && File.Exists(smartScreen))
                {
                    RunProcess("takeown.exe", $"/F \"{smartScreen}\" /A", log);
                    RunProcess("icacls.exe", $"\"{smartScreen}\" /grant Administrators:F", log);
                    try
                    {
                        File.Copy(smartScreen, smartScreenOld, overwrite: false);
                        File.Delete(smartScreen);
                        log("[PHASE2]   smartscreen.exe renamed to .old");
                    }
                    catch (Exception ex) { log($"[PHASE2]   smartscreen.exe takeover FAILED — {ex.Message}"); }
                }

                // 5. CI/Policy + DeviceGuard keys.
                log("[PHASE2] Setting CI/Policy and DeviceGuard keys...");
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\CI\Policy",
                    "VerifiedAndReputablePolicyState", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows Defender",
                    "PUAProtection", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\CI\Config",
                    "VulnerableDriverBlocklistEnable", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                    "Enabled", 0, RegistryValueKind.DWord);

                // 6. Disable Defender scheduled tasks.
                log("[PHASE2] Disabling Defender scheduled tasks...");
                foreach (var task in DefenderScheduledTasks)
                    RunProcess("schtasks.exe", $"/change /disable /TN \"{task}\"", log);
            }, log);

            log(ok ? "[PHASE2] Native phase-2 SYSTEM block completed."
                   : "[PHASE2] Native phase-2 FAILED to acquire SYSTEM — see errors above.");
        }

        // ═════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extracts an embedded resource (matched by a suffix such as ".NoDefender.cab")
        /// to a file in the temp folder and returns its path. Self-contained — no
        /// PostInstall folder dependency.
        /// </summary>
        private static async Task<string> ExtractEmbeddedAsync(string endsWith, string destFileName)
        {
            var asm = typeof(DefenderService).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException($"Embedded resource not found: {endsWith}");
            var dest = Path.Combine(Path.GetTempPath(), destFileName);
            await using var rs = asm.GetManifestResourceStream(name)!;
            await using var fs = File.Create(dest);
            await rs.CopyToAsync(fs);
            return dest;
        }

        /// <summary>True unless TamperProtection == 4 (disabled). Fails closed (true) on error.</summary>
        public static bool IsTamperProtectionEnabled()
        {
            try
            {
                var val = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features", "TamperProtection", null);
                return val is not int i || i != 4;
            }
            catch { return true; }
        }

        /// <summary>Runs a .ps1 file elevated (UAC prompt), waits for exit. Plain admin, not a shim.</summary>
        private static async Task RunElevatedPsFileAsync(string ps1Path)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -NonInteractive -NoLogo -NoProfile -File \"{ps1Path}\"",
                UseShellExecute = true, Verb = "runas", CreateNoWindow = false
            };
            await Process.Start(psi)!.WaitForExitAsync();
        }

        /// <summary>Runs an inline PowerShell command (already-elevated process), waits for exit.</summary>
        private static async Task RunElevatedPsAsync(string command)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NonInteractive -NoLogo -NoProfile -C \"{command}\"",
                UseShellExecute = false, CreateNoWindow = true
            };
            await Process.Start(psi)!.WaitForExitAsync();
        }

        /// <summary>Synchronous process run for the headless/native path; logs a non-zero exit code.</summary>
        private static void RunProcess(string file, string args, Action<string> log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file, Arguments = args,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                using var proc = Process.Start(psi)!;
                proc.WaitForExit();
                if (proc.ExitCode != 0) log($"[PHASE2]   '{file} {args}' exit code {proc.ExitCode}");
            }
            catch (Exception ex) { log($"[PHASE2]   '{file} {args}' threw — {ex.Message}"); }
        }

        /// <summary>Best-effort immediate relaunch of the Security tray icon after re-enable.</summary>
        private static void TryStartSecurityHealthSystray(ToolService log)
        {
            try
            {
                var exe = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "SecurityHealthSystray.exe");
                if (!File.Exists(exe))
                {
                    log.Log("[DEFENDER] SecurityHealthSystray.exe not found — icon will appear after restart.");
                    return;
                }
                Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
                log.Log("[DEFENDER] Security notification icon relaunched.");
            }
            catch (Exception ex)
            {
                log.Log($"[DEFENDER] Could not relaunch tray icon ({ex.Message}) — it will appear after restart.");
            }
        }
    }
}
