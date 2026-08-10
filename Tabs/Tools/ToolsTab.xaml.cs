using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class ToolsTab : BaseTab
    {
        public ToolsTab() => InitializeComponent();

        public override string NavTag   => "Tools";
        public override string NavLabel => "Tools";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Tools", "System utilities, repair tools, and quick shortcuts."));

            BuildSystemInfo(RootPanel);
            BuildRepair(RootPanel);
            BuildNetwork(RootPanel);
            BuildMaintenance(RootPanel);
            BuildShortcuts(RootPanel);
        }

        // ── System Info ────────────────────────────────────────────────────────

        private void BuildSystemInfo(StackPanel panel)
        {
            panel.Children.Add(MakeSectionHeading("System Information"));
            var card = MakeCard();
            var inner = (StackPanel)card.Child;

            var specs    = GatherSpecs();
            var infoText = string.Join("\n", specs.Select(kv => $"{kv.Label,-12}{kv.Value}"));

            // Two-column label/value grid
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int rowIdx = 0;
            foreach (var (label, value) in specs)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var labelBlock = new TextBlock
                {
                    Text       = label,
                    FontSize   = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TweakHelpers.IconNeutral,
                    FontFamily = new FontFamily("Consolas"),
                    Margin     = new Thickness(0, 2, 16, 2)
                };
                Grid.SetRow(labelBlock, rowIdx);
                Grid.SetColumn(labelBlock, 0);

                var valueBlock = new TextBlock
                {
                    Text         = value,
                    FontSize     = 13,
                    Foreground   = TweakHelpers.TextPrimary,
                    FontFamily   = new FontFamily("Consolas"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin       = new Thickness(0, 2, 0, 2)
                };
                Grid.SetRow(valueBlock, rowIdx);
                Grid.SetColumn(valueBlock, 1);

                grid.Children.Add(labelBlock);
                grid.Children.Add(valueBlock);
                rowIdx++;
            }
            inner.Children.Add(grid);

            var copyBtn = MakeButton("Copy to Clipboard", "#e0142a");
            copyBtn.Click += (_, _) =>
            {
                // WinUI: WPF's static Clipboard.SetText is replaced by the WinRT
                // DataPackage/Clipboard API (same user-visible behaviour).
                var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
                pkg.SetText(infoText);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
                Service?.Log("[INFO] System info copied to clipboard.");
            };
            inner.Children.Add(copyBtn);

            panel.Children.Add(card);
        }

        private static List<(string Label, string Value)> GatherSpecs()
        {
            var specs = new List<(string, string)>();

            // OS
            var edition    = GetRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID") ?? "Pro";
            var build      = GetRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber") ?? "0";
            var displayVer = GetRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion") ?? "";
            var ubr        = GetRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR") ?? "0";
            var isWin11    = int.TryParse(build, out var buildNum) && buildNum >= 22000;
            var winVer     = isWin11 ? "Windows 11" : "Windows 10";
            specs.Add(("OS", $"{winVer} {edition} {displayVer} (Build {build}.{ubr})".Trim()));

            // CPU
            var cpu      = GetRegValue(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString")?.Trim() ?? "Unknown";
            var coreStr  = GetWmiValue("Win32_Processor", "NumberOfCores");
            var lCoreStr = GetWmiValue("Win32_Processor", "NumberOfLogicalProcessors");
            var mhz      = GetWmiValue("Win32_Processor", "MaxClockSpeed");
            var ghz      = double.TryParse(mhz, out var mhzVal) ? $" @ {mhzVal / 1000.0:F2} GHz" : "";
            var cores    = (!string.IsNullOrEmpty(coreStr) && !string.IsNullOrEmpty(lCoreStr))
                           ? $" ({coreStr}C / {lCoreStr}T)" : "";
            specs.Add(("CPU", $"{cpu}{ghz}{cores}"));

            // RAM
            var ramGb    = GetRamGb();
            var ramSpeed = GetWmiValue("Win32_PhysicalMemory", "Speed");
            var ramType  = GetRamType();
            var ramExtra = new List<string>();
            if (!string.IsNullOrEmpty(ramType))  ramExtra.Add(ramType);
            if (!string.IsNullOrEmpty(ramSpeed)) ramExtra.Add($"{ramSpeed} MHz");
            specs.Add(("RAM", $"{ramGb} GB" + (ramExtra.Count > 0 ? $" {string.Join(" ", ramExtra)}" : "")));

            // GPU(s)
            var gpus = GetWmiValues("Win32_VideoController", "Name");
            foreach (var (gpu, i) in gpus.Select((g, i) => (g, i)))
                specs.Add((i == 0 ? "GPU" : $"GPU {i + 1}", gpu.Trim()));

            // Motherboard
            var moboMfr   = GetWmiValue("Win32_BaseBoard", "Manufacturer");
            var moboModel = GetWmiValue("Win32_BaseBoard", "Product");
            if (!string.IsNullOrEmpty(moboMfr) || !string.IsNullOrEmpty(moboModel))
                specs.Add(("Motherboard", $"{moboMfr} {moboModel}".Trim()));

            // Storage
            var drives = GetDriveInfo();
            foreach (var (drive, i) in drives.Select((d, i) => (d, i)))
                specs.Add((i == 0 ? "Storage" : $"Storage {i + 1}", drive));

            // Displays
            var monitors = GetWmiValues("Win32_DesktopMonitor", "Name");
            foreach (var (mon, i) in monitors.Where(m => !string.IsNullOrWhiteSpace(m)).Select((m, i) => (m, i)))
                specs.Add((i == 0 ? "Display" : $"Display {i + 1}", mon.Trim()));

            // Network adapter
            var nic = GetWmiValue("Win32_NetworkAdapter", "Name", "PhysicalAdapter = True");
            if (!string.IsNullOrEmpty(nic))
                specs.Add(("Network", nic.Trim()));

            // Activation
            specs.Add(("Activation", GetActivationStatus()));

            return specs;
        }

        // Reg/WMI lookups are shared with the Home banner — see SystemInfoService.
        private static string? GetRegValue(string subKey, string valueName) =>
            SystemInfoService.GetRegValue(subKey, valueName);

        private static string GetWmiValue(string wmiClass, string property, string? where = null) =>
            SystemInfoService.GetWmiValue(wmiClass, property, where);

        private static List<string> GetWmiValues(string wmiClass, string property, string? where = null) =>
            SystemInfoService.GetWmiValues(wmiClass, property, where);

        private static string GetRamType()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory");
                foreach (var obj in searcher.Get())
                {
                    return Convert.ToInt32(obj["SMBIOSMemoryType"]) switch
                    {
                        26 => "DDR4",
                        34 => "DDR5",
                        24 => "DDR3",
                        20 => "DDR2",
                        _  => ""
                    };
                }
            }
            catch { }
            return "";
        }

        private static List<string> GetDriveInfo()
        {
            var results = new List<string>();
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Model, Size, MediaType FROM Win32_DiskDrive");
                foreach (var obj in searcher.Get())
                {
                    var model     = obj["Model"]?.ToString()?.Trim() ?? "Unknown";
                    var sizeBytes = Convert.ToInt64(obj["Size"] ?? 0L);
                    var sizeGb    = sizeBytes / (1024L * 1024 * 1024);
                    var mediaType = obj["MediaType"]?.ToString() ?? "";
                    var typeLabel = mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ? "SSD"
                                  : mediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase) ? "HDD"
                                  : mediaType.Contains("Removable", StringComparison.OrdinalIgnoreCase) ? "USB"
                                  : "";
                    results.Add(typeLabel.Length > 0
                        ? $"{model} ({sizeGb} GB, {typeLabel})"
                        : $"{model} ({sizeGb} GB)");
                }
            }
            catch { }
            return results;
        }

        private static string GetActivationStatus()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT LicenseStatus FROM SoftwareLicensingProduct " +
                    "WHERE PartialProductKey IS NOT NULL AND ApplicationID = '55c92734-d682-4d71-983e-d6ec3f16059f'");
                foreach (var obj in searcher.Get())
                    return Convert.ToInt32(obj["LicenseStatus"]) == 1 ? "Activated" : "Not Activated";
            }
            catch { }
            return "Unknown";
        }

        // ── Repair & Health ────────────────────────────────────────────────────

        private void BuildRepair(StackPanel panel)
        {
            panel.Children.Add(MakeSectionHeading("Repair & Health"));
            var card = MakeCard();
            var inner = (StackPanel)card.Child;

            AddItem(inner, "SFC Scan",            "Scan and repair corrupted Windows system files.",               "SfcScan.ps1");
            AddDivider(inner);
            AddItem(inner, "DISM Repair",          "Check and restore the Windows image component store.",          "DismRepair.ps1");
            AddDivider(inner);
            AddItem(inner, "Create Restore Point", "Take a snapshot of your current system state.",                 "RestorePoint.ps1");

            panel.Children.Add(card);
        }

        // ── Network ────────────────────────────────────────────────────────────

        private void BuildNetwork(StackPanel panel)
        {
            panel.Children.Add(MakeSectionHeading("Network"));
            var card = MakeCard();
            var inner = (StackPanel)card.Child;

            AddItem(inner, "Flush DNS Cache",    "Clear the local DNS resolver cache.",                               "FlushDns.ps1");
            AddDivider(inner);
            AddItem(inner, "Reset Network Stack","Reset Winsock, TCP/IP, release and renew IP. Reboot recommended.", "WinsockReset.ps1");
            AddDivider(inner);

            inner.Children.Add(new TextBlock
            {
                Text       = "DNS Server",
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                Foreground = TweakHelpers.TextPrimary,
                Margin     = new Thickness(0, 4, 0, 2)
            });
            inner.Children.Add(new TextBlock
            {
                Text       = "Switch the DNS provider for all active adapters.",
                FontSize   = 13,
                Foreground = TweakHelpers.TextSecondary,
                Margin     = new Thickness(0, 0, 0, 10)
            });

            // WinUI ships no in-box WrapPanel; these 4 short DNS buttons fit one row,
            // so a horizontal StackPanel is behaviourally equivalent here.
            var dnsRow = new StackPanel { Orientation = Orientation.Horizontal };
            dnsRow.Children.Add(MakeDnsButton("Cloudflare",  "SetDnsCloudflare.ps1", "#ff5e6e"));
            dnsRow.Children.Add(MakeDnsButton("Google",      "SetDnsGoogle.ps1",     "#a6e3a1"));
            dnsRow.Children.Add(MakeDnsButton("Quad9",       "SetDnsQuad9.ps1",      "#fab387"));
            dnsRow.Children.Add(MakeDnsButton("Auto (DHCP)", "SetDnsAuto.ps1",       "#9A9AA0"));
            inner.Children.Add(dnsRow);

            panel.Children.Add(card);
        }

        // ── Maintenance ────────────────────────────────────────────────────────

        private void BuildMaintenance(StackPanel panel)
        {
            panel.Children.Add(MakeSectionHeading("Maintenance"));
            var card = MakeCard();
            var inner = (StackPanel)card.Child;

            AddItem(inner, "Clear Temp Files",     "Delete contents of %TEMP%, Windows\\Temp, and Prefetch.",     "TempFiles.ps1");
            AddDivider(inner);
            AddItem(inner, "Disk Cleanup",          "Run cleanmgr and DISM component cleanup on C: drive.",       "DiskCleanup.ps1");
            AddDivider(inner);
            AddItem(inner, "Rebuild Icon Cache",    "Force Explorer to regenerate the icon cache database.",       "IconCacheRebuild.ps1");

            panel.Children.Add(card);
        }

        // ── Quick Shortcuts ────────────────────────────────────────────────────

        private void BuildShortcuts(StackPanel panel)
        {
            panel.Children.Add(MakeSectionHeading("Quick Shortcuts"));
            var card = MakeCard();
            var inner = (StackPanel)card.Child;

            // WinUI ships no UniformGrid — an equivalent 2-column Grid is built by hand
            // (same visual result: items fill left-to-right, top-to-bottom).
            var items = new FrameworkElement[]
            {
                MakeShortcutButton("Task Manager",    () => Launch("taskmgr")),
                MakeShortcutButton("Startup Apps",    () => LaunchMs("ms-settings:startupapps")),
                MakeShortcutButton("MSConfig",        () => Launch("msconfig")),
                MakeShortcutButton("Device Manager",  () => Launch("devmgmt.msc")),
                MakeShortcutButton("Event Viewer",    () => Launch("eventvwr.msc")),
                MakeShortcutButton("Windows Update",  () => LaunchMs("ms-settings:windowsupdate")),
                MakeShortcutButton("Disk Management", () => Launch("diskmgmt.msc")),
                MakeShortcutButton("Services",        () => Launch("services.msc")),
                MakeShortcutButton("Resource Monitor",() => Launch("resmon")),
                MakeShortcutButton("Registry Editor", () => Launch("regedit")),
            };

            const int columns = 2;
            var grid = new Grid();
            for (int c = 0; c < columns; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int rows = (items.Length + columns - 1) / columns;
            for (int r = 0; r < rows; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < items.Length; i++)
            {
                Grid.SetColumn(items[i], i % columns);
                Grid.SetRow(items[i], i / columns);
                grid.Children.Add(items[i]);
            }

            inner.Children.Add(grid);
            panel.Children.Add(card);
        }


        // ── UI Helpers ─────────────────────────────────────────────────────────


        private static FrameworkElement MakeSectionHeading(string text)
        {
            return new TextBlock
            {
                Text       = text.ToUpperInvariant(),
                FontFamily = (FontFamily)(Application.Current.Resources["MonoFont"] ?? new FontFamily("Consolas")),
                FontSize   = 10.5,
                FontWeight = FontWeights.Medium,
                Foreground = TweakHelpers.TextMuted,
                Margin     = new Thickness(4, 16, 0, 6)
            };
        }

        private static Border MakeCard()
        {
            return new Border
            {
                Background      = TweakHelpers.CardBackground(),
                BorderBrush     = TweakHelpers.Token("AkariOverlayMedium"),
                BorderThickness = new Thickness(1),
                CornerRadius    = TweakHelpers.CardRadius,
                Padding         = new Thickness(20, 16, 20, 16),
                Margin          = new Thickness(0, 0, 0, 16),
                // Effect = TweakHelpers.CardShadow() dropped — no WinUI Effect
                // (card shadows return in the post-migration cosmetic pass).
                Child           = new StackPanel()
            };
        }

        private static void AddDivider(StackPanel panel)
        {
            panel.Children.Add(new Border
            {
                Background = TweakHelpers.Hairline,
                Margin     = new Thickness(0, 8, 0, 8),
                Height     = 1
            });
        }

        private static Button MakeButton(string label, string? color = null)
        {
            return new Button
            {
                Content         = label,
                // null = the live-updating token; an explicit colour stays literal.
                Foreground      = color is null ? TweakHelpers.TextPrimary : BrushFrom(color),
                // WPF "GridBtn" style dropped (no longer defined) — native WinUI Button
                // chrome with a transparent fill + Akari hairline, same intent.
                Background      = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush     = TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(12, 6, 12, 6),
                Margin          = new Thickness(0, 0, 8, 0),
                FontSize        = 13,
            };
        }

        private Button MakeDnsButton(string label, string script, string color)
        {
            var btn = MakeButton(label, color);
            btn.Margin = new Thickness(0, 0, 8, 8);
            btn.Click += async (_, _) => await Service!.RunScript(script);
            return btn;
        }

        private Button MakeShortcutButton(string label, Action action)
        {
            var btn = new Button
            {
                Content         = label,
                Foreground      = TweakHelpers.TextPrimary,
                // WPF "GridBtn" style dropped — native WinUI Button chrome.
                Background      = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush     = TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(10, 8, 10, 8),
                Margin          = new Thickness(0, 0, 8, 8),
                FontSize        = 13,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            btn.Click += (_, _) => action();
            return btn;
        }

        private void AddItem(StackPanel panel, string title, string description, string scriptName)
        {
            var row = new Grid { Margin = new Thickness(0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(info, 0);
            info.Children.Add(new TextBlock
            {
                Text       = title,
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                Foreground = TweakHelpers.TextPrimary
            });
            info.Children.Add(new TextBlock
            {
                Text         = description,
                FontSize     = 13,
                Foreground   = TweakHelpers.TextSecondary,
                TextWrapping = TextWrapping.Wrap
            });

            var runBtn = new Button
            {
                Content           = "Run",
                Style             = (Style)Application.Current.Resources["AccentButtonStyle"],
                VerticalAlignment = VerticalAlignment.Center
            };
            runBtn.Click += async (_, _) => await Service!.RunScript(scriptName);
            Grid.SetColumn(runBtn, 1);

            row.Children.Add(info);
            row.Children.Add(runBtn);
            panel.Children.Add(row);
        }

        // ── System helpers ─────────────────────────────────────────────────────

        private static string GetRamGb()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Capacity FROM Win32_PhysicalMemory");
                long total = 0;
                foreach (var obj in searcher.Get())
                    total += Convert.ToInt64(obj["Capacity"]);
                return (total / (1024L * 1024 * 1024)).ToString();
            }
            catch { return "?"; }
        }

        private static void Launch(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch { }
        }

        private static void LaunchMs(string uri)
        {
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
            catch { }
        }
    }
}