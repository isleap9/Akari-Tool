using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Explorer.Sidebar.cs.
    // Section "Sidebar"/"Navigation Pane" — 2 sidebar toggles, 6 nav-pane personal-folder
    // rows (loop via the NavPaneFolder generator), and 1 Libraries row.
    public static partial class CustomizeTweaks
    {
        public static TweakDefinition[] ExplorerSidebar(Action<string> Log)
        {
            var result = new List<TweakDefinition>
            {
                new()
                {
                    Id          = "customize-explorer-folder-auto-discovery-policy",
                    Name        = "Disable Folder Auto-Discovery (Policy)",
                    Description = "Sets FolderType to NotSpecified — stops Explorer overriding your view layout based on folder content",
                    Group       = "Sidebar",
                    // Not modelled by Winhance. FolderType is unset out of the box → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadFolderDiscoveryDisabled,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        const string path = @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags\AllFolders\Shell";
                        if (enable)
                        {
                            using var key = Registry.CurrentUser.CreateSubKey(path);
                            key?.SetValue("FolderType", "NotSpecified", RegistryValueKind.String);
                        }
                        else
                        {
                            using var key = Registry.CurrentUser.OpenSubKey(path, writable: true);
                            key?.DeleteValue("FolderType", throwOnMissingValue: false);
                        }
                        Log($"[EXPLORER] Folder auto-discovery policy {(enable ? "disabled" : "enabled")}.");
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-hide-home-gallery-hkcu",
                    Name        = "Hide Home from Sidebar (HKCU)",
                    Description = "Sets System.IsPinnedToNameSpaceTree=0 on Home and Gallery CLSIDs — hides them from Explorer sidebar",
                    Group       = "Sidebar",
                    // Not modelled by Winhance. Value absent out of the box → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadHomeHidden,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        const string homeGuid    = "{f874310e-b6b7-47dc-bc84-b9e6b38f5903}";
                        const string galleryGuid = "{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}";
                        foreach (var guid in new[] { homeGuid, galleryGuid })
                        {
                            var path = $@"Software\Classes\CLSID\{guid}";
                            if (enable)
                            {
                                using var key = Registry.CurrentUser.CreateSubKey(path);
                                key?.SetValue("System.IsPinnedToNameSpaceTree", 0, RegistryValueKind.DWord);
                            }
                            else
                            {
                                using var key = Registry.CurrentUser.OpenSubKey(path, writable: true);
                                key?.DeleteValue("System.IsPinnedToNameSpaceTree", throwOnMissingValue: false);
                            }
                        }
                        Log($"[EXPLORER] Home/Gallery sidebar {(enable ? "hidden" : "restored")}.");
                    }),
                },
            };

            // ── Navigation Pane personal folders (Winhance parity) ────────────
            foreach (var (id, name, guid) in new[]
            {
                ("customize-navpane-desktop",   "Show Desktop Folder",   "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"),
                ("customize-navpane-documents", "Show Documents Folder", "{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}"),
                ("customize-navpane-downloads", "Show Downloads Folder", "{374DE290-123F-4565-9164-39C4925E467B}"),
                ("customize-navpane-music",     "Show Music Folder",     "{1CF1260C-4DD0-4ebb-811F-33C572699FDE}"),
                ("customize-navpane-pictures",  "Show Pictures Folder",  "{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}"),
                ("customize-navpane-videos",    "Show Videos Folder",    "{A0953C92-50DC-43bf-BE83-3742FED03C9C}"),
            })
                result.Add(NavPaneFolder(Log, id, name, guid));

            result.Add(new TweakDefinition
            {
                Id = "customize-navpane-libraries", Name = "Show Libraries in Navigation Pane", Group = "Navigation Pane",
                Description = "Pin the Libraries node to the File Explorer navigation pane",
                IsPreference = true, RecommendedState = false, DefaultState = false,
                ReadState = () => { var v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Classes\CLSID\{031E4825-7B94-4dc3-B131-E946B44C8DD5}", "System.IsPinnedToNameSpaceTree", null) as int?; return v == 1; },
                Apply = on =>
                {
                    const string guid = "{031E4825-7B94-4dc3-B131-E946B44C8DD5}";
                    const string nonEnum = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum";
                    string ns = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{guid}";
                    using (var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\CLSID\{guid}")) k?.SetValue("System.IsPinnedToNameSpaceTree", on ? 1 : 0, RegistryValueKind.DWord);
                    if (on) { using (var k = Registry.LocalMachine.OpenSubKey(nonEnum, true)) k?.DeleteValue(guid, false);
                              using (var k = Registry.LocalMachine.OpenSubKey(ns, true)) k?.DeleteValue("HiddenByDefault", false); }
                    else    { using (var k = Registry.LocalMachine.CreateSubKey(nonEnum)) k?.SetValue(guid, 1, RegistryValueKind.DWord);
                              using (var k = Registry.LocalMachine.CreateSubKey(ns)) k?.SetValue("HiddenByDefault", 1, RegistryValueKind.DWord); }
                    Log($"Libraries in nav pane {(on ? "shown" : "hidden")}.");
                },
            });

            return result.ToArray();
        }

        // Factory: one navigation-pane personal-folder toggle. Show = clear the
        // NonEnum policy + HiddenByDefault; hide = set both. Distinct from the
        // This-PC rows (MyComputer\NameSpace) and desktop-icon rows.
        private static TweakDefinition NavPaneFolder(Action<string> Log, string id, string name, string guid) => new TweakDefinition
        {
            Id = id, Name = name, Group = "Navigation Pane",
            Description = "Show this personal folder in the File Explorer navigation pane",
            IsPreference = true,
            ReadState = () => { var v = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum", guid, null) as int?; return v != 1; },
            Apply = on =>
            {
                const string nonEnum = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum";
                string ns = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{guid}";
                if (on) { using (var k = Registry.LocalMachine.OpenSubKey(nonEnum, true)) k?.DeleteValue(guid, false);
                          using (var k = Registry.LocalMachine.OpenSubKey(ns, true)) k?.DeleteValue("HiddenByDefault", false); }
                else    { using (var k = Registry.LocalMachine.CreateSubKey(nonEnum)) k?.SetValue(guid, 1, RegistryValueKind.DWord);
                          using (var k = Registry.LocalMachine.CreateSubKey(ns)) k?.SetValue("HiddenByDefault", 1, RegistryValueKind.DWord); }
                Log($"{name} {(on ? "shown" : "hidden")}.");
            },
        };
    }
}
