namespace AkariTool.Core.Features.Apps.Models;

/// <summary>Outcome of a WinGet install/uninstall operation.</summary>
public enum WingetOperationStatus
{
    Succeeded,
    Failed,
    WinGetNotAvailable,
    Cancelled,
}

/// <summary>Result of a package install/uninstall attempt.</summary>
public sealed record WingetOperationResult(
    WingetOperationStatus Status,
    string? ErrorMessage = null,
    int? ExitCode = null)
{
    public bool Success => Status == WingetOperationStatus.Succeeded;

    public static WingetOperationResult Succeeded() => new(WingetOperationStatus.Succeeded);
    public static WingetOperationResult Failed(string message, int? exitCode = null) => new(WingetOperationStatus.Failed, message, exitCode);
    public static WingetOperationResult NotAvailable(string message) => new(WingetOperationStatus.WinGetNotAvailable, message);
    public static WingetOperationResult Cancelled() => new(WingetOperationStatus.Cancelled);
}

/// <summary>
/// Install progress report. Percent is -1 when indeterminate (winget hasn't
/// reported progress yet); Message is a human-readable phase description.
/// </summary>
public sealed record WingetInstallProgress(
    int Percent,
    string Message);
