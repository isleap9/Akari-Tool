using System;
using System.Threading;
using System.Threading.Tasks;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IProcessExecutor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        string fileName,
        string arguments,
        CancellationToken ct = default);

    Task<ProcessExecutionResult> ExecuteWithStreamingAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken ct = default);

    void KillProcessesByName(string processName);

    Task<int?> ShellExecuteAsync(
        string fileName,
        string? arguments = null,
        bool waitForExit = false,
        CancellationToken ct = default);
}