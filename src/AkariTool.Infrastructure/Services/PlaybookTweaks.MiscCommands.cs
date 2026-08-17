using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class PlaybookTweaks
    {
        // ═════════════════════════════════════════════════════════════════════
        // MEMORY COMPRESSION & DISM
        // ═════════════════════════════════════════════════════════════════════

        private static async Task ApplyMemoryCompressionAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Disabling memory compression...");
            await RunPsCommandAsync("Disable-MMAgent -MemoryCompression", log);
        }

        private static async Task UndoMemoryCompressionAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Re-enabling memory compression...");
            await RunPsCommandAsync("Enable-MMAgent -MemoryCompression", log);
        }

        private static async Task ApplyDismTweaksAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Disabling reserved storage (DISM)...");
            await RunCommandAsync("DISM.exe", "/Online /set-reservedstoragestate /state:disabled", log);
        }

        // ═════════════════════════════════════════════════════════════════════
        // BLOCK WINDOWS AUTO-INSTALLS (UScheduler keys)
        // Removes the orchestrator keys Windows uses to silently push
        // Outlook, Dev Home, Edge, and Cross Device as "recommended" apps.
        // Windows may recreate these after a feature update — that's expected.
        // ═════════════════════════════════════════════════════════════════════

        private static readonly string[] AutoInstallKeys =
        {
            @"SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\DevHomeUpdate",
            @"SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\OutlookUpdate",
            @"SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\CrossDeviceUpdate",
            @"SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\EdgeUpdate",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator\UScheduler\DevHomeUpdate",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator\UScheduler\OutlookUpdate",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator\UScheduler\CrossDeviceUpdate",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator\UScheduler\EdgeUpdate",
        };

        private static void BlockAutoInstalls(ToolService log)
        {
            log.Log("[PLAYBOOK] Removing Windows auto-install orchestrator keys...");
            int ok = 0;
            foreach (var keyPath in AutoInstallKeys)
            {
                try
                {
                    // Navigate to the parent key and delete the leaf
                    var lastSlash = keyPath.LastIndexOf('\\');
                    var parentPath = keyPath[..lastSlash];
                    var leafName   = keyPath[(lastSlash + 1)..];

                    using var parent = Registry.LocalMachine.OpenSubKey(parentPath, writable: true);
                    if (parent is null) continue; // key doesn't exist — already gone

                    parent.DeleteSubKeyTree(leafName, throwOnMissingSubKey: false);
                    ok++;
                }
                catch (Exception ex)
                {
                    log.Log($"[PLAYBOOK] AutoInstall warning: {ex.Message}");
                }
            }
            log.Log($"[PLAYBOOK] Auto-install keys: {ok} removed (keys not present are already clean).");
        }

        // ═════════════════════════════════════════════════════════════════════
        // PROCESS HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private static async Task<bool> RunSchtasksAsync(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "schtasks",
                    Arguments              = args,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    RedirectStandardError  = true,
                    RedirectStandardOutput = true,
                };
                var p = Process.Start(psi)!;
                await p.WaitForExitAsync();
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
