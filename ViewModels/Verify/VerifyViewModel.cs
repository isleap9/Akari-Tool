using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariTool.Services;
using AkariTool.Tabs;
using WinUI.Framework.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels.Verify;

/// <summary>
/// Verify page — drift's home. Compares every tweak Akari Tool has applied (the drift
/// baseline) against what the system currently reports, groups the mismatches, and
/// offers re-apply / stop-tracking per tweak. MVVM port of net8 <c>Tabs/Verify/VerifyTab</c>.
///
/// ⚠ Bespoke, NOT a tweak page: renders scan RESULTS, not toggleable tweaks. It owns no
/// TweakDefinition rows and registers NOTHING with TweakRegistry, so the <c>[WARMUP]</c>
/// total is unchanged. Structured like <see cref="AkariTool.ViewModels.Backup.BackupViewModel"/>
/// (a report/action page), not <c>TweakPageViewModel</c>.
///
/// Wording stays conservative: a mismatch is an observation, not proof that Windows
/// Update caused it. The "changed to another value" group is never bulk re-applied
/// because those usually reflect deliberate user choices.
///
/// The backend (<see cref="DriftScanner"/> / <see cref="DriftBaseline"/>) is unchanged
/// and byte-identical to net8 — this is presentation only.
/// </summary>
public sealed partial class VerifyViewModel : ObservableObject
{
    private readonly ToolService _tool;
    private readonly IDialogService _dialogs;

    public VerifyViewModel(ToolService tool, IDialogService dialogs)
    {
        _tool = tool;
        _dialogs = dialogs;
    }

    // ── Static copy (near-verbatim from net8 VerifyTab, "tab" → "page") ──────────

    public string Title => "Verify";

    public string Subtitle =>
        "Windows Update can silently revert registry tweaks. This checks every setting " +
        "Akari Tool has applied against what the system currently reports.";

    public string BaselineCardTitle => "Baseline";

    public string BaselineCardDescription =>
        "The baseline records what Akari Tool wrote. Scan to compare it against what " +
        "Windows currently reports.";

    public string RevertedGroupDescription =>
        "These are now sitting on the Windows factory value — the usual signature of a " +
        "Windows Update rollback.";

    public string ChangedGroupDescription =>
        "These no longer match what Akari set, but they are not on the Windows default " +
        "either. That usually means a deliberate change — review before re-applying.";

    // ── Scan state ──────────────────────────────────────────────────────────────

    [ObservableProperty] public partial string StatusText { get; set; } = "Last scan: not yet this session";

    public ObservableCollection<DriftRowViewModel> Reverted { get; } = new();
    public ObservableCollection<DriftRowViewModel> Changed { get; } = new();

    [ObservableProperty] public partial bool HasReverted { get; set; }
    [ObservableProperty] public partial bool HasChanged { get; set; }
    [ObservableProperty] public partial bool NothingTracked { get; set; }
    [ObservableProperty] public partial bool EverythingMatches { get; set; }
    [ObservableProperty] public partial string EverythingMatchesText { get; set; } = "";

    [ObservableProperty] public partial int RevertedCount { get; set; }
    [ObservableProperty] public partial int ChangedCount { get; set; }

    public string RevertedHeader => $"Reverted to Windows default · {RevertedCount}";
    public string ChangedHeader => $"Changed to a different value · {ChangedCount}";

    partial void OnRevertedCountChanged(int value) => OnPropertyChanged(nameof(RevertedHeader));
    partial void OnChangedCountChanged(int value) => OnPropertyChanged(nameof(ChangedHeader));

    // ── Scan (synchronous on the UI thread, by DriftScanner's own design) ───────

    /// <summary>
    /// Runs a drift scan and rebuilds the result groups. Called on every navigation to
    /// the page (a scan is a read — safe to repeat) and after each re-apply / stop-track.
    /// </summary>
    [RelayCommand]
    public void Scan()
    {
        var result = DriftScanner.Scan();

        StatusText = $"Last scan: {result.ScannedAt:t}  ·  {result.Tracked} tweaks tracked  ·  build {result.CurrentOsBuild}";

        Reverted.Clear();
        Changed.Clear();
        foreach (var d in result.Drifted)
        {
            if (d.Kind == DriftKind.RevertedToWindowsDefault) Reverted.Add(new DriftRowViewModel(d, this));
            else Changed.Add(new DriftRowViewModel(d, this));
        }

        RevertedCount = Reverted.Count;
        ChangedCount = Changed.Count;
        HasReverted = Reverted.Count > 0;
        HasChanged = Changed.Count > 0;

        NothingTracked = result.Tracked == 0;
        EverythingMatches = result.Tracked > 0 && !result.HasDrift;
        EverythingMatchesText =
            $"All {result.Tracked} tracked tweaks are still set the way Akari Tool applied them.";
    }

    // ── Actions ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// "Re-apply all" for the reverted group only — behind a confirm. The changed group
    /// deliberately has no bulk re-apply (those are usually intentional user changes).
    /// </summary>
    [RelayCommand]
    private async Task ReapplyAllRevertedAsync()
    {
        var rows = Reverted.ToList();
        if (rows.Count == 0) return;

        bool ok = await _dialogs.ConfirmAsync(
            "Re-apply reverted tweaks",
            $"Re-apply {rows.Count} tweak{(rows.Count == 1 ? "" : "s")} to the value Akari Tool originally set?",
            "Re-apply", "Cancel");
        if (!ok) return;

        ReapplyRows(rows);
    }

    /// <summary>Per-row re-apply (no confirm — matches net8's single-item button).</summary>
    internal void ReapplySingle(DriftRowViewModel row) => ReapplyRows(new[] { row });

    /// <summary>
    /// Re-writes the RECORDED value for each item — <see cref="DriftItem.RecordedToggle"/>
    /// for toggles, the option at <see cref="DriftItem.RecordedIndex"/> for dropdowns —
    /// NOT the current live value and NOT the Windows default. The whole point of re-apply
    /// is restoring what Akari originally set. ApplyToggle/ApplyOption re-stamp the baseline
    /// (timestamp + OS build), which is correct: the tweak is now set on this build.
    /// </summary>
    private void ReapplyRows(IReadOnlyList<DriftRowViewModel> rows)
    {
        int applied = 0, failed = 0;

        ExplorerRestart.BeginBatch();
        DriftBaseline.BeginBatch();
        try
        {
            foreach (var row in rows)
            {
                var item = row.Item;
                try
                {
                    if (!TweakRegistry.TryGetDefinition(item.Id, out var def)) { failed++; continue; }

                    if (def.InputKind == TweakInputKind.Toggle && item.RecordedToggle is bool b)
                        TweakHelpers.ApplyToggle(def, b);
                    else if (def.InputKind == TweakInputKind.Dropdown && item.RecordedIndex is int i)
                        TweakHelpers.ApplyOption(def, i);
                    else { failed++; continue; }

                    TweakRegistry.RefreshRows(item.Id);   // refresh the tweak's own rows on their home page
                    applied++;
                }
                catch { failed++; }
            }
        }
        finally { DriftBaseline.EndBatch(); ExplorerRestart.EndBatch(); }

        // Cross-page: refresh every tab's section "pending" pills + quick-action counts,
        // the MVVM seam net8 covered with TweakHelpers.RefreshAllSectionPills().
        TweakRegistry.NotifySectionsNeedRefresh();

        _tool.Log($"[VERIFY] Re-applied {applied} tweak(s)" + (failed > 0 ? $", {failed} failed" : ""));

        Scan();   // re-scan so the page reflects reality, not assumption
    }

    /// <summary>Stop tracking one tweak (user dismissed it as intentional) → re-scan.</summary>
    internal void StopTracking(DriftRowViewModel row)
    {
        DriftBaseline.Forget(row.Id);
        _tool.Log($"[VERIFY] Stopped tracking {row.Id}");
        Scan();
    }
}

/// <summary>
/// One drifted tweak in a result group. Wraps a <see cref="DriftItem"/> for display and
/// delegates its two per-row actions back to the parent <see cref="VerifyViewModel"/>.
/// </summary>
public sealed partial class DriftRowViewModel : ObservableObject
{
    private readonly VerifyViewModel _parent;

    public DriftItem Item { get; }

    public DriftRowViewModel(DriftItem item, VerifyViewModel parent)
    {
        Item = item;
        _parent = parent;
    }

    public string Id => Item.Id;
    public string Name => Item.Name;

    // net8: "{recorded} → {current} · set {d MMM} on build {osBuild}"
    public string DetailText =>
        $"{Item.RecordedDisplay} → {Item.CurrentDisplay} · set {Item.RecordedAt.ToLocalTime():d MMM} on build {Item.RecordedOsBuild}";

    public bool AcrossOsUpdate => Item.AcrossOsUpdate;

    [RelayCommand] private void Reapply() => _parent.ReapplySingle(this);
    [RelayCommand] private void StopTracking() => _parent.StopTracking(this);
}
