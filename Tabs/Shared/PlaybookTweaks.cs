using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// All safe AkariOS playbook tweaks ported to direct registry/command
    /// operations. No external scripts or playbook required.
    ///
    /// Sources: AkariOS registry.yml, commands.yml, ScheduledTasks.yml
    ///
    /// Coverage:
    ///   - 60+ registry tweaks (privacy, telemetry, gaming, UI, QoL)
    ///   - 15 WMI autologger disables
    ///   - 12 IFEO process priority entries + 5 launcher helper throttles
    ///   - 4 environment variable telemetry opt-outs
    ///   - 2 fsutil filesystem tweaks
    ///   - 66 scheduled task disables
    ///   - Full AkariOS service preset (139 service changes)
    ///   - Memory compression disable (PowerShell)
    ///   - DISM reserved storage disable
    ///
    /// Target: ~42-46 processes on a clean Windows 11 install.
    ///
    /// All changes are covered by System Restore EXCEPT:
    ///   - Memory compression (WMI-based)
    ///   - DISM reserved storage
    ///   - BCD tweaks (handled separately by BcdBackup.cs)
    /// </summary>
    public static partial class PlaybookTweaks
    {
        // ── Main apply entry point ────────────────────────────────────────────

        public static async Task ApplyAllAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Applying all AkariOS tweaks...");

            ApplyRegistryTweaks(log);
            ApplyAutologgers(log);
            ApplyIFEO(log);
            BlockAutoInstalls(log);
            await ApplyEnvironmentVarsAsync(log);
            await ApplyFsutilAsync(log);
            await ApplyScheduledTasksAsync(log);
            await ServicesPreset.ApplyAkariGaming(log);
            await ApplyMemoryCompressionAsync(log);
            await ApplyDismTweaksAsync(log);

            TweakHelpers.SaveState("PlaybookTweaksApplied");
            log.Log("[PLAYBOOK] All tweaks applied. Restart recommended.");
        }

        public static async Task UndoAllAsync(ToolService log)
        {
            log.Log("[PLAYBOOK] Reverting playbook tweaks...");

            UndoRegistryTweaks(log);
            UndoAutologgers(log);
            UndoIFEO(log);
            await UndoEnvironmentVarsAsync(log);
            await UndoFsutilAsync(log);
            await EnableScheduledTasksAsync(log);
            await ServicesPreset.ApplyStockDefault(log);
            await UndoMemoryCompressionAsync(log);

            TweakHelpers.ClearState("PlaybookTweaksApplied");
            log.Log("[PLAYBOOK] Tweaks reverted. Restart recommended.");
        }

        public static bool IsApplied => TweakHelpers.HasState("PlaybookTweaksApplied");


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
                var p = Process.Start(psi)!;
                await p.WaitForExitAsync();
            }
            catch (Exception ex) { log.Log($"[PLAYBOOK] {exe} error: {ex.Message}"); }
        }

        private static async Task RunPsCommandAsync(string command, ToolService log)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName        = "powershell",
                    Arguments       = $"-NoProfile -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                };
                var p = Process.Start(psi)!;
                await p.WaitForExitAsync();
            }
            catch (Exception ex) { log.Log($"[PLAYBOOK] PS error: {ex.Message}"); }
        }
    }
}