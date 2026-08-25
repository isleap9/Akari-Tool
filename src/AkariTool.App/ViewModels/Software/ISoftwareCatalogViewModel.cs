using CommunityToolkit.Mvvm.Input;

namespace AkariTool.ViewModels.Software;

/// <summary>
/// Common surface the Software &amp; Apps shared toolbar binds to, so one toolbar can drive
/// whichever catalog tab is active (Windows Apps or External Apps). Implemented by both
/// <see cref="WindowsAppsViewModel"/> and <see cref="ExternalAppsViewModel"/>.
///
/// <c>UninstallCommand</c> unifies the two panels' differently-named commands
/// (Windows: RemoveSelected, External: UninstallSelected).
/// </summary>
public interface ISoftwareCatalogViewModel
{
    string SelectedCountText { get; }
    bool ButtonsEnabled { get; }
    string SearchText { get; set; }

    bool SelectAll { get; set; }
    bool SelectInstalled { get; set; }
    bool SelectNotInstalled { get; set; }

    SoftwareViewMode ViewMode { get; set; }
    SoftwareSortMode SortMode { get; set; }

    IAsyncRelayCommand InstallSelectedCommand { get; }
    IAsyncRelayCommand UninstallCommand { get; }
    IAsyncRelayCommand RefreshStatusCommand { get; }
}
