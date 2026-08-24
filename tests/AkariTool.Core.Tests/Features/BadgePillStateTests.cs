using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using FluentAssertions;
using Xunit;

namespace AkariTool.Core.Tests.Features;

public class BadgePillStateTests
{
    [Fact]
    public void RecordEquality_SameArgs_AreEqual()
    {
        var a = new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip");
        var b = new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip");
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentHighlight_NotEqual()
    {
        var a = new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip");
        var b = new BadgePillState(SettingBadgeKind.Recommended, false, "R", "tip");
        a.Should().NotBe(b);
    }

    [Fact]
    public void DefaultMode_IsNone()
    {
        new BadgePillState(SettingBadgeKind.Recommended, true, "R", "tip").Mode.Should().Be(SettingBadgeMode.None);
    }
}
