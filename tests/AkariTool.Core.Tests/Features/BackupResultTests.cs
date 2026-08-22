using System;
using AkariTool.Core.Features.Common.Models;
using FluentAssertions;
using Xunit;

namespace AkariTool.Core.Tests.Features;

// 4g — Winhance SystemBackupServiceTests BackupResult coverage, ported verbatim.
public class BackupResultTests
{
    // ── BackupResult model coverage ──

    [Fact]
    public void CreateSuccess_SetsCorrectProperties()
    {
        var date = new DateTime(2025, 1, 15);
        var result = BackupResult.CreateSuccess(
            restorePointDate: date,
            restorePointCreated: true);

        result.Success.Should().BeTrue();
        result.RestorePointDate.Should().Be(date);
        result.RestorePointCreated.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CreateFailure_SetsCorrectProperties()
    {
        var result = BackupResult.CreateFailure("Something went wrong");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Something went wrong");
        result.RestorePointCreated.Should().BeFalse();
    }
}
