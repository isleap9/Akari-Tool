using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace AkariTool.Spike.WinUiControlCompat;

/// <summary>
/// SPIKE-02 throwaway window (deleted in Task 3 per D-05). Its only job is to put the
/// three controls under test on screen at once and stay there.
/// </summary>
/// <remarks>
/// Deliberately no DI, no service locator, no framework base class. The code-behind
/// exists to size the window and to supply the DataGrid two rows; everything else is
/// declared in XAML.
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>Fixed comfortable size so nothing under test is clipped.</summary>
    private const int ProbeWidth = 720;

    private const int ProbeHeight = 640;

    public MainWindow()
    {
        InitializeComponent();

        // Fixed size, deliberately not user-resizable away mid-check: a render verdict
        // must not depend on the window the operator happened to drag.
        AppWindow.Resize(new SizeInt32(ProbeWidth, ProbeHeight));
    }

    /// <summary>
    /// The two rows the DataGrid paints. Plain objects defined here rather than in XAML,
    /// because a grid with an ItemsSource of zero items renders headers only and that is
    /// a different symptom from one that renders nothing.
    /// </summary>
    public ProbeRowList ProbeRows { get; } = new();
}

/// <summary>One DataGrid row. Two columns is all the probe needs.</summary>
public sealed class ProbeRow
{
    /// <summary>First column value.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Second column value.</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>The DataGrid ItemsSource: exactly two rows.</summary>
public sealed class ProbeRowList : List<ProbeRow>
{
    public ProbeRowList()
    {
        Add(new ProbeRow { Name = "row-one", Value = "alpha" });
        Add(new ProbeRow { Name = "row-two", Value = "beta" });
    }
}
