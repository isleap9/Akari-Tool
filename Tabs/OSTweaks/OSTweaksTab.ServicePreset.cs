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
        // SERVICE PRESET ROW
        // ══════════════════════════════════════════════════════════════════════

        private System.Windows.Controls.TextBlock? _servicePresetLabel;

        private void BuildServicePresetRow(System.Windows.Controls.StackPanel panel)
        {
            var section = TweakHelpers.BuildSection(panel);
            var row = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(0, 12, 0, 12) };
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

            var info = new System.Windows.Controls.StackPanel();
            System.Windows.Controls.Grid.SetColumn(info, 0);
            info.Children.Add(new System.Windows.Controls.TextBlock { Text = "Service Preset", FontSize = 14, FontWeight = System.Windows.FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
            info.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Apply a service configuration preset — AkariOS disables 166 services (telemetry, Bluetooth, Xbox, Hyper-V guests, search, and more). Defender is always protected.",
                FontSize = 13, Foreground = TweakHelpers.TextSecondary, Margin = new System.Windows.Thickness(0, 2, 0, 0), TextWrapping = System.Windows.TextWrapping.Wrap
            });
            _servicePresetLabel = new System.Windows.Controls.TextBlock { FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new System.Windows.Thickness(0, 4, 0, 0), FontStyle = System.Windows.FontStyles.Italic };
            info.Children.Add(_servicePresetLabel);
            SyncPresetLabel();

            var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new System.Windows.Thickness(12, 0, 0, 0) };
            System.Windows.Controls.Grid.SetColumn(buttons, 1);

            var applyBtn = new System.Windows.Controls.Button { Content = "Apply AkariOS", Style = (System.Windows.Style)FindResource("RunBtn"), Margin = new System.Windows.Thickness(0, 0, 6, 0) };
            applyBtn.Click += async (_, _) => { await ServicesPreset.ApplyAkariGaming(Service!); SyncPresetLabel(); };

            var restoreBtn = new System.Windows.Controls.Button { Content = "Restore AkariOS Windows Default", Style = (System.Windows.Style)FindResource("UndoBtn"), Margin = new System.Windows.Thickness(0, 0, 6, 0) };
            restoreBtn.Click += async (_, _) => { await ServicesPreset.ApplyStockDefault(Service!); SyncPresetLabel(); };

            var stockBtn = new System.Windows.Controls.Button { Content = "Restore Windows Default", Style = (System.Windows.Style)FindResource("UndoBtn") };
            stockBtn.Click += async (_, _) => { stockBtn.IsEnabled = false; try { await ApplyStockWindowsDefaultRegAsync(); SyncPresetLabel(); } finally { stockBtn.IsEnabled = true; } };

            buttons.Children.Add(applyBtn);
            buttons.Children.Add(restoreBtn);
            buttons.Children.Add(stockBtn);
            row.Children.Add(info);
            row.Children.Add(buttons);
            section.Children.Add(row);
        }

        private void SyncPresetLabel()
        {
            if (_servicePresetLabel is null) return;
            var preset = SystemStateReader.DetectServicePreset();
            (_servicePresetLabel.Text, _servicePresetLabel.Foreground) = preset switch
            {
                SystemStateReader.ServicePreset.AkariGaming => ("Current: AkariOS",       TweakHelpers.SuccessFg),
                SystemStateReader.ServicePreset.Stock    => ("Current: Windows Stock", TweakHelpers.Accent),
                SystemStateReader.ServicePreset.Mixed    => ("Current: Mixed",         TweakHelpers.WarnFg),
                _                                        => ("Current: Unknown",       TweakHelpers.TextSecondary),
            };
        }

        private async Task ApplyStockWindowsDefaultRegAsync()
        {
            var candidates = new[]
            {
                Path.Combine(@"C:\PostInstall\Services", "win11_stock_services.reg"),
                Path.Combine(AppContext.BaseDirectory, "Resources", "win11_stock_services.reg"),
            };
            var regPath = candidates.FirstOrDefault(File.Exists);
            if (regPath is null) { Service?.Log("[STOCK] ✗ win11_stock_services.reg not found. Expected at C:\\PostInstall\\Services\\ or <app>\\Resources\\."); return; }
            Service?.Log($"[STOCK] Importing stock Windows 11 service defaults from: {regPath}");
            var psi = new System.Diagnostics.ProcessStartInfo { FileName = "regedit.exe", Arguments = $"/s \"{regPath}\"", UseShellExecute = true, Verb = "runas", CreateNoWindow = false };
            try
            {
                var proc = System.Diagnostics.Process.Start(psi)!;
                await proc.WaitForExitAsync();
                if (proc.ExitCode == 0) Service?.Log("[STOCK] ✓ Stock Windows 11 service defaults restored. Restart to fully apply.");
                else Service?.Log($"[STOCK] ⚠ regedit exited with code {proc.ExitCode}.");
            }
            catch (Exception ex) { Service?.Log($"[STOCK] ERROR: {ex.Message}"); }
        }

    }
}