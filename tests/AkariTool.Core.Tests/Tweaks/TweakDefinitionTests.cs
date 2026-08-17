using AkariTool.Core.Tweaks;
using FluentAssertions;
using Xunit;

namespace AkariTool.Core.Tests.Tweaks;

/// <summary>
/// Tests for the badge-pill computation on <see cref="TweakDefinition"/>.
///
/// A badge pill's <c>IsActive</c> flag is what drives whether the badge lights up in
/// the UI. For toggles the active-highlight math is a plain equality between the
/// current toggle state and the pill's <c>RecommendedState</c>/<c>DefaultState</c>.
/// For dropdowns it is driven by the selected option's <c>IsRecommended</c>/
/// <c>IsDefault</c> flags, with a "Custom" pill that lights when the current value
/// matches neither (including the unselected index == -1 case).
/// </summary>
public class TweakDefinitionTests
{
    private static TweakDefinition MakeToggle(bool? recommended, bool? defaultState, bool isPreference = false) =>
        new()
        {
            Id = "test.toggle",
            Name = "Test Toggle",
            Description = "A toggle tweak for testing.",
            InputKind = TweakInputKind.Toggle,
            RecommendedState = recommended,
            DefaultState = defaultState,
            IsPreference = isPreference,
        };

    private static TweakDefinition MakeDropdown(params TweakDropdownOption[] options) =>
        new()
        {
            Id = "test.dropdown",
            Name = "Test Dropdown",
            Description = "A dropdown tweak for testing.",
            InputKind = TweakInputKind.Dropdown,
            Options = options,
        };

    // ── 1. Recommended pill is active when current matches recommended ──────────

    [Fact]
    public void ToggleBadges_RecommendedPill_IsActive_WhenCurrentMatchesRecommended()
    {
        var def = MakeToggle(recommended: true, defaultState: false);

        var recommended = def.ComputeToggleBadges(currentState: true)
            .Single(p => p.Kind == TweakBadgeKind.Recommended);

        recommended.IsActive.Should().BeTrue();
    }

    // ── 2. Default pill is active when current matches the Windows default ──────

    [Fact]
    public void ToggleBadges_DefaultPill_IsActive_WhenCurrentMatchesDefault()
    {
        var def = MakeToggle(recommended: true, defaultState: false);

        var pills = def.ComputeToggleBadges(currentState: false);

        pills.Single(p => p.Kind == TweakBadgeKind.Default).IsActive.Should().BeTrue();
        pills.Single(p => p.Kind == TweakBadgeKind.Recommended).IsActive.Should().BeFalse();
    }

    // ── 3. "Custom" for a toggle: neither pill is active ────────────────────────
    // A toggle has no dedicated Custom pill, so the "matches neither" case surfaces
    // as no pill being active. Here recommended==default==true, current==false.

    [Fact]
    public void ToggleBadges_NoPillActive_WhenCurrentMatchesNeither()
    {
        var def = MakeToggle(recommended: true, defaultState: true);

        var pills = def.ComputeToggleBadges(currentState: false);

        pills.Should().OnlyContain(p => p.IsActive == false);
    }

    // ── 4. Toggle on/off correctness across both directions ─────────────────────

    [Theory]
    [InlineData(true, true, true)]     // current ON, recommended ON  → active
    [InlineData(false, true, false)]   // current OFF, recommended ON → inactive
    [InlineData(false, false, true)]   // current OFF, recommended OFF → active
    [InlineData(true, false, false)]   // current ON, recommended OFF → inactive
    public void ToggleBadges_RecommendedActive_TracksEquality(
        bool current, bool recommendedState, bool expectedActive)
    {
        var def = MakeToggle(recommended: recommendedState, defaultState: null);

        var recommended = def.ComputeToggleBadges(current)
            .Single(p => p.Kind == TweakBadgeKind.Recommended);

        recommended.IsActive.Should().Be(expectedActive);
    }

    [Fact]
    public void ToggleBadges_OmitsRecommendedPill_WhenRecommendedStateNull()
    {
        var def = MakeToggle(recommended: null, defaultState: true);

        def.ComputeToggleBadges(currentState: true)
            .Should().NotContain(p => p.Kind == TweakBadgeKind.Recommended);
    }

    [Fact]
    public void ToggleBadges_Preference_AddsAlwaysActivePreferencePillFirst()
    {
        var def = MakeToggle(recommended: true, defaultState: false, isPreference: true);

        var pills = def.ComputeToggleBadges(currentState: false);

        pills[0].Kind.Should().Be(TweakBadgeKind.Preference);
        pills[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public void ToggleBadges_InvertBadgeLabelWording_ChangesTooltipNotActiveMath()
    {
        var normal = new TweakDefinition
        {
            Id = "x", Name = "x", Description = "x",
            RecommendedState = true, InvertBadgeLabelWording = false,
        };
        var inverted = new TweakDefinition
        {
            Id = "x", Name = "x", Description = "x",
            RecommendedState = true, InvertBadgeLabelWording = true,
        };

        var normalPill = normal.ComputeToggleBadges(true).Single(p => p.Kind == TweakBadgeKind.Recommended);
        var invertedPill = inverted.ComputeToggleBadges(true).Single(p => p.Kind == TweakBadgeKind.Recommended);

        // Active-highlight math is unchanged by the wording flag.
        invertedPill.IsActive.Should().Be(normalPill.IsActive);
        // Only the tooltip wording differs.
        invertedPill.Tooltip.Should().NotBe(normalPill.Tooltip);
        invertedPill.Tooltip.Should().Contain("feature");
    }

    // ── 5. Dropdown badges are index-based ──────────────────────────────────────

    [Fact]
    public void DropdownBadges_RecommendedPill_IsActive_WhenSelectedOptionIsRecommended()
    {
        var def = MakeDropdown(
            new TweakDropdownOption("Off", 0, IsDefault: true),
            new TweakDropdownOption("Balanced", 1, IsRecommended: true),
            new TweakDropdownOption("Max", 2));

        var pills = def.ComputeDropdownBadges(currentIndex: 1);

        pills.Single(p => p.Kind == TweakBadgeKind.Recommended).IsActive.Should().BeTrue();
        pills.Single(p => p.Kind == TweakBadgeKind.Default).IsActive.Should().BeFalse();
        pills.Single(p => p.Kind == TweakBadgeKind.Custom).IsActive.Should().BeFalse();
    }

    [Fact]
    public void DropdownBadges_DefaultPill_IsActive_WhenSelectedOptionIsDefault()
    {
        var def = MakeDropdown(
            new TweakDropdownOption("Off", 0, IsDefault: true),
            new TweakDropdownOption("Balanced", 1, IsRecommended: true));

        var pills = def.ComputeDropdownBadges(currentIndex: 0);

        pills.Single(p => p.Kind == TweakBadgeKind.Default).IsActive.Should().BeTrue();
        pills.Single(p => p.Kind == TweakBadgeKind.Custom).IsActive.Should().BeFalse();
    }

    [Fact]
    public void DropdownBadges_CustomPill_IsActive_WhenSelectedMatchesNeither()
    {
        var def = MakeDropdown(
            new TweakDropdownOption("Off", 0, IsDefault: true),
            new TweakDropdownOption("Balanced", 1, IsRecommended: true),
            new TweakDropdownOption("Max", 2));   // neither recommended nor default

        var pills = def.ComputeDropdownBadges(currentIndex: 2);

        pills.Single(p => p.Kind == TweakBadgeKind.Custom).IsActive.Should().BeTrue();
        pills.Single(p => p.Kind == TweakBadgeKind.Recommended).IsActive.Should().BeFalse();
        pills.Single(p => p.Kind == TweakBadgeKind.Default).IsActive.Should().BeFalse();
    }

    // ── 6. Initial / unset state (before a value is known) ──────────────────────
    // currentIndex == -1 means the machine's value matches no listed option, which
    // is the row's "nothing is known / unselected" state. The Custom pill lights.

    [Fact]
    public void DropdownBadges_UnsetIndex_LightsCustomPill()
    {
        var def = MakeDropdown(
            new TweakDropdownOption("Off", 0, IsDefault: true),
            new TweakDropdownOption("On", 1, IsRecommended: true));

        var pills = def.ComputeDropdownBadges(currentIndex: -1);

        pills.Single(p => p.Kind == TweakBadgeKind.Custom).IsActive.Should().BeTrue();
        pills.Single(p => p.Kind == TweakBadgeKind.Recommended).IsActive.Should().BeFalse();
        pills.Single(p => p.Kind == TweakBadgeKind.Default).IsActive.Should().BeFalse();
    }

    [Fact]
    public void DropdownBadges_EmptyOptions_ReturnsNoPills()
    {
        var def = MakeDropdown();   // no options

        def.ComputeDropdownBadges(currentIndex: 0).Should().BeEmpty();
    }

    [Fact]
    public void DropdownBadges_OmitsRecommendedPill_WhenNoOptionIsRecommended()
    {
        var def = MakeDropdown(
            new TweakDropdownOption("A", 0, IsDefault: true),
            new TweakDropdownOption("B", 1));

        def.ComputeDropdownBadges(currentIndex: 0)
            .Should().NotContain(p => p.Kind == TweakBadgeKind.Recommended);
    }
}
