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
        // BCD CARD
        // ══════════════════════════════════════════════════════════════════════

        private void BuildBcdCard(StackPanel panel)
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
            info.Children.Add(new TextBlock { Text = "BCD Tweaks", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
            info.Children.Add(new TextBlock
            {
                Text = "Legacy boot menu (F8), disable dynamic tick (better timer resolution), " +
                       "disable recovery mode (faster boot), disable hibernation (removes hiberfil.sys). " +
                       "Current values are backed up to registry before applying.",
                FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            info.Children.Add(new TextBlock
            {
                Text = "⚠  BCD changes are NOT covered by System Restore — values are backed up internally for undo.",
                FontSize = 11, Foreground = TweakHelpers.Accent, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(btns, 1);

            var applyBtn = new Button { Content = "Apply", Style = (Style)FindResource("RunBtn"), Margin = new Thickness(0, 0, 6, 0) };
            applyBtn.Click += async (_, _) =>
            {
                applyBtn.IsEnabled = false;
                try { await BcdBackup.BackupAsync(Service!); await BcdBackup.ApplyAsync(Service!, new BcdTweakOptions()); }
                finally { applyBtn.IsEnabled = true; }
            };

            var undoBtn = new Button { Content = "Restore", Style = (Style)FindResource("UndoBtn") };
            undoBtn.Click += async (_, _) => { undoBtn.IsEnabled = false; try { await BcdBackup.RestoreAsync(Service!); } finally { undoBtn.IsEnabled = true; } };

            btns.Children.Add(applyBtn);
            btns.Children.Add(undoBtn);
            row.Children.Add(info);
            row.Children.Add(btns);
            card.Child = row;
            panel.Children.Add(card);
        }

    }
}
