using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Enums;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Runs PowerShell scripts in-memory via -EncodedCommand.
/// Shells out to Windows PowerShell 5.1.
/// </summary>
public sealed class PowerShellRunner : IPowerShellRunner
{
    private readonly IAkariLogService _log;
    private const string PowerShellPath =
        @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    public PowerShellRunner(IAkariLogService log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task RunScriptInMemoryAsync(string script)
    {
        if (string.IsNullOrWhiteSpace(script)) return;

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo(PowerShellPath,
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            UseShellExecute       = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow        = true,
        };

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start PowerShell process.");

            var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(stdout))
                _log.Log(LogLevel.Info, $"[PowerShell] {stdout.Trim()}");
            if (!string.IsNullOrWhiteSpace(stderr))
                _log.Log(LogLevel.Warning, $"[PowerShell] {stderr.Trim()}");
            if (process.ExitCode != 0)
                _log.Log(LogLevel.Warning, $"[PowerShell] Script exited with code {process.ExitCode}");
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"[PowerShell] Failed to run script: {ex.Message}");
        }
    }
}
