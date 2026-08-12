using System.Linq;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize ▸ Explorer sub-page. One slice of the former single-page
/// CustomizeViewModel — the net8 Explorer group, reached via the Customize
/// landing hub or the Explorer sub-nav rail item.
///
/// "View" = ExplorerView + ExplorerViewFolderOptions concatenated into one card
/// (net8 BuildExplorerView calls BuildExplorerViewFolderOptions on the same
/// section). Section order and every TweakDefinition Id are preserved
/// byte-for-byte from CustomizeViewModel.
/// </summary>
public sealed partial class ExplorerViewModel : TweakPageViewModel
{
    public ExplorerViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Explorer";
        Subtitle = "File Explorer view, behavior, associations, and This PC folders.";
    }

    public override string NavTag => "Explorer";
    public override string NavLabel => "Explorer";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("View",
            CustomizeTweaks.ExplorerView(log).Concat(CustomizeTweaks.ExplorerViewFolderOptions(log)).ToArray());
        yield return ("Behavior", CustomizeTweaks.ExplorerBehavior(log));
        yield return ("File Associations", CustomizeTweaks.ExplorerAssociations(log));
        yield return ("Sidebar", CustomizeTweaks.ExplorerSidebar(log));
        yield return ("This PC Folders", CustomizeTweaks.ExplorerThisPc(log));
    }
}
