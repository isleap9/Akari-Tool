using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Power page. Copy of the GamingPage shell — layout and behaviour live in the
/// shared templates + <see cref="SettingPageViewModel"/>; only the concrete VM
/// type and the flyout plumbing differ. (Power-specific content — the plan row
/// and the hardware gating — is data on <see cref="PowerViewModel"/>, rendered by
/// the shared rendering layer.)
/// </summary>
public sealed partial class PowerPage : Page
{
    public PowerViewModel ViewModel { get; }

    public PowerPage()
    {
        InitializeComponent();

        // SINGLETON: the view model owns the built catalog + row state, so the
        // Frame must reuse this page instance rather than rebuild it per navigation.
        ViewModel = ServiceLocator.GetService<PowerViewModel>();

        // Build once. Safe to call repeatedly — Build() is idempotent.
        ViewModel.Build();
    }

    /// <summary>
    /// Counts are computed when the menu opens, using the same predicate the bulk
    /// engine runs, so the menu can never advertise work the run then skips.
    /// </summary>
    private void QuickActionsFlyout_Opening(object sender, object e)
        => ViewModel.RefreshQuickActionCounts();

    /// <summary>
    /// Dismiss the flyout before the command's dialog opens — a ContentDialog
    /// shown while a Flyout is still up leaves the flyout floating over it.
    /// </summary>
    private void QuickActionItem_Click(object sender, RoutedEventArgs e)
        => QuickActionsFlyout.Hide();
}