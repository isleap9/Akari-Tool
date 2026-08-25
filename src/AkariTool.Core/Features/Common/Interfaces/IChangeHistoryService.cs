namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Winhance IChangeHistoryService 1:1: appends user-facing entries to
/// ChangeHistory.txt — the plain-language receipt of every change Akari makes.
/// Implementations MUST never throw: a failed history write logs a warning and
/// the actual operation proceeds.
/// </summary>
public interface IChangeHistoryService
{
    /// <summary>One entry: "[ts] {group} — {name}: {before} → {after}" (group omitted when null).</summary>
    void LogSettingChange(string displayName, string? groupName, string before, string after);

    /// <summary>One entry for an Action-type setting that ran: "[ts] {group} — {name}".</summary>
    void LogSettingAction(string displayName, string? groupName);

    /// <summary>One entry: "[ts] App installed|App removed: {appName}".</summary>
    void LogAppChange(string appDisplayName, AppChangeKind kind);

    /// <summary>
    /// Starts a batch (config import, bulk action). The header line is written lazily
    /// when the first entry inside the batch arrives; entries inside are indented.
    /// Dispose the return value to end the batch. Nested batches join the outermost one.
    /// </summary>
    IDisposable BeginBatch(string header);

    /// <summary>Ensures the file exists (writing the header if creating) and returns its full path. Never throws.</summary>
    string GetFilePath();
}

/// <summary>Whether an app install/uninstall was recorded.</summary>
public enum AppChangeKind
{
    Installed,
    Removed,
}
