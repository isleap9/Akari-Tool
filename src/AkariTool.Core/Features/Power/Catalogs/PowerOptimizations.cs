using System.Collections.Generic;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Native;
using Microsoft.Win32;

namespace AkariTool.Tabs.Power;

// Declarative Power catalog — 1:1 port of Winhance PowerOptimizations.cs (Winhance IDs, GUIDs,
// recommendations, ParentSettingId/Dependencies/RequiresBattery/RequiresHybridSleepCapable/
// ValidateExistence/RequiresAdvancedUnlock). Split into SettingGroups by GroupName with the
// power-plan-selection dropdown first. Icons are dropped (Akari catalog convention).
public static class PowerOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildPowerPlan(),
        .. BuildDisplay(),
        .. BuildHardDisk(),
        .. BuildInternetExplorer(),
        .. BuildDesktopBackground(),
        .. BuildWireless(),
        .. BuildSleep(),
        .. BuildUsb(),
        .. BuildIntelGraphics(),
        .. BuildPowerButtonsAndLid(),
        .. BuildPciExpress(),
        .. BuildProcessor(),
        .. BuildMultimedia(),
        .. BuildBattery(),
        .. BuildAmdPowerSlider(),
        .. BuildAtiPowerPlay(),
        .. BuildSwitchableGraphics(),
        .. BuildStartMenu(),
    ];

    private static IReadOnlyList<SettingGroup> BuildPowerPlan() =>
    [
        new SettingGroup
        {
            Name = "Power",
            FeatureId = "power",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "power-plan-selection",
                    Icon = "NotebookLightning",
                    IconPack = "Fluent",
                    IsSubjectivePreference = true,
                    Name = "Power Plan",
                    Description = "Select the active power plan for your system",
                    InputType = InputType.Selection,
                    Recommendation = new PowerRecommendation { LoadDynamicOptions = true }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildDisplay() =>
    [
        new SettingGroup
        {
            Name = "Display",
            FeatureId = "power-display",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "power-display-timeout",
                    Icon = "MonitorOff",
                    IsSubjectivePreference = true,
                    Name = "Turn off the display",
                    Description = "Specifies the period of inactivity before Windows turns off the display",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_VIDEO",
                            SettingGUIDAlias = "VIDEOIDLE",
                            SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
                            SettingGuid = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 300,
                            DefaultValueAC = 300,
                            DefaultValueDC = 180
                        }
                    ],
                    ComboBox = PowerTemplates.TimeIntervals,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Never", RecommendedOptionDC = "5 minutes" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildHardDisk() =>
    [
        new SettingGroup
        {
            Name = "Hard Disk",
            FeatureId = "power-hard-disk",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "power-harddisk-timeout",
                    Icon = "Harddisk",
                    IsSubjectivePreference = true,
                    Name = "Turn off hard disk after",
                    Description = "Specifies the period of inactivity before Windows turns off the hard disk",
                    InputType = InputType.NumericRange,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_DISK",
                            SettingGUIDAlias = "DISKIDLE",
                            SubgroupGuid = "0012ee47-9041-4b5d-9b77-535fba8b1442",
                            SettingGuid = "6738e2c4-e8a5-4a42-b16a-e040e769756e",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "Seconds",
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 600,
                            DefaultValueAC = 1200,
                            DefaultValueDC = 600
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, int.MaxValue, "Minutes")
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildInternetExplorer() =>
    [
        new SettingGroup
        {
            Name = "Internet Explorer",
            FeatureId = "power-internet-explorer",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "internet-explorer-javascript-timer",
                    Icon = "CodeBraces",
                    Name = "JavaScript Timer Frequency",
                    Description = "Specifies the frequency of JavaScript timers",
                    ValidateExistence = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "02f815b5-a5cf-4c84-bf20-649d1f75d3d8",
                            SettingGuid = "4c793e7d-a264-42e1-87d3-7a0d2f523ccd",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 0,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.JavaScriptTimers,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Maximum Performance", RecommendedOptionDC = "Maximum Performance" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildDesktopBackground() =>
    [
        new SettingGroup
        {
            Name = "Desktop Background Settings",
            FeatureId = "power-desktop-background",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "desktop-slideshow",
                    Icon = "Image",
                    IsSubjectivePreference = true,
                    Name = "Desktop Background Slide Show",
                    Description = "Allow or prevent Windows from rotating through multiple wallpaper images",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "0d7dbae2-4294-402a-ba8e-26777e8488cd",
                            SettingGuid = "309dce9b-bef4-4119-9921-a851fb12f0f4",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 1,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 0,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.Slideshow,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Available", RecommendedOptionDC = "Paused" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildWireless() =>
    [
        new SettingGroup
        {
            Name = "Wireless Adapter Settings",
            FeatureId = "power-wireless",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "wireless-power-mode",
                    Icon = "Wifi",
                    IsSubjectivePreference = true,
                    Name = "Power Saving Mode",
                    Description = "Balance wireless network performance with battery life by adjusting adapter power usage",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "19cbb8fa-5279-450e-9fac-8a3d5fedd0c1",
                            SettingGuid = "12bbebe6-58d6-4636-95bb-3217ef867c1a",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 2,
                            DefaultValueAC = 0,
                            DefaultValueDC = 2
                        }
                    ],
                    ComboBox = PowerTemplates.WirelessPower,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Maximum Performance", RecommendedOptionDC = "Medium Power Saving" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildSleep() =>
    [
        new SettingGroup
        {
            Name = "Sleep",
            FeatureId = "power-sleep",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "power-sleep-timeout",
                    Icon = "Sleep",
                    IsSubjectivePreference = true,
                    Name = "Put the computer to sleep",
                    Description = "Specifies the period of inactivity before Windows puts the computer to sleep",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_SLEEP",
                            SettingGUIDAlias = "STANBYIDLE",
                            SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                            SettingGuid = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 900,
                            DefaultValueAC = 900,
                            DefaultValueDC = 600
                        }
                    ],
                    ComboBox = PowerTemplates.TimeIntervals,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Never", RecommendedOptionDC = "15 minutes" }
                },
                new SettingDefinition
                {
                    Id = "power-wake-timers",
                    Icon = "Alarm",
                    Name = "Allow wake timers",
                    Description = "Allow scheduled tasks and applications to wake your computer from sleep",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_SLEEP",
                            SettingGUIDAlias = "RTCWAKE",
                            SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                            SettingGuid = "bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.WakeTimers,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Disable", RecommendedOptionDC = "Disable" }
                },
                new SettingDefinition
                {
                    Id = "power-hibernation-enable",
                    Icon = "PowerSleep",
                    IsSubjectivePreference = true,
                    Name = "Hibernation",
                    Description = "Save your session to disk and power down completely, using no battery while preserving your work",
                    InputType = InputType.Toggle,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power",
                            ValueName = "HibernateEnabled",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                    NativePowerApiSettings =
                    [
                        new NativePowerApiSetting
                        {
                            InformationLevel = PowerProf.SystemReserveHiberFile,
                            EnabledValue = 1,
                            DisabledValue = 0,
                        },
                    ],
                    AutoEnableSettingIds = new[] { "start-power-hibernate-option" },
                },
                new SettingDefinition
                {
                    Id = "power-hibernate-timeout",
                    Icon = "BedClock",
                    IsSubjectivePreference = true,
                    Name = "Hibernate after",
                    Description = "Specifies the period of inactivity before Windows hibernates the computer",
                    ParentSettingId = "power-hibernation-enable",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_SLEEP",
                            SettingGUIDAlias = "HIBERNATEIDLE",
                            SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                            SettingGuid = "9d7815a6-7ee4-497e-8888-515a05f02364",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 0,
                            DefaultValueDC = 10800
                        }
                    ],
                    ComboBox = PowerTemplates.TimeIntervals
                },
                new SettingDefinition
                {
                    Id = "power-hybrid-sleep",
                    Icon = "WeatherNight",
                    IsSubjectivePreference = true,
                    Name = "Allow hybrid sleep",
                    Description = "Combines sleep and hibernate by saving your session to disk while staying in low-power mode for faster wake",
                    ParentSettingId = "power-hibernation-enable",
                    RequiresHybridSleepCapable = true,
                    InputType = InputType.Selection,
                    Dependencies =
                    [
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "power-hybrid-sleep",
                            RequiredSettingId = "power-hibernation-enable",
                        },
                    ],
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_SLEEP",
                            SettingGUIDAlias = "HYBRIDSLEEP",
                            SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                            SettingGuid = "94ac6d29-73ce-41a6-809f-6363ba21b47e",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.OnOff
                },
                new SettingDefinition
                {
                    Id = "power-fast-startup",
                    Icon = "FlashAuto",
                    Name = "Fast Startup",
                    Description = "Hibernate system state during shutdown for faster boot times (does not affect restart)",
                    ParentSettingId = "power-hibernation-enable",
                    InputType = InputType.Toggle,
                    Dependencies =
                    [
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "power-fast-startup",
                            RequiredSettingId = "power-hibernation-enable",
                        },
                    ],
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Session Manager\Power",
                            ValueName = "HiberbootEnabled",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "start-power-hibernate-option",
                    Icon = "FlashRedEye",
                    IsSubjectivePreference = true,
                    Name = "Show Hibernate Option",
                    Description = "Display the Hibernate option in the Start Menu power button menu",
                    ParentSettingId = "power-hibernation-enable",
                    InputType = InputType.Toggle,
                    Dependencies =
                    [
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "start-power-hibernate-option",
                            RequiredSettingId = "power-hibernation-enable",
                        },
                    ],
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                            ValueName = "ShowHibernateOption",
                            RecommendedValue = 0,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildUsb() =>
    [
        new SettingGroup
        {
            Name = "USB settings",
            FeatureId = "power-usb",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "usb-hub-selective-suspend-timeout",
                    Icon = "TimerPause",
                    Name = "USB Hub Selective Suspend Timeout",
                    Description = "Set how long USB hubs wait idle before powering down to save energy",
                    InputType = InputType.NumericRange,
                    ValidateExistence = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "2a737441-1930-4402-8d77-b2bebba308a3",
                            SettingGuid = "0853a681-27c8-4100-a2fd-82013e970683",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "Milliseconds",
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 1000,
                            DefaultValueAC = 50,
                            DefaultValueDC = 50,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\0853a681-27c8-4100-a2fd-82013e970683",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100000, "Milliseconds")
                },
                new SettingDefinition
                {
                    Id = "usb-selective-suspend",
                    Icon = "Usb",
                    Name = "USB selective suspend setting",
                    Description = "Allow Windows to power down individual USB ports when devices are idle to save energy",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "2a737441-1930-4402-8d77-b2bebba308a3",
                            SettingGuid = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.EnabledDisabled,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Disabled", RecommendedOptionDC = "Enabled" }
                },
                new SettingDefinition
                {
                    Id = "usb3-link-power-management",
                    Icon = "UsbPort",
                    Name = "USB 3 Link Power Management",
                    Description = "Control how aggressively USB 3.0 ports enter low-power states when devices are idle",
                    InputType = InputType.Selection,
                    ValidateExistence = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "2a737441-1930-4402-8d77-b2bebba308a3",
                            SettingGuid = "d4e98f31-5ffe-4ce1-be31-1b38b384c009",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 2,
                            DefaultValueAC = 2,
                            DefaultValueDC = 2,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\d4e98f31-5ffe-4ce1-be31-1b38b384c009",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    ComboBox = PowerTemplates.Usb3LinkPower,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Off", RecommendedOptionDC = "Moderate power savings" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildIntelGraphics() =>
    [
        new SettingGroup
        {
            Name = "Intel(R) Graphics Settings",
            FeatureId = "power-intel-graphics",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "intel-graphics-power-plan",
                    Icon = "ExpansionCard",
                    Name = "Intel(R) Graphics Power Plan",
                    Description = "Balance Intel integrated graphics performance with power consumption and battery life",
                    ValidateExistence = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "44f3beca-a7c0-460e-9df2-bb8b99e0cba6",
                            SettingGuid = "3619c3f2-afb2-4afc-b0e9-e7fef372de36",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 2,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.IntelGraphics,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Maximum Performance", RecommendedOptionDC = "Balanced" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildPowerButtonsAndLid() =>
    [
        new SettingGroup
        {
            Name = "Power Buttons and Lid",
            FeatureId = "power-buttons-lid",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "power-button-action",
                    Icon = "PowerSettings",
                    IsSubjectivePreference = true,
                    Name = "Power button action",
                    Description = "Choose what happens when you press the physical power button on your computer",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BUTTONS",
                            SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                            SettingGuid = "7648efa3-dd9c-4e3e-b566-50f929386280",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 3,
                            DefaultValueDC = 3,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\7648efa3-dd9c-4e3e-b566-50f929386280",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    ComboBox = PowerTemplates.PowerButtonActions,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Do nothing", RecommendedOptionDC = "Do nothing" }
                },
                new SettingDefinition
                {
                    Id = "sleep-button-action",
                    Icon = "Sleep",
                    IsSubjectivePreference = true,
                    Name = "Sleep button action",
                    Description = "Choose what happens when you press the dedicated sleep button on your keyboard or computer",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BUTTONS",
                            SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                            SettingGuid = "96996bc0-ad50-47ec-923b-6f41874dd9eb",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\96996bc0-ad50-47ec-923b-6f41874dd9eb",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    ComboBox = PowerTemplates.PowerButtonActions,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Do nothing", RecommendedOptionDC = "Do nothing" }
                },
                new SettingDefinition
                {
                    Id = "lid-close-action",
                    Icon = "Laptop",
                    IsSubjectivePreference = true,
                    Name = "Lid close action",
                    Description = "Choose what happens when you close your laptop lid",
                    RequiresBattery = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BUTTONS",
                            SettingGUIDAlias = "LIDACTION",
                            SubgroupGuid = "4f971e89-eebd-4455-a8de-9e59040e7347",
                            SettingGuid = "5ca83367-6e45-459f-a27b-476b1d01c936",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 1,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\4f971e89-eebd-4455-a8de-9e59040e7347\5ca83367-6e45-459f-a27b-476b1d01c936",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    ComboBox = PowerTemplates.LidActions,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Sleep", RecommendedOptionDC = "Sleep" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildPciExpress() =>
    [
        new SettingGroup
        {
            Name = "PCI Express",
            FeatureId = "power-pci-express",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "pci-link-state-power-management",
                    Icon = "Router",
                    Name = "Link State Power Management",
                    Description = "Control power savings for PCIe devices like graphics cards, SSDs, and expansion cards",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PCIEXPRESS",
                            SettingGUIDAlias = "ASPM",
                            SubgroupGuid = "501a4d13-42af-4429-9fd1-a8218c268e20",
                            SettingGuid = "ee12f906-d277-404b-b6da-e5fa1a576df5",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 2,
                            DefaultValueAC = 1,
                            DefaultValueDC = 2
                        }
                    ],
                    ComboBox = PowerTemplates.PciExpress,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Off", RecommendedOptionDC = "Maximum power savings" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildProcessor() =>
    [
        new SettingGroup
        {
            Name = "Processor Power Management",
            FeatureId = "power-processor",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "processor-min-state",
                    Icon = "SpeedometerSlow",
                    Name = "Minimum processor state",
                    Description = "Set the lowest CPU speed allowed as a percentage of maximum frequency",
                    InputType = InputType.NumericRange,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "PROCTHROTTLEMIN",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "893dee8e-2bef-41e0-89c6-b55d0929964c",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 100,
                            RecommendedValueDC = 5,
                            DefaultValueAC = 0,
                            DefaultValueDC = 5
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "processor-max-state",
                    Icon = "Speedometer",
                    Name = "Maximum processor state",
                    Description = "Set the highest CPU speed allowed as a percentage of maximum frequency",
                    InputType = InputType.NumericRange,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "PROCTHROTTLEMAX",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "bc5038f7-23e0-4960-96da-33abaf5935ec",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 100,
                            RecommendedValueDC = 100,
                            DefaultValueAC = 100,
                            DefaultValueDC = 100
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "system-cooling-policy",
                    Icon = "Fan",
                    Name = "System cooling policy",
                    Description = "Choose whether to slow down the processor first (passive) or speed up fans first (active) when hot",
                    InputType = InputType.Selection,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "94d3a615-a899-4ac5-ae2b-e4d8f634367f",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 1,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 0,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\94d3a615-a899-4ac5-ae2b-e4d8f634367f",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    ComboBox = PowerTemplates.CoolingPolicy,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Active", RecommendedOptionDC = "Active" }
                },
                new SettingDefinition
                {
                    Id = "processor-performance-boost-mode",
                    Icon = "RocketLaunch",
                    Name = "Processor performance boost mode",
                    Description = "Control how aggressively your CPU boosts above base frequency for demanding tasks",
                    InputType = InputType.Selection,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "PERFBOOSTMODE",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "be337238-0d82-4146-a960-4f3749d470c7",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 2,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 2,
                            DefaultValueDC = 2,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\be337238-0d82-4146-a960-4f3749d470c7",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    ComboBox = PowerTemplates.ProcessorBoostMode,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Aggressive", RecommendedOptionDC = "Enabled" }
                },
                new SettingDefinition
                {
                    Id = "processor-performance-increase-policy",
                    Icon = "TrendingUp",
                    Name = "Processor Performance Increase Policy",
                    Description = "Control how quickly CPU ramps up speed when workload increases (for legacy non-HWP processors)",
                    InputType = InputType.Selection,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "PERFINCPOL",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "465e1f50-b610-473a-ab58-00d1077dc418",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 2,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 2,
                            DefaultValueDC = 0,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\465e1f50-b610-473a-ab58-00d1077dc418",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    ComboBox = PowerTemplates.PerformanceIncreasePolicy,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Rocket", RecommendedOptionDC = "Ideal" }
                },
                new SettingDefinition
                {
                    Id = "processor-performance-decrease-policy",
                    Icon = "TrendingDown",
                    Name = "Processor Performance Decrease Policy",
                    Description = "Control how quickly CPU reduces speed when workload decreases (for legacy non-HWP processors)",
                    InputType = InputType.Selection,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "PERFDECPOL",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "40fbefc7-2e9d-4d25-a185-0cfd8574bac6",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 1,
                            RecommendedValueDC = 2,
                            DefaultValueAC = 1,
                            DefaultValueDC = 0,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\40fbefc7-2e9d-4d25-a185-0cfd8574bac6",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    ComboBox = PowerTemplates.PerformanceDecreasePolicy,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Single", RecommendedOptionDC = "Rocket" }
                },
                new SettingDefinition
                {
                    Id = "processor-core-parking-min-cores",
                    Icon = "Cpu64Bit",
                    Name = "CPU Core Parking Minimum Cores",
                    Description = "Set the minimum percentage of CPU cores that must remain active and responsive",
                    InputType = InputType.NumericRange,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "CPMINCORES",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "0cc5b647-c1df-4637-891a-dec35c318583",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 100,
                            DefaultValueDC = 10,
                            CheckForHardwareControl = true,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "processor-core-parking-max-cores",
                    Icon = "Cpu64Bit",
                    Name = "CPU Core Parking Maximum Cores",
                    Description = "Set the maximum percentage of CPU cores allowed to be active (100% for best performance)",
                    InputType = InputType.NumericRange,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "CPMAXCORES",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "ea062031-0e34-4ff1-9b6d-eb1059334028",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 100,
                            RecommendedValueDC = 100,
                            DefaultValueAC = 100,
                            DefaultValueDC = 100,
                            CheckForHardwareControl = true,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\ea062031-0e34-4ff1-9b6d-eb1059334028",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "processor-energy-performance-preference",
                    Icon = "Tune",
                    Name = "Processor Energy Performance Preference",
                    Description = "Balance power efficiency and performance for modern CPUs with HWP (0 = max performance, 100 = max efficiency)",
                    InputType = InputType.NumericRange,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "PERFEPP",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "36687f9e-e3a5-4dbf-b1dc-15eb381c6863",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 50,
                            DefaultValueAC = 25,
                            DefaultValueDC = 50,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\36687f9e-e3a5-4dbf-b1dc-15eb381c6863",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "processor-performance-increase-threshold",
                    Icon = "TrendingUp",
                    Name = "Processor Performance Increase Threshold",
                    Description = "Set CPU usage percentage that triggers speed increase (lower = more responsive, for legacy non-HWP CPUs)",
                    InputType = InputType.NumericRange,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "PERFINCTHRESHOLD",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "06cadf0e-64ed-448a-8927-ce7bf90eb35d",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 10,
                            RecommendedValueDC = 30,
                            DefaultValueAC = 30,
                            DefaultValueDC = 90,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\06cadf0e-64ed-448a-8927-ce7bf90eb35d",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "processor-performance-decrease-threshold",
                    Icon = "TrendingDown",
                    Name = "Processor Performance Decrease Threshold",
                    Description = "Set CPU usage percentage that triggers speed reduction (lower = maintains performance longer, for legacy non-HWP CPUs)",
                    InputType = InputType.NumericRange,
                    ValidateExistence = true,
                    RequiresAdvancedUnlock = true,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_PROCESSOR",
                            SettingGUIDAlias = "PERFDECTHRESHOLD",
                            SubgroupGuid = "54533251-82be-4824-96c1-47b60b740d00",
                            SettingGuid = "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 8,
                            RecommendedValueDC = 20,
                            DefaultValueAC = 10,
                            DefaultValueDC = 30,
                            EnablementRegistrySetting = new RegistrySetting
                            {
                                KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\12a0ab44-fe28-4fa9-b3bd-4b64f44960a6",
                                ValueName = "Attributes",
                                EnabledValue = new object?[] { 0 },
                                DisabledValue = new object?[] { 1 },
                                RecommendedValue = 0,
                                DefaultValue = 1,
                                ValueType = RegistryValueKind.DWord
                            }
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "power-throttling",
                    Icon = "SelectOff",
                    Name = "Power Throttling",
                    Description = "Allow Windows to reduce CPU performance for background processes to save power",
                    InputType = InputType.Toggle,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                            ValueName = "PowerThrottlingOff",
                            RecommendedValue = 1,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildMultimedia() =>
    [
        new SettingGroup
        {
            Name = "Multimedia Settings",
            FeatureId = "power-multimedia",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "multimedia-when-sharing-media",
                    Icon = "Share",
                    Name = "When Sharing Media",
                    Description = "Control whether your PC can sleep while streaming media to other devices on your network",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                            SettingGuid = "03680956-93bc-4294-bba6-4e0f09bb717f",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 1,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 0
                        }
                    ],
                    ComboBox = PowerTemplates.MediaSharing,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Prevent idling to sleep", RecommendedOptionDC = "Prevent idling to sleep" }
                },
                new SettingDefinition
                {
                    Id = "multimedia-video-playback-quality-bias",
                    Icon = "HighDefinition",
                    Name = "Video Playback Quality Bias",
                    Description = "Prioritize smooth video playback over battery life when watching videos",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                            SettingGuid = "10778347-1370-4ee0-8bbd-33bdacaade49",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 1,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 0
                        }
                    ],
                    ComboBox = PowerTemplates.VideoQualityBias,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Video playback performance bias", RecommendedOptionDC = "Video playback performance bias" }
                },
                new SettingDefinition
                {
                    Id = "multimedia-when-playing-video",
                    Icon = "Play",
                    Name = "When Playing Video",
                    Description = "Balance video quality and power consumption during video playback",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "9596fb26-9850-41fd-ac3e-f7c3c00afd4b",
                            SettingGuid = "34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 0,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.VideoPlayback,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Optimize video quality", RecommendedOptionDC = "Optimize video quality" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildBattery() =>
    [
        new SettingGroup
        {
            Name = "Battery",
            FeatureId = "power-battery",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "critical-battery-notification",
                    Icon = "AlertCircle",
                    Name = "Critical battery notification",
                    Description = "Show notification when battery reaches critically low level",
                    RequiresBattery = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BATTERY",
                            SettingGUIDAlias = "BATFLAGSCRIT",
                            SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                            SettingGuid = "5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 1,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.OnOff,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "On", RecommendedOptionDC = "On" }
                },
                new SettingDefinition
                {
                    Id = "critical-battery-action",
                    Icon = "BatteryAlert",
                    Name = "Critical battery action",
                    Description = "Choose what happens when battery reaches critically low level",
                    RequiresBattery = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BATTERY",
                            SettingGUIDAlias = "BATACTIONCRIT",
                            SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                            SettingGuid = "637ea02f-bbcb-4015-8e2c-a1c7b9c0b546",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 2,
                            RecommendedValueDC = 2,
                            DefaultValueAC = 2,
                            DefaultValueDC = 2
                        }
                    ],
                    ComboBox = PowerTemplates.BatteryActions,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Hibernate", RecommendedOptionDC = "Hibernate" }
                },
                new SettingDefinition
                {
                    Id = "low-battery-level",
                    Icon = "Battery20",
                    IsSubjectivePreference = true,
                    Name = "Low battery level",
                    Description = "Set the battery percentage that triggers low battery warnings and actions",
                    RequiresBattery = true,
                    InputType = InputType.NumericRange,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BATTERY",
                            SettingGUIDAlias = "BATLEVELOW",
                            SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                            SettingGuid = "8183ba9a-e910-48da-8769-14ae6dc1170a",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 10,
                            RecommendedValueDC = 10,
                            DefaultValueAC = 10,
                            DefaultValueDC = 10
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "critical-battery-level",
                    Icon = "BatteryOutline",
                    Name = "Critical battery level",
                    Description = "Set the battery percentage that triggers critical battery warnings and emergency actions",
                    RequiresBattery = true,
                    InputType = InputType.NumericRange,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BATTERY",
                            SettingGUIDAlias = "BATLEVELCRIT",
                            SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                            SettingGuid = "9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 5,
                            RecommendedValueDC = 5,
                            DefaultValueAC = 5,
                            DefaultValueDC = 5
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
                new SettingDefinition
                {
                    Id = "low-battery-notification",
                    Icon = "Bell",
                    Name = "Low battery notification",
                    Description = "Show notification when battery reaches low battery level",
                    RequiresBattery = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BATTERY",
                            SettingGUIDAlias = "BATFLAGSLOW",
                            SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                            SettingGuid = "bcded951-187b-4d05-bccc-f7e51960c258",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 1,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.OnOff,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "On", RecommendedOptionDC = "On" }
                },
                new SettingDefinition
                {
                    Id = "low-battery-action",
                    Icon = "Battery20",
                    IsSubjectivePreference = true,
                    Name = "Low battery action",
                    Description = "Choose what happens when battery reaches low battery level",
                    RequiresBattery = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BATTERY",
                            SettingGUIDAlias = "BATACTIONLOW",
                            SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                            SettingGuid = "d8742dcb-3e6a-4b3c-b3fe-374623cdcf06",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 0,
                            RecommendedValueDC = 0,
                            DefaultValueAC = 0,
                            DefaultValueDC = 0
                        }
                    ],
                    ComboBox = PowerTemplates.BatteryActions,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Do nothing", RecommendedOptionDC = "Do nothing" }
                },
                new SettingDefinition
                {
                    Id = "reserve-battery-level",
                    Icon = "BatteryCharging",
                    Name = "Reserve battery level",
                    Description = "Set battery percentage reserved to protect battery health and prevent unexpected shutdowns",
                    RequiresBattery = true,
                    InputType = InputType.NumericRange,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGUIDAlias = "SUB_BATTERY",
                            SubgroupGuid = "e73a048d-bf27-4f12-9731-8b2076e8891f",
                            SettingGuid = "f3c5027d-cd16-4930-aa6b-90db844a8f00",
                            PowerModeSupport = PowerModeSupport.Separate,
                            Units = "%",
                            RecommendedValueAC = 7,
                            RecommendedValueDC = 7,
                            DefaultValueAC = 7,
                            DefaultValueDC = 7
                        }
                    ],
                    NumericRange = PowerTemplates.CreateNumericRange(0, 100, "%")
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildAmdPowerSlider() =>
    [
        new SettingGroup
        {
            Name = "AMD Power Slider",
            FeatureId = "power-amd-power-slider",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "amd-power-slider-overlay",
                    Icon = "ExpansionCard",
                    Name = "Overlay",
                    Description = "Balance AMD laptop performance and battery life with quick power mode selection",
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "c763b4ec-0e50-4b6b-9bed-2b92a6ee884e",
                            SettingGuid = "7ec1751b-60ed-4588-afb5-9819d3d77d90",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 3,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 2,
                            DefaultValueDC = 2
                        }
                    ],
                    ComboBox = PowerTemplates.AmdPowerSlider,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Best Performance", RecommendedOptionDC = "Better Battery" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildAtiPowerPlay() =>
    [
        new SettingGroup
        {
            Name = "ATI PowerPlay",
            FeatureId = "power-ati-powerplay",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "ati-powerplay-setting",
                    Icon = "ExpansionCard",
                    Name = "ATI PowerPlay Setting",
                    Description = "Control power management for older AMD Radeon graphics cards",
                    ValidateExistence = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "f693fb01-e858-4f00-b20f-f30e12ac06d6",
                            SettingGuid = "191f65b5-d45c-4a4f-8aae-1ab8bfd980e6",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 2,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.AtiPowerPlay,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Maximum Performance", RecommendedOptionDC = "Balanced" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildSwitchableGraphics() =>
    [
        new SettingGroup
        {
            Name = "Switchable Graphics",
            FeatureId = "power-switchable-graphics",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "switchable-graphics-gpu-preference",
                    Icon = "SwapHorizontal",
                    Name = "GPU Preference",
                    Description = "Choose between integrated GPU for battery life or dedicated GPU for performance in hybrid graphics laptops",
                    ValidateExistence = true,
                    InputType = InputType.Selection,
                    PowerCfgSettings =
                    [
                        new PowerCfgSetting
                        {
                            SubgroupGuid = "e276e160-7cb0-43c6-b20b-73f5dce39954",
                            SettingGuid = "a1662ab2-9d34-4e53-ba8b-2639b9e20857",
                            PowerModeSupport = PowerModeSupport.Separate,
                            RecommendedValueAC = 2,
                            RecommendedValueDC = 1,
                            DefaultValueAC = 1,
                            DefaultValueDC = 1
                        }
                    ],
                    ComboBox = PowerTemplates.SwitchableGraphics,
                    Recommendation = new PowerRecommendation { RecommendedOptionAC = "Maximize Performance", RecommendedOptionDC = "Optimize Power Savings" }
                },
            ],
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildStartMenu() =>
    [
        new SettingGroup
        {
            Name = "Start Menu",
            FeatureId = "power-start-menu",
            Settings =
            [
                new SettingDefinition
                {
                    Id = "start-power-lock-option",
                    Icon = "EyeLock",
                    IsSubjectivePreference = true,
                    Name = "Show Lock Option",
                    Description = "Display the Lock option in the Start Menu power button menu",
                    InputType = InputType.Toggle,
                    Dependencies =
                    [
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "start-power-lock-option",
                            RequiredSettingId = "privacy-lock-screen",
                            RequiredModule = "PrivacyOptimizations",
                        },
                    ],
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                            ValueName = "ShowLockOption",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
                new SettingDefinition
                {
                    Id = "start-power-sleep-option",
                    Icon = "LightbulbNight",
                    IsSubjectivePreference = true,
                    Name = "Show Sleep Option",
                    Description = "Display the Sleep option in the Start Menu power button menu",
                    InputType = InputType.Toggle,
                    RegistrySettings =
                    [
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings",
                            ValueName = "ShowSleepOption",
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            ValueType = RegistryValueKind.DWord,
                        },
                    ],
                },
            ],
        },
    ];
}
