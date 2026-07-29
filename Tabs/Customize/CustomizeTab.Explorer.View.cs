using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── EXPLORER ▸ VIEW ──
        private void BuildExplorerView(StackPanel panel)
        {
            // ── View ─────────────────────────────────────────────────────────
            var viewSection = TweakHelpers.BuildSection(panel, "View");

            var viewToggles = new TweakDefinition[]
            {
                new()
                {
                    Id          = "customize-explorer-show-file-extensions",
                    Name        = "Show File Extensions",
                    Description = "Shows file extensions for known file types (e.g. .exe, .txt) — hidden by default",
                    Group       = "View",
                    // Winhance explorer-customization-show-file-ext (HideFileExt):
                    // Recommended=0 → EnabledValue → ON; Default=1 → DisabledValue → OFF.
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadShowFileExtensions,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", enable ? 0 : 1)),
                },
                new()
                {
                    Id          = "customize-explorer-show-hidden-files",
                    Name        = "Show Hidden Files",
                    Description = "Makes hidden files and folders visible in Explorer",
                    Group       = "View",
                    // Winhance explorer-customization-show-hidden-files (Hidden):
                    // Recommended=1 → ON; Default=0 → OFF. Same polarity.
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadShowHiddenFiles,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", enable ? 1 : 2)),
                },
                new()
                {
                    Id          = "customize-explorer-show-protected-os-files",
                    Name        = "Show Protected OS Files",
                    Description = "Shows protected system files — useful for advanced users (use with care)",
                    Group       = "View",
                    // Winhance explorer-customization-hide-protected-files (ShowSuperHidden):
                    // Recommended=0 and Default=0 → both DisabledValue → OFF. Same polarity.
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadShowSuperHidden,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSuperHidden", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-full-path-title-bar",
                    Name        = "Show Full Path in Title Bar",
                    Description = "Displays the full folder path in the Explorer title bar instead of just the folder name",
                    Group       = "View",
                    // Winhance explorer-customization-full-path (FullPath) claims Recommended=1,
                    // Default=1. Its Recommended is taken as-is, but its Default is wrong:
                    // Windows ships FullPath=0 (path not shown), so OFF is the factory state.
                    // Corrected in pass-A review — do not re-seed this from Winhance.
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadFullPathTitleBar,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CabinetState", "FullPath", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-full-path-address-bar",
                    Name        = "Show Full Path in Address Bar",
                    Description = "Displays the full path in the address bar instead of breadcrumbs",
                    Group       = "View",
                    // Not modelled by Winhance and no unambiguous factory value → needs-confirm.
                    ReadState   = SystemStateReader.ReadFullPathAddressBar,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CabinetState", "FullPathAddress", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-compact-view",
                    Name        = "Compact View",
                    Description = "Reduces spacing between items in Explorer — more items visible without scrolling",
                    Group       = "View",
                    // Winhance explorer-customization-item-space (UseCompactMode):
                    // Recommended=0 and Default=0 → both OFF. Same polarity.
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = SystemStateReader.ReadCompactView,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "UseCompactMode", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-launch-to-this-pc",
                    Name        = "Launch in This PC",
                    Description = "Opens Explorer to This PC instead of Quick Access",
                    Group       = "View",
                    // Winhance explorer-customization-launch-to models this as a preference
                    // dropdown with no recommended value. Windows opens to Quick Access → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadLaunchToThisPC,
                    Apply       = enable => WriteExplorer(() =>
                        SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo", enable ? 1 : 0)),
                },
                new()
                {
                    Id          = "customize-explorer-show-lnk-extension",
                    Name        = "Show .lnk File Extension",
                    Description = "Shows the .lnk extension on shortcuts. Requires 'Show File Extensions' to be on. Needs admin rights",
                    Group       = "View",
                    // Not modelled by Winhance. Windows hides .lnk out of the box → OFF.
                    DefaultState = false,
                    ReadState   = SystemStateReader.ReadLnkExtensionShown,
                    Apply       = enable => WriteExplorer(() =>
                    {
                        // HKCR\lnkfile\NeverShowExt — show = value absent, hide (default) = empty REG_SZ
                        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\lnkfile", writable: true);
                        if (enable) key?.DeleteValue("NeverShowExt", throwOnMissingValue: false);
                        else        key?.SetValue("NeverShowExt", "", RegistryValueKind.String);
                    }),
                },
            };

            foreach (var def in viewToggles)
                TweakHelpers.AddTweakRow(viewSection, def);

            // Folder Options completion rows live in their own partial to keep
            // this file within the size conventions.
            BuildExplorerViewFolderOptions(viewSection);
        }
    }
}
