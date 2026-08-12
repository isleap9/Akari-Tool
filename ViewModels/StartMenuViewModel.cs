using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Start Menu sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Start Menu group, reached via the Customize
/// landing hub or the Start Menu sub-nav rail item. Section order and every
/// TweakDefinition Id are preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class StartMenuViewModel : TweakPageViewModel
{
    public StartMenuViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Start Menu";
        Subtitle = "Start menu layout and behavior.";
    }

    public override string NavTag => "StartMenu";
    public override string NavLabel => "Start Menu";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Layout", CustomizeTweaks.StartMenuLayout(log));
        yield return ("Behavior", CustomizeTweaks.StartMenuBehavior(log));
    }
}
