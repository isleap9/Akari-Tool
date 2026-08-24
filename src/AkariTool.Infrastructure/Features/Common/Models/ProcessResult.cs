namespace AkariTool.Infrastructure.Features.Common.Models;

public sealed record ProcessResult
{
    public int ExitCode { get; init; }
    public string StandardError { get; init; } = string.Empty;
}
