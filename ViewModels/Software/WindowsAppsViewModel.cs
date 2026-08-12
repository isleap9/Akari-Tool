using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariTool.Services;
using AkariTool.Tabs;

namespace AkariTool.ViewModels.Software;

/// <summary>
/// The Software ▸ Windows Apps panel (rail tag "Bloatware") — MVVM port of net8's
/// <c>SoftwareTab.WindowsApps.cs</c> plus the <c>isWindowsApps: true</c> half of
/// <c>SoftwareTab.InstallQueue.cs</c>. Stage 2 of the Software rollout.
///
/// ⚠ BESPOKE, NOT A TWEAK PAGE — no <c>TweakDefinition</c> rows, never registers with
/// <c>TweakRegistry</c>, absent from the warm-up enumeration. The <c>[WARMUP]</c> total
/// must stay 439.
///
/// ⚠ DESTRUCTIVE. Remove Selected drives the already-ported
/// <see cref="SoftwareAppService.RemoveWindowsAppsAsync"/>, which generates and runs
/// BloatRemoval-Run.ps1 AND (unconditionally, as its own step 3) persists the merged
/// script + registers the SYSTEM ONSTART scheduled task. No removal, categorization or
/// persistence logic is reimplemented here — this file is UI state and call sites only.
/// </summary>
public sealed partial class WindowsAppsViewModel : ObservableObject
{
    private readonly ToolService _tool;
    private readonly TweakDialogs _dialogs;

    // The same List<AppDefinition> the service layer reads IsSelected off, as net8's
    // _windowsApps did.
    private readonly List<AppDefinition> _windowsApps = [];
    private readonly List<AppCardViewModel> _cards = [];

    private bool _built;
    private readonly object _buildLock = new();

    public ObservableCollection<AppSectionViewModel> Sections { get; } = [];

    // Header copy verbatim from net8 BuildWindowsAppsPanel's PageHeader.
    public string Title => "Windows Apps";

    public string Subtitle =>
        "Remove pre-installed Windows apps, legacy capabilities, and optional features — or reinstall them. Removed apps stay removed across Windows updates.";

    public WindowsAppsViewModel(ToolService tool, TweakDialogs dialogs)
    {
        _tool = tool;
        _dialogs = dialogs;
    }

    // ── Bar state ─────────────────────────────────────────────────────────────

    [ObservableProperty] public partial string SelectedCountText { get; set; } = "0 selected";
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool StatusVisible { get; set; }
    [ObservableProperty] public partial bool ButtonsEnabled { get; set; } = true;

    private bool _busy;

    // ── Search ────────────────────────────────────────────────────────────────

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        var query = value.Trim();
        foreach (var section in Sections) section.ApplySearch(query);
    }

    // ── Select-all row ────────────────────────────────────────────────────────

    [ObservableProperty] public partial bool SelectAll { get; set; }
    [ObservableProperty] public partial bool SelectInstalled { get; set; }
    [ObservableProperty] public partial bool SelectNotInstalled { get; set; }

    partial void OnSelectAllChanged(bool value) => SetSelection(_ => true, value);
    partial void OnSelectInstalledChanged(bool value) => SetSelection(a => a.IsInstalled, value);
    partial void OnSelectNotInstalledChanged(bool value) => SetSelection(a => !a.IsInstalled, value);

    /// <summary>net8 SetSelection — only cards currently passing the search filter.</summary>
    private void SetSelection(Func<AppDefinition, bool> predicate, bool selected)
    {
        foreach (var card in _cards.Where(c => c.Visible && predicate(c.App)))
            card.IsSelected = selected;
        RefreshCounts();
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the three catalogs and builds net8's THREE FIXED sections — filtered by
    /// definition SHAPE, not by GroupName (that is the External Apps panel's scheme),
    /// each ordered by Name / OrdinalIgnoreCase. Idempotent + lock-guarded.
    /// </summary>
    public void Build()
    {
        lock (_buildLock)
        {
            if (_built) return;
            _built = true;
        }

        _windowsApps.AddRange(WindowsAppCatalog.GetWindowsApps().Items);
        _windowsApps.AddRange(CapabilityCatalog.GetWindowsCapabilities().Items);
        _windowsApps.AddRange(OptionalFeatureCatalog.GetWindowsOptionalFeatures().Items);

        // Stage 3 (Phase 26): the Stage-2 scope filter that excluded the two
        // RemovalScript-backed definitions (Edge, OneDrive) has been removed, so they
        // render as regular cards again and RemoveWindowsAppsAsync's scriptApps branch
        // dispatches them to their dedicated scripts — net8 behaviour restored. Phase 26
        // recon (points 1/2/4) confirmed no other Stage-2 code assumed their absence.
        // See MIGRATION_LOG2.md "Phase 26 ▸ Standalone note (3b)" for Edge's separate
        // OpenWebSearchRepair persistence, deliberately left as-is.

        AddSection("Windows Apps",
            _windowsApps.Where(a => a.CapabilityName == null && a.OptionalFeatureName == null));
        AddSection("Legacy Capabilities",
            _windowsApps.Where(a => a.CapabilityName != null));
        AddSection("Optional Features",
            _windowsApps.Where(a => a.OptionalFeatureName != null));

        // Selected-count tracks selection STATE, not the click that caused it
        // (Phase 23 — the same fix, for the same reason).
        foreach (var app in _windowsApps)
            app.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppDefinition.IsSelected)) RefreshCounts();
            };

        RefreshCounts();
    }

    private void AddSection(string title, IEnumerable<AppDefinition> apps)
    {
        var cards = apps
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new AppCardViewModel(a, isWindowsApps: true))   // enables the Permanent pill
            .ToList();

        _cards.AddRange(cards);
        Sections.Add(new AppSectionViewModel(title, cards));
    }

    /// <summary>Icons + first status read; UI thread (BitmapImage is UI-thread affine).</summary>
    public void StartDeferredLoads()
    {
        foreach (var card in _cards) _ = card.LoadIconAsync();
        _ = RefreshStatusAsync();
    }

    // ── Advanced Tools hook ───────────────────────────────────────────────────

    /// <summary>
    /// The live Bloatware selection, for the Advanced Tools Autounattend generator.
    /// net8 wired this as <c>adv.SetSelectedAppsProvider(() => _software.GetSelectedWindowsApps())</c>
    /// (MainWindow.xaml.cs:254); build #3's <c>AutounattendService.GenerateToFile</c>
    /// already takes the same <c>IReadOnlyList&lt;AppDefinition&gt;</c>. Exposed now so
    /// the Advanced Tools wave has something to bind to — that UI is NOT built yet and
    /// nothing calls this in-app today.
    ///
    /// Returns a snapshot (net8 returned a fresh List too). Call on the UI thread —
    /// per the CLAUDE.md cross-thread rule, snapshot the selection BEFORE any Task.Run.
    /// </summary>
    public List<AppDefinition> GetSelectedWindowsApps() =>
        _windowsApps.Where(a => a.IsSelected).ToList();

    // ── Actions ───────────────────────────────────────────────────────────────

    /// <summary>
    /// net8 RemoveSelectedWindowsAppsAsync. The confirmation message, title, Yes/No pair
    /// and the early return on decline are byte-identical to net8; nothing runs before
    /// the user confirms.
    /// </summary>
    [RelayCommand]
    private async Task RemoveSelectedAsync()
    {
        var selected = _windowsApps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0 || _busy) return;

        var warnings = selected.Where(a => a.HasInstabilityWarning).Select(a => a.Name).ToList();
        var msg = $"Remove {selected.Count} item(s) from Windows?\n\n" +
                  string.Join(", ", selected.Take(10).Select(a => a.Name)) +
                  (selected.Count > 10 ? $" (+{selected.Count - 10} more)" : "") +
                  (warnings.Count > 0 ? $"\n\n⚠ {string.Join(", ", warnings)}: removal can affect Windows components that depend on it." : "") +
                  "\n\nA startup task keeps these removed after Windows updates.";
        if (!await _dialogs.ConfirmYesNoAsync("Remove Windows Apps", DialogText(msg)))
            return;

        _busy = true;
        SetButtonsEnabled(false);
        try
        {
            // Already-ported, unmodified. Step 3 of this call ALWAYS persists the merged
            // BloatRemoval.ps1 and registers the SYSTEM ONSTART task — private, not
            // separately callable, and deliberately not bypassed.
            await SoftwareAppService.RemoveWindowsAppsAsync(selected,
                log: m => _tool.Log(m),
                status: SetStatusOnUi);
            foreach (var app in selected) app.IsSelected = false;
        }
        finally
        {
            _busy = false;
            SetButtonsEnabled(true);
        }
        await RefreshStatusAsync();
    }

    /// <summary>
    /// net8 InstallSelectedAsync(_windowsApps, isWindowsApps: true).
    ///
    /// No Yes/No confirmation, as in net8 — the only dialog is the info-only "Permanent
    /// Items" notice. The isWindowsApps-only <c>RemoveFromSavedScriptAsync</c> call at
    /// the end is LOAD-BEARING: without it the startup task would silently re-remove a
    /// just-reinstalled app on the next boot.
    /// </summary>
    [RelayCommand]
    private async Task InstallSelectedAsync()
    {
        var selected = _windowsApps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0 || _busy) return;

        var notReinstallable = selected.Where(a => !a.CanBeReinstalled).ToList();
        if (notReinstallable.Count > 0)
        {
            await _dialogs.InfoAsync(
                "Permanent Items",
                DialogText(
                    "These items are permanent — once removed they can't be reinstalled — and will be skipped:\n\n" +
                    string.Join(", ", notReinstallable.Select(a => a.Name))));
            selected = selected.Where(a => a.CanBeReinstalled).ToList();
            if (selected.Count == 0) return;
        }

        _busy = true;
        SetButtonsEnabled(false);
        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                var app = selected[i];
                SetStatus($"Installing {app.Name}… ({i + 1}/{selected.Count})");
                var ok = await SoftwareAppService.InstallAppAsync(app, m => _tool.Log(m));
                if (ok) app.IsSelected = false;
            }

            // Reinstalled Windows apps must leave the keep-removed script.
            //
            // NOTE (net8 asymmetry, PRESERVED VERBATIM — do not "fix"): the save path
            // filters with `apps.Where(a => a.RemovalScript == null)` before Categorize,
            // this inverse path does not. Flagged in Phase 24; left exactly as net8 has
            // it. net8 also passes the FULL pre-loop `selected` list, including any app
            // whose install failed — same here.
            await SoftwareAppService.RemoveFromSavedScriptAsync(selected, m => _tool.Log(m));
        }
        finally
        {
            _busy = false;
            SetButtonsEnabled(true);
        }
        await RefreshStatusAsync();
    }

    /// <summary>
    /// net8 RefreshStatusAsync, scoped to this panel (Stage 1's copy covers only External
    /// Apps — net8's single method refreshed both because both lived in one tab).
    /// Read-only: one detection snapshot, then the Installed badges repaint.
    /// </summary>
    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        if (_busy) return;
        _busy = true;
        SetButtonsEnabled(false);
        SetStatus("Checking installed status…");

        try
        {
            var snapshot = await SoftwareAppService.GetInstallSnapshotAsync();
            SoftwareAppService.ApplyInstallStatus(_windowsApps, snapshot);
        }
        catch (Exception ex)
        {
            _tool.Log($"[ERROR] Status refresh failed: {ex.Message}");
        }
        finally
        {
            _busy = false;
            SetButtonsEnabled(true);
            StatusVisible = false;
            RefreshCounts();
        }
    }

    // ── Plumbing ──────────────────────────────────────────────────────────────

    public void ToggleCard(AppCardViewModel card)
    {
        card.Toggle();
        RefreshCounts();
    }

    public void SetCardSelected(AppCardViewModel card, bool selected)
    {
        card.IsSelected = selected;
        RefreshCounts();
    }

    public Task OpenWebsiteAsync(string url) => _tool.RunAction(new UrlAction(url));

    private void RefreshCounts() =>
        SelectedCountText = $"{_windowsApps.Count(a => a.IsSelected)} selected";

    private void SetStatus(string text)
    {
        StatusText = text;
        StatusVisible = true;
    }

    /// <summary>
    /// Status sink handed to RemoveWindowsAppsAsync. net8 marshalled the same callback
    /// with DispatcherQueue.TryEnqueue because the removal pipeline can report from a
    /// non-UI thread; kept.
    /// </summary>
    private void SetStatusOnUi(string text) =>
        App.DispatcherQueue.TryEnqueue(() => SetStatus(text));

    private void SetButtonsEnabled(bool enabled) => ButtonsEnabled = enabled;

    /// <summary>Matches net8 AkariDialogs' content element (wrapping TextBlock, MaxWidth 440).</summary>
    private static Microsoft.UI.Xaml.Controls.TextBlock DialogText(string message) => new()
    {
        Text = message,
        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
        MaxWidth = 440,
    };
}
