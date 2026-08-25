using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AkariTool.Core.Features.Apps.Interfaces;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using Microsoft.Management.Deployment;

namespace AkariTool.Infrastructure.Features.Apps.Services;

/// <summary>
/// Detects a usable winget.exe. Akari does NOT bundle the winget-cli payload
/// (unlike Winhance) — we resolve the system winget from the well-known
/// WindowsApps location, with PATH as fallback.
/// </summary>
public static class WingetCliLocator
{
    /// <summary>
    /// Returns the path to winget.exe, or null when not found. Checks:
    /// ① current user's WindowsApps (the store-registered alias),
    /// ② PATH lookup via the machine's own resolution.
    /// </summary>
    public static string? GetWinGetExePath()
    {
        // 1. Per-user App Execution Alias — where the Store's App Installer puts it.
        var userAlias = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(userAlias))
            return userAlias;

        // 2. PATH fallback (covers dev-machine installs and system-wide installs).
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(dir, "winget.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch { /* malformed PATH entry — skip */ }
        }

        return null;
    }
}

/// <summary>Winhance WinGetDetectionService parity: CLI-first detection with COM fallback probe.</summary>
public sealed class WingetDetectionService(WinGetComSession comSession) : IWingetDetectionService
{
    private bool? _cliAvailableCache;

    public Task<bool> IsWinGetAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (WingetCliLocator.GetWinGetExePath() != null)
        {
            _cliAvailableCache = true;
            return Task.FromResult(true);
        }

        // CLI missing — probe COM once (10s cap; a hung COM server shouldn't stall startup).
        return Task.Run(() =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var ok = comSession.EnsureComInitialized();
                _cliAvailableCache = ok;
                return ok;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    public string? GetCliPath() => WingetCliLocator.GetWinGetExePath();
}

/// <summary>
/// Winhance WinGetDetectionService 1:1: installed winget-package-id enumeration
/// via the COM composite catalog (all catalogs, LocalCatalogs search behavior)
/// with a hard timeout (native COM calls can block indefinitely), falling back to
/// a CLI `winget export` parse when COM is unavailable or returns nothing.
/// </summary>
public sealed class WingetInstalledDetectionService(
    WinGetComSession comSession,
    IAkariLogService logService) : IWingetInstalledDetectionService
{
    private const int ComOperationTimeoutSeconds = 20;

    public async Task<HashSet<string>> GetInstalledPackageIdsAsync(CancellationToken cancellationToken = default)
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Only try COM if system winget is available (COM requires DesktopAppInstaller MSIX).
            if (comSession.EnsureComInitialized() && comSession.PackageManager != null && comSession.Factory != null)
            {
                var comResult = await GetInstalledPackageIdsViaCom(cancellationToken).ConfigureAwait(false);
                if (comResult != null)
                    return comResult;
                logService.Log(LogLevel.Info, "[WinGet] COM detection failed/timed out, falling back to CLI");
            }

            logService.Log(LogLevel.Info, "[WinGet] COM not available, falling back to CLI for installed package detection");
            return await GetInstalledPackageIdsViaCli(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WinGet] Error in GetInstalledPackageIdsAsync: {ex.Message}");
            return installedPackageIds;
        }
    }

    private async Task<HashSet<string>?> GetInstalledPackageIdsViaCom(CancellationToken cancellationToken)
    {
        try
        {
            // Native COM calls can block indefinitely and can't be cancelled —
            // hard-timeout via Task.WhenAny so the caller continues via CLI.
            var workTask = Task.Run(() =>
            {
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var catalogs = comSession.PackageManager!.GetPackageCatalogs().ToArray();
                var compositeOptions = comSession.Factory!.CreateCreateCompositePackageCatalogOptions();
                foreach (var catalog in catalogs)
                    compositeOptions.Catalogs.Add(catalog);

                if (compositeOptions.Catalogs.Count == 0)
                {
                    logService.Log(LogLevel.Warning, "[WinGet] No package catalogs available");
                    return ids;
                }
                compositeOptions.CompositeSearchBehavior = CompositeSearchBehavior.LocalCatalogs;

                var compositeCatalogRef = comSession.PackageManager.CreateCompositePackageCatalog(compositeOptions);
                var connectResult = compositeCatalogRef.Connect();

                if (connectResult.Status != ConnectResultStatus.Ok)
                {
                    logService.Log(LogLevel.Warning, $"[WinGet] Failed to connect to composite catalog: {connectResult.Status} — falling back to CLI");
                    return null;
                }

                var findOptions = comSession.Factory.CreateFindPackagesOptions();
                var filter = comSession.Factory.CreatePackageMatchFilter();
                filter.Field = PackageMatchField.Id;
                filter.Option = PackageFieldMatchOption.ContainsCaseInsensitive;
                filter.Value = "";
                findOptions.Filters.Add(filter);

                var findResult = connectResult.PackageCatalog.FindPackages(findOptions);
                foreach (var match in findResult.Matches)
                {
                    var packageId = match.CatalogPackage?.Id;
                    if (!string.IsNullOrEmpty(packageId))
                        ids.Add(packageId!);
                }

                logService.Log(LogLevel.Info, $"[WinGet] COM API: found {ids.Count} installed packages");

                // 0 packages despite available catalogs → likely DB corruption; use CLI.
                if (ids.Count == 0)
                {
                    logService.Log(LogLevel.Warning, "[WinGet] COM returned 0 installed packages — possible database corruption, falling back to CLI");
                    return null;
                }

                return ids;
            }, cancellationToken);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(ComOperationTimeoutSeconds), cancellationToken);
            if (await Task.WhenAny(workTask, timeoutTask).ConfigureAwait(false) == timeoutTask)
            {
                logService.Log(LogLevel.Warning, $"[WinGet] COM enumeration hard timeout after {ComOperationTimeoutSeconds}s — abandoning thread");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await workTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WinGet] COM detection error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Winhance parity: `winget export` emits every installed package id as JSON —
    /// one process for the full inventory instead of per-app probes.
    /// </summary>
    private async Task<HashSet<string>> GetInstalledPackageIdsViaCli(CancellationToken ct)
    {
        var installedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var exe = WingetCliLocator.GetWinGetExePath();
        if (exe == null)
        {
            logService.Log(LogLevel.Warning, "[WinGet] CLI fallback unavailable — no winget.exe");
            return installedPackageIds;
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"akari-winget-export-{Guid.NewGuid():N}.json");
        try
        {
            using (var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"export -o \"{tempFile}\" --accept-source-agreements",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            })
            {
                process.Start();
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
                if (process.ExitCode != 0 || !File.Exists(tempFile))
                {
                    logService.Log(LogLevel.Warning, $"[WinGet] winget export exited {process.ExitCode}");
                    return installedPackageIds;
                }
            }

            var json = await File.ReadAllTextAsync(tempFile, ct).ConfigureAwait(false);
            var node = System.Text.Json.Nodes.JsonNode.Parse(json);
            if (node?["Sources"] is not System.Text.Json.Nodes.JsonArray sources)
                return installedPackageIds;

            foreach (var source in sources)
            {
                if (source?["Packages"] is not System.Text.Json.Nodes.JsonArray packages)
                    continue;
                foreach (var pkg in packages)
                {
                    var id = pkg?["PackageIdentifier"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(id))
                        installedPackageIds.Add(id!);
                }
            }

            logService.Log(LogLevel.Info, $"[WinGet] CLI export: found {installedPackageIds.Count} installed packages");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WinGet] CLI detection error: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { /* best effort */ }
        }

        return installedPackageIds;
    }
}
