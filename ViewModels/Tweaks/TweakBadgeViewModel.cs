using AkariTool.Tabs;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// One badge pill on a tweak row (Recommended / Windows Default / Custom /
/// Preference). Data-only: the KIND selects a DataTemplate (see
/// TweakBadgeTemplateSelector) so all colouring stays in XAML with live
/// {ThemeResource} brushes, and IsActive drives the pill's opacity.
///
/// This is a projection of <see cref="TweakBadgePill"/>, which the logic layer
/// computes via TweakDefinition.ComputeToggleBadges / ComputeDropdownBadges. The
/// computation is NOT reimplemented here — the badge math is unchanged from net8.
/// </summary>
public sealed class TweakBadgeViewModel
{
    public TweakBadgeViewModel(TweakBadgePill pill)
    {
        Kind = pill.Kind;
        IsActive = pill.IsActive;
        Label = pill.Label;
        Tooltip = pill.Tooltip;
    }

    public TweakBadgeKind Kind { get; }
    public bool IsActive { get; }
    public string Label { get; }
    public string Tooltip { get; }

    /// <summary>net8 design: active pills at full opacity, inactive at 0.35.</summary>
    public double PillOpacity => IsActive ? 1.0 : 0.35;
}
