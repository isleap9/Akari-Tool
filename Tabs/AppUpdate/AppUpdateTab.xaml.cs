using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using AkariTool.Services;

namespace AkariTool.Tabs.AppUpdate
{
    /// <summary>
    /// App self-updater page (distinct from the Windows-update page under Optimize).
    ///
    /// Flow: "Check for Updates" queries GitHub Releases. If a newer version with an
    /// AkariTool-Setup-*.exe asset exists, "Update Now" downloads it to %TEMP% (with
    /// progress), launches it silently with /RELAUNCH=1 and exits — Inno Setup
    /// upgrades in place and relaunches the app. Fully seamless, one click.
    ///
    /// The "What's new" card is populated live from GitHub release notes; the static
    /// array below is only the offline fallback.
    /// </summary>
    public partial class AppUpdateTab : BaseTab
    {
        private TextBlock _headline = null!;
        private TextBlock _subline = null!;
        private Path _stateIcon = null!;
        private Border _chip = null!;
        private Button _checkBtn = null!;
        private Button _updateBtn = null!;
        private StackPanel _changelogStack = null!;
        private RotateTransform _spin = null!;
        private Storyboard? _spinSb;
        private bool _busy;
        private UpdateCheckResult? _lastResult;

        // Offline fallback only — the real changelog comes from GitHub releases.
        private static readonly (string V, string D, bool Current)[] FallbackChangelog =
        {
            ("v2.0", "New collapsible sidebar with grouped sections, About & Update pages, " +
                     "and the Advanced Tools custom-ISO builder.", true),
            ("v1.5", "Added the Advanced Tools tab: WIM utility and autounattend.xml generator.", false),
            ("v1.0", "Initial release — debloat, gaming, privacy, power and customization tweaks.", false),
        };

        public AppUpdateTab() => InitializeComponent();

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
            _ = LoadChangelogAsync();   // fire-and-forget; falls back silently
        }

        private void Build()
        {
            RootPanel.MaxWidth = 720;
            RootPanel.HorizontalAlignment = HorizontalAlignment.Center;

            RootPanel.Children.Add(new TextBlock
            {
                Text = "Update",
                FontFamily = MonoFont, FontSize = 10,
                Foreground = TweakHelpers.TextMuted,
                Margin = new Thickness(0, 4, 0, 0)
            });
            RootPanel.Children.Add(new TextBlock
            {
                Text = "Updates",
                FontFamily = DisplayFont, FontSize = 24, FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary, Margin = new Thickness(0, 6, 0, 0)
            });
            RootPanel.Children.Add(new TextBlock
            {
                Text = "Keep Akari Tool up to date with the latest tweaks and fixes.",
                FontSize = 12.5, Foreground = TweakHelpers.TextSecondary,
                Margin = new Thickness(0, 6, 0, 0)
            });

            RootPanel.Children.Add(StatusCard());

            RootPanel.Children.Add(new TextBlock
            {
                Text = "What's new",
                FontFamily = MonoFont, FontSize = 10,
                Foreground = TweakHelpers.TextMuted,
                Margin = new Thickness(0, 20, 0, 8)
            });
            RootPanel.Children.Add(ChangelogCard());
        }

        // ── Status card ─────────────────────────────────────────────────────────
        private Border StatusCard()
        {
            var card = new Border
            {
                Margin = new Thickness(0, 16, 0, 0),
                Padding = new Thickness(22, 20, 22, 20),
                CornerRadius = TweakHelpers.CardRadius,
                Background = TweakHelpers.CardBg,
                BorderBrush = TweakHelpers.CardElevationBorder,
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _chip = new Border
            {
                Width = 46, Height = 46,
                Margin = new Thickness(0, 0, 16, 0),
                CornerRadius = TweakHelpers.CardRadius,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            _spin = new RotateTransform(0);
            _stateIcon = new Path
            {
                StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _spin
            };
            _chip.Child = _stateIcon;
            Grid.SetColumn(_chip, 0);

            var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _headline = new TextBlock
            {
                FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary
            };
            _subline = new TextBlock
            {
                FontSize = 12.5, Foreground = TweakHelpers.TextSecondary,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            textCol.Children.Add(_headline);
            textCol.Children.Add(_subline);
            Grid.SetColumn(textCol, 1);

            var btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            _updateBtn = new Button
            {
                Content = "Update Now",
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 8, 0),
                Style = (Style)Application.Current.Resources["AppButton"],
                Padding = new Thickness(18, 0, 18, 0)
            };
            _updateBtn.Click += UpdateNow;
            _checkBtn = new Button
            {
                Content = "Check for Updates",
                Style = (Style)Application.Current.Resources["AppButton"],
                Padding = new Thickness(18, 0, 18, 0)
            };
            _checkBtn.Click += CheckForUpdates;
            btnRow.Children.Add(_updateBtn);
            btnRow.Children.Add(_checkBtn);
            Grid.SetColumn(btnRow, 2);

            grid.Children.Add(_chip);
            grid.Children.Add(textCol);
            grid.Children.Add(btnRow);
            card.Child = grid;

            SetChipColor(Green);
            _stateIcon.Data = CheckGeometry();
            _headline.Text = "You're up to date";
            SetSubline("click Check for Updates to query GitHub");
            return card;
        }

        private void SetSubline(string tail) =>
            _subline.Text = $"Akari Tool {UpdateService.CurrentVersionDisplay} · {tail}";

        // ── Check ───────────────────────────────────────────────────────────────
        private async void CheckForUpdates(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            _busy = true;
            _checkBtn.IsEnabled = false;
            _updateBtn.Visibility = Visibility.Collapsed;
            _lastResult = null;

            SetChipColor(Neutral);
            _stateIcon.Data = SpinnerGeometry();
            _headline.Text = "Checking for updates…";
            SetSubline("contacting github.com");
            StartSpin();

            var result = await UpdateService.CheckAsync();
            _lastResult = result;

            StopSpin();
            switch (result.Status)
            {
                case UpdateStatus.UpdateAvailable:
                    SetChipColor(Crimson);
                    _stateIcon.Data = DownloadGeometry();
                    _headline.Text = $"Update available — {result.LatestTag}";
                    if (result.InstallerUrl != null)
                    {
                        SetSubline("one click to download and install");
                        _updateBtn.Content = "Update Now";
                    }
                    else
                    {
                        SetSubline("no installer attached — opens the release page");
                        _updateBtn.Content = "View Release";
                    }
                    _updateBtn.Visibility = Visibility.Visible;
                    break;

                case UpdateStatus.UpToDate:
                    SetChipColor(Green);
                    _stateIcon.Data = CheckGeometry();
                    _headline.Text = "You're on the latest version";
                    SetSubline($"latest release is {result.LatestTag} · checked just now");
                    break;

                case UpdateStatus.NoReleases:
                    SetChipColor(Green);
                    _stateIcon.Data = CheckGeometry();
                    _headline.Text = "You're on the latest version";
                    SetSubline("no releases published on GitHub yet");
                    break;

                default: // Error
                    SetChipColor(Amber);
                    _stateIcon.Data = WarnGeometry();
                    _headline.Text = "Couldn't check for updates";
                    SetSubline(result.ErrorMessage ?? "network error — try again later");
                    break;
            }

            _checkBtn.IsEnabled = true;
            _busy = false;
        }

        // ── Seamless update: download → silent install → relaunch ───────────────
        private async void UpdateNow(object sender, RoutedEventArgs e)
        {
            if (_busy || _lastResult is null) return;

            // No installer asset → just open the release page.
            if (_lastResult.InstallerUrl is null)
            {
                OpenUrl(_lastResult.ReleasePageUrl ?? UpdateService.ReleasesPageUrl);
                return;
            }

            _busy = true;
            _updateBtn.IsEnabled = false;
            _checkBtn.IsEnabled = false;

            SetChipColor(Neutral);
            _stateIcon.Data = SpinnerGeometry();
            _headline.Text = $"Downloading {_lastResult.LatestTag}…";
            SetSubline("starting download");
            StartSpin();

            try
            {
                var progress = new Progress<double>(p =>
                    SetSubline($"downloading installer — {p:P0}"));
                string setupPath = await UpdateService.DownloadInstallerAsync(
                    _lastResult.InstallerUrl, progress);

                StopSpin();
                _stateIcon.Data = DownloadGeometry();
                _headline.Text = "Installing update…";
                SetSubline("Akari Tool will restart automatically");

                // Silent in-place upgrade; /RELAUNCH=1 makes the installer start the
                // app again when done (see [Run] in AkariTool.iss). CloseApplications
                // in the .iss handles shutting us down, but exiting proactively is
                // cleaner than being killed by Restart Manager.
                Process.Start(new ProcessStartInfo(setupPath)
                {
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1",
                    UseShellExecute = true
                });

                await Task.Delay(800);           // let the installer process spin up
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                StopSpin();
                SetChipColor(Amber);
                _stateIcon.Data = WarnGeometry();
                _headline.Text = "Update failed";
                SetSubline(ex.Message);
                _updateBtn.IsEnabled = true;
                _checkBtn.IsEnabled = true;
                _busy = false;
            }
        }

        // ── chip palettes: token keys (bg, border, stroke) — live-theme per mode ───
        private static readonly (string Bg, string Bd, string Fg) Green   = ("AkariSuccessBgColor", "AkariSuccessBorderColor", "AkariSuccessFgColor");
        private static readonly (string Bg, string Bd, string Fg) Crimson = ("AkariDangerBgColor", "AkariDangerBorderColor", "AkariDangerFgColor");
        private static readonly (string Bg, string Bd, string Fg) Amber   = ("AkariWarnBgColor", "AkariWarnBorderColor", "AkariWarnFgColor");
        private static readonly (string Bg, string Bd, string Fg) Neutral = ("AkariOverlayMedium", "AkariOverlayStrong", "AkariTextSecondaryColor");

        private void SetChipColor((string Bg, string Bd, string Fg) p)
        {
            _chip.Background = Services.ThemeService.ManagedBrush(p.Bg);
            _chip.BorderBrush = Services.ThemeService.ManagedBrush(p.Bd);
            _stateIcon.Stroke = Services.ThemeService.ManagedBrush(p.Fg);
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* browser launch failed — nothing sensible to do */ }
        }

        private void StartSpin()
        {
            var anim = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.1)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            _spinSb = new Storyboard();
            _spinSb.Children.Add(anim);
            Storyboard.SetTarget(anim, _stateIcon);
            Storyboard.SetTargetProperty(anim,
                new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _spinSb.Begin();
        }

        private void StopSpin()
        {
            _spinSb?.Stop();
            _spinSb = null;
            _spin.Angle = 0;
        }

        // ── Changelog (live from GitHub, static fallback) ───────────────────────
        private Border ChangelogCard()
        {
            var card = new Border
            {
                Padding = new Thickness(20, 4, 20, 4),
                CornerRadius = TweakHelpers.CardRadius,
                Background = TweakHelpers.CardBg,
                BorderBrush = TweakHelpers.CardElevationBorder,
                BorderThickness = new Thickness(1)
            };
            _changelogStack = new StackPanel();
            foreach (var (v, d, cur) in FallbackChangelog)
                _changelogStack.Children.Add(ChangelogRow(v, d, cur, _changelogStack.Children.Count > 0));
            card.Child = _changelogStack;
            return card;
        }

        private async Task LoadChangelogAsync()
        {
            var releases = await UpdateService.GetReleasesAsync();
            if (releases is null) return;   // offline / rate-limited → keep fallback

            _changelogStack.Children.Clear();
            foreach (var r in releases)
            {
                string body = string.IsNullOrWhiteSpace(r.Body)
                    ? (string.IsNullOrWhiteSpace(r.Name) ? "No release notes." : r.Name)
                    : CleanMarkdown(r.Body);
                _changelogStack.Children.Add(
                    ChangelogRow(ShortTag(r.Tag), body, r.IsCurrent, _changelogStack.Children.Count > 0));
            }
        }

        private static string ShortTag(string tag)
        {
            // "v2.1.0" → "v2.1" for the narrow version column (drop trailing .0 only)
            return tag.EndsWith(".0") && tag.Count(c => c == '.') == 2 ? tag[..^2] : tag;
        }

        /// <summary>Very light markdown → plain text for TextBlock display.</summary>
        private static string CleanMarkdown(string md)
        {
            var lines = md.Replace("\r\n", "\n").Split('\n');
            var sb = new System.Text.StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (line.Length == 0) continue;
                line = line.TrimStart('#', ' ');                 // headers
                if (line.StartsWith("- ") || line.StartsWith("* "))
                    line = "•" + line[1..];                       // bullets
                line = line.Replace("**", "").Replace("`", "");   // bold/code markers
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(line);
            }
            return sb.Length > 0 ? sb.ToString() : md;
        }

        private Grid ChangelogRow(string version, string desc, bool current, bool withDivider)
        {
            var row = new Grid { Margin = new Thickness(0, 14, 0, 14) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (withDivider)
            {
                var sep = new Border
                {
                    Height = 1, Background = TweakHelpers.Token("AkariOverlayMedium"),
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -14, 0, 0)
                };
                Grid.SetColumnSpan(sep, 2);
                row.Children.Add(sep);
            }

            var ver = new TextBlock
            {
                Text = version, FontFamily = MonoFont, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.AccentTextMuted, VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(ver, 0);
            row.Children.Add(ver);

            var col = new StackPanel();
            Grid.SetColumn(col, 1);
            if (current)
            {
                col.Children.Add(new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 4),
                    Padding = new Thickness(9, 2, 9, 2),
                    CornerRadius = TweakHelpers.ControlRadius,
                    Background = TweakHelpers.SuccessBg,
                    BorderBrush = TweakHelpers.SuccessBorder,
                    BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = "CURRENT", FontFamily = MonoFont, FontSize = 9,
                        Foreground = TweakHelpers.SuccessFg
                    }
                });
            }
            col.Children.Add(new TextBlock
            {
                Text = desc, FontSize = 12.5, Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap, LineHeight = 19
            });
            row.Children.Add(col);
            return row;
        }

        // ── Icon geometries (Lucide-style, 24x24 viewbox, drawn centred) ────────
        private static Geometry CheckGeometry()    => Geometry.Parse("M20 6 9 17l-5-5");
        private static Geometry SpinnerGeometry()  => Geometry.Parse("M21 12a9 9 0 1 1-6.2-8.6");
        private static Geometry DownloadGeometry() => Geometry.Parse("M12 3v12m0 0 5-5m-5 5-5-5M4 21h16");
        private static Geometry WarnGeometry()     => Geometry.Parse("M12 4 2 20h20L12 4Zm0 6v5m0 3v.01");
    }
}
