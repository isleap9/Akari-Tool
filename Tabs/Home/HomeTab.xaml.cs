using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class HomeTab : BaseTab
    {
        private Action<string>? _navigate;

        // Home content shown when search is empty
        private FrameworkElement? _homeContent;

        // Results panel shown when search has text
        private StackPanel? _resultsPanel;
        private Border?     _resultsCard;

        // Registered search sources: (display label, root panel to scan, navigate action)
        private readonly List<(string Label, StackPanel Root, Action Navigate)> _searchSources = [];

        public HomeTab() => InitializeComponent();

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        // Called by MainWindow to wire up card navigation
        public void SetNavigationCallback(Action<string> navigate)
        {
            _navigate = navigate;
        }

        /// <summary>
        /// Called by MainWindow after all tabs are initialized.
        /// Registers each tab's RootPanel as a global search source.
        /// </summary>
        public void SetupGlobalSearch(IEnumerable<(string Label, StackPanel Root, Action Navigate)> sources)
        {
            _searchSources.Clear();
            _searchSources.AddRange(sources);
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Akari Tool",
                "Your control center for Windows — optimization, software & utilities"));

            // ── System information banner ─────────────────────────────────
            BuildSystemBanner();

            // ── Global search bar ─────────────────────────────────────────
            BuildGlobalSearchBar();

            // ── Home content (cards) — hidden when searching ──────────────
            var homeStack = new StackPanel();
            _homeContent = homeStack;

            // All-tabs quick access — grouped like the sidebar
            AddCardSection(homeStack, "SOFTWARE", isFirst: true, new[]
            {
                CardDef("Windows Apps",     "\uE71D", "Manage built-in Windows apps & features",            "Bloatware"),
                CardDef("External Apps",    "\uE896", "Install apps via WinGet",                            "AppInstaller"),
                CardDef("Debloat",          "\uE74D", "Remove bloatware, Edge & OneDrive",                  "Debloat"),
            });

            AddCardSection(homeStack, "OPTIMIZE", isFirst: false, new[]
            {
                CardDef("AkariOS",          "\uE734", "AkariOS presets & environment tools",                "AkariOS"),
                CardDef("Gaming",           "\uE7FC", "Latency, GPU & gaming performance tweaks",           "Gaming"),
                CardDef("Privacy",          "\uE72E", "Telemetry, tracking & privacy hardening",            "Privacy"),
                CardDef("Update",           "\uE895", "Windows Update behavior & policies",                 "Update"),
                CardDef("Notifications",    "\uEA8F", "Notification, tips & suggestion controls",           "Notifications"),
                CardDef("Power",            "\uE945", "Power plans & performance settings",                 "Power"),
                CardDef("Customize",        "\uE771", "Taskbar, Explorer, Start menu & context menu",       "Taskbar"),
                CardDef("Tools",            "\uE90F", "System info, repair, network & maintenance",         "Tools"),
            });

            AddCardSection(homeStack, "ADVANCED", isFirst: false, new[]
            {
                CardDef("Advanced Tools",   "\uEC7A", "ISO wizard & unattended setup builder",              "Advanced"),
                CardDef("Backup & Restore", "\uE74E", "Export & import your tweak configuration",           "Backup"),
                CardDef("Verify System",    "\uE7BA", "Track tweaks Windows reverted",                      "Verify"),
            });

            RootPanel.Children.Add(homeStack);

            // ── Results panel (initially hidden) ─────────────────────────
            _resultsPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            _resultsCard = new Border
            {
                Background      = TweakHelpers.CardBackground(),
                BorderBrush     = TweakHelpers.CardElevationBorder,
                BorderThickness = new Thickness(1),
                CornerRadius    = TweakHelpers.CardRadius,
                Padding         = new Thickness(18, 10, 18, 10),
                Child           = _resultsPanel,
                Visibility      = Visibility.Collapsed
            };
            // The field stays the INNER border: collapsing it leaves the shadow
            // wrapper empty, which paints nothing, so the toggle still works.
            RootPanel.Children.Add(TweakHelpers.ShadowWrapCard(_resultsCard));
        }

        // ── System information banner ─────────────────────────────────────

        // Held so the background WMI gather can fill them in once it completes.
        private TextBlock? _sysEdition, _sysVersion, _sysCpu, _sysGpu, _sysMemory;

        private void BuildSystemBanner()
        {
            var inner = new StackPanel();

            _sysEdition = new TextBlock
            {
                Text       = "Detecting…",
                FontSize   = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary
            };
            _sysVersion = new TextBlock
            {
                Text       = "Detecting…",
                FontSize   = 12,
                Foreground = TweakHelpers.TextSecondary,
                Margin     = new Thickness(0, 2, 0, 0)
            };
            inner.Children.Add(_sysEdition);
            inner.Children.Add(_sysVersion);

            inner.Children.Add(new Border
            {
                Height          = 1,
                Background      = TweakHelpers.Hairline,
                Margin          = new Thickness(0, 14, 0, 14)
            });

            var stats = new Grid();
            for (int c = 0; c < 3; c++)
                stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _sysCpu    = AddStat(stats, 0, "PROCESSOR");
            _sysGpu    = AddStat(stats, 1, "GRAPHICS");
            _sysMemory = AddStat(stats, 2, "MEMORY");
            inner.Children.Add(stats);

            RootPanel.Children.Add(TweakHelpers.ShadowWrapCard(new Border
            {
                Background      = TweakHelpers.CardBg,
                BorderBrush     = TweakHelpers.CardElevationBorder,
                BorderThickness = new Thickness(1),
                CornerRadius    = TweakHelpers.CardRadius,
                Padding         = new Thickness(20),
                Margin          = new Thickness(0, 0, 0, 16),
                Child           = inner
            }));

            // WMI takes a few hundred ms — never on the tab-construction path.
            // LIVE DATA: the gather runs off-thread and marshals back via the
            // WinUI DispatcherQueue (WPF's Dispatcher.Invoke has no equivalent).
            // TryEnqueue is fire-and-forget where Invoke was synchronous — fine
            // here (the UI just fills in when the data arrives).
            _ = Task.Run(() =>
            {
                var info = SystemInfoService.Gather();
                DispatcherQueue.TryEnqueue(() =>
                {
                    _sysEdition.Text = info.Edition;
                    _sysVersion.Text = info.Version;
                    SetStat(_sysCpu,    info.Cpu);
                    SetStat(_sysGpu,    info.Gpu);
                    SetStat(_sysMemory, info.Memory);
                });
            });
        }

        /// <summary>One label/value stat column; returns the value TextBlock to fill in later.</summary>
        private static TextBlock AddStat(Grid grid, int column, string label)
        {
            var stack = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 12, 0, 12, 0) };
            stack.Children.Add(new TextBlock
            {
                Text       = label,
                FontSize   = 10,
                Foreground = TweakHelpers.TextMuted,
                // WPF TextBlock has no letter-spacing; the mono face gives these eyebrow
                // labels the same airy look as the page crumbs.
                FontFamily = (FontFamily)(Application.Current.Resources["MonoFont"] ?? new FontFamily("Consolas"))
            });
            var value = new TextBlock
            {
                Text         = "Detecting…",
                FontSize     = 13,
                Foreground   = TweakHelpers.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin       = new Thickness(0, 3, 0, 0)
            };
            stack.Children.Add(value);

            Grid.SetColumn(stack, column);
            grid.Children.Add(stack);
            return value;
        }

        /// <summary>Sets a stat value and carries the full string in a tooltip (GPU names are long).</summary>
        private static void SetStat(TextBlock? block, string value)
        {
            if (block is null) return;
            block.Text = value;
            ToolTipService.SetToolTip(block, value);
        }

        // ── Global search bar ─────────────────────────────────────────────

        private void BuildGlobalSearchBar()
        {
            // WinUI AutoSuggestBox: built-in placeholder, query icon and clear button
            // (replaces the WPF-UI TextBox; CaretBrush/SelectionBrush have no direct
            // per-instance equivalent — the crimson caret/selection come from the
            // TextControl* theme overrides in App.xaml).
            var searchBox = new AutoSuggestBox
            {
                PlaceholderText = "Search all tweaks across every tab…",
                QueryIcon       = new SymbolIcon(Symbol.Find),
                FontSize        = 14,
                Height          = 44,
                Margin          = new Thickness(0, 0, 0, 20),
            };
            searchBox.TextChanged += (_, _) => RunGlobalSearch(searchBox.Text.Trim());
            RootPanel.Children.Add(searchBox);
        }

        // ── Global search logic ───────────────────────────────────────────

        private void RunGlobalSearch(string query)
        {
            if (_resultsCard == null || _resultsPanel == null || _homeContent == null) return;

            _resultsPanel.Children.Clear();

            if (query.Length == 0)
            {
                _homeContent.Visibility = Visibility.Visible;
                _resultsCard.Visibility = Visibility.Collapsed;
                return;
            }

            _homeContent.Visibility = Visibility.Collapsed;

            string q = query.ToLowerInvariant();
            int totalFound = 0;

            foreach (var (label, root, navigate) in _searchSources)
            {
                var matches = FindTaggedMatches(root, q);
                if (matches.Count == 0) continue;

                // Section header for this tab
                _resultsPanel.Children.Add(new TextBlock
                {
                    Text       = label.ToUpperInvariant(),
                    FontFamily = MonoFont,
                    FontSize   = 11.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = TweakHelpers.TextMuted,
                    Margin     = new Thickness(0, totalFound == 0 ? 4 : 14, 0, 6)
                });

                bool firstInGroup = true;
                foreach (var (name, desc) in matches)
                {
                    if (!firstInGroup)
                        _resultsPanel.Children.Add(new Border
                        {
                            Background = TweakHelpers.Token("AkariOverlayStrong"),
                            Height     = 1,
                            Margin     = new Thickness(-18, 0, -18, 0),
                            Tag        = "separator"
                        });
                    firstInGroup = false;

                    var capturedNavigate = navigate;
                    var row = BuildResultRow(name, desc, label, capturedNavigate);
                    _resultsPanel.Children.Add(row);
                    totalFound++;
                }
            }

            if (totalFound == 0)
            {
                _resultsPanel.Children.Add(new TextBlock
                {
                    Text              = "No tweaks found matching \"" + query + "\"",
                    FontSize          = 14,
                    Foreground        = TweakHelpers.TextMuted,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin            = new Thickness(0, 16, 0, 16)
                });
            }

            _resultsCard.Visibility = Visibility.Visible;
        }

        private static List<(string Name, string Desc)> FindTaggedMatches(StackPanel root, string q)
        {
            var results = new List<(string, string)>();
            ScanPanel(root, q, results);
            return results;
        }

        private static void ScanPanel(Panel panel, string q, List<(string, string)> results)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is FrameworkElement fe)
                {
                    if (fe.Tag is string tag && tag.StartsWith("search:"))
                    {
                        var payload = tag[7..]; // "Name|Desc"
                        if (payload.ToLowerInvariant().Contains(q))
                        {
                            var sep = payload.IndexOf('|');
                            var name = sep >= 0 ? payload[..sep] : payload;
                            var desc = sep >= 0 ? payload[(sep + 1)..] : "";
                            results.Add((name, desc));
                        }
                        continue; // tagged rows don't have searchable children
                    }

                    // Recurse into containers
                    if (fe is Border b && b.Child is Panel bp)  ScanPanel(bp, q, results);
                    else if (fe is Panel p)                     ScanPanel(p,  q, results);
                }
            }
        }

        private Grid BuildResultRow(string name, string desc, string tabLabel, Action navigate)
        {
            // Cursors.Hand dropped (no WinUI equivalent); Background makes the whole row hit-testable.
            var row = new Grid { Margin = new Thickness(0, 10, 0, 10), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text       = name,
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary
            });
            if (desc.Length > 0)
                info.Children.Add(new TextBlock
                {
                    Text         = desc,
                    FontSize     = 12,
                    Foreground   = TweakHelpers.TextSecondary,
                    Margin       = new Thickness(0, 2, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            Grid.SetColumn(info, 0);

            // Tab badge pill
            var badge = new Border
            {
                Background      = TweakHelpers.Token("AkariOverlayFaint"),
                BorderBrush     = TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius    = TweakHelpers.ControlRadius,
                Padding         = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin          = new Thickness(12, 0, 0, 0)
            };
            badge.Child = new TextBlock
            {
                Text       = tabLabel,
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextSecondary
            };
            Grid.SetColumn(badge, 1);

            row.Children.Add(info);
            row.Children.Add(badge);

            // Navigate on click (WinUI pointer/tap events replace the WPF mouse events)
            row.Tapped += (_, _) => { _navigate?.Invoke(tabLabel.Split(' ')[0]); navigate(); };
            row.PointerEntered += (_, _) => info.Children.OfType<TextBlock>().FirstOrDefault()!.Foreground = TweakHelpers.Accent;
            row.PointerExited  += (_, _) => info.Children.OfType<TextBlock>().FirstOrDefault()!.Foreground = TweakHelpers.TextPrimary;

            return row;
        }

        // ── Quick-access section builder ──────────────────────────────────

        private record HomeCardDef(string Title, string Icon, string Desc, string Tag);
        private static HomeCardDef CardDef(string title, string icon, string desc, string tag)
            => new(title, icon, desc, tag);

        // Maps each Home card (by its nav Tag) to the same PNG icon the sidebar uses.
        // MIGRATION: the WPF build resolved these through the NavIcons.xaml
        // ResourceDictionary (NavIco_* keys → BitmapImage). Under WinUI the PNGs ship
        // as ms-appx Content, so the map now holds file names and the image is built
        // directly from an ms-appx:/// URI — same artwork, no resource dictionary.
        private static readonly Dictionary<string, string> _cardNavIcon = new()
        {
            ["Bloatware"]     = "bloatware",
            ["AppInstaller"]  = "appinstaller",
            ["Debloat"]       = "debloat",
            ["AkariOS"]       = "akarios",
            ["Gaming"]        = "gaming",
            ["Privacy"]       = "privacy",
            ["Update"]        = "update",
            ["Notifications"] = "notifications",
            ["Power"]         = "power",
            ["Taskbar"]       = "customize",
            ["Tools"]         = "tools",
            ["Advanced"]      = "advanced",
            ["Backup"]        = "backup",
            ["Verify"]        = "verify",
        };

        private static Microsoft.UI.Xaml.Media.Imaging.BitmapImage NavIconSource(string fileName) =>
            new(new Uri($"ms-appx:///Resource/NavIcons/{fileName}.png"));

        private void AddCardSection(StackPanel host, string label, bool isFirst, HomeCardDef[] defs)
        {
            host.Children.Add(new TextBlock
            {
                Text       = label,
                FontFamily = (FontFamily)(Application.Current.Resources["MonoFont"] ?? new FontFamily("Consolas")),
                FontSize   = 10,
                FontWeight = FontWeights.Medium,
                Foreground = TweakHelpers.TextMuted,
                Margin     = new Thickness(2, isFirst ? 0 : 20, 0, 10)
            });

            const int columns = 3;
            const double gap  = 14;

            var grid = new Grid();
            for (int c = 0; c < columns; c++)
            {
                if (c > 0) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            int rowsNeeded = (defs.Length + columns - 1) / columns;
            for (int r = 0; r < rowsNeeded; r++)
            {
                if (r > 0) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(gap) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (int i = 0; i < defs.Length; i++)
            {
                var d    = defs[i];
                var card = BuildCard(d.Title, "#e0142a", d.Icon, d.Desc, d.Tag);
                card.VerticalAlignment = VerticalAlignment.Stretch;
                Grid.SetColumn(card, (i % columns) * 2);
                Grid.SetRow(card,    (i / columns) * 2);
                grid.Children.Add(card);
            }

            host.Children.Add(grid);
        }

        // ── Feature cards ─────────────────────────────────────────────────

        private Border BuildCard(string title, string accentColor, string iconGlyph,
                                 string description, string tabTarget)
        {
            // Compact horizontal nav card: [38px icon tile] [title + desc] [› chevron]
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBox = new Border
            {
                Width             = 38,
                Height            = 38,
                CornerRadius      = TweakHelpers.CardRadius,
                BorderThickness   = new Thickness(1),
                BorderBrush       = TweakHelpers.Hairline,
                Background        = TweakHelpers.Token("AkariOverlaySoft"),
                Margin            = new Thickness(0, 0, 13, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            // Prefer the sidebar's PNG nav icon; fall back to the Segoe glyph if unmapped.
            TextBlock? iconGlyphBlock = null;
            if (_cardNavIcon.TryGetValue(tabTarget, out var icoKey))
            {
                if (tabTarget == "AkariOS" && ThemeService.Current == AkariTheme.Light)
                    icoKey = "akarios_light";

                // RenderOptions.SetBitmapScalingMode dropped — no WinUI equivalent
                // (WinUI handles DPI/bitmap scaling itself).
                var iconImg = new Image
                {
                    Source              = NavIconSource(icoKey),
                    Width               = 20,
                    Height              = 20,
                    Stretch             = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                iconBox.Child = iconImg;
            }
            else
            {
                iconGlyphBlock = new TextBlock
                {
                    Text                = iconGlyph,
                    FontFamily          = new FontFamily("Segoe MDL2 Assets"),
                    FontSize            = 16,
                    Foreground          = TweakHelpers.IconNeutral,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                iconBox.Child = iconGlyphBlock;
            }

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            text.Children.Add(new TextBlock
            {
                Text       = title,
                FontFamily = DisplayFont,
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary
            });
            text.Children.Add(new TextBlock
            {
                Text         = description,
                FontSize     = 12,
                Foreground   = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 2, 0, 0),
                LineHeight   = 16
            });
            Grid.SetColumn(text, 1);

            var chevron = new TextBlock
            {
                Text              = "", // Segoe MDL2 chevron right
                FontFamily        = new FontFamily("Segoe MDL2 Assets"),
                FontSize          = 13,
                Foreground        = TweakHelpers.IconNeutral,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(chevron, 2);

            row.Children.Add(iconBox);
            row.Children.Add(text);
            row.Children.Add(chevron);

            var card = new Border
            {
                Background      = TweakHelpers.CardBackground(),
                BorderBrush     = TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius    = TweakHelpers.CardRadius,
                Padding         = new Thickness(14, 12, 16, 12),
                Child           = row,
            };

            // V3 hover: neutral surface lift + hairline brighten + crimson icon accent.
            // (Cursors.Hand dropped — no WinUI equivalent.)
            card.PointerEntered += (_, _) =>
            {
                card.Background   = TweakHelpers.CardBgHover;
                card.BorderBrush  = TweakHelpers.HairlineHover;
                if (iconGlyphBlock != null) iconGlyphBlock.Foreground = TweakHelpers.Accent;
            };
            card.PointerExited += (_, _) =>
            {
                card.Background   = TweakHelpers.CardBackground();
                card.BorderBrush  = TweakHelpers.Hairline;
                if (iconGlyphBlock != null) iconGlyphBlock.Foreground = TweakHelpers.IconNeutral;
            };
            card.Tapped += (_, _) => _navigate?.Invoke(tabTarget);

            return TweakHelpers.ShadowWrapCard(card);
        }
    }
}