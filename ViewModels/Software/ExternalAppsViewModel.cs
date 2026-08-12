using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariTool.Services;
using AkariTool.Tabs;

namespace AkariTool.ViewModels.Software;

/// <summary>
/// The Software ▸ External Apps panel (rail tag "AppInstaller") — MVVM port of net8's
/// <c>SoftwareTab.ExternalApps.cs</c> plus the External-Apps half of
/// <c>SoftwareTab.InstallQueue.cs</c>, <c>.Cards.cs</c> and <c>.UiHelpers.cs</c>.
/// Stage 1 of the Software rollout; Windows Apps (Bloatware) and Debloat are separate
/// stages and are deliberately NOT touched here.
///
/// ⚠ BESPOKE, NOT A TWEAK PAGE. This is deliberately NOT a <c>TweakPageViewModel</c>:
/// the Software tab has no <c>TweakDefinition</c> rows and must never register with
/// <c>TweakRegistry</c>, so it is registered as a plain DI singleton and is absent
/// from the warm-up enumeration. The <c>[WARMUP]</c> guard total must be unchanged
/// (439) after this page exists.
///
/// Every install/uninstall/detection call goes to the ALREADY-PORTED
/// <see cref="SoftwareAppService"/> verbatim — no winget or removal logic is
/// reimplemented here. The catalog, the ordering, the dialog copy and the busy/enable
/// semantics are net8's.
/// </summary>
public sealed partial class ExternalAppsViewModel : ObservableObject
{
    private readonly ToolService _tool;
    private readonly TweakDialogs _dialogs;

    // Catalog backing store — the same List<AppDefinition> the service layer reads
    // IsSelected off, exactly as net8's _externalApps did.
    private readonly List<AppDefinition> _externalApps = [];
    private readonly List<AppCardViewModel> _cards = [];

    private bool _built;
    private readonly object _buildLock = new();

    public ObservableCollection<AppSectionViewModel> Sections { get; } = [];

    // Header copy verbatim from net8 BuildExternalAppsPanel's PageHeader.
    public string Title => "External Apps";

    public string Subtitle =>
        "Install and manage third-party applications via WinGet — browsers, media, gaming, development tools, runtimes, and more.";

    public ExternalAppsViewModel(ToolService tool, TweakDialogs dialogs)
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

    // ── Select-all row (net8 SelectAllRow: three independent checkboxes) ───────

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
    /// Populates the catalog and the section/card tree. Idempotent + lock-guarded so a
    /// repeat navigation is a no-op (the VM is a DI singleton).
    /// </summary>
    public void Build()
    {
        lock (_buildLock)
        {
            if (_built) return;
            _built = true;
        }

        _externalApps.AddRange(ExternalAppCatalog.GetExternalApps().Items);

        // net8: category sections in catalog order, alphabetical within each.
        foreach (var group in _externalApps.GroupBy(a => a.GroupName ?? "Other"))
        {
            var cards = group
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => new AppCardViewModel(a))
                .ToList();

            _cards.AddRange(cards);
            Sections.Add(new AppSectionViewModel(group.Key, cards));
        }

        // The "N selected" badge tracks the SELECTION STATE, not the click that
        // caused it. net8 called RefreshCounts() by hand from each of the four
        // mutation sites; driving it from the definition's own PropertyChanged
        // covers every path — including ones with no click at all (post-install
        // deselect, import refresh, automation) — and cannot drift out of sync.
        foreach (var app in _externalApps)
            app.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppDefinition.IsSelected)) RefreshCounts();
            };

        RefreshCounts();
    }

    /// <summary>
    /// Starts the cosmetic icon loads and the first status read. Called from the page
    /// on the UI thread (BitmapImage is UI-thread affine), matching net8, where both
    /// were kicked off from card construction / Build().
    /// </summary>
    public void StartDeferredLoads()
    {
        foreach (var card in _cards) _ = card.LoadIconAsync();
        _ = RefreshStatusAsync();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    /// <summary>
    /// net8 InstallSelectedAsync(_externalApps, isWindowsApps: false). Note the
    /// isWindowsApps-only <c>RemoveFromSavedScriptAsync</c> call is correctly absent —
    /// that keep-removed bookkeeping belongs to the Bloatware panel.
    ///
    /// As in net8, the install path has NO Yes/No confirmation; the only dialog is the
    /// "Permanent Items" notice below. Not changed here — behaviour is preserved.
    /// </summary>
    [RelayCommand]
    private async Task InstallSelectedAsync()
    {
        var selected = _externalApps.Where(a => a.IsSelected).ToList();
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
        }
        finally
        {
            _busy = false;
            SetButtonsEnabled(true);
        }
        await RefreshStatusAsync();
    }

    /// <summary>
    /// net8 UninstallSelectedExternalAsync. The confirmation message, title, button
    /// pair (Yes/No) and the early return on decline are byte-identical to net8 —
    /// nothing runs before the user confirms.
    /// </summary>
    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        var selected = _externalApps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0 || _busy) return;

        var msg = $"Uninstall {selected.Count} app(s)?\n\n" +
                  string.Join(", ", selected.Take(10).Select(a => a.Name)) +
                  (selected.Count > 10 ? $" (+{selected.Count - 10} more)" : "");
        if (!await _dialogs.ConfirmYesNoAsync("Uninstall External Apps", DialogText(msg)))
            return;

        _busy = true;
        SetButtonsEnabled(false);
        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                var app = selected[i];
                SetStatus($"Uninstalling {app.Name}… ({i + 1}/{selected.Count})");
                await SoftwareAppService.UninstallExternalAppAsync(app, m => _tool.Log(m));
                app.IsSelected = false;
            }
        }
        finally
        {
            _busy = false;
            SetButtonsEnabled(true);
        }
        await RefreshStatusAsync();
    }

    /// <summary>
    /// net8 RefreshStatusAsync, scoped to this panel — read-only: one detection
    /// snapshot, then ApplyInstallStatus repaints the Installed badges.
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
            SoftwareAppService.ApplyInstallStatus(_externalApps, snapshot);
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

    /// <summary>Card click-through from the page (net8 card.Tapped / check.Click).</summary>
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

    /// <summary>net8 UrlAction from the card's website glyph.</summary>
    public Task OpenWebsiteAsync(string url) => _tool.RunAction(new UrlAction(url));

    private void RefreshCounts() =>
        SelectedCountText = $"{_externalApps.Count(a => a.IsSelected)} selected";

    private void SetStatus(string text)
    {
        StatusText = text;
        StatusVisible = true;
    }

    private void SetButtonsEnabled(bool enabled) => ButtonsEnabled = enabled;

    /// <summary>
    /// Matches net8 AkariDialogs' content element (wrapping TextBlock, MaxWidth 440) so
    /// long name lists lay out identically.
    /// </summary>
    private static Microsoft.UI.Xaml.Controls.TextBlock DialogText(string message) => new()
    {
        Text = message,
        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
        MaxWidth = 440,
    };
}
