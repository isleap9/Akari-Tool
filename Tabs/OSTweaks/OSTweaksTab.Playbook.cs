using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.OSTweaks
{
    public partial class OSTweaksTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // PLAYBOOK CARD
        // ══════════════════════════════════════════════════════════════════════

        private void BuildPlaybookCard(StackPanel panel)
        {
            var card = new Border
            {
                Background = TweakHelpers.DangerBg, BorderBrush = TweakHelpers.DangerBorder,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 14, 16, 14), Margin = new Thickness(0, 0, 0, 16)
            };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock { Text = "AkariOS Playbook Tweaks", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
            info.Children.Add(new TextBlock
            {
                Text = "Applies 30 registry tweaks, 15 ETW autologger disables, 12 IFEO process priorities, " +
                       "66 scheduled task disables, filesystem tweaks, and telemetry opt-outs from the AkariOS playbook. " +
                       "A system restore point is created automatically before applying.",
                FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            info.Children.Add(new TextBlock
            {
                Text = "ℹ  Registry/task changes are restore-point-recoverable. Memory compression and DISM changes are not.",
                FontSize = 11, Foreground = TweakHelpers.WarnFg, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(btns, 1);

            var applyBtn = new Button { Content = "Apply All", Style = (Style)FindResource("RunBtn"), Margin = new Thickness(0, 0, 6, 0) };
            applyBtn.Click += async (_, _) =>
            {
                applyBtn.IsEnabled = false;
                try
                {
                    Service!.Log("[RESTORE] Creating system restore point...");
                    bool rpOk = await RestorePointHelper.EnsureRestorePointAsync(Service!);
                    Service!.Log(rpOk ? "[RESTORE] ✓ Restore point ready." : "[RESTORE] ⚠ Could not create restore point — proceeding anyway.");
                    await PlaybookTweaks.ApplyAllAsync(Service!);
                }
                finally { applyBtn.IsEnabled = true; }
            };

            var undoBtn = new Button { Content = "Undo All", Style = (Style)FindResource("UndoBtn") };
            undoBtn.Click += async (_, _) => { undoBtn.IsEnabled = false; try { await PlaybookTweaks.UndoAllAsync(Service!); } finally { undoBtn.IsEnabled = true; } };

            btns.Children.Add(applyBtn);
            btns.Children.Add(undoBtn);
            row.Children.Add(info);
            row.Children.Add(btns);
            card.Child = row;
            panel.Children.Add(card);
        }

    }
}
