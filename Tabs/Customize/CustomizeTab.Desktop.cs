using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ─────────────────────────────────────────────────────────────────────
        // DESKTOP
        //
        // Sections live in sibling partials — section order here is the render
        // order on the page:
        //   CustomizeTab.Desktop.Icons.cs     → BuildDesktopIcons     (icons + shortcuts)
        //   CustomizeTab.Desktop.System.cs    → BuildDesktopSystem    (startup, devices, lock screen)
        //   CustomizeTab.Desktop.Regional.cs  → BuildDesktopRegional  (International dropdowns)
        // ─────────────────────────────────────────────────────────────────────

        private void BuildDesktop(StackPanel panel)
        {
            panel.Children.Add(PageHeader("Desktop", "Desktop icons, lock screen, and system startup behavior.",
                withActions: true, panel));

            BuildDesktopIcons(panel);
            BuildDesktopSystem(panel);
            BuildDesktopRegional(panel);
        }
    }
}
