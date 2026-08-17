using AkariTool.Core.Tweaks;
using FluentAssertions;
using Xunit;

namespace AkariTool.Core.Tests.Tweaks;

/// <summary>
/// Tests for the shared target-resolution / mismatch predicates in
/// <see cref="TweakTargets"/>. These are the pure functions that every bulk surface
/// (section bars, Quick Actions counts, the bulk engine) agrees on, so bugs here
/// would make the surfaces silently disagree.
///
/// NOTE: The task brief mentions "AkariOS vs non-AkariOS" OS-family targeting.
/// No such concept exists in TweakTargets.cs — there is no OS-family field on
/// TweakDefinition and no per-OS predicate. What TweakTargets actually resolves is
/// the *recommended* vs *default* target VALUE for a tweak, and whether the tweak's
/// current state is mismatched against that target. These tests cover the real
/// surface; the OS-family tests are reported as not-applicable rather than faked.
/// </summary>
public class TweakTargetsTests
{
    // ── helpers ─────────────────────────────────────────────────────────────────

    private static TweakDefinition Toggle(
        bool? recommended = null, bool? defaultState = null,
        bool? currentState = null, bool withApply = true) =>
        new()
        {
            Id = "t", Name = "t", Description = "t",
            InputKind = TweakInputKind.Toggle,
            RecommendedState = recommended,
            DefaultState = defaultState,
            ReadState = () => currentState,
            Apply = withApply ? _ => { } : null,
        };

    private static TweakDefinition Dropdown(
        int? currentIndex, bool withApplyIndex = true, params TweakDropdownOption[] options) =>
        new()
        {
            Id = "d", Name = "d", Description = "d",
            InputKind = TweakInputKind.Dropdown,
            Options = options,
            ReadCurrentIndex = () => currentIndex,
            ApplyIndex = withApplyIndex ? _ => { } : null,
        };

    // ── TryGetRecommendedTarget ─────────────────────────────────────────────────

    [Fact]
    public void TryGetRecommendedTarget_Toggle_ReturnsRecommendedState()
    {
        var def = Toggle(recommended: true);

        var ok = TweakTargets.TryGetRecommendedTarget(def, out var toggle, out var option);

        ok.Should().BeTrue();
        toggle.Should().BeTrue();
        option.Should().Be(-1);
    }

    [Fact]
    public void TryGetRecommendedTarget_Toggle_FailsWhenNoRecommendedState()
    {
        var def = Toggle(recommended: null);

        TweakTargets.TryGetRecommendedTarget(def, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetRecommendedTarget_Toggle_FailsWhenApplyMissing()
    {
        var def = Toggle(recommended: true, withApply: false);

        TweakTargets.TryGetRecommendedTarget(def, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetRecommendedTarget_Dropdown_ReturnsIndexOfRecommendedOption()
    {
        var def = Dropdown(currentIndex: 0, options: new[]
        {
            new TweakDropdownOption("A", 0),
            new TweakDropdownOption("B", 1, IsRecommended: true),
        });

        var ok = TweakTargets.TryGetRecommendedTarget(def, out _, out var option);

        ok.Should().BeTrue();
        option.Should().Be(1);
    }

    [Fact]
    public void TryGetRecommendedTarget_Dropdown_FailsWhenNoRecommendedOption()
    {
        var def = Dropdown(currentIndex: 0, options: new[]
        {
            new TweakDropdownOption("A", 0),
            new TweakDropdownOption("B", 1),
        });

        TweakTargets.TryGetRecommendedTarget(def, out _, out _).Should().BeFalse();
    }

    // ── TryGetDefaultTarget ─────────────────────────────────────────────────────

    [Fact]
    public void TryGetDefaultTarget_Toggle_ReturnsDefaultState()
    {
        var def = Toggle(defaultState: false);

        var ok = TweakTargets.TryGetDefaultTarget(def, out var toggle, out _);

        ok.Should().BeTrue();
        toggle.Should().BeFalse();
    }

    [Fact]
    public void TryGetDefaultTarget_Dropdown_ReturnsIndexOfDefaultOption()
    {
        var def = Dropdown(currentIndex: 1, options: new[]
        {
            new TweakDropdownOption("A", 0, IsDefault: true),
            new TweakDropdownOption("B", 1),
        });

        var ok = TweakTargets.TryGetDefaultTarget(def, out _, out var option);

        ok.Should().BeTrue();
        option.Should().Be(0);
    }

    // ── IsMismatched ────────────────────────────────────────────────────────────

    [Fact]
    public void IsMismatched_Toggle_TrueWhenCurrentDiffersFromTarget()
    {
        var def = Toggle(currentState: false);

        TweakTargets.IsMismatched(def, toggleTarget: true, optionTarget: -1).Should().BeTrue();
    }

    [Fact]
    public void IsMismatched_Toggle_FalseWhenCurrentEqualsTarget()
    {
        var def = Toggle(currentState: true);

        TweakTargets.IsMismatched(def, toggleTarget: true, optionTarget: -1).Should().BeFalse();
    }

    [Fact]
    public void IsMismatched_Toggle_FalseWhenCurrentStateUnknown()
    {
        // Unknown (null) current state must NOT count as a mismatch.
        var def = Toggle(currentState: null);

        TweakTargets.IsMismatched(def, toggleTarget: true, optionTarget: -1).Should().BeFalse();
    }

    [Fact]
    public void IsMismatched_Dropdown_TrueWhenSelectedIndexDiffers()
    {
        var def = Dropdown(currentIndex: 2, options: new[]
        {
            new TweakDropdownOption("A", 0),
            new TweakDropdownOption("B", 1),
            new TweakDropdownOption("C", 2),
        });

        TweakTargets.IsMismatched(def, toggleTarget: false, optionTarget: 0).Should().BeTrue();
    }

    [Fact]
    public void IsMismatched_Dropdown_FalseWhenIndexUnknownOrNegative()
    {
        var def = Dropdown(currentIndex: -1, options: new[]
        {
            new TweakDropdownOption("A", 0),
        });

        // idx < 0 means the current value matches no option → not counted as mismatch.
        TweakTargets.IsMismatched(def, toggleTarget: false, optionTarget: 0).Should().BeFalse();
    }

    [Fact]
    public void IsMismatched_SwallowsReaderExceptions_AndReturnsFalse()
    {
        var def = new TweakDefinition
        {
            Id = "t", Name = "t", Description = "t",
            InputKind = TweakInputKind.Toggle,
            ReadState = () => throw new InvalidOperationException("registry blew up"),
        };

        TweakTargets.IsMismatched(def, toggleTarget: true, optionTarget: -1).Should().BeFalse();
    }

    // ── CollectPending ──────────────────────────────────────────────────────────

    [Fact]
    public void CollectPending_IncludesOnlyMismatchedTweaks()
    {
        var needsChange = Toggle(recommended: true, currentState: false);  // mismatched
        var alreadyOk   = Toggle(recommended: true, currentState: true);   // matches
        var noTarget    = Toggle(recommended: null, currentState: false);  // no recommended target

        var entries = new (TweakDefinition, Action)[]
        {
            (needsChange, () => { }),
            (alreadyOk,   () => { }),
            (noTarget,    () => { }),
        };

        var pending = TweakTargets.CollectPending(entries, useRecommended: true);

        pending.Should().ContainSingle();
        pending[0].Def.Should().BeSameAs(needsChange);
        pending[0].ToggleTarget.Should().BeTrue();
    }

    [Fact]
    public void CollectPending_UsesDefaultTarget_WhenUseRecommendedFalse()
    {
        var def = Toggle(recommended: true, defaultState: false, currentState: true);

        // Against the DEFAULT target (false) the current ON state is a mismatch.
        var pending = TweakTargets.CollectPending(
            new (TweakDefinition, Action)[] { (def, () => { }) }, useRecommended: false);

        pending.Should().ContainSingle();
        pending[0].ToggleTarget.Should().BeFalse();
    }

    [Fact]
    public void CollectPending_EmptyWhenNothingMismatched()
    {
        var def = Toggle(recommended: true, currentState: true);

        TweakTargets.CollectPending(
            new (TweakDefinition, Action)[] { (def, () => { }) }, useRecommended: true)
            .Should().BeEmpty();
    }

    // ── WarningFor ──────────────────────────────────────────────────────────────

    [Fact]
    public void WarningFor_Toggle_ReturnsWarningForTargetDirection()
    {
        var def = new TweakDefinition
        {
            Id = "t", Name = "t", Description = "t",
            InputKind = TweakInputKind.Toggle,
            Warning = "Are you sure?",
            WarningState = true,   // warn only when switching ON
            RecommendedState = true,
            Apply = _ => { },
        };
        var work = new TweakTargets.PendingWork(def, () => { }, ToggleTarget: true, OptionTarget: -1);

        TweakTargets.WarningFor(work).Should().Be("Are you sure?");
    }

    [Fact]
    public void WarningFor_Toggle_NullWhenTargetDirectionDoesNotMatchWarningState()
    {
        var def = new TweakDefinition
        {
            Id = "t", Name = "t", Description = "t",
            InputKind = TweakInputKind.Toggle,
            Warning = "Are you sure?",
            WarningState = true,   // warn only when switching ON
        };
        var work = new TweakTargets.PendingWork(def, () => { }, ToggleTarget: false, OptionTarget: -1);

        TweakTargets.WarningFor(work).Should().BeNull();
    }

    [Fact]
    public void WarningFor_Dropdown_ReturnsPerOptionWarning()
    {
        var def = Dropdown(currentIndex: 0, options: new[]
        {
            new TweakDropdownOption("Safe", 0),
            new TweakDropdownOption("Risky", 1, Warning: "This may break things"),
        });
        var work = new TweakTargets.PendingWork(def, () => { }, ToggleTarget: false, OptionTarget: 1);

        TweakTargets.WarningFor(work).Should().Be("This may break things");
    }
}
