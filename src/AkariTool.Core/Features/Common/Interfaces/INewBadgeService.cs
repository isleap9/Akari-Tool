namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Tracks which settings are "new" relative to the user's last-seen app version,
/// so catalog rows tagged with an <c>AddedInVersion</c> can render a NEW badge.
/// </summary>
public interface INewBadgeService
{
    /// <summary>
    /// Computes the NEW-badge baseline from every AddedInVersion tag present in
    /// the loaded catalogs plus the persisted highest-seen version. Call once at
    /// startup, before rows consult <see cref="IsSettingNew"/>.
    /// </summary>
    void Initialize(IEnumerable<string?> allAddedInVersions);

    /// <summary>Whether a setting tagged <paramref name="addedInVersion"/> is newer than the baseline.</summary>
    bool IsSettingNew(string? addedInVersion, string settingId);

    /// <summary>Global kill switch for NEW badges (persisted preference).</summary>
    bool ShowNewBadges { get; set; }
}
