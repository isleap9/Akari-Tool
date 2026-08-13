using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Desktop sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Desktop group, reached via the Customize landing
/// hub or the Desktop sub-nav rail item. Section order and every TweakDefinition
/// Id are preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class DesktopViewModel : TweakPageViewModel
{
    public DesktopViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Desktop";
        Subtitle = "Desktop icons, shortcuts, startup, devices, lock screen, and regional settings.";
    }

    public override string NavTag => "Desktop";
    public override string NavLabel => "Desktop";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Desktop Icons", CustomizeTweaks.DesktopIcons(log));
        yield return ("Shortcuts", CustomizeTweaks.DesktopShortcuts(log));
        yield return ("Startup", CustomizeTweaks.DesktopStartup(log));
        yield return ("Devices", CustomizeTweaks.DesktopDevices(log));
        yield return ("Lock Screen", CustomizeTweaks.DesktopLockScreen(log));
        yield return ("Regional Settings", CustomizeTweaks.RegionalSettings(log)); // contains os-set-utc
    }
}
