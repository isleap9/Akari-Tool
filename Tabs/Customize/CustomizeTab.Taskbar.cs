using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ─────────────────────────────────────────────────────────────────────
        // TASKBAR
        //
        // Sections live in sibling partials — section order here is the render
        // order on the page:
        //   CustomizeTab.Taskbar.Layout.cs    → BuildTaskbarLayout
        //   CustomizeTab.Taskbar.Behavior.cs  → BuildTaskbarBehavior
        //   CustomizeTab.Taskbar.Grouping.cs  → BuildTaskbarGrouping
        // ─────────────────────────────────────────────────────────────────────

        private void BuildTaskbar(StackPanel panel)
        {
            panel.Children.Add(PageHeader("Taskbar", "Taskbar layout, behavior, and system tray tweaks.",
                withActions: true, panel));

            BuildTaskbarLayout(panel);
            BuildTaskbarBehavior(panel);
            BuildTaskbarGrouping(panel);
        }

        /// <summary>
        /// Auto-hide lives in the StuckRects3 settings blob rather than a plain
        /// DWORD, so it needs a read-modify-write of bit 0 in byte 8.
        /// </summary>
        private void SetTaskbarAutoHide(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3", writable: true);
            if (key?.GetValue("Settings") is not byte[] data) return;
            if (enable)
                data[8] |= 0x01;   // set auto-hide bit
            else
                data[8] &= 0xFE;   // clear auto-hide bit
            key.SetValue("Settings", data, RegistryValueKind.Binary);
            if (!_suppressRestart) ExplorerRestart.Request();
        }
    }
}
