using CommunityToolkit.Mvvm.ComponentModel;

namespace WinUI.Framework.Mvvm;

/// <summary>
/// Base class for all view models. Derives from <see cref="ObservableValidator"/>
/// so every view model inherits change notification AND DataAnnotations-based
/// input validation. Inject framework services (navigation, dialogs, settings,
/// theming) through the constructor.
/// </summary>
public abstract partial class ViewModelBase : ObservableValidator
{
    /// <summary>Title shown in the window title bar or page header.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>Optional descriptive subtitle shown under the title.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; set; } = string.Empty;
}
