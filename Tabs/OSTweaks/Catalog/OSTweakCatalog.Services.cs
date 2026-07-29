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
        // SERVICES
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] Services(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "os-disable-prefetch", Name = "Disable Prefetch / SysMain",
                    Description = "Disables the Superfetch/SysMain service — recommended on SSDs where prefetching adds overhead",
                    RecommendedState = true, DefaultState = false,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\SysMain", "Start");
                        return v.HasValue ? v == 4 : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain", "Start", disable ? 4 : 2, RegistryValueKind.DWord);
                        using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", writable: true);
                        k?.SetValue("EnablePrefetcher", disable ? 0 : 3, RegistryValueKind.DWord);
                        Log($"Prefetch {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-print-spooler", Name = "Disable Print Spooler",
                    Description = "Disables the print spooler service — use only if you have no printer",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\Spooler", "Start");
                        return v.HasValue ? v == 4 : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Spooler", "Start", disable ? 4 : 2, RegistryValueKind.DWord);
                        Log($"Print Spooler {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-enable-clipboard", Name = "Enable Clipboard Service",
                    Description = "Enables the Windows Clipboard service (cbdhsvc) for clipboard history and sync",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\cbdhsvc", "Start");
                        return v.HasValue ? v != 4 : true;
                    },
                    Apply = enable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\cbdhsvc", "Start", enable ? 2 : 4, RegistryValueKind.DWord);
                        Log($"Clipboard service {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-enable-cdrom", Name = "Enable CDROM Services",
                    Description = "Re-enables the CDROM driver and IMAPI2 burning service — needed for optical drives",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\cdrom", "Start");
                        return v.HasValue ? v != 4 : true;
                    },
                    Apply = enable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\cdrom",   "Start", enable ? 1 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\IMAPI2",  "Start", enable ? 3 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\IMAPI2FS","Start", enable ? 3 : 4, RegistryValueKind.DWord);
                        Log($"CDROM {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-enable-vr", Name = "Enable VR Services",
                    Description = "Enables SMB networking, QWave, and FontCache services required by some VR runtimes",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\ControlSet001\Services\LanmanWorkstation", "Start");
                        return v.HasValue ? v != 4 : false;
                    },
                    Apply = enable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\LanmanWorkstation","Start", enable ? 2 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\mrxsmb",           "Start", enable ? 3 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\mrxsmb20",         "Start", enable ? 3 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\rdbss",            "Start", enable ? 1 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\QwaveDrv",         "Start", enable ? 3 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\Qwave",            "Start", enable ? 3 : 4, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\FontCache",        "Start", enable ? 2 : 4, RegistryValueKind.DWord);
                        Log($"VR services {(enable ? "enabled" : "disabled")}. Restart to apply.");
                    }
                },
            };
        }

    }
}
