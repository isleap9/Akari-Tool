using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.Services;
using AkariTool.Tabs;
using WinUI.Framework.Mvvm;
using WinUI.Framework.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Tweaks;

/// <summary>
/// Base view model for a tweak-list page. Replaces net8's <c>BaseTab</c>:
/// page header, per-page search, the Quick Actions menu, and the
/// <see cref="TweakRegistry"/> bracketing all live here, so a concrete tab is
/// reduced to "name the sections and hand over the catalog arrays".
///
/// ⚠ LIFETIME: concrete page view models MUST be registered as SINGLETONS. Rows
/// register themselves with TweakRegistry on construction, and TweakRegistry has
/// no unregister. A transient view model would re-register the whole tab on every
/// navigation, inflating the tweak count, duplicating Backup/Restore entries and
/// breaking the contiguous index range ClaimRange depends on. Pages that host one
/// also set NavigationCacheMode="Required" so the Frame reuses the page instance.
/// </summary>
public abstract partial class TweakPageViewModel : ViewModelBase
{
    protected readonly TweakDialogs Dialogs;
    protected readonly ToolService Tool;

    // Build() runs either from the startup warm-up (a background thread) or from
    // the page constructor (the UI thread) — whichever reaches the tab first. The
    // lock makes the two mutually exclusive so a navigation landing mid-warm-up
    // waits and then no-ops, instead of a second Build() racing TweakRegistry's
    // index ranges. _built is volatile + set LAST so the fast-path reader never
    // sees "built" before Sections is fully populated.
    private readonly object _buildLock = new();
    private volatile bool _built;

    protected TweakPageViewModel(TweakDialogs dialogs, ToolService tool)
    {
        Dialogs = dialogs;
        Tool = tool;
    }

    /// <summary>The rail tag this page is reached by — also the TweakRegistry range tag.</summary>
    public abstract string NavTag { get; }

    /// <summary>The human label used in search results and bulk dialogs.</summary>
    public abstract string NavLabel { get; }

    public ObservableCollection<TweakSectionViewModel> Sections { get; } = new();

    /// <summary>Returns each section as (title, definitions), in render order.</summary>
    protected abstract IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog();

    /// <summary>
    /// Builds every section and brackets the whole tab in TweakRegistry.
    ///
    /// The Mark() / ClaimRange() pair is what attributes a contiguous block of
    /// registered rows to this tab — global search uses it to name the destination,
    /// and Backup/Restore relies on the same registration list. Rows register
    /// inside the item view model constructors, i.e. strictly between these two
    /// calls. Do not add registrations outside the bracket.
    /// </summary>
    public void Build()
    {
        if (_built) return;                    // fast path once built — no lock

        lock (_buildLock)
        {
            if (_built) return;                // double-checked under the lock

            int start = TweakRegistry.Mark();

            foreach (var (title, tweaks) in BuildCatalog())
            {
                var items = new List<TweakItemViewModel>(tweaks.Length);
                foreach (var def in tweaks)
                {
                    TweakItemViewModel item = def.InputKind == TweakInputKind.Dropdown && def.Options is { Length: > 0 }
                        ? new DropdownTweakViewModel(def, Dialogs)
                        : new ToggleTweakViewModel(def, Dialogs);

                    TweakRegistry.Register(def, item.RefreshFromSystem);
                    items.Add(item);
                }

                if (items.Count == 0) continue;   // hardware-gated section (e.g. Battery on a desktop)
                Sections.Add(new TweakSectionViewModel(title, items, Dialogs));
            }

            TweakRegistry.ClaimRange(NavTag, NavLabel, start);

            // Registration count per tab — the net8 build tracked this globally ("433
            // tweaks tracked") and it is the cheapest guard against a tab silently
            // registering twice or not at all.
            Tool.Log($"[{NavTag}] {TweakRegistry.Count - start} tweaks registered " +
                     $"in {Sections.Count} sections (registry total {TweakRegistry.Count}).");

            // HEADLESS-EVENT SUBSCRIBER #2: TweakRegistry.SectionsNeedRefresh. Raised
            // after a settings import applies tweaks; without a subscriber the section
            // pills keep showing pre-import counts and nothing looks wrong until the
            // user interacts. Rows refresh themselves via their registered delegate;
            // this call updates the aggregate pills.
            TweakRegistry.SectionsNeedRefresh += OnSectionsNeedRefresh;

            RefreshQuickActionCounts();

            _built = true;                     // set LAST: a concurrent caller waited, now no-ops
        }
    }

    private void OnSectionsNeedRefresh()
    {
        foreach (var s in Sections) s.RefreshPendingPill();
        RefreshQuickActionCounts();
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value)
    {
        foreach (var s in Sections) s.ApplySearch(value);
    }

    // ── Quick Actions (tab scope) ─────────────────────────────────────────────

    [ObservableProperty]
    public partial int RecommendedPendingCount { get; set; }

    [ObservableProperty]
    public partial int DefaultPendingCount { get; set; }

    public string RecommendedPendingSubtitle =>
        $"{RecommendedPendingCount} tweak{(RecommendedPendingCount == 1 ? "" : "s")} differ on this tab";

    public string DefaultPendingSubtitle =>
        $"Reset every tweak on this tab — {DefaultPendingCount} differ";

    partial void OnRecommendedPendingCountChanged(int value)
    {
        OnPropertyChanged(nameof(RecommendedPendingSubtitle));
        ApplyAllRecommendedCommand.NotifyCanExecuteChanged();
    }

    partial void OnDefaultPendingCountChanged(int value)
    {
        OnPropertyChanged(nameof(DefaultPendingSubtitle));
        RestoreDefaultsCommand.NotifyCanExecuteChanged();
    }

    private IEnumerable<(TweakDefinition Def, Action Refresh)> AllEntries =>
        Sections.SelectMany(s => s.Entries);

    /// <summary>
    /// Recomputes both counts using the SAME predicate the bulk engine runs, so
    /// the menu can never advertise work the run then skips. Called when the
    /// Quick Actions flyout opens (net8 computed the counts on open too).
    /// </summary>
    [RelayCommand]
    public void RefreshQuickActionCounts()
    {
        var entries = AllEntries.ToList();
        RecommendedPendingCount = TweakTargets.CollectPending(entries, useRecommended: true).Count;
        DefaultPendingCount = TweakTargets.CollectPending(entries, useRecommended: false).Count;
    }

    private bool CanApplyAllRecommended() => RecommendedPendingCount > 0;
    private bool CanRestoreDefaults() => DefaultPendingCount > 0;

    [RelayCommand(CanExecute = nameof(CanApplyAllRecommended))]
    private Task ApplyAllRecommendedAsync() => RunTabBulkAsync(useRecommended: true);

    [RelayCommand(CanExecute = nameof(CanRestoreDefaults))]
    private Task RestoreDefaultsAsync() => RunTabBulkAsync(useRecommended: false);

    /// <summary>
    /// Tab-scoped bulk run — net8's RunTabBulkAsync, behaviour preserved:
    /// one confirmation callout instead of per-tweak prompts, an optional restore
    /// point that is never silently skipped when it fails, a re-check of each
    /// tweak immediately before applying (state can drift between the dialog and
    /// the run), batched Explorer restart + drift baseline, and a summary log line.
    /// </summary>
    private async Task RunTabBulkAsync(bool useRecommended)
    {
        try
        {
            var work = TweakTargets.CollectPending(AllEntries.ToList(), useRecommended);
            if (work.Count == 0) return;

            var warnedNames = work
                .Where(w => TweakTargets.WarningFor(w) != null)
                .Select(w => w.Def.Name)
                .ToList();

            bool anyRestart = work.Any(w => w.Def.RequiresRestart);

            var (confirmed, createRestorePoint) =
                await ShowBulkConfirmDialogAsync(useRecommended, work.Count, warnedNames, anyRestart);
            if (!confirmed) return;

            if (createRestorePoint)
            {
                bool rpOk = await RestorePointHelper.EnsureRestorePointAsync(Tool);
                if (!rpOk)
                {
                    // Never proceed silently past a failed restore point the user asked for.
                    if (!await Dialogs.ConfirmContentAsync(
                            "Restore point failed",
                            new TextBlock
                            {
                                Text = "The restore point could not be created (System Restore may be disabled " +
                                       "or requires administrator rights).\n\nApply the tweaks anyway?",
                                TextWrapping = TextWrapping.Wrap,
                                MaxWidth = 440,
                            },
                            "Continue anyway"))
                        return;
                }
            }

            int applied = 0, skipped = 0, failed = 0;
            ExplorerRestart.BeginBatch();
            DriftBaseline.BeginBatch();
            bool restarted;
            try
            {
                foreach (var w in work)
                {
                    try
                    {
                        // Re-check right before applying — state may have drifted
                        // between the dialog and the run (same guard as import).
                        if (!TweakTargets.IsMismatched(w.Def, w.ToggleTarget, w.OptionTarget)) skipped++;
                        else if (w.Def.InputKind == TweakInputKind.Toggle) { TweakHelpers.ApplyToggle(w.Def, w.ToggleTarget); applied++; }
                        else { TweakHelpers.ApplyOption(w.Def, w.OptionTarget); applied++; }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Tool.Log($"[QUICK] FAILED {w.Def.Name}: {ex.Message}");
                    }
                    try { w.Refresh(); } catch { }
                }
            }
            finally { DriftBaseline.EndBatch(); restarted = ExplorerRestart.EndBatch(); }

            foreach (var s in Sections) s.RefreshPendingPill();
            RefreshQuickActionCounts();

            Tool.Log($"[QUICK] {NavLabel}: applied {applied} tweak{(applied == 1 ? "" : "s")}" +
                     (skipped > 0 ? $" · {skipped} skipped (already set)" : "") +
                     (failed > 0 ? $" · {failed} failed — see log" : "") +
                     (restarted ? " · Explorer restarted" : ""));
        }
        catch (Exception ex)
        {
            Tool.Log($"[QUICK] RunTabBulkAsync crashed: {ex.Message}");
        }
    }

    private async Task<(bool Confirmed, bool CreateRestorePoint)> ShowBulkConfirmDialogAsync(
        bool useRecommended, int count, List<string> warnedNames, bool anyRestart)
    {
        var body = new StackPanel { MaxWidth = 440 };

        string valueWord = useRecommended ? "recommended" : "Windows default";
        body.Children.Add(new TextBlock
        {
            Text = $"{count} tweak{(count == 1 ? "" : "s")} on {NavLabel} differ from their " +
                   $"{valueWord} values and will be changed." +
                   (anyRestart ? " Explorer will restart once at the end." : ""),
            TextWrapping = TextWrapping.Wrap,
        });

        if (warnedNames.Count > 0)
        {
            var calloutText = new StackPanel();
            calloutText.Children.Add(new TextBlock
            {
                Text = $"{warnedNames.Count} carry warnings:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            foreach (var name in warnedNames)
                calloutText.Children.Add(new TextBlock
                {
                    Text = "•  " + name,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 3, 0, 0),
                });

            var callout = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 12, 0, 0),
                Child = calloutText,
            };
            // Danger colours come from the stock palette — the Akari crimson token
            // dictionary is not ported to this build yet (see MIGRATION_LOG).
            if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBackgroundBrush", out var bg))
                callout.Background = (Microsoft.UI.Xaml.Media.Brush)bg;
            if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBrush", out var fg))
                callout.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)fg;

            body.Children.Add(callout);
        }

        var restorePointBox = new CheckBox
        {
            Content = "Create a restore point first (recommended)",
            IsChecked = true,
            Margin = new Thickness(0, 14, 0, 0),
        };
        body.Children.Add(restorePointBox);

        bool ok = await Dialogs.ConfirmContentAsync(
            useRecommended ? "Apply all recommended settings?" : "Restore Windows defaults?",
            body,
            useRecommended ? $"Apply {count} tweaks" : $"Reset {count} tweaks");

        return (ok, ok && restorePointBox.IsChecked == true);
    }

    // ── Restore point (Quick Actions menu item 3) ─────────────────────────────

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        try
        {
            Tool.Log("[RESTORE] Creating restore point (Quick actions)…");
            bool ok = await RestorePointHelper.EnsureRestorePointAsync(Tool);
            await Dialogs.InfoAsync(
                ok ? "Restore point ready" : "Restore point failed",
                ok ? "Restore point created (or a recent one already exists)."
                   : "Restore point creation failed — System Restore may be disabled for C:. See the log for details.");
        }
        catch (Exception ex)
        {
            Tool.Log($"[QUICK] CreateRestorePointAsync crashed: {ex.Message}");
        }
    }
}
