using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class AppearanceOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildTheme(),
        .. BuildEffects(),
        .. BuildColor(),
        .. BuildWindowStyle(),
    ];

    private static IReadOnlyList<SettingGroup> BuildTheme()
    {
        // TODO: Appearance Theme — CustomizeTweaks.Appearance.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildEffects()
    {
        // TODO: Appearance Transparency & Effects — CustomizeTweaks.Appearance.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildColor()
    {
        // TODO: Appearance Color — CustomizeTweaks.Appearance.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildWindowStyle()
    {
        // TODO: Appearance Window Style — CustomizeTweaks.Appearance.cs
        return [];
    }
}
