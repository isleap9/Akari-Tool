using System.Diagnostics;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class PlaybookTweaks
    {
        // ═════════════════════════════════════════════════════════════════════
        // AUTOLOGGERS (ETW trace sessions)
        // ═════════════════════════════════════════════════════════════════════

        private static readonly string[] AutologgerNames =
        {
            "CimFSUnionFS-Filter", "FilterMgr-Logger", "SpoolerLogger",
            "Circular Kernel Context Logger", "DiagLog", "Diagtrack-Listener",
            "LwtNetLog", "Microsoft-Windows-Rdp-Graphics-RdpIdd-Trace",
            "NetCore", "NtfsLog", "RadioMgr", "ReFSLogr", "UBPM",
            "WiFiSession", "AutoLogger-Diagtrack-Listener",
        };

        private static void ApplyAutologgers(ToolService log)
        {
            log.Log("[PLAYBOOK] Disabling ETW autologgers...");
            int ok = 0;
            foreach (var name in AutologgerNames)
            {
                try
                {
                    Registry.SetValue(
                        $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\WMI\Autologger\{name}",
                        "Start", 0, RegistryValueKind.DWord);
                    ok++;
                }
                catch { /* key may not exist on all systems */ }
            }
            log.Log($"[PLAYBOOK] Autologgers: {ok}/{AutologgerNames.Length} disabled.");
        }

        private static void UndoAutologgers(ToolService log)
        {
            log.Log("[PLAYBOOK] Re-enabling ETW autologgers...");
            foreach (var name in AutologgerNames)
            {
                try
                {
                    Registry.SetValue(
                        $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\WMI\Autologger\{name}",
                        "Start", 1, RegistryValueKind.DWord);
                }
                catch { }
            }
            log.Log("[PLAYBOOK] Autologgers restored.");
        }

    }
}
