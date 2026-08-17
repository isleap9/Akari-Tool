using CommunityToolkit.Mvvm.ComponentModel;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// A dropdown tweak row.
///
/// Two behaviours carried over deliberately from net8's AddTweakRow:
///  • <b>Cancel reverts to the previous index</b> — hence <see cref="_lastIndex"/>.
///    Without it, a cancelled ComboBox change would leave the UI showing a value
///    the machine does not have.
///  • <b>SelectedIndex = -1 is a real state</b>, not an error: ReadCurrentIndex
///    returning null means the machine's value matches NO listed option (custom
///    locale, vendor-specific index). The row is then left unselected and the
///    "Custom" badge lights. Do not "fix" -1 to 0.
/// </summary>
public sealed partial class DropdownTweakViewModel : TweakItemViewModel
{
    private bool _suppress;
    private int _lastIndex = -1;

    public DropdownTweakViewModel(TweakDefinition definition, TweakDialogs dialogs)
        : base(definition, dialogs)
    {
        Options = definition.Options?.Select(o => o.Label).ToArray() ?? Array.Empty<string>();
        RefreshFromSystem();
    }

    public string[] Options { get; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    partial void OnSelectedIndexChanged(int value)
    {
        if (_suppress) return;
        _ = OnUserSelectedAsync(value);
    }

    private async Task OnUserSelectedAsync(int newIndex)
    {
        if (!await Dialogs.ConfirmWarningAsync(Name, Definition.GetOptionWarning(newIndex)))
        {
            SetSilently(_lastIndex);   // cancelled — revert without re-applying
            return;
        }

        _lastIndex = newIndex;
        TweakHelpers.ApplyOption(Definition, newIndex);
        SetBadges(Definition.ComputeDropdownBadges(newIndex));
        RaiseChanged();
    }

    private void SetSilently(int index)
    {
        _suppress = true;
        SelectedIndex = index;
        _suppress = false;
        _lastIndex = index;
    }

    public override void RefreshFromSystem()
    {
        // null from ReadCurrentIndex = "matches no option": leave unselected.
        int idx = Definition.ReadCurrentIndex?.Invoke() ?? -1;
        SetSilently(Math.Max(-1, idx));
        SetBadges(Definition.ComputeDropdownBadges(SelectedIndex));
    }

    // ── Quick-set ─────────────────────────────────────────────────────────────

    private int RecommendedIndex =>
        Definition.Options is { Length: > 0 } o ? Array.FindIndex(o, x => x.IsRecommended) : -1;

    private int DefaultIndex =>
        Definition.Options is { Length: > 0 } o ? Array.FindIndex(o, x => x.IsDefault) : -1;

    public override bool HasRecommendedQuickSet => RecommendedIndex >= 0;
    public override bool HasDefaultQuickSet     => DefaultIndex >= 0;

    public override string RecommendedTooltip =>
        RecommendedIndex < 0 ? "" : $"Apply recommended: {Definition.Options![RecommendedIndex].Label}";

    public override string DefaultTooltip =>
        DefaultIndex < 0 ? "" : $"Apply Windows default: {Definition.Options![DefaultIndex].Label}";

    protected override async Task ApplyQuickSetAsync(bool useRecommended)
    {
        int idx = useRecommended ? RecommendedIndex : DefaultIndex;
        if (idx < 0) return;

        if (!await Dialogs.ConfirmWarningAsync(Name, Definition.GetOptionWarning(idx))) return;

        TweakHelpers.ApplyOption(Definition, idx);
        SetSilently(idx);
        SetBadges(Definition.ComputeDropdownBadges(idx));
        RaiseChanged();
    }
}
