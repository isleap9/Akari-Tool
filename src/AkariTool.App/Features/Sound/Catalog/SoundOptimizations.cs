using System.Collections.Generic;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;

namespace AkariTool.Tabs.Sound;

/// <summary>
/// Declarative Sound catalog — the SettingDefinition replacement for the old
/// delegate-based SoundTweaks.SystemSounds(). One group ("System Sounds"), seven settings,
/// IDs preserved byte-for-byte for backup compatibility.
/// </summary>
public static class SoundOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() => new[]
    {
        new SettingGroup
        {
            Name = "System Sounds",
            FeatureId = "sound",
            Settings = new[]
            {
                // 1 ── Startup sound during boot (inverted: reg stores *Disable*StartupSound)
                new SettingDefinition
                {
                    Id = "sound-startup",
                    Name = "Startup Sound During Boot",
                    Description = "Plays a sound when Windows starts. Disable to silence the boot chime.",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation",
                            ValueName = "DisableStartupSound",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\EditionOverrides",
                            ValueName = "UserSetting_DisableStartupSound",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },

                // 2 ── Sound ducking preference (Selection). Values match Windows
                // UserDuckingPreference semantics: 0=mute, 1=80% (default), 2=50%, 3=nothing.
                new SettingDefinition
                {
                    Id = "sound-ducking",
                    Name = "Sound Ducking Preference",
                    Description = "Controls how Windows lowers the volume of other sounds when communication activity is detected.",
                    InputType = InputType.Selection,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Multimedia\Audio",
                            ValueName = "UserDuckingPreference",
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
                                DisplayName = "Do Nothing",
                                IsRecommended = true,
                                ValueMappings = new Dictionary<string, object?> { ["UserDuckingPreference"] = 3 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Mute all other sounds",
                                ValueMappings = new Dictionary<string, object?> { ["UserDuckingPreference"] = 0 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Reduce volume by 80%",
                                IsDefault = true,
                                ValueMappings = new Dictionary<string, object?> { ["UserDuckingPreference"] = 1 },
                            },
                            new ComboBoxOption
                            {
                                DisplayName = "Reduce volume by 50%",
                                ValueMappings = new Dictionary<string, object?> { ["UserDuckingPreference"] = 2 },
                            },
                        },
                    },
                },

                // 3 ── Narrator audio ducking
                new SettingDefinition
                {
                    Id = "sound-narrator-ducking",
                    Name = "Narrator Audio Ducking",
                    Description = "Reduces the volume of other audio when Narrator is speaking.",
                    InputType = InputType.Toggle,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Narrator\NoRoam",
                            ValueName = "DuckAudio",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },

                // 4 ── Voice activation for apps
                new SettingDefinition
                {
                    Id = "sound-voice-activation",
                    Name = "Voice Activation for Apps",
                    Description = "Allow apps to listen and respond to voice commands like \"Hey Cortana\"",
                    InputType = InputType.Toggle,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings",
                            ValueName = "AgentActivationEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                    },
                },

                // 5 ── Last used voice activation setting
                new SettingDefinition
                {
                    Id = "sound-voice-activation-last-used",
                    Name = "Last Used Voice Activation Setting",
                    Description = "Remember and apply the most recently used voice activation configuration",
                    InputType = InputType.Toggle,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings",
                            ValueName = "AgentActivationLastUsed",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                    },
                },

                // 6 ── Accessibility activation sounds
                new SettingDefinition
                {
                    Id = "sound-accessibility-activation",
                    Name = "Accessibility Activation Sounds",
                    Description = "Play sounds when accessibility features like StickyKeys or FilterKeys are activated",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility",
                            ValueName = "Sound on Activation",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                    },
                },

                // 7 ── Accessibility warning sounds
                new SettingDefinition
                {
                    Id = "sound-accessibility-warnings",
                    Name = "Accessibility Warning Sounds",
                    Description = "Play warning sounds when attempting to activate accessibility features or when accessibility-related events occur",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = true,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility",
                            ValueName = "Warning Sounds",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1, null },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                    },
                },
            },
        },
    };
}
