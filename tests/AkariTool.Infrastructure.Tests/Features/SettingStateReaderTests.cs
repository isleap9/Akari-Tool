using AkariTool.Core.Features.Common.Constants;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Services;
using FluentAssertions;
using Microsoft.Win32;
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

    private static SettingStateReader MakeReader(IPowerSettingsQueryService queryService)
    {
        var specialHandlerRegistry = Substitute.For<ISpecialSettingHandlerRegistry>();
        specialHandlerRegistry.TryGet(Arg.Any<string>()).Returns((ISpecialSettingHandler?)null);
        return new(queryService, specialHandlerRegistry);
    }

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

    private static SettingDefinition MakePowerNumericSetting(PowerModeSupport mode, string? units = "Minutes")
    {
        return new SettingDefinition
        {
            Id = "power-numeric",
            Name = "Test",
            Description = "Desc",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 100, Units = units },
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SubgroupGuid = "0012ee47-9041-4b5d-9b77-535fba8b1442",
                    SettingGuid = "6738e2c4-e8a5-4a42-b16a-e040e769756e",
                    PowerModeSupport = mode,
                    Units = units,
                    RecommendedValueAC = 0,
                    RecommendedValueDC = 0,
                    DefaultValueAC = 0,
                    DefaultValueDC = 0,
                },
            },
        };
    }

    [Fact]
    public void ReadNumericValue_Separate_ConvertsSystemToDisplayUnits_ForBothSides()
    {
        var queryService = Substitute.For<IPowerSettingsQueryService>();
        queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
            .Returns(Task.FromResult(((int?)1200, (int?)600))); // seconds

        var reader = MakeReader(queryService);

        var (ac, dc) = reader.ReadNumericValue(MakePowerNumericSetting(PowerModeSupport.Separate));

        ac.Should().Be(20);   // 1200s / 60
        dc.Should().Be(10);   // 600s / 60
    }

    [Fact]
    public void ReadNumericValue_NonSeparate_ReturnsSingleValueInAc()
    {
        var queryService = Substitute.For<IPowerSettingsQueryService>();
        queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
            .Returns(Task.FromResult(((int?)1200, (int?)1200)));

        var reader = MakeReader(queryService);

        var (ac, dc) = reader.ReadNumericValue(MakePowerNumericSetting(PowerModeSupport.Both));

        ac.Should().Be(20);
        dc.Should().Be(20); // reader returns both; the VM uses acValue for non-Separate
    }

    [Fact]
    public void ReadNumericValue_NoPowerCfg_ReturnsNulls()
    {
        var reader = MakeReader(Substitute.For<IPowerSettingsQueryService>());

        var setting = new SettingDefinition
        {
            Id = "reg-numeric",
            Name = "Test",
            Description = "Desc",
            InputType = InputType.NumericRange,
        };

        var (ac, dc) = reader.ReadNumericValue(setting);

        ac.Should().BeNull();
        dc.Should().BeNull();
    }

    [Fact]
    public void ReadNumericValue_Milliseconds_IsOneToOne()
    {
        var queryService = Substitute.For<IPowerSettingsQueryService>();
        queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
            .Returns(Task.FromResult(((int?)1000, (int?)1000)));

        var reader = MakeReader(queryService);

        var (ac, dc) = reader.ReadNumericValue(MakePowerNumericSetting(PowerModeSupport.Separate, "Milliseconds"));

        ac.Should().Be(1000);
        dc.Should().Be(1000);
    }

    // ── Composite REG_SZ toggle state (Winhance IsRegistryValueInEnabledState parity) ──

    private const string DirectXComposite = "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;AutoHDREnable=0;";

    private static RegistrySetting MakeDirectXSetting(string compositeKey, string defaultValue) => new()
    {
        KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
        ValueName = "DirectXUserGlobalSettings",
        RecommendedValue = "1",
        DefaultValue = defaultValue,
        EnabledValue = new object?[] { "1", null },
        DisabledValue = new object?[] { "0" },
        ValueType = RegistryValueKind.String,
        CompositeStringKey = compositeKey,
        IsPrimary = true,
    };

    [Theory]
    [InlineData("SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;", true)]   // applied recommended
    [InlineData("SwapEffectUpgradeEnable=0;VRROptimizeEnable=0;", false)]  // applied disabled
    [InlineData("VRROptimizeEnable=0;AutoHDREnable=1;", true)]             // flip-model default-on implied
    public void ResolveCompositeState_FlipModel_MatchesWinhanceSemantics(string raw, bool expected)
    {
        // DefaultToggleState = true → DefaultValue "1"; absent sub-key implies enabled.
        var setting = MakeDirectXSetting("SwapEffectUpgradeEnable", "1");

        SettingStateReader.ResolveCompositeState(setting, raw).Should().Be(expected);
    }

    [Fact]
    public void ResolveCompositeState_SubKeyAbsent_DefaultOff_ImpliesDisabled()
    {
        // Auto HDR: DefaultValue "0" vs Enabled "1" → absent sub-key reads disabled.
        var setting = MakeDirectXSetting("AutoHDREnable", "0");

        SettingStateReader.ResolveCompositeState(setting, DirectXComposite).Should().BeFalse();
    }

    [Fact]
    public void ResolveCompositeState_ValueAbsent_ResolvesViaDefaultValue()
    {
        var setting = MakeDirectXSetting("SwapEffectUpgradeEnable", "1");

        SettingStateReader.ResolveCompositeState(setting, null).Should().BeTrue();
        SettingStateReader.ResolveCompositeState(setting, "").Should().BeTrue();
    }

    [Fact]
    public void ResolveCompositeState_ValueAbsent_DefaultDiffersFromEnabled_IsDisabled()
    {
        var setting = MakeDirectXSetting("AutoHDREnable", "0");

        SettingStateReader.ResolveCompositeState(setting, null).Should().BeFalse();
    }

    [Fact]
    public void ResolveCompositeState_KeyMatchIsCaseInsensitive()
    {
        var setting = MakeDirectXSetting("SwapEffectUpgradeEnable", "1");

        SettingStateReader.ResolveCompositeState(setting, "swapeffectupgradeenable=1;").Should().BeTrue();
    }
}