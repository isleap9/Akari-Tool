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
        // NETWORK
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] Network(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "os-disable-wifi", Name = "Disable WiFi",
                    Description = "Disables WiFi services — use only if you rely exclusively on Ethernet",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WlanSvc", "Start");
                        return v.HasValue ? v == 4 : false;
                    },
                    Apply = disable =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WlanSvc",  "Start", disable ? 4 : 2, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\vwififlt", "Start", disable ? 4 : 1, RegistryValueKind.DWord);
                        Log($"WiFi {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-bluetooth", Name = "Disable Bluetooth",
                    Description = "Disables all Bluetooth services — use only if you have no Bluetooth peripherals",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\BTHPORT", "Start");
                        return v.HasValue ? v == 4 : false;
                    },
                    Apply = disable =>
                    {
                        foreach (var svc in new[] { "BthA4dp","BthEnum","BthHFEnum","BthLEEnum","BTHMODEM",
                            "BluetoothUserService","BthAvctpSvc","RFCOMM","bthserv","BTAGService",
                            "BTHUSB","BTHPORT","BthMini","HidBth" })
                            Registry.SetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{svc}", "Start", disable ? 4 : 3, RegistryValueKind.DWord);
                        Log($"Bluetooth {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id = "os-disable-vpn", Name = "Disable VPN Services",
                    Description = "Disables IKEEXT, RasMan, and SstpSvc — use only if you never use VPN",
                    IsPreference = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\RasMan", "Start");
                        return v.HasValue ? v == 4 : false;
                    },
                    Apply = disable =>
                    {
                        foreach (var svc in new[] { "IKEEXT","RasMan","SstpSvc" })
                            Registry.SetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{svc}", "Start", disable ? 4 : 3, RegistryValueKind.DWord);
                        Log($"VPN services {(disable ? "disabled" : "enabled")}. Restart to apply.");
                    }
                },
            };
        }

    }
}
