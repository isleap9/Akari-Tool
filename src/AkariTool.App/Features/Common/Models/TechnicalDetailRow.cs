using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.App.Features.Common.Models;

/// <summary>
/// UI-layer extension of TechnicalDetailRow with command and icon properties for XAML binding.
/// </summary>
public class TechnicalDetailRow : AkariTool.Core.Features.Common.Models.TechnicalDetailRow
{
    // Command and icon set from parent ViewModel
    public IRelayCommand<string>? OpenRegeditCommand { get; set; }
    public SoftwareBitmapSource? RegeditIconSource { get; set; }

    /// <summary>
    /// False when the registry key path does not exist, disabling the regedit button.
    /// </summary>
    public bool CanOpenRegedit { get; set; } = true;
}