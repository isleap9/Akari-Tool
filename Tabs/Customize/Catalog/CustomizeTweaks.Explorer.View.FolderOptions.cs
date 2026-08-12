using Microsoft.Win32;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Explorer.View.FolderOptions.cs.
    // Part of the "View" section (Winhance parity vol.2) — 10 HKCU\...\Explorer\Advanced
    // DWord toggles plus the thumbnail-cache-cleanup row. All are IsPreference. In net8 these
    // were added to the same viewSection as Explorer.View; here they are a separate method that
    // returns the same Group="View" definitions.
    public static partial class CustomizeTweaks
    {
        private const string AdvancedSub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        public static TweakDefinition[] ExplorerViewFolderOptions(Action<string> Log)
        {
            var result = new List<TweakDefinition>
            {
                new()
                {
                    Id          = "customize-explorer-checkbox-select",
                    Name        = "Use Check Boxes to Select Items",
                    Description = "Adds checkboxes next to files and folders for easier multi-selection",
                    Group       = "View",
                    IsPreference = true,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "AutoCheckSelect") is int v ? v == 1 : null,
                    Apply       = enable => WriteExplorer(() => SetHkcu(AdvancedSub, "AutoCheckSelect", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-icons-only",
                    Name        = "Always Show Icons, Never Thumbnails",
                    Description = "Displays generic file icons instead of image/document previews",
                    Group       = "View",
                    IsPreference = true,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "IconsOnly") is int v ? v == 1 : null,
                    Apply       = enable => WriteExplorer(() => SetHkcu(AdvancedSub, "IconsOnly", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-folder-tips",
                    Name        = "Display File Size in Folder Tips",
                    Description = "Shows total size and file count when hovering over folders",
                    Group       = "View",
                    IsPreference = true,
                    // ON deletes the value. NOTE: the folder auto-discovery tweak also writes this value.
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "FolderContentsInfoTip") is int v ? v != 0 : true,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        if (enable)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(AdvancedSub, writable: true);
                            k?.DeleteValue("FolderContentsInfoTip", throwOnMissingValue: false);
                        }
                        else SetHkcu(AdvancedSub, "FolderContentsInfoTip", 0);
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-status-bar",
                    Name        = "Show Status Bar",
                    Description = "Displays the bar at the bottom of Explorer showing item count and selected file sizes",
                    Group       = "View",
                    IsPreference = true,
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "ShowStatusBar") is int v ? v == 1 : null,
                    Apply       = enable => WriteExplorer(() => SetHkcu(AdvancedSub, "ShowStatusBar", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-persist-browsers",
                    Name        = "Restore Previous Folder Windows at Logon",
                    Description = "Reopens Explorer windows that were open when you last shut down or logged off",
                    Group       = "View",
                    IsPreference = true,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "PersistBrowsers") is int v ? v == 1 : null,
                    Apply       = enable => WriteExplorer(() => SetHkcu(AdvancedSub, "PersistBrowsers", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-separate-process",
                    Name        = "Launch Folder Windows in a Separate Process",
                    Description = "Runs each Explorer window in its own process so one crash can't take down all windows",
                    Group       = "View",
                    IsPreference = true,
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "SeparateProcess") is int v ? v == 1 : null,
                    Apply       = enable => WriteExplorer(() => SetHkcu(AdvancedSub, "SeparateProcess", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-encrypted-color",
                    Name        = "Show Encrypted or Compressed NTFS Files in Color",
                    Description = "Displays encrypted files in green and compressed files in blue",
                    Group       = "View",
                    IsPreference = true,
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "ShowEncryptCompressedColor") is int v ? v == 1 : null,
                    Apply       = enable => WriteExplorer(() => SetHkcu(AdvancedSub, "ShowEncryptCompressedColor", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-hide-merge-conflicts",
                    Name        = "Hide Folder Merge Conflicts",
                    Description = "Automatically merges same-name folders without showing a confirmation dialog",
                    Group       = "View",
                    IsPreference = true,
                    // ON deletes the value.
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "HideMergeConflicts") is int v ? v != 0 : true,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        if (enable)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(AdvancedSub, writable: true);
                            k?.DeleteValue("HideMergeConflicts", throwOnMissingValue: false);
                        }
                        else SetHkcu(AdvancedSub, "HideMergeConflicts", 0);
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-hide-empty-drives",
                    Name        = "Hide Empty Drives",
                    Description = "Hides drives with no media inserted, like empty card readers or optical drives",
                    Group       = "View",
                    IsPreference = true,
                    // ON deletes the value.
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "HideDrivesWithNoMedia") is int v ? v != 0 : true,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        if (enable)
                        {
                            using var k = Registry.CurrentUser.OpenSubKey(AdvancedSub, writable: true);
                            k?.DeleteValue("HideDrivesWithNoMedia", throwOnMissingValue: false);
                        }
                        else SetHkcu(AdvancedSub, "HideDrivesWithNoMedia", 0);
                    }),
                },
                new()
                {
                    Id          = "customize-explorer-show-info-tip",
                    Name        = "Show Pop-up Descriptions for Folder and Desktop Items",
                    Description = "Displays a tooltip with item details when hovering over files and folders",
                    Group       = "View",
                    IsPreference = true,
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(AdvancedSub, "ShowInfoTip") is int v ? v == 1 : null,
                    Apply       = enable => WriteExplorer(() => SetHkcu(AdvancedSub, "ShowInfoTip", enable ? 1 : 0)),
                },
            };

            result.Add(new TweakDefinition
            {
                Id          = "customize-explorer-thumbnail-cache-cleanup",
                Name        = "Automatic Thumbnail Cache Cleanup",
                Group       = "View",
                Description = "Let Disk Cleanup automatically clear the thumbnail cache",
                IsPreference = true,
                RecommendedState = true,
                DefaultState     = true,
                ReadState = () => { var v = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches\Thumbnail Cache", "Autorun", null) as int?; return v != 0; },
                Apply = on =>
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches\Thumbnail Cache", "Autorun", on ? 3 : 0, RegistryValueKind.DWord);
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches\Thumbnail Cache", "Autorun", on ? 3 : 0, RegistryValueKind.DWord);
                    Log($"Thumbnail cache cleanup {(on ? "enabled" : "disabled")}.");
                },
            });

            return result.ToArray();
        }
    }
}
