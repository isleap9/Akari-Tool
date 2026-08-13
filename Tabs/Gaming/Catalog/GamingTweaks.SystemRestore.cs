using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs.Gaming
{
    // MVVM PORT (Phase 19): the Gaming ▸ System Restore section, extracted from build
    // #2's Tabs/Gaming/GamingTab.SystemRestore.cs. 2 toggle TweakDefinitions. net8's
    // AddSection titled this "System"; wired into the MVVM catalog as the clearer
    // "System Restore" per isleap (a section-card label only — not an Id, no
    // Backup/Restore impact). Reuses the existing static GamingTweaks.ReadDword +
    // TweakHelpers.RunCommand; only the dropdown/toggle wrapper differs from a raw tab.
    //
    // ONE deliberate behavioral addition (isleap, Phase 18 option B):
    // `system-restore-protection` gains Warning + WarningState = false so a MANUAL
    // toggle-OFF (the only path that reaches Disable-ComputerRestore) shows an
    // OK/Cancel confirmation before applying — restoring the Yes/No gate Windows' own
    // System Protection UI shows before it deletes restore points. Id, registry read,
    // and the Enable/Disable-ComputerRestore apply logic are otherwise BYTE-IDENTICAL
    // to net8. `fs-long-paths` is ported verbatim (no Warning — no landmine).
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] SystemRestore(Action<string> Log) => new[]
        {
            new TweakDefinition
            {
                Id               = "system-restore-protection",
                Name             = "System Protection (Restore Points)",
                Description      = "Allow Windows to automatically create restore points for the C: drive",
                IsPreference     = true,
                RecommendedState = true,
                DefaultState     = true,
                // Behavioral addition (Phase 18 option B): confirm before the
                // destructive disable. WarningState = false → fires only on OFF.
                Warning          = "Turning off System Protection will delete all existing restore points on this drive. Continue?",
                WarningState     = false,
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
        };
    }
}
