using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AkariTool.Services;

namespace AkariTool.Tabs.Verify
{
    /// <summary>
    /// Verify tab — drift's permanent home. Compares every tweak Akari Tool has
    /// applied (the drift baseline) against what the system currently reports,
    /// groups the mismatches, and offers re-apply / stop-tracking per tweak.
    ///
    /// Wording stays conservative: a mismatch is an observation, not proof that
    /// Windows Update caused it. The "changed to another value" group is never
    /// bulk re-applied because those usually reflect deliberate user choices.
    /// </summary>
    public partial class VerifyTab : BaseTab
    {
        // Segoe Fluent glyphs, built at runtime to keep PUA code points out of source.
        private static readonly string GlyphBaseline = char.ConvertFromUtf32(0xE9D9);   // diagnostic
        private static readonly string GlyphMatch    = char.ConvertFromUtf32(0xE73E);   // checkmark
        private static readonly string GlyphWarn     = char.ConvertFromUtf32(0xE7BA);   // warning

        private TextBlock _status = null!;
        private StackPanel _results = null!;

        public VerifyTab() => InitializeComponent();

        public override string NavTag   => "Verify";
        public override string NavLabel => "Verify";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Verify",
                "Windows Update can silently revert registry tweaks. This checks every setting Akari Tool has applied against what the system currently reports."));

            BuildStatusCard();

            _results = new StackPanel();
            RootPanel.Children.Add(_results);

            // Seed from any scan already done this session; otherwise say so.
            if (DriftScanner.Last is { } last)
            {
                RenderStatus(last);
                RenderResult(last);
            }
            else
            {
                _status.Text = "Last scan: not yet this session";
            }

            // Re-scan whenever the tab is shown (a scan is a read — safe to repeat).
            IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) RunScan(); };
        }

        // ═════════════════════════════════════════════════════════════════
        //  Status card
        // ═════════════════════════════════════════════════════════════════

        private void BuildStatusCard()
        {
            var body = MakeCard(
                GlyphBaseline, "Baseline",
                "The baseline records what Akari Tool wrote. Scan to compare it against what Windows currently reports.");

            _status = new TextBlock
            {
                FontSize = 12,
                FontFamily = MonoFont,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
            };
            body.Children.Add(_status);

            var btn = MakeActionButton("Scan now", primary: false);
            btn.Click += (_, _) => RunScan();
            body.Children.Add(btn);
        }

        private void RenderStatus(DriftScanResult result) =>
            _status.Text = $"Last scan: {result.ScannedAt:t}  ·  {result.Tracked} tweaks tracked  ·  build {result.CurrentOsBuild}";

        // ═════════════════════════════════════════════════════════════════
        //  Results
        // ═════════════════════════════════════════════════════════════════

        private void RenderResult(DriftScanResult result)
        {
            _results.Children.Clear();

            if (result.Tracked == 0)
            {
                MakeCard(GlyphBaseline, "Nothing tracked yet",
                    "Apply a tweak and it will be checked here from then on.");
                return;
            }

            if (!result.HasDrift)
            {
                MakeCard(GlyphMatch, "Everything matches",
                    $"All {result.Tracked} tracked tweaks are still set the way Akari Tool applied them.");
                return;
            }

            var reverted = result.Drifted.Where(d => d.Kind == DriftKind.RevertedToWindowsDefault).ToList();
            var changed  = result.Drifted.Where(d => d.Kind == DriftKind.ChangedToOther).ToList();

            if (reverted.Count > 0)
            {
                var body = MakeCard(GlyphWarn, $"Reverted to Windows default · {reverted.Count}",
                    "These are now sitting on the Windows factory value — the usual signature of a Windows Update rollback.");

                var reapplyAll = MakeActionButton("Re-apply all", primary: true);
                reapplyAll.Margin = new Thickness(0, 0, 0, 12);
                reapplyAll.Click += (_, _) =>
                {
                    if (AkariDialogs.ConfirmOkCancel(
                            $"Re-apply {reverted.Count} tweak{(reverted.Count == 1 ? "" : "s")} to the value Akari Tool originally set?",
                            "Re-apply reverted tweaks"))
                        ReapplyItems(reverted);
                };
                body.Children.Add(reapplyAll);

                foreach (var item in reverted)
                    body.Children.Add(BuildItemRow(item));
            }

            if (changed.Count > 0)
            {
                var body = MakeCard(GlyphWarn, $"Changed to a different value · {changed.Count}",
                    "These no longer match what Akari set, but they are not on the Windows default either. That usually means a deliberate change — review before re-applying.");

                foreach (var item in changed)
                    body.Children.Add(BuildItemRow(item));
            }
        }

        // ── Per-item row (used by both groups) ────────────────────────────

        private FrameworkElement BuildItemRow(DriftItem item)
        {
            var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // re-apply
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // stop tracking

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(info, 0);

            info.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                TextWrapping = TextWrapping.Wrap,
            });

            var detail = new TextBlock
            {
                FontSize = 11.5,
                Foreground = TweakHelpers.TextMuted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            };
            detail.Inlines.Add(new System.Windows.Documents.Run(
                $"{item.RecordedDisplay} → {item.CurrentDisplay} · set {item.RecordedAt.ToLocalTime():d MMM} on build {item.RecordedOsBuild}"));
            if (item.AcrossOsUpdate)
                detail.Inlines.Add(new System.Windows.Documents.Run(" · changed across an OS update")
                {
                    Foreground = TweakHelpers.WarnFg,
                });
            info.Children.Add(detail);
            row.Children.Add(info);

            var reapply = MakeActionButton("Re-apply", primary: false);
            reapply.Margin = new Thickness(8, 0, 0, 0);
            reapply.VerticalAlignment = VerticalAlignment.Center;
            reapply.Click += (_, _) => ReapplyItems(new[] { item });
            Grid.SetColumn(reapply, 1);
            row.Children.Add(reapply);

            var stop = new Button
            {
                Content = "Stop tracking",
                FontSize = 12,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(6, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = TweakHelpers.TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Stop tracking this tweak — it will no longer be checked for drift",
            };
            stop.Click += (_, _) => StopTracking(item);
            Grid.SetColumn(stop, 2);
            row.Children.Add(stop);

            return row;
        }

        // ═════════════════════════════════════════════════════════════════
        //  Actions
        // ═════════════════════════════════════════════════════════════════

        private void RunScan()
        {
            var result = DriftScanner.Scan();
            RenderStatus(result);
            RenderResult(result);
            (Application.Current.MainWindow as MainWindow)?.RefreshDriftBanner(result);
        }

        /// <summary>
        /// Re-writes the recorded value for each item. Re-applying updates the
        /// baseline entry's timestamp and OS build stamp, which is correct: the
        /// tweak has now been set on this build.
        /// </summary>
        private void ReapplyItems(IEnumerable<DriftItem> items)
        {
            int applied = 0, failed = 0;

            ExplorerRestart.BeginBatch();
            DriftBaseline.BeginBatch();
            try
            {
                foreach (var item in items)
                {
                    try
                    {
                        if (!TweakRegistry.TryGetDefinition(item.Id, out var def)) { failed++; continue; }

                        if (def.InputKind == TweakInputKind.Toggle && item.RecordedToggle is bool b)
                            TweakHelpers.ApplyToggle(def, b);
                        else if (def.InputKind == TweakInputKind.Dropdown && item.RecordedIndex is int i)
                            TweakHelpers.ApplyOption(def, i);
                        else { failed++; continue; }

                        TweakRegistry.RefreshRows(item.Id);
                        applied++;
                    }
                    catch { failed++; }
                }
            }
            finally { DriftBaseline.EndBatch(); ExplorerRestart.EndBatch(); }

            TweakHelpers.RefreshAllSectionPills();
            Service?.Log($"[VERIFY] Re-applied {applied} tweak(s)" + (failed > 0 ? $", {failed} failed" : ""));

            RunScan();   // re-scan so the tab and banner reflect reality, not assumption
        }

        private void StopTracking(DriftItem item)
        {
            DriftBaseline.Forget(item.Id);
            RunScan();
        }

        // ═════════════════════════════════════════════════════════════════
        //  UI helpers (matching the Backup tab card language)
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
            if (!string.IsNullOrEmpty(glyph))
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

            // Status card is built before _results exists → it lives directly in
            // RootPanel, above the results panel. Result cards go into _results.
            if (_results != null) _results.Children.Add(card);
            else RootPanel.Children.Add(card);
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
    }
}
