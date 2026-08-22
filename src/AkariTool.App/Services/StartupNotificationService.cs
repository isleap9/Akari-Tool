using System;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using WinUI.Framework.Services;
using LogLevel = AkariTool.Core.Features.Common.Enums.LogLevel;

namespace AkariTool.Services;

/// <summary>
/// First-launch "protect your system" offer (4g — Winhance StartupNotificationService
/// 1:1 port). Shows once per machine: a consent dialog offering to create a system
/// restore point, then runs the creation through the TaskProgressService card.
///
/// Deliberate adaptations from Winhance:
/// - Preference persistence uses the framework ISettingsService (same store as
///   AppTheme) instead of IUserPreferencesService — sync Get/Set fits the startup path.
/// - Dialogs go through TweakDialogs (serialized + XamlRoot fail-safe).
/// - Localization keys inlined as literal English strings (Akari is literal-English).
/// - Copy adapted Winhance → Akari; the config-backup line points at Akari's Backup tab.
/// </summary>
public class StartupNotificationService : IStartupNotificationService
{
    private const string InitialRestorePointOffered = "InitialRestorePointOffered";

    private readonly TweakDialogs _dialogs;
    private readonly ISettingsService _prefs;
    private readonly ISystemBackupService _backupService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IAkariLogService _log;

    public StartupNotificationService(
        TweakDialogs dialogs,
        ISettingsService prefs,
        ISystemBackupService backupService,
        ITaskProgressService taskProgressService,
        IAkariLogService log)
    {
        _dialogs = dialogs;
        _prefs = prefs;
        _backupService = backupService;
        _taskProgressService = taskProgressService;
        _log = log;
    }

    public async Task ShowFirstLaunchRestoreOfferAsync()
    {
        try
        {
            // Check if we've already offered
            if (_prefs.Get(InitialRestorePointOffered, false))
                return;

            // Mark as offered immediately so we don't show again even if something fails
            // (Winhance behavior — deliberate).
            _prefs.Set(InitialRestorePointOffered, true);

            // Build the consent dialog message (Winhance en.json copy, Akari-adapted)
            var message =
                "Welcome to Akari Tool! Before making any changes, we recommend protecting your system." + "\n\n"
                + "A backup config of your current Windows settings has been automatically saved. You can restore it anytime from the Backup tab." + "\n\n"
                + "It is highly recommended to also create a System Restore point. This allows you to roll back all system-level changes made by Windows or Akari Tool." + "\n\n"
                + "If you skip this now, you can create a restore point any time from the Quick Actions on any tweak tab. However, creating a restore point now is advised as it captures the state of your system before Akari Tool was first used.";

            var confirmed = await _dialogs.ConfirmWithButtonsAsync(
                "System Protection",
                message,
                primaryText: "Create Restore Point",
                secondaryText: "Skip").ConfigureAwait(true);

            if (confirmed)
            {
                _log.Log(LogLevel.Info, "User chose to create restore point on first launch");

                // Use TaskProgressService so the main window progress card shows status
                var cts = _taskProgressService.StartTask(
                    "Creating system restore point...",
                    isIndeterminate: true);
                var progress = _taskProgressService.CreateDetailedProgress();

                try
                {
                    var result = await _backupService.CreateRestorePointAsync(
                        progress: progress, cancellationToken: cts.Token).ConfigureAwait(true);

                    if (result.Success && result.RestorePointCreated)
                    {
                        await _dialogs.InfoAsync(
                            "Restore Point Created",
                            "A System Restore point has been successfully created. You can revert to this point anytime by searching 'Create a restore point' in Windows Settings.").ConfigureAwait(true);
                    }
                    else
                    {
                        var failMsg = "Failed to create a System Restore point. You can try again later from Akari Tool."
                            + (result.ErrorMessage != null ? $"\n\n{result.ErrorMessage}" : "");
                        await _dialogs.InfoAsync("Restore Point Failed", failMsg).ConfigureAwait(true);
                    }
                }
                finally
                {
                    _taskProgressService.CompleteTask();
                }
            }
            else
            {
                _log.Log(LogLevel.Info, "User skipped restore point creation on first launch");
            }
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"Error showing first launch restore offer: {ex.Message}");
        }
    }
}
