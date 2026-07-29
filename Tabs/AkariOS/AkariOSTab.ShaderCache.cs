using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AkariTool.Services;

namespace AkariTool.Tabs.AkariOS
{
    public partial class AkariOSTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // SHADER CACHE CLEANER
        // An ACTION section, not a tweak section — nothing here is registered with
        // TweakRegistry or Quick Actions, because there is no persistent state to
        // reflect: clearing a cache is a one-shot operation the OS immediately
        // starts undoing.
        // ══════════════════════════════════════════════════════════════════════

        private sealed class ShaderCacheRow
        {
            public required ShaderCacheTarget Target { get; init; }
            public required CheckBox          Box    { get; init; }
            public required TextBlock         Label  { get; init; }
        }

        private readonly List<ShaderCacheRow> _shaderRows = new();
        private Button?    _shaderRescanBtn;
        private Button?    _shaderCleanBtn;
        private TextBlock? _shaderStatus;
        private Wpf.Ui.Controls.ProgressRing? _shaderRing;
        private bool _shaderScanStarted;

        private void BuildShaderCacheContent(StackPanel panel)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Clears DirectX, NVIDIA, AMD, Intel and Steam per-game shader caches. " +
                       "Games will rebuild shaders on next launch, which may cause brief stutter the first time.",
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // ── One checkbox per target ───────────────────────────────────────
            var boxes = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };

            foreach (var target in ShaderCacheService.GetTargets())
            {
                // Steam only appears when Steam is actually installed — an empty
                // Steam row would just be a permanently greyed "not found".
                if (target.Id == "steam" && !ShaderCacheService.IsSteamInstalled()) continue;

                var label = new TextBlock
                {
                    Text = $"{target.DisplayName} — scanning…",
                    FontSize = 12.5,
                    Foreground = TweakHelpers.TextPrimary,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var box = new CheckBox
                {
                    Style = (Style)Application.Current.Resources["AppCheckBox"],
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 22, 4),
                    Content = label,
                    // Nothing is selectable until the scan says what exists.
                    IsEnabled = false
                };

                boxes.Children.Add(box);
                _shaderRows.Add(new ShaderCacheRow { Target = target, Box = box, Label = label });
            }

            panel.Children.Add(boxes);

            // ── Buttons + spinner ─────────────────────────────────────────────
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            _shaderRescanBtn = new Button
            {
                Content = "Rescan",
                Padding = new Thickness(18, 10, 18, 10),
                Margin  = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                Style = (Style)FindResource("GridBtn"),
                IsEnabled = false
            };
            _shaderRescanBtn.Click += (_, _) => _ = RunShaderScanAsync();
            actions.Children.Add(_shaderRescanBtn);

            _shaderCleanBtn = new Button
            {
                Content = "Clean Now",
                Padding = new Thickness(18, 10, 18, 10),
                Margin  = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                Style = (Style)FindResource("RunBtn"),
                IsEnabled = false
            };
            _shaderCleanBtn.Click += (_, _) => _ = RunShaderCleanAsync();
            actions.Children.Add(_shaderCleanBtn);

            _shaderRing = new Wpf.Ui.Controls.ProgressRing
            {
                IsIndeterminate = true,
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            actions.Children.Add(_shaderRing);

            panel.Children.Add(actions);

            _shaderStatus = new TextBlock
            {
                Text = "Scanning…",
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(_shaderStatus);

            // The first scan is deferred to Loaded so tab construction stays
            // synchronous — enumerating a multi-gigabyte cache would otherwise
            // stall the navigation that created this tab.
            if (IsLoaded) OnShaderCacheLoaded(this, new RoutedEventArgs());
            else          Loaded += OnShaderCacheLoaded;
        }

        private void OnShaderCacheLoaded(object sender, RoutedEventArgs e)
        {
            if (_shaderScanStarted) return;   // Loaded fires again on re-parenting
            _shaderScanStarted = true;
            _ = RunShaderScanAsync();
        }

        // ── Scan ──────────────────────────────────────────────────────────────

        private async Task RunShaderScanAsync()
        {
            SetShaderBusy(true);
            if (_shaderStatus is not null) _shaderStatus.Text = "Scanning…";

            try
            {
                // Re-resolved every scan: a Steam library added since the tab was
                // built would otherwise stay invisible.
                var targets = ShaderCacheService.GetTargets()
                                                .ToDictionary(t => t.Id, StringComparer.Ordinal);

                var toScan = _shaderRows
                    .Select(r => targets.TryGetValue(r.Target.Id, out var t) ? t : r.Target)
                    .ToList();

                var results = (await ShaderCacheService.ScanAsync(toScan))
                              .ToDictionary(r => r.TargetId, StringComparer.Ordinal);

                long total = 0;
                foreach (var row in _shaderRows)
                {
                    if (!results.TryGetValue(row.Target.Id, out var res)) continue;

                    if (res.Exists)
                    {
                        row.Label.Text       = $"{row.Target.DisplayName} — {ShaderCacheService.FormatBytes(res.TotalBytes)}";
                        row.Label.Foreground = TweakHelpers.TextPrimary;
                        row.Box.IsEnabled    = true;
                        row.Box.IsChecked    = true;
                        total += res.TotalBytes;
                    }
                    else
                    {
                        row.Label.Text       = $"{row.Target.DisplayName} — not found";
                        row.Label.Foreground = TweakHelpers.TextSecondary;
                        row.Box.IsChecked    = false;
                        row.Box.IsEnabled    = false;
                    }
                }

                if (_shaderStatus is not null)
                    _shaderStatus.Text = $"{ShaderCacheService.FormatBytes(total)} of shader cache found.";
            }
            catch (Exception ex)
            {
                if (_shaderStatus is not null) _shaderStatus.Text = $"Scan failed: {ex.Message}";
                Service?.Log($"ERROR Shader cache scan: {ex.Message}");
            }
            finally
            {
                SetShaderBusy(false);
            }
        }

        // ── Clean ─────────────────────────────────────────────────────────────

        private async Task RunShaderCleanAsync()
        {
            var selected = _shaderRows.Where(r => r.Box.IsChecked == true).ToList();
            if (selected.Count == 0)
            {
                if (_shaderStatus is not null) _shaderStatus.Text = "Select at least one cache to clean.";
                return;
            }

            // Sizes are measured again right before the prompt so the confirmation
            // quotes what is actually there, not a stale scan.
            SetShaderBusy(true);
            IReadOnlyList<ShaderCacheScanResult> sizes;
            try
            {
                sizes = await ShaderCacheService.ScanAsync(selected.Select(r => r.Target));
            }
            catch
            {
                sizes = Array.Empty<ShaderCacheScanResult>();
            }
            finally
            {
                SetShaderBusy(false);
            }

            long totalBytes = sizes.Sum(s => s.TotalBytes);
            string message =
                "The following shader caches will be cleared:\n\n" +
                string.Join("\n", selected.Select(r => "  • " + r.Target.DisplayName)) +
                $"\n\nAbout {ShaderCacheService.FormatBytes(totalBytes)} will be freed. " +
                "Games will rebuild their shaders on next launch.";

            if (ShaderCacheService.IsSteamRunning())
                message += "\n\nSteam is running. Close Steam and any games before cleaning to avoid errors.";

            if (!await ConfirmShaderCleanAsync(message)) return;

            SetShaderBusy(true);
            try
            {
                var progress = new Progress<string>(text =>
                {
                    if (_shaderStatus is not null) _shaderStatus.Text = text;
                });

                var targets = selected.Select(r => r.Target).ToList();
                var results = await ShaderCacheService.CleanAsync(targets, progress);

                long freed   = results.Sum(r => r.BytesFreed);
                int  deleted = results.Sum(r => r.FilesDeleted);
                int  skipped = results.Sum(r => r.FilesSkipped);
                bool errored = results.Any(r => r.Error is not null);

                string status = $"Freed {ShaderCacheService.FormatBytes(freed)} across {deleted} files.";
                if (skipped > 0) status += $" ({skipped} files in use were skipped.)";
                if (errored)     status += " Some locations could not be accessed.";

                Service?.Log($"Shader cache cleaned — {status}");
                if (_shaderStatus is not null) _shaderStatus.Text = status;

                foreach (var r in results.Where(r => r.Error is not null))
                    Service?.Log($"ERROR Shader cache ({r.TargetId}): {r.Error}");

                // Refresh so the labels drop to ~0 and reflect what is left behind.
                await RunShaderScanAsync();
                if (_shaderStatus is not null) _shaderStatus.Text = status;
            }
            catch (Exception ex)
            {
                if (_shaderStatus is not null) _shaderStatus.Text = $"Clean failed: {ex.Message}";
                Service?.Log($"ERROR Shader cache clean: {ex.Message}");
            }
            finally
            {
                // A finally block, not a trailing call: an exception above must never
                // leave the section permanently disabled.
                SetShaderBusy(false);
            }
        }

        /// <summary>
        /// Clean / Cancel confirmation. Awaited directly rather than going through
        /// AkariDialogs, which pumps a nested dispatcher frame — from an async
        /// handler that would re-enter the UI thread while this method is suspended.
        /// Owner must be set or the WPF-UI MessageBox opens unparented.
        /// </summary>
        private async Task<bool> ConfirmShaderCleanAsync(string message)
        {
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title   = "Clean Shader Caches",
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
                PrimaryButtonText = "Clean",
                CloseButtonText   = "Cancel",
            };

            var owner = Window.GetWindow(this);
            if (owner is not null) box.Owner = owner;

            return await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
        }

        // ── Busy state ────────────────────────────────────────────────────────

        private void SetShaderBusy(bool busy)
        {
            if (_shaderRescanBtn is not null) _shaderRescanBtn.IsEnabled = !busy;
            if (_shaderCleanBtn  is not null) _shaderCleanBtn.IsEnabled  = !busy;
            if (_shaderRing      is not null) _shaderRing.Visibility      = busy ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
