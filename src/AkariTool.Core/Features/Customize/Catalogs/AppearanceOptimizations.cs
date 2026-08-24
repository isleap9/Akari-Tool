using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class AppearanceOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildTheme(),
        .. BuildEffects(),
        .. BuildColor(),
        .. BuildWindowStyle(),
    ];

    private static IReadOnlyList<SettingGroup> BuildTheme() =>
    [
        new SettingGroup
        {
            Name = "Theme",
            FeatureId = "customize-appearance-theme",
            Settings = new[]
            {
                // customize-appearance-dark-mode-apps — AppsUseLightTheme (inverted):
                //   dark (on) = 0 / light (off) = 1. Reader: AppsUseLightTheme == 0 → enabled.
                new SettingDefinition
                {
                    Id = "customize-appearance-dark-mode-apps",
                    Icon = "ThemeLightDark",
                    Name = "Dark Mode (Apps)",
                    Description = "Switches Windows apps to dark mode",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                            ValueName = "AppsUseLightTheme",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 0 },
                            DisabledValue = new object?[] { 1 },
                        },
                    },
                },
                // customize-appearance-dark-mode-system — SystemUsesLightTheme (inverted):
                //   dark (on) = 0 / light (off) = 1. Reader: SystemUsesLightTheme == 0 → enabled.
                new SettingDefinition
                {
                    Id = "customize-appearance-dark-mode-system",
                    Icon = "Brightness4",
                    Name = "Dark Mode (System)",
                    Description = "Switches the Windows shell, taskbar, and Start Menu to dark mode",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                            ValueName = "SystemUsesLightTheme",
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

    private static IReadOnlyList<SettingGroup> BuildEffects() =>
    [
        new SettingGroup
        {
            Name = "Transparency & Effects",
            FeatureId = "customize-appearance-effects",
            Settings = new[]
            {
                // customize-appearance-transparency — EnableTransparency: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-appearance-transparency",
                    Icon = "Opacity",
                    Name = "Transparency Effects",
                    Description = "Enables transparency and blur effects on the taskbar, Start Menu, and Action Center",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                            ValueName = "EnableTransparency",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-appearance-oled-taskbar-transparency — UseOLEDTaskbarTransparency: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-appearance-oled-taskbar-transparency",
                    Icon = "CircleOpacity",
                    Name = "OLED Taskbar Transparency",
                    Description = "Enables the deeper OLED-style taskbar transparency (requires Transparency Effects on)",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "UseOLEDTaskbarTransparency",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-appearance-dwm-effect-mode — ForceEffectMode (non-1 enabled value):
                //   forced (on) = 2 / default (off) = 0. Reader: ForceEffectMode == 2 → enabled.
                new SettingDefinition
                {
                    Id = "customize-appearance-dwm-effect-mode",
                    Icon = "Blur",
                    Name = "Mica/Acrylic Effects (DWM)",
                    Description = "Forces DWM effect mode — enables Mica and Acrylic material effects on supported apps",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm",
                            ValueName = "ForceEffectMode",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 2 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
            },
        },
    ];

    private static IReadOnlyList<SettingGroup> BuildColor() =>
    [
        new SettingGroup
        {
            Name = "Color",
            FeatureId = "customize-appearance-color",
            Settings = new[]
            {
                // customize-appearance-accent-title-bars — DWM ColorPrevalence: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-appearance-accent-title-bars",
                    Icon = "BorderColor",
                    Name = "Accent Color on Title Bars & Borders",
                    Description = "Shows your accent color on window title bars and borders instead of white/black",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                            ValueName = "ColorPrevalence",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-appearance-accent-taskbar-start — Personalize ColorPrevalence: on=1/off=0.
                new SettingDefinition
                {
                    Id = "customize-appearance-accent-taskbar-start",
                    Icon = "Palette",
                    Name = "Accent Color on Taskbar & Start",
                    Description = "Shows your accent color on the taskbar and Start Menu",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                            ValueName = "ColorPrevalence",
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

    private static IReadOnlyList<SettingGroup> BuildWindowStyle() =>
    [
        new SettingGroup
        {
            Name = "Window Style",
            FeatureId = "customize-appearance-window-style",
            Settings = new[]
            {
                // customize-appearance-square-window-corners — DisableRoundedCorners:
                //   square (on) = 1 / rounded (off) = 0. Reader: DisableRoundedCorners == 1 → enabled.
                //   (The source also writes UseWindowDarkMode = 0 in BOTH branches — a documented
                //    dwmapi placeholder that carries no state, so it is not modelled as a setting.)
                new SettingDefinition
                {
                    Id = "customize-appearance-square-window-corners",
                    Icon = "SquareOutline",
                    Name = "Square Window Corners",
                    Description = "Disables rounded window corners — restores the sharp square corners from Windows 10",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    DefaultToggleState = false,
                    RequiresRestart = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DWM",
                            ValueName = "DisableRoundedCorners",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { 1 },
                            DisabledValue = new object?[] { 0 },
                        },
                    },
                },
                // customize-appearance-drag-full-windows — DragFullWindows (REG_SZ): on="1"/off="0".
                new SettingDefinition
                {
                    Id = "customize-appearance-drag-full-windows",
                    Icon = "DragVariant",
                    Name = "Show Window Contents While Dragging",
                    Description = "Displays live window contents when dragging — disabling shows only an outline for lower CPU use",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = true,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "DragFullWindows",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "1" },
                            DisabledValue = new object?[] { "0" },
                        },
                    },
                },
                // customize-appearance-animate-windows — MinAnimate (REG_SZ): on="1"/off="0".
                new SettingDefinition
                {
                    Id = "customize-appearance-animate-windows",
                    Icon = "Animation",
                    Name = "Animate Windows on Open/Close",
                    Description = "Enables the open/minimize/maximize animations for windows",
                    InputType = InputType.Toggle,
                    IsSubjectivePreference = false,
                    RecommendedToggleState = false,
                    DefaultToggleState = true,
                    RegistrySettings = new[]
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics",
                            ValueName = "MinAnimate",
                            ValueType = RegistryValueKind.String,
                            RecommendedValue = null,
                            DefaultValue = null,
                            EnabledValue = new object?[] { "1" },
                            DisabledValue = new object?[] { "0" },
                        },
                    },
                },
            },
        },
    ];
}
