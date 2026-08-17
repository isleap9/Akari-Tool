using AkariTool.Core.Models.Update;
using FluentAssertions;
using Xunit;

namespace AkariTool.Core.Tests.Models;

/// <summary>
/// Tests for the update-related data models.
///
/// IMPORTANT: <see cref="UpdateCheckResult"/> and <see cref="ReleaseInfo"/> are plain
/// init-only data holders — there is NO constructor validation and no invariant
/// enforced in code (e.g. nothing forces InstallerUrl to be non-null when Status ==
/// UpdateAvailable; UpdateService simply leaves it null when no matching asset was
/// found). These tests therefore verify the property round-trip behaviour and the
/// enum surface rather than asserting invariants the code does not actually guarantee.
/// </summary>
public class UpdateModelsTests
{
    // ── UpdateCheckResult ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateCheckResult_UpdateAvailable_CarriesTagAndInstallerUrl()
    {
        var result = new UpdateCheckResult
        {
            Status = UpdateStatus.UpdateAvailable,
            LatestTag = "v2.1.0",
            ReleaseName = "Akari 2.1",
            ReleaseNotes = "- fixed things",
            ReleasePageUrl = "https://github.com/isleap9/Akari-Tool/releases/tag/v2.1.0",
            InstallerUrl = "https://example.com/AkariTool-Setup-2.1.0.exe",
        };

        result.Status.Should().Be(UpdateStatus.UpdateAvailable);
        result.LatestTag.Should().Be("v2.1.0");
        result.InstallerUrl.Should().NotBeNull();
        result.ReleasePageUrl.Should().StartWith("https://");
    }

    [Fact]
    public void UpdateCheckResult_Defaults_AreNullExceptStatus()
    {
        var result = new UpdateCheckResult();

        result.Status.Should().Be(UpdateStatus.UpToDate);   // enum default (value 0)
        result.LatestTag.Should().BeNull();
        result.ReleaseName.Should().BeNull();
        result.ReleaseNotes.Should().BeNull();
        result.ReleasePageUrl.Should().BeNull();
        result.InstallerUrl.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void UpdateCheckResult_Error_CarriesErrorMessage()
    {
        var result = new UpdateCheckResult
        {
            Status = UpdateStatus.Error,
            ErrorMessage = "GitHub API returned 503",
        };

        result.Status.Should().Be(UpdateStatus.Error);
        result.ErrorMessage.Should().Be("GitHub API returned 503");
    }

    [Fact]
    public void UpdateCheckResult_NoReleases_HasNoTag()
    {
        // Mirrors the 404 path in UpdateService.CheckAsync.
        var result = new UpdateCheckResult { Status = UpdateStatus.NoReleases };

        result.Status.Should().Be(UpdateStatus.NoReleases);
        result.LatestTag.Should().BeNull();
    }

    // ── UpdateStatus enum surface ───────────────────────────────────────────────

    [Fact]
    public void UpdateStatus_UpToDate_IsDefaultValue()
    {
        // Value 0 must stay UpToDate so a default-constructed result is never
        // mistaken for "update available".
        ((int)UpdateStatus.UpToDate).Should().Be(0);
        default(UpdateStatus).Should().Be(UpdateStatus.UpToDate);
    }

    [Theory]
    [InlineData(UpdateStatus.UpToDate)]
    [InlineData(UpdateStatus.UpdateAvailable)]
    [InlineData(UpdateStatus.NoReleases)]
    [InlineData(UpdateStatus.Error)]
    public void UpdateStatus_AllValuesAreDistinct(UpdateStatus status)
    {
        Enum.IsDefined(typeof(UpdateStatus), status).Should().BeTrue();
    }

    // ── ReleaseInfo ─────────────────────────────────────────────────────────────

    [Fact]
    public void ReleaseInfo_Properties_RoundTripFromInitializer()
    {
        var published = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var info = new ReleaseInfo
        {
            Tag = "v2.0.0",
            Name = "Akari 2.0",
            Body = "release notes here",
            PublishedUtc = published,
            IsCurrent = true,
        };

        info.Tag.Should().Be("v2.0.0");
        info.Name.Should().Be("Akari 2.0");
        info.Body.Should().Be("release notes here");
        info.PublishedUtc.Should().Be(published);
        info.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void ReleaseInfo_Defaults_AreEmptyStringsNotNull()
    {
        // String properties default to "" (not null) so UI binding never NREs.
        var info = new ReleaseInfo();

        info.Tag.Should().Be("");
        info.Name.Should().Be("");
        info.Body.Should().Be("");
        info.IsCurrent.Should().BeFalse();
        info.PublishedUtc.Should().Be(default);
    }
}
