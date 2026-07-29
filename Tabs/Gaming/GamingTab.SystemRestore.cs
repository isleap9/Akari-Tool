using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public partial class GamingTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // SYSTEM RESTORE
        // ══════════════════════════════════════════════════════════════════════

        private void BuildSystemRestore(StackPanel panel)
        {
            AddSection(panel, "System", new[]
            {
                new TweakDefinition
                {
                    Id               = "system-restore-protection",
                    Name             = "System Protection (Restore Points)",
                    Description      = "Allow Windows to automatically create restore points for the C: drive",
                    IsPreference     = true,
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore",
                            "RPSessionInterval");
                        return v.HasValue ? v > 0 : true;
                    },
                    Apply = on =>
                    {
                        try
                        {
                            string verb = on ? "Enable" : "Disable";
                            string cmd  = verb + @"-ComputerRestore -Drive ""C:\""";
                            TweakHelpers.RunCommand("powershell.exe",
                                "-NoProfile -ExecutionPolicy Bypass -Command " + cmd);
                            Log("System Restore " + (on ? "enabled" : "disabled") + " for C:\\.");
                        }
                        catch (Exception ex) { Log("ERROR System Restore: " + ex.Message); }
                    }
                },
                new TweakDefinition
                {
                    Id               = "fs-long-paths",
                    Name             = "Enable Long File Paths",
                    Description      = "Removes the 260-character path limit (MAX_PATH) for apps that support it — useful for deep mod folders and dev projects",
                    RecommendedState = true,
                    DefaultState     = false,
                    RequiresRestart  = true,
                    ReadState = () => (ReadDword(RegistryHive.LocalMachine,
                        @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled") ?? 0) == 1,
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem",
                            "LongPathsEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Long file paths {(on ? "enabled" : "disabled")}.");
                    }
                },
            });
        }
    }
}
