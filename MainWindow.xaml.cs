using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Text;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.UI;
using AkariTool.Helpers;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.About;
using AkariTool.Tabs.Notifications;
using AkariTool.Tabs.Sound;
using AkariTool.Tabs.Update;
using AkariTool.Tabs.Privacy;
using AkariTool.Tabs.Gaming;
using AkariTool.Tabs.Power;
using AkariTool.Tabs.AppUpdate;
using AkariTool.Tabs.AkariOS;
using AkariTool.Tabs.AdvancedTools;
using AkariTool.Tabs.Backup;
using AkariTool.Tabs.Verify;

namespace AkariTool;

/// <summary>
/// WinUI 3 shell. Native NavigationView rail (SOFTWARE / OPTIMIZE / ADVANCED +
/// About footer), custom AppWindow title bar, status + log panel, theme toggle.
/// Content uses the Visibility-toggled UserControl-stack pattern from the WPF
/// shell; migrated tabs register in _tabs, everything else shows a placeholder.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly ToolService _service;
    private readonly Dictionary<string, BaseTab> _tabs = new();
    private TextBlock? _placeholder;
    private SoftwareTab? _software;
    private CustomizeTab? _customize;
    private AkariOSTab? _akariOS;

    // ── Nav routing contract ──────────────────────────────────────────────
    // Ported VERBATIM from the WPF shell's _subInfo. A rail tag listed here is a
    // SUB-PANEL of a parent tab: select the parent, then call its ShowPanel(panel).
    // Getting this wrong does not throw — it silently renders the placeholder (or
    // one default sub-panel), which is exactly the bug this map exists to prevent.
    private static readonly Dictionary<string, (string Parent, string Panel)> _subInfo = new()
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

    /// <summary>Parent tab tag → the sub-panel shown when the parent itself is selected.</summary>
    private static readonly Dictionary<string, string> _defaultSub = new()
    {
        ["Customize"] = "Taskbar",
    };

    /// <summary>
    /// Rail tags that intentionally have no tab yet (not migrated). The startup
    /// nav-contract assertion allows these and nothing else — shrink this list as
    /// batches land.
    /// </summary>
    private static readonly HashSet<string> _notYetMigrated = new();   // all tabs migrated

    /// <summary>
    /// HWND of the main window. Unpackaged WinUI file/folder pickers must be
    /// associated with a window handle before they can be shown, so
    /// <see cref="Tabs.FilePickers"/> reads it from here.
    /// </summary>
    public static nint WindowHandle { get; private set; }

    /// <summary>
    /// The single shell window. WinUI has no <c>Window.GetWindow(element)</c>, so
    /// code that needs to show/hide/restore the app window (e.g. Competitive Mode)
    /// reaches it through here.
    /// </summary>
    public static MainWindow? Instance { get; private set; }

    // ── Force rounded window corners (DWM) ──────────────────────────────────
    // Opts this window into rounded corners even when the OS-wide "rounded corners"
    // setting is off, so the footer bar's bottom corners follow the shell radius.
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    public MainWindow()
    {
        this.InitializeComponent();

        Instance = this;
        WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Force rounded window corners for THIS window regardless of the OS-wide setting.
        // On installs where rounded corners are disabled globally (common on debloated
        // Windows), the window — and therefore the footer bar's two bottom corners — draws
        // square. DWMWCP_ROUND opts this window into the standard ~8px shell radius so the
        // footer's bottom-left/right corners curve. Does not touch Mica, colours, or layout.
        int cornerPref = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(WindowHandle, DWMWA_WINDOW_CORNER_PREFERENCE,
                                  ref cornerPref, sizeof(int));

        // Custom title bar via the AppWindow title-bar extension.
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);

        // Theme: attach the root, then apply the persisted (or Windows-following) theme
        // BEFORE building content so tokens resolve correctly on first paint.
        ThemeService.AttachRoot(RootGrid);
        ThemeService.Apply(ThemeService.LoadPersisted());

        // Shared log/exec service bound to the status + log controls.
        _service = new ToolService(TxtLog, LogProgress, TxtProgressStatus);

        // Mica system backdrop — the Winhance "rounded floating panel" look. Set after the
        // service so the outcome is logged.
        TrySetBackdrop();

        // ContentDialogs (tweak warnings, bulk confirms) need the XamlRoot.
        // The nav-contract assertion also runs here — it may show a dialog, so it
        // needs the XamlRoot to exist.
        // Build stamp in the status bar (WPF parity; version string is canonical).
        BuildStamp.Text = $"WinUI 3 · .NET 8 · {UpdateService.CurrentVersionDisplay}";

        RootGrid.Loaded += (_, _) =>
        {
            AkariDialogs.XamlRoot = RootGrid.XamlRoot;
            AssertNavContract();

            // ── Cosmetic-pass glows/shadows (WPF DropShadowEffect parity via Composition,
            //    since WinUI has no UIElement.Effect). Attached here so the targets are
            //    realized (ActualWidth > 0). ──
            // Crimson is sourced from the AkariAccentColor token (invariant #E0142A in both
            // themes → zero visual change).
            var crimson = ThemeService.Color("AkariAccentColor");
            // AkariSuccessColor is theme-VARIANT (#3DDC84 dark / #1E9E5A light); the glow is a
            // fixed-brand green, so it uses the dedicated invariant AkariSuccessGlowColor
            // (#3DDC84 in both themes → zero visual change).
            var green   = ThemeService.Color("AkariSuccessGlowColor");
            AkariShadow.Attach(TitleDotShadowHost, TitleDot, crimson, blurRadius: 8, opacity: 0.9f);
            AkariShadow.Attach(StatusDotShadowHost, StatusDot, green, blurRadius: 7, opacity: 0.7f);
            // (The log console is now docked inside the content card, so it no longer needs
            //  its own drop shadow — the card carries the elevation.)

            // Startup orchestration hooks (WPF ran these from MainWindow.Loaded too).
            _ = CheckOrphanedCompetitiveSessionAsync();
            _ = RunStartupUpdateCheckAsync();
            RunStartupDriftScan();
        };

        // Clip the inset content card to its rounded corners so ALL FOUR corners render
        // the radius (the opaque drift banner and the ScrollViewer's scrolling content
        // otherwise overdraw them square). The clip geometry's Size is bound to the card's
        // own visual Size via an ExpressionAnimation, so it always matches the card exactly
        // at any width — a one-shot ActualWidth/Height read goes stale after a resize and
        // leaves three corners square (only the origin-anchored top-left keeps its curve).
        ClipCardToRadius(ContentCard);

        BuildContent();
        UpdateThemeVisuals();

        // Select Home on launch (matches the WPF build's landing tab).
        SelectNavItem("Home");
    }

    private void BuildContent()
    {
        // Each tab calls Build() from its own Initialize override, so this is the
        // single place where the registry rows a tab produces are bracketed and
        // attributed (same Init pattern as the WPF shell).
        void Init(string tag, BaseTab tab)
        {
            // ── Content width normalisation (shell-level, all tabs at once) ──
            // The WPF tab XAML was just <StackPanel x:Name="RootPanel"/> with NO
            // width cap, so content filled the pane. The migrated XAML added
            // MaxWidth="920" (1000 on Software); a Stretch-aligned panel clamped by
            // MaxWidth is laid out LEFT, which produced a large right-hand gutter on
            // every tab. Clearing the cap here restores the WPF fill behaviour for
            // every tab from one place.
            //
            // Done BEFORE Initialize() on purpose: About (860), AppUpdate (720) and
            // Update (860) set their own MaxWidth + HorizontalAlignment.Center inside
            // Build() — that is original WPF behaviour, and running first means those
            // deliberate per-tab widths still win.
            if (tab.FindName("RootPanel") is FrameworkElement rootPanel)
            {
                rootPanel.MaxWidth = double.PositiveInfinity;
                rootPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            // A WinUI ScrollViewer's ScrollContentPresenter defaults to
            // HorizontalContentAlignment = Left, so it arranges its child at the
            // child's DESIRED width, pinned left. For the 13 stretch tabs that is
            // invisible (a Stretch child's desired width is the full viewport), but
            // for the three tabs that deliberately cap their column — About (860),
            // AppUpdate (720), Update (860) — it meant their RootPanel's own
            // HorizontalAlignment.Center had no wider area to centre within, so they
            // rendered left-anchored. Stretching the presenter makes it fill the
            // viewport, which lets the child's own alignment take effect.
            if (tab.Content is ScrollViewer tabScroller)
                tabScroller.HorizontalContentAlignment = HorizontalAlignment.Stretch;

            int start = TweakRegistry.Mark();
            tab.Initialize(_service);
            if (!string.IsNullOrEmpty(tab.NavTag))
                TweakRegistry.ClaimRange(tab.NavTag, tab.NavLabel, start);

            tab.Visibility = Visibility.Collapsed;
            _tabs[tag] = tab;
            ContentHost.Children.Add(tab);
        }

        var home = new HomeTab();
        Init("Home", home);

        // Parent tabs register under their PARENT key only ("Software", "Customize").
        // Their rail sub-panel tags (Bloatware / AppInstaller / Debloat, and the six
        // Customize panels) resolve through _subInfo → ShowPanel, mirroring WPF.
        _software = new SoftwareTab();
        Init("Software", _software);

        // NOTE: DebloatTab is NOT instantiated here. SoftwareTab already creates and
        // hosts one in its _panelDebloat (the WPF design); a second instance here was
        // a duplicate, and the "Debloat" rail tag is a Software sub-panel.
        Init("About", new AboutTab());
        Init("Notifications", new NotificationsTab());
        Init("Sound", new SoundTab());
        Init("Update", new UpdateTab());
        Init("Privacy", new PrivacyTab());
        Init("Gaming", new GamingTab());
        _customize = new CustomizeTab();
        Init("Customize", _customize);
        Init("Power", new PowerTab());
        _akariOS = new AkariOSTab();
        Init("AkariOS", _akariOS);
        Init("Advanced", new AdvancedToolsTab());
        Init("Tools", new ToolsTab());
        Init("Backup", new BackupTab());
        Init("Verify", new VerifyTab());
        Init("AppUpdate", new AppUpdateTab());

        // Advanced Tools' autounattend generator reads the Windows apps ticked in
        // the Software tab (identical wiring to the WPF shell).
        if (_tabs.TryGetValue("Advanced", out var advTab) && advTab is AdvancedToolsTab adv && _software is not null)
            adv.SetSelectedAppsProvider(() => _software.GetSelectedWindowsApps());

        // Home's cards navigate by nav tag.
        home.SetNavigationCallback(SelectNavItem);

        // Home's own cross-tab search sources. Each tab's root StackPanel is
        // resolved by name (FindName) so no tab XAML or BaseTab signature changes.
        // NOTE: this is HomeTab's search box only — the rail-pinned global
        // "Find a setting" box remains a separate deferred restore item.
        var sources = new List<(string Label, StackPanel Root, Action Navigate)>();
        foreach (var (tag, tab) in _tabs)
        {
            if (tag == "Home") continue;
            if (tab.FindName("RootPanel") is not StackPanel root) continue;
            var capturedTag = tag;
            sources.Add((string.IsNullOrEmpty(tab.NavLabel) ? tag : tab.NavLabel,
                         root,
                         () => SelectNavItem(capturedTag)));
        }
        home.SetupGlobalSearch(sources);

        _placeholder = new TextBlock
        {
            Text = "This tab is migrated in a later phase.",
            FontSize = 15,
            Foreground = TweakHelpers.TextMuted,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        ContentHost.Children.Add(_placeholder);
    }

    /// <summary>Every NavigationViewItem in the rail, including nested MenuItems children.</summary>
    private IEnumerable<NavigationViewItem> AllNavItems()
    {
        IEnumerable<NavigationViewItem> Walk(IList<object> items)
        {
            foreach (var o in items)
            {
                if (o is not NavigationViewItem nvi) continue;
                yield return nvi;
                foreach (var child in Walk(nvi.MenuItems)) yield return child;
            }
        }
        foreach (var i in Walk(Nav.MenuItems)) yield return i;
        foreach (var i in Walk(Nav.FooterMenuItems)) yield return i;
    }

    /// <summary>Selects a rail item by its Tag (used by Home's cards and search results).</summary>
    private void SelectNavItem(string tag)
    {
        foreach (var item in AllNavItems())
            if ((item.Tag as string) == tag) { Nav.SelectedItem = item; return; }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  NAV-CONTRACT ASSERTION (Debug only)
    //
    //  Nav wiring cannot throw when it is wrong — a bad tag silently renders the
    //  placeholder, and a missing sub-panel item silently hides content. That is
    //  how the Software tab became unreachable and 58 of Customize's 85 rows went
    //  missing while the build stayed clean and the app launched fine.
    //
    //  This is the nav equivalent of the stale-style sweep: it fails LOUDLY at
    //  launch so the class cannot silently recur in batches nobody re-clicked.
    // ══════════════════════════════════════════════════════════════════════
    [System.Diagnostics.Conditional("DEBUG")]
    private void AssertNavContract()
    {
        var problems = new List<string>();

        var railTags = AllNavItems()
            .Select(i => i.Tag as string)
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t!)
            .ToList();

        foreach (var dup in railTags.GroupBy(t => t).Where(g => g.Count() > 1))
            problems.Add($"Rail tag '{dup.Key}' appears {dup.Count()} times (tags must be unique).");

        // (1) Every rail tag must resolve to a tab, or to a sub-panel of a real tab.
        foreach (var tag in railTags.Distinct())
        {
            if (_notYetMigrated.Contains(tag)) continue;

            if (_subInfo.TryGetValue(tag, out var sub))
            {
                if (!_tabs.ContainsKey(sub.Parent))
                    problems.Add($"Rail tag '{tag}' routes to sub-panel '{sub.Panel}' of tab '{sub.Parent}', but no tab is registered under '{sub.Parent}'.");
                continue;
            }
            if (!_tabs.ContainsKey(tag))
                problems.Add($"Rail tag '{tag}' resolves to NOTHING — it will render the placeholder. Add a tab, a _subInfo entry, or list it in _notYetMigrated.");
        }

        // (2) Every registered tab must be reachable from the rail (directly, or via
        //     a sub-panel tag that routes to it).
        foreach (var tabKey in _tabs.Keys)
        {
            bool direct = railTags.Contains(tabKey);
            bool viaSub = _subInfo.Values.Any(v => v.Parent == tabKey && railTags.Contains(
                              _subInfo.First(kv => kv.Value.Parent == tabKey && kv.Value.Panel == v.Panel).Key));
            if (!direct && !viaSub)
                problems.Add($"Tab '{tabKey}' is registered but UNREACHABLE — no rail item targets it.");
        }

        // (3) Every sub-panel a parent tab can show must have a rail item.
        foreach (var (parentKey, panels) in new (string, string[])[]
        {
            ("Software",  new[] { "Bloatware", "AppInstaller", "Debloat" }),
            ("Customize", new[] { "Taskbar", "Explorer", "ContextMenu", "Appearance", "StartMenu", "Desktop" }),
        })
        {
            if (!_tabs.ContainsKey(parentKey)) continue;
            foreach (var panel in panels)
            {
                bool reachable = railTags.Any(t =>
                    _subInfo.TryGetValue(t, out var s) && s.Parent == parentKey && s.Panel == panel);
                if (!reachable)
                    problems.Add($"Sub-panel '{parentKey}.{panel}' has NO rail item — its content is unreachable (built but never shown).");
            }
        }

        if (problems.Count == 0) return;

        var report = "NAV CONTRACT VIOLATION — rail/tab routing is broken:\n\n • "
                   + string.Join("\n • ", problems);

        // Fail loudly on every channel: log file, debug output, debugger break, and a
        // modal dialog. Deliberately NOT thrown — App.UnhandledException marks
        // exceptions handled, which would swallow it silently.
        System.Diagnostics.Debug.WriteLine(report);
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AkariTool");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "nav-contract-violation.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{report}\n---\n");
        }
        catch { }

        _service.Log("[NAV] " + report.Replace("\n", " "));
        if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();

        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await new ContentDialog
                {
                    Title = "Nav contract violation (Debug)",
                    Content = new ScrollViewer
                    {
                        MaxHeight = 380,
                        Content = new TextBlock { Text = report, TextWrapping = TextWrapping.Wrap }
                    },
                    CloseButtonText = "Continue anyway",
                    XamlRoot = RootGrid.XamlRoot,
                }.ShowAsync();
            }
            catch { }
        });
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        string tag = item.Tag as string ?? "";

        // Resolve the tag: a sub-panel tag routes to its PARENT tab + ShowPanel,
        // otherwise the tag is a top-level tab. A parent selected directly falls
        // back to its default sub-panel (_defaultSub), same as the WPF shell.
        string targetTab = tag;
        string? panel = null;

        if (_subInfo.TryGetValue(tag, out var sub))
        {
            targetTab = sub.Parent;
            panel = sub.Panel;
        }
        else if (_defaultSub.TryGetValue(tag, out var defPanel))
        {
            panel = defPanel;
        }

        bool found = _tabs.ContainsKey(targetTab);
        foreach (var (t, tab) in _tabs)
            tab.Visibility = t == targetTab ? Visibility.Visible : Visibility.Collapsed;

        if (panel is not null)
        {
            if (targetTab == "Software")  _software?.ShowPanel(panel);
            if (targetTab == "Customize") _customize?.ShowPanel(panel);
        }

        if (_placeholder is not null)
            _placeholder.Visibility = found ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Mica / Acrylic system backdrop ──────────────────────────────────────
    // Winhance's rounded, floating look is NOT an inner card — it's a Mica
    // SystemBackdrop on the window: Windows 11 DWM rounds the outer window corners and
    // draws the translucent Mica material in every transparent area of the content
    // (the extended title bar + the margin gutters around the opaque rail/content/log
    // panels). Mirrors Winhance's TrySetMicaBackdrop: prefer Mica (Base), fall back to
    // Desktop Acrylic, and gate on the controllers' IsSupported so it degrades cleanly.
    //
    // The modern Window.SystemBackdrop property wires the backdrop controller +
    // activation/theme handling internally (works unpackaged and elevated on Windows
    // App SDK 1.x), so this stays declarative. The IsSupported gate + the logged result
    // are what prove it actually initialised rather than silently no-opping.
    private void TrySetBackdrop()
    {
        string status;
        if (MicaController.IsSupported())
        {
            this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
            status = "Mica (Base)";
        }
        else if (DesktopAcrylicController.IsSupported())
        {
            this.SystemBackdrop = new DesktopAcrylicBackdrop();
            status = "Desktop Acrylic (Mica unsupported on this build)";
        }
        else
        {
            status = "none supported — keeping the flat AkariFlatBackdrop fill";
        }

        // Only drop the opaque root fill when a backdrop was actually applied, so a
        // machine with no backdrop support keeps the flat look instead of a see-through
        // (black) window. RootGrid keeps AkariFlatBackdrop in XAML as that fallback.
        if (this.SystemBackdrop is not null)
            RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        _service?.Log($"[Backdrop] {status}");
    }

    // Rounds all four corners of the content card by clipping its whole visual subtree
    // (background + drift banner + scrolling content) to a rounded rectangle. The clip
    // geometry's Size is driven by an ExpressionAnimation referencing the card visual's
    // own Size, so it tracks every layout/resize automatically and never goes stale — the
    // failure mode of a static ActualWidth/Height read was an oversized clip that only
    // rounded the top-left (origin) corner and left the other three square.
    private static void ClipCardToRadius(Border card)
    {
        var visual = ElementCompositionPreview.GetElementVisual(card);
        var compositor = visual.Compositor;

        float r = (float)card.CornerRadius.TopLeft;
        if (r <= 0) r = 8;   // AkariCardRadius fallback

        var geometry = compositor.CreateRoundedRectangleGeometry();
        geometry.CornerRadius = new System.Numerics.Vector2(r, r);

        // geometry.Size = visual.Size, continuously.
        var bindSize = compositor.CreateExpressionAnimation("host.Size");
        bindSize.SetReferenceParameter("host", visual);
        geometry.StartAnimation("Size", bindSize);

        visual.Clip = compositor.CreateGeometricClip(geometry);
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.Toggle();
        UpdateThemeVisuals();
    }

    // Footer LOG toggle: shows/hides the log console docked at the bottom of the content
    // card. Collapsing its row lets the tab content reclaim the space; the icon flips
    // between chevron-down (visible → click to hide) and chevron-up (hidden → click to show).
    private void LogToggle_Click(object sender, RoutedEventArgs e)
    {
        bool show = TxtLogPanel.Visibility == Visibility.Collapsed;
        TxtLogPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LogToggleIcon.Glyph = show ? "\uE70D" : "\uE70E";   // ChevronDown (visible) / ChevronUp (hidden)
    }

    private void UpdateThemeVisuals()
    {
        TitleLogo.Source = ThemeService.Logo;
        // Sun glyph while dark (tap → light); moon glyph while light (tap → dark).
        ThemeToggleIcon.Glyph = ThemeService.Current == AkariTheme.Light ? "" : "";   // moon (light) / sun (dark)
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Startup: Competitive Mode orphan recovery
    // ══════════════════════════════════════════════════════════════════════

    private bool _competitiveRecoveryChecked;

    /// <summary>
    /// Offers to undo a session that was never closed properly (crash, power loss,
    /// task-manager kill). Public because the --competitive startup path never shows
    /// the window, so it cannot rely on Loaded to run this.
    /// </summary>
    public async Task CheckOrphanedCompetitiveSessionAsync()
    {
        if (_competitiveRecoveryChecked) return;   // Loaded can re-fire
        _competitiveRecoveryChecked = true;

        CompetitiveSessionState state;
        try { if (!CompetitiveSessionStore.TryLoad(out state)) return; }
        catch { return; }

        // MIGRATION: WPF had to Show() the window first because a WPF dialog needs a
        // shown Owner. A WinUI ContentDialog needs a XamlRoot instead, which exists as
        // soon as the content is loaded — so the window no longer has to be revealed
        // just to host this prompt. On the --competitive path the XamlRoot may not be
        // set yet, so it is taken directly from RootGrid.
        AkariDialogs.XamlRoot ??= RootGrid.XamlRoot;

        try
        {
            bool restore = await AkariDialogs.ConfirmContentAsync(
                new TextBlock
                {
                    Text = $"A Competitive Mode session from {state.StartedUtc.ToLocalTime():g} was not " +
                           "closed properly. Some background apps may still be suspended and some " +
                           "services stopped. Restore normal settings now?",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                "Competitive Mode",
                primaryText: "Restore", closeText: "Ignore");

            if (restore)
            {
                await CompetitiveService.EndAsync(state, null);
                _service.Log("Competitive Mode: orphaned session restored.");
            }
            else
            {
                // Clear either way — otherwise the prompt reappears on every launch for
                // a session the user has chosen not to undo.
                CompetitiveSessionStore.Clear();
                _service.Log("Competitive Mode: orphaned session record discarded.");
            }
        }
        catch { /* recovery must never block startup */ }
    }

    /// <summary>Starts a session from the --competitive command line.</summary>
    public Task StartCompetitiveFromCommandLineAsync(string exePath) =>
        _akariOS is null ? Task.CompletedTask : _akariOS.StartCompetitiveFromCommandLineAsync(exePath);

    // ══════════════════════════════════════════════════════════════════════
    //  Startup: update check
    // ══════════════════════════════════════════════════════════════════════

    private bool _startupUpdateChecked;

    /// <summary>
    /// Silent startup update check. Never surfaces on error or when up to date;
    /// offers to open the App Updates tab when a newer release exists.
    /// </summary>
    private async Task RunStartupUpdateCheckAsync()
    {
        if (_startupUpdateChecked) return;
        _startupUpdateChecked = true;

        UpdateCheckResult result;
        try { result = await UpdateService.CheckAsync(); }
        catch { return; }   // never let a network hiccup surface at startup

        if (result.Status != UpdateStatus.UpdateAvailable) return;

        try
        {
            bool update = await AkariDialogs.ConfirmContentAsync(
                new TextBlock
                {
                    Text = $"Akari Tool {result.LatestTag} is available " +
                           $"(you have {UpdateService.CurrentVersionDisplay}).\n\n" +
                           "Update now?",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                "Update available",
                primaryText: "Update now", closeText: "Later");

            // MIGRATION: WPF navigated to the AppUpdate tab on accept; same here.
            if (update) SelectNavItem("AppUpdate");
        }
        catch { /* the prompt is a convenience — never block startup */ }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Drift banner (shell-level, above the content)
    // ══════════════════════════════════════════════════════════════════════

    private bool _startupDriftScanned;

    /// <summary>Scans once at startup and shows the banner if anything drifted.</summary>
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

    /// <summary>Called by the Verify tab after a manual re-scan.</summary>
    public void RefreshDriftBanner(DriftScanResult result)
    {
        if (result.HasDrift) ShowDriftBanner(result);
        else HideDriftBanner();
    }

    private void ShowDriftBanner(DriftScanResult result)
    {
        DriftBannerHost.Content = DriftBanner.Build(
            result,
            onReview:  () => SelectNavItem("Verify"),
            onDismiss: HideDriftBanner);
        DriftBannerHost.Visibility = Visibility.Visible;
    }

    private void HideDriftBanner()
    {
        DriftBannerHost.Content = null;
        DriftBannerHost.Visibility = Visibility.Collapsed;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Global "Find a setting" search (rail pane header)
    // ══════════════════════════════════════════════════════════════════════

    private void GlobalSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var q = sender.Text.Trim();
        if (q.Length < 2) { sender.ItemsSource = null; return; }
        sender.ItemsSource = TweakRegistry.Search(q);
    }

    // A hit was picked from the dropdown: navigate to its tab and re-run the query
    // in that tab's own search box (Loaded-priority so the tab is visible first).
    private void GlobalSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is TweakRegistry.SearchHit hit) NavigateToHit(hit);
    }

    // Enter with no explicit suggestion selected: navigate to the first hit.
    private void GlobalSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is TweakRegistry.SearchHit hit) { NavigateToHit(hit); return; }
        var q = sender.Text.Trim();
        if (q.Length < 2) return;
        var first = TweakRegistry.Search(q).FirstOrDefault();
        if (first.Id is not null) NavigateToHit(first);
    }

    private void NavigateToHit(TweakRegistry.SearchHit hit)
    {
        SelectNavItem(hit.TabTag);

        // Re-run the query inside the destination tab's own search box, after it is
        // visible. TabTag may be a sub-panel tag (e.g. "Taskbar"), so resolve the
        // owning tab through _subInfo.
        string tabKey = _subInfo.TryGetValue(hit.TabTag, out var sub) ? sub.Parent : hit.TabTag;
        if (_tabs.TryGetValue(tabKey, out var tab))
            DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => tab.ApplySearch(hit.Name));
    }

    private async void InfoDialog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Akari Tool",
            Content = "A gaming-first Windows 11 optimization utility.\n\n" +
                      "WinUI 3 migration — shell + AboutTab, NotificationsTab, SoundTab.",
            CloseButtonText = "Close",
            XamlRoot = RootGrid.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
