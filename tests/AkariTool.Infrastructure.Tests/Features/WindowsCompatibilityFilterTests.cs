using System;
using System.Collections.Generic;
using System.Linq;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;
using AkariTool.Infrastructure.Features.Common.Services;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

// 4h — Winhance WindowsCompatibilityFilterTests ported (Moq → NSubstitute).
public class WindowsCompatibilityFilterTests
{
    private readonly IWindowsVersionService _versionService = Substitute.For<IWindowsVersionService>();
    private readonly IAkariLogService _logService = Substitute.For<IAkariLogService>();

    #region Constructor

    [Fact]
    public void Constructor_NullVersionService_ThrowsArgumentNullException()
    {
        var act = () => new WindowsCompatibilityFilter(null!, _logService);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("versionService");
    }

    [Fact]
    public void Constructor_NullLogService_ThrowsArgumentNullException()
    {
        var act = () => new WindowsCompatibilityFilter(_versionService, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    #endregion

    #region FilterSettingsByWindowsVersion (with applyFilter=true)

    [Fact]
    public void FilterSettingsByWindowsVersion_NoRestrictions_ReturnsAllSettings()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(22621);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("s1"),
            CreateSetting("s2"),
            CreateSetting("s3")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_Windows10OnlySetting_OnWindows11_FilteredOut()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(22621);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("win10only", isWindows10Only: true),
            CreateSetting("normal")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_Windows11OnlySetting_OnWindows10_FilteredOut()
    {
        _versionService.IsWindows11().Returns(false);
        _versionService.GetWindowsBuildNumber().Returns(19045);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("win11only", isWindows11Only: true),
            CreateSetting("normal")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_MinimumBuildNotMet_FilteredOut()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(22000);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("needsNewBuild", minimumBuild: 22621),
            CreateSetting("normal")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_MaximumBuildExceeded_FilteredOut()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(26100);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("oldBuild", maximumBuild: 22621),
            CreateSetting("normal")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_BuildInSupportedRange_NotFilteredOut()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(22621);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("ranged", supportedRanges: new[] { (22000, 23000) })
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("ranged");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_BuildOutsideSupportedRange_FilteredOut()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(26100);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("ranged", supportedRanges: new[] { (22000, 22631) }),
            CreateSetting("normal")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_MinAndMaxBuild_BuildAboveMax_FilteredOut()
    {
        // Regression: when both MinimumBuildNumber and MaximumBuildNumber were set,
        // the else-if chain entered the min-build branch first and exited without
        // checking the max-build branch. Builds above the maximum leaked through.
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(26200);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("bounded", minimumBuild: 22000, maximumBuild: 26120),
            CreateSetting("normal")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_MinAndMaxBuild_BuildInRange_Kept()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(26100);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("bounded", minimumBuild: 22000, maximumBuild: 26120),
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("bounded");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_MinAndMaxBuild_BuildBelowMin_FilteredOut()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(21000);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("bounded", minimumBuild: 22000, maximumBuild: 26120),
            CreateSetting("normal")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().ContainSingle()
            .Which.Id.Should().Be("normal");
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_OnWindowsServer_DoesNotFilterSettings()
    {
        // Server 2022 (build 20348) detected as Windows 10
        _versionService.IsWindows11().Returns(false);
        _versionService.GetWindowsBuildNumber().Returns(20348);
        _versionService.IsWindowsServer().Returns(true);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("s1"),
            CreateSetting("s2"),
            CreateSetting("s3")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void FilterSettingsByWindowsVersion_OnWindowsServer_LogsServerDetection()
    {
        _versionService.IsWindows11().Returns(false);
        _versionService.GetWindowsBuildNumber().Returns(20348);
        _versionService.IsWindowsServer().Returns(true);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition> { CreateSetting("s1") };

        filter.FilterSettingsByWindowsVersion(settings);

        _logService.Received(1).Log(
            LogLevel.Info,
            Arg.Is<string>(s => s.Contains("Windows Server detected")));
    }

    #endregion

    #region FilterSettingsByWindowsVersion (with applyFilter=false)

    [Fact]
    public void FilterSettingsByWindowsVersion_ApplyFilterFalse_ReturnsAllWithCompatibilityMessages()
    {
        _versionService.IsWindows11().Returns(true);
        _versionService.GetWindowsBuildNumber().Returns(22621);

        var filter = CreateFilter();
        var settings = new List<SettingDefinition>
        {
            CreateSetting("win10only", isWindows10Only: true),
            CreateSetting("normal")
        };

        var result = filter.FilterSettingsByWindowsVersion(settings, applyFilter: false).ToList();

        result.Should().HaveCount(2);
        var win10Setting = result.First(s => s.Id == "win10only");
        win10Setting.VersionCompatibilityMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Helpers

    private WindowsCompatibilityFilter CreateFilter()
    {
        return new WindowsCompatibilityFilter(
            _versionService,
            _logService);
    }

    private static SettingDefinition CreateSetting(
        string id,
        bool isWindows10Only = false,
        bool isWindows11Only = false,
        int? minimumBuild = null,
        int? maximumBuild = null,
        (int MinBuild, int MaxBuild)[]? supportedRanges = null)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = $"Test setting {id}",
            IsWindows10Only = isWindows10Only,
            IsWindows11Only = isWindows11Only,
            MinimumBuildNumber = minimumBuild,
            MaximumBuildNumber = maximumBuild,
            SupportedBuildRanges = supportedRanges ?? Array.Empty<(int, int)>()
        };
    }

    #endregion
}
