using Microsoft.Win32;

namespace AkariTool.Tabs
{
    // MVVM PORT: extracted verbatim from net8 CustomizeTab.Explorer.View.cs.
    // Section "View" (with some rows grouped "Navigation Pane"/"General") — 8 view toggles
    // plus 12 further View/Nav-Pane/device rows. The Folder Options completion rows live in
    // ExplorerViewFolderOptions (separate partial), exactly as net8 split them.
    public static partial class CustomizeTweaks
    {
        public static TweakDefinition[] ExplorerView(Action<string> Log)
        {
            var result = new List<TweakDefinition>
            {
                new()
                {
                    Id          = "customize-explorer-show-file-extensions",
                    Name        = "Show File Extensions",
                    Description = "Shows file extensions for known file types (e.g. .exe, .txt) — hidden by default",
                    Group       = "View",
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
                new TweakDefinition
                {
                    Id          = "customize-explorer-always-show-menus",
                    Name        = "Always Show Menus",
                    Group       = "View",
                    Description = "Always show the classic File Explorer menu bar",
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "AlwaysShowMenus") is int v ? v != 0 : false,
                    Apply       = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "AlwaysShowMenus", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Always show menus {(on ? "on" : "off")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-preview-handlers",
                    Name        = "Show Preview Handlers in Preview Pane",
                    Group       = "View",
                    Description = "Render file contents in the preview pane",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowPreviewHandlers") is int v ? v != 0 : true,
                    Apply       = on =>
                    {
                        const string s = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(s, true); k?.DeleteValue("ShowPreviewHandlers", false); }
                        else Registry.SetValue(@"HKEY_CURRENT_USER\" + s, "ShowPreviewHandlers", 0, RegistryValueKind.DWord);
                        Log($"Preview handlers {(on ? "on" : "off")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-icon-on-thumbnails",
                    Name        = "Display File Icon on Thumbnails",
                    Group       = "View",
                    Description = "Overlay the file-type icon on thumbnail previews",
                    RecommendedState = true,
                    DefaultState     = false,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTypeOverlay") is int v ? v != 0 : false,
                    Apply       = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "ShowTypeOverlay", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Thumbnail type overlay {(on ? "on" : "off")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-office-files",
                    Name        = "Show Files from Office.com",
                    Group       = "View",
                    Description = "Show recent Office.com cloud files in Quick Access",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowCloudFilesInQuickAccess") is int v ? v != 0 : true,
                    Apply       = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            "ShowCloudFilesInQuickAccess", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Office.com files {(on ? "shown" : "hidden")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-recommendations",
                    Name        = "Show Recommendations",
                    Group       = "View",
                    Description = "Show the recommended files section in File Explorer Home",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecommendations") is int v ? v != 0 : true,
                    Apply       = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            "ShowRecommendations", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Explorer recommendations {(on ? "shown" : "hidden")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-nav-cloud-states",
                    Name        = "Always Show Availability Status",
                    Group       = "Navigation Pane",
                    Description = "Always show cloud sync availability icons in the navigation pane",
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "NavPaneShowAllCloudStates") is int v ? v != 0 : false,
                    Apply       = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "NavPaneShowAllCloudStates", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Nav availability status {(on ? "on" : "off")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-nav-expand-current",
                    Name        = "Expand to Open Folder",
                    Group       = "Navigation Pane",
                    Description = "Automatically expand the navigation pane to the current folder",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "NavPaneExpandToCurrentFolder") is int v ? v != 0 : true,
                    Apply       = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "NavPaneExpandToCurrentFolder", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Nav expand to current {(on ? "on" : "off")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-nav-all-folders",
                    Name        = "Show All Folders",
                    Group       = "Navigation Pane",
                    Description = "Show all folders (including Desktop, Recycle Bin) in the navigation pane",
                    RecommendedState = false,
                    DefaultState     = false,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "NavPaneShowAllFolders") is int v ? v != 0 : false,
                    Apply       = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "NavPaneShowAllFolders", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Nav show all folders {(on ? "on" : "off")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-devices-default-printer",
                    Name        = "Automatic Default Printer Management",
                    Group       = "View",
                    Description = "Let Windows manage which printer is the default (your last-used printer)",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState   = () => ReadDwordCu(@"Software\Microsoft\Windows NT\CurrentVersion\Windows", "LegacyDefaultPrinterMode") is int v ? v == 0 : true,
                    Apply       = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Windows",
                            "LegacyDefaultPrinterMode", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Automatic default printer {(on ? "on" : "off")}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-typing-behavior",
                    Name        = "When Typing into List View",
                    Group       = "View",
                    Description = "What File Explorer does when you type in a list view",
                    IsPreference = true,
                    InputKind   = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Select the typed item in the view", 0, IsDefault: true),
                        new TweakDropdownOption("Automatically type into the Search Box", 1),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TypeAhead");
                        return v switch { 0 => 0, 1 => 1, _ => 0 };
                    },
                    ApplyIndex = idx =>
                    {
                        if (idx < 0 || idx > 1) return;
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            "TypeAhead", idx, RegistryValueKind.DWord);
                        Log($"List-view typing set to option {idx}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-icon-cache-size",
                    Name        = "Icon Cache Size",
                    Group       = "View",
                    Description = "Maximum number of icons Windows caches (larger can reduce icon reload flicker)",
                    IsPreference = true,
                    InputKind   = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Windows default",         "default", IsDefault: true),
                        new TweakDropdownOption("Large (4,096 icons)",     "4096"),
                        new TweakDropdownOption("Very large (8,192 icons)", "8192"),
                    },
                    ReadCurrentIndex = () =>
                    {
                        var v = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "MaxCachedIcons", null) as string;
                        return v switch { "4096" => 1, "8192" => 2, _ => 0 };
                    },
                    ApplyIndex = idx =>
                    {
                        const string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer";
                        if (idx == 0) { using var k = Registry.LocalMachine.OpenSubKey(sub, true); k?.DeleteValue("MaxCachedIcons", false); }
                        else Registry.SetValue(@"HKEY_LOCAL_MACHINE\" + sub, "MaxCachedIcons", idx == 1 ? "4096" : "8192", RegistryValueKind.String);
                        Log($"Icon cache size set to option {idx}.");
                    },
                },
                new TweakDefinition
                {
                    Id          = "customize-explorer-click-items",
                    Name        = "Click Items As Follows",
                    Group       = "General",
                    Description = "Open files and folders with a single click, or the traditional double-click",
                    IsPreference = true,
                    InputKind   = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Double-click to open (single-click to select)", 0, IsDefault: true),
                        new TweakDropdownOption("Single-click to open (underline titles like a browser)", 1),
                        new TweakDropdownOption("Single-click to open (underline titles on hover)", 2),
                    },
                    ReadCurrentIndex = () =>
                    {
                        if (ShellStateDoubleClick()) return 0;
                        var u = ReadDwordCu(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "IconUnderline");
                        return u == 2 ? 2 : 1;
                    },
                    ApplyIndex = idx =>
                    {
                        const string ek = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer";
                        WriteShellStateDoubleClick(idx == 0);
                        Registry.SetValue(ek, "IconUnderline", idx == 2 ? 2 : 3, RegistryValueKind.DWord);
                        Log($"Click behavior set to option {idx}.");
                    },
                },
            };

            return result.ToArray();
        }
    }
}
