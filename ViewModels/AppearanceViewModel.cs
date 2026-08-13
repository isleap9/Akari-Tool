using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Appearance sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Appearance group, reached via the Customize
/// landing hub or the Appearance sub-nav rail item. Section order and every
/// TweakDefinition Id are preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class AppearanceViewModel : TweakPageViewModel
{
    public AppearanceViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Appearance";
        Subtitle = "Theme, transparency, color, and window style.";
    }

    public override string NavTag => "Appearance";
    public override string NavLabel => "Appearance";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Theme", CustomizeTweaks.AppearanceTheme(log));
        yield return ("Transparency & Effects", CustomizeTweaks.AppearanceEffects(log));
        yield return ("Color", CustomizeTweaks.AppearanceColor(log));
        yield return ("Window Style", CustomizeTweaks.AppearanceWindowStyle(log));
    }
}
