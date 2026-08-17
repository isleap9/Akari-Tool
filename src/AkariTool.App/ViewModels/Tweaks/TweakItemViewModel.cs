using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Base view model for one rendered <see cref="TweakDefinition"/> row. Replaces the
/// net8 factory's <c>TweakHelpers.AddTweakRow</c>: instead of imperatively building
/// a control tree and returning a refresh closure, the row is a bindable object and
/// the DataTemplate does the layout.
///
/// The behavioural contract that AddTweakRow established is preserved exactly:
///  • the interaction handler awaits the warning confirmation BEFORE applying, and
///    reverts the visual when the user cancels;
///  • every apply routes through TweakHelpers.ApplyToggle / ApplyOption, so the
///    drift baseline still sees every write from one place;
///  • the row registers itself with TweakRegistry, passing its refresh delegate, so
///    Backup/Restore and search keep working;
///  • the row raises <see cref="Changed"/> after a successful apply so its section
///    can recompute the "N pending" pill (net8: NotifySectionChanged).
/// </summary>
public abstract partial class TweakItemViewModel : ObservableObject
{
    protected readonly TweakDialogs Dialogs;

    protected TweakItemViewModel(TweakDefinition definition, TweakDialogs dialogs)
    {
        Definition = definition;
        Dialogs = dialogs;
    }

    public TweakDefinition Definition { get; }

    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string Description => Definition.Description;

    /// <summary>Badge pills, recomputed on every state change.</summary>
    public ObservableCollection<TweakBadgeViewModel> Badges { get; } = new();

    /// <summary>False when the row is filtered out by the page's search box.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>Raised after a successful apply so the section can refresh its pill.</summary>
    public event Action? Changed;

    protected void RaiseChanged() => Changed?.Invoke();

    // ── Quick-set buttons (★ recommended / ⊞ Windows default) ─────────────────

    public abstract bool HasRecommendedQuickSet { get; }
    public abstract bool HasDefaultQuickSet { get; }
    public abstract string RecommendedTooltip { get; }
    public abstract string DefaultTooltip { get; }

    [RelayCommand]
    private Task ApplyRecommendedAsync() => ApplyQuickSetAsync(useRecommended: true);

    [RelayCommand]
    private Task ApplyDefaultAsync() => ApplyQuickSetAsync(useRecommended: false);

    protected abstract Task ApplyQuickSetAsync(bool useRecommended);

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Re-reads the tweak from the system and updates the control + badges without
    /// applying anything. This is the delegate handed to TweakRegistry.Register, so
    /// a settings import refreshes the row live (net8: the refreshBadges closure).
    /// </summary>
    public abstract void RefreshFromSystem();

    protected void SetBadges(TweakBadgePill[] pills)
    {
        Badges.Clear();
        foreach (var p in pills) Badges.Add(new TweakBadgeViewModel(p));
        OnPropertyChanged(nameof(HasBadges));
    }

    public bool HasBadges => Badges.Count > 0;

    // ── Search ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Matches the net8 row search tag ("search:{Name}|{Description}") that
    /// BaseTab.ApplySearch filtered on.
    /// </summary>
    public bool MatchesSearch(string query) =>
        Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(query, StringComparison.OrdinalIgnoreCase);
}
