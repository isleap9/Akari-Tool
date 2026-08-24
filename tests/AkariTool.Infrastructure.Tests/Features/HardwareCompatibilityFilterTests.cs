using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Services;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

// 4h — Winhance HardwareCompatibilityFilterTests ported (Moq → NSubstitute).
public class HardwareCompatibilityFilterTests
{
    private readonly IHardwareDetectionService _hardwareDetection = Substitute.For<IHardwareDetectionService>();
    private readonly IAkariLogService _logService = Substitute.For<IAkariLogService>();
    private readonly HardwareCompatibilityFilter _filter;

    public HardwareCompatibilityFilterTests()
    {
        // Default: desktop machine (no battery, no lid, no brightness, no hybrid sleep)
        _hardwareDetection.HasBatteryAsync().Returns(false);
        _hardwareDetection.HasLidAsync().Returns(false);
        _hardwareDetection.SupportsBrightnessControlAsync().Returns(false);
        _hardwareDetection.SupportsHybridSleepAsync().Returns(false);

        _filter = new HardwareCompatibilityFilter(
            _hardwareDetection,
            _logService);
    }

    #region Constructor

    [Fact]
    public void Constructor_NullHardwareDetectionService_ThrowsArgumentNullException()
    {
        var act = () => new HardwareCompatibilityFilter(null!, _logService);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("hardwareDetectionService");
    }

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        var act = () => new HardwareCompatibilityFilter(_hardwareDetection, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    #endregion

    #region FilterSettingsByHardwareAsync

    [Fact]
    public async Task FilterSettingsByHardwareAsync_NoRestrictions_ReturnsAllSettings()
    {
        var settings = new List<SettingDefinition>
        {
            CreateSetting("setting1"),
            CreateSetting("setting2"),
            CreateSetting("setting3")
        };

        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresBatteryOnDesktop_FilteredOut()
    {
        var settings = new List<SettingDefinition>
        {
            CreateSetting("batteryOnly", requiresBattery: true),
            CreateSetting("normal")
        };

        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresLidOnDesktop_FilteredOut()
    {
        var settings = new List<SettingDefinition>
        {
            CreateSetting("lidOnly", requiresLid: true),
            CreateSetting("normal")
        };

        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresDesktopOnLaptop_FilteredOut()
    {
        // Simulate laptop (has battery + lid)
        _hardwareDetection.HasBatteryAsync().Returns(true);
        _hardwareDetection.HasLidAsync().Returns(true);

        // Need a new filter instance since detection results are cached per instance
        var filter = new HardwareCompatibilityFilter(
            _hardwareDetection,
            _logService);

        var settings = new List<SettingDefinition>
        {
            CreateSetting("desktopOnly", requiresDesktop: true),
            CreateSetting("normal")
        };

        var result = await filter.FilterSettingsByHardwareAsync(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresBrightnessWithoutSupport_FilteredOut()
    {
        var settings = new List<SettingDefinition>
        {
            CreateSetting("brightness", requiresBrightness: true),
            CreateSetting("normal")
        };

        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_RequiresHybridSleepWithoutSupport_FilteredOut()
    {
        var settings = new List<SettingDefinition>
        {
            CreateSetting("hybridSleep", requiresHybridSleep: true),
            CreateSetting("normal")
        };

        var result = await _filter.FilterSettingsByHardwareAsync(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public async Task FilterSettingsByHardwareAsync_CachesDetectionResults_OnlyQueriesOnce()
    {
        var settings = new List<SettingDefinition> { CreateSetting("s1") };

        // Call twice
        await _filter.FilterSettingsByHardwareAsync(settings);
        await _filter.FilterSettingsByHardwareAsync(settings);

        // Detection methods called only once due to caching
        await _hardwareDetection.Received(1).HasBatteryAsync();
        await _hardwareDetection.Received(1).HasLidAsync();
    }

    #endregion

    #region Helpers

    private static SettingDefinition CreateSetting(
        string id,
        bool requiresBattery = false,
        bool requiresLid = false,
        bool requiresDesktop = false,
        bool requiresBrightness = false,
        bool requiresHybridSleep = false)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = $"Test setting {id}",
            RequiresBattery = requiresBattery,
            RequiresLid = requiresLid,
            RequiresDesktop = requiresDesktop,
            RequiresBrightnessSupport = requiresBrightness,
            RequiresHybridSleepCapable = requiresHybridSleep
        };
    }

    #endregion
}
