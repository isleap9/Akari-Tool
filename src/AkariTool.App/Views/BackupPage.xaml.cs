using Microsoft.UI.Xaml.Controls;
using AkariTool.ViewModels.Backup;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Backup &amp; Restore page (rail tag "Backup"). Thin view over
/// <see cref="BackupViewModel"/>; the export summary count is refreshed on each load
/// (net8 refreshed on tab (re)load — TweakRegistry.Count can grow as tabs warm up).
/// </summary>
public sealed partial class BackupPage : Page
{
    public BackupViewModel ViewModel { get; }

    public BackupPage()
    {
        // Resolve BEFORE InitializeComponent: x:Bind evaluates during Initialize.
        ViewModel = ServiceLocator.GetService<BackupViewModel>();

        InitializeComponent();

        Loaded += (_, _) => ViewModel.RefreshSummary();
    }
}
