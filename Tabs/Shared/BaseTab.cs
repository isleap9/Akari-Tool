using Microsoft.UI.Text;                 // FontWeights
using Microsoft.UI.Xaml;                 // Thickness, GridLength, Visibility, FrameworkElement, UIElement
using Microsoft.UI.Xaml.Controls;        // UserControl, StackPanel, Grid, Button, TextBlock, AutoSuggestBox
using Microsoft.UI.Xaml.Documents;       // Run
using Microsoft.UI.Xaml.Media;           // Brush, FontFamily
using AkariTool.Services;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Base class for all tab UserControls (WinUI 3).
    /// Provides AddItem / AddSectionTitle helpers and holds the shared ToolService.
    ///
    /// Public method signatures are preserved from the WPF version so the ~13 tabs
    /// port without per-call rewrites; only the internals are reimplemented against
    /// WinUI controls.
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

        public virtual void Initialize(ToolService service)
        {
            Service = service;
        }

        /// <summary>Nav tag this tab is registered under in MainWindow (e.g. "Gaming").</summary>
        public virtual string NavTag => "";

        /// <summary>Human label shown in global search results.</summary>
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

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(buttons, 1);

            // Run = native accent button (crimson comes from the AccentFillColor override).
            var runButton = new Button
            {
                Content = "Run",
                Style = (Style)Application.Current.Resources["AccentButtonStyle"]
            };
            runButton.Click += async (_, _) =>
                await Service!.RunWithTracking(action, title, AppliedTweaks);
            buttons.Children.Add(runButton);

            if (undoAction is not null)
            {
                var undoButton = new Button { Content = "Undo" };   // native default style
                undoButton.Click += async (_, _) => await Service!.RunAction(undoAction);
                buttons.Children.Add(undoButton);
            }

            row.Children.Add(info);
            row.Children.Add(buttons);
            panel.Children.Add(row);
        }

        // ── Page header (title + optional subtitle) ────────────────────────────

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
            ["Sound"] = new[] { "OPTIMIZE", "SOUND" },
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
        /// Two-column page header for tabs with registered tweaks: crumb/title/subtitle
        /// on the left, a [Quick actions] [search] cluster bottom-aligned on the right.
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

            var cluster = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(16, 0, 0, 0)
            };
            Grid.SetColumn(cluster, 1);
            Grid.SetRow(cluster, 0);

            var quickActions = TweakHelpers.BuildQuickActionsButton(title, root, () => Service);
            cluster.Children.Add(quickActions);
            cluster.Children.Add(BuildHeaderSearchBox(root));

            grid.Children.Add(cluster);
            return grid;
        }

        /// <summary>Shared eyebrow-crumb + title (+ optional subtitle) builder.</summary>
        private static void BuildHeaderStack(StackPanel stack, string title, string subtitle)
        {
            if (_crumbs.TryGetValue(title, out var segs))
            {
                var mono = (FontFamily)(Application.Current.Resources["MonoFont"] ?? new FontFamily("Consolas"));
                var eyebrow = new TextBlock { FontFamily = mono, FontSize = 10, Margin = new Thickness(0, 0, 0, 9) };
                char sep = (char)0x25B8; // ▸
                for (int i = 0; i < segs.Length; i++)
                {
                    if (i > 0)
                        eyebrow.Inlines.Add(new Run { Text = $"  {sep}  ", Foreground = TweakHelpers.TextMuted });
                    eyebrow.Inlines.Add(new Run { Text = segs[i], Foreground = TweakHelpers.TextMuted });
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
        /// Header search box (right cluster). Filters the tweak sections under
        /// <paramref name="root"/>. WinUI AutoSuggestBox (built-in placeholder + clear
        /// + query icon) replaces the WPF-UI TextBox; the focus-grow animation is dropped.
        /// </summary>
        private AutoSuggestBox BuildHeaderSearchBox(StackPanel root)
        {
            var searchBox = new AutoSuggestBox
            {
                PlaceholderText = "Search tweaks…",
                QueryIcon = new SymbolIcon(Symbol.Find),
                FontSize = 13,
                Width = 260,
                VerticalAlignment = VerticalAlignment.Center,
            };
            searchBox.TextChanged += (_, _) => FilterTweaks(root, searchBox.Text.Trim());
            _headerSearchBox = searchBox;
            return searchBox;
        }

        private AutoSuggestBox? _headerSearchBox;

        /// <summary>Applies a query to this tab's own header search box.</summary>
        public void ApplySearch(string query)
        {
            if (_headerSearchBox is null) return;
            _headerSearchBox.Text = query;
            _headerSearchBox.Focus(FocusState.Programmatic);
        }

        // ── Section heading ────────────────────────────────────────────────────

        public void AddSectionTitle(StackPanel panel, string text,
            int fontSize = 14, string? color = null, int topMargin = 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = color is null ? TweakHelpers.TextSecondary : BrushFrom(color),
                FontSize = fontSize,
                FontWeight = fontSize > 14 ? FontWeights.Bold : FontWeights.Normal,
                Margin = new Thickness(0, topMargin, 0, 8)
            });
        }

        // ── Search bar ─────────────────────────────────────────────────────────

        /// <summary>
        /// Inserts a search box after the page header and wires it to filter all tagged
        /// tweak rows across sections in <paramref name="root"/>.
        /// </summary>
        protected void AttachSearch(StackPanel root)
        {
            var searchBox = new AutoSuggestBox
            {
                PlaceholderText = "Search tweaks…",
                QueryIcon = new SymbolIcon(Symbol.Find),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20),
            };
            searchBox.TextChanged += (_, _) => FilterTweaks(root, searchBox.Text.Trim());
            root.Children.Add(searchBox);
        }

        /// <summary>
        /// Walks the RootPanel tree and shows/hides tagged tweak rows. Section cards
        /// (Border) and their label (TextBlock above them) are hidden when all their
        /// rows are hidden.
        /// </summary>
        private static void FilterTweaks(StackPanel root, string query)
        {
            bool empty = query.Length == 0;
            string q = query.ToLowerInvariant();

            TextBlock? pendingLabel = null;

            foreach (UIElement child in root.Children)
            {
                if (child is Border sectionCard && sectionCard.Child is StackPanel sectionContent)
                {
                    var body = sectionContent.Children.OfType<StackPanel>()
                        .FirstOrDefault(p => (p.Tag as string) == "sectionbody") ?? sectionContent;

                    bool anyVisible = FilterSectionCard(body, q, empty);

                    if (TweakHelpers.SectionCollapseStates.TryGetValue(body, out var st))
                    {
                        st.ForcedOpenBySearch = !empty && anyVisible;
                        st.Render();
                    }

                    sectionCard.Visibility = anyVisible || empty
                        ? Visibility.Visible : Visibility.Collapsed;

                    if (pendingLabel != null)
                        pendingLabel.Visibility = sectionCard.Visibility;
                    pendingLabel = null;
                }
                else if (child is TextBlock label && label.Tag is null)
                {
                    pendingLabel = label;
                }
                else
                {
                    child.Visibility = Visibility.Visible;
                    pendingLabel = null;
                }
            }
        }

        private static bool FilterSectionCard(StackPanel content, string q, bool empty)
        {
            bool anyVisible = false;

            // Row dividers are Borders tagged "separator" (WinUI has no Separator control);
            // track the divider before each row so it hides with its row.
            FrameworkElement? pendingSep = null;

            foreach (UIElement item in content.Children)
            {
                var fe = item as FrameworkElement;
                string? tag = fe?.Tag as string;

                if (tag == "separator")
                {
                    pendingSep = fe;
                    continue;
                }

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
