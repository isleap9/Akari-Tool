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

    // ── Selection unmatched fallbacks (Winhance ResolveRawValuesToIndex parity, 4h-era fix) ──
    //
    // Registry-path tests point at a syntactically valid but NONEXISTENT key: TryOpenSubkey
    // parses the hive prefix and returns a null subkey, so no real registry value is read
    // (repo rule: no real-registry tests). The absent-read path then exercises the
    // DefaultValue substitution + pristine/ResolveUnmatchedToDefault fallbacks purely.

    private static SettingDefinition MakeRegistrySelection(
        bool resolveUnmatchedToDefault,
        bool declareDefaultValue,
        int? substitutedValue = null)
    {
        return new SettingDefinition
        {
            Id = "reg-selection",
            Name = "Test",
            Description = "Desc",
            InputType = InputType.Selection,
            ResolveUnmatchedToDefault = resolveUnmatchedToDefault,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\AkariTool\Tests\Nonexistent_SelectionKey",
                    ValueName = "TestValue",
                    RecommendedValue = 2,
                    DefaultValue = declareDefaultValue ? 1 : substitutedValue,
                    ValueType = RegistryValueKind.DWord,
                },
            },
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new ComboBoxOption { DisplayName = "Default", IsDefault = true,
                        ValueMappings = new Dictionary<string, object?> { ["TestValue"] = 1 } },
                    new ComboBoxOption { DisplayName = "Recommended", IsRecommended = true,
                        ValueMappings = new Dictionary<string, object?> { ["TestValue"] = 2 } },
                    new ComboBoxOption { DisplayName = "Aggressive",
                        ValueMappings = new Dictionary<string, object?> { ["TestValue"] = 3 } },
                },
            },
        };
    }

    [Fact]
    public void ReadSelectionIndex_AbsentValue_SubstitutesDeclaredDefault_MatchesDefaultOption()
    {
        // Value absent but DefaultValue=1 declared: Winhance substitutes it before
        // matching, so the default option's mapping resolves instead of Custom.
        var reader = MakeReader(Substitute.For<IPowerSettingsQueryService>());

        reader.ReadSelectionIndex(MakeRegistrySelection(resolveUnmatchedToDefault: false, declareDefaultValue: true))
            .Should().Be(0);
    }

    [Fact]
    public void ReadSelectionIndex_PristineSystem_NoDefaultValue_FallsBackToIsDefault()
    {
        // No live value and no declared DefaultValue → pristine → IsDefault option.
        var reader = MakeReader(Substitute.For<IPowerSettingsQueryService>());

        reader.ReadSelectionIndex(MakeRegistrySelection(resolveUnmatchedToDefault: false, declareDefaultValue: false))
            .Should().Be(0);
    }

    [Fact]
    public void ReadSelectionIndex_ResolveUnmatchedToDefault_UnmatchedResolvesToDefault()
    {
        // A substituted-but-unmatched state (DefaultValue=9 maps to no option) with the
        // opt-in flag set resolves to the IsDefault option instead of Custom.
        var reader = MakeReader(Substitute.For<IPowerSettingsQueryService>());

        reader.ReadSelectionIndex(MakeRegistrySelection(resolveUnmatchedToDefault: true, declareDefaultValue: false, substitutedValue: 9))
            .Should().Be(0);
    }

    [Fact]
    public void ReadSelectionIndex_NoOptIn_NonPristineUnmatched_StaysCustom()
    {
        // Substituted value matches no option and no ResolveUnmatchedToDefault opt-in:
        // non-pristine unmatched stays Custom (-1) — the badge pipeline's Custom pill case.
        var reader = MakeReader(Substitute.For<IPowerSettingsQueryService>());

        reader.ReadSelectionIndex(MakeRegistrySelection(resolveUnmatchedToDefault: false, declareDefaultValue: false, substitutedValue: 9))
            .Should().Be(ComboBoxConstants.CustomStateIndex);
    }

    [Fact]
    public void ReadSelectionIndex_PowerCfgOnly_ResolveUnmatchedToDefault_FallsBackToIsDefault()
    {
        var queryService = Substitute.For<IPowerSettingsQueryService>();
        queryService.GetPowerSettingACDCValuesAsync(Arg.Any<PowerCfgSetting>())
            .Returns(Task.FromResult(((int?)9, (int?)9))); // matches no option

        var reader = MakeReader(queryService);

        var setting = MakePowerSelectionSetting() with { ResolveUnmatchedToDefault = true };
        reader.ReadSelectionIndex(setting).Should().Be(0);
    }

    // ── DNS Server detection (Winhance DetectDnsServerIndex parity — pure core) ──

    private static SettingDefinition MakeDnsSetting() => new()
    {
        Id = "gaming-dns-server",
        Name = "DNS Server",
        Description = "Desc",
        InputType = InputType.Selection,
        DetectionType = DetectionType.DnsServer,
        ComboBox = new ComboBoxMetadata
        {
            Options = new[]
            {
                new ComboBoxOption { DisplayName = "Automatic", IsDefault = true, IsRecommended = true,
                    ScriptVariables = new Dictionary<string, string> { ["primary"] = "" } },
                new ComboBoxOption { DisplayName = "Cloudflare",
                    ScriptVariables = new Dictionary<string, string> { ["primary"] = "1.1.1.1" } },
                new ComboBoxOption { DisplayName = "Google",
                    ScriptVariables = new Dictionary<string, string> { ["primary"] = "8.8.8.8" } },
            },
        },
    };

    [Theory]
    [InlineData(null, "1.2.3.4", 0)]   // NameServer empty/absent = DHCP → Automatic
    [InlineData("", "1.2.3.4", 0)]
    [InlineData("1.1.1.1,8.8.4.4", "1.1.1.1", 1)]  // manual, primary matched via ScriptVariables
    [InlineData("8.8.8.8,8.8.4.4", "8.8.8.8", 2)]
    [InlineData("203.0.113.7", "203.0.113.7", -1)] // manual but unknown server → Custom
    public void ResolveDnsServerIndex_MatchesWinhanceSemantics(string? nameServer, string? primaryDns, int expected)
    {
        SettingStateReader.ResolveDnsServerIndex(MakeDnsSetting(), nameServer, primaryDns)
            .Should().Be(expected);
    }

    // ── REG_BINARY full-array comparison (shortcut-suffix toggle) ──────────────
    // Regression: two distinct byte[] instances used to fall through ValuesEqual to
    // ToString() ("System.Byte[]" == "System.Byte[]") and always match, so a full-array
    // binary toggle read as enabled regardless of its real value.

    [Fact]
    public void ValuesEqual_ByteArrays_SameContent_ReturnsTrue()
    {
        SettingStateReader.ValuesEqual(
            new byte[] { 0x00, 0x00, 0x00, 0x00 },
            new byte[] { 0x00, 0x00, 0x00, 0x00 }).Should().BeTrue();
    }

    [Fact]
    public void ValuesEqual_ByteArrays_DifferentContent_ReturnsFalse()
    {
        // enabled (00 00 00 00) vs disabled (1E 00 00 00) must NOT match
        SettingStateReader.ValuesEqual(
            new byte[] { 0x1E, 0x00, 0x00, 0x00 },
            new byte[] { 0x00, 0x00, 0x00, 0x00 }).Should().BeFalse();
    }

    [Fact]
    public void ValuesEqual_ByteArrays_DifferentLength_ReturnsFalse()
    {
        SettingStateReader.ValuesEqual(
            new byte[] { 0x00, 0x00 },
            new byte[] { 0x00, 0x00, 0x00, 0x00 }).Should().BeFalse();
    }

    // ── REG_BINARY bit extraction (ShellState "Click items as follows" Selection) ──

    [Fact]
    public void BitIsSet_MaskSetAtByte_ReturnsTrue()
    {
        // ShellState byte 4, bit 0x20 set
        var blob = new byte[] { 0, 0, 0, 0, 0x20, 0, 0, 0 };
        SettingStateReader.BitIsSet(blob, 4, 0x20).Should().BeTrue();
    }

    [Fact]
    public void BitIsSet_MaskClearAtByte_ReturnsFalse()
    {
        var blob = new byte[] { 0, 0, 0, 0, 0x00, 0, 0, 0 };
        SettingStateReader.BitIsSet(blob, 4, 0x20).Should().BeFalse();
    }

    [Fact]
    public void BitIsSet_ByteIndexOutOfRange_ReturnsFalse()
    {
        SettingStateReader.BitIsSet(new byte[] { 0x20 }, 4, 0x20).Should().BeFalse();
    }
}