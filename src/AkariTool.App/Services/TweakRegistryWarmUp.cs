using Microsoft.Extensions.DependencyInjection;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;
using WinUI.Framework.Services;

namespace AkariTool.Services;

/// <summary>
/// Startup warm-up for the tweak registry.
///
/// PROBLEM this solves: a tweak-page view model registers its rows with
/// <see cref="TweakRegistry"/> only inside <c>Build()</c>, which runs on first
/// navigation to that page. But <see cref="TweakRegistry.ExportToFile"/> and
/// <see cref="TweakRegistry.Search"/> iterate the entries list — so any tab the
/// user never opened is silently missing from Backup exports and global search.
///
/// FIX: after the shell activates, resolve every registered
/// <see cref="TweakPageViewModel"/> and call <c>Build()</c> once, so registration
/// is complete regardless of what the user navigates to. <c>Build()</c> is
/// idempotent, so the later real navigation to a page is a safe no-op.
///
/// THREADING — deliberately a SINGLE background thread, SEQUENTIAL:
///  • <c>TweakRegistry._entries</c> is a plain <see cref="System.Collections.Generic.List{T}"/>;
///    two <c>Build()</c>s in parallel would interleave <c>Mark()</c>/<c>Register</c>/
///    <c>ClaimRange()</c> and corrupt the per-tab index ranges. Sequential build
///    keeps each tab's range contiguous, exactly as navigation-order building did.
///  • Building <c>Sections</c> (an ObservableCollection) OFF the UI thread is safe
///    here because nothing is bound to it yet: a page binds its VM's <c>Sections</c>
///    only when that page is navigated, and by then <c>Build()</c> has finished
///    populating it (the Build lock makes a mid-warm-up navigation wait, then
///    no-op). No <c>CollectionChanged</c> ever fires against a bound collection, so
///    no dispatcher marshaling is needed. The item/section VMs are pure data +
///    registry reads — they touch no UI. Logging is already marshaled by the shell
///    (AkariUiLogService.LineLogged → DispatcherQueue.TryEnqueue), so Build()'s log
///    lines are safe from here too.
///
/// This method is also the seam where the future staged-progress SPLASH hooks in:
/// it already enumerates the pages sequentially, so a splash can report
/// "warming N of M" per <c>Build()</c> before the main window is shown. Not wired
/// yet (see MIGRATION_LOG).
/// </summary>
public static class TweakRegistryWarmUp
{
    /// <summary>
    /// Builds every tweak page once and validates the registry. Call ONCE, on a
    /// single background thread, after the shell is activated.
    /// </summary>
    public static void Run(IServiceProvider services, ILogService log)
    {
        try
        {
            // GetServices returns every TweakPageViewModel registration in
            // registration order (GamingViewModel is registered first). Each
            // resolves to its singleton instance — the same object the page binds.
            var pages = services.GetServices<TweakPageViewModel>().ToList();

            var built = new List<(string Tag, string Label)>();
            foreach (var page in pages)          // SEQUENTIAL — never parallel
            {
                page.Build();                    // idempotent; later navigation no-ops
                built.Add((page.NavTag, page.NavLabel));
            }

            LogGuard(log, built);
        }
        catch (Exception ex)
        {
            log.Error("[WARMUP] Tweak registry warm-up failed — some tabs may be missing from Backup/search.", ex);
        }
    }

    /// <summary>
    /// Logs the total count + every claimed range, then asserts the invariants
    /// Backup/search depend on: every warmed page owns a non-empty range, and the
    /// ranges tile [0, Count) with no gap or overlap. Logs loudly (Error) on any
    /// violation so a broken rollout tab is caught at startup, not by a user with
    /// a short Backup file.
    /// </summary>
    private static void LogGuard(ILogService log, List<(string Tag, string Label)> built)
    {
        var ranges = TweakRegistry.TabRanges;
        int total = TweakRegistry.Count;

        log.Info($"[WARMUP] Tweak registry warmed: {built.Count} tweak page(s), " +
                 $"{total} tweaks, {ranges.Count} claimed range(s).");
        foreach (var r in ranges)
            log.Info($"[WARMUP]   {r.Tag} [{r.Start}..{r.End}) {r.Count} rows — {r.Label}");

        bool ok = true;

        // (a) Every built page must own a non-empty range. ClaimRange skips empty
        //     ranges, so a page that registered zero rows is ABSENT from TabRanges
        //     entirely — that is the silent "missing from Backup/search" bug.
        foreach (var (tag, _) in built)
        {
            if (!ranges.Any(r => r.Tag == tag && r.Count > 0))
            {
                ok = false;
                log.Error($"[WARMUP] ✗ page '{tag}' claimed NO rows — it will be absent from Backup export and search.");
            }
        }

        // (b) Ranges must be non-empty and tile [0, Count) contiguously.
        int cursor = 0;
        foreach (var r in ranges.OrderBy(r => r.Start))
        {
            if (r.Count <= 0)
            {
                ok = false;
                log.Error($"[WARMUP] ✗ empty range claimed for '{r.Tag}'.");
            }
            if (r.Start != cursor)
            {
                ok = false;
                log.Error($"[WARMUP] ✗ range discontinuity before '{r.Tag}': expected start {cursor}, got {r.Start} (gap or overlap).");
            }
            cursor = Math.Max(cursor, r.End);
        }
        if (cursor != total)
        {
            ok = false;
            log.Error($"[WARMUP] ✗ ranges cover [0..{cursor}) but the registry holds {total} entries — " +
                      $"{total - cursor} unattributed row(s) (registered outside a Mark/ClaimRange bracket).");
        }

        if (ok)
            log.Info($"[WARMUP] OK: {ranges.Count} range(s) contiguous and non-empty, tiling [0..{total}).");
        else
            log.Error("[WARMUP] ✗ TweakRegistry range validation FAILED (see lines above) — " +
                      "Backup/search attribution is unreliable until fixed.");
    }
}
