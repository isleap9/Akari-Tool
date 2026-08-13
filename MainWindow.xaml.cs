using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Windows.Graphics;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.AdvancedTools;
using AkariTool.ViewModels.Software;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Views;
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
        ["ContextMenu"] = typeof(ContextMenuPage),
        ["Appearance"] = typeof(AppearancePage),
        ["StartMenu"] = typeof(StartMenuPage),
        ["Desktop"] = typeof(DesktopPage),
        ["Power"] = typeof(PowerPage),
        ["AppInstaller"] = typeof(ExternalAppsPage),
        ["Bloatware"] = typeof(WindowsAppsPage),
        ["Debloat"] = typeof(DebloatPage),
        ["Backup"] = typeof(BackupPage),
        ["Advanced"] = typeof(AdvancedToolsPage),
        ["Tools"] = typeof(ToolsPage),
        ["Verify"] = typeof(VerifyPage),
        ["Settings"] = typeof(SettingsPage),
        ["AkariOS"] = typeof(AkariOSPage),
    };

    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _themes;
    private readonly ILogService _log;
    private readonly AkariUiLogService _uiLog;
    private readonly ISettingsService _settings;
    private readonly ToolService _tool;
    private readonly IFileService _files;

    // Named (not a lambda) so OnClosed can unsubscribe the exact same delegate instance.
    private readonly EventHandler<AppTheme> _themeChangedHandler;

    public MainWindow(
        INavigationService navigation,
        IDialogService dialogs,
        IThemeService themes,
        ILogService log,
        AkariUiLogService uiLog,
        ISettingsService settings,
        ToolService tool,
        IFileService files)
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _themes = themes;
        _log = log;
        _uiLog = uiLog;
        _settings = settings;
        _tool = tool;
        _files = files;
        _themeChangedHandler = (_, _) => UpdateThemeVisuals();

        InitializeComponent();

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
            rootElement.Loaded += (_, _) => _dialogs.XamlRoot = rootElement.XamlRoot;
        }

        // Theme BEFORE content paints. The framework theme service persists the
        // choice; Akari's brand default is dark.
        _themes.RootElement = RootGrid;
        _themes.Initialize();
        if (_themes.CurrentTheme == AppTheme.Default)
        {
            _themes.ApplyTheme(AppTheme.Dark);
        }

        // Keep the title-bar logo/glyph in sync with theme changes from ANY source —
        // the title-bar toggle button, the Settings page theme picker, etc. Previously
        // UpdateThemeVisuals() only ran from ThemeToggle_Click, so switching theme via
        // Settings left the title bar logo showing the wrong (illegible) variant.
        _themes.ThemeChanged += _themeChangedHandler;

        // Navigation shell.
        _navigation.Frame = ContentFrame;
        ContentFrame.NavigationFailed += OnNavigationFailed;
        ContentFrame.Navigated += OnNavigated;
        Nav.SelectionChanged += Nav_SelectionChanged;

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
        Nav.SelectedItem = AllNavItems().FirstOrDefault(i => (i.Tag as string) == "Home");
    }

    // ── Nav routing ───────────────────────────────────────────────────────

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

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag }) return;

        if (PageMap.TryGetValue(tag, out var pageType))
        {
            // Guard on the tag the CURRENT page belongs to, not on an exact page-type
            // match: 6 category pages (Taskbar…Desktop) all report the "Customize" tag,
            // so if we're already on one of them and "Customize" is (re)selected, we must
            // NOT navigate to the hub — that would bounce a search landing back to the
            // card grid. Only navigate when the current page belongs to a different tag.
            if (TagForPage(ContentFrame.Content) != tag)
            {
                _navigation.NavigateTo(pageType);
            }
        }
        else
        {
            // Not migrated yet → placeholder, carrying the rail tag so it can name itself.
            if (ContentFrame.CurrentSourcePageType != typeof(PlaceholderPage)
                || ContentFrame.Content is not PlaceholderPage { ViewModel.TabTag: var current } || current != tag)
            {
                _navigation.NavigateTo(typeof(PlaceholderPage), tag);
            }
        }
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
        var item = AllNavItems().FirstOrDefault(i => (i.Tag as string) == tag);
        if (item is not null) Nav.SelectedItem = item;
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
        GamingPage => "Gaming",
        SoundPage => "Sound",
        NotificationsPage => "Notifications",
        UpdatePage => "Update",
        PrivacyPage => "Privacy",
        CustomizePage => "Customize",
        TaskbarPage => "Customize",
        ExplorerPage => "Customize",
        ContextMenuPage => "Customize",
        AppearancePage => "Customize",
        StartMenuPage => "Customize",
        DesktopPage => "Customize",
        PowerPage => "Power",
        ExternalAppsPage => "AppInstaller",
        WindowsAppsPage => "Bloatware",
        DebloatPage => "Debloat",
        BackupPage => "Backup",
        AdvancedToolsPage => "Advanced",
        ToolsPage => "Tools",
        VerifyPage => "Verify",
        AkariOSPage => "AkariOS",
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
        var item = AllNavItems().FirstOrDefault(i => (i.Tag as string) == tag);
        if (item is not null)
        {
            Nav.SelectedItem = item;
            return;
        }

        // No rail item carries this tag — this happens for the 6 Customize category
        // tags (Taskbar…Desktop), which have no sidebar entry any more. Navigate the
        // content Frame straight to the page instead. SyncSelectedItem then highlights
        // the "Customize" rail item (TagForPage maps all 6 to "Customize"), and
        // Nav_SelectionChanged's tag-based guard keeps us on the category page rather
        // than bouncing back to the hub.
        if (PageMap.TryGetValue(tag, out var pageType))
        {
            _navigation.NavigateTo(pageType);
        }
    }

    // ── Global search (rail AutoSuggestBox → TweakRegistry.Search) ─────────────

    /// <summary>Rail global-search projection: a TweakRegistry hit as a display row
    /// the AutoSuggestBox can show ("Name — TabLabel") and navigate from. Local to the shell.</summary>
    private sealed record SearchResult(string Display, string Tag, string Name);

    private void GlobalSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        var query = sender.Text.Trim();
        if (query.Length < 2) { sender.ItemsSource = null; return; }

        sender.ItemsSource = TweakRegistry.Search(query, max: 10)
            .Select(h => new SearchResult($"{h.Name}  —  {h.TabLabel}", h.TabTag, h.Name))
            .ToList();
    }

    private void GlobalSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // A tapped/entered suggestion arrives as ChosenSuggestion; a bare Enter (no pick)
        // falls back to the top hit for the typed text.
        var result = args.ChosenSuggestion as SearchResult
            ?? TweakRegistry.Search(sender.Text.Trim(), max: 1)
                  .Select(h => new SearchResult($"{h.Name}  —  {h.TabLabel}", h.TabTag, h.Name))
                  .FirstOrDefault();
        if (result is null) return;

        // 1. Navigate + rail highlight — the existing public path (same as Home cards).
        SelectRailTag(result.Tag);

        // 2. Pre-fill the destination tweak page's own per-page search. The page VMs are
        //    DI SINGLETONS keyed by NavTag, so we set SearchText on the singleton (which
        //    persists into the page whenever it renders) — no page-instance handle needed.
        //    OnSearchTextChanged → ApplySearch already filters to the matched row.
        var vm = App.Services.GetServices<TweakPageViewModel>()
                             .FirstOrDefault(v => v.NavTag == result.Tag);
        if (vm is not null) vm.SearchText = result.Name;
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