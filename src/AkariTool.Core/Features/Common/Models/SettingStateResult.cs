namespace AkariTool.Core.Features.Common.Models;

/// <summary>
/// Interpreted state for one setting, produced by SystemSettingsDiscoveryService.
/// Winhance SettingStateResult 1:1: raw values are read in a batched discovery pass,
/// then interpreted (IsEnabled / CurrentValue) per setting; Success=false carries the
/// failure reason without throwing to callers.
/// </summary>
public sealed record SettingStateResult
{
    public bool IsEnabled { get; init; }
    public object? CurrentValue { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, object?>? RawValues { get; init; }
    public SettingTooltipData? TooltipData { get; init; }
}
