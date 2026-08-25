using System;
using System.Threading;
using System.Threading.Tasks;
using AkariTool.Core.Features.Apps.Interfaces;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Apps.Services;

/// <summary>
/// Winhance WinGetBootstrapper parity (adapted): when winget is missing entirely,
/// installs App Installer via a Store-triggered PowerShell command, then polls the
/// COM API until ready (10 × 3 s). When winget exists, this is a no-op success.
/// </summary>
public sealed class WingetBootstrapper(
    WinGetComSession comSession,
    IAkariLogService logService) : IWingetBootstrapper
{
    private volatile bool _systemWinGetAvailable;

    /// <summary>Raised once after a successful bootstrap makes COM ready.</summary>
    public event EventHandler? WingetInstalled;

    public async Task<bool> EnsureWinGetReadyAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: CLI present → usable.
        if (WingetCliLocator.GetWinGetExePath() != null)
        {
            _systemWinGetAvailable = true;
            return true;
        }

        logService.Log(LogLevel.Info, "[WinGet] winget not found — attempting App Installer install...");

        var installOk = await TryInstallAppInstallerAsync(cancellationToken).ConfigureAwait(false);
        if (!installOk)
        {
            logService.Log(LogLevel.Error, "[WinGet] App Installer installation failed.");
            return false;
        }

        // Poll COM readiness: 10 attempts, 3s apart (Winhance loop).
        for (int i = 0; i < 10; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(3000, cancellationToken).ConfigureAwait(false);

            comSession.ResetFactory();
            if (await Task.Run(comSession.EnsureComInitialized, cancellationToken).ConfigureAwait(false))
            {
                logService.Log(LogLevel.Info, $"[WinGet] COM API ready after {i + 1} attempt(s)");
                _systemWinGetAvailable = true;
                WingetInstalled?.Invoke(this, EventArgs.Empty);
                return true;
            }

            logService.Log(LogLevel.Info, $"[WinGet] COM init attempt {i + 1}/10 failed, retrying...");
        }

        logService.Log(LogLevel.Warning, "[WinGet] COM API did not become ready after App Installer installation");
        _systemWinGetAvailable = WingetCliLocator.GetWinGetExePath() != null;
        return _systemWinGetAvailable;
    }

    /// <summary>
    /// Installs App Installer without a bundled payload: asks the Store to install
    /// via its protocol handler. Works on stock Windows 11 where ms-windows-store
    /// resolves; fails gracefully on stripped images.
    /// </summary>
    private static async Task<bool> TryInstallAppInstallerAsync(CancellationToken ct)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1", // App Installer
                    UseShellExecute = true,
                },
            };
            process.Start();

            // The Store page opens; actual package install is user-driven there.
            // We can't force it silently without a bundled copy — report and let the
            // readiness poll decide.
            await Task.Delay(1000, ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
