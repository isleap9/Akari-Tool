using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.AkariOS
{
    public partial class AkariOSTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // COMPETITIVE MODE
        // An ACTION section — nothing here is a TweakDefinition, nothing is
        // registered with TweakRegistry, and it is excluded from Quick Actions and
        // drift verification. Every change it makes is session-scoped and undone on
        // exit, so there is no steady state for drift to compare against.
        // ══════════════════════════════════════════════════════════════════════

        // Game picker
        private ComboBox? _cmGameCombo;
        private Button?   _cmBrowseBtn;
        private Button?   _cmShortcutBtn;
        private Button?   _cmPrimaryBtn;
        private TextBlock? _cmPickerHint;
        private TextBlock? _cmLaunchInfo;
        private readonly List<DetectedGame> _cmGames = new();
        private string? _cmSelectedPath;

        // Options
        private CheckBox? _cmBoostPriority, _cmGameFocus, _cmPauseServices,
                          _cmCloseAfterLaunch, _cmConsistentPerf, _cmClearStandby,
                          _cmLaunchThroughSteam;
        private ComboBox? _cmPriorityLevel, _cmIoPriority, _cmCpuSets;
        private StackPanel? _cmPrioritySubOptions;

        // Status
        private TextBlock? _cmStatusHeadline;
        private StackPanel? _cmStatusDetail;
        private DispatcherTimer? _cmElapsedTimer;

        private bool _cmBusy;

        // ── Build ─────────────────────────────────────────────────────────────

        private void BuildCompetitiveContent(StackPanel panel)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Applies a set of temporary, session-scoped tweaks around a single game launch " +
                       "and undoes all of them when the game exits.",
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            BuildCompetitivePicker(panel);
            BuildCompetitiveAntiCheatNotice(panel);
            BuildCompetitiveOptionGroups(panel);
            BuildCompetitiveStatus(panel);

            LoadCompetitiveOptionsIntoUi(CompetitivePrefs.LoadOptions());
            SyncCompetitiveControlStates();

            // Detection walks every .exe under every Steam library, which on a large
            // install is seconds of I/O — far too slow for the constructor, which
            // runs inside the splash's MainWindow build stage.
            if (IsLoaded) BeginCompetitiveGameDetection();
            else          Loaded += OnCompetitiveLoaded;

            // The watcher ends the session on a background thread; the UI has to be
            // told so the button flips back from "End Session" on its own.
            CompetitiveService.SessionEndedByGameExit += OnCompetitiveSessionEndedExternally;
            Unloaded += (_, _) =>
                CompetitiveService.SessionEndedByGameExit -= OnCompetitiveSessionEndedExternally;
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
                MinHeight = 36,   // MinHeight not Height — see AddCompetitiveDropdown
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            _cmGameCombo.SelectionChanged += (_, _) =>
            {
                if (_cmGameCombo!.SelectedIndex >= 0 && _cmGameCombo.SelectedIndex < _cmGames.Count)
                {
                    _cmSelectedPath = _cmGames[_cmGameCombo.SelectedIndex].ExePath;
                    CompetitivePrefs.SaveLastGamePath(_cmSelectedPath);
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
                () => _ = OnCompetitivePrimaryClickAsync());
            Grid.SetColumn(_cmPrimaryBtn, 3);
            row.Children.Add(_cmPrimaryBtn);

            panel.Children.Add(row);

            _cmPickerHint = new TextBlock
            {
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            panel.Children.Add(_cmPickerHint);

            // Which exe detection resolved to, and how it will be started. This is
            // the user's only window into a detection or launch-method mistake.
            _cmLaunchInfo = new TextBlock
            {
                FontSize = 11.5,
                Foreground = TweakHelpers.TextMuted,
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

            if (_cmSelectedPath is null)
            {
                _cmLaunchInfo.Visibility = Visibility.Collapsed;
                return;
            }

            string method;
            try
            {
                var plan = CompetitiveService.ResolveLaunch(_cmSelectedPath, ReadCompetitiveOptionsFromUi());
                method = plan.ViaSteam ? $"via Steam (AppID {plan.AppId})" : "direct launch";
            }
            catch { method = "direct launch"; }

            _cmLaunchInfo.Text = $"{Path.GetFileName(_cmSelectedPath)} — {method}";
            _cmLaunchInfo.Visibility = Visibility.Visible;
        }

        private Button MakeCompetitiveButton(string label, string style, Action onClick)
        {
            var btn = new Button
            {
                Content = label,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(16, 10, 16, 10),
                FontSize = 13,
                Style = (Style)FindResource(style),
            };
            btn.Click += (_, _) =>
            {
                try { onClick(); }
                catch (Exception ex) { SetCompetitiveStatus($"Error: {ex.Message}"); Service?.Log($"ERROR {label}: {ex.Message}"); }
            };
            return btn;
        }

        private bool _cmDetectionStarted;

        private void OnCompetitiveLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnCompetitiveLoaded;
            BeginCompetitiveGameDetection();
        }

        /// <summary>
        /// Runs Steam detection off the UI thread, then populates the picker.
        /// Until it lands the combo is empty and the hint reads "Detecting games…".
        /// </summary>
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

                return Dispatcher.BeginInvoke(() => PopulateCompetitiveGames(found));
            });
        }

        private void PopulateCompetitiveGames(IReadOnlyList<DetectedGame> detected)
        {
            _cmGames.Clear();
            _cmGames.AddRange(detected);

            // A previously browsed exe that detection does not know about is added so
            // the persisted choice survives a restart.
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
                if (i >= 0) { _cmGameCombo.SelectedIndex = i; _cmSelectedPath = _cmGames[i].ExePath; }
            }

            // Create Shortcut and the status line both depend on whether anything
            // ended up selected.
            SyncCompetitiveControlStates();
        }

        private void BrowseForGame()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select a game executable",
                Filter = "Executables (*.exe)|*.exe",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog() != true) return;

            string path = dlg.FileName;
            string name = Path.GetFileNameWithoutExtension(path);

            int existing = _cmGames.FindIndex(g => g.ExePath.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existing < 0)
            {
                _cmGames.Insert(0, new DetectedGame(name, path));
                _cmGameCombo?.Items.Insert(0, name);
                existing = 0;
            }

            if (_cmGameCombo is not null) _cmGameCombo.SelectedIndex = existing;
            _cmSelectedPath = path;
            CompetitivePrefs.SaveLastGamePath(path);

            if (_cmPickerHint is not null) _cmPickerHint.Visibility = Visibility.Collapsed;
            SyncCompetitiveControlStates();
        }

        // ── Anti-cheat notice ─────────────────────────────────────────────────

        private void BuildCompetitiveAntiCheatNotice(StackPanel panel)
        {
            // ⚠ glyph + WarnFg, not the brand accent: at 12px this is body-size
            // text, and the crimson only reaches 3.34:1 on the dark canvas —
            // below AA. WarnFg clears AA in both themes.
            panel.Children.Add(new TextBlock
            {
                Text = "⚠  Experimental — use at your own risk. Competitive Mode temporarily suspends " +
                       "apps, stops services and changes process priorities around a game launch, then " +
                       "restores everything when the game exits. Some anti-cheats block priority and I/O " +
                       "changes. Akari Tool only uses standard Windows APIs and never modifies game memory. " +
                       "If Akari Tool is closed unexpectedly during a session, it will offer to restore your " +
                       "settings on next launch.",
                FontSize = 12,
                Foreground = TweakHelpers.WarnFg,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            });
        }

        // ── One-time experimental disclaimer ──────────────────────────────────

        private const string DisclaimerPrefKey = @"HKEY_CURRENT_USER\Software\AkariTool";
        private const string DisclaimerPrefName = "CompetitiveDisclaimerAccepted";

        private static bool CompetitiveDisclaimerAccepted()
        {
            try { return Registry.GetValue(DisclaimerPrefKey, DisclaimerPrefName, null) is int i && i != 0; }
            catch { return false; }
        }

        /// <summary>
        /// Shows the experimental disclaimer on first use only. True when the user
        /// has accepted (now or previously); false means abort with nothing changed.
        ///
        /// Awaited directly rather than via AkariDialogs, which pumps a nested
        /// dispatcher frame — re-entering the UI thread from an async handler that is
        /// itself suspended. Owner must be set or the box opens unparented.
        /// </summary>
        private async Task<bool> ConfirmCompetitiveDisclaimerAsync()
        {
            if (CompetitiveDisclaimerAccepted()) return true;

            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Competitive Mode is experimental",
                Content = new TextBlock
                {
                    Text = "This feature suspends background apps, stops Windows services and changes " +
                           "the power plan for the duration of your game session, then restores them " +
                           "when the game exits.\n\n" +
                           "Most issues are recoverable — if something is left in a bad state, restarting " +
                           "your PC restores all of it. But this is new and not yet widely tested, so use " +
                           "it at your own risk.\n\n" +
                           "Continue?",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                PrimaryButtonText = "I understand, continue",
                CloseButtonText = "Cancel",
            };

            var owner = Window.GetWindow(this);
            if (owner is not null && owner.IsVisible) box.Owner = owner;

            if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
                return false;

            try { Registry.SetValue(DisclaimerPrefKey, DisclaimerPrefName, 1, RegistryValueKind.DWord); }
            catch { /* a lost flag only means asking again — never block the start */ }

            return true;
        }

        // ── Option groups ─────────────────────────────────────────────────────

        private void BuildCompetitiveOptionGroups(StackPanel panel)
        {
            // ── Game Process ──────────────────────────────────────────────────
            var gameProcess = AddCompetitiveGroup(panel, "Game Process");

            _cmBoostPriority = AddCompetitiveCheck(gameProcess, "Boost Game Priority", null);
            _cmBoostPriority.Click += (_, _) => { SyncCompetitiveControlStates(); SaveCompetitiveOptions(); };

            _cmPrioritySubOptions = new StackPanel { Margin = new Thickness(26, 4, 0, 0) };
            gameProcess.Children.Add(_cmPrioritySubOptions);

            // Realtime is intentionally absent — it starves the audio and input
            // threads and makes the machine feel worse, not better.
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
                Foreground = TweakHelpers.TextPrimary,
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
                Foreground = TweakHelpers.TextPrimary,
                TextWrapping = TextWrapping.Wrap
            });
            if (description is not null)
                content.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 11.5,
                    Foreground = TweakHelpers.TextSecondary,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 1, 0, 0)
                });

            var cb = new CheckBox
            {
                Style = (Style)Application.Current.Resources["AppCheckBox"],
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
                Foreground = TweakHelpers.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(text, 0);
            row.Children.Add(text);

            // MinHeight, never Height: the WPF-UI implicit ComboBox style adds its own
            // padding on top of the text, so a hardcoded Height clips the descenders
            // of the closed-state display area. Every other ComboBox in the app lets
            // the control size to content — this matches them.
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

        // ── Status area ───────────────────────────────────────────────────────

        private void BuildCompetitiveStatus(StackPanel panel)
        {
            panel.Children.Add(new Separator
            {
                Background = TweakHelpers.Token("AkariOverlayStrong"),
                Height = 1,
                Margin = new Thickness(-20, 6, -20, 10)
            });

            _cmStatusHeadline = new TextBlock
            {
                FontSize = 12.5,
                Foreground = TweakHelpers.TextSecondary,
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

        /// <summary>Idle summary, or the live session readout with elapsed time.</summary>
        private void RefreshCompetitiveStatus()
        {
            if (_cmStatusHeadline is null || _cmStatusDetail is null) return;
            _cmStatusDetail.Children.Clear();

            var state = CompetitiveService.CurrentState;
            if (state is null)
            {
                _cmStatusHeadline.Text = _cmSelectedPath is null
                    ? "Idle. Select a game to begin."
                    : $"Idle. {Path.GetFileNameWithoutExtension(_cmSelectedPath)} selected.";
                return;
            }

            var elapsed = DateTime.UtcNow - state.StartedUtc;
            _cmStatusHeadline.Text =
                $"Active — {state.GameProcessName} — {elapsed:hh\\:mm\\:ss}";

            void Bullet(string text) => _cmStatusDetail!.Children.Add(new TextBlock
            {
                Text = "•  " + text,
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 1)
            });

            if (state.SuspendedProcesses.Count > 0)
                Bullet($"{state.SuspendedProcesses.Count} background apps suspended");

            if (state.PreviousPowerSchemeGuid is not null)
                Bullet($"Power plan: {_cmActiveSchemeName ?? "performance plan"}");

            foreach (var svc in state.StoppedServices)
                Bullet($"{FriendlyServiceName(svc.Name)} paused");

            if (state.TuningFailures.Count > 0)
                Bullet("Some tuning was blocked by anti-cheat.");
        }

        private string? _cmActiveSchemeName;

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

        private void OnCompetitiveTimerTick(object? sender, EventArgs e) => RefreshCompetitiveStatus();

        private void StopCompetitiveTimer() => _cmElapsedTimer?.Stop();

        // ── Options <-> UI ────────────────────────────────────────────────────

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
        /// Single place that decides what is enabled. Called after every state
        /// change so the enable/disable rules cannot drift apart across handlers.
        /// </summary>
        private void SyncCompetitiveControlStates()
        {
            bool active = CompetitiveService.IsSessionActive;
            bool locked = active || _cmBusy;

            if (_cmPrimaryBtn is not null)
            {
                _cmPrimaryBtn.Content   = active ? "End Session" : "Start Competitive Mode";
                _cmPrimaryBtn.IsEnabled = !_cmBusy;
            }

            if (_cmGameCombo   is not null) _cmGameCombo.IsEnabled   = !locked;
            if (_cmBrowseBtn   is not null) _cmBrowseBtn.IsEnabled   = !locked;
            if (_cmShortcutBtn is not null) _cmShortcutBtn.IsEnabled = !locked && _cmSelectedPath is not null;

            foreach (var cb in new[] { _cmBoostPriority, _cmGameFocus, _cmPauseServices,
                                       _cmCloseAfterLaunch, _cmConsistentPerf, _cmClearStandby,
                                       _cmLaunchThroughSteam })
                if (cb is not null) cb.IsEnabled = !locked;

            // The three sub-dropdowns follow their parent checkbox as well as the
            // session lock.
            bool subs = !locked && _cmBoostPriority?.IsChecked == true;
            foreach (var combo in new[] { _cmPriorityLevel, _cmIoPriority, _cmCpuSets })
                if (combo is not null) combo.IsEnabled = subs;
            if (_cmPrioritySubOptions is not null)
                _cmPrioritySubOptions.Opacity = subs ? 1.0 : 0.5;

            RefreshCompetitiveLaunchInfo();
            RefreshCompetitiveStatus();
        }

        // ── Start / End ───────────────────────────────────────────────────────

        private async Task OnCompetitivePrimaryClickAsync()
        {
            if (CompetitiveService.IsSessionActive) { await EndCompetitiveSessionAsync(); return; }
            await StartCompetitiveSessionAsync();
        }

        private async Task StartCompetitiveSessionAsync()
        {
            string? path = _cmSelectedPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                SetCompetitiveStatus("Select a game before starting.");
                return;
            }
            if (!File.Exists(path))
            {
                SetCompetitiveStatus($"That executable no longer exists: {path}");
                return;
            }

            // One-time experimental disclaimer, before ANY mutation — including the
            // power plan switch, which is the first thing StartAsync touches.
            if (!await ConfirmCompetitiveDisclaimerAsync())
            {
                SetCompetitiveStatus("Cancelled — nothing was changed.");
                SyncCompetitiveControlStates();
                return;
            }

            _cmBusy = true;
            SyncCompetitiveControlStates();

            try
            {
                var options = ReadCompetitiveOptionsFromUi();
                CompetitivePrefs.SaveOptions(options);

                var progress = new Progress<string>(SetCompetitiveStatus);
                var result = await CompetitiveService.StartAsync(path, options, progress, CancellationToken.None);

                if (!result.Started)
                {
                    // StartAsync has already restored everything it applied.
                    HandleCompetitiveStartFailure(result);
                    return;
                }

                var state = result.State!;
                _cmActiveSchemeName = state.PreviousPowerSchemeGuid is null
                    ? null
                    : await Task.Run(() => CompetitiveService.DescribeScheme(GetActiveSchemeForDisplay()));

                Service?.Log($"Competitive Mode started for {state.GameProcessName}.");
                StartCompetitiveTimer();

                if (options.CloseAfterLaunch) ScheduleHideForCompetitive(state.GameProcessName);
            }
            catch (Exception ex)
            {
                SetCompetitiveStatus($"Could not start: {ex.Message}");
                Service?.Log($"ERROR Competitive Mode start: {ex.Message}");
            }
            finally
            {
                // finally, so a throw above cannot leave the section permanently
                // disabled with no way back.
                _cmBusy = false;
                SyncCompetitiveControlStates();
            }
        }

        /// <summary>
        /// Reports a start that never reached a running game. Nothing needs undoing
        /// here — StartAsync guarantees it has already rolled back — so this only
        /// has to explain what happened and leave the section idle.
        /// </summary>
        private void HandleCompetitiveStartFailure(CompetitiveStartResult result)
        {
            _cmActiveSchemeName = null;
            StopCompetitiveTimer();

            string message = result.Outcome switch
            {
                CompetitiveStartOutcome.GameNotFound =>
                    "The game did not start. Nothing was left changed — all settings have been " +
                    "restored. If you picked the wrong executable, use Browse to select the " +
                    "game's main .exe.",

                CompetitiveStartOutcome.Cancelled =>
                    "Cancelled before the game started. All settings have been restored.",

                _ =>
                    $"Could not start: {result.Error}. All settings have been restored.",
            };

            SetCompetitiveStatus(message);
            Service?.Log($"Competitive Mode start failed ({result.Outcome}) — settings restored.");
        }

        private static string? GetActiveSchemeForDisplay()
        {
            var (_, guid) = SystemStateReader.ReadActivePowerPlan();
            return guid;
        }

        private async Task EndCompetitiveSessionAsync()
        {
            var state = CompetitiveService.CurrentState;
            if (state is null) { SyncCompetitiveControlStates(); return; }

            _cmBusy = true;
            SyncCompetitiveControlStates();

            try
            {
                var progress = new Progress<string>(SetCompetitiveStatus);
                await CompetitiveService.EndAsync(state, progress);
                Service?.Log("Competitive Mode ended — settings restored.");
            }
            catch (Exception ex)
            {
                SetCompetitiveStatus($"Restore reported a problem: {ex.Message}");
                Service?.Log($"ERROR Competitive Mode end: {ex.Message}");
            }
            finally
            {
                StopCompetitiveTimer();
                _cmActiveSchemeName = null;
                RestoreMainWindowAfterCompetitive();
                _cmBusy = false;
                SyncCompetitiveControlStates();
            }
        }

        /// <summary>Watcher-driven end — marshal to the dispatcher before touching UI.</summary>
        private void OnCompetitiveSessionEndedExternally()
        {
            Dispatcher.BeginInvoke(() =>
            {
                StopCompetitiveTimer();
                _cmActiveSchemeName = null;
                RestoreMainWindowAfterCompetitive();
                SetCompetitiveStatus("Game exited — settings restored.");
                SyncCompetitiveControlStates();
            });
        }

        // ── Close-after-launch window handling ────────────────────────────────

        /// <summary>
        /// Hides the window ~10s after the game is first seen. Never exits the app:
        /// the session watcher lives in this process, so exiting would strand every
        /// suspended app and stopped service.
        /// </summary>
        private void ScheduleHideForCompetitive(string processName)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait for the game to actually appear before starting the 10s clock.
                    for (int i = 0; i < 60; i++)
                    {
                        if (System.Diagnostics.Process.GetProcessesByName(processName).Length > 0) break;
                        await Task.Delay(1000);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(10));

                    await Dispatcher.BeginInvoke(() =>
                    {
                        var w = Window.GetWindow(this);
                        if (w is not null && CompetitiveService.IsSessionActive) w.Hide();
                    });
                }
                catch { /* hiding is a convenience — never let it surface */ }
            });
        }

        private void RestoreMainWindowAfterCompetitive()
        {
            try
            {
                var w = Window.GetWindow(this);
                if (w is null) return;
                if (!w.IsVisible) w.Show();
                if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                w.Activate();
            }
            catch { }
        }

        // ── Create Shortcut ───────────────────────────────────────────────────

        /// <summary>
        /// Writes a Desktop .lnk that re-launches Akari Tool in --competitive mode.
        /// Late-bound WScript.Shell keeps this free of an IWshRuntimeLibrary interop
        /// reference.
        /// </summary>
        private void CreateCompetitiveShortcut()
        {
            string? path = _cmSelectedPath;
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

        // ── Entry point for the --competitive command line ────────────────────

        /// <summary>
        /// Starts a session from a Desktop shortcut, using the persisted options and
        /// honouring CloseAfterLaunch as if it were checked.
        /// </summary>
        public async Task StartCompetitiveFromCommandLineAsync(string exePath)
        {
            if (!File.Exists(exePath))
            {
                SetCompetitiveStatus($"That executable no longer exists: {exePath}");
                return;
            }

            _cmSelectedPath = exePath;
            CompetitivePrefs.SaveLastGamePath(exePath);
            BeginCompetitiveGameDetection();

            var stored = CompetitivePrefs.LoadOptions();
            var options = stored with { CloseAfterLaunch = true };
            LoadCompetitiveOptionsIntoUi(options);

            // The shortcut path is a first use like any other — the disclaimer gates
            // it too, before anything is touched.
            if (!await ConfirmCompetitiveDisclaimerAsync())
            {
                SetCompetitiveStatus("Cancelled — nothing was changed.");
                SyncCompetitiveControlStates();
                return;
            }

            _cmBusy = true;
            SyncCompetitiveControlStates();
            try
            {
                var progress = new Progress<string>(SetCompetitiveStatus);
                var result = await CompetitiveService.StartAsync(exePath, options, progress, CancellationToken.None);

                if (!result.Started)
                {
                    HandleCompetitiveStartFailure(result);
                    return;
                }

                var state = result.State!;
                Service?.Log($"Competitive Mode started from shortcut for {state.GameProcessName}.");
                StartCompetitiveTimer();
                ScheduleHideForCompetitive(state.GameProcessName);
            }
            catch (Exception ex)
            {
                SetCompetitiveStatus($"Could not start: {ex.Message}");
                Service?.Log($"ERROR Competitive Mode (shortcut) start: {ex.Message}");
            }
            finally
            {
                _cmBusy = false;
                SyncCompetitiveControlStates();
            }
        }

        /// <summary>Used by MainWindow's Closing handler.</summary>
        public Task EndCompetitiveSessionForShutdownAsync() => EndCompetitiveSessionAsync();
    }
}
