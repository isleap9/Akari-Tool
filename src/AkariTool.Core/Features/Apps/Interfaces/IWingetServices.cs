using System;
using System.Threading;
using System.Threading.Tasks;
using AkariTool.Core.Features.Apps.Models;

namespace AkariTool.Core.Features.Apps.Interfaces;

/// <summary>
/// Detects whether winget is usable on this system (CLI present or COM API
/// reachable) and reports the best available execution path.
/// </summary>
public interface IWingetDetectionService
{
    /// <summary>True when winget can install packages (CLI found or COM ready).</summary>
    Task<bool> IsWinGetAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>Path to a usable winget.exe, or null when only COM is available.</summary>
    string? GetCliPath();
}

/// <summary>
/// Winhance IWinGetDetectionService parity: enumerates installed winget package
/// ids via the COM composite catalog with CLI `winget export` fallback.
/// </summary>
public interface IWingetInstalledDetectionService
{
    Task<System.Collections.Generic.HashSet<string>> GetInstalledPackageIdsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Installs/repairs the App Installer (winget) package when missing, then waits
/// for the COM API to become ready. Winhance WinGetBootstrapper parity.
/// </summary>
public interface IWingetBootstrapper
{
    /// <summary>Raised once after a successful bootstrap makes COM ready.</summary>
    event EventHandler? WingetInstalled;

    /// <summary>
    /// Ensures winget is installed and COM-initialized. Returns true when winget
    /// is usable afterwards (either it already was, or bootstrap succeeded).
    /// </summary>
    Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Package install/uninstall via the WinGet COM API with CLI fallback.
/// Reports granular progress suitable for the existing task-progress card.
/// </summary>
public interface IWingetPackageInstaller
{
    Task<WingetOperationResult> InstallAsync(
        string packageId,
        IProgress<WingetInstallProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool useMsStoreSource = false);

    Task<WingetOperationResult> UninstallAsync(
        string packageId,
        IProgress<WingetInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
