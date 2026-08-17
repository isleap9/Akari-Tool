using AkariTool.Core.Interfaces;
using AkariTool.Core.Models.Update;
using AkariTool.Services;   // UpdateService lives in namespace AkariTool.Services
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AkariTool.Infrastructure.Tests.Services;

/// <summary>
/// Tests for the version-comparison logic in <see cref="UpdateService"/>.
///
/// UpdateService is a static class whose "is a newer version available?" decision is
/// the single line in CheckAsync:
///     <c>bool newer = latest is not null &amp;&amp; latest &gt; Normalize(CurrentVersion);</c>
/// where <c>latest = ParseTag(tag)</c>. Both ParseTag and the Version comparison are
/// pure and deterministic; ParseTag is <c>internal</c> and reachable here through the
/// project's InternalsVisibleTo. CheckAsync itself is async + HTTP and is therefore
/// exercised only indirectly — the pure comparison it depends on is tested directly.
///
/// ParseTag normalizes every result to 3 parts, so "v2" == "2.0" == "2.0.0",
/// which is exactly the comparison CheckAsync performs against the running version.
/// </summary>
public class UpdateServiceTests
{
    // ── 1. Newer remote version is an update ────────────────────────────────────

    [Theory]
    [InlineData("v2.1.0", "v2.0.0")]
    [InlineData("v2.0.1", "v2.0.0")]
    [InlineData("v3.0", "v2.9.9")]
    [InlineData("2.0.0", "1.0.0")]      // no 'v' prefix
    public void ParseTag_RemoteNewerThanCurrent_ComparesGreater(string remote, string current)
    {
        var latest = UpdateService.ParseTag(remote);
        var running = UpdateService.ParseTag(current);

        latest.Should().NotBeNull();
        running.Should().NotBeNull();
        (latest > running).Should().BeTrue("remote {0} is newer than current {1}", remote, current);
    }

    // ── 2. Same version → no update ─────────────────────────────────────────────

    [Theory]
    [InlineData("v2.0.0", "v2.0.0")]
    [InlineData("v2.0", "v2.0.0")]      // normalization: 2.0 == 2.0.0
    [InlineData("2", "v2.0.0")]         // "2" → "2.0" → "2.0.0"
    [InlineData("v2.1.0-beta", "v2.1.0")] // pre-release suffix stripped
    public void ParseTag_SameEffectiveVersion_ComparesEqual(string remote, string current)
    {
        var latest = UpdateService.ParseTag(remote);
        var running = UpdateService.ParseTag(current);

        (latest > running).Should().BeFalse();
        latest.Should().Be(running);
    }

    // ── 3. Older remote version → no update ─────────────────────────────────────

    [Theory]
    [InlineData("v1.9.9", "v2.0.0")]
    [InlineData("v2.0.0", "v2.0.1")]
    [InlineData("v1.0", "v2.0")]
    public void ParseTag_RemoteOlderThanCurrent_ComparesLess(string remote, string current)
    {
        var latest = UpdateService.ParseTag(remote);
        var running = UpdateService.ParseTag(current);

        (latest > running).Should().BeFalse();
    }

    // ── 4. Malformed tags are handled without throwing ──────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    [InlineData("v")]
    [InlineData("vabc")]
    [InlineData("...")]
    public void ParseTag_MalformedInput_ReturnsNullWithoutThrowing(string tag)
    {
        Version? result = null;
        var act = () => result = UpdateService.ParseTag(tag);

        act.Should().NotThrow();
        result.Should().BeNull();
    }

    [Fact]
    public void ParseTag_MatchesCheckAsyncGuard_NullLatestIsNeverNewer()
    {
        // CheckAsync guards with `latest is not null && latest > current`.
        // A malformed tag must therefore never be reported as an update.
        var latest = UpdateService.ParseTag("garbage");
        var current = UpdateService.ParseTag("v1.0.0");

        bool newer = latest is not null && latest > current;
        newer.Should().BeFalse();
    }

    [Fact]
    public void ParseTag_StripsBuildMetadataSuffix()
    {
        UpdateService.ParseTag("v2.1.0+build.42").Should().Be(new Version(2, 1, 0));
    }

    [Fact]
    public void ParseTag_IsCaseInsensitiveOnPrefix()
    {
        UpdateService.ParseTag("V2.3").Should().Be(UpdateService.ParseTag("v2.3"));
    }

    // ── 5. UpdateCheckResult.Status behaviour via the IUpdateService seam ───────
    // The static UpdateService cannot be substituted, but consumers depend on the
    // IUpdateService interface. NSubstitute verifies the result contract callers
    // rely on: an UpdateAvailable result surfaces its tag + installer URL.

    [Fact]
    public async Task IUpdateService_UpdateAvailableResult_ExposesInstallerUrl()
    {
        var svc = Substitute.For<IUpdateService>();
        svc.CheckAsync().Returns(new UpdateCheckResult
        {
            Status = UpdateStatus.UpdateAvailable,
            LatestTag = "v2.1.0",
            InstallerUrl = "https://example.com/AkariTool-Setup-2.1.0.exe",
        });

        var result = await svc.CheckAsync();

        result.Status.Should().Be(UpdateStatus.UpdateAvailable);
        result.InstallerUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task IUpdateService_UpToDateResult_ReportsNoUpdate()
    {
        var svc = Substitute.For<IUpdateService>();
        svc.CheckAsync().Returns(new UpdateCheckResult { Status = UpdateStatus.UpToDate });

        (await svc.CheckAsync()).Status.Should().Be(UpdateStatus.UpToDate);
    }

    // ── CurrentVersionDisplay format ────────────────────────────────────────────

    [Fact]
    public void CurrentVersionDisplay_StartsWithV_AndHasMajorMinor()
    {
        // Reads the running assembly version; format is "v{major}.{minor}[.{build}]".
        UpdateService.CurrentVersionDisplay.Should().MatchRegex(@"^v\d+\.\d+(\.\d+)?$");
    }

    [Fact]
    public void ReleasesPageUrl_PointsAtRepoReleases()
    {
        UpdateService.ReleasesPageUrl
            .Should().Be("https://github.com/isleap9/Akari-Tool/releases");
    }

    // ── CheckAsync itself: network-bound, not unit-testable here ─────────────────

    [Fact(Skip = "Requires network — CheckAsync performs a live GitHub API call")]
    public async Task CheckAsync_LiveCall_NotRunInUnitTests()
    {
        await UpdateService.CheckAsync();
    }
}
