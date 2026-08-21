using System;
using System.Collections.Generic;
using System.Reflection;
using AkariTool.Core.Features.Common.Constants;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using WinUI.Framework.Services;
using LogLevel = AkariTool.Core.Features.Common.Enums.LogLevel;

namespace AkariTool.Services;

/// <summary>
/// Port of Winhance's NewBadgeService: decides which catalog rows render a NEW
/// badge by comparing each row's AddedInVersion tag against a persisted baseline.
///
/// Baseline rules (identical to Winhance):
///  - Uninitialized / half-populated state → baseline 0.0.0, every tagged row is
///    NEW, both keys get seeded so the next launch has a consistent pair.
///  - Effective upgrade (catalog highest &gt; stored highest) → baseline = stored
///    highest, ShowNewBadges reset to true.
///  - No upgrade → keep the stored baseline so badges persist across launches.
///
/// Lives in the App project (not Infrastructure) because it depends on the
/// vendored ISettingsService, which Infrastructure does not reference.
/// </summary>
public class NewBadgeService : INewBadgeService
{
    private readonly ISettingsService _settings;
    private readonly IAkariLogService _log;
    private Version _baseline = new(99, 99, 99);

    public NewBadgeService(ISettingsService settings, IAkariLogService log)
    {
        _settings = settings;
        _log = log;
    }

    public bool ShowNewBadges
    {
        get => _settings.Get(UserPreferenceKeys.ShowNewBadges, true);
        set => _settings.Set(UserPreferenceKeys.ShowNewBadges, value);
    }

    public void Initialize(IEnumerable<string?> allAddedInVersions)
    {
        // Keep writing LastRunVersion for future migration use — it no longer drives badges.
        var currentAssemblyVersion = GetAppVersion();
        _settings.Set("LastRunVersion", currentAssemblyVersion);

        // Compute the highest AddedInVersion present in the loaded catalogs.
        Version? highestInRegistry = null;
        if (allAddedInVersions != null)
        {
            foreach (var raw in allAddedInVersions)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                if (!TryParseVersion(raw, out var parsed))
                    continue;
                if (highestInRegistry is null || parsed > highestInRegistry)
                    highestInRegistry = parsed;
            }
        }

        var storedHighestStr = _settings.Get(UserPreferenceKeys.HighestSeenAddedInVersion, "");
        var storedBaselineStr = _settings.Get("NewBadgeBaseline", "");

        // Branch A: uninitialized state — first-ever install, returning user whose
        // preferences predate the badge system, OR a half-populated state where one
        // of the two keys is missing (or unparseable). All roads lead to: baseline =
        // 0.0.0, every tagged setting renders as NEW, both keys get seeded so the
        // next launch has a consistent pair.
        var highestOk = TryParseVersion(storedHighestStr, out var storedHighest);
        var baselineOk = TryParseVersion(storedBaselineStr, out var storedBaseline);
        if (!highestOk || !baselineOk)
        {
            _baseline = new Version(0, 0, 0);
            if (highestInRegistry is not null)
            {
                _settings.Set(
                    UserPreferenceKeys.HighestSeenAddedInVersion,
                    VersionToString(highestInRegistry));
            }
            _settings.Set("NewBadgeBaseline", VersionToString(_baseline));
            // Do NOT touch ShowNewBadges — leave whatever the user already has.
            _log.Log(LogLevel.Info,
                "[NewBadge] Uninitialized or half-populated state. Baseline set to 0.0.0 (all tagged settings treated as new).");
            return;
        }

        // Branch B: effective upgrade detected — new settings added to the catalogs since last run.
        if (highestInRegistry is not null && highestInRegistry > storedHighest)
        {
            _baseline = storedHighest;
            _settings.Set(
                UserPreferenceKeys.HighestSeenAddedInVersion,
                VersionToString(highestInRegistry));
            _settings.Set("NewBadgeBaseline", VersionToString(storedHighest));
            ShowNewBadges = true;
            _log.Log(LogLevel.Info,
                $"[NewBadge] Effective upgrade: catalog highest {highestInRegistry} > stored {storedHighest}. " +
                $"Baseline={storedHighest}; ShowNewBadges reset to true.");
            return;
        }

        // Branch C: no upgrade since last run — use the stored NewBadgeBaseline so NEW
        // badges persist across app launches until the next upgrade.
        _baseline = storedBaseline;
        _log.Log(LogLevel.Debug,
            $"[NewBadge] No upgrade. Baseline={_baseline}, ShowNewBadges={ShowNewBadges}.");
    }

    public bool IsSettingNew(string? addedInVersion, string settingId)
    {
        if (string.IsNullOrEmpty(addedInVersion))
            return false;

        if (!TryParseVersion(addedInVersion, out var settingVersion))
            return false;

        return settingVersion > _baseline;
    }

    private static string GetAppVersion()
    {
        var attr = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = attr?.InformationalVersion ?? "0.0.0";
        // Strip leading 'v' and any '+commithash' suffix
        version = version.TrimStart('v');
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];
        return version;
    }

    private static bool TryParseVersion(string versionStr, out Version parsed)
    {
        if (string.IsNullOrWhiteSpace(versionStr))
        {
            parsed = new Version(0, 0, 0);
            return false;
        }
        versionStr = versionStr.Trim().TrimStart('v');
        return Version.TryParse(versionStr, out parsed!);
    }

    private static string VersionToString(Version v)
    {
        // Version.Build is -1 when not specified; normalise to 0.
        var build = v.Build < 0 ? 0 : v.Build;
        return $"{v.Major}.{v.Minor}.{build}";
    }
}
