using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Customize page. Copy of the wave-1 page — layout and behaviour live in the
/// shared templates + <see cref="TweakPageViewModel"/>; only the concrete VM type
/// and the flyout plumbing differ.
/// </summary>
public sealed partial class CustomizePage : Page
{
    public CustomizeViewModel ViewModel { get; }

    public CustomizePage()
    {
        InitializeComponent();

        // SINGLETON: rows register with TweakRegistry on construction and there is
        // no unregister, so the view model must not be rebuilt per navigation.
        ViewModel = ServiceLocator.GetService<CustomizeViewModel>();

        // Build once. Idempotent + thread-safe — the startup warm-up may already
        // have built it before this navigation, in which case this is a no-op.
        ViewModel.Build();
    }

    private void QuickActionsFlyout_Opening(object sender, object e)
        => ViewModel.RefreshQuickActionCounts();

    private void QuickActionItem_Click(object sender, RoutedEventArgs e)
        => QuickActionsFlyout.Hide();
}
