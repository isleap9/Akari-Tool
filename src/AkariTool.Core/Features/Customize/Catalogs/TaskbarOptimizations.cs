using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class TaskbarOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildLayout(),
        .. BuildBehavior(),
        .. BuildGrouping(),
    ];

    private static IReadOnlyList<SettingGroup> BuildLayout() =>
    [
        new SettingGroup
        {
            Name = "Layout",
            FeatureId = "customize-taskbar-layout",
            Settings = new[]
            {
                // customize-taskbar-clean — Winhance "Clean Taskbar" 1:1: Action that clears the
                //   pinned-items list (Taskband\Favorites = empty REG_BINARY) and restarts Explorer.
                new SettingDefinition
                {
                    Id = "customize-taskbar-clean",
                    Icon = "Broom",
                    Name = "Clean Taskbar",
                    Description = "Removes all pinned items from the Taskbar",
                    InputType = InputType.Action,
                    IsSubjectivePreference = true,
                    RequiresConfirmation = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband",
                            ValueName = "Favorites",
                            ValueType = RegistryValueKind.Binary,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { new byte[0] },
                            DisabledValue = new object?[] { null },
                            IsPrimary = true,
                        },
                    },
                },
                // customize-taskbar-news-and-interests — Winhance "Show News and Interests" 1:1:
                //   Windows 10 only; EnableFeeds group policy (HKCU + HKLM): show=1/hide=0.
                new SettingDefinition
                {
                    Id = "customize-taskbar-news-and-interests",
                    Icon = "Newspaper",
                    Name = "Show News and Interests",
                    Description = "Show the News and Interests widget that displays headlines, weather, stocks, and other personalized content",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    IsWindows10Only = true,
                    RestartProcess = "Explorer",
                    RecommendedToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Windows Feeds",
                            ValueName = "EnableFeeds",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsGroupPolicy = true,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds",
                            ValueName = "EnableFeeds",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsGroupPolicy = true,
                        },
                    },
                },
                // customize-taskbar-align-left — TaskbarAl: enable=0 (left), disable=1 (center); absent=disabled
                new SettingDefinition
                {
                    Id = "customize-taskbar-align-left",
                    Icon = "FileTableBoxOutline",
                    Name = "Align Taskbar Left",
                    Description = "Moves taskbar icons to the left — restores Windows 10 layout",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarAl",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-hide-search — SearchboxTaskbarMode: enable=0 (hidden), disable=3 (full box)
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-search",
                    Icon = "Magnify",
                    Name = "Hide Search Bar",
                    Description = "Removes the search box/icon from the taskbar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Search",
                            ValueName = "SearchboxTaskbarMode",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 3 },
                        },
                    },
                },
                // customize-taskbar-hide-task-view — ShowTaskViewButton: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-task-view",
                    Icon = "DockWindow",
                    Name = "Hide Task View Button",
                    Description = "Removes the Task View (virtual desktops) button from the taskbar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowTaskViewButton",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // taskbar-widgets — AllowNewsAndInterests (Dsh group policy, HKCU+HKLM): show=1/hide=0
                new SettingDefinition
                {
                    Id = "taskbar-widgets",
                    Icon = "Widgets",
                    Name = "Show Widgets",
                    Description = "Show the Widgets button that displays personalized news, weather, calendar, and other information",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    IsWindows11Only = true,
                    RestartProcess = "Explorer",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Dsh",
                            ValueName = "AllowNewsAndInterests",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh",
                            ValueName = "AllowNewsAndInterests",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = 0,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-hide-chat — TaskbarMn: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-chat",
                    Icon = "Chat",
                    Name = "Hide Chat / Meet Now",
                    Description = "Removes the Chat (Microsoft Teams) button from the taskbar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarMn",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-hide-copilot — ShowCopilotButton: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-copilot",
                    Icon = "BrainCircuit",
                    IconPack = "Fluent",
                    Name = "Hide Copilot Button",
                    Description = "Removes the Copilot AI button from the taskbar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowCopilotButton",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-hide-copilot-companion — TaskbarCompanion: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-copilot-companion",
                    Icon = "Robot",
                    Name = "Hide Copilot Companion Button",
                    Description = "Removes the Copilot Companion button from the taskbar (newer builds)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarCompanion",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-hide-copilot-pwa-pin — CopilotPWAPin: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-copilot-pwa-pin",
                    Icon = "Pin",
                    Name = "Hide Copilot PWA Pin",
                    Description = "Removes the pinned Copilot web app from the taskbar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "CopilotPWAPin",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-hide-recall-pin — RecallPin: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-recall-pin",
                    Icon = "History",
                    Name = "Hide Recall Pin",
                    Description = "Removes the pinned Recall shortcut from the taskbar",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "RecallPin",
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

    private static IReadOnlyList<SettingGroup> BuildBehavior() =>
    [
        new SettingGroup
        {
            Name = "Behavior",
            FeatureId = "customize-taskbar-behavior",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "customize-taskbar-auto-hide",
                    Icon = "ArrowCollapseDown",
                    Name = "Auto-hide the Taskbar",
                    Description = "Automatically hides the taskbar when not in use, revealing it when you move the cursor to the bottom of the screen.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RestartProcess = "explorer",
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3",
                            ValueName = "Settings",
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 8,
                            BitMask = 0x01,
                            RecommendedValue = null,
                            DefaultValue = null,
                            IsPrimary = true,
                        },
                    },
                },

                // customize-taskbar-end-task — TaskbarEndTask: enable=1/disable=0
                new SettingDefinition
                {
                    Id = "customize-taskbar-end-task",
                    Icon = "ApplicationCog",
                    Name = "End Task on Taskbar",
                    Description = "Adds 'End Task' to the right-click menu for taskbar app buttons",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                            ValueName = "TaskbarEndTask",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-clock-seconds — HKLM ShowSecondsInSystemClock: enable=1/disable=0
                new SettingDefinition
                {
                    Id = "customize-taskbar-clock-seconds",
                    Icon = "ClockOutline",
                    Name = "Show Seconds in Clock",
                    Description = "Displays seconds in the system tray clock",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowSecondsInSystemClock",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-hide-badges — TaskbarBadges: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-badges",
                    Icon = "Bell",
                    Name = "Hide Taskbar Badges",
                    Description = "Removes notification badge counters from taskbar app icons",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarBadges",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-disable-thumbnails — ExtendedUIHoverTime: enable=30000/disable=400
                new SettingDefinition
                {
                    Id = "customize-taskbar-disable-thumbnails",
                    Icon = "ImageMultiple",
                    Name = "Disable Taskbar Thumbnails",
                    Description = "Stops thumbnail previews appearing when hovering taskbar icons — reduces RAM usage",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ExtendedUIHoverTime",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 30000 },
                            DisabledValue = new object?[] { 400 },
                        },
                    },
                },
                // customize-taskbar-disable-aero-peek — DisablePreviewDesktop: enable=1/disable=0
                new SettingDefinition
                {
                    Id = "customize-taskbar-disable-aero-peek",
                    Icon = "EyeOffOutline",
                    Name = "Disable Aero Peek",
                    Description = "Disables the Show Desktop peek when hovering the bottom-right corner",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "DisablePreviewDesktop",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-disable-snap-assist — SnapAssist: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-disable-snap-assist",
                    Icon = "ViewDashboardOutline",
                    Name = "Disable Snap Assist",
                    Description = "Disables the snap layout overlay that appears when dragging windows",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "SnapAssist",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-verbose-status — HKLM verbosestatus: enable=1/disable=0
                new SettingDefinition
                {
                    Id = "customize-taskbar-verbose-status",
                    Icon = "MessageTextOutline",
                    Name = "Verbose Boot/Shutdown Messages",
                    Description = "Shows detailed status messages during Windows startup and shutdown",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                            ValueName = "verbosestatus",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-show-all-tray-icons — EnableAutoTray: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-show-all-tray-icons",
                    Icon = "TrayFull",
                    Name = "Always Show All Tray Icons",
                    Description = "Shows every system tray icon instead of collapsing them into the overflow flyout",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "EnableAutoTray",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-hide-on-other-displays — MMTaskbarEnabled: enable=0/disable=1
                new SettingDefinition
                {
                    Id = "customize-taskbar-hide-on-other-displays",
                    Icon = "MonitorMultiple",
                    Name = "Hide Taskbar on Other Displays",
                    Description = "Shows the taskbar only on the main monitor in multi-display setups",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MMTaskbarEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-taskbar-autohide-hover-delay — Selection: ExtendedUIHoverTime (1/10/50/100/200/400)
                new SettingDefinition
                {
                    Id = "customize-taskbar-autohide-hover-delay",
                    Icon = "DockBottom",
                    Name = "Taskbar Auto-Hide Hover Delay",
                    Description = "How long you must hover at the screen edge before the auto-hidden taskbar appears. Lower values reveal the taskbar faster",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ExtendedUIHoverTime",
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
                            new ComboBoxOption { DisplayName = "1ms (Instant)", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 1 } },
                            new ComboBoxOption { DisplayName = "10ms (Very Fast)", ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 10 } },
                            new ComboBoxOption { DisplayName = "50ms (Fast)", ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 50 } },
                            new ComboBoxOption { DisplayName = "100ms (Moderate)", ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 100 } },
                            new ComboBoxOption { DisplayName = "200ms", ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 200 } },
                            new ComboBoxOption { DisplayName = "400ms (Default)", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["ExtendedUIHoverTime"] = 400 } },
                        },
                    },
                },
                // customize-taskbar-show-desktop-corner — TaskbarSd: enable=1/disable=0; absent=enabled
                new SettingDefinition
                {
                    Id = "customize-taskbar-show-desktop-corner",
                    Icon = "DesktopClassic",
                    Name = "Show Desktop from Taskbar Corner",
                    Description = "Click the far corner of the taskbar to minimize all windows and show the desktop",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarSd",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-share-any-window — TaskbarSn: enable=1/disable=0; absent=enabled
                new SettingDefinition
                {
                    Id = "customize-taskbar-share-any-window",
                    Icon = "ShareVariant",
                    Name = "Share Any Window from Taskbar",
                    Description = "Lets you share any open window directly from the taskbar during a Teams call",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarSn",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-flashing — TaskbarFlashing: on=1/off=0; absent=enabled
                new SettingDefinition
                {
                    Id = "customize-taskbar-flashing",
                    Icon = "FlashAlert",
                    Name = "Show Flashing on Taskbar Apps",
                    Description = "Flash taskbar buttons when an app needs attention",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarFlashing",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-small-icons — TaskbarSmallIcons: on=1/off=0; absent=disabled
                new SettingDefinition
                {
                    Id = "customize-taskbar-small-icons",
                    Icon = "SizeXxs",
                    Name = "Use Small Taskbar Buttons",
                    Description = "Use smaller taskbar buttons (Windows 10 only; ignored on Windows 11)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarSmallIcons",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-meet-now — HideSCAMeetNow: on=1/off=0; absent=disabled
                new SettingDefinition
                {
                    Id = "customize-taskbar-meet-now",
                    Icon = "Video",
                    IconPack = "Fluent",
                    Name = "Remove Meet Now Button",
                    Description = "Hide the Meet Now button from the system tray",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer",
                            ValueName = "HideSCAMeetNow",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-taskbar-button-size — Selection: IconSizePreference (Always=0 / When full=2 default / Never=1)
                new SettingDefinition
                {
                    Id = "customize-taskbar-button-size",
                    Icon = "Resize",
                    Name = "Combine Taskbar Buttons / Small Buttons",
                    Description = "When to use smaller taskbar buttons",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    ResolveUnmatchedToDefault = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "IconSizePreference",
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
                            new ComboBoxOption { DisplayName = "Always", ValueMappings = new Dictionary<string, object?> { ["IconSizePreference"] = 0 } },
                            new ComboBoxOption { DisplayName = "When taskbar is full", IsDefault = true, IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["IconSizePreference"] = 2 } },
                            new ComboBoxOption { DisplayName = "Never", ValueMappings = new Dictionary<string, object?> { ["IconSizePreference"] = 1 } },
                        },
                    },
                },
                // customize-taskbar-transparency — Selection: TaskbarAcrylicOpacity (Windows default=absent / Transparent=0 / Opaque=255)
                new SettingDefinition
                {
                    Id = "customize-taskbar-transparency",
                    Icon = "Opacity",
                    Name = "Taskbar Transparency",
                    Description = "Force the taskbar fully transparent or fully opaque",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    ResolveUnmatchedToDefault = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarAcrylicOpacity",
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
                            new ComboBoxOption { DisplayName = "Windows default", IsDefault = true, IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["TaskbarAcrylicOpacity"] = null } },
                            new ComboBoxOption { DisplayName = "Transparent", ValueMappings = new Dictionary<string, object?> { ["TaskbarAcrylicOpacity"] = 0 } },
                            new ComboBoxOption { DisplayName = "Opaque", ValueMappings = new Dictionary<string, object?> { ["TaskbarAcrylicOpacity"] = 255 } },
                        },
                    },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildGrouping() =>
    [
        new SettingGroup
        {
            Name = "Button Grouping",
            FeatureId = "customize-taskbar-grouping",
            Settings = new[]
            {
                // customize-taskbar-button-grouping — Selection: TaskbarGlomLevel (0 default / 1 / 2)
                new SettingDefinition
                {
                    Id = "customize-taskbar-button-grouping",
                    Icon = "Tab",
                    Name = "Combine Taskbar Buttons",
                    Description = "Controls whether taskbar buttons for the same app are grouped together",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    ResolveUnmatchedToDefault = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarGlomLevel",
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
                            new ComboBoxOption { DisplayName = "Always combine", IsDefault = true, IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["TaskbarGlomLevel"] = 0 } },
                            new ComboBoxOption { DisplayName = "Combine when taskbar is full", ValueMappings = new Dictionary<string, object?> { ["TaskbarGlomLevel"] = 1 } },
                            new ComboBoxOption { DisplayName = "Never combine", ValueMappings = new Dictionary<string, object?> { ["TaskbarGlomLevel"] = 2 } },
                        },
                    },
                },
                // customize-taskbar-grouping-multimonitor — Selection: MMTaskbarMode (0 default / 1 / 2)
                new SettingDefinition
                {
                    Id = "customize-taskbar-grouping-multimonitor",
                    Icon = "Monitor",
                    Name = "Show Taskbar Apps On",
                    Description = "Which monitors show open-window buttons when the taskbar spans multiple displays",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    ResolveUnmatchedToDefault = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MMTaskbarMode",
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
                            new ComboBoxOption { DisplayName = "All taskbars", IsDefault = true, IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["MMTaskbarMode"] = 0 } },
                            new ComboBoxOption { DisplayName = "Main taskbar and taskbar where window is open", ValueMappings = new Dictionary<string, object?> { ["MMTaskbarMode"] = 1 } },
                            new ComboBoxOption { DisplayName = "Taskbar where window is open", ValueMappings = new Dictionary<string, object?> { ["MMTaskbarMode"] = 2 } },
                        },
                    },
                },
                // customize-taskbar-grouping-other-taskbars — Selection: MMTaskbarGlomLevel (0 default / 1 / 2)
                new SettingDefinition
                {
                    Id = "customize-taskbar-grouping-other-taskbars",
                    Icon = "TabUnselected",
                    Name = "Combine Buttons on Other Taskbars",
                    Description = "Button grouping behaviour on secondary-monitor taskbars",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    ResolveUnmatchedToDefault = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MMTaskbarGlomLevel",
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
                            new ComboBoxOption { DisplayName = "Always combine", IsDefault = true, IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["MMTaskbarGlomLevel"] = 0 } },
                            new ComboBoxOption { DisplayName = "Combine when taskbar is full", ValueMappings = new Dictionary<string, object?> { ["MMTaskbarGlomLevel"] = 1 } },
                            new ComboBoxOption { DisplayName = "Never combine", ValueMappings = new Dictionary<string, object?> { ["MMTaskbarGlomLevel"] = 2 } },
                        },
                    },
                },
            },
        },
    ];
}
