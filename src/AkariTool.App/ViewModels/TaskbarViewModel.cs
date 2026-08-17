using System.Linq;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Taskbar sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Taskbar group (Layout → Behavior → Grouping),
/// reached via the Customize landing hub or the Taskbar sub-nav rail item.
///
/// "Behavior" = TaskbarBehavior + TaskbarBehaviorExtras concatenated into one
/// card (net8 BuildTaskbarBehavior calls BuildTaskbarBehaviorExtras on the same
/// section). "Button Grouping" is net8's 3 hand-made ComboBoxes as 3 dropdown
/// TweakDefinitions (Phase 17). Section order and every TweakDefinition Id are
/// preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class TaskbarViewModel : TweakPageViewModel
{
    public TaskbarViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Taskbar";
        Subtitle = "Layout, behavior, and button grouping.";
    }

    public override string NavTag => "Taskbar";
    public override string NavLabel => "Taskbar";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Layout", CustomizeTweaks.TaskbarLayout(log));
        yield return ("Behavior",
            CustomizeTweaks.TaskbarBehavior(log).Concat(CustomizeTweaks.TaskbarBehaviorExtras(log)).ToArray());
        yield return ("Button Grouping", CustomizeTweaks.TaskbarButtonGrouping(log));
    }
}
