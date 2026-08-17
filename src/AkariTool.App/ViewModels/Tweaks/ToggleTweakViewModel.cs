using CommunityToolkit.Mvvm.ComponentModel;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// A toggle tweak row.
///
/// SUPPRESS FLAG — the same trap the net8 migration hit: WPF-UI's ToggleSwitch had
/// a Click event that fired only on user interaction; WinUI's ToggleSwitch raises
/// Toggled on programmatic IsOn changes too. Two-way binding has exactly the same
/// hazard — a refresh or a cancel-revert would re-enter Apply. <see cref="_suppress"/>
/// restores "user interaction only" semantics, so programmatic writes are silent.
/// </summary>
public sealed partial class ToggleTweakViewModel : TweakItemViewModel
{
    private bool _suppress;

    public ToggleTweakViewModel(TweakDefinition definition, TweakDialogs dialogs)
        : base(definition, dialogs)
    {
        // Seed the control + badges from the machine WITHOUT invoking Apply.
        bool state = definition.ReadState?.Invoke() ?? false;
        _suppress = true;
        IsOn = state;
        _suppress = false;
        SetBadges(definition.ComputeToggleBadges(state));
    }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    partial void OnIsOnChanged(bool value)
    {
        if (_suppress) return;
        _ = OnUserToggledAsync(value);
    }

    private async Task OnUserToggledAsync(bool newState)
    {
        if (!await Dialogs.ConfirmWarningAsync(Name, Definition.GetToggleWarning(newState)))
        {
            // Cancelled — flip the visual back, apply nothing.
            SetSilently(!newState);
            return;
        }

        TweakHelpers.ApplyToggle(Definition, newState);
        SetBadges(Definition.ComputeToggleBadges(newState));
        RaiseChanged();
    }

    /// <summary>Sets the toggle without re-entering the apply path.</summary>
    private void SetSilently(bool state)
    {
        if (IsOn == state) return;
        _suppress = true;
        IsOn = state;
        _suppress = false;
    }

    public override void RefreshFromSystem()
    {
        bool state = Definition.ReadState?.Invoke() ?? false;
        SetSilently(state);
        SetBadges(Definition.ComputeToggleBadges(state));
    }

    // ── Quick-set ─────────────────────────────────────────────────────────────

    public override bool HasRecommendedQuickSet => Definition.RecommendedState.HasValue;
    public override bool HasDefaultQuickSet     => Definition.DefaultState.HasValue;

    public override string RecommendedTooltip =>
        Definition.RecommendedState is not { } v ? "" :
        Definition.InvertBadgeLabelWording
            ? $"Apply recommended: {(v ? "Off" : "On")}"
            : $"Apply recommended: {(v ? "On" : "Off")}";

    public override string DefaultTooltip =>
        Definition.DefaultState is not { } v ? "" :
        Definition.InvertBadgeLabelWording
            ? $"Apply Windows default: {(v ? "Off" : "On")}"
            : $"Apply Windows default: {(v ? "On" : "Off")}";

    protected override async Task ApplyQuickSetAsync(bool useRecommended)
    {
        var target = useRecommended ? Definition.RecommendedState : Definition.DefaultState;
        if (target is not { } value) return;

        if (!await Dialogs.ConfirmWarningAsync(Name, Definition.GetToggleWarning(value))) return;

        TweakHelpers.ApplyToggle(Definition, value);
        SetSilently(value);
        SetBadges(Definition.ComputeToggleBadges(value));
        RaiseChanged();
    }
}
