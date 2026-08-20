using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;

namespace AkariTool.Tabs.Customize;

public static class DesktopOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildIcons(),
        .. BuildShortcuts(),
        .. BuildStartup(),
        .. BuildDevices(),
        .. BuildLockScreen(),
        .. BuildRegional(),
    ];

    private static IReadOnlyList<SettingGroup> BuildIcons() =>
    [
        new SettingGroup
        {
            Name = "Desktop Icons",
            FeatureId = "customize-desktop-icons",
            Settings = new[]
            {
                // HideDesktopIcons\NewStartPanel {guid}: show (on) = 0 / hide (off) = 1.
                // DefaultState per icon (Win11 ships Recycle Bin only); taste → no Recommended.
                DesktopIcon("customize-desktop-icon-this-pc",       "Show This PC Icon",       "Shows the This PC (Computer) icon on the desktop",  "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", false),
                DesktopIcon("customize-desktop-icon-user-folder",   "Show User Folder Icon",   "Shows your personal user folder icon on the desktop", "{59031a47-3f72-44a7-89c5-5595fe6b30ee}", false),
                DesktopIcon("customize-desktop-icon-network",       "Show Network Icon",       "Shows the Network icon on the desktop",             "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", false),
                DesktopIcon("customize-desktop-icon-recycle-bin",   "Show Recycle Bin Icon",   "Shows the Recycle Bin icon on the desktop",         "{645FF040-5081-101B-9F08-00AA002F954E}", true),
                DesktopIcon("customize-desktop-icon-control-panel", "Show Control Panel Icon", "Shows the Control Panel icon on the desktop",       "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", false),
                DesktopIcon("customize-desktop-icon-libraries",     "Show Libraries Icon",     "Shows the Libraries folder icon on the desktop",    "{031E4825-7B94-4dc3-B131-E946B44C8DD5}", false),
            },
        },
    ];

    // Factory: one desktop-icon visibility toggle. HideDesktopIcons\NewStartPanel {guid}:
    // 0 = shown, 1 = hidden.
    private static SettingDefinition DesktopIcon(string id, string name, string description, string guid, bool defaultShown) =>
        new SettingDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            InputType = InputType.Toggle,
            IsSubjectivePreference = false,
            DefaultToggleState = defaultShown,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                    ValueName = guid,
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = null,
                    DefaultValue = null,
                    EnabledValue = new object?[] { 0 },
                    DisabledValue = new object?[] { 1 },
                },
            },
        };

    // Source: Desktop.Icons.cs — DesktopShortcuts (the Shortcuts rows live alongside the icons).
    private static IReadOnlyList<SettingGroup> BuildShortcuts() =>
    [
        new SettingGroup
        {
            Name = "Shortcuts",
            FeatureId = "customize-desktop-shortcuts",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "customize-desktop-remove-shortcut-arrow",
                    Name = "Remove Shortcut Arrow",
                    Description = "Removes the shortcut arrow overlay icon from desktop and file shortcuts",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RestartProcess = "explorer",
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons",
                            ValueName = "29",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { @"C:\Windows\blank.ico" },
                            DisabledValue = new object?[] { null },
                            IsPrimary = true,
                        }
                    },
                    PowerShellScripts = new[]
                    {
                        new PowerShellScriptSetting
                        {
                            RunContext = RunContext.User,
                            EnabledScript = "$icoPath = \"$env:WINDIR\\blank.ico\"\r\n[System.IO.File]::WriteAllBytes($icoPath, [byte[]](0x00,0x00,0x01,0x00,0x01,0x00,0x01,0x01,0x00,0x00,0x01,0x00,0x20,0x00,0x34,0x00,0x00,0x00,0x16,0x00,0x00,0x00,0x28,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x02,0x00,0x00,0x00,0x01,0x00,0x20,0x00,0x00,0x00,0x00,0x00,0x04,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00))\r\n$regPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Icons'\r\nif (-not (Test-Path $regPath)) { New-Item -Path $regPath -Force | Out-Null }\r\nSet-ItemProperty -Path $regPath -Name '29' -Value $icoPath -Type String",
                            DisabledScript = "$regPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Icons'\r\nRemove-ItemProperty -Path $regPath -Name '29' -ErrorAction SilentlyContinue\r\n$icoPath = \"$env:WINDIR\\blank.ico\"\r\nif (Test-Path $icoPath) { Remove-Item $icoPath -Force -ErrorAction SilentlyContinue }",
                        }
                    },
                },

                // customize-desktop-remove-shortcut-suffix — Explorer "link" (REG_BINARY),
                //   full 4-byte value replace: removed (on) = 00 00 00 00 / present (off) = 1E 00 00 00.
                new SettingDefinition
                {
                    Id = "customize-desktop-remove-shortcut-suffix",
                    Name = "Remove '- Shortcut' Suffix",
                    Description = "Stops Windows appending '- Shortcut' to newly created shortcuts",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "link",
                            ValueType = RegistryValueKind.Binary,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { new byte[] { 0x00, 0x00, 0x00, 0x00 } },
                            DisabledValue = new object?[] { new byte[] { 0x1E, 0x00, 0x00, 0x00 } },
                        },
                    },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildStartup() =>
    [
        new SettingGroup
        {
            Name = "Startup",
            FeatureId = "customize-desktop-startup",
            Settings = new[]
            {
                // customize-desktop-show-auto-login-option — DevicePasswordLessBuildVersion:
                //   shown (on) = 0 / hidden (off) = 2.
                new SettingDefinition
                {
                    Id = "customize-desktop-show-auto-login-option",
                    Name = "Show Auto-Login Option",
                    Description = "Re-enables the 'Users must enter a user name and password' checkbox in netplwiz",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device",
                            ValueName = "DevicePasswordLessBuildVersion",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 2 },
                        },
                    },
                },
                // customize-desktop-numlock-at-startup — InitialKeyboardIndicators (REG_SZ):
                //   on = "2" / off = "0".
                new SettingDefinition
                {
                    Id = "customize-desktop-numlock-at-startup",
                    Name = "NumLock On at Startup",
                    Description = "Enables NumLock automatically when Windows starts",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Keyboard",
                            ValueName = "InitialKeyboardIndicators",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "2" },
                            DisabledValue = new object?[] { "0" },
                        },
                    },
                },
            },
        },
    ];

    // Source: Desktop.System.cs — Devices section (RGB lighting rows; no service-start rows exist).
    private static IReadOnlyList<SettingGroup> BuildDevices() =>
    [
        new SettingGroup
        {
            Name = "Devices",
            FeatureId = "customize-desktop-devices",
            Settings = new[]
            {
                // customize-desktop-dynamic-lighting — AmbientLightingEnabled: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-desktop-dynamic-lighting",
                    Name = "Dynamic Lighting",
                    Description = "Let Windows control RGB lighting on compatible devices (keyboards, mice, strips)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Lighting",
                            ValueName = "AmbientLightingEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-desktop-foreground-lighting-control — ControlledByForegroundApp: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-desktop-foreground-lighting-control",
                    Name = "Foreground Apps Control Lighting",
                    Description = "Let the app in the foreground take over RGB lighting from Windows",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Lighting",
                            ValueName = "ControlledByForegroundApp",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildLockScreen() =>
    [
        new SettingGroup
        {
            Name = "Lock Screen",
            FeatureId = "customize-desktop-lockscreen",
            Settings = new[]
            {
                // customize-desktop-disable-spotlight — DisableWindowsSpotlightFeatures: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-desktop-disable-spotlight",
                    Name = "Disable Windows Spotlight",
                    Description = "Stops Windows from changing the lock screen image via Spotlight (online)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableWindowsSpotlightFeatures",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-desktop-disable-lock-screen — NoLockScreen: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-desktop-disable-lock-screen",
                    Name = "Disable Lock Screen",
                    Description = "Skips the lock screen and goes straight to the sign-in screen",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Personalization",
                            ValueName = "NoLockScreen",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-desktop-disable-lock-screen-tips — two inverted writes:
                //   RotatingLockScreenEnabled + RotatingLockScreenOverlayEnabled, disable (on) = 0 / enable (off) = 1.
                new SettingDefinition
                {
                    Id = "customize-desktop-disable-lock-screen-tips",
                    Name = "Disable Lock Screen Tips & Tricks",
                    Description = "Removes fun facts, tips, and Spotlight info on the lock screen",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "RotatingLockScreenEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "RotatingLockScreenOverlayEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildRegional() =>
    [
        new SettingGroup
        {
            Name = "Regional Settings",
            FeatureId = "customize-desktop-regional",
            Settings = new[]
            {
                // Six HKCU\Control Panel\International REG_SZ preference dropdowns (MakeRegionalDropdown).
                // Pure preference: no IsDefault/IsRecommended; a value matching no option reads Custom.
                RegionalDropdown("region.firstDayOfWeek", "First Day of Week",
                    "Sets the day calendars and date pickers start on. Applies to new processes — sign out and back in to apply everywhere",
                    "iFirstDayOfWeek",
                    new[] { ("Monday", "0"), ("Tuesday", "1"), ("Wednesday", "2"), ("Thursday", "3"), ("Friday", "4"), ("Saturday", "5"), ("Sunday", "6") }),
                RegionalDropdown("region.measurement", "Measurement System",
                    "Sets the unit system Windows and apps use. Applies to new processes — sign out and back in to apply everywhere",
                    "iMeasure",
                    new[] { ("Metric", "0"), ("U.S. (Imperial)", "1") }),
                RegionalDropdown("region.shortDate", "Short Date Format",
                    "Sets the short date pattern used across Windows. Applies to new processes — sign out and back in to apply everywhere",
                    "sShortDate",
                    new[] { ("M/d/yyyy", "M/d/yyyy"), ("dd/MM/yyyy", "dd/MM/yyyy"), ("yyyy-MM-dd", "yyyy-MM-dd"), ("yyyy/MM/dd", "yyyy/MM/dd"), ("dd MMM yyyy", "dd MMM yyyy") }),
                RegionalDropdown("region.decimal", "Decimal Symbol",
                    "Sets the character used as the decimal separator. Applies to new processes — sign out and back in to apply everywhere",
                    "sDecimal",
                    new[] { (". (Period)", "."), (", (Comma)", ",") }),
                RegionalDropdown("region.currencyDecimal", "Currency Decimal Symbol",
                    "Sets the character used as the decimal separator in currency amounts. Applies to new processes — sign out and back in to apply everywhere",
                    "sMonDecimalSep",
                    new[] { (". (Period)", "."), (", (Comma)", ",") }),
                RegionalDropdown("region.listSeparator", "List Separator",
                    "Sets the character that separates list items, e.g. in CSV exports. Applies to new processes — sign out and back in to apply everywhere",
                    "sList",
                    new[] { (", (Comma)", ","), ("; (Semicolon)", ";") }),

                // os-set-utc (relocated) — RealTimeIsUniversal: enable = 1 / disable = delete (absent).
                //   Reader: RealTimeIsUniversal == 1 → enabled, else false. Source sets no Recommended/Default.
                new SettingDefinition
                {
                    Id = "os-set-utc",
                    Name = "Set Clock to UTC",
                    Description = "Stores the hardware clock as UTC — fixes time sync conflict when dual-booting with Linux",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\TimeZoneInformation",
                            ValueName = "RealTimeIsUniversal",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { null },
                        },
                    },
                },
            },
        },
    ];

    // Factory: one HKCU\Control Panel\International REG_SZ preference dropdown. Pure preference —
    // no IsDefault/IsRecommended; a value matching no listed option reads back as Custom.
    private static SettingDefinition RegionalDropdown(string id, string name, string description, string valueName, (string Label, string Data)[] options) =>
        new SettingDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            InputType = InputType.Selection,
            IsSubjectivePreference = true,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Control Panel\International",
                    ValueName = valueName,
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
                Options = options
                    .Select(o => new ComboBoxOption { DisplayName = o.Label, ValueMappings = new Dictionary<string, object?> { [valueName] = o.Data } })
                    .ToArray(),
            },
        };
}
