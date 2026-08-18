using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Privacy;

public static class PrivacyOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildSecurity(),
        .. BuildContentDeliveryLockScreenGeneral(),
        .. BuildSpeechInkingDiagnostics(),
        .. BuildSearchActivityAppPermissions(),
        .. BuildWindowsAI(),
        .. BuildEdgeAI(),
        .. BuildOfficeAI(),
    ];

    private static IReadOnlyList<SettingGroup> BuildSecurity() =>
    [
        new SettingGroup
        {
            Name = "Security",
            FeatureId = "privacy-security",
            Settings = new[]
            {
                // security-uac-level — composite two-value dropdown (ConsentPromptBehaviorAdmin + PromptOnSecureDesktop)
                new SettingDefinition
                {
                    Id = "security-uac-level",
                    Name = "User Account Control Level",
                    Description = "Controls UAC notification level and secure desktop behavior",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                            ValueName = "ConsentPromptBehaviorAdmin",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = null,
                            DisabledValue = null,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                            ValueName = "PromptOnSecureDesktop",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = null,
                            DisabledValue = null,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Prompt for Credentials",
                                ValueMappings = new Dictionary<string, object?> { ["ConsentPromptBehaviorAdmin"] = 1, ["PromptOnSecureDesktop"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Always notify",
                                ValueMappings = new Dictionary<string, object?> { ["ConsentPromptBehaviorAdmin"] = 2, ["PromptOnSecureDesktop"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Notify when apps make changes (Default)",
                                IsDefault = true,
                                IsRecommended = true,
                                ValueMappings = new Dictionary<string, object?> { ["ConsentPromptBehaviorAdmin"] = 5, ["PromptOnSecureDesktop"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Notify when apps make changes (no dim)",
                                Warning = "Disables the secure desktop: UAC prompts render on the normal desktop where other software can read or spoof them. Slightly weakens protection against prompt-hijacking malware.",
                                ValueMappings = new Dictionary<string, object?> { ["ConsentPromptBehaviorAdmin"] = 5, ["PromptOnSecureDesktop"] = 0 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Never notify",
                                Warning = "Turns UAC prompts off entirely — anything running as your user can silently elevate to administrator. This significantly weakens system security and some apps (and the Microsoft Store) may misbehave. Not recommended.",
                                ValueMappings = new Dictionary<string, object?> { ["ConsentPromptBehaviorAdmin"] = 0, ["PromptOnSecureDesktop"] = 0 },
                            },
                        },
                    },
                },
                // security-workplace-join-messages — writes HKLM + HKCU; delete-on-enable, set 1 on disable
                new SettingDefinition
                {
                    Id = "security-workplace-join-messages",
                    Name = "Workplace Join Message Prompts",
                    Description = "Show 'Allow my organization to manage my device' prompts throughout Windows",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin",
                            ValueName = "BlockAADWorkplaceJoin",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\WorkplaceJoin",
                            ValueName = "BlockAADWorkplaceJoin",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // security-bitlocker-auto-encryption — INVERTED: PreventDeviceEncryption 0=enabled, 1=disabled
                new SettingDefinition
                {
                    Id = "security-bitlocker-auto-encryption",
                    Name = "BitLocker Auto Encryption",
                    Description = "Controls whether Windows can automatically encrypt drives with BitLocker",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\BitLocker",
                            ValueName = "PreventDeviceEncryption",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // security-wifi-sense — two RegistrySettings (two distinct WiFi policy subkeys, each ValueName "Value")
                new SettingDefinition
                {
                    Id = "security-wifi-sense",
                    Name = "WiFi-Sense",
                    Description = "Allow sharing WiFi passwords with contacts and automatically connecting to suggested open hotspots",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // security-automatic-maintenance — INVERTED: MaintenanceDisabled 0=enabled, 1=disabled
                new SettingDefinition
                {
                    Id = "security-automatic-maintenance",
                    Name = "Automatic Maintenance",
                    Description = "Choose if Windows should run automatic system maintenance tasks during idle time",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance",
                            ValueName = "MaintenanceDisabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // security-error-reporting — INVERTED: Disabled 0=enabled, 1=disabled; writes HKLM (primary) + HKCU
                new SettingDefinition
                {
                    Id = "security-error-reporting",
                    Name = "Windows Error Reporting",
                    Description = "Choose if Windows should collect and send crash reports and error information to Microsoft",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                            ValueName = "Disabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                            ValueName = "Disabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // security-remote-assistance — fAllowToGetHelp 1=enabled, 0=disabled
                new SettingDefinition
                {
                    Id = "security-remote-assistance",
                    Name = "Remote Assistance",
                    Description = "Choose if other people can connect to your computer remotely to provide technical support",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance",
                            ValueName = "fAllowToGetHelp",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // security-smart-app-control — Selection (VerifiedAndReputablePolicyState)
                new SettingDefinition
                {
                    Id = "security-smart-app-control",
                    Name = "Smart App Control",
                    Description = "Controls the Smart App Control feature which blocks untrusted and potentially dangerous applications",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\CI\Policy",
                            ValueName = "VerifiedAndReputablePolicyState",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = null,
                            DisabledValue = null,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption
                            {
                                DisplayName = "Off (Recommended)",
                                IsRecommended = true,
                                ValueMappings = new Dictionary<string, object?> { ["VerifiedAndReputablePolicyState"] = 0 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "On (Enforced)",
                                ValueMappings = new Dictionary<string, object?> { ["VerifiedAndReputablePolicyState"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Evaluation Mode (Default)",
                                IsDefault = true,
                                ValueMappings = new Dictionary<string, object?> { ["VerifiedAndReputablePolicyState"] = 2 },
                            },
                        },
                    },
                },
                // security-developer-mode — AllowDevelopmentWithoutDevLicense 1=enabled; absent=disabled (DefaultState=false)
                new SettingDefinition
                {
                    Id = "security-developer-mode",
                    Name = "Developer Mode",
                    Description = "Allows the installation of apps from any source, including loose files",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\AppModelUnlock",
                            ValueName = "AllowDevelopmentWithoutDevLicense",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // security-powershell-execution-policy deferred: HKCU→HKLM fallback read not expressible in single-primary-value model
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildContentDeliveryLockScreenGeneral() =>
    [
        // ══════════════════════════════════════════════════════════════════
        // GROUP 2 — Content Delivery & Advertising
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Content Delivery & Advertising",
            FeatureId = "privacy-content-delivery",
            Settings = new[]
            {
                // privacy-content-delivery-allowed — non-absent: on writes 1, off writes 0; absent=enabled
                new SettingDefinition
                {
                    Id = "privacy-content-delivery-allowed",
                    Name = "Content Delivery",
                    Description = "Allows Windows to deliver promotional content and automatically install suggested apps",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "ContentDeliveryAllowed",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-subscribed-content — isAbsent: on deletes value, off writes 0; absent=enabled
                new SettingDefinition
                {
                    Id = "privacy-subscribed-content",
                    Name = "Subscribed Content",
                    Description = "Enables promotional content subscriptions from Microsoft and partners throughout Windows",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContentEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-feature-management — non-absent
                new SettingDefinition
                {
                    Id = "privacy-feature-management",
                    Name = "Feature Management",
                    Description = "Enables Windows feature management functionality for promotional features and automatic app installations",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "FeatureManagementEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-soft-landing — non-absent
                new SettingDefinition
                {
                    Id = "privacy-soft-landing",
                    Name = "Soft Landing Experiences",
                    Description = "Displays tips and notifications about Windows features as you use the operating system",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SoftLandingEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-oem-preinstalled-apps — non-absent
                new SettingDefinition
                {
                    Id = "privacy-oem-preinstalled-apps",
                    Name = "OEM Pre-installed Apps",
                    Description = "Prevents OEM manufacturers from automatically installing bloatware apps",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "OemPreInstalledAppsEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-preinstalled-apps — non-absent
                new SettingDefinition
                {
                    Id = "privacy-preinstalled-apps",
                    Name = "Pre-installed Suggested Apps",
                    Description = "Prevents Microsoft from automatically installing suggested apps",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "PreInstalledAppsEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-preinstalled-apps-ever — isAbsent
                new SettingDefinition
                {
                    Id = "privacy-preinstalled-apps-ever",
                    Name = "Pre-installed Apps History Tracking",
                    Description = "Disables tracking of whether pre-installed apps were ever enabled",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "PreInstalledAppsEverEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-silent-installed-apps — non-absent
                new SettingDefinition
                {
                    Id = "privacy-silent-installed-apps",
                    Name = "Silent App Installation",
                    Description = "Prevents apps from being silently installed in the background",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SilentInstalledAppsEnabled",
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

        // ══════════════════════════════════════════════════════════════════
        // GROUP 3 — Lock Screen
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Lock Screen",
            FeatureId = "privacy-lock-screen",
            Settings = new[]
            {
                // privacy-lock-screen — INVERTED: DisableLockWorkstation 0/absent=enabled, 1=disabled; IsPreference=true
                new SettingDefinition
                {
                    Id = "privacy-lock-screen",
                    Name = "Lock Screen",
                    Description = "Allows users to lock their computer using Windows+L, Start menu, or Ctrl+Alt+Del. Disabling prevents locking the workstation",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon",
                            ValueName = "DisableLockWorkstation",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // privacy-rotating-lock-screen — non-absent; absent=enabled
                new SettingDefinition
                {
                    Id = "privacy-rotating-lock-screen",
                    Name = "Windows Spotlight on Lock Screen",
                    Description = "Displays rotating Windows Spotlight images on your lock screen instead of a static background",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "RotatingLockScreenEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-lock-screen-overlay — non-absent; writes two values (primary read = RotatingLockScreenOverlayEnabled)
                new SettingDefinition
                {
                    Id = "privacy-lock-screen-overlay",
                    Name = "Lock Screen Fun Facts and Tips",
                    Description = "Displays fun facts, tips, and tricks as an overlay on your lock screen",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "RotatingLockScreenOverlayEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338387Enabled",
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

        // ══════════════════════════════════════════════════════════════════
        // GROUP 4 — General
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "General",
            FeatureId = "privacy-general",
            Settings = new[]
            {
                // privacy-advertising-id — three RegistrySettings: primary Enabled, CPSS Store mirror, HKCU policy (disable-only)
                new SettingDefinition
                {
                    Id = "privacy-advertising-id",
                    Name = "Let apps show personalized ads by using my advertising ID",
                    Description = "Windows generates a unique advertising ID that apps use to track your activity and deliver personalized ads",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
                            ValueName = "Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\AdvertisingInfo",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
                            ValueName = "DisabledByGroupPolicy",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // privacy-language-list — delete-on-enable, set 1 on disable
                new SettingDefinition
                {
                    Id = "privacy-language-list",
                    Name = "Let websites show locally relevant content by accessing my language list",
                    Description = "Allows websites to access your language preferences so they can display content in your preferred language",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\International\User Profile",
                            ValueName = "HttpAcceptLanguageOptOut",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // privacy-app-launch-tracking — delete-on-enable, set 0 on disable
                new SettingDefinition
                {
                    Id = "privacy-app-launch-tracking",
                    Name = "Let Windows improve Start and search results by tracking app launches",
                    Description = "Windows records which apps you use most frequently to personalize your Start menu and improve search results",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Start_TrackProgs",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-settings-content — three values; delete-on-enable, set 0 on disable (primary read = 338393)
                new SettingDefinition
                {
                    Id = "privacy-settings-content",
                    Name = "Show me suggested content in the Settings app",
                    Description = "Displays promotional content, tips, and feature suggestions within the Windows Settings app",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338393Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353694Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353696Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-settings-notifications — delete-on-enable, set 0 on disable
                new SettingDefinition
                {
                    Id = "privacy-settings-notifications",
                    Name = "Settings App Notifications",
                    Description = "Shows account notifications in the Settings app, including prompts to reauthenticate and backup your device",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SystemSettings\AccountNotifications",
                            ValueName = "EnableAccountNotifications",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildSpeechInkingDiagnostics() =>
    [
        // ══════════════════════════════════════════════════════════════════
        // GROUP 5 — Speech
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Speech",
            FeatureId = "privacy-speech",
            Settings = new[]
            {
                // privacy-speech-recognition — primary HasAccepted {1}/{0}; policy AllowInputPersonalization deleted on disable (HKCU+HKLM)
                new SettingDefinition
                {
                    Id = "privacy-speech-recognition",
                    Name = "Online Speech Recognition",
                    Description = "Use your voice for apps using Microsoft's online speech recognition technology",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy",
                            ValueName = "HasAccepted",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\InputPersonalization",
                            ValueName = "AllowInputPersonalization",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { null },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\InputPersonalization",
                            ValueName = "AllowInputPersonalization",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { null },
                        },
                    },
                },
                // privacy-narrator-online-services — delete-on-enable, set 0 on disable
                new SettingDefinition
                {
                    Id = "privacy-narrator-online-services",
                    Name = "Narrator Online Services",
                    Description = "Allow Narrator to use Microsoft cloud services for features like intelligent image descriptions",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam",
                            ValueName = "OnlineServicesEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-narrator-scripting — delete-on-enable, set 0 on disable
                new SettingDefinition
                {
                    Id = "privacy-narrator-scripting",
                    Name = "Narrator Scripting Support",
                    Description = "Allow Narrator to execute scripts for automation and custom functionality",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam",
                            ValueName = "ScriptingEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
            },
        },

        // ══════════════════════════════════════════════════════════════════
        // GROUP 6 — Inking & Typing Personalization
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Inking & Typing Personalization",
            FeatureId = "privacy-inking",
            Settings = new[]
            {
                // privacy-inking-typing-dictionary — 4 RegistrySettings across three key paths
                new SettingDefinition
                {
                    Id = "privacy-inking-typing-dictionary",
                    Name = "Custom Inking and Typing Dictionary",
                    Description = "Uses your typing history and handwriting patterns to create a custom dictionary",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings",
                            ValueName = "AcceptedPrivacyPolicy",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization",
                            ValueName = "RestrictImplicitTextCollection",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization\TrainedDataStore",
                            ValueName = "HarvestContacts",
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

        // ══════════════════════════════════════════════════════════════════
        // GROUP 7 — Diagnostics & Feedback
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Diagnostics & Feedback",
            FeatureId = "privacy-diagnostics",
            Settings = new[]
            {
                // privacy-diagnostics — primary ShowedToastAtLevel {3}/{1}; telemetry {3}/{0} across 4 paths; AITEnable set 0 on disable (HKCU+HKLM)
                new SettingDefinition
                {
                    Id = "privacy-diagnostics",
                    Name = "Send Diagnostic Data",
                    Description = "Send diagnostic data to Microsoft to help improve Windows and keep it secure",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack",
                            ValueName = "ShowedToastAtLevel",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 1 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "AllowTelemetry",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "AllowTelemetry",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "AllowTelemetry",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "AllowTelemetry",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "MaxTelemetryAllowed",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 3 },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AppCompat",
                            ValueName = "AITEnable",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppCompat",
                            ValueName = "AITEnable",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-improve-inking-typing — two RegistrySettings, both {1}/{0}
                new SettingDefinition
                {
                    Id = "privacy-improve-inking-typing",
                    Name = "Improve inking and typing",
                    Description = "Send optional inking and typing diagnostic data to Microsoft",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Input\TIPC",
                            ValueName = "Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\ImproveInkingAndTyping",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-tailored-experiences — primary {1}/{0}; policy DisableTailored... deleted on disable (HKCU+HKLM)
                new SettingDefinition
                {
                    Id = "privacy-tailored-experiences",
                    Name = "Tailored Experiences",
                    Description = "Let Microsoft use your diagnostic data to show personalized tips, ads and recommendations",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Privacy",
                            ValueName = "TailoredExperiencesWithDiagnosticDataEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableTailoredExperiencesWithDiagnosticData",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { null },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\CloudContent",
                            ValueName = "DisableTailoredExperiencesWithDiagnosticData",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { null },
                        },
                    },
                },
                // privacy-feedback-frequency — delete-on-enable: Siuf NumberOfSIUFInPeriod (primary) + DoNotShowFeedbackNotifications (HKCU+HKLM)
                new SettingDefinition
                {
                    Id = "privacy-feedback-frequency",
                    Name = "Allow Windows to ask you for feedback",
                    Description = "Let Windows ask you to provide feedback on experiences in Windows",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Siuf\Rules",
                            ValueName = "NumberOfSIUFInPeriod",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "DoNotShowFeedbackNotifications",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            ValueName = "DoNotShowFeedbackNotifications",
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

    private static IReadOnlyList<SettingGroup> BuildSearchActivityAppPermissions() =>
    [
        // ══════════════════════════════════════════════════════════════════
        // GROUP 8 — Search Permissions
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Search Permissions",
            FeatureId = "privacy-search",
            Settings = new[]
            {
                // privacy-search-history — delete-on-enable under HKCU SearchSettings
                new SettingDefinition
                {
                    Id = "privacy-search-history",
                    Name = "Search history on this device",
                    Description = "Improves search results by allowing Windows Search to store your search history locally",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsDeviceSearchHistoryEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-search-highlights — delete-on-enable under HKCU SearchSettings
                new SettingDefinition
                {
                    Id = "privacy-search-highlights",
                    Name = "Show search highlights",
                    Description = "See content suggestions in search",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsDynamicSearchBoxEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-search-msa-cloud — delete-on-enable under HKCU SearchSettings
                new SettingDefinition
                {
                    Id = "privacy-search-msa-cloud",
                    Name = "Cloud Content Search (Microsoft account)",
                    Description = "Allow Windows Search to show results from services signed in with your Microsoft account",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsMSACloudSearchEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-search-aad-cloud — delete-on-enable under HKCU SearchSettings
                new SettingDefinition
                {
                    Id = "privacy-search-aad-cloud",
                    Name = "Cloud Content Search (Work or School account)",
                    Description = "Allow Windows Search to show results from services signed in with your work or school account",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\SearchSettings",
                            ValueName = "IsAADCloudSearchEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-allow-cortana — delete-on-enable; HKLM primary + HKCU secondary
                new SettingDefinition
                {
                    Id = "privacy-allow-cortana",
                    Name = "Allow Cortana",
                    Description = "Enables Microsoft's Cortana virtual assistant for voice commands and searches",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                            ValueName = "AllowCortana",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                            ValueName = "AllowCortana",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
            },
        },

        // ══════════════════════════════════════════════════════════════════
        // GROUP 9 — Activity History
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Activity History",
            FeatureId = "privacy-activity",
            Settings = new[]
            {
                // privacy-activity-history — delete-on-enable, set 0 on disable
                new SettingDefinition
                {
                    Id = "privacy-activity-history",
                    Name = "Activity History",
                    Description = "Allows Windows to jump back into what you were doing with apps, docs, or other activities on startup (Windows 10 Timeline feature)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\System",
                            ValueName = "PublishUserActivities",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // privacy-timeline-suggestions — {1}/{0}, DefaultValue=1
                new SettingDefinition
                {
                    Id = "privacy-timeline-suggestions",
                    Name = "Timeline Suggestions",
                    Description = "Shows suggestions in the Windows 10 Timeline feature based on your activity history",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-353698Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
            },
        },

        // ══════════════════════════════════════════════════════════════════
        // GROUP 10 — App Permissions
        // ══════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "App Permissions",
            FeatureId = "privacy-app-permissions",
            Settings = new[]
            {
                // privacy-location-services — ConsentStore\location + secondary DisableLocation (HKCU+HKLM, deleted on disable)
                new SettingDefinition
                {
                    Id = "privacy-location-services",
                    Name = "Location Services",
                    Description = "Allows Windows and apps to access your device location for location-based features",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "Allow", null },
                            DisabledValue = new object?[] { "Deny" },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                            ValueName = "DisableLocation",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { null },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                            ValueName = "DisableLocation",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { null },
                        },
                    },
                },
                // privacy-camera-access — ConsentStore\webcam; preference, recommended allow
                new SettingDefinition
                {
                    Id = "privacy-camera-access",
                    Name = "Camera Access",
                    Description = "Allow apps to have camera access",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "Allow", null },
                            DisabledValue = new object?[] { "Deny" },
                        },
                    },
                },
                // privacy-microphone-access — ConsentStore\microphone; preference, recommended allow
                new SettingDefinition
                {
                    Id = "privacy-microphone-access",
                    Name = "Microphone Access",
                    Description = "Allow apps to have microphone access",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "Allow", null },
                            DisabledValue = new object?[] { "Deny" },
                        },
                    },
                },
                // privacy-account-info-access — ConsentStore\userAccountInformation
                new SettingDefinition
                {
                    Id = "privacy-account-info-access",
                    Name = "Account Info Access",
                    Description = "Allow apps to have account info access",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\userAccountInformation",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "Allow", null },
                            DisabledValue = new object?[] { "Deny" },
                        },
                    },
                },
                // privacy-app-diagnostic-access — ConsentStore\appDiagnostics
                new SettingDefinition
                {
                    Id = "privacy-app-diagnostic-access",
                    Name = "App Diagnostic Access",
                    Description = "Allow apps to have app diagnostic access",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics",
                            ValueName = "Value",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "Allow", null },
                            DisabledValue = new object?[] { "Deny" },
                        },
                    },
                },
                // privacy-onedrive-auto-backup — delete-on-enable; HKLM primary + HKCU secondary; preference
                new SettingDefinition
                {
                    Id = "privacy-onedrive-auto-backup",
                    Name = "OneDrive Automatic Backups",
                    Description = "Controls whether OneDrive automatically backs up your Documents, Pictures, and Desktop folders",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\OneDrive",
                            ValueName = "KFMBlockOptIn",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\OneDrive",
                            ValueName = "KFMBlockOptIn",
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

    private static IReadOnlyList<SettingGroup> BuildWindowsAI()
    {
        // TODO: Group 11 (Windows AI) — PrivacyTweaks.WindowsAI.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildEdgeAI()
    {
        // TODO: Group 12 (Edge AI) — PrivacyTweaks.EdgeAI.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildOfficeAI()
    {
        // TODO: Group 13 (Office AI) — PrivacyTweaks.OfficeAI.cs
        return [];
    }
}
