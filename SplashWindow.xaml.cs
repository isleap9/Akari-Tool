using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using AkariTool.Services;

namespace AkariTool
{
    /// <summary>
    /// Borderless startup splash. Paints before the heavy <see cref="MainWindow"/> constructor
    /// runs (see <c>App.OnStartup</c>) and reports the seven init stages via <see cref="Report"/>.
    /// </summary>
    public partial class SplashWindow : Window
    {
        public const int TotalSteps = 7;

        private readonly Border[] _pips;
        private DoubleAnimation? _pipPulse;

        public SplashWindow()
        {
            InitializeComponent();

            // Brand mark for the active theme (single source of truth). Ctor-only: the splash
            // shows after the persisted theme is applied and never survives a switch.
            LogoImage.Source = ThemeService.Logo;

            _pips = new[]
            {
                (Border)PipRow.Children[0], (Border)PipRow.Children[1], (Border)PipRow.Children[2],
                (Border)PipRow.Children[3], (Border)PipRow.Children[4], (Border)PipRow.Children[5],
                (Border)PipRow.Children[6],
            };

            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            ((Storyboard)Resources["DotSb"]).Begin();
            ((Storyboard)Resources["FadeUpSb"]).Begin();
        }

        /// <summary>
        /// Reports progress with <paramref name="completed"/> stages finished (0..7) while the
        /// stage named by <paramref name="label"/> is being worked. Shows percentage
        /// <c>completed/7</c>, fully lights pips <c>0..completed-1</c>, and pulses the active
        /// pip (<c>completed</c>) that is currently filling. Call on the UI thread.
        /// </summary>
        public void Report(int completed, string label)
        {
            completed = Math.Clamp(completed, 0, TotalSteps);

            StatusText.Text = label;
            PercentText.Text = (int)Math.Round(completed / (double)TotalSteps * 100) + "%";

            var accent = ThemeService.Color("AkariAccentColor");
            var empty  = ThemeService.Color("AkariHairlineColor");

            for (int i = 0; i < _pips.Length; i++)
            {
                bool lit = i < completed;
                _pips[i].BeginAnimation(OpacityProperty, null);
                _pips[i].Opacity = 1;
                _pips[i].Background = new SolidColorBrush(lit ? accent : empty);
            }

            // The stage now in progress: light its pip and pulse it (opacity 0.35 ↔ 1) so the
            // bar shows forward motion at 0% before the first stage even completes.
            int active = completed;
            if (active >= 0 && active < _pips.Length)
            {
                _pips[active].Background = new SolidColorBrush(accent);

                _pipPulse ??= new DoubleAnimation
                {
                    From = 0.35,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.45),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                };
                _pips[active].BeginAnimation(OpacityProperty, _pipPulse);
            }
        }

        /// <summary>Fades the splash out (~250ms) and closes it.</summary>
        public Task FadeOutAndCloseAsync()
        {
            var tcs = new TaskCompletionSource();
            var fade = new DoubleAnimation
            {
                From = Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
            };
            fade.Completed += (_, _) => { Close(); tcs.TrySetResult(); };
            BeginAnimation(OpacityProperty, fade);
            return tcs.Task;
        }
    }
}
