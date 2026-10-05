using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Models;
using AkariTool.ViewModels.Tweaks;
using Microsoft.Win32;
using FluentAssertions;
using Xunit;

namespace AkariTool.App.Tests.ViewModels.Tweaks;

/// <summary>
/// Characterization suite for <see cref="SettingBadgeCalculator.Compute"/> (D-02, D-04).
///
/// THIS FILE FREEZES BEHAVIOUR; IT DOES NOT IMPROVE IT. Phase 5 extracts this logic into a
/// shared row primitive (CORE-04); this suite is the baseline that proves the extraction did
/// not change anything. If an assertion here fails, the BEHAVIOUR changed - that is a finding
/// for Phase 5, not a bug in the test and not something to fix by editing the assertion.
///
/// Two behaviours are pinned deliberately even though they look like defects:
///   * <c>Compute</c> guards on <c>definition.InputType</c> while every later branch switches on
///     the <c>inputType</c> parameter. That asymmetry is frozen by
///     <see cref="ActionDefinition_NonActionInputType_ReturnsNoPills"/> and must NOT be
///     "cleaned up" here, or Phase 5's extraction would silently change shipped behaviour.
///   * Pill ORDER is behaviour: it decides what a user sees first. Assertions below therefore
///     compare the whole returned sequence (kind, highlight, order), never just a count.
///
/// NSubstitute is deliberately unused: Compute is pure, takes no services and does no I/O.
/// This is also the ONLY test class in the project (D-04) - the real-catalog validity test is
/// Phase 2/BUG-02, the container-resolution test is Phase 3/TEST-02 and the Core-references
/// test is Phase 3/TEST-03.
/// </summary>
public class SettingBadgeCalculatorTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static RegistrySetting Registry(string id, int? recommended = 1, int? @default = 0) => new()
    {
        KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\AkariTest",
        ValueName = id,
        RecommendedValue = recommended,
        DefaultValue = @default,
        ValueType = RegistryValueKind.DWord,
        EnabledValue = new object?[] { 1 },
        DisabledValue = new object?[] { 0 },
    };

    private static ComboBoxOption Opt(string name, bool rec = false, bool def = false, int? powerCfgValue = null) => new()
    {
        DisplayName = name,
        IsRecommended = rec,
        IsDefault = def,
        ValueMappings = powerCfgValue is null
            ? null
            : new Dictionary<string, object?> { ["PowerCfgValue"] = powerCfgValue.Value },
    };

    private static SettingDefinition Toggle(
        string id,
        bool? recommendedState = null,
        bool? defaultState = null,
        bool subjective = false,
        int? registryRecommended = 1,
        int? registryDefault = 1) => new()
        {
            Id = id,
            Name = id,
            Description = "d",
            InputType = InputType.Toggle,
            RecommendedToggleState = recommendedState,
            DefaultToggleState = defaultState,
            IsSubjectivePreference = subjective,
            RegistrySettings = new[] { Registry(id, registryRecommended, registryDefault) },
        };

    private static SettingDefinition Action(string id) => new()
    {
        Id = id,
        Name = id,
        Description = "d",
        InputType = InputType.Action,
        // Deliberately badge-bearing: the Action guard must return BEFORE the hasBadgeData
        // gate is ever consulted, so this fixture has to carry real badge data.
        RegistrySettings = new[] { Registry(id) },
    };

    private static SettingDefinition Selection(
        string id,
        IEnumerable<ComboBoxOption>? options = null,
        bool subjective = false,
        PowerCfgSetting? powerCfg = null) => new()
        {
            Id = id,
            Name = id,
            Description = "d",
            InputType = InputType.Selection,
            IsSubjectivePreference = subjective,
            ComboBox = new ComboBoxMetadata
            {
                Options = options?.ToList() ?? new List<ComboBoxOption>
                {
                    Opt("Recommended", rec: true),
                    Opt("Default", def: true),
                },
            },
            PowerCfgSettings = powerCfg is null ? null : new[] { powerCfg },
        };

    private static SettingDefinition NumericRange(
        string id,
        string? units = "Seconds",
        int? recommendedAc = null,
        int? recommendedDc = null,
        int? defaultAc = null,
        int? defaultDc = null,
        PowerModeSupport powerModeSupport = PowerModeSupport.Both) => new()
        {
            Id = id,
            Name = id,
            Description = "d",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 9999, Units = units },
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SettingGuid = "sub_sleep",
                    PowerModeSupport = powerModeSupport,
                    Units = units,
                    RecommendedValueAC = recommendedAc,
                    RecommendedValueDC = recommendedDc,
                    DefaultValueAC = defaultAc,
                    DefaultValueDC = defaultDc,
                },
            },
        };

    private static IReadOnlyList<BadgePillState> Compute(
        SettingDefinition definition,
        InputType inputType,
        bool isOn = false,
        int selectedIndex = -1,
        int numericValue = 0,
        int acNumericValue = 0,
        int dcNumericValue = 0,
        bool hasBattery = false,
        bool supportsSeparateACDC = false) =>
        SettingBadgeCalculator.Compute(
            definition, inputType, isOn, selectedIndex,
            numericValue, acNumericValue, dcNumericValue, hasBattery, supportsSeparateACDC);

    // ── Behaviour 1: the InputType.Action guard reads the DEFINITION, not the parameter ──

    [Fact]
    public void ActionDefinition_NonActionInputType_ReturnsNoPills()
    {
        // FROZEN AS-IS. The guard at SettingBadgeCalculator.cs:30-31 reads
        // definition.InputType, while every branch after it switches on the inputType
        // parameter. So an Action definition passed a NON-action inputType still returns
        // nothing. Phase 5's shared-primitive extraction must not silently change this;
        // whether the asymmetry is intentional or a latent defect is NOT this phase's call.
        var pills = Compute(Action("act"), InputType.Toggle, isOn: true);

        pills.Should().BeEmpty(
            because: "the Action guard reads definition.InputType, not the inputType parameter - " +
                     "a later refactor must not quietly start honouring the parameter instead");
    }

    // ── Behaviour 2: the hasBadgeData gate ───────────────────────────────────

    [Fact]
    public void Compute_NoBadgeDataAnywhere_ReturnsNoPills()
    {
        // A Toggle whose only registry setting carries neither a recommended nor a default
        // value, with no scheduled task, no flagged combo option and no valued PowerCfg
        // entry, satisfies none of the hasBadgeData sources (lines 33-41).
        var definition = new SettingDefinition
        {
            Id = "no-badge-data",
            Name = "no-badge-data",
            Description = "d",
            InputType = InputType.Toggle,
            RegistrySettings = new[] { Registry("no-badge-data", recommended: null, @default: null) },
        };

        Compute(definition, InputType.Toggle, isOn: true).Should().BeEmpty();
    }

    // ── Behaviour 3: Toggle matching recommended AND default ─────────────────

    [Fact]
    public void Toggle_MatchingRecommendedAndDefault_ReturnsBothPillsHighlightedInOrder()
    {
        var pills = Compute(Toggle("t", recommendedState: true, defaultState: true), InputType.Toggle, isOn: true);

        // Expected kinds are passed as an explicit collection, not as bare varargs: with a
        // trailing `because:` argument FluentAssertions cannot disambiguate the
        // Equal(params TExpectation[]) overload from Equal(IEnumerable<TExpectation>).
        pills.Select(p => p.Kind).Should().Equal(
            new[] { SettingBadgeKind.Recommended, SettingBadgeKind.Default },
            because: "pill order decides what the user sees first and must survive the extraction");

        pills.Select(p => p.IsHighlighted).Should().Equal(new[] { true, true });
    }

    // ── Behaviour 4: evidence folding - Recommended present but dimmed ────────

    [Fact]
    public void Toggle_DisagreesWithRecommendedOnly_DimsRecommendedAndHighlightsDefault()
    {
        var pills = Compute(
            Toggle("t", recommendedState: true, defaultState: false, registryDefault: 0),
            InputType.Toggle,
            isOn: false);

        pills.Select(p => p.Kind).Should().Equal(SettingBadgeKind.Recommended, SettingBadgeKind.Default);

        // Recommended is present but NOT highlighted; Default still is. This is the
        // evidence-folding behaviour: matchesRec/matchesDef are AND-folded across every
        // source, so ANY source with a disagreeing opinion dims the flag and sources
        // without one abstain. Here RecommendedToggleState(true) disagrees with isOn=false
        // and dims Recommended, while DefaultToggleState(false) agrees and the registry
        // setting's own default (0) agrees too, so Default stays highlighted.
        pills[0].IsHighlighted.Should().BeFalse();
        pills[1].IsHighlighted.Should().BeTrue(
            because: "both the toggle-level default and the registry default agree with isOn=false, " +
                     "so no evidence source dims the Default pill");
    }

    // ── Behaviour 5: IsSubjectivePreference replaces both pills ──────────────

    [Fact]
    public void Toggle_SubjectivePreference_ReplacesRecommendedAndDefaultWithSinglePreferencePill()
    {
        var pills = Compute(Toggle("t", recommendedState: true, defaultState: false, subjective: true),
            InputType.Toggle, isOn: true);

        pills.Should().ContainSingle();
        pills[0].Kind.Should().Be(SettingBadgeKind.Preference);
        pills[0].IsHighlighted.Should().BeTrue(
            because: "the Preference pill is a setting-level attribute, so it is unconditionally highlighted");
    }

    // ── Behaviour 6: Selection by ComboBoxOption flags ───────────────────────

    [Fact]
    public void Selection_SelectedIndexIsRecommended_Option_HighlightsRecommendedAndDimsDefault()
    {
        var pills = Compute(
            Selection("s", new[] { Opt("A", rec: true), Opt("B", def: true) }),
            InputType.Selection,
            selectedIndex: 0);

        // Both pills are PRESENT, in this order. Only Recommended is highlighted: the
        // highlight for a Selection comes from the flag on the option AT the selected
        // index, and index 0 carries IsRecommended but not IsDefault. That is the
        // frozen behaviour - a refactor that made the Default pill inherit the
        // Recommended highlight would change what the user sees.
        pills.Select(p => p.Kind).Should().Equal(
            new[] { SettingBadgeKind.Recommended, SettingBadgeKind.Default });
        pills.Select(p => p.IsHighlighted).Should().Equal(new[] { true, false });
    }

    // ── Behaviour 7: null NumericRange edge on a flag-classified Selection ───

    [Fact]
    public void Selection_NullNumericRangeAndNoSeparateAcDc_StillClassifiesByOptionFlags()
    {
        // No NumericRange at all and supportsSeparateACDC=false, so the only classification
        // path is the ComboBoxOption flags. A regression that dereferences NumericRange on
        // this path would throw rather than classify.
        var definition = Selection("s", new[] { Opt("A", rec: true), Opt("B", def: true) });
        definition.NumericRange.Should().BeNull();

        var pills = Compute(definition, InputType.Selection, selectedIndex: 0);

        pills.Select(p => p.Kind).Should().Equal(SettingBadgeKind.Recommended, SettingBadgeKind.Default);
        pills[0].IsHighlighted.Should().BeTrue();
    }

    // ── Behaviour 8: AC/DC-backed Selection and the considerDc rule ──────────

    [Fact]
    public void Selection_AcDcSeparate_WithBattery_IndexMatchesAcButNotDc_DoesNotHighlightRecommended()
    {
        // AC/DC selections compare the selected index against RecommendedValueAC/DC through
        // the option's PowerCfgValue mapping, NOT against ComboBoxOption flags. The index
        // matches AC (value 1) but not DC (value 2), and considerDc = hasBattery is true, so
        // Recommended must not be highlighted.
        var definition = Selection(
            "s",
            new[] { Opt("A", powerCfgValue: 1), Opt("B", powerCfgValue: 2) },
            powerCfg: new PowerCfgSetting
            {
                SettingGuid = "sub_sleep",
                RecommendedValueAC = 1,
                RecommendedValueDC = 2,
                DefaultValueAC = null,
                DefaultValueDC = null,
            });

        var pills = Compute(definition, InputType.Selection, selectedIndex: 0, hasBattery: true, supportsSeparateACDC: true);

        pills.Should().ContainSingle();
        pills[0].Kind.Should().Be(SettingBadgeKind.Recommended);
        pills[0].IsHighlighted.Should().BeFalse(
            because: "considerDc = hasBattery is true, so the DC value (2) is compared too and the " +
                     "selected index (0 -> 1) does not match it");
    }

    [Fact]
    public void Selection_AcDcSeparate_IndexMatchesBothAcAndDc_HighlightsRecommended()
    {
        var definition = Selection(
            "s",
            new[] { Opt("A", powerCfgValue: 1), Opt("B", powerCfgValue: 2) },
            powerCfg: new PowerCfgSetting
            {
                SettingGuid = "sub_sleep",
                RecommendedValueAC = 1,
                RecommendedValueDC = 1,
                DefaultValueAC = null,
                DefaultValueDC = null,
            });

        var pills = Compute(definition, InputType.Selection, selectedIndex: 0, hasBattery: true, supportsSeparateACDC: true);

        pills.Should().ContainSingle();
        pills[0].Kind.Should().Be(SettingBadgeKind.Recommended);
        pills[0].IsHighlighted.Should().BeTrue();
    }

    // ── Behaviour 9 + 10: the NumericRange unit-conversion path ──────────────

    [Theory]
    // "Minutes" divides the PowerCfg system value by 60. 3600/60 = 60 exactly; 3599/60 = 59
    // truncating. The adjacent pair is the integer-division boundary one below the hour: a
    // refactor that changes the unit path (rounding, or a float division) moves it, and this
    // is what catches that rather than assuming it.
    [InlineData(3600, 60)]
    [InlineData(3599, 59)]
    public void NumericRange_MinutesUnits_ConvertsSystemValueToDisplayUnits(int systemValue, int expectedDisplay)
    {
        // PowerCfg stores the system value; display units are Minutes, so a currently-set
        // display value of 60 must highlight Recommended.
        var definition = NumericRange("n", units: "Minutes", recommendedAc: systemValue);

        var pills = Compute(definition, InputType.NumericRange, numericValue: expectedDisplay);

        // FROZEN: this path emits TWO pills, not one. Recommended is added because
        // hasRecData is true, and a Custom pill is then added unconditionally whenever
        // hasRecData || hasDefData (SettingBadgeCalculator.cs:203-204). Custom is NOT
        // highlighted here because the value matches the recommendation.
        pills.Select(p => p.Kind).Should().Equal(
            new[] { SettingBadgeKind.Recommended, SettingBadgeKind.Custom });
        pills.Select(p => p.IsHighlighted).Should().Equal(new[] { true, false });
        pills[0].IsHighlighted.Should().BeTrue(
            because: $"NumericConversionHelper converts {systemValue} seconds to " +
                     $"{expectedDisplay} minutes, so the display value matches the recommendation");
    }

    [Fact]
    public void NumericRange_NullUnits_DegradesToIdentityConversion()
    {
        // No unit string -> NumericConversionHelper falls through to the identity branch, so
        // the system value and the display value are compared 1:1 with no scaling at all.
        var definition = NumericRange("n", units: null, recommendedAc: 42);

        var pills = Compute(definition, InputType.NumericRange, numericValue: 42);

        pills.Select(p => p.Kind).Should().Equal(
            new[] { SettingBadgeKind.Recommended, SettingBadgeKind.Custom });
        pills[0].IsHighlighted.Should().BeTrue();
    }

    // ── Smoke: discovery + reachability ─────────────────────────────────────

    [Fact]
    public void Compute_ToggleWithNoBadgeData_ReturnsNoPills()
    {
        // The smoke test for discovery + reachability: the single registry setting must
        // carry NO recommended and NO default value, otherwise the definition has badge
        // data and the hasBadgeData gate opens.
        var pills = Compute(
            Toggle("toggle-no-badge-data", registryRecommended: null, registryDefault: null),
            InputType.Toggle,
            isOn: true);

        pills.Should().BeEmpty();
    }
}