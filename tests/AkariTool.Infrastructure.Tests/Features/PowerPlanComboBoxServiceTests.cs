using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

public class PowerPlanComboBoxServiceTests
{
    private static readonly PowerPlan BalancedPlan = new()
    {
        Name = "Balanced",
        Guid = "381b4222-f694-41f0-9685-ff5bb260df2e",
        IsActive = true,
    };

    private static readonly PowerPlan HighPerformancePlan = new()
    {
        Name = "High performance",
        Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
    };

    private static readonly PowerPlan CustomPlan = new()
    {
        Name = "My Custom Plan",
        Guid = "11111111-2222-3333-4444-555555555555",
    };

    private static (IPowerSettingsQueryService query, PowerPlanComboBoxService service) MakeService(IReadOnlyList<PowerPlan> systemPlans)
    {
        var query = Substitute.For<IPowerSettingsQueryService>();
        query.GetAvailablePowerPlansAsync().Returns(Task.FromResult(systemPlans.ToList()));
        query.GetActivePowerPlanAsync().Returns(Task.FromResult(systemPlans.FirstOrDefault(p => p.IsActive) ?? new PowerPlan()));

        var log = Substitute.For<IAkariLogService>();
        return (query, new PowerPlanComboBoxService(query, log));
    }

    [Fact]
    public async Task GetPowerPlanOptionsAsync_MatchesPredefinedByGuid()
    {
        var (_, service) = MakeService(new[] { BalancedPlan, HighPerformancePlan, CustomPlan });

        var options = await service.GetPowerPlanOptionsAsync();

        var balanced = options.Single(o => o.PredefinedPlan?.Name == "Balanced");
        balanced.ExistsOnSystem.Should().BeTrue();
        balanced.IsActive.Should().BeTrue();
        balanced.SystemPlan.Should().NotBeNull();
        balanced.DisplayName.Should().Be("Balanced");

        var akari = options.Single(o => o.PredefinedPlan?.Name == "Akari Power Plan");
        akari.ExistsOnSystem.Should().BeFalse();
        akari.SystemPlan.Should().BeNull();
    }

    [Fact]
    public async Task GetPowerPlanOptionsAsync_AppendsUnmatchedSystemPlans()
    {
        var (_, service) = MakeService(new[] { BalancedPlan, CustomPlan });

        var options = await service.GetPowerPlanOptionsAsync();

        var custom = options.Single(o => o.SystemPlan?.Guid == CustomPlan.Guid);
        custom.PredefinedPlan.Should().BeNull();
        custom.ExistsOnSystem.Should().BeTrue();
        custom.DisplayName.Should().Be("My Custom Plan");
    }

    [Fact]
    public async Task GetPowerPlanOptionsAsync_UltimatePerformance_DetectedByNameWhenGuidAbsent()
    {
        var localizedUltimate = new PowerPlan
        {
            Name = "Höchstleistung",
            Guid = "22222222-3333-4444-5555-666666666666",
        };

        var (_, service) = MakeService(new[] { BalancedPlan, localizedUltimate });

        var options = await service.GetPowerPlanOptionsAsync();

        var ultimate = options.Single(o => o.PredefinedPlan?.Name == "Ultimate Performance");
        ultimate.ExistsOnSystem.Should().BeTrue();
        ultimate.SystemPlan!.Guid.Should().Be(localizedUltimate.Guid);
    }

    [Fact]
    public async Task ResolveIndexFromRawValuesAsync_MatchesByGuid()
    {
        var (_, service) = MakeService(new[] { BalancedPlan, HighPerformancePlan });

        var raw = new Dictionary<string, object?>
        {
            ["ActivePowerPlan"] = "High performance",
            ["ActivePowerPlanGuid"] = HighPerformancePlan.Guid,
        };

        var index = await service.ResolveIndexFromRawValuesAsync(new SettingDefinition { Id = "power-plan-selection", Name = "Power Plan", Description = "Plan", InputType = InputType.Selection }, raw);

        index.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ResolvePowerPlanByIndexAsync_ReturnsGuid()
    {
        var (_, service) = MakeService(new[] { BalancedPlan, HighPerformancePlan, CustomPlan });

        var options = await service.GetPowerPlanOptionsAsync();
        var targetIndex = options.FindIndex(o => o.SystemPlan?.Guid == CustomPlan.Guid);

        var result = await service.ResolvePowerPlanByIndexAsync(targetIndex);

        result.Success.Should().BeTrue();
        result.Guid.Should().Be(CustomPlan.Guid);
        result.DisplayName.Should().Be("My Custom Plan");
    }

    [Fact]
    public async Task ResolvePowerPlanByIndexAsync_OutOfRange_ReturnsFailure()
    {
        var (_, service) = MakeService(new[] { BalancedPlan });

        var result = await service.ResolvePowerPlanByIndexAsync(999);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeEmpty();
    }
}