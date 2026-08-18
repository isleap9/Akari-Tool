using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class StartMenuOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildLayout(),
        .. BuildBehavior(),
    ];

    private static IReadOnlyList<SettingGroup> BuildLayout()
    {
        // TODO: Start Menu Layout — CustomizeTweaks.StartMenu.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildBehavior()
    {
        // TODO: Start Menu Behavior — CustomizeTweaks.StartMenu.cs
        return [];
    }
}
