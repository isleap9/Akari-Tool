using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── TASKBAR ▸ BUTTON GROUPING ──
        private void BuildTaskbarGrouping(StackPanel panel)
        {
            // ── Button grouping dropdown ───────────────────────────────────────
            var groupSection = TweakHelpers.BuildSection(panel, "Button Grouping");
            groupSection.Children.Add(new TextBlock
            {
                Text       = "Combine Taskbar Buttons",
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary
            });
            groupSection.Children.Add(new TextBlock
            {
                Text         = "Controls whether taskbar buttons for the same app are grouped together",
                FontSize     = 13,
                Foreground   = TweakHelpers.TextSecondary,
                Margin       = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            // Styling comes from the app-wide implicit ComboBox style (App.xaml, §5b).
            var combineDropdown = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width               = 300
            };
            combineDropdown.Items.Add("Always combine");
            combineDropdown.Items.Add("Combine when taskbar is full");
            combineDropdown.Items.Add("Never combine");

            var currentCombine = SystemStateReader.ReadCombineTaskbarButtons();
            combineDropdown.SelectedIndex = currentCombine.HasValue ? Math.Min(currentCombine.Value, 2) : 0;

            bool combineInit = false;
            combineDropdown.Loaded           += (_, _) => combineInit = true;
            combineDropdown.SelectionChanged += (_, _) =>
            {
                if (!combineInit) return;
                SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "TaskbarGlomLevel", combineDropdown.SelectedIndex);
                if (!_suppressRestart) ExplorerRestart.Request();
                Service?.Log($"[TASKBAR] Button grouping: {combineDropdown.SelectedItem}.");
            };

            groupSection.Children.Add(combineDropdown);

            // ── Multi-monitor grouping (Winhance port) ────────────────────────
            groupSection.Children.Add(new TextBlock
            {
                Text       = "Show Taskbar Apps On",
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                Margin     = new Thickness(0, 16, 0, 0)
            });
            groupSection.Children.Add(new TextBlock
            {
                Text         = "Which monitors show open-window buttons when the taskbar spans multiple displays",
                FontSize     = 13,
                Foreground   = TweakHelpers.TextSecondary,
                Margin       = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            var mmModeDropdown = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width               = 300
            };
            mmModeDropdown.Items.Add("All taskbars");
            mmModeDropdown.Items.Add("Main taskbar and taskbar where window is open");
            mmModeDropdown.Items.Add("Taskbar where window is open");

            var currentMmMode = ReadDwordCu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "MMTaskbarMode");
            mmModeDropdown.SelectedIndex = currentMmMode.HasValue ? Math.Min(currentMmMode.Value, 2) : 0;

            bool mmModeInit = false;
            mmModeDropdown.Loaded           += (_, _) => mmModeInit = true;
            mmModeDropdown.SelectionChanged += (_, _) =>
            {
                if (!mmModeInit) return;
                SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "MMTaskbarMode", mmModeDropdown.SelectedIndex);
                if (!_suppressRestart) ExplorerRestart.Request();
                Service?.Log($"[TASKBAR] Multi-monitor mode: {mmModeDropdown.SelectedItem}.");
            };
            groupSection.Children.Add(mmModeDropdown);

            groupSection.Children.Add(new TextBlock
            {
                Text       = "Combine Buttons on Other Taskbars",
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                Margin     = new Thickness(0, 16, 0, 0)
            });
            groupSection.Children.Add(new TextBlock
            {
                Text         = "Button grouping behaviour on secondary-monitor taskbars",
                FontSize     = 13,
                Foreground   = TweakHelpers.TextSecondary,
                Margin       = new Thickness(0, 2, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            var mmGlomDropdown = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width               = 300
            };
            mmGlomDropdown.Items.Add("Always combine");
            mmGlomDropdown.Items.Add("Combine when taskbar is full");
            mmGlomDropdown.Items.Add("Never combine");

            var currentMmGlom = ReadDwordCu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "MMTaskbarGlomLevel");
            mmGlomDropdown.SelectedIndex = currentMmGlom.HasValue ? Math.Min(currentMmGlom.Value, 2) : 0;

            bool mmGlomInit = false;
            mmGlomDropdown.Loaded           += (_, _) => mmGlomInit = true;
            mmGlomDropdown.SelectionChanged += (_, _) =>
            {
                if (!mmGlomInit) return;
                SetHkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "MMTaskbarGlomLevel", mmGlomDropdown.SelectedIndex);
                if (!_suppressRestart) ExplorerRestart.Request();
                Service?.Log($"[TASKBAR] Secondary taskbar grouping: {mmGlomDropdown.SelectedItem}.");
            };
            groupSection.Children.Add(mmGlomDropdown);
        }
    }
}
