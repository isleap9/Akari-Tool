using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Tabs;

namespace AkariTool.Services
{
    /// <summary>
    /// Native replacements for the PostInstall batch files: Alt-Tab style,
    /// time resync, and DMA remapping. Registry + stock Windows binaries only —
    /// no PostInstall folder, no NSudo/MinSudo.
    ///
    /// Every method catches all exceptions, logs, and never throws.
    /// </summary>
    public static class SystemUtilities
    {
        // ── Alt-Tab ───────────────────────────────────────────────────────
        // Classic:   AltTabSettings=1, HoverSelectDesktops=0
        // Immersive: AltTabSettings=0, HoverSelectDesktops=1  (Windows default)

        private const string ExplorerKey =
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer";
        private const string ExplorerSubKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer";

        /// <summary>true = Classic, false = Immersive, null = unset.</summary>
        public static bool? ReadAltTabClassic()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(ExplorerSubKey);
                return k?.GetValue("AltTabSettings") is int v ? v == 1 : null;
            }
            catch { return null; }
        }

        public static void SetAltTabClassic(bool classic, Action<string> log)
        {
            try
            {
                Registry.SetValue(ExplorerKey, "AltTabSettings",      classic ? 1 : 0, RegistryValueKind.DWord);
                Registry.SetValue(ExplorerKey, "HoverSelectDesktops", classic ? 0 : 1, RegistryValueKind.DWord);

                // Request() restarts now, or coalesces into the batch if one is open.
                ExplorerRestart.Request();
                log($"Alt-Tab set to {(classic ? "Classic" : "Immersive")}. Explorer restarted.");
            }
            catch (Exception ex)
            {
                log($"ERROR setting Alt-Tab style: {ex.Message}");
            }
        }

        // ── Time Resync ───────────────────────────────────────────────────

        /// <summary>
        /// Temporarily enables w32time, resyncs, then restores it to disabled.
        /// Mirrors resync.bat. Runs off the UI thread.
        /// </summary>
        public static async Task ResyncTimeAsync(Action<string> log)
        {
            log("Resyncing system time...");
            try
            {
                await RunAsync("sc.exe",   "config w32time start= auto", log);
                await RunAsync("net.exe",  "start w32time",              log);

                // The only step whose failure is worth calling out: no network or
                // no reachable time source leaves the clock untouched.
                var (code, output) = await RunAsync("w32tm.exe", "/resync", log);
                if (code != 0)
                    log($"w32tm /resync reported failure (exit {code}) — no network or no reachable time source. {output}".TrimEnd());

                await RunAsync("net.exe", "stop w32time",              log);
                await RunAsync("sc.exe",  "config w32time start= disabled", log);

                log("Time resync complete. Windows Time service left disabled.");
            }
            catch (Exception ex)
            {
                log($"ERROR during time resync: {ex.Message}");
            }
        }

        /// <summary>Runs a console binary to completion off the UI thread. Failures are logged, never thrown.</summary>
        private static async Task<(int ExitCode, string Output)> RunAsync(string exe, string args, Action<string> log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = exe,
                    Arguments              = args,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };

                using var p = Process.Start(psi)!;
                string stdout = await p.StandardOutput.ReadToEndAsync();
                string stderr = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();

                return (p.ExitCode, (stdout + stderr).Trim());
            }
            catch (Exception ex)
            {
                log($"ERROR running {exe} {args}: {ex.Message}");
                return (-1, string.Empty);
            }
        }

        // ── DMA Remapping ─────────────────────────────────────────────────

        private const string ServicesSubKey = @"SYSTEM\CurrentControlSet\Services";
        private const string DmaValueName   = "DmaRemappingCompatible";

        /// <summary>
        /// Recursively finds every "DmaRemappingCompatible" value under
        /// HKLM\SYSTEM\CurrentControlSet\Services and sets it to 1 (enabled) or 0 (disabled).
        /// Replaces the batch file's `reg query /s /f | findstr` loop with native enumeration.
        /// </summary>
        public static void SetDmaRemapping(bool enabled, Action<string> log)
        {
            try
            {
                int changed = 0;

                // Some driver keys under Services are ACL-protected against Administrator.
                bool elevated = ElevationService.RunAsSystem(() =>
                {
                    using var services = Registry.LocalMachine.OpenSubKey(ServicesSubKey);
                    if (services is null)
                    {
                        log("Could not open the Services registry key.");
                        return;
                    }
                    changed = WalkAndSet(services, enabled ? 1 : 0, 0);
                }, log);

                if (!elevated)
                {
                    log("Could not impersonate SYSTEM — DMA remapping not changed.");
                    return;
                }

                log($"DMA remapping {(enabled ? "enabled" : "disabled")} on {changed} device(s). Restart required.");
            }
            catch (Exception ex)
            {
                log($"ERROR setting DMA remapping: {ex.Message}");
            }
        }

        // Depth cap: the Services tree is shallow (Service\Parameters\...), so this is
        // well clear of real data while still guarding against a pathological hive.
        private const int MaxDepth = 8;

        /// <summary>Recursively writes <paramref name="value"/> to every DmaRemappingCompatible found. Returns the count written.</summary>
        private static int WalkAndSet(RegistryKey key, int value, int depth)
        {
            if (depth > MaxDepth) return 0;

            int count = 0;

            // Write here if this key carries the value at all.
            try
            {
                if (key.GetValue(DmaValueName) is not null)
                {
                    key.SetValue(DmaValueName, value, RegistryValueKind.DWord);
                    count++;
                }
            }
            catch { /* per-key: one ACL failure must not abort the walk */ }

            string[] subNames;
            try { subNames = key.GetSubKeyNames(); }
            catch { return count; }

            foreach (var name in subNames)
            {
                try
                {
                    using var sub = key.OpenSubKey(name, writable: true);
                    if (sub is not null) count += WalkAndSet(sub, value, depth + 1);
                }
                catch { /* unreadable/unwritable subtree — skip it and keep going */ }
            }

            return count;
        }
    }
}
