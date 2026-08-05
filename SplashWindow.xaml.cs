using Microsoft.UI;                       // Colors, Win32Interop
using Microsoft.UI.Windowing;             // AppWindow, OverlappedPresenter, DisplayArea
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using AkariTool.Services;

namespace AkariTool
{
    /// <summary>
    /// Borderless startup splash (WinUI 3). Paints before the heavy
    /// <see cref="MainWindow"/> constructor runs (see <c>App.OnLaunched</c>) and
    /// reports the seven init stages via <see cref="Report"/>.
    ///
    /// MIGRATION: WPF set chrome declaratively (WindowStyle=None, ResizeMode,
    /// Topmost, WindowStartupLocation=CenterScreen, ShowInTaskbar). A WinUI Window
    /// has none of those, so they are applied here through AppWindow +
    /// OverlappedPresenter. WPF also animated <c>Window.Opacity</c> to fade out —
    /// WinUI Windows have no Opacity, so the ROOT ELEMENT is faded instead and the
    /// window closed when that completes.
    /// </summary>
    public sealed partial class SplashWindow : Window
    {
        public const int TotalSteps = 7;

        private const int SplashWidth  = 1120;
        private const int SplashHeight = 748;

        private readonly Border[] _pips;
        private Storyboard? _pipPulse;
        private Border? _pulsingPip;

        public SplashWindow()
        {
            this.InitializeComponent();

            // Brand mark for the active theme (single source of truth). Ctor-only: the
            // splash shows after the persisted theme is applied and never survives a switch.
            LogoImage.Source = ThemeService.Logo;

            ConfigureWindowChrome();

            _pips = new[]
            {
                (Border)PipRow.Children[0], (Border)PipRow.Children[1], (Border)PipRow.Children[2],
                (Border)PipRow.Children[3], (Border)PipRow.Children[4], (Border)PipRow.Children[5],
                (Border)PipRow.Children[6],
            };

            SplashRoot.Loaded += OnLoaded;
        }

        /// <summary>Borderless, centred, always-on-top.</summary>
        private void ConfigureWindowChrome()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);   // WindowStyle=None + NoResize
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;                 // Topmost
            }

            appWindow.Resize(new Windows.Graphics.SizeInt32(SplashWidth, SplashHeight));

            // WindowStartupLocation=CenterScreen has no WinUI equivalent — centre on the
            // work area of the display the window opened on.
            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            if (area is not null)
            {
                appWindow.Move(new Windows.Graphics.PointInt32(
                    area.WorkArea.X + (area.WorkArea.Width  - SplashWidth)  / 2,
                    area.WorkArea.Y + (area.WorkArea.Height - SplashHeight) / 2));
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ((Storyboard)SplashRoot.Resources["DotSb"]).Begin();
            ((Storyboard)SplashRoot.Resources["FadeUpSb"]).Begin();
        }

        /// <summary>
        /// Reports progress with <paramref name="completed"/> stages finished (0..7) while
        /// the stage named by <paramref name="label"/> is being worked. Shows percentage
        /// <c>completed/7</c>, fully lights pips <c>0..completed-1</c>, and pulses the
        /// active pip. Call on the UI thread.
        /// </summary>
        public void Report(int completed, string label)
        {
            completed = Math.Clamp(completed, 0, TotalSteps);

            StatusText.Text = label;
            PercentText.Text = (int)Math.Round(completed / (double)TotalSteps * 100) + "%";

            var accent = ThemeService.Color("AkariAccentColor");
            var empty  = ThemeService.Color("AkariHairlineColor");

            // Stop any previous pulse first, or the stopped storyboard's hold value
            // fights the opacities assigned below.
            StopPulse();

            for (int i = 0; i < _pips.Length; i++)
            {
                bool lit = i < completed;
                _pips[i].Opacity = 1;
                _pips[i].Background = new SolidColorBrush(lit ? accent : empty);
            }

            int active = completed;
            if (active >= 0 && active < _pips.Length)
            {
                _pips[active].Background = new SolidColorBrush(accent);
                StartPulse(_pips[active]);
            }
        }

        // MIGRATION: WPF used element.BeginAnimation(OpacityProperty, …), which has no
        // WinUI equivalent — a Storyboard is retargeted at the active pip instead.
        private void StartPulse(Border pip)
        {
            var anim = new DoubleAnimation
            {
                From = 0.35,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.45)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                EnableDependentAnimation = true,
            };
            Storyboard.SetTarget(anim, pip);
            Storyboard.SetTargetProperty(anim, "Opacity");

            _pipPulse = new Storyboard();
            _pipPulse.Children.Add(anim);
            _pulsingPip = pip;
            _pipPulse.Begin();
        }

        private void StopPulse()
        {
            if (_pipPulse is null) return;
            try { _pipPulse.Stop(); } catch { }
            _pipPulse = null;
            if (_pulsingPip is not null) { _pulsingPip.Opacity = 1; _pulsingPip = null; }
        }

        /// <summary>Fades the splash out (~250ms) and closes it.</summary>
        public Task FadeOutAndCloseAsync()
        {
            var tcs = new TaskCompletionSource();
            try
            {
                StopPulse();

                // WinUI Window has no Opacity — fade the root element instead.
                var fade = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                    EnableDependentAnimation = true,
                };
                Storyboard.SetTarget(fade, SplashRoot);
                Storyboard.SetTargetProperty(fade, "Opacity");

                var sb = new Storyboard();
                sb.Children.Add(fade);
                sb.Completed += (_, _) => { try { Close(); } catch { } tcs.TrySetResult(); };
                sb.Begin();
            }
            catch
            {
                try { Close(); } catch { }
                tcs.TrySetResult();
            }
            return tcs.Task;
        }
    }
}
