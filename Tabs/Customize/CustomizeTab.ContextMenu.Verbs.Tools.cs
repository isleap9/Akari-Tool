using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── CONTEXT MENU ▸ VERB IMPLEMENTATIONS: tools + associations ──
        // Registered by the entries table in CustomizeTab.ContextMenu.cs.

        // ── Compress To on the classic menu — Win11 24H2+, build 26100+ ─────
        // Credit: ThioJoe — surfaces the built-in Compress To handler on the
        // old context menu via its ExplorerCommandHandler CLSID.
        private static void AddCompressTo()
        {
            const string clsid = "{7AE6900F-6EB0-44A2-9CA1-DB2F7EF352AF}";
            foreach (var root in new[] { @"*\shell\CompressToFullMenu_ForOldContextMenu",
                                         @"Folder\shell\CompressToFullMenu_ForOldContextMenu" })
            {
                using var k = Registry.ClassesRoot.CreateSubKey(root);
                k.SetValue("ExplorerCommandHandler", clsid);
            }
        }

        private static void RemoveCompressTo()
        {
            Registry.ClassesRoot.DeleteSubKeyTree(@"*\shell\CompressToFullMenu_ForOldContextMenu", false);
            Registry.ClassesRoot.DeleteSubKeyTree(@"Folder\shell\CompressToFullMenu_ForOldContextMenu", false);
        }

        private static void AddControlPanel()
        {
            using var key = Registry.ClassesRoot.CreateSubKey("DesktopBackground\\shell\\ControlPanel");
            key.SetValue("MUIVerb", "@shell32.dll,-4161"); key.SetValue("Icon", "imageres.dll,-27"); key.SetValue("Position", "Bottom"); key.SetValue("SubCommands", "");
            using var sub1 = key.CreateSubKey("shell\\1ControlPanelCmd"); sub1.SetValue("MUIVerb", "@shell32.dll,-31061"); sub1.SetValue("Icon", "imageres.dll,-27");
            using var cmd1 = sub1.CreateSubKey("Command"); cmd1.SetValue("", "explorer.exe shell:::{26EE0668-A00A-44D7-9371-BEB064C98683}");
            using var sub2 = key.CreateSubKey("shell\\2ControlPanelCmd"); sub2.SetValue("MUIVerb", "@shell32.dll,-31062"); sub2.SetValue("Icon", "imageres.dll,-27");
            using var cmd2 = sub2.CreateSubKey("Command"); cmd2.SetValue("", "explorer.exe shell:::{21EC2020-3AEA-1069-A2DD-08002B30309D}");
            using var sub3 = key.CreateSubKey("shell\\3ControlPanelCmd"); sub3.SetValue("MUIVerb", "@shell32.dll,-32537"); sub3.SetValue("Icon", "imageres.dll,-27"); sub3.SetValue("CommandFlags", 32, RegistryValueKind.DWord);
            using var cmd3 = sub3.CreateSubKey("Command"); cmd3.SetValue("", "explorer.exe shell:::{ED7BA470-8E54-465E-825C-99712043E01C}");
        }
        private static void RemoveControlPanel() => Registry.ClassesRoot.OpenSubKey(@"DesktopBackground\shell\", true)?.DeleteSubKeyTree("ControlPanel", throwOnMissingSubKey: false);

        private static void AddFileHash()
        {
            using var key = Registry.ClassesRoot.CreateSubKey("*\\shell\\Hash");
            key.SetValue("MUIVerb", "Hash"); key.SetValue("SubCommands", "");
            var hashes = new (string Label, string Algo)[] { ("01Menu","SHA1"),("02Menu","SHA256"),("03Menu","SHA384"),("04Menu","SHA512"),("05Menu","MACTripleDES"),("06Menu","MD5"),("07Menu","RIPEMD160") };
            foreach (var (menu, algo) in hashes)
            {
                using var sub = key.CreateSubKey($"shell\\{menu}"); sub.SetValue("MUIVerb", algo);
                using var cmd = sub.CreateSubKey("Command"); cmd.SetValue("", $"powershell -noexit get-filehash -literalpath '%1' -algorithm {algo} | format-list");
            }
            using var all = key.CreateSubKey("shell\\08Menu"); all.SetValue("MUIVerb", "Show all"); all.SetValue("CommandFlags", 0x32, RegistryValueKind.DWord);
            using var allCmd = all.CreateSubKey("Command"); allCmd.SetValue("", "powershell -noexit get-filehash -literalpath '%1' -algorithm SHA1 | format-list;get-filehash -literalpath '%1' -algorithm SHA256 | format-list;get-filehash -literalpath '%1' -algorithm MD5 | format-list");
        }
        private static void RemoveFileHash() => Registry.ClassesRoot.OpenSubKey(@"*\shell\", true)?.DeleteSubKeyTree("Hash", throwOnMissingSubKey: false);

        private static void AddKillNotResponding()
        {
            using var key = Registry.ClassesRoot.CreateSubKey("DesktopBackground\\shell\\KillNotResponding");
            key.SetValue("MUIVerb", "Kill not responding tasks"); key.SetValue("Icon", "%SystemRoot%\\System32\\imageres.dll,-98"); key.SetValue("Position", "Top");
            using var cmd = key.CreateSubKey("Command"); cmd.SetValue("", "cmd.exe /K taskkill.exe /F /FI \"status eq NOT RESPONDING\"");
        }
        private static void RemoveKillNotResponding() => Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\DesktopBackground\Shell", true)?.DeleteSubKeyTree("KillNotResponding", throwOnMissingSubKey: false);

        private static void AddWindowsTools()
        {
            using var key = Registry.ClassesRoot.CreateSubKey("DesktopBackground\\Shell\\WindowsTools");
            key.SetValue("MUIVerb", "Windows Tools"); key.SetValue("Icon", "imageres.dll,-114"); key.SetValue("Position", "Bottom");
            using var cmd = key.CreateSubKey("command"); cmd.SetValue("", "explorer.exe shell:::{D20EA4E1-3957-11d2-A40B-0C5020524153}");
        }
        private static void RemoveWindowsTools() => Registry.ClassesRoot.OpenSubKey(@"DesktopBackground\Shell", true)?.DeleteSubKeyTree("WindowsTools", throwOnMissingSubKey: false);

        private static void AddShutDownMenu()
        {
            using var key = Registry.ClassesRoot.CreateSubKey("DesktopBackground\\shell\\ShutDown");
            key.SetValue("MUIVerb", "Shut Down"); key.SetValue("Icon", "shell32.dll,-28"); key.SetValue("Position", "Bottom"); key.SetValue("SubCommands", "");
            using var s1 = key.CreateSubKey("shell\\001ShutdownInstantly"); s1.SetValue("MUIVerb", "Shut down instantly"); s1.SetValue("Icon", "shell32.dll,-28");
            using var c1 = s1.CreateSubKey("Command"); c1.SetValue("", "shutdown -s -f -t 0");
            using var s2 = key.CreateSubKey("shell\\002ShutdownWarning"); s2.SetValue("MUIVerb", "Shut down with warning"); s2.SetValue("Icon", "shell32.dll,-28");
            using var c2 = s2.CreateSubKey("Command"); c2.SetValue("", "shutdown -s");
            using var s3 = key.CreateSubKey("shell\\003RestartInstantly"); s3.SetValue("MUIVerb", "Restart instantly"); s3.SetValue("Icon", "shell32.dll,-16739"); s3.SetValue("CommandFlags", 32, RegistryValueKind.DWord);
            using var c3 = s3.CreateSubKey("Command"); c3.SetValue("", "shutdown -r -f -t 0");
            using var s4 = key.CreateSubKey("shell\\004RestartWarning"); s4.SetValue("MUIVerb", "Restart with warning"); s4.SetValue("Icon", "shell32.dll,-16739");
            using var c4 = s4.CreateSubKey("Command"); c4.SetValue("", "shutdown -r");
        }
        private static void RemoveShutDownMenu() => Registry.ClassesRoot.OpenSubKey(@"DesktopBackground\shell", true)?.DeleteSubKeyTree("ShutDown", throwOnMissingSubKey: false);

        private static void AddPowerPlanAssoc()
        {
            using var key  = Registry.ClassesRoot.CreateSubKey(".pow");
            using var icon = Registry.ClassesRoot.CreateSubKey(".pow\\DefaultIcon"); icon.SetValue("", @"%SystemRoot%\System32\powercfg.cpl,-202");
            using var cmd  = key.CreateSubKey("Shell\\open\\command"); cmd.SetValue("", "powercfg /import %1");
        }
        private static void RemovePowerPlanAssoc() => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64).DeleteSubKeyTree(".pow", throwOnMissingSubKey: false);

        private static void AddRunWithPriority()
        {
            using var key = Registry.ClassesRoot.CreateSubKey(@"exefile\shell\Priority");
            key.SetValue("MUIVerb", "Run with priority"); key.SetValue("SubCommands", "");
            using var rt = key.CreateSubKey(@"shell\001flyout"); rt.SetValue("", "Realtime");
            using var rtCmd = rt.CreateSubKey("command"); rtCmd.SetValue("", @"cmd /c start """" /Realtime ""%1""");
            using var hi = key.CreateSubKey(@"shell\002flyout"); hi.SetValue("", "High");
            using var hiCmd = hi.CreateSubKey("command"); hiCmd.SetValue("", @"cmd /c start """" /High ""%1""");
        }
        private static void RemoveRunWithPriority() => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64).DeleteSubKeyTree(@"exefile\shell\Priority", throwOnMissingSubKey: false);

        private static void AddChangeResolution()
        {
            using var key = Registry.ClassesRoot.CreateSubKey(@"Directory\background\shell\Change Res");
            key.SetValue("MUIVerb", "Change Resolution");
            using var cmd = key.CreateSubKey("command"); cmd.SetValue("", @"C:\Windows\System32\rundll32.exe display.dll,ShowAdapterSettings 0");
        }
        private static void RemoveChangeResolution() => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64).DeleteSubKeyTree(@"Directory\background\shell\Change Res", throwOnMissingSubKey: false);

        private static void AddRebootToBios()
        {
            using var key = Registry.ClassesRoot.CreateSubKey(@"Directory\background\shell\reboot to fw");
            key.SetValue("MUIVerb", "Reboot To BIOS");
            using var cmd = key.CreateSubKey("command"); cmd.SetValue("", @"shutdown /r /fw /t 0");
        }
        private static void RemoveRebootToBios() => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64).DeleteSubKeyTree(@"Directory\background\shell\reboot to fw", throwOnMissingSubKey: false);

    }
}
