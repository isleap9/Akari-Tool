namespace AkariTool.Core.Features.Common.Models;

/// <summary>
/// A power plan (scheme) on the system, as enumerated by
/// <see cref="Interfaces.IPowerSettingsQueryService"/>.
/// </summary>
public record PowerPlan
{
    public string Name { get; init; } = string.Empty;
    public string Guid { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}