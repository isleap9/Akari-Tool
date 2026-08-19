using AkariTool.Core.Features.Common.Constants;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

public class SettingStateReaderTests
{
    private static SettingDefinition MakePowerSelectionSetting()
    {
        return new SettingDefinition
        {
            Id = "power-test",
            Name = "Test",
            Description = "Desc",
            InputType = InputType.Selection,
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SubgroupGuid = "0012ee47-9041-4b5d-9b77-535fba8b1442",
                    SettingGuid = "6738e2c4-e8a5-4a42-b16a-e040e769756e",
                    PowerModeSupport = PowerModeSupport.Both,
                    RecommendedValueAC = 0,
                    RecommendedValueDC = 0,
                    DefaultValueAC = 0,
                    DefaultValueDC = 0,
                },
            },
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new ComboBoxOption { DisplayName = "Balanced", ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 }, IsDefault = true },
                    new ComboBoxOption { DisplayName = "High performance", ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 }, IsRecommended = true },
                    new ComboBoxOption { DisplayName = "Max performance", ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 } },
                },
            },
        };
    }

    private static SettingStateReader MakeReader(IPowerSettingsQueryService queryService) =>
        new(queryService);

    [Fact]
    public void ReadSelectionIndex_PowerCfgValueMatchesOption_ReturnsThatIndex()
    {
        var queryService = Substitute.For<IPowerSettingsQueryService>();
        queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
            .Returns(Task.FromResult(((int?)1, (int?)1)));

        var reader = MakeReader(queryService);

        reader.ReadSelectionIndex(MakePowerSelectionSetting()).Should().Be(1);
    }

    [Fact]
    public void ReadSelectionIndex_PowerCfgValueUnmatched_ReturnsCustom()
    {
        var queryService = Substitute.For<IPowerSettingsQueryService>();
        queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
            .Returns(Task.FromResult(((int?)9, (int?)9)));

        var reader = MakeReader(queryService);

        reader.ReadSelectionIndex(MakePowerSelectionSetting()).Should().Be(ComboBoxConstants.CustomStateIndex);
    }

    [Fact]
    public void ReadSelectionIndex_PowerCfgQueryFails_FallsBackToIsDefault()
    {
        var queryService = Substitute.For<IPowerSettingsQueryService>();
        queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
            .Returns(Task.FromResult(((int?)null, (int?)null)));

        var reader = MakeReader(queryService);

        reader.ReadSelectionIndex(MakePowerSelectionSetting()).Should().Be(0);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void ReadToggleState_PowerCfgOnly_NonZeroAcValue_IsEnabled(int acValue, bool expected)
    {
        var queryService = Substitute.For<IPowerSettingsQueryService>();
        queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
            .Returns(Task.FromResult(((int?)acValue, (int?)acValue)));

        var reader = MakeReader(queryService);

        var toggleSetting = new SettingDefinition
        {
            Id = "power-toggle",
            Name = "Test",
            Description = "Desc",
            InputType = InputType.Toggle,
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SubgroupGuid = "0012ee47-9041-4b5d-9b77-535fba8b1442",
                    SettingGuid = "6738e2c4-e8a5-4a42-b16a-e040e769756e",
                    PowerModeSupport = PowerModeSupport.Both,
                    RecommendedValueAC = 0,
                    RecommendedValueDC = 0,
                    DefaultValueAC = 0,
                    DefaultValueDC = 0,
                },
            },
        };

        reader.ReadToggleState(toggleSetting).Should().Be(expected);
    }
}