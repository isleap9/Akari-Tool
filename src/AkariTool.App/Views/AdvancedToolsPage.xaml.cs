using System.IO;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.AdvancedTools;
using WinUI.Framework.IoC;
using WinUI.Framework.Services;

namespace AkariTool.Views;

/// <summary>
/// Advanced Tools page — faithful code-behind port of net8 <c>AdvancedToolsTab</c>
/// (+ .Landing/.Wizard/.Generator/.Steps partials). Landing → WIM wizard or Autounattend
/// generator. Logic is the already-ported <see cref="WimUtilService"/> /
/// <see cref="AutounattendService"/> (byte-identical); this file adapts net8's imperative
/// UI: TweakHelpers tokens → ThemeResource brushes, FilePickers → <see cref="IFileService"/>,
/// <c>Service</c> → <see cref="ToolService"/>. No confirmation dialogs (net8 parity).
/// </summary>
public sealed partial class AdvancedToolsPage : Page
{
    public AdvancedToolsViewModel ViewModel { get; }

    private readonly ToolService _tool;
    private readonly IFileService _files;
    private WimUtilService _wim => ViewModel.Wim;
    private AutounattendService _xmlGen => ViewModel.Xml;

    // ── Wizard state (net8) ────────────────────────────────────────────────
    private string _isoPath = string.Empty;
    private string _workDir = Path.Combine(Path.GetTempPath(), "AkariWIM");
    private string _outputIsoPath = string.Empty;
    private bool _extractionDone;
    private bool _xmlAdded;
    private bool _driversAdded;
    private bool _busy;
    private CancellationTokenSource? _cts;

    // ── Step UI references ─────────────────────────────────────────────────
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

    // ── Generator UI references ────────────────────────────────────────────
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

    public AdvancedToolsPage()
    {
        ViewModel = ServiceLocator.GetService<AdvancedToolsViewModel>();
        _tool = ServiceLocator.GetService<ToolService>();
        _files = ServiceLocator.GetService<IFileService>();

        InitializeComponent();

        BuildLanding();
        BuildWizard();
        BuildGenerator();
    }

    // ═════════════════════════════════════════════════════════════════
    //  Panel navigation (net8 Show*)
    // ═════════════════════════════════════════════════════════════════

    private void ShowWizard()
    {
        LandingPanel.Visibility = Visibility.Collapsed;
        GeneratorPanel.Visibility = Visibility.Collapsed;
        WizardPanel.Visibility = Visibility.Visible;
        _ = RefreshOscdimgStateAsync();
    }

    private void ShowGenerator()
    {
        LandingPanel.Visibility = Visibility.Collapsed;
        WizardPanel.Visibility = Visibility.Collapsed;
        GeneratorPanel.Visibility = Visibility.Visible;
        RefreshGeneratorSummary();
    }

    private void ShowLanding()
    {
        if (_busy) return; // don't navigate away mid-operation
        WizardPanel.Visibility = Visibility.Collapsed;
        GeneratorPanel.Visibility = Visibility.Collapsed;
        LandingPanel.Visibility = Visibility.Visible;
    }

    // ═════════════════════════════════════════════════════════════════
    //  Landing
    // ═════════════════════════════════════════════════════════════════

    private void BuildLanding()
    {
        LandingPanel.Children.Add(MakeEntryCard(
            G("E958"), "Windows Installation Media Utility",
            "Create Custom Windows Installation Media", ShowWizard));

        LandingPanel.Children.Add(MakeEntryCard(
            G("E943"), "Create Autounattend XML",
            "Generate an autounattend.xml based on your current Akari Tool selections to customize Windows during installation.",
            ShowGenerator));
    }

    private Border MakeEntryCard(string glyph, string title, string description, Action onClick)
    {
        var card = new Border
        {
            Background = Res("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 16, 20, 16),
            Margin = new Thickness(0, 0, 0, 14),
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 20,
            Foreground = Res("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };
        Grid.SetColumn(icon, 0);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
        });
        Grid.SetColumn(text, 1);

        var chevron = new TextBlock
        {
            Text = G("E76C"),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 13,
            Foreground = Res("TextFillColorTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
        };
        Grid.SetColumn(chevron, 2);

        row.Children.Add(icon);
        row.Children.Add(text);
        row.Children.Add(chevron);
        card.Child = row;

        card.Tapped += (_, _) => onClick();
        card.PointerEntered += (_, _) => card.BorderBrush = Res("ControlStrokeColorSecondaryBrush");
        card.PointerExited += (_, _) => card.BorderBrush = Res("CardStrokeColorDefaultBrush");
        return card;
    }

    // ═════════════════════════════════════════════════════════════════
    //  Step status / gating / busy wrapper (net8)
    // ═════════════════════════════════════════════════════════════════

    private void SetStatus(int step, string text, bool done = false)
    {
        var ui = _steps[step - 1];
        DispatcherQueue.TryEnqueue(() =>
        {
            ui.Status.Text = text;
            if (done)
            {
                ui.Badge.Background = Hex("#3DDC84");
                ui.BadgeText.Text = G("E73E"); // check mark
                ui.BadgeText.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                ui.BadgeText.FontSize = 11;
                ui.BadgeText.Foreground = Hex("#0c0506");
            }
        });
    }

    /// <summary>Steps 2-4 are only interactive once extraction is complete.</summary>
    private void UpdateStepStates()
    {
        for (int i = 1; i < 4; i++)
        {
            var available = _extractionDone;
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
        _tool.StartProgress(operationName);

        try { await work(_cts.Token); }
        catch (OperationCanceledException) { _tool.Log($"[WIM] {operationName} cancelled by user."); }
        catch (Exception ex) { _tool.Log($"[WIM] {operationName} failed: {ex}"); }
        finally
        {
            _tool.StopProgress();
            _busy = false;
            _cts.Dispose();
            _cts = null;
            _cancelBtn.Visibility = Visibility.Collapsed;
            foreach (var b in _actionButtons) b.IsEnabled = true;
            UpdateStepStates();
        }
    }

    // ═════════════════════════════════════════════════════════════════
    //  Element helpers (net8, brush-adapted)
    // ═════════════════════════════════════════════════════════════════

    private Button MakeButton(string label, string color = "#F2F2F4")
    {
        var b = new Button
        {
            Content = label,
            Foreground = Hex(color),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 13,
        };
        _actionButtons.Add(b);
        return b;
    }

    private Button MakePrimaryButton(string label)
    {
        var b = new Button
        {
            Content = label,
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 13,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        _actionButtons.Add(b);
        return b;
    }

    private TextBlock MakeHint(string text) => new()
    {
        Text = text,
        FontSize = 12.5,
        Foreground = Res("TextFillColorSecondaryBrush"),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 8),
    };

    private TextBlock MakePathText(string text) => new()
    {
        Text = text,
        FontSize = 12.5,
        Foreground = Res("TextFillColorSecondaryBrush"),
        FontFamily = new FontFamily("Consolas"),
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static StackPanel HRow(params UIElement[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        foreach (var c in children) p.Children.Add(c);
        return p;
    }

    private Border MakeGenCard() => new()
    {
        Background = Res("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = Res("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(18, 14, 18, 14),
        Margin = new Thickness(0, 0, 0, 12),
        Child = new StackPanel(),
    };

    /// <summary>Segoe Fluent Icons glyph from a hex code point — keeps glyph chars out of source.</summary>
    internal static string G(string hex) => ((char)Convert.ToInt32(hex, 16)).ToString();

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    private static Brush Hex(string hex)
    {
        var s = hex.TrimStart('#');
        byte a = 0xFF, r, g, b;
        if (s.Length == 8)
        {
            a = Convert.ToByte(s.Substring(0, 2), 16);
            r = Convert.ToByte(s.Substring(2, 2), 16);
            g = Convert.ToByte(s.Substring(4, 2), 16);
            b = Convert.ToByte(s.Substring(6, 2), 16);
        }
        else
        {
            r = Convert.ToByte(s.Substring(0, 2), 16);
            g = Convert.ToByte(s.Substring(2, 2), 16);
            b = Convert.ToByte(s.Substring(4, 2), 16);
        }
        return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
    }
}
