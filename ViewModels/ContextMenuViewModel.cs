using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Context Menu sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Context Menu group, reached via the Customize
/// landing hub or the Context Menu sub-nav rail item. Every TweakDefinition Id is
/// preserved byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class ContextMenuViewModel : TweakPageViewModel
{
    public ContextMenuViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Context Menu";
        Subtitle = "Right-click menu entries.";
    }

    public override string NavTag => "ContextMenu";
    public override string NavLabel => "Context Menu";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Entries", CustomizeTweaks.ContextMenuEntries(log));
    }
}
