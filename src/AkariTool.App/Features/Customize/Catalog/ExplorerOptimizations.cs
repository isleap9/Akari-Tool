using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class ExplorerOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildView(),
        .. BuildBehavior(),
        .. BuildAssociations(),
        .. BuildSidebar(),
        .. BuildThisPc(),
    ];

    private static IReadOnlyList<SettingGroup> BuildView()
    {
        // TODO: Explorer View + FolderOptions — CustomizeTweaks.Explorer.View.cs + FolderOptions
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildBehavior()
    {
        // TODO: Explorer Behavior — CustomizeTweaks.Explorer.Behavior.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildAssociations()
    {
        // TODO: Explorer File Associations — CustomizeTweaks.Explorer.Associations.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildSidebar()
    {
        // TODO: Explorer Sidebar — CustomizeTweaks.Explorer.Sidebar.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildThisPc()
    {
        // TODO: Explorer This PC Folders — CustomizeTweaks.Explorer.ThisPc.cs
        return [];
    }
}
