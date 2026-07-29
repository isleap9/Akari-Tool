using System.Management;
using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Creates Windows System Restore points before batch tweak operations.
    ///
    /// Handles the Windows 24-hour throttle by temporarily setting
    /// SystemRestorePointCreationFrequency=0 before creation.
    ///
    /// Note: Restore points cover registry changes, scheduled tasks, and
    /// environment variables — but NOT bcdedit, Appx removals, DISM changes,
    /// or Disable-MMAgent. Those are handled separately.
    /// </summary>
    public static class RestorePointHelper
    {
        private const string StateKey = @"Software\AkariTool";
        private const string LastRestorePointValue = "LastRestorePointTime";

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a restore point if one hasn't been created in the last hour.
        /// Returns true if a restore point exists (newly created or recent).
        /// </summary>
        public static async Task<bool> EnsureRestorePointAsync(ToolService log, string description = "Akari Tool - Pre-Tweak Backup")
        {
            // Check if we already made one recently (within 1 hour)
            if (HasRecentRestorePoint(out var lastTime))
            {
                log.Log($"[RESTORE] Recent restore point found ({lastTime:HH:mm:ss}) — skipping creation.");
                return true;
            }

            log.Log("[RESTORE] Creating system restore point...");
            bool success = await CreateRestorePointAsync(log, description);

            if (success)
            {
                // Save timestamp so we don't create duplicates
                using var key = Registry.CurrentUser.CreateSubKey(StateKey);
                key?.SetValue(LastRestorePointValue, DateTime.Now.ToBinary(), RegistryValueKind.QWord);
                log.Log("[RESTORE] Restore point created successfully.");
            }
            else
            {
                log.Log("[RESTORE] WARNING: Could not create restore point. Proceeding anyway.");
            }

            return success;
        }

        /// <summary>
        /// Checks if System Restore is enabled on C:.
        /// </summary>
        public static bool IsSystemRestoreEnabled()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\default",
                    "SELECT * FROM SystemRestoreConfig WHERE Drive='C:\\\\'");

                foreach (var obj in searcher.Get())
                    return true;

                return false;
            }
            catch { return false; }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static bool HasRecentRestorePoint(out DateTime lastTime)
        {
            lastTime = DateTime.MinValue;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StateKey);
                if (key?.GetValue(LastRestorePointValue) is long binary)
                {
                    lastTime = DateTime.FromBinary(binary);
                    return (DateTime.Now - lastTime).TotalHours < 1;
                }
                return false;
            }
            catch { return false; }
        }

        private static async Task<bool> CreateRestorePointAsync(ToolService log, string description)
        {
            try
            {
                // Bypass the 24-hour throttle
                Registry.SetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore",
                    "SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);

                // Enable System Restore on C: if disabled
                await EnableSystemRestoreAsync();

                // Create restore point via WMI
                bool created = await Task.Run(() => CreateViaWmi(description));
                if (created) return true;

                // Fallback: PowerShell
                log.Log("[RESTORE] WMI failed, trying PowerShell...");
                return await CreateViaPowerShellAsync(description);
            }
            catch (Exception ex)
            {
                log.Log($"[RESTORE] Exception: {ex.Message}");
                return false;
            }
        }

        private static bool CreateViaWmi(string description)
        {
            try
            {
                var scope  = new ManagementScope(@"\\localhost\root\default");
                var path   = new ManagementPath("SystemRestore");
                var cls    = new ManagementClass(scope, path, null);
                var inArgs = cls.GetMethodParameters("CreateRestorePoint");

                inArgs["Description"]       = description;
                inArgs["RestorePointType"]  = 12;  // MODIFY_SETTINGS
                inArgs["EventType"]         = 100; // BEGIN_SYSTEM_CHANGE

                var outArgs = cls.InvokeMethod("CreateRestorePoint", inArgs, null);
                return outArgs?["ReturnValue"] is uint rv && rv == 0;
            }
            catch { return false; }
        }

        private static async Task<bool> CreateViaPowerShellAsync(string description)
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "powershell",
                Arguments              = $"-NoProfile -Command \"Checkpoint-Computer -Description '{description}' -RestorePointType MODIFY_SETTINGS\"",
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };

            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        private static async Task EnableSystemRestoreAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "powershell",
                Arguments              = "-NoProfile -Command \"Enable-ComputerRestore -Drive 'C:\\'\"",
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();
        }
    }
}
