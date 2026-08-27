using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.AkariOS;
using AkariTool.Core.Models.ShaderCache;
using WinUI.Framework.IoC;
using WinUI.Framework.Services;
using AkariTool.Core.Competitive;

namespace AkariTool.Views;

public sealed partial class AkariOSPage
{
    // ══════════════════════════════════════════════════════════════════════
    //  COMPETITIVE MODE — Sub-part A: scaffold + game picker + file picker
    //  (net8 AkariOSTab.Competitive.cs, ported near line-for-line)
    //
    //  Session-scoped tweaks around a single game launch. This sub-part ports the
    //  shell + picker (combo/Browse/Create-Shortcut/Start) + read-only Steam game
    //  detection + the file-picker (FilePickers → IFileService, elevation-safe).
    //  Anti-cheat notice (B), option groups (C), and status/session state machine (D)
    //  are STUBBED below — filled in later signed-off sub-parts.
    // ══════════════════════════════════════════════════════════════════════

    // ── Game picker ───────────────────────────────────────────────────
    private ComboBox? _cmGameCombo;
    private Button?   _cmBrowseBtn;
    private Button?   _cmShortcutBtn;
    private Button?   _cmPrimaryBtn;
    private TextBlock? _cmPickerHint;
    private TextBlock? _cmLaunchInfo;
    private readonly List<DetectedGame> _cmGames = new();
    private bool _cmDetectionStarted;
    // Session-control state (_cmSelectedPath, _cmBusy, _cmCts, _cmActiveSchemeName) moved to
    // AkariOSViewModel (E1). The page reads them via ViewModel.SelectedPath/IsBusy/ActiveSchemeName.

    // ── Options (sub-part C) ──────────────────────────────────────────
    private CheckBox? _cmBoostPriority, _cmGameFocus, _cmPauseServices,
                      _cmCloseAfterLaunch, _cmConsistentPerf, _cmClearStandby,
                      _cmLaunchThroughSteam;
    private ComboBox? _cmPriorityLevel, _cmIoPriority, _cmCpuSets;
    private StackPanel? _cmPrioritySubOptions;

    // ── Status UI (sub-part D; session-control state lives in the VM since E1) ──
    private TextBlock? _cmStatusHeadline;
    private StackPanel? _cmStatusDetail;
    private DispatcherTimer? _cmElapsedTimer;

    private void BuildCompetitiveContent(StackPanel panel)
    {
        panel.Children.Add(new TextBlock
        {
            Text = "Applies a set of temporary, session-scoped tweaks around a single game launch " +
                   "and undoes all of them when the game exits.",
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        BuildCompetitivePicker(panel);
        BuildCompetitiveAntiCheatNotice(panel);   // PORTED (sub-part B)
        BuildCompetitiveOptionGroups(panel);      // PORTED (sub-part C)
        BuildCompetitiveStatus(panel);            // PORTED (sub-part D)

        LoadCompetitiveOptionsIntoUi(CompetitivePrefs.LoadOptions());

        // The session-control machine lives in the VM (E1). Render its events; OnVmStateChanged
        // also picks up an already-active session on a mid-session page rebuild (E1 flag 5).
        ViewModel.Status += SetCompetitiveStatus;
        ViewModel.StateChanged += OnVmStateChanged;
        Unloaded += (_, _) =>
        {
            ViewModel.Status -= SetCompetitiveStatus;
            ViewModel.StateChanged -= OnVmStateChanged;
        };
        OnVmStateChanged();   // initial sync (net8 SyncCompetitiveControlStates) + timer if active

        // Detection walks every .exe under every Steam library — seconds of I/O — so it
        // is deferred to Loaded rather than run during page construction.
        if (IsLoaded) BeginCompetitiveGameDetection();
        else          Loaded += OnCompetitiveLoaded;
    }

    // Renders VM session/busy state: re-syncs page controls + drives the elapsed timer
    // (page-owned UI). Called on every VM StateChanged and once on (re)build — so a page
    // rebuilt mid-session reflects the already-active session instead of resetting to idle.
    private void OnVmStateChanged()
    {
        SyncCompetitiveControlStates();
        if (CompetitiveService.IsSessionActive) StartCompetitiveTimer();
        else                                    StopCompetitiveTimer();
    }

    // ── Game picker row ───────────────────────────────────────────────────

    private void BuildCompetitivePicker(StackPanel panel)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _cmGameCombo = new ComboBox
        {
            FontSize = 13,
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _cmGameCombo.SelectionChanged += (_, _) =>
        {
            if (_cmGameCombo!.SelectedIndex >= 0 && _cmGameCombo.SelectedIndex < _cmGames.Count)
            {
                var chosen = _cmGames[_cmGameCombo.SelectedIndex].ExePath;
                ViewModel.SelectedPath = chosen;
                CompetitivePrefs.SaveLastGamePath(chosen);
            }
            SyncCompetitiveControlStates();
        };
        Grid.SetColumn(_cmGameCombo, 0);
        row.Children.Add(_cmGameCombo);

        _cmBrowseBtn = MakeCompetitiveButton("Browse…", "GridBtn", BrowseForGame);
        Grid.SetColumn(_cmBrowseBtn, 1);
        row.Children.Add(_cmBrowseBtn);

        _cmShortcutBtn = MakeCompetitiveButton("Create Shortcut", "GridBtn", CreateCompetitiveShortcut);
        Grid.SetColumn(_cmShortcutBtn, 2);
        row.Children.Add(_cmShortcutBtn);

        _cmPrimaryBtn = MakeCompetitiveButton("Start Competitive Mode", "RunBtn",
            () => _ = ViewModel.PrimaryClickAsync(ReadCompetitiveOptionsFromUi()));
        Grid.SetColumn(_cmPrimaryBtn, 3);
        row.Children.Add(_cmPrimaryBtn);

        panel.Children.Add(row);

        _cmPickerHint = new TextBlock
        {
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed
        };
        panel.Children.Add(_cmPickerHint);

        // Which exe detection resolved to, and how it will be started.
        _cmLaunchInfo = new TextBlock
        {
            FontSize = 11.5,
            Foreground = Res("TextFillColorTertiaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed
        };
        panel.Children.Add(_cmLaunchInfo);
    }

    /// <summary>Updates the "&lt;exe&gt; — via Steam (AppID n) / direct launch" line.</summary>
    private void RefreshCompetitiveLaunchInfo()
    {
        if (_cmLaunchInfo is null) return;

        var selected = ViewModel.SelectedPath;
        if (selected is null)
        {
            _cmLaunchInfo.Visibility = Visibility.Collapsed;
            return;
        }

        string method;
        try
        {
            var plan = CompetitiveService.ResolveLaunch(selected, ReadCompetitiveOptionsFromUi());
            method = plan.ViaSteam ? $"via Steam (AppID {plan.AppId})" : "direct launch";
        }
        catch { method = "direct launch"; }

        _cmLaunchInfo.Text = $"{Path.GetFileName(selected)} — {method}";
        _cmLaunchInfo.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// MIGRATION: net8 mapped WPF style keys → native chrome: RunBtn → AccentButtonStyle,
    /// anything else → default Button. The style param is kept so call sites are unchanged.
    /// </summary>
    private Button MakeCompetitiveButton(string label, string style, Action onClick)
    {
        var btn = new Button
        {
            Content = label,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(16, 10, 16, 10),
            FontSize = 13,
        };
        if (style == "RunBtn")
            btn.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        btn.Click += (_, _) =>
        {
            try { onClick(); }
            catch (Exception ex) { SetCompetitiveStatus($"Error: {ex.Message}"); Service?.Log($"ERROR {label}: {ex.Message}"); }
        };
        return btn;
    }

    private void OnCompetitiveLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnCompetitiveLoaded;
        BeginCompetitiveGameDetection();
    }

    /// <summary>Runs Steam detection off the UI thread, then populates the picker.</summary>
    private void BeginCompetitiveGameDetection()
    {
        if (_cmDetectionStarted) return;
        _cmDetectionStarted = true;

        if (_cmPickerHint is not null)
        {
            _cmPickerHint.Text = "Detecting installed games…";
            _cmPickerHint.Visibility = Visibility.Visible;
        }

        _ = Task.Run(() =>
        {
            IReadOnlyList<DetectedGame> found;
            try { found = GameDetection.DetectSteamGames(); }
            catch { found = Array.Empty<DetectedGame>(); }

            return DispatcherQueue.TryEnqueue(() => PopulateCompetitiveGames(found));
        });
    }

    private void PopulateCompetitiveGames(IReadOnlyList<DetectedGame> detected)
    {
        _cmGames.Clear();
        _cmGames.AddRange(detected);

        // A previously browsed exe that detection does not know about is added so the
        // persisted choice survives a restart.
        string? last = CompetitivePrefs.LoadLastGamePath();
        if (last is not null && !_cmGames.Any(g => g.ExePath.Equals(last, StringComparison.OrdinalIgnoreCase)))
        {
            try { if (File.Exists(last)) _cmGames.Insert(0, new DetectedGame(Path.GetFileNameWithoutExtension(last), last)); }
            catch { }
        }

        if (_cmGameCombo is not null)
        {
            _cmGameCombo.Items.Clear();
            foreach (var g in _cmGames) _cmGameCombo.Items.Add(g.Name);
        }

        if (_cmPickerHint is not null)
        {
            bool empty = _cmGames.Count == 0;
            _cmPickerHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            _cmPickerHint.Text = "No games detected. Use Browse to pick an .exe manually.";
        }

        // Restore the persisted selection.
        if (last is not null && _cmGameCombo is not null)
        {
            int i = _cmGames.FindIndex(g => g.ExePath.Equals(last, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) { _cmGameCombo.SelectedIndex = i; ViewModel.SelectedPath = _cmGames[i].ExePath; }
        }

        SyncCompetitiveControlStates();
    }

    // MIGRATION: WinUI pickers are async, so the body is BrowseForGameAsync and this stays
    // a void Action for MakeCompetitiveButton's signature.
    private void BrowseForGame() => _ = BrowseForGameAsync();

    private async Task BrowseForGameAsync()
    {
        // net8 FilePickers.OpenFileAsync(".exe") → IFileService.PickSingleFileAsync (Win32
        // IFileOpenDialog via AkariFileService — elevation-safe; a raw WinRT picker throws
        // COMException 0x80004005 under requireAdministrator, Phase 4). Returns a StorageFile.
        var picked = await _files.PickSingleFileAsync(new[] { ".exe" });
        if (picked is null) return;

        string path = picked.Path;
        string name = Path.GetFileNameWithoutExtension(path);

        int existing = _cmGames.FindIndex(g => g.ExePath.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing < 0)
        {
            _cmGames.Insert(0, new DetectedGame(name, path));
            _cmGameCombo?.Items.Insert(0, name);
            existing = 0;
        }

        if (_cmGameCombo is not null) _cmGameCombo.SelectedIndex = existing;
        ViewModel.SelectedPath = path;
        CompetitivePrefs.SaveLastGamePath(path);

        if (_cmPickerHint is not null) _cmPickerHint.Visibility = Visibility.Collapsed;
        SyncCompetitiveControlStates();
    }

    private void CreateCompetitiveShortcut()
    {
        string? path = ViewModel.SelectedPath;
        if (string.IsNullOrWhiteSpace(path)) { SetCompetitiveStatus("Select a game first."); return; }

        try
        {
            string gameName = Path.GetFileNameWithoutExtension(path);
            string desktop  = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string linkPath = Path.Combine(desktop, $"{SanitizeFileName(gameName)} (Competitive).lnk");

            string? akariExe = Environment.ProcessPath;
            if (akariExe is null) { SetCompetitiveStatus("Could not resolve the Akari Tool executable path."); return; }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) { SetCompetitiveStatus("Windows Script Host is unavailable — shortcut not created."); return; }

            object? shell = Activator.CreateInstance(shellType);
            if (shell is null) { SetCompetitiveStatus("Could not create the shortcut."); return; }

            object? link = shellType.InvokeMember("CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
            if (link is null) { SetCompetitiveStatus("Could not create the shortcut."); return; }

            var linkType = link.GetType();
            void Set(string prop, string value) => linkType.InvokeMember(prop,
                System.Reflection.BindingFlags.SetProperty, null, link, new object[] { value });

            Set("TargetPath",       akariExe);
            Set("Arguments",        $"--competitive \"{path}\"");
            Set("WorkingDirectory", Path.GetDirectoryName(akariExe) ?? "");
            Set("IconLocation",     path);
            Set("Description",      $"Launch {gameName} with Akari Tool Competitive Mode");

            linkType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, link, null);

            SetCompetitiveStatus($"Shortcut created on the Desktop: {Path.GetFileName(linkPath)}");
            Service?.Log($"Competitive Mode shortcut created: {linkPath}");
        }
        catch (Exception ex)
        {
            SetCompetitiveStatus($"Could not create the shortcut: {ex.Message}");
            Service?.Log($"ERROR Create Shortcut: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    // ── Anti-cheat notice (sub-part B) ────────────────────────────────────

    private void BuildCompetitiveAntiCheatNotice(StackPanel panel)
    {
        // ⚠ glyph + Caution tint (net8 WarnFg): at 12px body size the brand crimson is
        // below AA on dark; caution amber clears AA in both themes.
        panel.Children.Add(new TextBlock
        {
            Text = "⚠  Experimental — use at your own risk. Competitive Mode temporarily suspends " +
                   "apps, stops services and changes process priorities around a game launch, then " +
                   "restores everything when the game exits. Some anti-cheats block priority and I/O " +
                   "changes. Akari Tool only uses standard Windows APIs and never modifies game memory. " +
                   "If Akari Tool is closed unexpectedly during a session, it will offer to restore your " +
                   "settings on next launch.",
            FontSize = 12,
            Foreground = Res("SystemFillColorCautionBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });
    }

    // ── Option groups (sub-part C) ────────────────────────────────────────

    private void BuildCompetitiveOptionGroups(StackPanel panel)
    {
        // ── Game Process ──────────────────────────────────────────────────
        var gameProcess = AddCompetitiveGroup(panel, "Game Process");

        _cmBoostPriority = AddCompetitiveCheck(gameProcess, "Boost Game Priority", null);
        _cmBoostPriority.Click += (_, _) => { SyncCompetitiveControlStates(); SaveCompetitiveOptions(); };

        _cmPrioritySubOptions = new StackPanel { Margin = new Thickness(26, 4, 0, 0) };
        gameProcess.Children.Add(_cmPrioritySubOptions);

        // Realtime is intentionally absent — it starves the audio and input threads.
        _cmPriorityLevel = AddCompetitiveDropdown(_cmPrioritySubOptions, "Priority Level",
            new[] { "Above Normal", "High" }, 1);
        _cmIoPriority = AddCompetitiveDropdown(_cmPrioritySubOptions, "I/O Priority",
            new[] { "Normal", "High" }, 1);
        _cmCpuSets = AddCompetitiveDropdown(_cmPrioritySubOptions, "CPU Sets",
            new[] { "All Cores" }, 0);

        _cmLaunchThroughSteam = AddCompetitiveCheck(gameProcess, "Launch through Steam when available",
            "Some games fail to authenticate when their .exe is started directly.");
        _cmLaunchThroughSteam.Click += (_, _) => { SaveCompetitiveOptions(); RefreshCompetitiveLaunchInfo(); };

        // ── Background Activity ───────────────────────────────────────────
        var background = AddCompetitiveGroup(panel, "Background Activity");

        _cmGameFocus = AddCompetitiveCheck(background, "Game Focus",
            "Suspends browsers, chat and launcher apps for the session, then resumes them. Nothing is closed.");
        _cmPauseServices = AddCompetitiveCheck(background, "Pause Non-Essential Services", null);
        _cmCloseAfterLaunch = AddCompetitiveCheck(background, "Close Akari Tool After Game Launch",
            "Hides the window; the session keeps running so your settings are restored on exit.");

        // ── System ────────────────────────────────────────────────────────
        var system = AddCompetitiveGroup(panel, "System");

        _cmConsistentPerf = AddCompetitiveCheck(system, "Consistent Performance",
            "Switches to the Ultimate/High Performance power plan and opts the game out of CPU power throttling.");
        _cmClearStandby = AddCompetitiveCheck(system, "Clear Standby Memory",
            "Frees cached memory at launch. Effect on framerate is usually negligible.");

        foreach (var cb in new[] { _cmGameFocus, _cmPauseServices, _cmCloseAfterLaunch, _cmConsistentPerf, _cmClearStandby })
            cb.Click += (_, _) => SaveCompetitiveOptions();
    }

    private StackPanel AddCompetitiveGroup(StackPanel parent, string title)
    {
        parent.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Margin = new Thickness(0, 8, 0, 6)
        });

        var group = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        parent.Children.Add(group);
        return group;
    }

    private CheckBox AddCompetitiveCheck(StackPanel parent, string label, string? description)
    {
        var content = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        content.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = Res("TextFillColorPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap
        });
        if (description is not null)
            content.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11.5,
                Foreground = Res("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 0)
            });

        var cb = new CheckBox
        {
            Margin = new Thickness(0, 5, 0, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = content
        };
        parent.Children.Add(cb);
        return cb;
    }

    private ComboBox AddCompetitiveDropdown(StackPanel parent, string label, string[] items, int defaultIndex)
    {
        var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

        var text = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var combo = new ComboBox
        {
            FontSize = 12.5,
            MinHeight = 32,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        foreach (string i in items) combo.Items.Add(i);
        combo.SelectedIndex = defaultIndex;
        combo.SelectionChanged += (_, _) => SaveCompetitiveOptions();
        Grid.SetColumn(combo, 1);
        row.Children.Add(combo);

        parent.Children.Add(row);
        return combo;
    }

    // ── Options <-> UI (sub-part C) ────────────────────────────────────────

    private void LoadCompetitiveOptionsIntoUi(CompetitiveOptions o)
    {
        if (_cmBoostPriority    is not null) _cmBoostPriority.IsChecked    = o.BoostGamePriority;
        if (_cmGameFocus        is not null) _cmGameFocus.IsChecked        = o.GameFocus;
        if (_cmPauseServices    is not null) _cmPauseServices.IsChecked    = o.PauseNonEssentialServices;
        if (_cmCloseAfterLaunch is not null) _cmCloseAfterLaunch.IsChecked = o.CloseAfterLaunch;
        if (_cmConsistentPerf   is not null) _cmConsistentPerf.IsChecked   = o.ConsistentPerformance;
        if (_cmClearStandby     is not null) _cmClearStandby.IsChecked     = o.ClearStandbyMemory;
        if (_cmLaunchThroughSteam is not null) _cmLaunchThroughSteam.IsChecked = o.LaunchThroughSteam;

        if (_cmPriorityLevel is not null) _cmPriorityLevel.SelectedIndex = o.PriorityLevel == GamePriorityLevel.High ? 1 : 0;
        if (_cmIoPriority    is not null) _cmIoPriority.SelectedIndex    = o.IoPriority    == GameIoPriority.High    ? 1 : 0;
        if (_cmCpuSets       is not null) _cmCpuSets.SelectedIndex       = 0;
    }

    private CompetitiveOptions ReadCompetitiveOptionsFromUi() => new(
        BoostGamePriority:         _cmBoostPriority?.IsChecked    == true,
        PriorityLevel:             _cmPriorityLevel?.SelectedIndex == 1 ? GamePriorityLevel.High : GamePriorityLevel.AboveNormal,
        IoPriority:                _cmIoPriority?.SelectedIndex    == 1 ? GameIoPriority.High    : GameIoPriority.Normal,
        CpuSets:                   CpuSetMode.AllCores,
        GameFocus:                 _cmGameFocus?.IsChecked        == true,
        PauseNonEssentialServices: _cmPauseServices?.IsChecked    == true,
        ConsistentPerformance:     _cmConsistentPerf?.IsChecked   == true,
        CloseAfterLaunch:          _cmCloseAfterLaunch?.IsChecked == true,
        ClearStandbyMemory:        _cmClearStandby?.IsChecked     == true,
        LaunchThroughSteam:        _cmLaunchThroughSteam?.IsChecked == true);

    private void SaveCompetitiveOptions()
    {
        try { CompetitivePrefs.SaveOptions(ReadCompetitiveOptionsFromUi()); }
        catch (Exception ex) { Service?.Log($"Competitive Mode: could not save options — {ex.Message}"); }
    }

    /// <summary>
    /// Single place that decides what is enabled. Called after every state change so the
    /// enable/disable rules cannot drift apart across handlers. (Real version, sub-part C —
    /// replaces the A stub; calls the sub-part-D RefreshCompetitiveStatus stub.)
    /// </summary>
    private void SyncCompetitiveControlStates()
    {
        bool active = CompetitiveService.IsSessionActive;
        bool locked = active || ViewModel.IsBusy;

        if (_cmPrimaryBtn is not null)
        {
            _cmPrimaryBtn.Content   = active ? "End Session"
                                      : ViewModel.IsBusy ? "Cancel"
                                      : "Start Competitive Mode";
            _cmPrimaryBtn.IsEnabled = true;
        }

        if (_cmGameCombo   is not null) _cmGameCombo.IsEnabled   = !locked;
        if (_cmBrowseBtn   is not null) _cmBrowseBtn.IsEnabled   = !locked;
        if (_cmShortcutBtn is not null) _cmShortcutBtn.IsEnabled = !locked && ViewModel.SelectedPath is not null;

        foreach (var cb in new[] { _cmBoostPriority, _cmGameFocus, _cmPauseServices,
                                   _cmCloseAfterLaunch, _cmConsistentPerf, _cmClearStandby,
                                   _cmLaunchThroughSteam })
            if (cb is not null) cb.IsEnabled = !locked;

        // The three sub-dropdowns follow their parent checkbox as well as the session lock.
        bool subs = !locked && _cmBoostPriority?.IsChecked == true;
        foreach (var combo in new[] { _cmPriorityLevel, _cmIoPriority, _cmCpuSets })
            if (combo is not null) combo.IsEnabled = subs;
        if (_cmPrioritySubOptions is not null)
            _cmPrioritySubOptions.Opacity = subs ? 1.0 : 0.5;

        RefreshCompetitiveLaunchInfo();
        RefreshCompetitiveStatus();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Status panel (sub-part D UI; the session state machine lives in the VM since E1)
    //  RefreshCompetitiveStatus is READ-ONLY — it reads CompetitiveService.CurrentState +
    //  ViewModel.ActiveSchemeName and never reactivates a scheme (the CLAUDE.md power-plan
    //  invariant; the scheme write lives only in CompetitiveService.StartAsync).
    // ══════════════════════════════════════════════════════════════════════

    private void BuildCompetitiveStatus(StackPanel panel)
    {
        panel.Children.Add(new Border
        {
            Background = Res("DividerStrokeColorDefaultBrush"),
            Height = 1,
            Margin = new Thickness(-20, 6, -20, 10)
        });

        _cmStatusHeadline = new TextBlock
        {
            FontSize = 12.5,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(_cmStatusHeadline);

        _cmStatusDetail = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        panel.Children.Add(_cmStatusDetail);
    }

    private void SetCompetitiveStatus(string text)
    {
        if (_cmStatusHeadline is not null) _cmStatusHeadline.Text = text;
    }

    /// <summary>Idle summary, or the live session readout with elapsed time. READ-ONLY —
    /// reads CurrentState + the already-resolved _cmActiveSchemeName; never writes a scheme.</summary>
    private void RefreshCompetitiveStatus()
    {
        if (_cmStatusHeadline is null || _cmStatusDetail is null) return;
        _cmStatusDetail.Children.Clear();

        var state = CompetitiveService.CurrentState;
        if (state is null)
        {
            var selected = ViewModel.SelectedPath;
            _cmStatusHeadline.Text = selected is null
                ? "Idle. Select a game to begin."
                : $"Idle. {Path.GetFileNameWithoutExtension(selected)} selected.";
            return;
        }

        var elapsed = DateTime.UtcNow - state.StartedUtc;
        _cmStatusHeadline.Text =
            $"Active — {state.GameProcessName} — {elapsed:hh\\:mm\\:ss}";

        void Bullet(string text) => _cmStatusDetail!.Children.Add(new TextBlock
        {
            Text = "•  " + text,
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 1)
        });

        if (state.SuspendedProcesses.Count > 0)
            Bullet($"{state.SuspendedProcesses.Count} background apps suspended");

        if (state.PreviousPowerSchemeGuid is not null)
            Bullet($"Power plan: {ViewModel.ActiveSchemeName ?? "performance plan"}");

        foreach (var svc in state.StoppedServices)
            Bullet($"{FriendlyServiceName(svc.Name)} paused");

        if (state.TuningFailures.Count > 0)
            Bullet("Some tuning was blocked by anti-cheat.");
    }

    private static string FriendlyServiceName(string name) => name switch
    {
        "WSearch" => "Windows Search",
        "SysMain" => "SysMain (Superfetch)",
        _         => name,
    };

    private void StartCompetitiveTimer()
    {
        _cmElapsedTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cmElapsedTimer.Tick -= OnCompetitiveTimerTick;
        _cmElapsedTimer.Tick += OnCompetitiveTimerTick;
        _cmElapsedTimer.Start();
    }

    // WinUI DispatcherTimer.Tick is EventHandler<object> (WPF used EventHandler).
    private void OnCompetitiveTimerTick(object? sender, object e) => RefreshCompetitiveStatus();

    private void StopCompetitiveTimer() => _cmElapsedTimer?.Stop();
}
