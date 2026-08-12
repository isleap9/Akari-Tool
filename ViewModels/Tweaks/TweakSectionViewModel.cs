using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariTool.Services;
using AkariTool.Tabs;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// One collapsible section card: a title, its rows, and the bulk-action bar
/// ("N pending" pill + ★ Recommended + ⊞ Defaults).
///
/// Replaces net8's <c>TweakHelpers.BuildSection</c> + <c>AttachBulkActions</c> +
/// the two static dictionaries (_sectionEntries / _sectionPillRefresh) that mapped
/// a StackPanel back to its rows. In MVVM the section OWNS its rows, so those
/// dictionaries — and the visual-tree walk in GetEntriesUnder — disappear.
/// </summary>
public sealed partial class TweakSectionViewModel : ObservableObject
{
    private readonly TweakDialogs _dialogs;

    public TweakSectionViewModel(string title, IEnumerable<TweakItemViewModel> items, TweakDialogs dialogs)
    {
        Title = title;
        _dialogs = dialogs;

        foreach (var item in items)
        {
            item.Changed += RefreshPendingPill;
            Items.Add(item);
        }

        RefreshPendingPill();
    }

    public string Title { get; }

    public ObservableCollection<TweakItemViewModel> Items { get; } = new();

    /// <summary>(Definition, refresh) pairs in the shape the shared bulk predicates expect.</summary>
    public IEnumerable<(TweakDefinition Def, Action Refresh)> Entries =>
        Items.Select(i => (i.Definition, (Action)i.RefreshFromSystem));

    // ── Search filtering ──────────────────────────────────────────────────────

    /// <summary>False when no row in this section matches the active query.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>
    /// Applies the page's search query. Empty query shows every row and section;
    /// a query hides non-matching rows and hides sections with no hit. Sections are
    /// always expanded (collapse was removed), so there is nothing to force open.
    /// </summary>
    public void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var item in Items) item.IsVisible = true;
            IsVisible = true;
            return;
        }

        int hits = 0;
        foreach (var item in Items)
        {
            item.IsVisible = item.MatchesSearch(query);
            if (item.IsVisible) hits++;
        }

        IsVisible = hits > 0;
    }

    // ── Bulk bar ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The bar is hidden when no row in the section carries a Recommended value
    /// (net8: AttachBulkActions returns early in that case).
    /// </summary>
    public bool HasBulkBar =>
        Items.Any(i => TweakTargets.TryGetRecommendedTarget(i.Definition, out _, out _));

    [ObservableProperty]
    public partial string PendingText { get; set; } = "";

    [ObservableProperty]
    public partial string PendingTooltip { get; set; } = "";

    [ObservableProperty]
    public partial double PendingOpacity { get; set; } = 1.0;

    /// <summary>Recomputes the "N pending" pill. Safe to call from any refresh path.</summary>
    public void RefreshPendingPill()
    {
        int pending = TweakTargets.CollectPending(Entries, useRecommended: true).Count;

        PendingText = pending > 0 ? $"{pending} pending" : "All recommended";
        PendingTooltip = pending > 0
            ? $"{pending} setting{(pending == 1 ? "" : "s")} in this section differ from the recommended value"
            : "Every setting in this section matches its recommended value";
        PendingOpacity = pending > 0 ? 1.0 : 0.6;
    }

    [RelayCommand]
    private Task ApplyRecommendedAsync() => RunBulkAsync(useRecommended: true);

    [RelayCommand]
    private Task ApplyDefaultsAsync() => RunBulkAsync(useRecommended: false);

    /// <summary>
    /// Section-scoped bulk run. Logic carried over from net8's RunBulkAsync,
    /// including the Explorer-restart and drift-baseline batching (so a bulk apply
    /// restarts Explorer exactly once and writes the baseline once) and the
    /// per-tweak try/catch that counts failures instead of aborting the run.
    /// </summary>
    private async Task RunBulkAsync(bool useRecommended)
    {
        var work = TweakTargets.CollectPending(Entries, useRecommended);

        if (work.Count == 0)
        {
            await _dialogs.InfoAsync("Nothing to change",
                useRecommended
                    ? "Every setting in this section already matches its recommended value."
                    : "Every setting in this section already matches its Windows default.");
            return;
        }

        var warned = work
            .Select(w => (w.Def.Name, Warning: TweakTargets.WarningFor(w)))
            .Where(w => w.Warning != null)
            .ToList();

        var msg = $"{(useRecommended ? "Apply recommended values to" : "Reset to Windows defaults")} " +
                  $"{work.Count} setting{(work.Count == 1 ? "" : "s")} in this section?";
        if (warned.Count > 0)
        {
            msg += "\n\nNote — the following carry warnings:\n" +
                   string.Join("\n", warned.Take(5).Select(w => $"  • {w.Name}: {w.Warning}"));
            if (warned.Count > 5) msg += $"\n  …and {warned.Count - 5} more.";
        }

        if (!await _dialogs.ConfirmAsync(useRecommended ? "Apply Recommended" : "Reset to Defaults", msg))
            return;

        int applied = 0, failed = 0;
        ExplorerRestart.BeginBatch();   // coalesce per-tweak Explorer restarts into one
        DriftBaseline.BeginBatch();     // coalesce per-tweak baseline writes into one
        try
        {
            foreach (var w in work)
            {
                try
                {
                    if (w.Def.InputKind == TweakInputKind.Toggle) TweakHelpers.ApplyToggle(w.Def, w.ToggleTarget);
                    else TweakHelpers.ApplyOption(w.Def, w.OptionTarget);
                    applied++;
                }
                catch { failed++; }
                try { w.Refresh(); } catch { }
            }
        }
        finally { DriftBaseline.EndBatch(); ExplorerRestart.EndBatch(); }

        RefreshPendingPill();

        if (failed > 0)
            await _dialogs.InfoAsync("Bulk apply finished",
                $"Applied {applied} setting{(applied == 1 ? "" : "s")}; {failed} failed " +
                "(some tweaks require running Akari Tool as administrator).");
    }
}
