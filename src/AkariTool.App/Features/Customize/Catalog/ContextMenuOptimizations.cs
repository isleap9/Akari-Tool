using AkariTool.Core.Features.Common.Models;
using AkariTool.Core.Features.Common.Enums;
using Microsoft.Win32;
using System.Collections.Generic;

namespace AkariTool.Tabs.Customize;

public static class ContextMenuOptimizations
{
    public static IReadOnlyList<SettingGroup> Build() =>
    [
        .. BuildEntries(),
    ];

    private static IReadOnlyList<SettingGroup> BuildEntries()
    {
        // TODO: Context Menu Entries + Verbs — ContextMenu.cs + Verbs.Shell + Verbs.Tools
        return [];
    }
}
