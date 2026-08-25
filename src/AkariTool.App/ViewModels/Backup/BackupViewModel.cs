using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkariTool.Services;
using AkariTool.Tabs;
using WinUI.Framework.Services;

namespace AkariTool.ViewModels.Backup;

/// <summary>
/// Backup &amp; Restore panel — MVVM port of net8 <c>Tabs/Backup/BackupTab.xaml.cs</c>.
/// Two static cards (Export / Import) over the SettingDefinition-native
/// <see cref="SettingBackupService"/> engine (Phase 3b-ii — the legacy TweakRegistry
/// holds zero entries since the declarative migration). JSON format stays byte-identical
/// to TweakRegistry so old backup files remain importable.
///
/// ⚠ Bespoke, NOT a tweak page: registers nothing with TweakRegistry, so the
/// <c>[WARMUP]</c> total is unchanged.
///
/// The only behavioural note beyond net8 is the Defender callout inside
/// <see cref="ImportReviewDialog"/> (isleap Option 2, Phase 1/LOG3) — presentation
/// only, wired there, not here. No Defender code is called/referenced/imported.
/// </summary>
public sealed partial class BackupViewModel : ObservableObject
{
    private readonly ToolService _tool;
    private readonly IFileService _files;
    private readonly IDialogService _dialogs;
    private readonly SettingBackupService _backup;

    public string Title => "Backup & Restore";

    public string Subtitle =>
        "Export every tweak currently set in Akari Tool to a file, or import a settings file to apply a saved or shared configuration.";

    // Card copy verbatim from net8 BuildExportCard / BuildImportCard.
    public string ExportCardTitle => "Export Settings";
    public string ExportCardDescription =>
        "Saves the current state of every tweak across Gaming, Privacy, Update, Notifications, Power, and OS Tweaks to a JSON file. Keep it as a backup or share it with someone else.";
    public string ImportCardTitle => "Import Settings";
    public string ImportCardDescription =>
        "Applies every tweak from an Akari Tool settings file to this system. Tweaks already matching the file are skipped, and all toggles and dropdowns across the app update live. Some changes may require a restart to take effect.";

    [ObservableProperty] public partial string SummaryText { get; set; } = "";

    [ObservableProperty] public partial string ExportStatus { get; set; } = "";
    [ObservableProperty] public partial bool ExportStatusVisible { get; set; }
    [ObservableProperty] public partial bool ExportStatusOk { get; set; } = true;

    [ObservableProperty] public partial string ImportStatus { get; set; } = "";
    [ObservableProperty] public partial bool ImportStatusVisible { get; set; }
    [ObservableProperty] public partial bool ImportStatusOk { get; set; } = true;

    // net8 used SuccessFg / DangerFg on the status text; derived here so the page needs
    // no bool→brush converter.
    public Microsoft.UI.Xaml.Media.Brush ExportStatusBrush => StatusBrush(ExportStatusOk);
    public Microsoft.UI.Xaml.Media.Brush ImportStatusBrush => StatusBrush(ImportStatusOk);

    partial void OnExportStatusOkChanged(bool value) => OnPropertyChanged(nameof(ExportStatusBrush));
    partial void OnImportStatusOkChanged(bool value) => OnPropertyChanged(nameof(ImportStatusBrush));

    private static Microsoft.UI.Xaml.Media.Brush StatusBrush(bool ok) =>
        (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources[
            ok ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush"];

    public BackupViewModel(ToolService tool, IFileService files, IDialogService dialogs, SettingBackupService backup)
    {
        _tool = tool;
        _files = files;
        _dialogs = dialogs;
        _backup = backup;
        RefreshSummary();
    }

    /// <summary>net8 refreshed the count on tab (re)load; the page calls this on Loaded.</summary>
    public void RefreshSummary() =>
        SummaryText = $"{_backup.Count} tweaks are currently tracked and will be included in the export.";

    // ── Export ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ExportAsync()
    {
        // net8: FilePickers.SaveFileAsync("Akari Tool settings", ".json", default name).
        // The framework IFileService covers this; it returns a StorageFile whose .Path
        // feeds TweakRegistry.ExportToFile unchanged.
        var file = await _files.SaveFileAsync(
            $"AkariTool-Settings-{DateTime.Now:yyyy-MM-dd}.json",
            new[] { ".json" });
        if (file is null) return;

        try
        {
            var result = _backup.ExportToFile(file.Path);
            _tool.Log($"[Backup] Exported {result.Exported} tweak(s) → {file.Path}" +
                      (result.Skipped > 0 ? $" ({result.Skipped} skipped — state unreadable)" : ""));
            SetExport($"Exported {result.Exported} tweak(s) to {Path.GetFileName(file.Path)}.", ok: true);
        }
        catch (Exception ex)
        {
            _tool.Log($"[Backup] Export failed: {ex.Message}");
            SetExport($"Export failed: {ex.Message}", ok: false);
        }
    }

    // ── Import ──────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ImportAsync()
    {
        var file = await _files.PickSingleFileAsync(new[] { ".json" });
        if (file is null) return;

        try
        {
            // Review Mode: preview the file, show only the differing entries, let the
            // user uncheck what to keep — verbatim net8 flow.
            var preview = _backup.PreviewImport(file.Path);
            var differing = preview.Entries.Where(e => e.Differs).ToList();

            if (differing.Count == 0)
            {
                var msg0 = "Everything already matches — nothing to apply.";
                if (preview.Unknown > 0)
                    msg0 += $" ({preview.Unknown} entr{(preview.Unknown == 1 ? "y" : "ies")} not recognized by this version.)";
                _tool.Log($"[Backup] Import preview: no differences ({preview.Unknown} unknown).");
                SetImport(msg0, ok: true);
                return;
            }

            var xamlRoot = _dialogs.XamlRoot;
            if (xamlRoot is null) { SetImport("Import unavailable — no window.", ok: false); return; }

            var review = new ImportReviewDialog(
                differing, preview.Unknown, xamlRoot,
                owners: _backup.ItemOwners,
                sections: null);
            if (!await review.ShowAsync() || review.SelectedIds.Count == 0)
            {
                SetImport("Import cancelled — nothing was changed.", ok: true);
                return;
            }

            var r = await _backup.ImportFromFile(file.Path, review.SelectedIds);
            _tool.Log($"[Backup] Import complete — {r.Applied} applied, {r.AlreadySet} already set, " +
                      $"{r.Unknown} unknown, {r.Failed} failed (of {r.Total}).");

            var msg = $"Applied {r.Applied} tweak(s). {r.AlreadySet} already matched.";
            if (r.Unknown > 0) msg += $" {r.Unknown} not recognized by this version.";
            if (r.Failed > 0) msg += $" {r.Failed} failed.";
            if (r.Applied > 0) msg += " Some changes may need a restart.";
            SetImport(msg, ok: r.Failed == 0);
        }
        catch (Exception ex)
        {
            _tool.Log($"[Backup] Import failed: {ex.Message}");
            SetImport($"Import failed: {ex.Message}", ok: false);
        }
    }

    private void SetExport(string text, bool ok)
    {
        ExportStatus = text;
        ExportStatusOk = ok;
        ExportStatusVisible = true;
    }

    private void SetImport(string text, bool ok)
    {
        ImportStatus = text;
        ImportStatusOk = ok;
        ImportStatusVisible = true;
    }
}
