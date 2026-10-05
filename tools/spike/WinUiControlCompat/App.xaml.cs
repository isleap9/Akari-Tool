using Microsoft.UI.Xaml;

namespace AkariTool.Spike.WinUiControlCompat;

/// <summary>
/// SPIKE-02 throwaway application entry point (deleted in Task 3 per D-05).
/// </summary>
/// <remarks>
/// Deliberately the whole of it: no DI container, no service registration, no
/// ServiceLocator call, no <c>vendor/WinUI.Framework</c>. The point is a clean-room
/// render of three third-party controls, so anything injected here would be a
/// variable that could explain away the very failure the spike exists to observe.
/// </remarks>
public partial class App : Application
{
    /// <summary>The single window the human render check looks at.</summary>
    public static Window? SpikeWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SpikeWindow = new MainWindow();
        SpikeWindow.Activate();
    }
}
