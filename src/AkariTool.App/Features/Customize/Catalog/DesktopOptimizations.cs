using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class DesktopOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildIcons(),
        .. BuildShortcuts(),
        .. BuildStartup(),
        .. BuildDevices(),
        .. BuildLockScreen(),
        .. BuildRegional(),
    ];

    private static IReadOnlyList<SettingGroup> BuildIcons()
    {
        // TODO: Desktop Icons — CustomizeTweaks.Desktop.Icons.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildShortcuts()
    {
        // TODO: Desktop Shortcuts — CustomizeTweaks.Desktop.System.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildStartup()
    {
        // TODO: Desktop Startup — CustomizeTweaks.Desktop.System.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildDevices()
    {
        // TODO: Desktop Devices — CustomizeTweaks.Desktop.System.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildLockScreen()
    {
        // TODO: Desktop Lock Screen — CustomizeTweaks.Desktop.System.cs
        return [];
    }

    private static IReadOnlyList<SettingGroup> BuildRegional()
    {
        // TODO: Desktop Regional Settings — CustomizeTweaks.Desktop.Regional.cs
        return [];
    }
}
