using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using FluentAssertions;
using Xunit;

namespace AkariTool.Core.Tests.Features;

public class BadgePillStateTests
{
    [Fact]
    public void PillOpacity_IsHighlighted_True_Returns1()
    {
        new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip").PillOpacity.Should().Be(1.0);
    }

    [Fact]
    public void PillOpacity_IsHighlighted_False_Returns035()
    {
        new BadgePillState(SettingBadgeKind.Recommended, false, "R", "tip").PillOpacity.Should().Be(0.35);
    }

    [Fact]
    public void RecordEquality_SameArgs_AreEqual()
    {
        var a = new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip");
        var b = new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip");
        a.Should().Be(b);
    }

    [Fact]
    public void DefaultMode_IsNone()
    {
        new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip").Mode.Should().Be(SettingBadgeMode.None);
    }
}
