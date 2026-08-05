using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.AdvancedTools
{
    /// <summary>
    /// Advanced Tools tab — ported from Winhance's Advanced Tools section.
    ///
    /// Landing page shows two entry cards (matching the Winhance layout):
    ///   1. Windows Installation Media Utility  → 4-step WIM wizard (this pass)
    ///   2. Create Autounattend XML             → placeholder (pass 2)
    ///
    /// The WIM wizard mirrors Winhance's flow:
    ///   Step 1  Select ISO   — pick ISO + working directory, extract (or reuse an
    ///                          already-extracted folder). Optional WIM ⇄ ESD conversion.
    ///   Step 2  Add XML File — generate an Akari autounattend.xml, pick a custom xml,
    ///                          or open the Schneegans generator.
    ///   Step 3  Add Drivers  — export current system drivers or pick a folder;
    ///                          storage drivers go to $WinpeDriver$, the rest to $OEM$.
    ///   Step 4  Create ISO   — locate/install oscdimg, pick output, build bootable ISO.
    /// </summary>
    public partial class AdvancedToolsTab : BaseTab
    {
        private WimUtilService _wim = null!;
        private AutounattendService _xmlGen = null!;
        private Func<List<AppDefinition>>? _selectedAppsProvider;

        // ── Views ──────────────────────────────────────────────────────────
        private StackPanel _landingPanel = null!;
        private StackPanel _wizardPanel = null!;
        private StackPanel _generatorPanel = null!;

        // ── Wizard state ───────────────────────────────────────────────────
        private string _isoPath = string.Empty;
        private string _workDir = Path.Combine(Path.GetTempPath(), "AkariWIM");
        private string _outputIsoPath = string.Empty;
        private bool _extractionDone;
        private bool _xmlAdded;
        private bool _driversAdded;
        private bool _busy;
        private CancellationTokenSource? _cts;

        // ── Step UI references ─────────────────────────────────────────────
        private readonly StepUi[] _steps = new StepUi[4];
        private TextBlock _isoPathText = null!;
        private TextBlock _workDirText = null!;
        private Button _extractBtn = null!;
        private CheckBox _alreadyExtracted = null!;
        private StackPanel _conversionPanel = null!;
        private TextBlock _oscdimgStatus = null!;
        private Button _oscdimgBtn = null!;
        private TextBlock _outputPathText = null!;
        private Button _createIsoBtn = null!;
        private Button _cancelBtn = null!;
        private readonly List<Button> _actionButtons = [];

        // ── Generator UI references ────────────────────────────────────────
        private TextBlock _genAppsSummary = null!;
        private readonly List<(UnattendTweakOption Option, CheckBox Box)> _tweakChecks = [];

        private sealed class StepUi
        {
            public Border Card = null!;
            public Border Badge = null!;
            public TextBlock BadgeText = null!;
            public TextBlock Status = null!;
            public StackPanel Body = null!;
            public TextBlock Chevron = null!;
        }

        public AdvancedToolsTab() => InitializeComponent();

        public override string NavTag   => "Advanced";
        public override string NavLabel => "Advanced Tools";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            _wim = new WimUtilService(service);
            _xmlGen = new AutounattendService(service);
            Build();
        }

        /// <summary>
        /// Wired by MainWindow so the generator can read the Windows apps
        /// currently ticked in the Software › Windows Apps grid.
        /// </summary>
        public void SetSelectedAppsProvider(Func<List<AppDefinition>> provider) =>
            _selectedAppsProvider = provider;

        // ═════════════════════════════════════════════════════════════════
        //  Build
        // ═════════════════════════════════════════════════════════════════

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Advanced Tools", "Power tools for Windows customization."));

            _landingPanel = new StackPanel();
            _wizardPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _generatorPanel = new StackPanel { Visibility = Visibility.Collapsed };
            RootPanel.Children.Add(_landingPanel);
            RootPanel.Children.Add(_wizardPanel);
            RootPanel.Children.Add(_generatorPanel);

            BuildLanding();
            BuildWizard();
            BuildGenerator();
        }

        private void ShowWizard()
        {
            _landingPanel.Visibility = Visibility.Collapsed;
            _generatorPanel.Visibility = Visibility.Collapsed;
            _wizardPanel.Visibility = Visibility.Visible;
            _ = RefreshOscdimgStateAsync();
        }

        private void ShowGenerator()
        {
            _landingPanel.Visibility = Visibility.Collapsed;
            _wizardPanel.Visibility = Visibility.Collapsed;
            _generatorPanel.Visibility = Visibility.Visible;
            RefreshGeneratorSummary();
        }

        private void ShowLanding()
        {
            if (_busy) return; // don't navigate away mid-operation
            _wizardPanel.Visibility = Visibility.Collapsed;
            _generatorPanel.Visibility = Visibility.Collapsed;
            _landingPanel.Visibility = Visibility.Visible;
        }

        // ═════════════════════════════════════════════════════════════════
        //  State plumbing
        // ═════════════════════════════════════════════════════════════════

        private void SetStatus(int step, string text, bool done = false)
        {
            var ui = _steps[step - 1];
            DispatcherQueue.TryEnqueue(() =>
            {
                ui.Status.Text = text;
                if (done)
                {
                    ui.Badge.Background = BrushFrom("#3DDC84");
                    ui.BadgeText.Text = "\uE73E"; // check mark
                    ui.BadgeText.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                    ui.BadgeText.FontSize = 11;
                    ui.BadgeText.Foreground = BrushFrom("#0c0506");
                }
            });
        }

        /// <summary>Steps 2-4 are only interactive once extraction is complete.</summary>
        private void UpdateStepStates()
        {
            for (int i = 1; i < 4; i++)
            {
                var available = _extractionDone;
                // WinUI: Border has no IsEnabled (that lives on Control), so
                // interactivity is gated with IsHitTestVisible; Opacity still
                // supplies the disabled look exactly as before.
                _steps[i].Card.IsHitTestVisible = available && !_busy;
                _steps[i].Card.Opacity = available ? 1.0 : 0.45;
                if (available && _steps[i].Status.Text == "Complete Step 1 first")
                    _steps[i].Status.Text = i switch
                    {
                        1 => _xmlAdded ? "autounattend.xml added" : "No XML added (optional)",
                        2 => _driversAdded ? "Drivers added" : "No drivers added (optional)",
                        _ => "Ready to create ISO",
                    };
            }
            _steps[0].Card.IsHitTestVisible = !_busy;
        }

        /// <summary>Wraps a long operation: busy state, cancel button, shared progress bar.</summary>
        private async Task RunBusyAsync(string operationName, Func<CancellationToken, Task> work)
        {
            if (_busy) return;
            _busy = true;
            _cts = new CancellationTokenSource();
            _cancelBtn.Visibility = Visibility.Visible;
            foreach (var b in _actionButtons) if (b != _cancelBtn) b.IsEnabled = false;
            UpdateStepStates();
            Service!.StartProgress(operationName);

            try { await work(_cts.Token); }
            catch (OperationCanceledException) { Service!.Log($"[WIM] {operationName} cancelled by user."); }
            catch (Exception ex) { Service!.Log($"[WIM] {operationName} failed: {ex}"); }
            finally
            {
                Service!.StopProgress();
                _busy = false;
                _cts.Dispose();
                _cts = null;
                _cancelBtn.Visibility = Visibility.Collapsed;
                foreach (var b in _actionButtons) b.IsEnabled = true;
                UpdateStepStates();
            }
        }
    }
}
