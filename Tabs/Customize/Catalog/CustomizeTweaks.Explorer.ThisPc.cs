using Microsoft.Win32;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Explorer.ThisPc.cs.
    // Section "This PC Folders" — 6 user-folder rows (loop over ThisPcFolders, Id = tuple id)
    // plus 2 literal rows (3D Objects, duplicate removable drives).
    public static partial class CustomizeTweaks
    {
        public static TweakDefinition[] ExplorerThisPc(Action<string> Log)
        {
            var result = new List<TweakDefinition>();

            foreach (var (id, title, desc, guid) in ThisPcFolders)
            {
                var capturedGuid  = guid;
                var capturedTitle = title;
                result.Add(new TweakDefinition
                {
                    Id          = id,
                    Name        = title,
                    Description = desc,
                    Group       = "This PC Folders",
                    // Not modelled by Winhance. The six user folders ship visible → ON.
                    DefaultState = true,
                    ReadState   = () => SystemStateReader.ReadThisPcFolderVisible(capturedGuid),
                    Apply       = enable => WriteExplorer(() =>
                    {
                        // HiddenByDefault: 0 = shown, 1 = hidden. Written to both the
                        // 64-bit and WOW6432Node views so 32-bit shell hosts agree.
                        foreach (var basePath in MyComputerNameSpaceRoots)
                        {
                            try
                            {
                                using var key = Registry.LocalMachine.CreateSubKey($@"{basePath}\{capturedGuid}");
                                key?.SetValue("HiddenByDefault", enable ? 0 : 1, RegistryValueKind.DWord);
                            }
                            catch { /* HKLM write needs elevation — silently skip */ }
                        }
                        Log($"[EXPLORER] {capturedTitle} {(enable ? "shown" : "hidden")}.");
                    }),
                });
            }

            result.Add(new TweakDefinition
            {
                Id          = "customize-explorer-show-3d-objects",
                Name        = "Show 3D Objects",
                Description = "Restores the 3D Objects folder in This PC — removed by default on Windows 11",
                Group       = "This PC Folders",
                // Not modelled by Winhance. Windows 11 ships without 3D Objects → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadThreeDObjectsShown,
                Apply       = enable => WriteExplorer(() =>
                {
                    const string objGuid = "{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}";
                    foreach (var basePath in MyComputerNameSpaceRoots)
                    {
                        try
                        {
                            if (enable) Registry.LocalMachine.CreateSubKey($@"{basePath}\{objGuid}")?.Dispose();
                            else Registry.LocalMachine.OpenSubKey(basePath, writable: true)
                                     ?.DeleteSubKeyTree(objGuid, throwOnMissingSubKey: false);
                        }
                        catch { /* HKLM write needs elevation — silently skip */ }
                    }
                    Log($"[EXPLORER] 3D Objects {(enable ? "shown" : "hidden")}.");
                }),
            });

            result.Add(new TweakDefinition
            {
                Id          = "customize-explorer-hide-duplicate-removable-drives",
                Name        = "Hide Duplicate Removable Drives",
                Description = "Removes the duplicate USB and removable drive entries Explorer's sidebar shows alongside This PC. The drives themselves are untouched — only the extra listing goes away",
                Group       = "This PC Folders",
                // Not modelled by Winhance. The DelegateFolders key ships present
                // (duplicates shown), so "hidden" is not the factory state → OFF.
                DefaultState = false,
                ReadState   = SystemStateReader.ReadDuplicateRemovableDrivesHidden,
                Apply       = enable => WriteExplorer(() =>
                {
                    const string dupGuid = "{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}";
                    foreach (var basePath in DelegateFolderRoots)
                    {
                        try
                        {
                            if (enable)
                                Registry.LocalMachine.OpenSubKey(basePath, writable: true)
                                    ?.DeleteSubKeyTree(dupGuid, throwOnMissingSubKey: false);
                            else
                            {
                                using var key = Registry.LocalMachine.CreateSubKey($@"{basePath}\{dupGuid}");
                                key?.SetValue("", "Removable Drives", RegistryValueKind.String);
                            }
                        }
                        catch { /* HKLM write needs elevation — silently skip */ }
                    }
                    Log($"[EXPLORER] Duplicate removable drives {(enable ? "hidden" : "restored")}.");
                }),
            });

            return result.ToArray();
        }

        // This PC NameSpace roots — 64-bit view and the WOW6432Node mirror.
        private static readonly string[] MyComputerNameSpaceRoots =
        {
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace",
        };

        private static readonly string[] DelegateFolderRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders",
        };

        // Tweak Id → title → shell NameSpace GUID for the six This PC user folders.
        private static readonly (string Id, string Title, string Desc, string Guid)[] ThisPcFolders =
        {
            ("customize-explorer-this-pc-desktop",   "Show Desktop in This PC",   "Shows the Desktop folder under This PC",   "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"),
            ("customize-explorer-this-pc-documents", "Show Documents in This PC", "Shows the Documents folder under This PC", "{D3162B92-9365-467A-956B-92703ACA08AF}"),
            ("customize-explorer-this-pc-downloads", "Show Downloads in This PC", "Shows the Downloads folder under This PC", "{088E3905-0323-4B02-9826-5D99428E115F}"),
            ("customize-explorer-this-pc-music",     "Show Music in This PC",     "Shows the Music folder under This PC",     "{3DFDF296-DBEC-4FB4-81D1-6A3438BCF4DE}"),
            ("customize-explorer-this-pc-pictures",  "Show Pictures in This PC",  "Shows the Pictures folder under This PC",  "{24AD3AD4-A569-4530-98E1-AB02F9417AA8}"),
            ("customize-explorer-this-pc-videos",    "Show Videos in This PC",    "Shows the Videos folder under This PC",    "{F86FA3AB-70D2-4FC7-9C99-FCBF05467F3A}"),
        };
    }
}
