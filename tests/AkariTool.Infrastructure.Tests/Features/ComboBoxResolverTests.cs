using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Services;
using FluentAssertions;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

public class ComboBoxResolverTests
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
                    new ComboBoxOption { DisplayName = "Balanced", ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 0 } },
                    new ComboBoxOption { DisplayName = "High performance", ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 1 } },
                    new ComboBoxOption { DisplayName = "Max performance", ValueMappings = new Dictionary<string, object?> { ["PowerCfgValue"] = 2 } },
                },
            },
        };
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void GetValueFromIndex_PowerCfgSetting_ReturnsMappedPowerCfgValue(int index, int expected)
    {
        var resolver = new ComboBoxResolver();
        resolver.GetValueFromIndex(MakePowerSelectionSetting(), index).Should().Be(expected);
    }

    [Fact]
    public void GetValueFromIndex_PowerCfgSetting_NotMappedIndex_FallsBackToIndex()
    {
        var resolver = new ComboBoxResolver();
        resolver.GetValueFromIndex(MakePowerSelectionSetting(), 7).Should().Be(7);
    }

    [Fact]
    public void GetValueFromIndex_CustomState_ReturnsZero()
    {
        var resolver = new ComboBoxResolver();
        resolver.GetValueFromIndex(MakePowerSelectionSetting(), AkariTool.Core.Features.Common.Constants.ComboBoxConstants.CustomStateIndex).Should().Be(0);
    }
}