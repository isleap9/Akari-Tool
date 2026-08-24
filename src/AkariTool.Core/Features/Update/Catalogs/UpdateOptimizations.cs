using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace AkariTool.Tabs.Update;

/// <summary>
/// Declarative Update catalog — the SettingDefinition replacement for the delegate-based
/// UpdateTweaks. Three groups (Update Policy → Delivery &amp; Store → Update Behavior),
/// IDs preserved byte-for-byte for backup compatibility.
///
/// NOTE: updates-policy-mode is intentionally NOT migrated here. It remains on the old
/// TweakDefinition path in UpdateTweaks.cs because its detection requires a composite
/// multi-value read (Paused/Disabled states collide under single-value matching).
/// UpdateViewModel therefore stays a hybrid page.
///
/// Registry values (KeyPath / ValueName / EnabledValue / DisabledValue / DefaultValue)
/// were cross-referenced against UpdateTweaks.cs, which is the ground truth. Where the
/// migration spec text and the source disagreed, the source's behaviour was preserved and
/// the deviation is called out in an inline comment.
/// </summary>
// updates-policy-mode deferred: requires custom detection path for composite multi-value read (Paused/Disabled states collide on single-value matching)
public static class UpdateOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() => new[]
    {
        // ══════════════════════════════════════════════════════════════════════
        // UPDATE POLICY
        // ══════════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Update Policy",
            FeatureId = "update-policy",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "updates-policy-mode",
                    Icon = "BookSync",
                    Name = "Windows Update Policy",
                    Description = "Control how Windows updates are installed on your system",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings = Array.Empty<RegistrySetting>(),
                    ComboBox = new ComboBoxMetadata
                    {
                        Options = new[]
                        {
                            new ComboBoxOption { DisplayName = "Normal (Windows Default)", IsDefault = true },
                            new ComboBoxOption { DisplayName = "Security Updates Only (Recommended)", IsRecommended = true },
                            new ComboBoxOption { DisplayName = "Paused for a long time (Unpause in Settings)" },
                            new ComboBoxOption { DisplayName = "Disabled (NOT Recommended, Security Risk)" },
                        },
                    },
                },
            },
        },

        // ══════════════════════════════════════════════════════════════════════
        // DELIVERY & STORE
        // ══════════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Delivery & Store",
            FeatureId = "update-delivery",
            Settings = new[]
            {
                new SettingDefinition
                {
                    Id = "updates-delivery-optimization",
                    Icon = "ShareVariant",
                    Name = "Delivery Optimization",
                    Description = "Share downloaded updates with other PCs on your network or the internet to reduce bandwidth usage",
                    InputType = InputType.Selection,
                    IsSubjectivePreference = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                            ValueName = "DODownloadMode",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = null,
                            DisabledValue = null,
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                            ValueName = "DODownloadMode",
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
                                DisplayName = "Windows Default",
                                IsDefault = true,
                                ValueMappings = new Dictionary<string, object?> { ["DODownloadMode"] = null },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Devices on LAN Only",
                                ValueMappings = new Dictionary<string, object?> { ["DODownloadMode"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Devices on LAN and Internet",
                                ValueMappings = new Dictionary<string, object?> { ["DODownloadMode"] = 3 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Disabled",
                                IsRecommended = true,
                                ValueMappings = new Dictionary<string, object?> { ["DODownloadMode"] = 99 },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "updates-store-auto-download",
                    Icon = "StoreMicrosoft",
                    IconPack = "Fluent",
                    Name = "Microsoft Store Auto-Downloads",
                    Description = "Automatically downloads app updates from the Microsoft Store in the background.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore",
                            ValueName = "AutoDownload",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            // SPEC/SOURCE DEVIATION: the migration spec gave EnabledValue=[2,null],
                            // DisabledValue=[4]. UpdateTweaks.cs is inverted from that: ReadState
                            // returns ON when value != 2 (i.e. 4 or absent), OFF when value == 2;
                            // Apply turns ON by deleting the value and OFF by writing 2. Source
                            // ground truth preserved: Enabled = {4, absent}, Disabled = {2}.
                            EnabledValue = new object?[] { 4, null },
                            DisabledValue = new object?[] { 2 },
                        },
                    },
                },
            },
        },

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE BEHAVIOR
        // ══════════════════════════════════════════════════════════════════════
        new SettingGroup
        {
            Name = "Update Behavior",
            FeatureId = "update-behavior",
            Settings = new[]
            {
                // source: UX\Settings IsContinuousInnovationOptedIn — ReadState absent=on, v!=0=on;
                // Apply on=delete, off=set 0. Enabled={1,absent}, Disabled={0}. IsPreference=false.
                new SettingDefinition
                {
                    Id = "updates-latest-updates",
                    Icon = "BullhornVariant",
                    Name = "Get Latest Updates",
                    Description = "Receives the latest updates as soon as they are available, before the standard rollout.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "IsContinuousInnovationOptedIn",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // source: UX\Settings AllowMUUpdateService — ReadState absent=off, v==1=on;
                // Apply always writes 1/0. Enabled={1}, Disabled={0}. IsPreference=TRUE in source
                // (spec text said IsSubjectivePreference false; source IsPreference is ground truth).
                new SettingDefinition
                {
                    Id = "updates-other-products",
                    Icon = "ArchiveSync",
                    Name = "Updates for Other Microsoft Products",
                    Description = "Receives updates for other Microsoft products alongside Windows Update.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "AllowMUUpdateService",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // source: UX\Settings IsExpedited — ReadState absent=on, v==1=on; Apply always
                // writes 1/0. Source comment: Enabled=[1], Disabled=[0], DefaultValue=1. IsPreference=false.
                new SettingDefinition
                {
                    Id = "updates-restart-asap",
                    Icon = "Restart",
                    Name = "Restart as Soon as Possible",
                    Description = "Restarts the device as soon as possible to finish installing updates, even during active hours.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "IsExpedited",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // source: ...WindowsUpdate\AU NoAutoRebootWithLoggedOnUsers — ReadState absent=on,
                // v!=1=on; Apply on=delete, off=set 1. Enabled={absent}, Disabled={1}. IsPreference=false.
                new SettingDefinition
                {
                    Id = "updates-restart-options",
                    Icon = "RestartOff",
                    Name = "Managed Restart Options",
                    Description = "Allows users to configure restart options for Windows Update.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                            ValueName = "NoAutoRebootWithLoggedOnUsers",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // source: ...WindowsUpdate SetUpdateNotificationLevel — Apply on=delete, off=set 2;
                // absent = notifications on (default). Enabled={absent}, Disabled={2}. IsPreference=TRUE.
                // NOTE: the source ReadState (v==2 -> on) and its inline comment ("EnabledValue=[2],
                // DisabledValue=[null]") contradict the Apply write-path and Windows policy semantics
                // (2 = suppress notifications). The Apply path is authoritative and is what is preserved.
                new SettingDefinition
                {
                    Id = "updates-notification-level",
                    Icon = "BellPlus",
                    Name = "Update Notifications",
                    Description = "Shows notifications about Windows Update activity including restart prompts.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "SetUpdateNotificationLevel",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 2 },
                        },
                    },
                },
                // source: UX\Settings RestartNotificationsAllowed2 — ReadState absent=off, v==1=on;
                // Apply always writes 1/0. Enabled={1}, Disabled={0}. IsPreference=TRUE, DefaultState=false.
                new SettingDefinition
                {
                    Id = "updates-restart-notification",
                    Icon = "RestartAlert",
                    Name = "Restart Notification",
                    Description = "Shows a notification before restarting to complete update installation.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "RestartNotificationsAllowed2",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // source: UX\Settings AllowAutoWindowsUpdateDownloadOverMeteredNetwork — ReadState
                // absent=on, v==1=on; Apply always writes 1/0. Enabled={1}, Disabled={0}, default on.
                // IsPreference=false.
                new SettingDefinition
                {
                    Id = "updates-metered-connection",
                    Icon = "Connection",
                    Name = "Updates on Metered Connections",
                    Description = "Allows Windows Update to download updates on metered network connections.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                            ValueName = "AllowAutoWindowsUpdateDownloadOverMeteredNetwork",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // source: ...WindowsUpdate ExcludeWUDriversInQualityUpdate — ReadState absent=on,
                // v!=1=on; Apply on=delete, off=set 1. Enabled={absent}, Disabled={1}. IsPreference=TRUE.
                new SettingDefinition
                {
                    Id = "updates-driver-controls",
                    Icon = "PackageVariantClosedMinus",
                    Name = "Driver Update Controls",
                    Description = "Allows Windows to automatically download and install driver updates through Windows Update.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                            ValueName = "ExcludeWUDriversInQualityUpdate",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { null },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // source: ...CurrentVersion\Device Installer DisableCoInstallers — ReadState absent=on,
                // v!=1=on; Apply on=delete, off=set 1. Enabled={absent}, Disabled={1}. IsPreference=TRUE.
                new SettingDefinition
                {
                    Id = "updates-driver-coinstallers",
                    Icon = "PackageVariantRemove",
                    Name = "Driver Co-installers",
                    Description = "Allows Windows to install driver co-installers and extension INFs from Windows Update.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Device Installer",
                            ValueName = "DisableCoInstallers",
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
    };
}
