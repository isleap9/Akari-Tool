using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class ExplorerOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildView(),
        .. BuildBehavior(),
        .. BuildAssociations(),
        .. BuildSidebar(),
        .. BuildThisPc(),
    ];

    private static IReadOnlyList<SettingGroup> BuildView() =>
    [
        new SettingGroup
        {
            Name = "View",
            FeatureId = "customize-explorer-view",
            Settings = new[]
            {
                // ── ExplorerView (CustomizeTweaks.Explorer.View.cs) ──────────────────────

                // customize-explorer-show-file-extensions — HideFileExt (inverted): show=0/hide=1
                new SettingDefinition
                {
                    Id = "customize-explorer-show-file-extensions",
                    Icon = "DocumentQuestionMark",
                    IconPack = "Fluent",
                    Name = "Show File Extensions",
                    Description = "Shows file extensions for known file types (e.g. .exe, .txt) — hidden by default",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "HideFileExt",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-explorer-show-hidden-files — Hidden: show=1/hide=2
                new SettingDefinition
                {
                    Id = "customize-explorer-show-hidden-files",
                    Icon = "FileEyeOutline",
                    Name = "Show Hidden Files",
                    Description = "Makes hidden files and folders visible in Explorer",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Hidden",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 2 },
                        },
                    },
                },
                // customize-explorer-show-protected-os-files — ShowSuperHidden: show=1/hide=0
                new SettingDefinition
                {
                    Id = "customize-explorer-show-protected-os-files",
                    Icon = "FileHidden",
                    Name = "Show Protected OS Files",
                    Description = "Shows protected system files — useful for advanced users (use with care)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowSuperHidden",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-full-path-title-bar — CabinetState FullPath: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-full-path-title-bar",
                    Icon = "PanelTopExpand",
                    IconPack = "Fluent",
                    Name = "Show Full Path in Title Bar",
                    Description = "Displays the full folder path in the Explorer title bar instead of just the folder name",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CabinetState",
                            ValueName = "FullPath",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-full-path-address-bar — CabinetState FullPathAddress: on=1/off=0
                // (Not modelled by Winhance and no unambiguous factory value → no Recommended/Default state.)
                new SettingDefinition
                {
                    Id = "customize-explorer-full-path-address-bar",
                    Icon = "TextBoxOutline",
                    Name = "Show Full Path in Address Bar",
                    Description = "Displays the full path in the address bar instead of breadcrumbs",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CabinetState",
                            ValueName = "FullPathAddress",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-compact-view — UseCompactMode: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-compact-view",
                    Icon = "ViewCompact",
                    Name = "Compact View",
                    Description = "Reduces spacing between items in Explorer — more items visible without scrolling",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "UseCompactMode",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-launch-to-this-pc — LaunchTo: on=1/off=0; DefaultState only
                new SettingDefinition
                {
                    Id = "customize-explorer-launch-to-this-pc",
                    Icon = "FolderOpen",
                    IconPack = "Fluent",
                    Name = "Launch in This PC",
                    Description = "Opens Explorer to This PC instead of Quick Access",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "LaunchTo",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-show-lnk-extension — HKCR lnkfile NeverShowExt (inverted):
                //   show = value absent, hide (default) = empty REG_SZ. DefaultState only.
                new SettingDefinition
                {
                    Id = "customize-explorer-show-lnk-extension",
                    Icon = "DocumentQuestionMark",
                    IconPack = "Fluent",
                    Name = "Show .lnk File Extension",
                    Description = "Shows the .lnk extension on shortcuts. Requires 'Show File Extensions' to be on. Needs admin rights",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\lnkfile",
                            ValueName = "NeverShowExt",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { "" },
                        },
                    },
                },
                // customize-explorer-always-show-menus — AlwaysShowMenus: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-always-show-menus",
                    Icon = "WindowApps",
                    IconPack = "Fluent",
                    Name = "Always Show Menus",
                    Description = "Always show the classic File Explorer menu bar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "AlwaysShowMenus",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-preview-handlers — ShowPreviewHandlers: on = absent, off = 0
                new SettingDefinition
                {
                    Id = "customize-explorer-preview-handlers",
                    Icon = "TableEye",
                    Name = "Show Preview Handlers in Preview Pane",
                    Description = "Render file contents in the preview pane",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowPreviewHandlers",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-icon-on-thumbnails — ShowTypeOverlay: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-icon-on-thumbnails",
                    Icon = "DocumentImage",
                    IconPack = "Fluent",
                    Name = "Display File Icon on Thumbnails",
                    Description = "Overlay the file-type icon on thumbnail previews",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowTypeOverlay",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-office-files — ShowCloudFilesInQuickAccess: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-office-files",
                    Icon = "FileCloud",
                    Name = "Show Files from Office.com",
                    Description = "Show recent Office.com cloud files in Quick Access",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowCloudFilesInQuickAccess",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-recommendations — ShowRecommendations: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-recommendations",
                    Icon = "StarOutline",
                    Name = "Show Recommendations",
                    Description = "Show the recommended files section in File Explorer Home",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowRecommendations",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-nav-cloud-states — NavPaneShowAllCloudStates: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-nav-cloud-states",
                    Icon = "ArchiveSync",
                    Name = "Always Show Availability Status",
                    Description = "Always show cloud sync availability icons in the navigation pane",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "NavPaneShowAllCloudStates",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-nav-expand-current — NavPaneExpandToCurrentFolder: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-nav-expand-current",
                    Icon = "FileTree",
                    Name = "Expand to Open Folder",
                    Description = "Automatically expand the navigation pane to the current folder",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "NavPaneExpandToCurrentFolder",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-nav-all-folders — NavPaneShowAllFolders: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-nav-all-folders",
                    Icon = "FolderMultiple",
                    Name = "Show All Folders",
                    Description = "Show all folders (including Desktop, Recycle Bin) in the navigation pane",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "NavPaneShowAllFolders",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-devices-default-printer — LegacyDefaultPrinterMode (inverted):
                //   auto (on)=0 / manual (off)=1
                new SettingDefinition
                {
                    Id = "customize-devices-default-printer",
                    Icon = "PrinterOff",
                    Name = "Automatic Default Printer Management",
                    Description = "Let Windows manage which printer is the default (your last-used printer)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows",
                            ValueName = "LegacyDefaultPrinterMode",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-explorer-typing-behavior — Selection: TypeAhead (0 default / 1)
                new SettingDefinition
                {
                    Id = "customize-explorer-typing-behavior",
                    Icon = "KeyboardOutline",
                    Name = "When Typing into List View",
                    Description = "What File Explorer does when you type in a list view",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    ResolveUnmatchedToDefault = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TypeAhead",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = null,
                            DisabledValue = null,
                            IsPrimary = true,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption { DisplayName = "Select the typed item in the view", IsDefault = true, IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["TypeAhead"] = 0 } },
                            new ComboBoxOption { DisplayName = "Automatically type into the Search Box", ValueMappings = new Dictionary<string, object?> { ["TypeAhead"] = 1 } },
                        },
                    },
                },
                // customize-explorer-icon-cache-size — Selection: MaxCachedIcons (default absent / "4096" / "8192")
                new SettingDefinition
                {
                    Id = "customize-explorer-icon-cache-size",
                    Icon = "Cached",
                    Name = "Icon Cache Size",
                    Description = "Maximum number of icons Windows caches (larger can reduce icon reload flicker)",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    ResolveUnmatchedToDefault = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "MaxCachedIcons",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = null,
                            DisabledValue = null,
                            IsPrimary = true,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption { DisplayName = "Windows default", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["MaxCachedIcons"] = null } },
                            new ComboBoxOption { DisplayName = "Large (4,096 icons)", ValueMappings = new Dictionary<string, object?> { ["MaxCachedIcons"] = "4096" } },
                            new ComboBoxOption { DisplayName = "Very large (8,192 icons)", ValueMappings = new Dictionary<string, object?> { ["MaxCachedIcons"] = "8192" } },
                        },
                    },
                },
                // customize-explorer-click-items — SingleClick: on=1 / off=0 (or absent)
                new SettingDefinition
                {
                    Id = "customize-explorer-click-items",
                    Icon = "CursorClick",
                    IconPack = "Fluent",
                    Name = "Single-Click to Open Items",
                    Description = "Opens files and folders with a single click instead of a double-click",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "SingleClick",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0, null },
                        }
                    },
                },
                // customize-explorer-classic-context-menu — Winhance explorer-customization-context-menu 1:1:
                //   CLSID InprocServer32 empty-default-value shim (enable = write @="" / disable = delete @)
                new SettingDefinition
                {
                    Id = "customize-explorer-classic-context-menu",
                    Icon = "Navigation",
                    IconPack = "Fluent",
                    Name = "Classic Right-Click Menu",
                    Description = "Restores the Windows 10 style right-click context menu in Windows 11",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    IsWindows11Only = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RestartProcess = "explorer",
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                            ValueName = "",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = "",
                            EnabledValue = new object?[] { "" },
                            DisabledValue = new object?[] { null },
                            DefaultValue = null,
                        }
                    },
                },

                // ── ExplorerViewFolderOptions (CustomizeTweaks.Explorer.View.FolderOptions.cs) ──

                new SettingDefinition
                {
                    Id = "customize-explorer-sync-provider-notifications",
                    Icon = "CloudSync",
                    Name = "Sync Provider Notifications",
                    Description = "Show notifications from sync providers like OneDrive in File Explorer",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowSyncProviderNotifications",
                            ValueType = RegistryValueKind.DWord,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            IsPrimary = true,
                        }
                    },
                },
                // customize-explorer-checkbox-select — AutoCheckSelect: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-checkbox-select",
                    Icon = "CheckboxMarked",
                    Name = "Use Check Boxes to Select Items",
                    Description = "Adds checkboxes next to files and folders for easier multi-selection",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "AutoCheckSelect",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-icons-only — IconsOnly: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-icons-only",
                    Icon = "ImageOff",
                    Name = "Always Show Icons, Never Thumbnails",
                    Description = "Displays generic file icons instead of image/document previews",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "IconsOnly",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-folder-tips — FolderContentsInfoTip: on = absent, off = 0
                new SettingDefinition
                {
                    Id = "customize-explorer-folder-tips",
                    Icon = "DocumentEndnote",
                    IconPack = "Fluent",
                    Name = "Display File Size in Folder Tips",
                    Description = "Shows total size and file count when hovering over folders",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "FolderContentsInfoTip",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-status-bar — ShowStatusBar: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-status-bar",
                    Icon = "DockBottom",
                    Name = "Show Status Bar",
                    Description = "Displays the bar at the bottom of Explorer showing item count and selected file sizes",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowStatusBar",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-persist-browsers — PersistBrowsers: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-persist-browsers",
                    Icon = "WindowAd",
                    IconPack = "Fluent",
                    Name = "Restore Previous Folder Windows at Logon",
                    Description = "Reopens Explorer windows that were open when you last shut down or logged off",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "PersistBrowsers",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-separate-process — SeparateProcess: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-separate-process",
                    Icon = "WindowRestore",
                    Name = "Launch Folder Windows in a Separate Process",
                    Description = "Runs each Explorer window in its own process so one crash can't take down all windows",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "SeparateProcess",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-encrypted-color — ShowEncryptCompressedColor: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-encrypted-color",
                    Icon = "DocumentLock",
                    IconPack = "Fluent",
                    Name = "Show Encrypted or Compressed NTFS Files in Color",
                    Description = "Displays encrypted files in green and compressed files in blue",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowEncryptCompressedColor",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-hide-merge-conflicts — HideMergeConflicts: on = absent, off = 0
                new SettingDefinition
                {
                    Id = "customize-explorer-hide-merge-conflicts",
                    Icon = "FolderAlert",
                    Name = "Hide Folder Merge Conflicts",
                    Description = "Automatically merges same-name folders without showing a confirmation dialog",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "HideMergeConflicts",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-hide-empty-drives — HideDrivesWithNoMedia: on = absent, off = 0
                new SettingDefinition
                {
                    Id = "customize-explorer-hide-empty-drives",
                    Icon = "HarddiskRemove",
                    Name = "Hide Empty Drives",
                    Description = "Hides drives with no media inserted, like empty card readers or optical drives",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "HideDrivesWithNoMedia",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-show-info-tip — ShowInfoTip: on=1/off=0
                new SettingDefinition
                {
                    Id = "customize-explorer-show-info-tip",
                    Icon = "TooltipText",
                    Name = "Show Pop-up Descriptions for Folder and Desktop Items",
                    Description = "Displays a tooltip with item details when hovering over files and folders",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowInfoTip",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-explorer-thumbnail-cache-cleanup — Thumbnail Cache Autorun: on=3/off=0
                //   (two HKLM keys: native + WOW6432Node)
                new SettingDefinition
                {
                    Id = "customize-explorer-thumbnail-cache-cleanup",
                    Icon = "ImageMultiple",
                    IconPack = "Fluent",
                    Name = "Automatic Thumbnail Cache Cleanup",
                    Description = "Let Disk Cleanup automatically clear the thumbnail cache",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches\Thumbnail Cache",
                            ValueName = "Autorun",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches\Thumbnail Cache",
                            ValueName = "Autorun",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildBehavior() =>
    [
        new SettingGroup
        {
            Name = "Behavior",
            FeatureId = "customize-explorer-behavior",
            Settings = new[]
            {
                // customize-explorer-disable-folder-auto-discovery — writes two values:
                //   enable = FolderContentsInfoTip 0 + ClassicViewState 1; disable = both absent.
                //   Recommended off (Winhance parity: folder tips shown); stock = both absent → Default off.
                new SettingDefinition
                {
                    Id = "customize-explorer-disable-folder-auto-discovery",
                    Icon = "FolderSearchOutline",
                    Name = "Disable Folder Auto-Discovery",
                    Description = "Stops Explorer from auto-changing folder view templates based on content",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "FolderContentsInfoTip",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { null },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ClassicViewState",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { null },
                        },
                    },
                },
                // customize-explorer-disable-quick-access — ShowRecent + ShowFrequent (same key,
                //   same values), inverted: disable (on)=0 / show (off)=1.
                new SettingDefinition
                {
                    Id = "customize-explorer-disable-quick-access",
                    Icon = "StarOffOutline",
                    Name = "Disable Quick Access",
                    Description = "Stops Explorer from showing recent files and frequent folders in Quick Access",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowRecent",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowFrequent",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // [DEFERRED: customize-explorer-hide-home-delegate-folder — HKLM subkey
                //  create/delete not a value write]

                // [DEFERRED: customize-explorer-hide-gallery-sidebar — HKLM subkey
                //  create/delete not a value write]

                // customize-explorer-disable-sharing-wizard — SharingWizardOn (inverted):
                //   disable (on)=0 / wizard on (off)=1.
                new SettingDefinition
                {
                    Id = "customize-explorer-disable-sharing-wizard",
                    Icon = "ShareAndroid",
                    IconPack = "Fluent",
                    Name = "Disable Sharing Wizard",
                    Description = "Disables the simplified sharing wizard — enables advanced sharing dialog",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "SharingWizardOn",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "customize-explorer-show-drive-letters-first",
                    Icon = "AlphaCBox",
                    Name = "Show Drive Letters First",
                    Description = "Displays drive letters before the drive name in File Explorer (e.g. C: Windows instead of Windows (C:))",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    ResolveUnmatchedToDefault = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowDriveLettersFirst",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 2 },
                            IsPrimary = true,
                        },
                    },
                },
                // customize-explorer-disable-autoplay — HKCU primary + HKLM secondary:
                //   HKCU DisableAutoplay on=1/off=0; HKLM NoDriveTypeAutoRun on=255/off=absent
                //   (delete restores default). HKLM may fail without elevation; HKCU still applies.
                new SettingDefinition
                {
                    Id = "customize-explorer-disable-autoplay",
                    Icon = "PlayBox",
                    Name = "Disable AutoPlay",
                    Description = "Stops the AutoPlay dialog when inserting USB drives, DVDs, or SD cards, and blocks autorun on all drive types. Off restores the Windows default",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers",
                            ValueName = "DisableAutoplay",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                            ValueName = "NoDriveTypeAutoRun",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 255 },
                            DisabledValue = new object?[] { null },
                        },
                    },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildAssociations() =>
    [
        new SettingGroup
        {
            Name = "File Associations",
            FeatureId = "customize-explorer-associations",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "customize-explorer-enable-photo-viewer",
                    Icon = "ImageOutline",
                    Name = "Windows Photo Viewer",
                    Description = "Restores Windows Photo Viewer as an option for opening image files (BMP, GIF, JPG, PNG, TIF)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[] { new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities\FileAssociations", ValueName = null, ValueType = RegistryValueKind.None, RecommendedValue = null, DefaultValue = null, EnabledValue = null, DisabledValue = null, IsPrimary = true } },
                    RegContents = new[] { new RegContentSetting { EnabledContent = "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows Photo Viewer\\Capabilities\\FileAssociations]\r\n\".bmp\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".gif\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".jpg\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".jpeg\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".png\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".tif\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".tiff\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows Photo Viewer\\Capabilities\\FileAssociations]\r\n\".bmp\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".gif\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".jpg\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".jpeg\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".png\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".tif\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n\".tiff\"=\"PhotoViewer.FileAssoc.Tiff\"\r\n", DisabledContent = "Windows Registry Editor Version 5.00\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows Photo Viewer\\Capabilities\\FileAssociations]\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows Photo Viewer\\Capabilities\\FileAssociations]\r\n" } },
                },
                new SettingDefinition
                {
                    Id = "customize-explorer-legacy-notepad",
                    Icon = "NotepadEdit",
                    IconPack = "Fluent",
                    Name = "Use Legacy Notepad",
                    Description = "Opens .txt files with classic Notepad instead of the modern Windows 11 Notepad app",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[] { new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Applications\notepad.exe\shell\open\command", ValueName = null, ValueType = RegistryValueKind.None, RecommendedValue = null, DefaultValue = null, EnabledValue = null, DisabledValue = null, IsPrimary = true } },
                    RegContents = new[] { new RegContentSetting { EnabledContent = "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\Applications\\notepad.exe]\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\Applications\\notepad.exe\\shell]\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\Applications\\notepad.exe\\shell\\open]\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\Applications\\notepad.exe\\shell\\open\\command]\r\n@=\"\\\"C:\\\\Windows\\\\System32\\\\notepad.exe\\\" \\\"%1\\\"\"\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\notepad.exe]\r\n\"useFilter\"=dword:00000001\r\n", DisabledContent = "Windows Registry Editor Version 5.00\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\Applications\\notepad.exe\\shell\\open\\command]\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\Applications\\notepad.exe\\shell\\open]\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\Applications\\notepad.exe\\shell]\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Classes\\Applications\\notepad.exe]\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\notepad.exe]\r\n" } },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildSidebar() =>
    [
        new SettingGroup
        {
            Name = "Sidebar",
            FeatureId = "customize-explorer-sidebar",
            Settings = new[]
            {
                // customize-explorer-folder-auto-discovery-policy — Bags\AllFolders\Shell
                //   FolderType: enable = "NotSpecified" / disable = absent.
                new SettingDefinition
                {
                    Id = "customize-explorer-folder-auto-discovery-policy",
                    Icon = "FolderCogOutline",
                    Name = "Disable Folder Auto-Discovery (Policy)",
                    Description = "Sets FolderType to NotSpecified — stops Explorer overriding your view layout based on folder content",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags\AllFolders\Shell",
                            ValueName = "FolderType",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "NotSpecified" },
                            DisabledValue = new object?[] { null },
                        },
                    },
                },
                // customize-explorer-hide-home-gallery-hkcu — System.IsPinnedToNameSpaceTree
                //   on Home + Gallery CLSIDs: enable (hide) = 0 / disable (show) = absent.
                new SettingDefinition
                {
                    Id = "customize-explorer-hide-home-gallery-hkcu",
                    Icon = "Home",
                    Name = "Hide Home from Sidebar (HKCU)",
                    Description = "Sets System.IsPinnedToNameSpaceTree=0 on Home and Gallery CLSIDs — hides them from Explorer sidebar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}",
                            ValueName = "System.IsPinnedToNameSpaceTree",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { null },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}",
                            ValueName = "System.IsPinnedToNameSpaceTree",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { null },
                        },
                    },
                },
                // ── Navigation Pane personal folders (NavPaneFolder factory) ─────────────
                //   Show = NonEnum {guid} absent + NameSpace\{guid} HiddenByDefault absent;
                //   Hide = both = 1. Read consults NonEnum {guid} (!= 1 → shown).
                NavPaneFolder("customize-navpane-desktop",   "Show Desktop Folder",   "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", "Monitor"),
                NavPaneFolder("customize-navpane-documents", "Show Documents Folder", "{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}", "FileDocument"),
                NavPaneFolder("customize-navpane-downloads", "Show Downloads Folder", "{374DE290-123F-4565-9164-39C4925E467B}", "Download"),
                NavPaneFolder("customize-navpane-music",     "Show Music Folder",     "{1CF1260C-4DD0-4ebb-811F-33C572699FDE}", "Music"),
                NavPaneFolder("customize-navpane-pictures",  "Show Pictures Folder",  "{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}", "Image"),
                NavPaneFolder("customize-navpane-videos",    "Show Videos Folder",    "{A0953C92-50DC-43bf-BE83-3742FED03C9C}", "Video"),
                // customize-navpane-libraries — HKCU CLSID pinned (primary read, on=1/off=0)
                //   plus HKLM NonEnum {guid} + NameSpace\{guid} HiddenByDefault enforcement
                //   (show = absent / hide = 1).
                new SettingDefinition
                {
                    Id = "customize-navpane-libraries",
                    Icon = "FolderTable",
                    Name = "Show Libraries in Navigation Pane",
                    Description = "Pin the Libraries node to the File Explorer navigation pane",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{031E4825-7B94-4dc3-B131-E946B44C8DD5}",
                            ValueName = "System.IsPinnedToNameSpaceTree",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0, null },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                            ValueName = "{031E4825-7B94-4dc3-B131-E946B44C8DD5}",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{031E4825-7B94-4dc3-B131-E946B44C8DD5}",
                            ValueName = "HiddenByDefault",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
            },
        },
    ];

    // Factory: one navigation-pane personal-folder toggle. Show = clear the NonEnum policy
    // value + NameSpace HiddenByDefault; hide = set both to 1. Read consults NonEnum {guid}.
    private static SettingDefinition NavPaneFolder(string id, string name, string guid, string icon) =>
        new SettingDefinition
        {
            Id = id,
            Icon = icon,
            Name = name,
            Description = "Show this personal folder in the File Explorer navigation pane",
            InputType = InputType.Toggle,
            IsSubjectivePreference = true,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                    ValueName = guid,
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = null,
                    DefaultValue = null,
                    EnabledValue = new object?[] { null },
                    DisabledValue = new object?[] { 1 },
                },
                new RegistrySetting
                {
                    KeyPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{guid}",
                    ValueName = "HiddenByDefault",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = null,
                    DefaultValue = null,
                    EnabledValue = new object?[] { null },
                    DisabledValue = new object?[] { 1 },
                },
            },
        };

    private static IReadOnlyList<SettingGroup> BuildThisPc() =>
    [
        new SettingGroup
        {
            Name = "This PC Folders",
            FeatureId = "customize-explorer-thispc",
            Settings = new[]
            {
                // ── Six user folders (ThisPcFolders loop) ────────────────────────────────
                //   HiddenByDefault written to the 64-bit + WOW6432Node MyComputer\NameSpace
                //   views: show (on) = 0 / hide (off) = 1. Ship visible → DefaultState on.
                ThisPcFolder("customize-explorer-this-pc-desktop",   "Show Desktop in This PC",   "Shows the Desktop folder under This PC",   "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", "Monitor"),
                ThisPcFolder("customize-explorer-this-pc-documents", "Show Documents in This PC", "Shows the Documents folder under This PC", "{D3162B92-9365-467A-956B-92703ACA08AF}", "FileDocument"),
                ThisPcFolder("customize-explorer-this-pc-downloads", "Show Downloads in This PC", "Shows the Downloads folder under This PC", "{088E3905-0323-4B02-9826-5D99428E115F}", "Download"),
                ThisPcFolder("customize-explorer-this-pc-music",     "Show Music in This PC",     "Shows the Music folder under This PC",     "{3DFDF296-DBEC-4FB4-81D1-6A3438BCF4DE}", "Music"),
                ThisPcFolder("customize-explorer-this-pc-pictures",  "Show Pictures in This PC",  "Shows the Pictures folder under This PC",  "{24AD3AD4-A569-4530-98E1-AB02F9417AA8}", "Image"),
                ThisPcFolder("customize-explorer-this-pc-videos",    "Show Videos in This PC",    "Shows the Videos folder under This PC",    "{F86FA3AB-70D2-4FC7-9C99-FCBF05467F3A}", "Video"),

                new SettingDefinition
                {
                    Id = "customize-explorer-3d-objects",
                    Icon = "Printer3d",
                    Name = "3D Objects Folder in This PC",
                    Description = "Shows the 3D Objects folder under This PC in File Explorer",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[] { new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}", ValueName = null, ValueType = RegistryValueKind.None, RecommendedValue = null, DefaultValue = null, EnabledValue = null, DisabledValue = null, IsPrimary = true } },
                    RegContents = new[]
                    {
                        new RegContentSetting
                        {
                            EnabledContent = "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}]\r\n",
                            DisabledContent = "Windows Registry Editor Version 5.00\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}]\r\n",
                        }
                    },
                },

                new SettingDefinition
                {
                    Id = "customize-explorer-duplicate-removable-drives",
                    Icon = "Usb",
                    Name = "Duplicate Removable Drives in Navigation Pane",
                    Description = "Shows removable drives in both This PC and the navigation pane sidebar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[] { new RegistrySetting { KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}", ValueName = null, ValueType = RegistryValueKind.None, RecommendedValue = null, DefaultValue = null, EnabledValue = null, DisabledValue = null, IsPrimary = true } },
                    RegContents = new[]
                    {
                        new RegContentSetting
                        {
                            EnabledContent = "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Desktop\\NameSpace\\DelegateFolders\\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}]\r\n@=\"Removable Drives\"\r\n",
                            DisabledContent = "Windows Registry Editor Version 5.00\r\n\r\n[-HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Desktop\\NameSpace\\DelegateFolders\\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}]\r\n",
                        }
                    },
                },
            },
        },
    ];

    // Factory: one This PC user-folder toggle. HiddenByDefault (0 = shown, 1 = hidden) written
    // to both the 64-bit and WOW6432Node MyComputer\NameSpace views. Ships visible → default on.
    private static SettingDefinition ThisPcFolder(string id, string name, string description, string guid, string icon) =>
        new SettingDefinition
        {
            Id = id,
            Icon = icon,
            Name = name,
            Description = description,
            InputType = InputType.Toggle,
            IsSubjectivePreference = false,
            DefaultToggleState = true,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = $@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{guid}",
                    ValueName = "HiddenByDefault",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = null,
                    DefaultValue = null,
                    EnabledValue = new object?[] { 0, null },
                    DisabledValue = new object?[] { 1 },
                },
                new RegistrySetting
                {
                    KeyPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{guid}",
                    ValueName = "HiddenByDefault",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = null,
                    DefaultValue = null,
                    EnabledValue = new object?[] { 0, null },
                    DisabledValue = new object?[] { 1 },
                },
            },
        };
}
