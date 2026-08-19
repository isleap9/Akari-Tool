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
        // All 17 entries deferred — shell-verb subkey create/delete tree, not value writes.
        // Detect uses subkey existence (OpenSubKey != null), not named-value reads.
        // [DEFERRED: all customize-context-menu-* entries]
        return [];
    }
}
