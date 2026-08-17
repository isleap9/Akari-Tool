using CommunityToolkit.Mvvm.ComponentModel;
using WinUI.Framework.Mvvm;

namespace AkariTool.ViewModels;

/// <summary>
/// Shown for every rail destination that has no page yet (all of them except
/// Home). The navigation parameter carries the rail tag so the placeholder can
/// name what is coming.
/// </summary>
public partial class PlaceholderViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string TabTag { get; set; } = string.Empty;

    partial void OnTabTagChanged(string value)
    {
        Title = value;
        Subtitle = $"{value} is arriving in a later phase.";
    }
}
