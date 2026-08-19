using System;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using FluentAssertions;
using Xunit;

namespace AkariTool.Core.Tests.Features;

public class SettingDefinitionToggleStateTests
{
    private static SettingDefinition Setting(params RegistrySetting[] registry) =>
        new SettingDefinition
        {
            Id = "id",
            Name = "name",
            Description = "desc",
            RegistrySettings = registry,
        };

    [Fact]
    public void GetPrimaryRegistrySetting_ReturnsPrimaryFlaggedEntry()
    {
        var first = new RegistrySetting
        {
            KeyPath = "HKLM\\A",
            RecommendedValue = 1,
            DefaultValue = 0,
            ValueType = RegistryValueKind.DWord,
        };
        var primary = new RegistrySetting
        {
            KeyPath = "HKLM\\B",
            RecommendedValue = 1,
            DefaultValue = 0,
            ValueType = RegistryValueKind.DWord,
            IsPrimary = true,
        };
        var setting = Setting(first, primary);

        SettingDefinitionToggleState.GetPrimaryRegistrySetting(setting).Should().Be(primary);
    }

    [Fact]
    public void GetPrimaryRegistrySetting_FallsBackToFirst_WhenNoPrimary()
    {
        var first = new RegistrySetting
        {
            KeyPath = "HKLM\\A",
            RecommendedValue = 1,
            DefaultValue = 0,
            ValueType = RegistryValueKind.DWord,
        };
        var second = new RegistrySetting
        {
            KeyPath = "HKLM\\B",
            RecommendedValue = 1,
            DefaultValue = 0,
            ValueType = RegistryValueKind.DWord,
        };
        var setting = Setting(first, second);

        SettingDefinitionToggleState.GetPrimaryRegistrySetting(setting).Should().Be(first);
    }

    [Fact]
    public void GetPrimaryRegistrySetting_ReturnsNull_WhenNoRegistryEntries()
    {
        SettingDefinitionToggleState.GetPrimaryRegistrySetting(Setting()).Should().BeNull();
    }

    [Fact]
    public void GetRecommendedToggleState_ReturnsExplicitOverride()
    {
        var reg = new RegistrySetting
        {
            KeyPath = "HKLM\\A",
            RecommendedValue = 1,
            DefaultValue = 0,
            EnabledValue = new object?[] { 1 },
            ValueType = RegistryValueKind.DWord,
        };
        var setting = new SettingDefinition
        {
            Id = "id",
            Name = "name",
            Description = "desc",
            RegistrySettings = new[] { reg },
            RecommendedToggleState = false,
        };

        SettingDefinitionToggleState.GetRecommendedToggleState(setting).Should().Be(false);
    }

    [Fact]
    public void GetRecommendedToggleState_DerivesTrue_FromEnabledValue()
    {
        var reg = new RegistrySetting
        {
            KeyPath = "HKLM\\A",
            RecommendedValue = 1,
            DefaultValue = 0,
            EnabledValue = new object?[] { 1 },
            ValueType = RegistryValueKind.DWord,
        };

        SettingDefinitionToggleState.GetRecommendedToggleState(Setting(reg)).Should().Be(true);
    }

    [Fact]
    public void GetRecommendedToggleState_ReturnsNull_WhenNoData()
    {
        SettingDefinitionToggleState.GetRecommendedToggleState(Setting()).Should().BeNull();
    }

    [Fact]
    public void GetDefaultToggleState_DerivesFalse_FromDisabledValue()
    {
        var reg = new RegistrySetting
        {
            KeyPath = "HKLM\\A",
            RecommendedValue = 1,
            DefaultValue = 0,
            DisabledValue = new object?[] { 0 },
            ValueType = RegistryValueKind.DWord,
        };

        SettingDefinitionToggleState.GetDefaultToggleState(Setting(reg)).Should().Be(false);
    }

    [Fact]
    public void GetDefaultToggleState_TreatsNullDefault_AsTrue_WhenEnabledValueContainsNull()
    {
        var reg = new RegistrySetting
        {
            KeyPath = "HKLM\\A",
            RecommendedValue = 1,
            DefaultValue = null,
            EnabledValue = new object?[] { null },
            ValueType = RegistryValueKind.DWord,
        };

        SettingDefinitionToggleState.GetDefaultToggleState(Setting(reg)).Should().Be(true);
    }

    [Fact]
    public void IsKeyExistenceToggle_ReturnsTrue_ForKeyExistencePattern()
    {
        var reg = new RegistrySetting
        {
            KeyPath = "HKLM\\A",
            ValueName = null,
            RecommendedValue = null,
            DefaultValue = null,
            EnabledValue = null,
            DisabledValue = null,
            ValueType = RegistryValueKind.None,
        };

        SettingDefinitionToggleState.IsKeyExistenceToggle(reg).Should().BeTrue();
    }

    [Fact]
    public void IsKeyExistenceToggle_ReturnsFalse_WhenValueNameSet()
    {
        var reg = new RegistrySetting
        {
            KeyPath = "HKLM\\A",
            ValueName = "Foo",
            RecommendedValue = null,
            DefaultValue = null,
            ValueType = RegistryValueKind.None,
        };

        SettingDefinitionToggleState.IsKeyExistenceToggle(reg).Should().BeFalse();
    }
}
