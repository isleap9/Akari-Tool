using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Notifications;

/// <summary>
/// Declarative Notifications catalog — the SettingDefinition replacement for the old
/// delegate-based NotificationsTweaks. Five groups (General → Additional Settings →
/// System Notifications → Privacy Notifications → Security Notifications), IDs preserved
/// byte-for-byte for backup compatibility.
/// </summary>
public static class NotificationsOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() => new[]
    {
        // ══════════════════════════════════════════════════════════════════════
        // GENERAL
        // ══════════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "General",
            FeatureId = "notifications-general",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "windows-pushnotifications",
                    Name = "Push Notifications",
                    Description = "Allows apps to send push notifications through Windows Push Notification Service.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\PushNotifications",
                            ValueName = "NoToastApplicationNotification",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-sound",
                    Name = "Notification Sounds",
                    Description = "Plays a sound when notifications arrive.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                            ValueName = "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-toast-above-lock",
                    Name = "Notifications on Lock Screen",
                    Description = "Shows notification banners on the lock screen.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                            ValueName = "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                            ValueName = "LockScreenToastEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-critical-toast-above-lock",
                    Name = "Critical Notifications on Lock Screen",
                    Description = "Shows critical notifications on the lock screen even when Do Not Disturb is on.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings",
                            ValueName = "NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-show-bell-icon",
                    Name = "Notification Bell Icon",
                    Description = "Shows the notification bell icon in the taskbar.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowNotificationIcon",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-action-center",
                    Name = "Action Center",
                    Description = "Shows the Action Center panel accessible from the taskbar.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    DisableWarning = "Disabling Action Center may cause issues with some apps including NVIDIA App. Re-enable if you experience problems.",
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                            ValueName = "DisableNotificationCenter",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
            },
        },

        // ══════════════════════════════════════════════════════════════════════
        // ADDITIONAL SETTINGS
        // ══════════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Additional Settings",
            FeatureId = "notifications-additional",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "notifications-welcome-experience",
                    Name = "Windows Welcome Experience",
                    Description = "Shows the Windows welcome experience after updates and when signing in to highlight new features.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-310093Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-system-setting-engagement",
                    Name = "Suggested Settings Engagement",
                    Description = "Shows suggested settings and engagement prompts in Windows notifications.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\UserProfileEngagement",
                            ValueName = "ScoobeSystemSettingEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-tips-suggestions",
                    Name = "Tips and Suggestions Notifications",
                    Description = "Shows tips, tricks, and suggestions as you use Windows.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SubscribedContent-338389Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-system-pane-suggestions",
                    Name = "Settings App Suggestions",
                    Description = "Shows suggestions and recommended content in the Windows Settings app.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                            ValueName = "SystemPaneSuggestionsEnabled",
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

        // ══════════════════════════════════════════════════════════════════════
        // SYSTEM NOTIFICATIONS
        // ══════════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "System Notifications",
            FeatureId = "notifications-system",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "notifications-capability-access",
                    Name = "Capability Access Notifications",
                    Description = "Notifies when apps access sensitive capabilities like camera, microphone, or location.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.CapabilityAccess",
                            ValueName = "Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-startup-app",
                    Name = "Startup App Notifications",
                    Description = "Notifies when apps are prevented from running at startup.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.StartupApp",
                            ValueName = "Enabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-clock-change",
                    Name = "Clock Change Notifications",
                    Description = "Notifies about automatic clock changes for daylight saving time.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "DstNotification",
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

        // ══════════════════════════════════════════════════════════════════════
        // PRIVACY NOTIFICATIONS
        // ══════════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Privacy Notifications",
            FeatureId = "notifications-privacy",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "notifications-app-location-request",
                    Name = "Location Access Notifications",
                    Description = "Notifies when apps request access to your location.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                            ValueName = "ShowGlobalPrompts",
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

        // ══════════════════════════════════════════════════════════════════════
        // SECURITY NOTIFICATIONS
        // ══════════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Security Notifications",
            FeatureId = "notifications-security",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "notifications-windows-security",
                    Name = "Windows Security Notifications",
                    Description = "Shows notifications from Windows Security about device health and safety.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableNotifications",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableNotifications",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableNotifications",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableEnhancedNotifications",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Notifications",
                            ValueName = "DisableEnhancedNotifications",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0, null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "notifications-security-maintenance",
                    Name = "Security and Maintenance Notifications",
                    Description = "Shows notifications from the Security and Maintenance center about system health.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance",
                            ValueName = "Enabled",
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
    };
}
