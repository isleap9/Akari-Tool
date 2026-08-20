using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class StartMenuOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildLayout(),
        .. BuildBehavior(),
    ];

    private static IReadOnlyList<SettingGroup> BuildLayout() =>
    [
        new SettingGroup
        {
            Name = "Layout",
            FeatureId = "customize-startmenu-layout",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "customize-start-clean-pins",
                    Name = "Clean Start Menu Pins",
                    Description = "Removes all pinned apps from the Start Menu, leaving a clean empty layout",
                    InputType = InputType.Action,
                    IsSubjectivePreference = true,
                    IsWindows11Only = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            ValueName = "ConfigureStartPins",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "{\"pinnedList\":[]}" },
                            DisabledValue = new object?[] { null },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                            ValueName = "ConfigureStartPins",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "{\"pinnedList\":[]}" },
                            DisabledValue = new object?[] { null },
                        }
                    },
                    PowerShellScripts = new[]
                    {
                        new PowerShellScriptSetting
                        {
                            RunContext = RunContext.System,
                            EnabledScript = "Stop-Process -Name StartMenuExperienceHost -Force -ErrorAction SilentlyContinue\r\nGet-ChildItem 'C:\\Users' -Directory -ErrorAction SilentlyContinue | ForEach-Object {\r\n    $statePath = Join-Path $_.FullName 'AppData\\Local\\Packages\\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\\LocalState'\r\n    if (Test-Path $statePath) {\r\n        Get-ChildItem $statePath -Filter 'start*.bin' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue\r\n    }\r\n}",
                            DisabledScript = null,
                        }
                    },
                },

                // customize-start-hide-recommended-policy — HideRecommendedSection written to three
                //   keys + IsEducationEnvironment to a fourth; enable=1/disable=0. Reader checks
                //   HKLM Policies\...\Explorer HideRecommendedSection == 1 (primary, first).
                new SettingDefinition
                {
                    Id = "customize-start-hide-recommended-policy",
                    Name = "Hide Recommended Section (Policy)",
                    Description = "Completely removes the Recommended section from Start using the Education-SKU policy trick",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            ValueName = "HideRecommendedSection",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            ValueName = "HideRecommendedSection",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start",
                            ValueName = "HideRecommendedSection",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education",
                            ValueName = "IsEducationEnvironment",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-start-more-pins — Start_Layout: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-start-more-pins",
                    Name = "More Pins (Less Recommendations)",
                    Description = "Sets Start Menu layout to show more pinned apps and fewer recommendations",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_Layout",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-start-disable-recommended — Start_IrisRecommendations (inverted):
                //   hide (on) = 0 / show (off) = 1. Reader: IrisRecommendations == 0 → hidden.
                new SettingDefinition
                {
                    Id = "customize-start-disable-recommended",
                    Name = "Disable Recommended Section",
                    Description = "Hides the Recommended apps/files section from the Start Menu",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_IrisRecommendations",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-start-show-recent-apps — ShowRecentList: on=1/off=0; absent = shown.
                new SettingDefinition
                {
                    Id = "customize-start-show-recent-apps",
                    Name = "Show Recently Added Apps",
                    Description = "Show a recently added apps list at the top of the Start menu",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "ShowRecentList",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-start-show-most-used — ShowFrequentList: on=1/off=0; absent = shown.
                new SettingDefinition
                {
                    Id = "customize-start-show-most-used",
                    Name = "Show Most Used Apps",
                    Description = "Show a most-used apps list in the Start menu",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "ShowFrequentList",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-start-show-suggestions — SubscribedContent-338388Enabled: on=1/off=0; absent = shown.
                new SettingDefinition
                {
                    Id = "customize-start-show-suggestions",
                    Name = "Show Suggestions in Start",
                    Description = "Show occasional app and tip suggestions in the Start menu",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338388Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-start-all-apps-view — Selection: AllAppsViewMode (Category=0 / Grid=1 / List=2 default)
                new SettingDefinition
                {
                    Id = "customize-start-all-apps-view",
                    Name = "All Apps View",
                    Description = "How the All apps list is displayed in the Start menu",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    ResolveUnmatchedToDefault = true,
                    MinimumBuildNumber = 26100,
                    MinimumBuildRevision = 7171,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start",
                            ValueName = "AllAppsViewMode",
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
                            new ComboBoxOption { DisplayName = "Category", IsDefault = true, ValueMappings = new Dictionary<string, object?> { ["AllAppsViewMode"] = 0 } },
                            new ComboBoxOption { DisplayName = "Grid", ValueMappings = new Dictionary<string, object?> { ["AllAppsViewMode"] = 1 } },
                            new ComboBoxOption { DisplayName = "List", IsRecommended = true, ValueMappings = new Dictionary<string, object?> { ["AllAppsViewMode"] = 2 } },
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
            FeatureId = "customize-startmenu-behavior",
            Settings = new[]
            {
                // customize-start-disable-bing-search — two keys with opposite polarity:
                //   policy DisableSearchBoxSuggestions on=1/off=0 (primary — reader checks it first),
                //   BingSearchEnabled on=0/off=1 (secondary fallback).
                new SettingDefinition
                {
                    Id = "customize-start-disable-bing-search",
                    Name = "Disable Bing Search in Start",
                    Description = "Removes web (Bing) search results from the Start Menu search box",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows",
                            ValueName = "DisableSearchBoxSuggestions",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                            ValueName = "BingSearchEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-start-disable-account-notifications — Start_AccountNotifications (inverted):
                //   disable (on) = 0 / enable (off) = 1. Reader: Start_AccountNotifications == 0 → disabled.
                new SettingDefinition
                {
                    Id = "customize-start-disable-account-notifications",
                    Name = "Disable Account-Related Notifications",
                    Description = "Removes 'Add an account' and Microsoft account prompts from Start",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_AccountNotifications",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-start-disable-web-suggestions — CortanaConsent (inverted):
                //   disable (on) = 0 / enable (off) = 1. Reader: CortanaConsent == 0 → disabled.
                new SettingDefinition
                {
                    Id = "customize-start-disable-web-suggestions",
                    Name = "Disable Web Suggestions in Search",
                    Description = "Prevents Windows Search from showing online/web suggestions",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search",
                            ValueName = "CortanaConsent",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-start-show-recently-opened-items — Start_TrackDocs: on=1/off=0; absent = shown.
                new SettingDefinition
                {
                    Id = "customize-start-show-recently-opened-items",
                    Name = "Show Recently Opened Items",
                    Description = "Shows recently opened files in Start, Jump Lists and File Explorer Quick Access",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_TrackDocs",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
            },
        },
    ];
}
