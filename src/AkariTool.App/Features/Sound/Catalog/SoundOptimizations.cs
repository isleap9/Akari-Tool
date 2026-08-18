using System.Collections.Generic;
using Microsoft.Win32;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;

namespace AkariTool.Tabs.Sound;

/// <summary>
/// Declarative Sound catalog — the SettingDefinition replacement for the old
/// delegate-based SoundTweaks.SystemSounds(). One group ("System Sounds"), five settings,
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

                // 4 ── Voice activation for apps (writes two values under one key)
                new SettingDefinition
                {
                    Id = "sound-voice-activation",
                    Name = "Voice Activation for Apps",
                    Description = "Allows apps to activate using voice commands even when the device is locked or the app is in the background.",
                    InputType = InputType.Toggle,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps",
                            ValueName = "AgentActivationEnabled",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps",
                            ValueName = "AgentActivationLastUsed",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = 1,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },

                // 5 ── Accessibility activation & warning sounds (writes two values under one key)
                new SettingDefinition
                {
                    Id = "sound-accessibility",
                    Name = "Accessibility Activation & Warning Sounds",
                    Description = "Plays sounds for accessibility events such as sticky keys and other accessibility feature activations.",
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
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                            IsPrimary = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility",
                            ValueName = "Warning Sounds",
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
    };
}
