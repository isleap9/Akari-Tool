using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public static partial class TweakHelpers
    {
        // ══════════════════════════════════════════════════════════════════════
        //  TAB-LEVEL QUICK ACTIONS
        //
        //  BaseTab.PageHeader(withActions: true) places this button in the page
        //  header. It opens a dropdown with tab-wide bulk operations:
        //    • Apply all recommended   (live count of mismatched tweaks)
        //    • Restore Windows defaults
        //    • Create restore point
        //
        //  Scope = every tweak registered under the page's root panel, resolved
        //  through GetEntriesUnder (logical-tree walk, see BulkActions partial).
        //  Counts and the engine share one predicate: CollectPending.
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Builds the header "Quick actions" dropdown button for one tab page.</summary>
        public static FrameworkElement BuildQuickActionsButton(
            string tabTitle, StackPanel scopeRoot, Func<ToolService?> serviceGetter)
        {
            var fluent = new FontFamily("Segoe Fluent Icons");

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock
            {
                Text = "\uE945", // LightningBolt
                FontFamily = fluent,
                FontSize = 12,
                Foreground = Accent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0),
            });
            content.Children.Add(new TextBlock
            {
                Text = "Quick actions",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
            });
            content.Children.Add(new TextBlock
            {
                Text = "\uE70D", // ChevronDown
                FontFamily = fluent,
                FontSize = 10,
                Foreground = TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 1, 0, 0),
            });

            // Hairline accent border, chromeless button host (matches pill language).
            var border = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Accent,
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius, // design: 8 — do not change
                Padding = new Thickness(12, 7, 10, 7),
                Child = content,
            };

            var btn = BuildChromelessButton();
            btn.Content = border;
            btn.ToolTip = "Bulk actions for every tweak on this page";
            btn.MouseEnter += (_, _) => border.Background = Token("AkariOverlayMedium");
            btn.MouseLeave += (_, _) => border.Background = Brushes.Transparent;

            // Defensive: hide when the page registered no tweaks at all
            // (entries register after the header is built, so check on Loaded).
            btn.Loaded += (_, _) => btn.Dispatcher.BeginInvoke(() =>
            {
                if (GetEntriesUnder(scopeRoot).Count == 0)
                    btn.Visibility = Visibility.Collapsed;
            });

            btn.Click += (_, _) => OpenQuickActionsMenu(btn, tabTitle, scopeRoot, serviceGetter());
            return btn;
        }

        // ── Dropdown menu ─────────────────────────────────────────────────────

        private static void OpenQuickActionsMenu(
            FrameworkElement anchor, string tabTitle, StackPanel scopeRoot, ToolService? service)
        {
            // Counts computed once, on open — same predicate the engine uses.
            var entries = GetEntriesUnder(scopeRoot);
            int recCount = CollectPending(entries, useRecommended: true).Count;
            int defCount = CollectPending(entries, useRecommended: false).Count;

            var menu = new ContextMenu
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                MinWidth = 260,
            };

            menu.Items.Add(BuildMenuItem(
                "\uE735", "Apply all recommended", // star
                $"{recCount} tweak{(recCount == 1 ? "" : "s")} differ on this tab",
                recCount > 0,
                () => RunTabBulk(tabTitle, scopeRoot, useRecommended: true, service)));

            menu.Items.Add(BuildMenuItem(
                "\uE777", "Restore Windows defaults", // UpdateRestore
                $"Reset every tweak on this tab — {defCount} differ",
                defCount > 0,
                () => RunTabBulk(tabTitle, scopeRoot, useRecommended: false, service)));

            menu.Items.Add(new Separator());

            menu.Items.Add(BuildMenuItem(
                "\uE81C", "Create restore point", // History
                "System snapshot before you change anything",
                true,
                () => CreateRestorePointInteractive(service)));

            menu.IsOpen = true;
        }

        private static MenuItem BuildMenuItem(
            string glyph, string title, string subtitle, bool enabled, Action onClick)
        {
            var stack = new StackPanel { Margin = new Thickness(2, 4, 2, 4) };
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
            });
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11,
                Foreground = TextSecondary,
                Margin = new Thickness(0, 1, 0, 0),
            });

            var item = new MenuItem
            {
                Header = stack,
                Icon = new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 14,
                    Foreground = Accent,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                IsEnabled = enabled,
            };
            if (!enabled) item.Opacity = 0.45;
            item.Click += (_, _) => onClick();
            return item;
        }

        // ── Bulk engine (tab scope) ───────────────────────────────────────────

        private static async void RunTabBulk(
            string tabTitle, StackPanel scopeRoot, bool useRecommended, ToolService? service)
        {
            try
            {
                var entries = GetEntriesUnder(scopeRoot);
                var work = CollectPending(entries, useRecommended);
                if (work.Count == 0) return;

                // Warning callout replaces per-tweak prompts for the whole bulk run.
                var warnedNames = work
                    .Where(w => (w.Def.InputKind == TweakInputKind.Toggle
                        ? w.Def.GetToggleWarning(w.ToggleTarget)
                        : w.Def.GetOptionWarning(w.OptionTarget)) != null)
                    .Select(w => w.Def.Name)
                    .ToList();

                bool anyRestart = work.Any(w => w.Def.RequiresRestart);

                var (confirmed, createRestorePoint) =
                    ShowBulkConfirmDialog(tabTitle, useRecommended, work.Count, warnedNames, anyRestart);
                if (!confirmed) return;

                if (createRestorePoint && service != null)
                {
                    bool rpOk = await RestorePointHelper.EnsureRestorePointAsync(service);
                    if (!rpOk)
                    {
                        // Never proceed silently past a failed restore point the user asked for.
                        if (!ShowOwnedConfirm(
                            "The restore point could not be created (System Restore may be disabled " +
                            "or requires administrator rights).\n\nApply the tweaks anyway?",
                            "Restore point failed", primaryText: "Continue anyway"))
                            return;
                    }
                }

                int applied = 0, skipped = 0, failed = 0;
                ExplorerRestart.BeginBatch();
                DriftBaseline.BeginBatch();     // coalesce per-tweak baseline writes into one
                bool restarted;
                try
                {
                    foreach (var (def, refresh, toggleTarget, optionTarget) in work)
                    {
                        try
                        {
                            // Re-check right before applying — state may have drifted
                            // between the dialog and the run (same guard as import).
                            if (!IsMismatched(def, toggleTarget, optionTarget)) { skipped++; }
                            else if (def.InputKind == TweakInputKind.Toggle) { ApplyToggle(def, toggleTarget); applied++; }
                            else { ApplyOption(def, optionTarget); applied++; }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            service?.Log($"[QUICK] FAILED {def.Name}: {ex.Message}");
                        }
                        try { refresh(); } catch { }
                    }
                }
                finally { DriftBaseline.EndBatch(); restarted = ExplorerRestart.EndBatch(); }

                RefreshAllSectionPills();

                string report = $"[QUICK] {tabTitle}: applied {applied} tweak{(applied == 1 ? "" : "s")}" +
                                (skipped > 0 ? $" · {skipped} skipped (already set)" : "") +
                                (failed > 0 ? $" · {failed} failed — see log" : "") +
                                (restarted ? " · Explorer restarted" : "");
                service?.Log(report);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QUICK] RunTabBulk crashed: {ex}");
                service?.Log($"[QUICK] RunTabBulk crashed: {ex.Message}");
            }
        }

        // ── Confirmation dialog ───────────────────────────────────────────────

        private static (bool Confirmed, bool CreateRestorePoint) ShowBulkConfirmDialog(
            string tabTitle, bool useRecommended, int count, List<string> warnedNames, bool anyRestart)
        {
            var body = new StackPanel { MaxWidth = 440 };

            string valueWord = useRecommended ? "recommended" : "Windows default";
            body.Children.Add(new TextBlock
            {
                Text = $"{count} tweak{(count == 1 ? "" : "s")} on {tabTitle} differ from their " +
                       $"{valueWord} values and will be changed." +
                       (anyRestart ? " Explorer will restart once at the end." : ""),
                TextWrapping = TextWrapping.Wrap,
                Foreground = TextPrimary,
            });

            if (warnedNames.Count > 0)
            {
                var calloutText = new StackPanel();
                calloutText.Children.Add(new TextBlock
                {
                    Text = $"{warnedNames.Count} carry warnings:",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DangerFg,
                    TextWrapping = TextWrapping.Wrap,
                });
                foreach (var name in warnedNames)
                    calloutText.Children.Add(new TextBlock
                    {
                        Text = "•  " + name,
                        Foreground = DangerFg,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(4, 3, 0, 0),
                    });

                body.Children.Add(new Border
                {
                    Background = DangerBg,
                    BorderBrush = DangerBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = TweakHelpers.CardRadius,
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 12, 0, 0),
                    Child = calloutText,
                });
            }

            var restorePointBox = new CheckBox
            {
                Content = "Create a restore point first (recommended)",
                IsChecked = true,
                Margin = new Thickness(0, 14, 0, 0),
            };
            body.Children.Add(restorePointBox);

            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = useRecommended ? "Apply all recommended settings?" : "Restore Windows defaults?",
                Content = body,
                PrimaryButtonText = useRecommended ? $"Apply {count} tweaks" : $"Reset {count} tweaks",
                CloseButtonText = "Cancel",
                Owner = Application.Current.MainWindow,
            };

            bool ok = PumpDialog(box) == Wpf.Ui.Controls.MessageBoxResult.Primary;
            return (ok, ok && restorePointBox.IsChecked == true);
        }

        // ── Restore point (menu item 3) ───────────────────────────────────────

        private static async void CreateRestorePointInteractive(ToolService? service)
        {
            try
            {
                if (service == null) return;
                service.Log("[RESTORE] Creating restore point (Quick actions)…");
                bool ok = await RestorePointHelper.EnsureRestorePointAsync(service);
                ShowOwnedInfo(
                    ok ? "Restore point created (or a recent one already exists)."
                       : "Restore point creation failed — System Restore may be disabled for C:. See the log for details.",
                    ok ? "Restore point ready" : "Restore point failed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QUICK] CreateRestorePointInteractive crashed: {ex}");
                service?.Log($"[QUICK] CreateRestorePointInteractive crashed: {ex.Message}");
            }
        }

        // ── Owned dialog helpers ──────────────────────────────────────────────
        // Same sync-pump pattern as AkariDialogs, but with Owner always set to
        // the main window so the box centers over the app.

        private static bool ShowOwnedConfirm(string message, string title, string primaryText)
        {
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
                PrimaryButtonText = primaryText,
                CloseButtonText = "Cancel",
                Owner = Application.Current.MainWindow,
            };
            return PumpDialog(box) == Wpf.Ui.Controls.MessageBoxResult.Primary;
        }

        private static void ShowOwnedInfo(string message, string title)
        {
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
                CloseButtonText = "OK",
                Owner = Application.Current.MainWindow,
            };
            PumpDialog(box);
        }

        private static Wpf.Ui.Controls.MessageBoxResult PumpDialog(Wpf.Ui.Controls.MessageBox box)
        {
            var task = box.ShowDialogAsync();
            if (!task.IsCompleted)
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                task.ContinueWith(_ => frame.Continue = false,
                    TaskScheduler.FromCurrentSynchronizationContext());
                System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            return task.GetAwaiter().GetResult();
        }
    }
}
