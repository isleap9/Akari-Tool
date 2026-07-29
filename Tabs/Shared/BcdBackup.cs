using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Backs up relevant BCD (Boot Configuration Data) values before applying
    /// bcdedit tweaks, and restores them on undo.
    ///
    /// BCD is NOT covered by System Restore, so this is the only way to
    /// provide a reliable undo for bcdedit changes.
    ///
    /// Backed-up values stored at: HKCU\Software\AkariTool\BcdBackup
    /// </summary>
    public static class BcdBackup
    {
        private const string BackupKeyPath = @"Software\AkariTool\BcdBackup";

        // The BCD values we touch and their safe stock defaults
        private static readonly Dictionary<string, string> StockDefaults = new()
        {
            ["bootmenupolicy"]      = "Standard",
            ["disabledynamictick"]  = "No",
            ["recoveryenabled"]     = "Yes",
            ["disableelamdrivers"]  = "No",    // we don't apply this but back it up anyway
        };

        // ── Public API ────────────────────────────────────────────────────────

        public static bool HasBackup => Registry.CurrentUser
            .OpenSubKey(BackupKeyPath) is not null;

        /// <summary>
        /// Synchronous, best-effort backup for sync tweak rows (Apply is Action&lt;bool&gt;).
        /// No-op if a backup already exists. Call this BEFORE writing with bcdedit.
        /// </summary>
        public static void EnsureBackup()
        {
            if (HasBackup) return;
            try
            {
                var current = ReadCurrentBcdValues();
                using var key = Registry.CurrentUser.CreateSubKey(BackupKeyPath, writable: true)!;
                foreach (var (name, value) in current)
                    key.SetValue(name, value, RegistryValueKind.String);
            }
            catch { /* best-effort */ }
        }

        /// <summary>
        /// Reads current BCD values and saves them to registry.
        /// Call this BEFORE applying any bcdedit changes.
        /// </summary>
        public static async Task BackupAsync(ToolService log)
        {
            log.Log("[BCD] Backing up current BCD values...");

            var current = await ReadCurrentBcdValuesAsync();

            using var key = Registry.CurrentUser.CreateSubKey(BackupKeyPath, writable: true)!;
            foreach (var (name, value) in current)
            {
                key.SetValue(name, value, RegistryValueKind.String);
                log.Log($"[BCD] Backed up {name} = {value}");
            }

            log.Log("[BCD] Backup complete.");
        }

        /// <summary>
        /// Applies the Akari Tool BCD tweaks (safe subset only).
        /// Always call BackupAsync() first.
        /// </summary>
        public static async Task ApplyAsync(ToolService log, BcdTweakOptions options)
        {
            log.Log("[BCD] Applying BCD tweaks...");

            var commands = new List<(string arg, string description)>();

            if (options.LegacyBootMenu)
                commands.Add(("/set bootmenupolicy Legacy", "Legacy boot menu (F8 key)"));

            if (options.DisableDynamicTick)
                commands.Add(("/set disabledynamictick yes", "Disable dynamic tick (better timer resolution)"));

            if (options.DisableRecovery)
                commands.Add(("/set {current} recoveryenabled no", "Disable recovery (faster boot)"));

            if (options.DisableHibernation)
            {
                // powercfg not bcdedit, but logically grouped here
                await RunCommandAsync("powercfg", "-h off", log);
                log.Log("[BCD] Hibernation disabled (hiberfil.sys removed)");
            }

            foreach (var (arg, desc) in commands)
            {
                bool ok = await RunBcdeditAsync(arg, log);
                log.Log(ok ? $"[BCD] ✓ {desc}" : $"[BCD] ✗ Failed: {desc}");
            }

            TweakHelpers.SaveState("BcdTweaksApplied");
            log.Log("[BCD] BCD tweaks applied. Restart to take full effect.");
        }

        /// <summary>
        /// Restores BCD values from registry backup, or falls back to stock defaults.
        /// </summary>
        public static async Task RestoreAsync(ToolService log)
        {
            log.Log("[BCD] Restoring BCD values...");

            Dictionary<string, string> values;

            using var backupKey = Registry.CurrentUser.OpenSubKey(BackupKeyPath);
            if (backupKey is not null)
            {
                values = new Dictionary<string, string>();
                foreach (var name in backupKey.GetValueNames())
                    values[name] = backupKey.GetValue(name)?.ToString() ?? "";
                log.Log("[BCD] Using saved backup values.");
            }
            else
            {
                values = StockDefaults;
                log.Log("[BCD] No backup found — using stock defaults.");
            }

            // Restore bootmenupolicy
            if (values.TryGetValue("bootmenupolicy", out var policy))
                await RunBcdeditAsync($"/set bootmenupolicy {policy}", log);

            // Restore disabledynamictick
            if (values.TryGetValue("disabledynamictick", out var dynTick))
                await RunBcdeditAsync($"/set disabledynamictick {dynTick}", log);

            // Restore recoveryenabled
            if (values.TryGetValue("recoveryenabled", out var recovery))
                await RunBcdeditAsync($"/set {{current}} recoveryenabled {recovery}", log);

            // Restore hibernation (re-enable)
            await RunCommandAsync("powercfg", "-h on", log);

            TweakHelpers.ClearState("BcdTweaksApplied");
            log.Log("[BCD] BCD restored. Restart to take full effect.");
        }

        // ── BCD reader ────────────────────────────────────────────────────────

        private static async Task<Dictionary<string, string>> ReadCurrentBcdValuesAsync()
        {
            var result = new Dictionary<string, string>(StockDefaults); // start with defaults

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "bcdedit",
                    Arguments              = "/enum {current}",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow         = true,
                };

                var process   = Process.Start(psi)!;
                var output    = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                ParseBcdEnum(output, result);
            }
            catch { /* use defaults */ }

            return result;
        }

        private static Dictionary<string, string> ReadCurrentBcdValues()
        {
            var result = new Dictionary<string, string>(StockDefaults);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "bcdedit",
                    Arguments              = "/enum {current}",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow         = true,
                };

                var process = Process.Start(psi)!;
                var output  = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                ParseBcdEnum(output, result);
            }
            catch { /* use defaults */ }

            return result;
        }

        // Parses lines like: "bootmenupolicy             Standard"
        private static void ParseBcdEnum(string output, Dictionary<string, string> result)
        {
            foreach (var line in output.Split('\n'))
            {
                var match = Regex.Match(line.Trim(), @"^(\w+)\s+(.+)$");
                if (!match.Success) continue;

                var key   = match.Groups[1].Value.ToLower();
                var value = match.Groups[2].Value.Trim();

                if (StockDefaults.ContainsKey(key))
                    result[key] = value;
            }
        }

        // ── Process helpers ───────────────────────────────────────────────────

        private static async Task<bool> RunBcdeditAsync(string args, ToolService log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "bcdedit",
                    Arguments              = args,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                var process = Process.Start(psi)!;
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                log.Log($"[BCD] Exception: {ex.Message}");
                return false;
            }
        }

        private static async Task RunCommandAsync(string exe, string args, ToolService log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName        = exe,
                    Arguments       = args,
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                };
                var process = Process.Start(psi)!;
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                log.Log($"[BCD] Exception running {exe}: {ex.Message}");
            }
        }
    }

    /// <summary>Options for which BCD tweaks to apply.</summary>
    public class BcdTweakOptions
    {
        public bool LegacyBootMenu     { get; set; } = true;
        public bool DisableDynamicTick { get; set; } = true;
        public bool DisableRecovery    { get; set; } = true;
        public bool DisableHibernation { get; set; } = true;
    }
}
