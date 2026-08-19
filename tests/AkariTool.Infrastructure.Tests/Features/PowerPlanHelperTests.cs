using AkariTool.Infrastructure.Features.Common.Utilities;
using FluentAssertions;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

public class PowerPlanHelperTests
{
    [Theory]
    [InlineData("Ultimate Performance", true)]
    [InlineData("Höchstleistung", true)]
    [InlineData("Ultieme prestaties", true)]
    [InlineData("Desempenho Máximo", true)]
    [InlineData("Balanced", false)]
    [InlineData("High performance", false)]
    [InlineData("Power Saver", false)]
    [InlineData("", false)]
    public void IsUltimatePerformancePlan_RecognizesUltimateVariants(string name, bool expected)
    {
        PowerPlanHelper.IsUltimatePerformancePlan(name).Should().Be(expected);
    }

    [Fact]
    public void IsUltimatePerformancePlan_NullName_ReturnsFalse()
    {
        PowerPlanHelper.IsUltimatePerformancePlan(null!).Should().BeFalse();
    }

    [Theory]
    [InlineData("  Balanced  ", "Balanced")]
    [InlineData("", "")]
    [InlineData("High performance", "High performance")]
    public void CleanPlanName_TrimsWhitespace(string name, string expected)
    {
        PowerPlanHelper.CleanPlanName(name).Should().Be(expected);
    }

    [Fact]
    public void CleanPlanName_NullName_ReturnsEmpty()
    {
        PowerPlanHelper.CleanPlanName(null!).Should().BeEmpty();
    }
}