using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Gaming &amp; Performance page — the spike host for the MVVM tweak-rendering layer.
///
/// The page owns almost nothing: layout is in GamingPage.xaml, rows/sections/badges
/// come from the shared templates in Views/Templates/TweakTemplates.xaml, and all
/// behaviour is on <see cref="GamingViewModel"/>. The only code-behind is the
/// Flyout plumbing, which has no view-model-friendly binding surface.
/// </summary>
public sealed partial class GamingPage : Page
{
    public GamingViewModel ViewModel { get; }

    public GamingPage()
    {
        InitializeComponent();

        // SINGLETON: rows register with TweakRegistry on construction and there is
        // no unregister, so the view model must not be rebuilt per navigation.
        ViewModel = ServiceLocator.GetService<GamingViewModel>();

        // Build once. Safe to call repeatedly — Build() is idempotent.
        ViewModel.Build();
        ViewModel.AddDefenderRow();
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
