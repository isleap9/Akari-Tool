using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] Storage(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "storage-nvme-tweaks",
                    Name             = "NVMe Latency Tweaks",
                    Description      = "Disable NVMe idle power states and diagnostic logging to reduce SSD access latency. Recommended for desktops on AC power; may increase idle power draw on laptops.",
                    RecommendedState = true,
                    DefaultState     = false,
                    RequiresRestart  = true,
                    ReadState        = () => SystemStateReader.ReadNvmeTweaks() ?? false,
                    Apply = on =>
                    {
                        const string path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device";
                        if (on)
                        {
                            Registry.SetValue(path, "ContiguousMemoryFromAnyNode", 1, RegistryValueKind.DWord);
                            Registry.SetValue(path, "LogSize",         0, RegistryValueKind.DWord);
                            Registry.SetValue(path, "IdlePowerMode",   0, RegistryValueKind.DWord);
                            Registry.SetValue(path, "DiagnosticFlags", 0, RegistryValueKind.DWord);
                        }
                        else
                        {
                            using var k = Registry.LocalMachine.OpenSubKey(
                                @"SYSTEM\CurrentControlSet\Services\stornvme\Parameters\Device", true);
                            foreach (var v in new[] { "ContiguousMemoryFromAnyNode", "LogSize", "IdlePowerMode", "DiagnosticFlags" })
                                k?.DeleteValue(v, false);
                        }
                        Log($"NVMe latency tweaks {(on ? "applied" : "reverted")}. Restart to apply.");
                    }
                },
            };
    }
}
