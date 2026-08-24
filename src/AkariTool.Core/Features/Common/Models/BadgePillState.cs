using AkariTool.Core.Features.Common.Enums;

namespace AkariTool.Core.Features.Common.Models;

/// <summary>
/// One pill in a setting's badge row. IsHighlighted is true when the current
/// value matches the pill's semantic. PillOpacity drives the XAML opacity
/// binding — highlighted = 1.0, unhighlighted = 0.35.
/// </summary>
public sealed record BadgePillState(
    SettingBadgeKind Kind,
    bool IsHighlighted,
    string Label,
    string Tooltip,
    SettingBadgeMode Mode = SettingBadgeMode.None)
{
    public double PillOpacity => IsHighlighted ? 1.0 : 0.35;
}
