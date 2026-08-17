namespace AkariTool.Core.Features.AkariOS.Models;

public sealed record BcdOperation
{
    public required string Element { get; init; }
    public required string Value { get; init; }
}
