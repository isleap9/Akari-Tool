using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.AkariOS
{
    public partial class AkariOSTab
    {
        // ── NVIDIA ────────────────────────────────────────────────────────

        private void BuildNvidiaContent(StackPanel panel)
        {
            AddButtonGrid(panel, new (string, Action)[]
            {
                ("Apply NVIDIA Profile",    () => _ = NvidiaProfileService.ApplyAkariProfileAsync(Service!)),
                ("P-State 0",               () => GpuTweaks.SetPState0(Service!.Log)),
                ("Disable ECC",             () => GpuTweaks.DisableEcc(Service!.Log)),
                ("Disable Telemetry",       () => GpuTweaks.DisableNvidiaTelemetry(Service!.Log)),
                ("Unrestrict Clock Policy", () => GpuTweaks.UnrestrictClockPolicy(Service!.Log)),
                ("NVCleanstall",            () => _ = ToolFetchService.LaunchAsync("NVCleanstall", Service!)),
                ("Miscellaneous",           ApplyNvidiaMisc),
            });
        }

        private void ApplyNvidiaMisc()
        {
            try
            {
                const string gpuClass = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000";
                bool ok = ElevationService.RunAsSystem(() =>
                {
                Registry.SetValue(gpuClass, "RmDisableHwFaultBuffer",     1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMD3Feature",                1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMDisableGpuASPMFlags",      3,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMBlcg",                     286331153,  RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMElcg",                     1431655765, RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMElpg",                     4095,       RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMFspg",                     15,         RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMSlcg",                     262143,     RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "EnableRuntimePowerManagement", 0,        RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "DisableOverlay",              1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "D3PCLatency",                 1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "F1TransitionLatency",         1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "Node3DLowLatency",            1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "PciLatencyTimerControl",      1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMDeepL1EntryLatencyUsec",    1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RmGspcMaxFtuS",               1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RmGspcMinFtuS",               1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RmGspcPerioduS",              1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMLpwrEiIdleThresholdUs",     1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMLpwrGrIdleThresholdUs",     1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMLpwrGrRgIdleThresholdUs",   1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RMLpwrMsIdleThresholdUs",     1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "PreferSystemMemoryContiguous", 1,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "TCCSupported",                 0,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "RmCacheLoc",                  0,          RegistryValueKind.DWord);
                Registry.SetValue(gpuClass, "TrackResetEngine",             0,          RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm\FTS", "EnableRID61684", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "DisplayPowerSaving",          0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "RmGpsPsEnablePerCpuCoreDpc",  1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "DisableWriteCombining",        1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "LogWarningEntries",            0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "LogPagingEntries",             0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "LogEventEntries",              0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\nvlddmkm", "LogErrorEntries",              0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "PlatformSupportMiracast", 0, RegistryValueKind.DWord);
                }, Service!.Log);

                if (ok) Service?.Log("NVIDIA Miscellaneous tweaks applied. Restart to apply.");
                else    Service?.Log("NVIDIA Miscellaneous: SYSTEM elevation failed — no values written.");
            }
            catch (Exception ex) { Service?.Log($"ERROR ApplyNvidiaMisc: {ex.Message}"); }
        }

        // ── AMD ───────────────────────────────────────────────────────────

        private void BuildAmdContent(StackPanel panel)
        {
            AddButtonGrid(panel, new (string, Action)[]
            {
                ("DWORDS",                () => GpuTweaks.ApplyAmdDwords(Service!.Log)),
                ("RSS",                   () => _ = ToolFetchService.LaunchAsync("RadeonSlimmer",   Service!)),
                ("Driver Download",       () => RunShellProcess("https://www.amd.com/en/support/download/drivers.html", "")),
                ("Disable DXNAVI",        () => _ = ToolFetchService.LaunchAsync("DisableDx11Navi", Service!)),
            });

            // Shader Cache is a genuine two-state setting, so it renders as a stateful
            // segmented control that reads the live value on build rather than a pair of
            // fire-and-forget buttons.
            BuildAmdShaderCacheControl(panel);
        }

        // ── AMD Shader Cache: stateful segmented control ──────────────────
        //
        // 0x32 => AlwaysON, 0x31 => Default, absent => unset. The read path uses
        // Registry.GetValue (no elevation needed); only the write goes through SYSTEM.
        private const string AmdShaderCacheKey =
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000\UMD";

        private enum ShaderCacheState { Unset, Default, AlwaysOn }

        private static ShaderCacheState ReadAmdShaderCache()
        {
            try
            {
                if (Registry.GetValue(AmdShaderCacheKey, "ShaderCache", null) is byte[] b && b.Length > 0)
                    return b[0] switch
                    {
                        0x32 => ShaderCacheState.AlwaysOn,
                        0x31 => ShaderCacheState.Default,
                        _    => ShaderCacheState.Unset
                    };
            }
            catch { /* unreadable key reads as Unset, never throws */ }
            return ShaderCacheState.Unset;
        }

        /// <returns>true when the value was written under SYSTEM elevation.</returns>
        private bool ApplyAmdShaderCache(bool alwaysOn)
        {
            try
            {
                bool ok = ElevationService.RunAsSystem(() =>
                {
                    Registry.SetValue(AmdShaderCacheKey, "ShaderCache",
                        alwaysOn ? new byte[] { 0x32, 0x00 } : new byte[] { 0x31, 0x00 },
                        RegistryValueKind.Binary);
                }, Service!.Log);

                if (ok) Service?.Log($"AMD Shader Cache set to {(alwaysOn ? "AlwaysON" : "Default")}.");
                else    Service?.Log("AMD Shader Cache: SYSTEM elevation failed — nothing written.");
                return ok;
            }
            catch (Exception ex)
            {
                Service?.Log($"ERROR ApplyAmdShaderCache: {ex.Message}");
                return false;
            }
        }

        private void BuildAmdShaderCacheControl(StackPanel panel)
        {
            var caption = new TextBlock
            {
                Text = "Shader Cache",
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                Margin = new Thickness(4, 8, 4, 6)
            };
            panel.Children.Add(caption);

            // Two segments sharing one hairline track; each rounds only its outer corners.
            var track = new Grid { Margin = new Thickness(4, 0, 4, 4) };
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var (onSeg,  onText)  = MakeSegment("AlwaysON", new CornerRadius(8, 0, 0, 8), new Thickness(1));
            var (offSeg, offText) = MakeSegment("Default",  new CornerRadius(0, 8, 8, 0), new Thickness(0, 1, 1, 1));
            Grid.SetColumn(onSeg, 0);
            Grid.SetColumn(offSeg, 1);
            track.Children.Add(onSeg);
            track.Children.Add(offSeg);
            panel.Children.Add(track);

            void Paint(Border seg, TextBlock txt, bool active)
            {
                seg.Background  = active ? TweakHelpers.Accent : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                txt.Foreground  = active ? TweakHelpers.AccentText : TweakHelpers.TextSecondary;
                txt.FontWeight  = active ? FontWeights.SemiBold : FontWeights.Normal;
                seg.Tag         = active;   // remembered so hover doesn't fight the active fill
            }

            void Refresh()
            {
                var s = ReadAmdShaderCache();
                Paint(onSeg,  onText,  s == ShaderCacheState.AlwaysOn);
                Paint(offSeg, offText, s == ShaderCacheState.Default);
            }

            void Wire(Border seg, TextBlock txt, bool alwaysOn)
            {
                seg.PointerEntered += (_, _) =>
                {
                    if (seg.Tag is bool active && !active) seg.Background = TweakHelpers.CardBgHover;
                };
                seg.PointerExited += (_, _) =>
                {
                    if (seg.Tag is bool active && !active) seg.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                };
                seg.Tapped += (_, _) =>
                {
                    if (ApplyAmdShaderCache(alwaysOn)) Refresh();
                };
            }

            Wire(onSeg,  onText,  true);
            Wire(offSeg, offText, false);
            Refresh();
        }

        private static (Border seg, TextBlock text) MakeSegment(string label, CornerRadius radius, Thickness border)
        {
            var text = new TextBlock
            {
                Text = label,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var seg = new Border
            {
                Child = text,
                Padding = new Thickness(0, 10, 0, 10),
                CornerRadius = radius,
                BorderThickness = border,
                BorderBrush = TweakHelpers.CardElevationBorder,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };
            return (seg, text);
        }

        // ── Useful Tools ──────────────────────────────────────────────────

        private void BuildToolsContent(StackPanel panel)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Tools are downloaded once and cached locally. First launch requires an internet connection.",
                FontSize = 12,
                Foreground = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 0, 4, 6)
            });

            AddButtonGrid(panel, new (string, Action)[]
            {
                ("Autoruns",         () => _ = ToolFetchService.LaunchAsync("Autoruns",          Service!)),
                ("Devmanview",       () => _ = ToolFetchService.LaunchAsync("DevManView",        Service!)),
                ("Serviwin",         () => _ = ToolFetchService.LaunchAsync("ServiWin",          Service!)),
                ("InSpectre",        () => _ = ToolFetchService.LaunchAsync("InSpectre",         Service!)),
                ("MouseTester",      () => _ = ToolFetchService.LaunchAsync("MouseTester",       Service!)),
                ("CRU",              () => _ = ToolFetchService.LaunchAsync("CRU",               Service!)),
                ("AUTO DSCP",        () => _ = ToolFetchService.LaunchAsync("AutoDSCP",          Service!)),
                ("DISM++",           () => _ = ToolFetchService.LaunchAsync("DismPP",            Service!)),
                ("Dev. Cleanup",     () => _ = ToolFetchService.LaunchAsync("DeviceCleanup",     Service!)),
                ("Interrupt AFPT",   () => _ = ToolFetchService.LaunchAsync("InterruptAffinity", Service!)),
                ("HIDUSB",           () => _ = ToolFetchService.LaunchAsync("HidUsbF",           Service!)),
                ("MeasureSleep",     () => _ = ToolFetchService.LaunchAsync("MeasureSleep",      Service!)),
                ("Process Explorer", () => _ = ToolFetchService.LaunchAsync("ProcessExplorer",   Service!)),
                ("ReservedCPUSets",  () => _ = ToolFetchService.LaunchAsync("ReservedCpuSets",   Service!)),
            });
        }

        private void AddButtonGrid(StackPanel parent, (string Label, Action Action)[] buttons)
        {
            const int cols = 3;
            int rows = (int)Math.Ceiling(buttons.Length / (double)cols);
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            for (int c = 0; c < cols; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < rows; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, action) = buttons[i];
                var btn = new Button
                {
                    Content = label,
                    Margin = new Thickness(4),
                    Padding = new Thickness(0, 10, 0, 10),
                    FontSize = 13
                };
                btn.Click += (_, _) =>
                {
                    try { action(); }
                    catch (Exception ex) { Service?.Log($"ERROR {label}: {ex.Message}"); }
                };
                Grid.SetColumn(btn, i % cols);
                Grid.SetRow(btn, i / cols);
                grid.Children.Add(btn);
            }
            parent.Children.Add(grid);
        }
    }
}