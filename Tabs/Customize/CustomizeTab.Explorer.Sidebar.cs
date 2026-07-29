using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── EXPLORER ▸ SIDEBAR ──
        private void BuildExplorerSidebar(StackPanel panel)
        {
            // ── Additional toggles ────────────────────────────────────────────
            var sidebarSection = TweakHelpers.BuildSection(panel, "Sidebar");

            var sidebarToggles = new TweakDefinition[]
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
                        Service?.Log($"[EXPLORER] Folder auto-discovery policy {(enable ? "disabled" : "enabled")}.");
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
                        Service?.Log($"[EXPLORER] Home/Gallery sidebar {(enable ? "hidden" : "restored")}.");
                    }),
                },
            };

            foreach (var def in sidebarToggles)
                TweakHelpers.AddTweakRow(sidebarSection, def);
        }
    }
}
