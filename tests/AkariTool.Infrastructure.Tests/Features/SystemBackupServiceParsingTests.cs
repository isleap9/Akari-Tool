using FluentAssertions;
using AkariTool.Infrastructure.Features.Common.Services;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Features;

// 4g — coverage for SystemBackupService's vssadmin output parsing (internal test
// seam, ParseTag precedent). Winhance's own tests for this class are env-dependent
// smoke tests hitting real WMI; the pure parsing functions are the mockable surface.
public class SystemBackupServiceParsingTests
{
    // ── ParseShadowStorageOutput ──

    [Fact]
    public void Parses_Used_And_Maximum_From_Realistic_Vssadmin_Output()
    {
        var output = string.Join('\n',
            "vssadmin 1.1 - Volume Shadow Copy Service administrative command-line tool",
            "(C) Copyright 2001-2013 Microsoft Corp. All rights reserved.",
            "",
            "Shadow Copy Storage association",
            "   For volume: (C:)\\\\?\\Volume{...}\\",
            "   Shadow Copy Storage volume: (C:)\\\\?\\Volume{...}\\",
            "   Used Shadow Copy Storage space: 9.25 GB (14%)",
            "   Allocated Shadow Copy Storage space: 10.4 GB (15%)",
            "   Maximum Shadow Copy Storage space: 68.3 GB (UNBOUNDED)");

        var (used, max) = SystemBackupService.ParseShadowStorageOutput(output);

        used.Should().Be((long)(9.25 * 1024 * 1024 * 1024));
        max.Should().Be((long)(68.3 * 1024 * 1024 * 1024));
    }

    [Fact]
    public void Missing_Lines_Returns_Negative_Sentinels()
    {
        var (used, max) = SystemBackupService.ParseShadowStorageOutput("no useful content here");

        used.Should().Be(-1);
        max.Should().Be(-1);
    }

    [Fact]
    public void Empty_Output_Returns_Negative_Sentinels()
    {
        var (used, max) = SystemBackupService.ParseShadowStorageOutput("");

        used.Should().Be(-1);
        max.Should().Be(-1);
    }

    // ── ParseByteValue ──

    [Theory]
    [InlineData("Used Shadow Copy Storage space: 512 KB (0%)", 512L * 1024)]
    [InlineData("Used Shadow Copy Storage space: 100 MB", 100L * 1024 * 1024)]
    [InlineData("Maximum Shadow Copy Storage space: 2 GB", 2L * 1024 * 1024 * 1024)]
    [InlineData("Maximum Shadow Copy Storage space: 1.5 TB", 1536L * 1024 * 1024 * 1024)]
    public void ParseByteValue_Handles_All_Units(string line, long expected)
    {
        SystemBackupService.ParseByteValue(line).Should().Be(expected);
    }

    [Fact]
    public void ParseByteValue_Strips_Percentage_Parenthetical()
    {
        SystemBackupService.ParseByteValue("Used Shadow Copy Storage space: 10.5 GB (14%)")
            .Should().Be((long)(10.5 * 1024 * 1024 * 1024));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no colon here")]
    [InlineData("Used Shadow Copy Storage space:")]
    [InlineData("Used Shadow Copy Storage space: 42")]
    [InlineData("Used Shadow Copy Storage space: abc GB")]
    [InlineData("Used Shadow Copy Storage space: 12 PB")]
    public void ParseByteValue_Malformed_Input_Returns_Negative_One(string line)
    {
        SystemBackupService.ParseByteValue(line).Should().Be(-1);
    }
}
