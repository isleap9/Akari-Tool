using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Base class for all tab UserControls.
    /// Provides AddItem / AddSectionTitle helpers and holds the shared ToolService.
    ///
    /// Each tab:
    ///   1. Inherits this class instead of UserControl directly.
    ///   2. Calls Initialize(service) from MainWindow after construction.
    ///   3. Has a root StackPanel named RootPanel in its XAML.
    ///   4. Calls Build() (its own method) inside Initialize() to populate rows.
    /// </summary>
    public class BaseTab : UserControl
    {
        protected ToolService? Service;
        protected readonly List<string> AppliedTweaks = [];

        /// <summary>
        /// Called by MainWindow once after construction to inject the shared service.
        /// Override in each tab to call Build() after base.Initialize().
        /// </summary>
        public virtual void Initialize(ToolService service)
        {
            Service = service;
        }

        /// <summary>
        /// Nav tag this tab is registered under in MainWindow (e.g. "Gaming"). Override per tab.
        /// Empty means the tab's rows are not attributed to any tab in global search.
        /// Public because Build() runs inside each tab's own Initialize override, so the
        /// registry bracketing happens in MainWindow around the Initialize call.
        /// </summary>
        public virtual string NavTag => "";

        /// <summary>Human label shown in global search results (e.g. "Gaming &amp; Performance").</summary>
        public virtual string NavLabel => "";

        // ── Row factory ────────────────────────────────────────────────────────

        /// <summary>Convenience overload: accepts script file name strings.</summary>
        public void AddItem(StackPanel panel, string title, string description,
            string scriptName, string? undoScriptName = null)
        {
            AddItem(panel, title, description,
                new ScriptAction(scriptName),
                undoScriptName is null ? null : new ScriptAction(undoScriptName));
        }

        /// <summary>Main row factory: text on the left, Run [Undo] buttons on the right.</summary>
        public void AddItem(StackPanel panel, string title, string description,
            RunAction action, RunAction? undoAction = null)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: title + description
            var info = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetColumn(info, 0);

            info.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = TweakHelpers.TextPrimary,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            });
            info.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = TweakHelpers.TextSecondary,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });

            // Right: Run [Undo]
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(buttons, 1);

            var runButton = new Button
            {
                Content = "Run",
                Style = (Style)FindResource("RunBtn")
            };
            runButton.Click += async (_, _) =>
                await Service!.RunWithTracking(action, title, AppliedTweaks);
            buttons.Children.Add(runButton);

            if (undoAction is not null)
            {
                var undoButton = new Button
                {
                    Content = "Undo",
                    Style = (Style)FindResource("UndoBtn")
                };
                undoButton.Click += async (_, _) => await Service!.RunAction(undoAction);
                buttons.Children.Add(undoButton);
            }

            row.Children.Add(info);
            row.Children.Add(buttons);
            panel.Children.Add(row);
        }

        // ── Page header (title + optional subtitle) ────────────────────────────

        /// <summary>
        /// Standard tab page header: display font 27px SemiBold + muted subtitle.
        /// Call this as the first child of RootPanel.Children in every tab's Build().
        /// </summary>
        // Page eyebrow / crumb path (section ▸ page). Rendered above each title.
        private static readonly Dictionary<string, string[]> _crumbs = new()
        {
            ["Windows Apps"] = new[] { "SOFTWARE", "WINDOWS APPS" },
            ["External Apps"] = new[] { "SOFTWARE", "EXTERNAL APPS" },
            ["Debloat"] = new[] { "SOFTWARE", "DEBLOAT" },
            ["AkariOS"] = new[] { "OPTIMIZE", "AKARIOS" },
            ["Gaming & Performance"] = new[] { "OPTIMIZE", "GAMING" },
            ["Privacy & Security"] = new[] { "OPTIMIZE", "PRIVACY" },
            ["Windows Updates"] = new[] { "OPTIMIZE", "UPDATE" },
            ["Notifications"] = new[] { "OPTIMIZE", "NOTIFICATIONS" },
            ["Power"] = new[] { "OPTIMIZE", "POWER" },
            ["Taskbar"] = new[] { "CUSTOMIZE", "TASKBAR" },
            ["Explorer"] = new[] { "CUSTOMIZE", "EXPLORER" },
            ["Context Menu"] = new[] { "CUSTOMIZE", "CONTEXT MENU" },
            ["Appearance"] = new[] { "CUSTOMIZE", "APPEARANCE" },
            ["Start Menu"] = new[] { "CUSTOMIZE", "START MENU" },
            ["Desktop"] = new[] { "CUSTOMIZE", "DESKTOP" },
            ["Tools"] = new[] { "TOOLS" },
            ["Advanced Tools"] = new[] { "TOOLS", "ADVANCED" },
            ["Backup & Restore"] = new[] { "ADVANCED", "BACKUP" },
        };

        protected static FrameworkElement PageHeader(string title, string subtitle = "")
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, subtitle.Length > 0 ? 0 : 24) };
            BuildHeaderStack(stack, title, subtitle);
            return stack;
        }

        /// <summary>
        /// Two-column page header for tabs with registered tweaks: the usual
        /// crumb/title/subtitle on the left, a [Quick actions] [search] cluster
        /// bottom-aligned to the title on the right. The search box filters the
        /// tweak sections under <paramref name="root"/> (replaces AttachSearch),
        /// and Quick Actions scopes its bulk operations to the same panel.
        /// </summary>
        protected FrameworkElement PageHeader(string title, string subtitle, bool withActions,
            StackPanel root)
        {
            if (!withActions) return PageHeader(title, subtitle);

            var grid = new Grid { Tag = "pageheader", Margin = new Thickness(0, 0, 0, subtitle.Length > 0 ? 0 : 24) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0 left: eyebrow + title. Row 1 (col-span): subtitle — keeps the
            // right cluster bottom-aligned to the title baseline area.
            var titleStack = new StackPanel();
            BuildHeaderStack(titleStack, title, subtitle: "");
            Grid.SetColumn(titleStack, 0);
            Grid.SetRow(titleStack, 0);
            grid.Children.Add(titleStack);

            if (subtitle.Length > 0)
            {
                var sub = new TextBlock
                {
                    Text = subtitle,
                    FontSize = 14.5,
                    Foreground = TweakHelpers.TextSecondary,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 11, 0, 26)
                };
                Grid.SetRow(sub, 1);
                Grid.SetColumn(sub, 0);
                Grid.SetColumnSpan(sub, 2);
                grid.Children.Add(sub);
            }

            // Right cluster: [Quick actions] [search], 10px gap.
            var cluster = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(16, 0, 0, 0)
            };
            Grid.SetColumn(cluster, 1);
            Grid.SetRow(cluster, 0);

            var quickActions = TweakHelpers.BuildQuickActionsButton(title, root, () => Service);
            quickActions.Margin = new Thickness(0, 0, 10, 0);
            cluster.Children.Add(quickActions);
            cluster.Children.Add(BuildHeaderSearchBox(root));

            grid.Children.Add(cluster);
            return grid;
        }

        /// <summary>Shared eyebrow-crumb + title (+ optional subtitle) builder.</summary>
        private static void BuildHeaderStack(StackPanel stack, string title, string subtitle)
        {

            // Eyebrow crumb — mono, uppercase, grey with a red ▸ separator
            if (_crumbs.TryGetValue(title, out var segs))
            {
                var mono = (FontFamily)(Application.Current.Resources["MonoFont"] ?? new FontFamily("Consolas"));
                var eyebrow = new TextBlock { FontFamily = mono, FontSize = 10, Margin = new Thickness(0, 0, 0, 9) };
                char sep = (char)0x25B8; // ▸
                for (int i = 0; i < segs.Length; i++)
                {
                    if (i > 0)
                        eyebrow.Inlines.Add(new System.Windows.Documents.Run($"  {sep}  ") { Foreground = TweakHelpers.TextMuted });
                    eyebrow.Inlines.Add(new System.Windows.Documents.Run(segs[i]) { Foreground = TweakHelpers.TextMuted });
                }
                stack.Children.Add(eyebrow);
            }

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = DisplayFont,
                FontSize = 25,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary
            });
            if (subtitle.Length > 0)
                stack.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    FontSize = 14.5,
                    Foreground = TweakHelpers.TextSecondary,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 11, 0, 26)
                });
        }

        /// <summary>
        /// Header search box (right cluster): 230px wide, grows to 300px with a
        /// quick 120ms animation while keyboard-focused. Filters the tweak
        /// sections under <paramref name="root"/>.
        /// </summary>
        private Wpf.Ui.Controls.TextBox BuildHeaderSearchBox(StackPanel root)
        {
            var searchBox = new Wpf.Ui.Controls.TextBox
            {
                PlaceholderText = "Search tweaks…",
                Icon = new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Search24),
                ClearButtonEnabled = true,
                FontSize = 13,
                Height = 38,
                Width = 230,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = TweakHelpers.CardBg,
                BorderBrush = TweakHelpers.Hairline,
                CaretBrush = TweakHelpers.Accent,
                SelectionBrush = TweakHelpers.Accent,
            };
            searchBox.TextChanged += (_, _) => FilterTweaks(root, searchBox.Text.Trim());

            void AnimateWidth(double to)
            {
                searchBox.BeginAnimation(WidthProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(to, TimeSpan.FromMilliseconds(120))
                    {
                        EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    });
            }
            searchBox.GotKeyboardFocus  += (_, _) => AnimateWidth(300);
            searchBox.LostKeyboardFocus += (_, _) => AnimateWidth(230);
            _headerSearchBox = searchBox;
            return searchBox;
        }

        private Wpf.Ui.Controls.TextBox? _headerSearchBox;

        /// <summary>
        /// Applies a query to this tab's own header search box (fires its TextChanged, so the
        /// existing FilterTweaks + section auto-expand run unchanged).
        /// </summary>
        public void ApplySearch(string query)
        {
            if (_headerSearchBox is null) return;
            _headerSearchBox.Text = query;
            _headerSearchBox.Focus();
            _headerSearchBox.CaretIndex = _headerSearchBox.Text.Length;
        }

        // ── Section heading ────────────────────────────────────────────────────

        public void AddSectionTitle(StackPanel panel, string text,
            int fontSize = 14, string? color = null, int topMargin = 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                // null = the live-updating token; an explicit colour stays literal.
                Foreground = color is null ? TweakHelpers.TextSecondary : BrushFrom(color),
                FontSize = fontSize,
                FontWeight = fontSize > 14 ? FontWeights.Bold : FontWeights.Normal,
                Margin = new Thickness(0, topMargin, 0, 8)
            });
        }

        // ── Search bar ─────────────────────────────────────────────────────────

        /// <summary>
        /// Inserts a search TextBox after the page header and wires it to filter
        /// all tagged tweak rows across sections in <paramref name="root"/>.
        /// Call once per tab's Build(), right after RootPanel.Children.Add(PageHeader(...)).
        /// </summary>
        protected void AttachSearch(StackPanel root)
        {
            // WPF-UI TextBox: built-in placeholder, leading icon and clear button
            // replace the old Border + watermark-TextBlock + manual clear button.
            var searchBox = new Wpf.Ui.Controls.TextBox
            {
                PlaceholderText = "Search tweaks…",
                Icon = new Wpf.Ui.Controls.SymbolIcon(Wpf.Ui.Controls.SymbolRegular.Search24),
                ClearButtonEnabled = true,
                FontSize = 14,
                Height = 40,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
                Background = TweakHelpers.CardBg,
                BorderBrush = TweakHelpers.Hairline,
                CaretBrush = TweakHelpers.Accent,
                SelectionBrush = TweakHelpers.Accent,
            };
            searchBox.TextChanged += (_, _) => FilterTweaks(root, searchBox.Text.Trim());
            root.Children.Add(searchBox);
        }

        /// <summary>
        /// Walks the RootPanel tree and shows/hides tagged tweak rows.
        /// Section cards (Border) and their label (TextBlock above them) are
        /// hidden when all their rows are hidden.
        /// </summary>
        private static void FilterTweaks(StackPanel root, string query)
        {
            bool empty = query.Length == 0;
            string q = query.ToLowerInvariant();

            // Children are identified by type/tag, not position: a Border whose
            // child is a StackPanel is a section card (from BuildSection); an
            // untagged TextBlock directly before a card is its label. Everything
            // else (the header Grid — which now hosts the search box — preset
            // cards, standalone controls) is left visible.
            TextBlock? pendingLabel = null;

            foreach (UIElement child in root.Children)
            {
                // Skip the search bar itself (no Tag) and page header
                if (child is Border sectionCard && sectionCard.Child is StackPanel sectionContent)
                {
                    // This is a section card — filter its rows. BuildSection cards nest
                    // their rows one level deeper, inside the tagged collapsible body;
                    // cards built elsewhere are still filtered directly.
                    var body = sectionContent.Children.OfType<StackPanel>()
                        .FirstOrDefault(p => (p.Tag as string) == "sectionbody") ?? sectionContent;

                    bool anyVisible = FilterSectionCard(body, q, empty);

                    if (TweakHelpers.SectionCollapseStates.TryGetValue(body, out var st))
                    {
                        // While a query is active, force-expand sections that contain matches so
                        // results are never hidden inside a collapsed section. When the query is
                        // cleared, restore the user's own collapsed choice.
                        st.ForcedOpenBySearch = !empty && anyVisible;
                        st.Render();
                    }

                    sectionCard.Visibility = anyVisible || empty
                        ? Visibility.Visible : Visibility.Collapsed;

                    // Show/hide the label that preceded this card
                    if (pendingLabel != null)
                        pendingLabel.Visibility = sectionCard.Visibility;
                    pendingLabel = null;
                }
                else if (child is TextBlock label && label.Tag is null)
                {
                    // Section label TextBlock — hold it until we process the card
                    pendingLabel = label;
                }
                else
                {
                    // PageHeader grid (hosts the search box), preset cards, etc. — always visible
                    child.Visibility = Visibility.Visible;
                    pendingLabel = null;
                }
            }
        }

        private static bool FilterSectionCard(StackPanel content, string q, bool empty)
        {
            bool anyVisible = false;

            // Separators are interspersed between rows; track the separator before each row
            Separator? pendingSep = null;

            foreach (UIElement item in content.Children)
            {
                if (item is Separator sep)
                {
                    pendingSep = sep;
                    continue;
                }

                string? tag = (item as FrameworkElement)?.Tag as string;

                // Bulk-action header bar and the section's own title row —
                // always visible while their section is
                if (tag == "bulkbar" || tag == "sectionheader")
                {
                    item.Visibility = Visibility.Visible;
                    pendingSep = null;
                    continue;
                }
                bool matches = empty || (tag != null && tag.StartsWith("search:") &&
                    tag[7..].ToLowerInvariant().Contains(q));

                item.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                if (pendingSep != null)
                    pendingSep.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                pendingSep = null;

                if (matches) anyVisible = true;
            }

            return anyVisible;
        }

        public static Brush BrushFrom(string color) =>
            ToolService.BrushFrom(color);

        /// <summary>Display/heading font from App resources, with safe fallback.</summary>
        protected static FontFamily DisplayFont =>
            (Application.Current?.Resources["DisplayFont"] as FontFamily) ?? new FontFamily("Segoe UI");

        /// <summary>Label/console mono font from App resources, with safe fallback.</summary>
        protected static FontFamily MonoFont =>
            (Application.Current?.Resources["MonoFont"] as FontFamily) ?? new FontFamily("Consolas");
    }
}
