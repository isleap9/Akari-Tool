using System;

namespace AkariTool.Core.Features.Common.Models;

/// <summary>
/// Result of executing a process.
/// </summary>
public sealed record ProcessExecutionResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
}