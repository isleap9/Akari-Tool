using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Backup
{
    /// <summary>
    /// Backup &amp; Restore tab — Akari Tool's equivalent of Winhance's
    /// config export/import. Exports the current state of every tweak
    /// registered through TweakHelpers.AddTweakRow() (via TweakRegistry)
    /// to a shareable JSON file, and applies such files back to the system.
    /// </summary>
    public partial class BackupTab : BaseTab
    {
        private TextBlock _summary = null!;
        private TextBlock _exportStatus = null!;
        private TextBlock _importStatus = null!;

        public BackupTab() => InitializeComponent();

        public override string NavTag   => "Backup";
        public override string NavLabel => "Backup & Restore";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Backup & Restore",
                "Export every tweak currently set in Akari Tool to a file, or import a settings file to apply a saved or shared configuration."));

            BuildExportCard();
            BuildImportCard();
        }

        // ═════════════════════════════════════════════════════════════════
        //  Export card
        // ═════════════════════════════════════════════════════════════════

        private void BuildExportCard()
        {
            var body = MakeCard(
                "\uE898", "Export Settings",
                "Saves the current state of every tweak across Gaming, Privacy, Update, Notifications, Power, and OS Tweaks to a JSON file. Keep it as a backup or share it with someone else.");

            _summary = new TextBlock
            {
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                Margin = new Thickness(0, 0, 0, 12),
            };
            body.Children.Add(_summary);
            RefreshSummary();
            IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) RefreshSummary(); };

            var btn = MakeActionButton("Export to File\u2026", primary: true);
            btn.Click += (_, _) => ExportSettings();
            body.Children.Add(btn);

            _exportStatus = MakeStatusBlock();
            body.Children.Add(_exportStatus);
        }

        private void RefreshSummary() =>
            _summary.Text = $"{TweakRegistry.Count} tweaks are currently tracked and will be included in the export.";

        private void ExportSettings()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Akari Tool settings",
                Filter = "Akari Tool settings (*.json)|*.json",
                FileName = $"AkariTool-Settings-{DateTime.Now:yyyy-MM-dd}.json",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var result = TweakRegistry.ExportToFile(dlg.FileName);
                Service!.Log($"[Backup] Exported {result.Exported} tweak(s) \u2192 {dlg.FileName}" +
                             (result.Skipped > 0 ? $" ({result.Skipped} skipped \u2014 state unreadable)" : ""));
                ShowStatus(_exportStatus, $"Exported {result.Exported} tweak(s) to {Path.GetFileName(dlg.FileName)}.", ok: true);
            }
            catch (Exception ex)
            {
                Service!.Log($"[Backup] Export failed: {ex.Message}");
                ShowStatus(_exportStatus, $"Export failed: {ex.Message}", ok: false);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Import card
        // ═════════════════════════════════════════════════════════════════

        private void BuildImportCard()
        {
            var body = MakeCard(
                "\uE8B5", "Import Settings",
                "Applies every tweak from an Akari Tool settings file to this system. Tweaks already matching the file are skipped, and all toggles and dropdowns across the app update live. Some changes may require a restart to take effect.");

            var btn = MakeActionButton("Import from File\u2026", primary: false);
            btn.Click += (_, _) => ImportSettings();
            body.Children.Add(btn);

            _importStatus = MakeStatusBlock();
            body.Children.Add(_importStatus);
        }

        private void ImportSettings()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Akari Tool settings",
                Filter = "Akari Tool settings (*.json)|*.json|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // Review Mode: preview the file, show only the differing entries,
                // let the user uncheck what they want to keep.
                var preview = TweakRegistry.PreviewImport(dlg.FileName);
                var differing = preview.Entries.Where(e => e.Differs).ToList();

                if (differing.Count == 0)
                {
                    var msg0 = "Everything already matches — nothing to apply.";
                    if (preview.Unknown > 0) msg0 += $" ({preview.Unknown} entr{(preview.Unknown == 1 ? "y" : "ies")} not recognized by this version.)";
                    Service!.Log($"[Backup] Import preview: no differences ({preview.Unknown} unknown).");
                    ShowStatus(_importStatus, msg0, ok: true);
                    return;
                }

                var review = new ImportReviewDialog(differing, preview.Unknown)
                {
                    Owner = Window.GetWindow(this),
                };
                if (review.ShowDialog() != true || review.SelectedIds.Count == 0)
                {
                    ShowStatus(_importStatus, "Import cancelled — nothing was changed.", ok: true);
                    return;
                }

                var r = TweakRegistry.ImportFromFile(dlg.FileName, review.SelectedIds);
                Service!.Log($"[Backup] Import complete \u2014 {r.Applied} applied, {r.AlreadySet} already set, " +
                             $"{r.Unknown} unknown, {r.Failed} failed (of {r.Total}).");

                var msg = $"Applied {r.Applied} tweak(s). {r.AlreadySet} already matched.";
                if (r.Unknown > 0) msg += $" {r.Unknown} not recognized by this version.";
                if (r.Failed > 0)  msg += $" {r.Failed} failed.";
                if (r.Applied > 0) msg += " Some changes may need a restart.";
                ShowStatus(_importStatus, msg, ok: r.Failed == 0);
            }
            catch (Exception ex)
            {
                Service!.Log($"[Backup] Import failed: {ex.Message}");
                ShowStatus(_importStatus, $"Import failed: {ex.Message}", ok: false);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  UI helpers (matching the Advanced Tools card language)
        // ═════════════════════════════════════════════════════════════════

        private StackPanel MakeCard(string glyph, string title, string description)
        {
            var card = new Border
            {
                Background = TweakHelpers.CardBackground(),
                BorderBrush = TweakHelpers.Token("AkariOverlayMedium"),
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 14),
                Effect = TweakHelpers.CardShadow(),
            };

            var outer = new StackPanel();

            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            header.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 16,
                Foreground = TweakHelpers.IconNeutral,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
            });
            header.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
            });
            outer.Children.Add(header);

            outer.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 13,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
            });

            var body = new StackPanel();
            outer.Children.Add(body);
            card.Child = outer;
            RootPanel.Children.Add(card);
            return body;
        }

        private Button MakeActionButton(string label, bool primary)
        {
            var b = new Button
            {
                Content = label,
                FontSize = 13,
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left,
                Style = (Style)Application.Current.Resources[primary ? "RunBtn" : "GridBtn"],
            };
            if (!primary)
            {
                b.Foreground = TweakHelpers.TextPrimary;
                b.BorderBrush = TweakHelpers.Hairline;
                b.BorderThickness = new Thickness(1);
            }
            return b;
        }

        private TextBlock MakeStatusBlock() => new()
        {
            FontSize = 12,
            FontFamily = MonoFont,
            Foreground = TweakHelpers.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        private static void ShowStatus(TextBlock block, string text, bool ok)
        {
            block.Text = text;
            block.Foreground = ok ? TweakHelpers.SuccessFg : TweakHelpers.DangerFg;
            block.Visibility = Visibility.Visible;
        }
    }
}
