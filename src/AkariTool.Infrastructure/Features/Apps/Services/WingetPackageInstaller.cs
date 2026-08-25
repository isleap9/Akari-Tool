using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AkariTool.Core.Features.Apps.Interfaces;
using AkariTool.Core.Features.Apps.Models;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using Microsoft.Management.Deployment;

namespace AkariTool.Infrastructure.Features.Apps.Services;

/// <summary>
/// Package install/uninstall via the WinGet COM API (live progress) with CLI
/// fallback when COM is unavailable. Winhance WinGetPackageInstaller shape,
/// adapted to Akari's WingetOperationResult contract.
///
/// COM install flow: open the pre-configured package catalog → find package by
/// id → DownloadAndInstallAsync with an IInstallProgress reporting Action.
/// </summary>
public sealed class WingetPackageInstaller(
    WinGetComSession comSession,
    IAkariLogService logService) : IWingetPackageInstaller
{
    private const int ComInitTimeoutSeconds = 8;

    public async Task<WingetOperationResult> InstallAsync(
        string packageId,
        IProgress<WingetInstallProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool useMsStoreSource = false)
    {
        if (!await EnsureReadyAsync(cancellationToken).ConfigureAwait(false))
            return WingetOperationResult.NotAvailable("winget is not installed and could not be bootstrapped.");

        // COM path first.
        try
        {
            return await Task.Run(async () =>
                await InstallViaComAsync(packageId, progress, cancellationToken, useMsStoreSource)
                    .ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return WingetOperationResult.Cancelled();
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[WinGet] COM install failed ({ex.Message}) — falling back to CLI.");
        }

        // CLI fallback.
        return await InstallViaCliAsync(packageId, progress, cancellationToken, useMsStoreSource).ConfigureAwait(false);
    }

    public async Task<WingetOperationResult> UninstallAsync(
        string packageId,
        IProgress<WingetInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!await EnsureReadyAsync(cancellationToken).ConfigureAwait(false))
            return WingetOperationResult.NotAvailable("winget is not installed and could not be bootstrapped.");

        try
        {
            return await Task.Run(async () =>
                await UninstallViaComAsync(packageId, progress, cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return WingetOperationResult.Cancelled();
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[WinGet] COM uninstall failed ({ex.Message}) — falling back to CLI.");
        }

        var exit = await RunCliAsync($"uninstall -e --id {packageId} --silent --disable-interactivity --accept-source-agreements", null, cancellationToken).ConfigureAwait(false);
        return exit == 0
            ? WingetOperationResult.Succeeded()
            : WingetOperationResult.Failed($"winget uninstall exited 0x{exit:X}", exit);
    }

    // ── Readiness ───────────────────────────────────────────────────────────────

    private async Task<bool> EnsureReadyAsync(CancellationToken ct)
    {
        var cli = WingetCliLocator.GetWinGetExePath();
        if (cli != null) return true;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(ComInitTimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            return await Task.Run(comSession.EnsureComInitialized, linked.Token).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    // ── COM paths ───────────────────────────────────────────────────────────────

    private async Task<WingetOperationResult> InstallViaComAsync(
        string packageId, IProgress<WingetInstallProgress>? progress, CancellationToken ct, bool useMsStore)
    {
        var manager = comSession.PackageManager
            ?? throw new InvalidOperationException("COM not initialized");

        progress?.Report(new WingetInstallProgress(-1, "Opening package catalog…"));

        var catalogRef = manager.GetPredefinedPackageCatalog(PredefinedPackageCatalog.OpenWindowsCatalog);
        var connect = await Task.Run(() => catalogRef.Connect(), ct).ConfigureAwait(false);
        if (connect.Status != ConnectResultStatus.Ok)
            throw new InvalidOperationException($"Catalog connect failed: {connect.Status}");

        var options = comSession.Factory!.CreateFindPackagesOptions();
        var filter = comSession.Factory.CreatePackageMatchFilter();
        filter.Field = PackageMatchField.Id;
        filter.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
        filter.Value = packageId;
        options.Filters.Add(filter);

        progress?.Report(new WingetInstallProgress(-1, $"Searching for '{packageId}'…"));
        var findResult = await Task.Run(() => connect.PackageCatalog.FindPackages(options), ct).ConfigureAwait(false);
        var matches = findResult.Matches.ToArray();

        if (matches.Length == 0)
            return WingetOperationResult.Failed($"Package '{packageId}' not found in the catalog.");

        // First match wins (exact-id filter makes this deterministic in practice).
        var package = matches[0].CatalogPackage;
        if (package == null)
            return WingetOperationResult.Failed($"Package '{packageId}' matched but returned no catalog entry.");

        var installOptions = comSession.Factory.CreateInstallOptions();
        installOptions.AcceptPackageAgreements = true;

        progress?.Report(new WingetInstallProgress(0, $"Installing '{package.Id}'…"));

        var installResult = await manager.InstallPackageAsync(package, installOptions)
            .AsTask(ct, new Progress<InstallProgress>(p =>
            {
                int pct = p.DownloadProgress is > 0 and <= 1.0
                    ? (int)(p.DownloadProgress * 100)
                    : -1;
                progress?.Report(new WingetInstallProgress(pct, p.State.ToString()));
            })).ConfigureAwait(false);

        return installResult.Status switch
        {
            InstallResultStatus.Ok => WingetOperationResult.Succeeded(),
            _ => WingetOperationResult.Failed($"Install ended with status: {installResult.Status}"),
        };
    }

    private async Task<WingetOperationResult> UninstallViaComAsync(
        string packageId, IProgress<WingetInstallProgress>? progress, CancellationToken ct)
    {
        var manager = comSession.PackageManager
            ?? throw new InvalidOperationException("COM not initialized");

        var catalogRef = manager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages);
        var connect = await Task.Run(() => catalogRef.Connect(), ct).ConfigureAwait(false);
        if (connect.Status != ConnectResultStatus.Ok)
            throw new InvalidOperationException($"Local catalog connect failed: {connect.Status}");

        var options = comSession.Factory!.CreateFindPackagesOptions();
        var filter = comSession.Factory.CreatePackageMatchFilter();
        filter.Field = PackageMatchField.Id;
        filter.Option = PackageFieldMatchOption.EqualsCaseInsensitive;
        filter.Value = packageId;
        options.Filters.Add(filter);

        var findResult = await Task.Run(() => connect.PackageCatalog.FindPackages(options), ct).ConfigureAwait(false);
        var matches = findResult.Matches.ToArray();
        if (matches.Length == 0)
            return WingetOperationResult.Failed($"Installed package '{packageId}' not found.");

        var package = matches[0].CatalogPackage;
        if (package == null)
            return WingetOperationResult.Failed($"Installed package '{packageId}' matched but returned no entry.");

        var uninstallOptions = comSession.Factory.CreateUninstallOptions();

        progress?.Report(new WingetInstallProgress(-1, $"Uninstalling '{package.Id}'…"));
        var result = await manager.UninstallPackageAsync(package, uninstallOptions).AsTask(ct).ConfigureAwait(false);

        return result.Status switch
        {
            UninstallResultStatus.Ok => WingetOperationResult.Succeeded(),
            _ => WingetOperationResult.Failed($"Uninstall ended with status: {result.Status}"),
        };
    }

    // ── CLI fallback ────────────────────────────────────────────────────────────

    private static async Task<WingetOperationResult> InstallViaCliAsync(
        string packageId, IProgress<WingetInstallProgress>? progress, CancellationToken ct, bool useMsStore)
    {
        var args = useMsStore
            ? $"install --id {packageId} --source msstore --silent --accept-source-agreements --accept-package-agreements --disable-interactivity"
            : $"install -e --id {packageId} --silent --accept-source-agreements --accept-package-agreements --disable-interactivity";

        var exit = await RunCliAsync(args, progress, ct).ConfigureAwait(false);
        return exit == 0
            ? WingetOperationResult.Succeeded()
            : WingetOperationResult.Failed($"winget install exited 0x{exit:X}", exit);
    }

    private static async Task<int> RunCliAsync(string args, IProgress<WingetInstallProgress>? progress, CancellationToken ct)
    {
        var exe = WingetCliLocator.GetWinGetExePath() ?? "winget";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        process.Start();

        while (!process.StandardOutput.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line)) continue;
            // Coarse phase reporting from winget's textual output.
            progress?.Report(new WingetInstallProgress(-1, line.Trim()));
        }

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
    }
}
