using System;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using FluentAssertions;
using Xunit;

namespace AkariTool.Core.Tests.Features;

public class SettingDefinitionModelTests
{
    [Fact]
    public void OperationResult_Succeeded_HasSuccessTrue()
    {
        var result = OperationResult.Succeeded();
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void OperationResult_Failed_HasSuccessFalse()
    {
        var result = OperationResult.Failed("err");
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("err");
    }

    [Fact]
    public void SettingGroup_RoundTrips_RequiredFields()
    {
        var group = new SettingGroup
        {
            Name = "G",
            FeatureId = "f",
            Settings = Array.Empty<SettingDefinition>(),
        };
        group.Name.Should().Be("G");
        group.FeatureId.Should().Be("f");
        group.Settings.Should().BeEmpty();
    }

    [Fact]
    public void SettingDefinition_Defaults_AreCorrect()
    {
        var setting = new SettingDefinition
        {
            Id = "id",
            Name = "name",
            Description = "desc",
            RegistrySettings = Array.Empty<RegistrySetting>(),
        };
        setting.InputType.Should().Be(InputType.Toggle);
        setting.IsSubjectivePreference.Should().BeFalse();
        setting.RecommendedToggleState.Should().BeNull();
    }
}
