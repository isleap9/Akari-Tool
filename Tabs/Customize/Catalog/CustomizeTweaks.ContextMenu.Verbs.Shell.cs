using Microsoft.Win32;

namespace AkariTool.Tabs
{
    // MVVM PORT: verbatim from net8 CustomizeTab.ContextMenu.Verbs.Shell.cs.
    // Verb implementations (shell + diagnostics) registered by the ContextMenuEntries table.
    public static partial class CustomizeTweaks
    {
        private static void AddClassicMenu()
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32");
            key?.SetValue("", "", RegistryValueKind.String);
        }
        private static void RemoveClassicMenu()
        {
            Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID", true)
                ?.DeleteSubKeyTree("{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", throwOnMissingSubKey: false);
        }

        private static void AddCmdAdmin()
        {
            foreach (var root in new[] { "Directory\\Shell\\OpenElevatedCMD", "Drive\\Shell\\OpenElevatedCMD",
                "LibraryFolder\\background\\Shell\\OpenElevatedCMD", "Directory\\Background\\Shell\\OpenElevatedCMD" })
            {
                using var key = Registry.ClassesRoot.CreateSubKey(root);
                key.SetValue("", "Open CMD As Administrator");
                key.SetValue("Icon", "imageres.dll,-5324");
                using var cmd = key.CreateSubKey("Command");
                cmd.SetValue("", "Powershell.exe -windowstyle hidden -Command \"Start-Process cmd.exe -ArgumentList '/s,/k,pushd,%V' -Verb RunAs\"");
            }
        }
        private static void RemoveCmdAdmin()
        {
            Registry.ClassesRoot.OpenSubKey(@"Directory\Background\Shell\", true)?.DeleteSubKeyTree("OpenElevatedCMD", throwOnMissingSubKey: false);
            Registry.ClassesRoot.OpenSubKey(@"Directory\Shell\", true)?.DeleteSubKeyTree("OpenElevatedCMD", throwOnMissingSubKey: false);
            Registry.ClassesRoot.OpenSubKey(@"Drive\Shell\", true)?.DeleteSubKeyTree("OpenElevatedCMD", throwOnMissingSubKey: false);
        }

        private static void AddPsAdmin()
        {
            foreach (var root in new[] { "Directory\\Shell\\OpenElevatedPS", "Drive\\Shell\\OpenElevatedPS",
                "LibraryFolder\\background\\Shell\\OpenElevatedPS", "Directory\\Background\\Shell\\OpenElevatedPS" })
            {
                using var key = Registry.ClassesRoot.CreateSubKey(root);
                key.SetValue("", "Open Powershell As Administrator");
                key.SetValue("Icon", "powershell.exe");
                using var cmd = key.CreateSubKey("Command");
                cmd.SetValue("", "Powershell.exe -windowstyle hidden -Command \"Start-Process cmd.exe -ArgumentList '/s,/c,pushd %V && powershell' -Verb RunAs\"");
            }
        }
        private static void RemovePsAdmin()
        {
            Registry.ClassesRoot.OpenSubKey(@"Directory\Background\Shell\", true)?.DeleteSubKeyTree("OpenElevatedPS", throwOnMissingSubKey: false);
            Registry.ClassesRoot.OpenSubKey(@"Directory\Shell\", true)?.DeleteSubKeyTree("OpenElevatedPS", throwOnMissingSubKey: false);
            Registry.ClassesRoot.OpenSubKey(@"Drive\Shell\", true)?.DeleteSubKeyTree("OpenElevatedPS", throwOnMissingSubKey: false);
        }

        private static void AddTakeOwnership()
        {
            using var file = Registry.ClassesRoot.CreateSubKey("*\\shell\\TakeOwnership");
            file.SetValue("", "Take Ownership"); file.SetValue("HasLUAShield", ""); file.SetValue("NoWorkingDirectory", ""); file.SetValue("NeverDefault", "");
            using var fileCmd = file.CreateSubKey("command");
            fileCmd.SetValue("", "powershell.exe -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/c takeown /f \\\"%1\\\" && icacls \\\"%1\\\" /grant *S-1-3-4:F /c /l & pause' -Verb runAs\"");
            fileCmd.SetValue("IsolatedCommand", "powershell.exe -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/c takeown /f \\\"%1\\\" && icacls \\\"%1\\\" /grant *S-1-3-4:F /c /l & pause' -Verb runAs\"");
            using var dir = Registry.ClassesRoot.CreateSubKey("Directory\\shell\\TakeOwnership");
            dir.SetValue("", "Take Ownership"); dir.SetValue("HasLUAShield", ""); dir.SetValue("NoWorkingDirectory", ""); dir.SetValue("NeverDefault", "");
            using var dirCmd = dir.CreateSubKey("command");
            dirCmd.SetValue("", "powershell.exe -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/c takeown /f \\\"%1\\\"  /r /d y /skipsl && icacls \\\"%1\\\" /grant *S-1-3-4:F /t /c /l & pause' -Verb runAs\"");
            dirCmd.SetValue("IsolatedCommand", "powershell.exe -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/c takeown /f \\\"%1\\\" /r /d y /skipsl && icacls \\\"%1\\\" /grant *S-1-3-4:F /t /c /l & pause' -Verb runAs\"");
        }
        private static void RemoveTakeOwnership()
        {
            Registry.ClassesRoot.OpenSubKey(@"*\shell\", true)?.DeleteSubKeyTree("TakeOwnership", throwOnMissingSubKey: false);
            Registry.ClassesRoot.OpenSubKey(@"Directory\shell", true)?.DeleteSubKeyTree("TakeOwnership", throwOnMissingSubKey: false);
        }

        // ── SFC /SCANNOW (Winhance port) ─────────────────────────────────────
        private static void AddSfcScan()
        {
            const string root = @"Directory\Background\shell\SFC";
            using (var k = Registry.ClassesRoot.CreateSubKey(root))
            {
                k.SetValue("MUIVerb", "SFC /SCANNOW");
                k.SetValue("Icon", "WmiPrvSE.exe");
            }
            using var cmd = Registry.ClassesRoot.CreateSubKey(root + @"\command");
            cmd.SetValue("", @"PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command ""Start-Process cmd -ArgumentList '/s,/k, sfc /scannow' -Verb runAs""");
        }

        private static void RemoveSfcScan() =>
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\Background\shell\SFC", false);

        // ── CHKDSK submenu (Winhance port) ───────────────────────────────────
        private static void AddChkdsk()
        {
            const string root = @"Directory\Background\shell\CHKDSK";
            using (var k = Registry.ClassesRoot.CreateSubKey(root))
            {
                k.SetValue("MUIVerb", "CHKDSK");
                k.SetValue("Icon", "WmiPrvSE.exe");
                k.SetValue("SubCommands", "");
            }
            var items = new (string Key, string Label, string Args)[]
            {
                ("001menu", "Run CHKDSK (scan only)",        @"'/v:on,/s,/k, set /p d=Enter drive letter (e.g. C): & chkdsk !d!:'"),
                ("002menu", "Run CHKDSK /F (fix errors)",    @"'/v:on,/s,/k, set /p d=Enter drive letter (e.g. C): & chkdsk !d!: /f'"),
                ("003menu", "Run CHKDSK /R (recover sectors)", @"'/v:on,/s,/k, set /p d=Enter drive letter (e.g. C): & chkdsk !d!: /r'"),
            };
            foreach (var (key, label, args) in items)
            {
                using (var m = Registry.ClassesRoot.CreateSubKey($@"{root}\shell\{key}"))
                    m.SetValue("MUIVerb", label);
                using var c = Registry.ClassesRoot.CreateSubKey($@"{root}\shell\{key}\command");
                c.SetValue("", $@"PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command ""Start-Process cmd -ArgumentList {args} -Verb runAs""");
            }
        }

        private static void RemoveChkdsk() =>
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\Background\shell\CHKDSK", false);

        // ── DISM /RestoreHealth (Winhance port) ──────────────────────────────
        private static void AddRepairImage()
        {
            const string root = @"Directory\Background\shell\RepairWindowsImage";
            using (var k = Registry.ClassesRoot.CreateSubKey(root))
            {
                k.SetValue("MUIVerb", "Repair Windows Image");
                k.SetValue("Icon", "WmiPrvSE.exe");
            }
            using var cmd = Registry.ClassesRoot.CreateSubKey(root + @"\command");
            cmd.SetValue("", @"PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command ""Start-Process cmd -ArgumentList '/s,/k, DISM /Online /Cleanup-Image /RestoreHealth' -Verb runAs""");
        }

        private static void RemoveRepairImage() =>
            Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\Background\shell\RepairWindowsImage", false);
    }
}
