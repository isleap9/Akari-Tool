using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Windows.Graphics;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.ViewModels.AdvancedTools;
using AkariTool.ViewModels.Software;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Views;
using AkariTool.Views.Controls;
using WinUI.Framework.Navigation;
using WinUI.Framework.Services;

namespace AkariTool;

/// <summary>
/// The shell window: native WinUI 3 layout — Mica backdrop, custom 48px title
/// bar (logo + wordmark), NavigationView rail (same tags as the migration
/// branch), a Frame hosting pages, a docked live log console, and a status bar
/// with theme toggle + build stamp. Pages navigate in the Frame via
/// <see cref="INavigationService"/>; Phase A routes only "Home" to a real page
/// and every other rail tag to <see cref="PlaceholderPage"/>. Services are
/// injected through the constructor.
/// </summary>
public sealed partial class MainWindow : Window
{
    // Nav routing contract: tags here get a real page, everything else falls
    // through to PlaceholderPage. Tags MUST match the rail tags in MainWindow.xaml.
    private static readonly Dictionary<string, Type> PageMap = new()
    {
        ["Home"] = typeof(HomePage),
        // Optimize hub (OptimizeHubPage): the single rail entry for the Optimize section.
        // The detail tags below (Gaming…Power, AkariOS) keep their PageMap entries so the
        // hub + global-search can resolve tag → detail page type, but they no longer have
        // their own rail item — SelectRailTag routes them through the hub (see below).
        ["Optimize"] = typeof(OptimizeHubPage),
        ["Gaming"] = typeof(GamingPage),
        ["Sound"] = typeof(SoundPage),
        ["Notifications"] = typeof(NotificationsPage),
        ["Update"] = typeof(UpdatePage),
        ["Privacy"] = typeof(PrivacyPage),
        // "Customize" is a single flat rail item landing on the card hub. The 6
        // category tags have NO rail item — they exist here only so the global-search
        // fallback and Home cards can navigate the content Frame straight to a category
        // page by tag. All 7 pages report the "Customize" tag from TagForPage, so the
        // one Customize rail item stays highlighted throughout.
        ["Customize"] = typeof(CustomizePage),
        ["Taskbar"] = typeof(TaskbarPage),
        ["Explorer"] = typeof(ExplorerPage),
        ["Appearance"] = typeof(AppearancePage),
        ["StartMenu"] = typeof(StartMenuPage),
        ["Desktop"] = typeof(DesktopPage),
        ["Power"] = typeof(PowerPage),
        // Software & Apps hub (SoftwareHubPage): the single rail entry for the software
        // section. The three detail tags keep their PageMap entries for hub/search
        // resolution; SelectRailTag routes them through the hub.
        ["SoftwareHub"] = typeof(SoftwareAppsPage),
        ["AppInstaller"] = typeof(ExternalAppsPage),
        ["Bloatware"] = typeof(WindowsAppsPage),
        ["Debloat"] = typeof(DebloatPage),
        // Advanced Tools hub (AdvancedHubPage): the single rail entry for the ADVANCED
        // section. The detail tags below keep their PageMap entries so the hub + global
        // search can resolve tag → page type; SelectRailTag routes them through the hub.
        ["AdvancedHub"] = typeof(AdvancedHubPage),
        ["Backup"] = typeof(BackupPage),
        ["Advanced"] = typeof(AdvancedToolsPage),
        ["Tools"] = typeof(ToolsPage),
        ["Verify"] = typeof(VerifyPage),
        ["Settings"] = typeof(SettingsPage),
        ["AkariOS"] = typeof(AkariOSPage),
    };

    // Optimize sections that folded into the hub — they have no rail item any more, so
    // SelectRailTag routes them through OptimizeHubPage's inner frame instead.
    private static readonly HashSet<string> OptimizeDetailTags = new()
    { "Gaming", "Privacy", "Update", "Notifications", "Sound", "Power", "AkariOS" };

    // Customize sections fold into the Customize hub the same way — no rail item each.
    private static readonly HashSet<string> CustomizeDetailTags = new()
    { "Taskbar", "Explorer", "Appearance", "StartMenu", "Desktop" };

    // Advanced Tools sections fold into the Advanced Tools hub — no rail item each.
    private static readonly HashSet<string> AdvancedDetailTags = new()
    { "Advanced", "Tools", "Backup", "Verify" };

    // Software catalog pages fold into the Software & Apps hub — no rail item each.
    private static readonly HashSet<string> SoftwareDetailTags = new()
    { "Bloatware", "AppInstaller", "Debloat" };

    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _themes;
    private readonly ILogService _log;
    private readonly AkariUiLogService _uiLog;
    private readonly ISettingsService _settings;
    private readonly ToolService _tool;
    private readonly IFileService _files;
    private readonly INavBadgeService _navBadges;
    private readonly IDispatcherService _dispatcher;
    private IDisposable? _navBadgeSubscription;

    // Named (not a lambda) so OnClosed can unsubscribe the exact same delegate instance.
    private readonly EventHandler<AppTheme> _themeChangedHandler;

    /// <summary>
    /// Recomputes the sidebar badges: the per-page pending counts are aggregated onto their
    /// owning hub button (e.g. Gaming + Privacy + … → Optimize), so a collapsed hub shows the
    /// total work behind it. Cheap — reads the same counts the pages already track.
    /// </summary>
    private void RefreshNavBadges()
    {
        var perHub = new Dictionary<string, int>
        {
            ["Home"] = 0, ["Optimize"] = 0, ["Customize"] = 0,
            ["SoftwareHub"] = 0, ["AdvancedHub"] = 0, ["Settings"] = 0,
        };
        foreach (var u in _navBadges.ComputeNavBadges())
        {
            var hub = HubTagFor(u.Tag);
            if (perHub.ContainsKey(hub)) perHub[hub] += u.Count;
        }
        foreach (var (tag, count) in perHub)
            Sidebar.SetBadge(tag, count);
    }

    /// <summary>Maps a page/detail tag to the top-level sidebar tag that owns it.</summary>
    private static string HubTagFor(string tag) =>
        OptimizeDetailTags.Contains(tag) ? "Optimize"
        : CustomizeDetailTags.Contains(tag) ? "Customize"
        : AdvancedDetailTags.Contains(tag) ? "AdvancedHub"
        : SoftwareDetailTags.Contains(tag) ? "SoftwareHub"
        : tag;

    public MainWindow(
        INavigationService navigation,
        IDialogService dialogs,
        IThemeService themes,
        ILogService log,
        AkariUiLogService uiLog,
        ISettingsService settings,
        ToolService tool,
        IFileService files,
        IDispatcherService dispatcherService,
        INavBadgeService navBadges)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _themes = themes;
        _log = log;
        _uiLog = uiLog;
        _settings = settings;
        _tool = tool;
        _files = files;
        _navBadges = navBadges;

        // Late-initialized service (see DispatcherService remarks): the DI container
        // builds before any Window exists, so the UI DispatcherQueue can only be
        // captured here, on the UI thread, after InitializeComponent.
        dispatcherService.Initialize(DispatcherQueue);
        _dispatcher = dispatcherService;
        _themeChangedHandler = (_, _) => UpdateThemeVisuals();

        InitializeComponent();

        // Sidebar badges (4e): first pass + subscription deferred to Loaded so the
        // service's IEnumerable<SettingPageViewModel> resolves AFTER construction —
        // eager resolution here would force all 11 page Builds onto the UI thread
        // during DI, stealing warm-up ownership.
        Sidebar.Loaded += (_, _) =>
        {
            RefreshNavBadges();
            _navBadgeSubscription = _navBadges.Subscribe((_, _) =>
                _dispatcher.RunOnUIThread(RefreshNavBadges));
        };

        // Custom title bar + icon.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AkariLogo.ico");

        // The system file pickers (Backup export/import) are Win32 dialogs and need the
        // app's HWND before they can be shown, or they throw "no owner window".
        _files.WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Cross-VM provider hook (Phase 25/Phase 3): the Advanced Tools Autounattend
        // generator reads the apps currently ticked in Software ▸ Windows Apps. net8's
        // MainWindow did the same one line — adv.SetSelectedAppsProvider(() =>
        // _software.GetSelectedWindowsApps()). Both are DI singletons, so this points at
        // the same WindowsAppsViewModel instance the Bloatware page uses (its selection
        // state persists across navigation).
        WinUI.Framework.IoC.ServiceLocator.GetService<AdvancedToolsViewModel>()
            .SetSelectedAppsProvider(
                () => WinUI.Framework.IoC.ServiceLocator.GetService<WindowsAppsViewModel>()
                          .GetSelectedWindowsApps());

        // XamlRoot is null until the window shows; resolve it on the first layout.
        if (Content is FrameworkElement rootElement)
        {
            rootElement.Loaded += (_, _) =>
            {
                _dialogs.XamlRoot = rootElement.XamlRoot;

                // First-launch restore-point offer (4g — Winhance StartupUiCoordinator
                // parity: fires once startup has rendered). Hooked HERE, not Nav.Loaded,
                // because Loaded fires child-first — Nav.Loaded runs before this handler,
                // and TweakDialogs fail-safes to DECLINED on a null XamlRoot, which would
                // silently consume the one-shot offer (the pref is set before the dialog).
                _ = WinUI.Framework.IoC.ServiceLocator.GetService<IStartupNotificationService>()
                    .ShowFirstLaunchRestoreOfferAsync();
            };
        }

        // Theme BEFORE content paints. The framework theme service persists the choice;
        // "Default" follows the user's current Windows theme — no forced dark. The crimson
        // accent is defined in both theme dictionaries (App.xaml), so it holds either way.
        _themes.RootElement = RootGrid;
        _themes.Initialize();

        // Keep the title-bar logo/glyph in sync with theme changes from ANY source —
        // the title-bar toggle button, the Settings page theme picker, etc. Previously
        // UpdateThemeVisuals() only ran from ThemeToggle_Click, so switching theme via
        // Settings left the title bar logo showing the wrong (illegible) variant.
        _themes.ThemeChanged += _themeChangedHandler;

        // Navigation shell.
        _navigation.Frame = ContentFrame;
        ContentFrame.NavigationFailed += OnNavigationFailed;
        ContentFrame.Navigated += OnNavigated;

        // Live log dock: the UI-log decorator raises a line for every log call.
        _uiLog.LineLogged += line =>
            DispatcherQueue.TryEnqueue(() =>
            {
                TxtLog.Text += line + Environment.NewLine;
                DispatcherQueue.TryEnqueue(
                    Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                    () => ScrollLogToEnd(TxtLog));
            });

        // HEADLESS-EVENT SUBSCRIBERS: ToolService.ProgressStarted / ProgressStopped.
        // (LineLogged is already covered — the ToolService sink writes through
        // ILogService, which _uiLog.LineLogged above renders into the dock.)
        // Without these the status bar would sit on "Ready" through a long script
        // run: no crash, just stale UI — the bug class this project keeps hitting.
        _tool.ProgressStarted += name =>
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = string.IsNullOrWhiteSpace(name) ? "Working…" : $"Running {name}…";
                StatusProgress.Visibility = Visibility.Visible;
                StatusProgress.IsIndeterminate = true;
            });

        _tool.ProgressStopped += () =>
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = "Ready";
                StatusProgress.IsIndeterminate = false;
                StatusProgress.Visibility = Visibility.Collapsed;
            });

        BuildStamp.Text = $"WinUI 3 · .NET {Environment.Version.Major} · build {GetBuildVersion()}";

        UpdateThemeVisuals();

        _log.Info("Akari Tool started.");

        RestoreWindowSize();
        Closed += OnClosed;

        // Land on Home.
        SelectRailTag("Home");
    }

    // ── Nav routing ───────────────────────────────────────────────────────

    /// <summary>A sidebar button was clicked (or invoked via keyboard) — route by its tag.</summary>
    private void Sidebar_ItemClicked(object? sender, NavButtonClickedEventArgs e)
    {
        if (e.Tag is { } tag) SelectRailTag(tag);
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        SyncSelectedItem();
    }

    /// <summary>
    /// Keeps the rail in sync when navigation happens outside the rail (Home quick
    /// links navigate directly to placeholder destinations).
    /// </summary>
    private void SyncSelectedItem()
    {
        string? tag = TagForPage(ContentFrame.Content);
        if (tag is null) return;
        // TagForPage already resolves detail pages to their hub; HubTagFor covers any
        // placeholder that still reports a raw detail tag. Result is one of the 6 sidebar tags.
        Sidebar.SelectedTag = HubTagFor(tag);
    }

    /// <summary>
    /// The rail tag a given content page belongs to. Usually 1:1 with the page type,
    /// but the 6 Customize category pages (Taskbar…Desktop) AND the CustomizePage hub
    /// all map to the single "Customize" rail item — the same way Advanced Tools keeps
    /// one rail item highlighted across its internal panel swaps. Shared by
    /// <see cref="SyncSelectedItem"/> (which rail item to highlight) and
    /// <see cref="Nav_SelectionChanged"/> (whether a nav is even needed).
    /// </summary>
    private static string? TagForPage(object? content) => content switch
    {
        HomePage => "Home",
        // Optimize hub + its detail pages (hosted in the hub's inner frame) all keep the
        // single "Optimize" rail item highlighted.
        OptimizeHubPage => "Optimize",
        GamingPage => "Optimize",
        SoundPage => "Optimize",
        NotificationsPage => "Optimize",
        UpdatePage => "Optimize",
        PrivacyPage => "Optimize",
        CustomizePage => "Customize",
        TaskbarPage => "Customize",
        ExplorerPage => "Customize",
        AppearancePage => "Customize",
        StartMenuPage => "Customize",
        DesktopPage => "Customize",
        PowerPage => "Optimize",
        // Software hub + its catalog pages (hosted in the hub inner frame) keep the
        // single "Software & Apps" rail item highlighted.
        SoftwareAppsPage => "SoftwareHub",
        ExternalAppsPage => "SoftwareHub",
        WindowsAppsPage => "SoftwareHub",
        DebloatPage => "SoftwareHub",
        // Advanced Tools hub + its detail pages (hosted in the hub inner frame) keep the
        // single "Advanced Tools" rail item highlighted.
        AdvancedHubPage => "AdvancedHub",
        BackupPage => "AdvancedHub",
        AdvancedToolsPage => "AdvancedHub",
        ToolsPage => "AdvancedHub",
        VerifyPage => "AdvancedHub",
        AkariOSPage => "Optimize",
        SettingsPage => "Settings",
        PlaceholderPage p => p.ViewModel.TabTag,
        _ => null,
    };

    /// <summary>
    /// Selects a rail item by tag, which triggers the normal Nav routing (real page or
    /// PlaceholderPage) + rail highlight — the same path a real click takes. Used by the
    /// Home quick-nav cards so they route exactly like the sidebar.
    /// </summary>
    public void SelectRailTag(string tag)
    {
        // Optimize detail sections have no rail item — select the hub, then drill into the
        // matching card so global search / Home cards land directly on the section.
        if (OptimizeDetailTags.Contains(tag))
        {
            SelectRailTag("Optimize");
            if (ContentFrame.Content is OptimizeHubPage hub
                && PageMap.TryGetValue(tag, out var detailType))
            {
                hub.ShowDetailFor(detailType);
            }
            return;
        }

        // Customize detail sections — same pattern through the Customize hub.
        if (CustomizeDetailTags.Contains(tag))
        {
            SelectRailTag("Customize");
            if (ContentFrame.Content is CustomizePage hub
                && PageMap.TryGetValue(tag, out var detailType))
            {
                hub.ShowDetailFor(detailType);
            }
            return;
        }

        // Advanced Tools detail sections — same pattern through the Advanced Tools hub.
        if (AdvancedDetailTags.Contains(tag))
        {
            SelectRailTag("AdvancedHub");
            if (ContentFrame.Content is AdvancedHubPage hub
                && PageMap.TryGetValue(tag, out var detailType))
            {
                hub.ShowDetailFor(detailType);
            }
            return;
        }

        // Software catalog pages — same pattern through the Software & Apps hub.
        if (SoftwareDetailTags.Contains(tag))
        {
            SelectRailTag("SoftwareHub");
            if (ContentFrame.Content is SoftwareAppsPage hub
                && PageMap.TryGetValue(tag, out var detailType))
            {
                hub.ShowDetailFor(detailType);
            }
            return;
        }

        // Top-level tag (Home / Optimize / Customize / SoftwareHub / AdvancedHub / Settings)
        // or a category tag with no sidebar entry: navigate the content Frame straight to the
        // page. OnNavigated → SyncSelectedItem then highlights the owning sidebar button. The
        // TagForPage guard avoids re-navigating when we're already on a page of this tag (six
        // Customize category pages all report "Customize", so re-selecting must not bounce a
        // search landing back to the hub).
        if (PageMap.TryGetValue(tag, out var pageType))
        {
            if (TagForPage(ContentFrame.Content) != tag)
                _navigation.NavigateTo(pageType);
        }
        else
        {
            // Not migrated yet → placeholder, carrying the tag so it can name itself.
            if (ContentFrame.CurrentSourcePageType != typeof(PlaceholderPage)
                || ContentFrame.Content is not PlaceholderPage { ViewModel.TabTag: var current } || current != tag)
            {
                _navigation.NavigateTo(typeof(PlaceholderPage), tag);
            }
        }
    }

    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        _log.Error($"Navigation to '{e.SourcePageType}' failed.", e.Exception);
        _ = _dialogs.ShowInfoAsync("Navigation failed", e.Exception?.Message ?? "Unknown navigation error.");
        e.Handled = true;
    }

    // ── Theme toggle ──────────────────────────────────────────────────────

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        // UpdateThemeVisuals() runs via the ThemeChanged subscription set up in the
        // constructor — no need to call it here too.
        _themes.ApplyTheme(_themes.CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
    }

    private void UpdateThemeVisuals()
    {
        // Sun glyph while dark (tap → light); moon glyph while light (tap → dark).
        ThemeToggleIcon.Glyph = _themes.CurrentTheme == AppTheme.Light ? "\uE708" : "\uE706";

        // The title-bar logo is now a vector Path (see MainWindow.xaml) filled with
        // ThemeResource brushes, so it repaints itself automatically whenever
        // RootElement.RequestedTheme changes — no bitmap swap needed here any more.
    }

    // ── Log dock toggle ───────────────────────────────────────────────────

    private void LogToggle_Click(object sender, RoutedEventArgs e)
    {
        bool show = TxtLogPanel.Visibility == Visibility.Collapsed;
        TxtLogPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LogToggleIcon.Glyph = show ? "\uE70D" : "\uE70E";   // ChevronDown (visible) / ChevronUp (hidden)
    }

    // ── Drift notification banner ──────────────────────────────────────────
    // Mirrors the old Akari Tool's title-bar drift alert: on startup, compare the
    // baseline of what Akari applied against live system state, and if Windows has
    // silently rolled any tweak back to its factory default, surface a banner just
    // under the title bar. Only "reverted to Windows default" drift raises it — a
    // "changed to some other value" is usually a deliberate user edit, not a rollback.

    /// <summary>
    /// Runs a drift scan and opens the banner if any tracked tweak has reverted to its
    /// Windows default. Called by App AFTER the tweak-registry warm-up completes — a scan
    /// before then can't resolve most baseline entries and would report no drift.
    /// </summary>
    public void RunDriftCheck()
    {
        try
        {
            var result = DriftScanner.Scan();
            int reverted = result.RevertedCount;
            if (reverted <= 0) return;

            DriftBanner.Message = reverted == 1
                ? "1 tweak has reverted to its Windows default — usually the signature of a Windows Update rollback."
                : $"{reverted} tweaks have reverted to their Windows defaults — usually the signature of a Windows Update rollback.";
            DriftBanner.IsOpen = true;
        }
        catch (Exception ex)
        {
            // Diagnostics only — a failed drift check must never break the shell.
            _log.Error("Startup drift scan failed.", ex);
        }
    }

    private void DriftBanner_Review_Click(object sender, RoutedEventArgs e)
    {
        DriftBanner.IsOpen = false;
        SelectRailTag("Verify");   // Verify page re-scans on navigation and shows the details.
    }

    private async void InfoDialog_Click(object sender, RoutedEventArgs e)
        => await _dialogs.ShowInfoAsync(
            "Akari Tool",
            "A gaming-first Windows 11 optimization utility.\n\n" +
            "WinUI 3 framework rebuild — Phase A: shell + Home.");

    // ── Log console scroll-to-bottom ───────────────────────────────────────

    private static void ScrollLogToEnd(TextBox box)
    {
        var viewer = FindDescendant<ScrollViewer>(box);
        if (viewer is not null && double.IsFinite(viewer.ExtentHeight) && viewer.ExtentHeight > 0)
        {
            viewer.ChangeView(null, viewer.ExtentHeight, null, disableAnimation: true);
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var found = FindDescendant<T>(child);
            if (found is not null) return found;
        }

        return null;
    }

    // ── Window size persistence ────────────────────────────────────────────

    private void RestoreWindowSize()
    {
        if (_settings.Contains("WindowWidth") && _settings.Contains("WindowHeight"))
        {
            AppWindow.Resize(new SizeInt32(
                _settings.Get("WindowWidth", 1100),
                _settings.Get("WindowHeight", 700)));
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _themes.ThemeChanged -= _themeChangedHandler;
        _settings.Set("WindowWidth", AppWindow.Size.Width);
        _settings.Set("WindowHeight", AppWindow.Size.Height);
        _log.Info("Akari Tool closed.");
    }

    private static string GetBuildVersion()
    {
        var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        return v is null ? "0.0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}