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
        // EXPLORER
        //
        // Sections live in sibling partials — section order here is the render
        // order on the page:
        //   CustomizeTab.Explorer.View.cs          → BuildExplorerView
        //   CustomizeTab.Explorer.Behavior.cs      → BuildExplorerBehavior
        //   CustomizeTab.Explorer.Associations.cs  → BuildExplorerAssociations
        //   CustomizeTab.Explorer.Sidebar.cs       → BuildExplorerSidebar
        //   CustomizeTab.Explorer.ThisPc.cs        → BuildExplorerThisPc
        // ─────────────────────────────────────────────────────────────────────

        private void BuildExplorer(StackPanel panel)
        {
            panel.Children.Add(PageHeader("Explorer",
                "Windows Explorer UI and behavior tweaks. Changes apply immediately — Explorer restarts after each change.",
                withActions: true, panel));

            BuildExplorerView(panel);
            BuildExplorerBehavior(panel);
            BuildExplorerAssociations(panel);
            BuildExplorerSidebar(panel);
            BuildExplorerThisPc(panel);
        }

        /// <summary>
        /// Runs an Explorer registry write, then restarts Explorer so the change
        /// shows immediately (same pattern as the Taskbar rows). Inside a bulk
        /// run the restart coalesces to one via ExplorerRestart batching.
        /// </summary>
        private void WriteExplorer(Action write)
        {
            try { write(); }
            catch (Exception ex) { Service?.Log($"[EXPLORER] ERROR: {ex.Message}"); }
            if (!_suppressRestart) ExplorerRestart.Request();
        }
    }
}
