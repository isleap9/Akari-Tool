using AkariTool.Core.Features.Common.Enums;

namespace AkariTool.Core.Features.Common.Models;

/// <summary>
/// One pill in a setting's badge row. <see cref="Kind"/> identifies the pill type;
/// <see cref="IsHighlighted"/> is true when the current value matches the pill's semantic
/// (or unconditionally true for the Preference pill, which is a setting-level attribute);
/// <paramref name="Label"/> and <paramref name="Tooltip"/> are pre-resolved strings
/// the view binds to directly. <see cref="Mode"/> is None for the usual single-pill case
/// and AC/DC for per-mode pills on PowerCfg AC/DC Separate settings with a battery present.
///
/// Winhance parity: opacity is NOT carried on this record — the view derives it from
/// <see cref="IsHighlighted"/> via BoolToDimOpacityConverter.
/// </summary>
public sealed record BadgePillState(
    SettingBadgeKind Kind,
    bool IsHighlighted,
    string Label,
    string Tooltip,
    SettingBadgeMode Mode = SettingBadgeMode.None);
