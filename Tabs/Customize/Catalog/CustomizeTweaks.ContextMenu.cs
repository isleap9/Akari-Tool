using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.ContextMenu.cs.
    // Section "Entries" — 17 registry-based context-menu entries built from a table via a
    // generator loop (Id = tuple id). The verb implementations (Add*/Remove*) live in the
    // .Verbs.Shell / .Verbs.Tools partials. Windows never ships these, so DefaultState=false
    // and RecommendedState is deliberately unset. Only the rendering scaffolding was dropped.
    public static partial class CustomizeTweaks
    {
        public static TweakDefinition[] ContextMenuEntries(Action<string> Log)
        {
            // Registry-based context menu entries. Every entry is "add a shell verb /
            // association that Windows does not ship with", so the Windows factory state is
            // always absent → DefaultState = false. Whether an entry is desirable is a matter
            // of taste, so RecommendedState is deliberately left unset.
            var entries = new (string Id, string Title, string Description, Action Add, Action Remove, Func<bool> Detect)[]
            {
                ("customize-contextmenu-classic-menu",       "Classic Right-Click Menu",   "Restores the old Windows 10 right-click context menu layout",     AddClassicMenu,       RemoveClassicMenu,       () => Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32") != null),
                ("customize-contextmenu-cmd-admin",          "Open CMD as Administrator",  "Adds 'Open CMD As Administrator' to the right-click menu",        AddCmdAdmin,          RemoveCmdAdmin,          () => Registry.ClassesRoot.OpenSubKey(@"Directory\Background\Shell\OpenElevatedCMD") != null),
                ("customize-contextmenu-powershell-admin",   "Open PowerShell as Admin",   "Adds 'Open PowerShell As Administrator' to the right-click menu", AddPsAdmin,           RemovePsAdmin,           () => Registry.ClassesRoot.OpenSubKey(@"Directory\Background\Shell\OpenElevatedPS") != null),
                ("customize-contextmenu-take-ownership",     "Take Ownership",             "Adds 'Take Ownership' to the right-click menu",                   AddTakeOwnership,     RemoveTakeOwnership,     () => Registry.ClassesRoot.OpenSubKey(@"*\shell\TakeOwnership") != null),
                ("customize-contextmenu-sfc-scan",           "SFC Scan",                   "Adds 'SFC /SCANNOW' to the desktop right-click menu",             AddSfcScan,           RemoveSfcScan,           () => Registry.ClassesRoot.OpenSubKey(@"Directory\Background\shell\SFC") != null),
                ("customize-contextmenu-chkdsk",             "CHKDSK",                     "Adds a CHKDSK submenu (scan / fix / recover) to the desktop right-click menu", AddChkdsk, RemoveChkdsk,   () => Registry.ClassesRoot.OpenSubKey(@"Directory\Background\shell\CHKDSK") != null),
                ("customize-contextmenu-repair-image",       "Repair Windows Image",       "Adds 'DISM /RestoreHealth' to the desktop right-click menu",      AddRepairImage,       RemoveRepairImage,       () => Registry.ClassesRoot.OpenSubKey(@"Directory\Background\shell\RepairWindowsImage") != null),
                ("customize-contextmenu-compress-to",        "Compress To (classic menu)", "Adds the Win11 'Compress To' (ZIP/7z/TAR) entry to the classic right-click menu — requires 24H2+", AddCompressTo, RemoveCompressTo, () => Registry.ClassesRoot.OpenSubKey(@"*\shell\CompressToFullMenu_ForOldContextMenu") != null),
                ("customize-contextmenu-control-panel",      "Control Panel",              "Adds 'Control Panel' shortcut to the desktop right-click menu",   AddControlPanel,      RemoveControlPanel,      () => Registry.ClassesRoot.OpenSubKey(@"DesktopBackground\shell\ControlPanel") != null),
                ("customize-contextmenu-file-hash",          "File Hash",                  "Adds a 'Hash' submenu to file right-click for SHA/MD5 checksums", AddFileHash,          RemoveFileHash,          () => Registry.ClassesRoot.OpenSubKey(@"*\shell\Hash") != null),
                ("customize-contextmenu-kill-not-responding","Kill Not Responding",        "Adds 'Kill not responding tasks' to the desktop right-click menu",AddKillNotResponding, RemoveKillNotResponding, () => Registry.ClassesRoot.OpenSubKey(@"DesktopBackground\shell\KillNotResponding") != null),
                ("customize-contextmenu-windows-tools",      "Windows Tools",              "Adds 'Windows Tools' shortcut to the desktop right-click menu",   AddWindowsTools,      RemoveWindowsTools,      () => Registry.ClassesRoot.OpenSubKey(@"DesktopBackground\Shell\WindowsTools") != null),
                ("customize-contextmenu-shut-down-menu",     "Shut Down Menu",             "Adds a Shut Down/Restart submenu to the desktop right-click menu",AddShutDownMenu,      RemoveShutDownMenu,      () => Registry.ClassesRoot.OpenSubKey(@"DesktopBackground\shell\ShutDown") != null),
                ("customize-contextmenu-power-plan-assoc",   "Import Power Plan (.pow)",   "Associates .pow files so double-clicking imports a power plan",   AddPowerPlanAssoc,    RemovePowerPlanAssoc,    () => Registry.ClassesRoot.OpenSubKey(@".pow") != null),
                ("customize-contextmenu-run-with-priority",  "Run with Priority",          "Adds 'Run with priority' (Realtime/High) to .exe right-click",    AddRunWithPriority,   RemoveRunWithPriority,   () => Registry.ClassesRoot.OpenSubKey(@"exefile\shell\Priority") != null),
                ("customize-contextmenu-change-resolution",  "Change Resolution",          "Adds 'Change Resolution' to the desktop right-click menu",        AddChangeResolution,  RemoveChangeResolution,  () => Registry.ClassesRoot.OpenSubKey(@"Directory\background\shell\Change Res") != null),
                ("customize-contextmenu-reboot-to-bios",     "Reboot to BIOS",             "Adds 'Reboot To BIOS' to the desktop right-click menu",           AddRebootToBios,      RemoveRebootToBios,      () => Registry.ClassesRoot.OpenSubKey(@"Directory\background\shell\reboot to fw") != null),
            };

            var result = new List<TweakDefinition>();

            foreach (var (id, title, description, add, remove, detect) in entries)
            {
                var capturedTitle  = title;
                var capturedAdd    = add;
                var capturedRemove = remove;
                var capturedDetect = detect;

                result.Add(new TweakDefinition
                {
                    Id           = id,
                    Name         = title,
                    Description  = description,
                    Group        = "Entries",
                    DefaultState = false,
                    ReadState    = () => { try { return capturedDetect(); } catch { return null; } },
                    Apply        = enable =>
                    {
                        try
                        {
                            if (enable) capturedAdd();
                            else        capturedRemove();
                            if (!_suppressRestart) ExplorerRestart.Request();
                            Log($"[CONTEXT] {capturedTitle} {(enable ? "added" : "removed")}.");
                        }
                        catch (Exception ex) { Log($"[CONTEXT] ERROR {capturedTitle}: {ex.Message}"); }
                    },
                });
            }

            return result.ToArray();
        }
    }
}
