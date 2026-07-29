using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.OSTweaks
{
    public static partial class OSTweakCatalog
    {
        // ══════════════════════════════════════════════════════════════════════
        // PERFORMANCE
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] Performance(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "os-boot-menu-policy", Name = "Boot Menu Policy Standard",
                    Description = "Sets Boot Menu Policy to Standard — enables the F8 legacy recovery menu on startup",
                    IsPreference = true,
                    ReadState = () => (bool?)null,  // bcdedit — no clean registry read
                    Apply = standard =>
                    {
                        TweakHelpers.RunCommand("bcdedit.exe", standard ? "/set bootmenupolicy Standard" : "/set bootmenupolicy legacy");
                        Log($"Boot menu policy set to {(standard ? "Standard" : "Legacy")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-enable-tsx", Name = "Enable Intel TSX",
                    Description = "Enables Intel Transactional Synchronization Extensions — improves multi-threaded workloads on supported CPUs",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\ControlSet001\Control\Session Manager\kernel", "DisableTsx");
                        return v.HasValue ? v == 0 : false;
                    },
                    Apply = enable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Session Manager\kernel", "DisableTsx", enable ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Intel TSX {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-no-lazy-mode", Name = "MMCSS NoLazyMode",
                    Description = "Disables MMCSS lazy mode — lower audio latency and more consistent scheduler timing",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NoLazyMode");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = enable =>
                    {
                        const string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                        Registry.SetValue(key, "NoLazyMode", enable ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(key, "AlwaysOn",   enable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"MMCSS NoLazyMode {(enable ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-large-system-cache", Name = "Large System Cache",
                    Description = "Enables large system cache — improves file I/O on systems with large RAM",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = enable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", enable ? 1 : 0, RegistryValueKind.DWord);
                        Log($"LargeSystemCache {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-nvme-tweaks", Name = "NVMe Tweaks",
                    Description = "Applies NVMe latency and power management tweaks — disables idle power states and diagnostic logging",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\ControlSet001\Services\stornvme\Parameters\Device", "IdlePowerMode");
                        return v.HasValue ? v == 0 : false;
                    },
                    Apply = enable =>
                    {
                        const string nvmeKey = @"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\stornvme\Parameters\Device";
                        if (enable)
                        {
                            Registry.SetValue(nvmeKey, "ContiguousMemoryFromAnyNode", 1, RegistryValueKind.DWord);
                            Registry.SetValue(nvmeKey, "LogSize",         0, RegistryValueKind.DWord);
                            Registry.SetValue(nvmeKey, "IdlePowerMode",   0, RegistryValueKind.DWord);
                            Registry.SetValue(nvmeKey, "DiagnosticFlags", 0, RegistryValueKind.DWord);
                        }
                        else
                        {
                            var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\ControlSet001\Services\stornvme\Parameters\Device", true);
                            foreach (var v in new[] { "ContiguousMemoryFromAnyNode","LogSize","IdlePowerMode","DiagnosticFlags" })
                                k?.DeleteValue(v, throwOnMissingValue: false);
                        }
                        Log($"NVMe tweaks {(enable ? "applied" : "reverted")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-system-profile", Name = "System Profile Tweaks",
                    Description = "Raises GPU priority to 8 and CPU scheduling category to High for games via SystemProfile registry",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority");
                        return v.HasValue ? v == 8 : false;
                    },
                    Apply = enable =>
                    {
                        const string games    = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
                        const string proAudio = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio";
                        if (enable)
                        {
                            Registry.SetValue(games, "Affinity",           0,        RegistryValueKind.DWord);
                            Registry.SetValue(games, "Background Only",    "False",  RegistryValueKind.String);
                            Registry.SetValue(games, "Clock Rate",         2710,     RegistryValueKind.DWord);
                            Registry.SetValue(games, "GPU Priority",       8,        RegistryValueKind.DWord);
                            Registry.SetValue(games, "Priority",           8,        RegistryValueKind.DWord);
                            Registry.SetValue(games, "Scheduling Category","High",   RegistryValueKind.String);
                            Registry.SetValue(games, "SFIO Priority",      "High",   RegistryValueKind.String);
                            Registry.SetValue(proAudio, "Priority",            8,        RegistryValueKind.DWord);
                            Registry.SetValue(proAudio, "Scheduling Category", "Medium", RegistryValueKind.String);
                        }
                        else
                        {
                            var gk = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", true);
                            foreach (var v in new[] { "Affinity","Background Only","Clock Rate","GPU Priority","Priority","Scheduling Category","SFIO Priority" })
                                gk?.DeleteValue(v, throwOnMissingValue: false);
                            var pk = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio", true);
                            pk?.DeleteValue("Priority", throwOnMissingValue: false);
                            pk?.DeleteValue("Scheduling Category", throwOnMissingValue: false);
                        }
                        Log($"System Profile {(enable ? "applied" : "reverted")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-set-utc", Name = "Set Clock to UTC",
                    Description = "Stores the hardware clock as UTC — fixes time sync conflict when dual-booting with Linux",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal");
                        return v.HasValue ? v == 1 : false;
                    },
                    Apply = enable =>
                    {
                        if (enable)
                            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\TimeZoneInformation", "RealTimeIsUniversal", 1, RegistryValueKind.DWord);
                        else
                            Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\TimeZoneInformation", true)
                                ?.DeleteValue("RealTimeIsUniversal", throwOnMissingValue: false);
                        Log($"UTC clock {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
            };
        }

    }
}
