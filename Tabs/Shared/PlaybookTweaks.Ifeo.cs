using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class PlaybookTweaks
    {
        // ═════════════════════════════════════════════════════════════════════
        // IFEO PROCESS PRIORITIES
        // ═════════════════════════════════════════════════════════════════════

        private static readonly (string Exe, int CpuPriority, int? IoPriority)[] IFEOEntries =
        {
            // Lower priority background processes to give more CPU to games
            ("ctfmon.exe",      5, null),   // Text input framework — below normal
            ("SearchIndexer.exe", 5, null), // Search indexer — below normal
            ("fontdrvhost.exe", 1, 0),      // Font driver host — idle CPU, idle IO
            ("lsass.exe",       1, null),   // Local Security Authority — idle CPU
            ("sihost.exe",      1, 0),      // Shell Infrastructure Host — idle
            ("sppsvc.exe",      1, 0),      // Software Protection — idle
            ("csrss.exe",       3, 3),      // Client/Server Runtime — normal (keep responsive)
            // Gaming launcher web helper processes — below normal so they don't compete with games
            ("OriginWebHelperService.exe", 5, null),
            ("EpicWebHelper.exe",          5, null),
            ("UplayWebCore.exe",           5, null),
            ("SocialClubHelper.exe",       5, null), // Rockstar Social Club
            ("steamwebhelper.exe",         5, null),
            ("ShareX.exe",                 5, null),
        };

        private static void ApplyIFEO(ToolService log)
        {
            log.Log("[PLAYBOOK] Applying IFEO process priorities...");
            int ok = 0;
            foreach (var (exe, cpu, io) in IFEOEntries)
            {
                try
                {
                    var path = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}\PerfOptions";
                    Registry.SetValue(path, "CpuPriorityClass", cpu, RegistryValueKind.DWord);
                    if (io.HasValue)
                        Registry.SetValue(path, "IoPriority", io.Value, RegistryValueKind.DWord);
                    ok++;
                }
                catch (Exception ex)
                {
                    log.Log($"[PLAYBOOK] IFEO warning {exe}: {ex.Message}");
                }
            }

            // Block telemetry binaries via IFEO debugger redirect to taskkill
            // taskkill.exe is present on all Windows installs (unlike noop.exe)
            var debuggerBlocks = new[]
            {
                "DeviceCensus.exe",    // Device telemetry census binary
                "CompatTelRunner.exe", // Compatibility telemetry runner
                "AggregatorHost.exe",  // Windows data aggregation host (pure telemetry)
            };
            foreach (var exe in debuggerBlocks)
            {
                try
                {
                    Registry.SetValue(
                        $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}",
                        "debugger", @"%WINDIR%\System32\taskkill.exe", RegistryValueKind.String);
                    ok++;
                }
                catch { }
            }

            log.Log($"[PLAYBOOK] IFEO: {ok}/{IFEOEntries.Length + debuggerBlocks.Length} applied.");
        }

        private static void UndoIFEO(ToolService log)
        {
            log.Log("[PLAYBOOK] Removing IFEO priority overrides...");
            var exes = IFEOEntries.Select(e => e.Exe)
                .Concat(new[] { "DeviceCensus.exe", "CompatTelRunner.exe", "AggregatorHost.exe" });
            foreach (var exe in exes)
            {
                try
                {
                    var key = Registry.LocalMachine.OpenSubKey(
                        $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}",
                        writable: true);
                    key?.DeleteSubKey("PerfOptions", throwOnMissingSubKey: false);
                    key?.DeleteValue("debugger", throwOnMissingValue: false);
                }
                catch { }
            }
            log.Log("[PLAYBOOK] IFEO overrides removed.");
        }

    }
}
