using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Gaming;
using AkariTool.Tabs.AkariOS;
using AkariTool.Tabs.Privacy;
using AkariTool.Tabs.Update;
using AkariTool.Tabs.Notifications;
using AkariTool.Tabs.Power;
using AkariTool.Tabs.AdvancedTools;
using AkariTool.Tabs.About;
using AkariTool.Tabs.AppUpdate;
using NavItem = Wpf.Ui.Controls.NavigationViewItem;

namespace AkariTool
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly ToolService _service;

        private Dictionary<string, FrameworkElement> _panels = null!;
        private Dictionary<string, NavItem> _navByTag = null!;
        private HashSet<string> _topTags = null!;
        private Dictionary<string, (string parent, string panel)> _subInfo = null!;
        private Dictionary<string, string> _defaultSub = null!;
        private NavItem? _activeNavItem;
        private NavItem? _activeParentItem;

        // Collapsible section groups (SOFTWARE/OPTIMIZE/ADVANCED): tag → owning group item.
        private Dictionary<string, NavItem> _groupByTag = null!;
        private NavItem[] _groupItems = null!;

        public MainWindow()
        {
            InitializeComponent();

            _service = new ToolService(TxtLog, LogProgress, TxtProgressStatus);

            // Each tab calls Build() from its own Initialize override, so this is the single
            // place where the registry rows a tab produces can be bracketed and attributed.
            void Init(BaseTab tab)
            {
                int start = TweakRegistry.Mark();
                tab.Initialize(_service);
                if (!string.IsNullOrEmpty(tab.NavTag))
                    TweakRegistry.ClaimRange(tab.NavTag, tab.NavLabel, start);
            }

            Init(TabHome);
            TabHome.SetNavigationCallback(SelectNavItem);
            Init(TabSoftware);
            Init(TabGaming);
            Init(TabAkariOS);
            Init(TabPrivacy);
            Init(TabUpdate);
            Init(TabNotifications);
            Init(TabPower);
            Init(TabCustomize);
            Init(TabTools);
            Init(TabAdvanced);
            Init(TabBackup);
            Init(TabVerify);
            Init(TabAbout);
            Init(TabAppUpdate);
            TabAdvanced.SetSelectedAppsProvider(() => TabSoftware.GetSelectedWindowsApps());

            var searchSources = new List<(string Label, StackPanel Root, Action Navigate)>
            {
                ("AkariOS",       TabAkariOS.RootPanel,       () => SelectNavItem("AkariOS")),
                ("Gaming",        TabGaming.RootPanel,        () => SelectNavItem("Gaming")),
                ("Privacy",       TabPrivacy.RootPanel,       () => SelectNavItem("Privacy")),
                ("Update",        TabUpdate.RootPanel,        () => SelectNavItem("Update")),
                ("Notifications", TabNotifications.RootPanel, () => SelectNavItem("Notifications")),
                ("Power",         TabPower.RootPanel,         () => SelectNavItem("Power")),
            };
            foreach (var (label, subPanel) in TabCustomize.SubPanels)
            {
                var capturedLabel = label;
                searchSources.Add(($"Customize › {capturedLabel}", subPanel,
                    () => SelectNavItem(capturedLabel.Replace(" ", ""))));
            }
            TabHome.SetupGlobalSearch(searchSources);

            _panels = new Dictionary<string, FrameworkElement>
            {
                ["Home"]          = TabHome,
                ["Software"]      = TabSoftware,
                ["AkariOS"]       = TabAkariOS,
                ["Gaming"]        = TabGaming,
                ["Privacy"]       = TabPrivacy,
                ["Update"]        = TabUpdate,
                ["Notifications"] = TabNotifications,
                ["Power"]         = TabPower,
                ["Customize"]     = TabCustomize,
                ["Tools"]         = TabTools,
                ["Advanced"]      = TabAdvanced,
                ["Backup"]        = TabBackup,
                ["Verify"]        = TabVerify,
                ["About"]         = TabAbout,
                ["AppUpdate"]     = TabAppUpdate,
            };

            _navByTag = new Dictionary<string, NavItem>
            {
                ["Home"]          = NavHome,
                ["AkariOS"]       = NavAkariOS,
                ["Gaming"]        = NavGaming,
                ["Privacy"]       = NavPrivacy,
                ["Update"]        = NavUpdate,
                ["Notifications"] = NavNotifications,
                ["Power"]         = NavPower,
                ["Customize"]     = NavCustomize,
                ["Tools"]         = NavTools,
                ["Advanced"]      = NavAdvanced,
                ["Backup"]        = NavBackup,
                ["Verify"]        = NavVerify,
                ["AppInstaller"]  = NavAppInstaller,
                ["Debloat"]       = NavDebloat,
                ["Bloatware"]     = NavBloatware,
                ["Taskbar"]       = NavTaskbar,
                ["Explorer"]      = NavExplorer,
                ["ContextMenu"]   = NavContextMenu,
                ["Appearance"]    = NavAppearance,
                ["StartMenu"]     = NavStartMenu,
                ["Desktop"]       = NavDesktop,
                ["About"]         = NavAbout,
                ["AppUpdate"]     = NavAppUpdate,
            };

            _topTags = new HashSet<string>
            {
                "Home", "AkariOS", "Gaming", "Privacy",
                "Update", "Notifications", "Power", "Customize", "Tools", "Advanced", "Backup", "Verify",
                "About", "AppUpdate",
            };

            // Which collapsible group item owns each navigable tag
            // (for auto-expand on navigation + accent-pink header tint).
            _groupItems = new[] { SoftwareGroup, OptimizeGroup, AdvancedGroup };
            _groupByTag = new Dictionary<string, NavItem>
            {
                ["Bloatware"] = SoftwareGroup, ["AppInstaller"] = SoftwareGroup, ["Debloat"] = SoftwareGroup,
                ["AkariOS"] = OptimizeGroup, ["Gaming"] = OptimizeGroup, ["Privacy"] = OptimizeGroup,
                ["Update"] = OptimizeGroup, ["Notifications"] = OptimizeGroup, ["Power"] = OptimizeGroup,
                ["Customize"] = OptimizeGroup, ["Tools"] = OptimizeGroup,
                ["Taskbar"] = OptimizeGroup, ["Explorer"] = OptimizeGroup, ["ContextMenu"] = OptimizeGroup,
                ["Appearance"] = OptimizeGroup, ["StartMenu"] = OptimizeGroup, ["Desktop"] = OptimizeGroup,
                ["Advanced"] = AdvancedGroup, ["Backup"] = AdvancedGroup, ["Verify"] = AdvancedGroup,
            };

            _subInfo = new Dictionary<string, (string, string)>
            {
                ["AppInstaller"] = ("Software",  "AppInstaller"),
                ["Debloat"]      = ("Software",  "Debloat"),
                ["Bloatware"]    = ("Software",  "Bloatware"),
                ["Taskbar"]      = ("Customize", "Taskbar"),
                ["Explorer"]     = ("Customize", "Explorer"),
                ["ContextMenu"]  = ("Customize", "ContextMenu"),
                ["Appearance"]   = ("Customize", "Appearance"),
                ["StartMenu"]    = ("Customize", "StartMenu"),
                ["Desktop"]      = ("Customize", "Desktop"),
            };

            _defaultSub = new Dictionary<string, string>
            {
                ["Customize"] = "Taskbar",
            };

            // Start on Home
            SetActiveNav(NavHome);
            ShowContent("Home");
            UpdateThemeToggleLabel();

            // Logo surfaces (title bar + Home watermark + window icon) follow the theme.
            ApplyThemedLogos();
            ThemeService.ThemeChanged += OnThemeChanged;
            Closed += (_, _) => ThemeService.ThemeChanged -= OnThemeChanged;

            // Log panel: auto-opens on first output, but an explicit user close wins —
            // later output then only lights the status-bar dot.
            TxtLog.TextChanged += (_, _) => OnLogOutput();
            RestoreLogPanelState();

            // Startup update check — silent unless a newer release exists.
            // Drift scan runs after it so the two never compete for the foreground.
            Loaded += async (_, _) =>
            {
                await CheckForUpdateOnStartupAsync();
                await CheckOrphanedCompetitiveSessionAsync();
                RunStartupDriftScan();
            };

            // A Competitive Mode session outlives the window it was started from —
            // closing without restoring would strand suspended apps and stopped
            // services with only the on-disk record to recover from.
            Closing += OnMainWindowClosing;
        }

        // ── Competitive Mode: orphaned session recovery ───────────────────────

        private bool _competitiveRecoveryChecked;

        /// <summary>
        /// Offers to undo a session that was never closed properly (crash, power
        /// loss, task-manager kill). Public because the --competitive startup path
        /// never shows the window, so it cannot rely on Loaded to run this.
        /// </summary>
        public async Task CheckOrphanedCompetitiveSessionAsync()
        {
            if (_competitiveRecoveryChecked) return;   // Loaded can re-fire
            _competitiveRecoveryChecked = true;

            CompetitiveSessionState state;
            try { if (!CompetitiveSessionStore.TryLoad(out state)) return; }
            catch { return; }

            // Owner must be set, and WPF refuses an owner that has never been shown.
            // In the shortcut path the window is still hidden at this point, so it is
            // shown just long enough to host the prompt.
            bool shownForPrompt = false;
            if (!IsVisible)
            {
                try { Show(); shownForPrompt = true; }
                catch { }
            }

            try
            {
                var box = new Wpf.Ui.Controls.MessageBox
                {
                    Owner = this,
                    Title = "Competitive Mode",
                    Content = new TextBlock
                    {
                        Text = $"A Competitive Mode session from {state.StartedUtc.ToLocalTime():g} was not " +
                               "closed properly. Some background apps may still be suspended and some " +
                               "services stopped. Restore normal settings now?",
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 440,
                    },
                    PrimaryButtonText = "Restore",
                    CloseButtonText = "Ignore",
                };

                if (await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    await CompetitiveService.EndAsync(state, null);
                    _service.Log("Competitive Mode: orphaned session restored.");
                }
                else
                {
                    // Clear either way — otherwise the prompt reappears on every
                    // launch for a session the user has chosen not to undo.
                    CompetitiveSessionStore.Clear();
                    _service.Log("Competitive Mode: orphaned session record discarded.");
                }
            }
            catch { /* recovery must never block startup */ }
            finally
            {
                if (shownForPrompt) try { Hide(); } catch { }
            }
        }

        /// <summary>Starts a session from the --competitive command line.</summary>
        public Task StartCompetitiveFromCommandLineAsync(string exePath) =>
            TabAkariOS.StartCompetitiveFromCommandLineAsync(exePath);

        // ── Competitive Mode: close interception ──────────────────────────────

        private bool _competitiveShutdownConfirmed;

        private async void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_competitiveShutdownConfirmed) return;      // second pass — let it close
            if (!CompetitiveService.IsSessionActive) return;

            // Cancel first: the confirmation is async, and the close cannot be held
            // open across an await. If the user confirms, we re-issue Close().
            e.Cancel = true;

            try
            {
                var box = new Wpf.Ui.Controls.MessageBox
                {
                    Owner = this,
                    Title = "Competitive Mode active",
                    Content = new TextBlock
                    {
                        Text = "A Competitive Mode session is active. End it and restore your settings " +
                               "before closing?",
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 440,
                    },
                    PrimaryButtonText = "End and close",
                    CloseButtonText = "Cancel",
                };

                if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
                    return;   // user cancelled — the close stays aborted

                await TabAkariOS.EndCompetitiveSessionForShutdownAsync();
            }
            catch (Exception ex)
            {
                _service.Log($"ERROR ending Competitive Mode on close: {ex.Message}");
            }

            _competitiveShutdownConfirmed = true;
            Close();
        }

        // ── Log panel open/close ──────────────────────────────────────────────

        private const string UiPrefKeyPath  = @"HKEY_CURRENT_USER\Software\AkariTool";
        private const string LogOpenPrefName = "LogPanelOpen";

        /// <summary>
        /// True once the user has explicitly closed the panel. While set, new output
        /// must not force the panel back open — it only raises the unread dot.
        /// </summary>
        private bool _logUserClosed;

        private bool LogPanelIsOpen => LogPanel.Visibility == Visibility.Visible;

        private void RestoreLogPanelState()
        {
            bool open;
            try
            {
                open = Microsoft.Win32.Registry.GetValue(UiPrefKeyPath, LogOpenPrefName, null)
                       is int i && i != 0;
            }
            catch { open = false; }

            // A restored-closed panel counts as an explicit close, so output during
            // this session does not override the choice the user made last session.
            _logUserClosed = !open;

            // Nothing has been logged yet, so an "open" preference still shows an
            // empty panel — that is intended: the user asked for it to be open.
            SetLogPanelOpen(open, persist: false, userInitiated: false);
        }

        private void SetLogPanelOpen(bool open, bool persist, bool userInitiated)
        {
            // Collapsed, not a zero-height strip: the row is Height="Auto", so a
            // collapsed child takes no vertical space at all.
            LogPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            LogToggleChevron.Text = open ? "▾" : "▴";

            if (open)
            {
                ClearLogUnread();
                // Land on the newest line rather than wherever the caret was.
                try { TxtLog.ScrollToEnd(); } catch { }
            }

            if (userInitiated) _logUserClosed = !open;

            if (persist)
            {
                try
                {
                    Microsoft.Win32.Registry.SetValue(UiPrefKeyPath, LogOpenPrefName,
                        open ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
                }
                catch { /* a lost UI preference must never break the window */ }
            }
        }

        private void OnLogOutput()
        {
            if (TxtLog.Text.Length == 0)
            {
                // Cleared log — drop back to closed and reset the indicator.
                LogPanel.Visibility = Visibility.Collapsed;
                LogToggleChevron.Text = "▴";
                ClearLogUnread();
                return;
            }

            if (_logUserClosed)
            {
                ShowLogUnread();   // respect the close; just signal there is output
                return;
            }

            if (!LogPanelIsOpen) SetLogPanelOpen(true, persist: false, userInitiated: false);
            else                 try { TxtLog.ScrollToEnd(); } catch { }
        }

        private void ShowLogUnread()
        {
            LogUnreadDot.Visibility = Visibility.Visible;
            LogToggleLabel.SetResourceReference(TextBlock.ForegroundProperty, "AkariAccentText");
        }

        private void ClearLogUnread()
        {
            LogUnreadDot.Visibility = Visibility.Collapsed;
            LogToggleLabel.SetResourceReference(TextBlock.ForegroundProperty, "AkariNavText");
        }

        private void LogToggle_Click(object sender, RoutedEventArgs e) =>
            SetLogPanelOpen(!LogPanelIsOpen, persist: true, userInitiated: true);

        private bool _startupUpdateChecked;

        private async Task CheckForUpdateOnStartupAsync()
        {
            if (_startupUpdateChecked) return;   // Loaded can re-fire
            _startupUpdateChecked = true;

            UpdateCheckResult result;
            try { result = await UpdateService.CheckAsync(); }
            catch { return; }   // never let a network hiccup surface at startup

            if (result.Status != UpdateStatus.UpdateAvailable) return;

            var box = new Wpf.Ui.Controls.MessageBox
            {
                Owner = this,
                Title = "Update available",
                Content = new TextBlock
                {
                    Text = $"Akari Tool {result.LatestTag} is available " +
                           $"(you have {UpdateService.CurrentVersionDisplay}).\n\n" +
                           "Update now?",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                PrimaryButtonText = "Update now",
                CloseButtonText = "Later",
            };

            var choice = await box.ShowDialogAsync();
            if (choice == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                ShowContent("AppUpdate");
                SetActiveNav(NavAppUpdate);
            }
        }

        private bool _startupDriftScanned;

        private void RunStartupDriftScan()
        {
            if (_startupDriftScanned) return;   // Loaded can re-fire
            _startupDriftScanned = true;

            try
            {
                var result = DriftScanner.Scan();
                if (result.HasDrift) ShowDriftBanner(result);
            }
            catch { /* never let a scan failure surface at startup */ }
        }

        /// <summary>Shows the drift banner in grid row 1. Safe to call repeatedly.</summary>
        public void ShowDriftBanner(DriftScanResult result)
        {
            DriftBannerHost.Content = DriftBanner.Build(
                result,
                onReview:  () => { ShowContent("Verify"); SetActiveNav(NavVerify); },
                onDismiss: HideDriftBanner);
            DriftBannerHost.Visibility = Visibility.Visible;
        }

        /// <summary>Shows or hides the banner to match a fresh scan. Called by the Verify tab.</summary>
        public void RefreshDriftBanner(DriftScanResult result)
        {
            if (result.HasDrift) ShowDriftBanner(result);
            else HideDriftBanner();
        }

        /// <summary>Hides the banner for the rest of this session.</summary>
        public void HideDriftBanner()
        {
            DriftBannerHost.Content = null;
            DriftBannerHost.Visibility = Visibility.Collapsed;
        }

        // ── Window chrome is now provided by ui:TitleBar (min/max/close). ──

        // ── Pane collapse (expanded 234px / compact 49px) ──
        public static readonly DependencyProperty IsPaneCompactProperty =
            DependencyProperty.Register(nameof(IsPaneCompact), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(false, OnIsPaneCompactChanged));

        public bool IsPaneCompact
        {
            get => (bool)GetValue(IsPaneCompactProperty);
            set => SetValue(IsPaneCompactProperty, value);
        }

        private static void OnIsPaneCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var w = (MainWindow)d;
            bool compact = (bool)e.NewValue;
            w.NavCol.Width = new GridLength(compact ? 49 : 234);

            // Broadcast to the whole tree (inherited attached property): the rail
            // templates/styles trigger on Nav.IsCompact to hide labels, centre icons,
            // and swap group headers for dividers.
            Nav.SetIsCompact(w, compact);

            // The full search box cannot render at rail width — swap it for an icon.
            // Null-checked: this callback can fire before the template is applied.
            if (w.GlobalSearch is not null && w.GlobalSearchIcon is not null)
            {
                w.GlobalSearch.Visibility     = compact ? Visibility.Collapsed : Visibility.Visible;
                w.GlobalSearchIcon.Visibility = compact ? Visibility.Visible   : Visibility.Collapsed;
                if (compact && w.GlobalSearchResults is not null)
                    w.GlobalSearchResults.IsOpen = false;   // popup must not survive the collapse
            }

            // 24px of horizontal inset overflows the 49px rail — drop it while compact and
            // let the button's own IsCompact trigger centre it.
            if (w.GlobalSearchHost is not null)
                w.GlobalSearchHost.Margin = compact
                    ? new Thickness(0, 8, 0, 10)
                    : new Thickness(12, 8, 12, 10);

            w.UpdateCustomizeExpansion();
        }

        /// <summary>Compact-rail search icon: expands the pane, then focuses the box.</summary>
        private void GlobalSearchIcon_Click(object sender, RoutedEventArgs e)
        {
            IsPaneCompact = false;
            // Loaded priority: focusing before the expand layout completes does not stick.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                GlobalSearch.Focus();
                GlobalSearch.CaretIndex = GlobalSearch.Text?.Length ?? 0;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void HamburgerBtn_Click(object sender, RoutedEventArgs e) => IsPaneCompact = !IsPaneCompact;

        // ── Theme switch (title-bar icon button) ──
        // Center-to-center distance between the two pill columns (38px pill / 2 columns).
        private const double ThemeThumbTravel = 19;

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            // Capture the thumb's resting position BEFORE toggling: ThemeService.Toggle()
            // fires ThemeChanged synchronously, and OnThemeChanged snaps the thumb to the
            // new side. We then override that snap with a slide from the old position.
            double fromX = ThemeThumbShift.X;
            ThemeService.Toggle();

            double toX = ThemeService.Current == AkariTheme.Dark ? 0 : ThemeThumbTravel;
            if (fromX != toX)
            {
                ThemeThumbShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                    new DoubleAnimation
                    {
                        From = fromX,
                        To = toX,
                        Duration = System.TimeSpan.FromMilliseconds(180),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                    });
            }
        }

        // Both icons stay visible; the thumb marks the ACTIVE theme.
        // Snaps the thumb into place (startup / external change); clicks animate separately.
        private void UpdateThemeToggleLabel()
        {
            bool dark = ThemeService.Current == AkariTheme.Dark;

            // Release any running click animation before setting the resting value.
            ThemeThumbShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            ThemeThumbShift.X = dark ? 0 : ThemeThumbTravel;

            // The glyph sitting on the thumb must contrast against it (thumb is AkariTextPrimary).
            var active   = (System.Windows.Media.Brush)FindResource("AkariSidebarBackground");
            var inactive = (System.Windows.Media.Brush)FindResource("AkariTextSecondary");

            ThemeMoonPath.Fill    = dark ? active : inactive;
            ThemeSunCorePath.Fill = dark ? inactive : active;
            ThemeSunRaysPath.Fill = dark ? inactive : active;

            ThemeToggleBtn.ToolTip = dark ? "Switch to light theme" : "Switch to dark theme";
        }

        private void OnThemeChanged(AkariTheme _)
        {
            ApplyThemedLogos();
            UpdateThemeToggleLabel();
        }

        // Point every logo surface at the single source of truth (ThemeService.Logo) and
        // fade the watermark more in Light (the black/red art is heavier on white).
        private void ApplyThemedLogos()
        {
            var logo = ThemeService.Logo;
            TitleBarLogo.Source = logo;
            Icon = logo;
            HomeWatermark.Source = logo;
            HomeWatermark.Opacity = ThemeService.Current == AkariTheme.Light ? 0.05 : 0.10;
            AkariOsNavIcon.Source = (System.Windows.Media.ImageSource)FindResource(
                ThemeService.Current == AkariTheme.Light ? "NavIco_AkariOS_Light" : "NavIco_AkariOS");
            AppUpdateNavIcon.Source = (System.Windows.Media.ImageSource)FindResource(
                ThemeService.Current == AkariTheme.Light ? "NavIco_AppUpdate_Light" : "NavIco_AppUpdate");
        }

        // ── Navigation ──
        // NavigationViewItems are ButtonBase, so leaf items route through Click just
        // like the old Buttons. Group parents have no Tag: NavigationView's built-in
        // click behaviour toggles their IsExpanded and nothing else happens.
        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not NavItem item || item.Tag is not string tag) return;
            // Stop the Click from bubbling to NavCustomize, whose handler would
            // re-route the navigation to its default child (Taskbar).
            e.Handled = true;
            HandleNav(tag, item);
        }

        private void HandleNav(string tag, NavItem item)
        {
            // Navigating into a collapsed group auto-expands it first.
            if (_groupByTag.TryGetValue(tag, out var group))
                group.IsExpanded = true;

            // Customize navigates to its default child (which lights up the section
            // and, via UpdateCustomizeExpansion, expands it inline).
            if (tag == "Customize"
                && _defaultSub.TryGetValue(tag, out var defChild)
                && _navByTag.TryGetValue(defChild, out var childItem))
            {
                HandleNav(defChild, childItem);
                return;
            }

            if (_subInfo.TryGetValue(tag, out var sub))
            {
                ShowContent(sub.parent);
                CallShowPanel(sub.parent, sub.panel);
                SetActiveNav(item);
                return;
            }

            if (_topTags.Contains(tag))
            {
                ShowContent(tag);
                SetActiveNav(item);
            }
        }

        private void SetActiveNav(NavItem item)
        {
            if (_activeNavItem != null) _activeNavItem.IsActive = false;
            if (_activeParentItem != null) { _activeParentItem.IsActive = false; _activeParentItem = null; }

            _activeNavItem = item;
            item.IsActive = true;

            // A selected Customize child also lights the Customize row.
            if (item.Tag is string tag && _subInfo.TryGetValue(tag, out var sub)
                && sub.parent == "Customize"
                && _navByTag.TryGetValue(sub.parent, out var parentItem))
            {
                _activeParentItem = parentItem;
                parentItem.IsActive = true;
            }

            UpdateGroupHeaders();
            UpdateCustomizeExpansion();
        }

        // Tints the header of the group that owns the active page accent-pink.
        private void UpdateGroupHeaders()
        {
            NavItem? activeGroup = null;
            if (_activeNavItem?.Tag is string tag) _groupByTag.TryGetValue(tag, out activeGroup);
            foreach (var g in _groupItems)
                g.IsActive = ReferenceEquals(g, activeGroup);
        }

        // Customize's six children show inline only while Customize (or one of them)
        // is active. Posted via Dispatcher so it wins over NavigationView's built-in
        // click-toggles-IsExpanded behaviour for items with children.
        private void UpdateCustomizeExpansion()
        {
            bool active = _activeNavItem?.Tag is string t
                && (t == "Customize" || (_subInfo.TryGetValue(t, out var s) && s.parent == "Customize"));

            Dispatcher.BeginInvoke(new Action(() => NavCustomize.IsExpanded = active),
                System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ShowContent(string name)
        {
            foreach (var panel in _panels.Values)
                panel.Visibility = Visibility.Collapsed;
            _panels[name].Visibility = Visibility.Visible;
        }

        private void CallShowPanel(string parent, string panel)
        {
            switch (parent)
            {
                case "Software":  TabSoftware.ShowPanel(panel);  break;
                case "Customize": TabCustomize.ShowPanel(panel); break;
            }
        }

        // ── Global "find a setting" search ────────────────────────────────

        private void GlobalSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = GlobalSearch.Text.Trim();
            if (q.Length < 2) { GlobalSearchResults.IsOpen = false; return; }

            var hits = TweakRegistry.Search(q);
            GlobalSearchList.ItemsSource = hits;
            GlobalSearchResults.IsOpen = hits.Count > 0;
        }

        private void GlobalSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            GlobalSearchResults.IsOpen = false;
            GlobalSearch.Text = "";
            e.Handled = true;
        }

        private void GlobalSearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GlobalSearchList.SelectedItem is not TweakRegistry.SearchHit hit) return;
            GlobalSearchResults.IsOpen = false;
            GlobalSearchList.SelectedItem = null;

            if (_navByTag.TryGetValue(hit.TabTag, out var navItem))
                HandleNav(hit.TabTag, navItem);

            // Loaded priority so the tab is visible before its own search box runs.
            if (_panels.TryGetValue(hit.TabTag, out var tab) && tab is BaseTab bt)
                Dispatcher.BeginInvoke(new Action(() => bt.ApplySearch(hit.Name)),
                    System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void SelectNavItem(string tag)
        {
            if (_navByTag.TryGetValue(tag, out var btn))
                HandleNav(tag, btn);
        }
    }

    /// <summary>
    /// Attached state kept for the TabBtn template (still used by the hamburger button);
    /// the rail items themselves now use NavigationViewItem's native IsActive/IsExpanded.
    /// </summary>
    public static class Nav
    {
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.RegisterAttached("IsActive", typeof(bool), typeof(Nav),
                new PropertyMetadata(false));

        public static void SetIsActive(DependencyObject o, bool value) => o.SetValue(IsActiveProperty, value);
        public static bool GetIsActive(DependencyObject o) => (bool)o.GetValue(IsActiveProperty);

        /// <summary>
        /// Compact-rail flag. Set once on the window and inherited by every descendant
        /// (like FontSize), so rail templates/styles can trigger on it via
        /// RelativeSource Self — robust inside NavigationView's item hosting, where
        /// FindAncestor bindings proved unreliable.
        /// </summary>
        public static readonly DependencyProperty IsCompactProperty =
            DependencyProperty.RegisterAttached("IsCompact", typeof(bool), typeof(Nav),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static void SetIsCompact(DependencyObject o, bool value) => o.SetValue(IsCompactProperty, value);
        public static bool GetIsCompact(DependencyObject o) => (bool)o.GetValue(IsCompactProperty);
    }
}