namespace AkariTool.Core.Features.AkariOS.Models;

public sealed record PlaybookTweakAction
{
    public required string ActionType { get; init; }
    public required string Target { get; init; }
}
