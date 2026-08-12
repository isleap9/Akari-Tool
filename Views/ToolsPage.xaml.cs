using System.Diagnostics;
using System.Management;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;
using WinUI.Framework.IoC;

namespace AkariTool.Views;

/// <summary>
/// Tools page — Phase 9 port of net8 ToolsTab. System Information reads are READ-ONLY
/// (registry + WMI, nothing written). Repair/Network/Maintenance buttons call
/// <c>ToolService.RunScript</c> against the already-embedded <c>Scripts/*.ps1</c> (no ps1
/// content modified). Quick Shortcuts shell-execute system tools / ms-settings URIs.
/// Registers nothing.
/// </summary>
public sealed partial class ToolsPage : Page
{
    private readonly ToolService _tool;

    public ToolsPage()
    {
        _tool = ServiceLocator.GetService<ToolService>();
        InitializeComponent();

        BuildSystemInfo();
        BuildRepair();
        BuildNetwork();
        BuildMaintenance();
        BuildShortcuts();
    }

    // ── System Information (read-only) ────────────────────────────────────────

    private void BuildSystemInfo()
    {
        RootPanel.Children.Add(SectionHeading("System Information"));
        var inner = new StackPanel();

        var specs = GatherSpecs();
        var infoText = string.Join("\n", specs.Select(kv => $"{kv.Label,-12}{kv.Value}"));

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12), ColumnSpacing = 16, RowSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < specs.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock { Text = specs[i].Label, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Res("TextFillColorSecondaryBrush"), FontFamily = Mono };
            Grid.SetRow(label, i); Grid.SetColumn(label, 0); grid.Children.Add(label);
            var value = new TextBlock { Text = specs[i].Value, FontSize = 13, Foreground = Res("TextFillColorPrimaryBrush"), FontFamily = Mono, TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(value, i); Grid.SetColumn(value, 1); grid.Children.Add(value);
        }
        inner.Children.Add(grid);

        var copyBtn = MakeButton("Copy to Clipboard", "#e0142a");
        copyBtn.Click += (_, _) =>
        {
            var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
            pkg.SetText(infoText);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
            _tool.Log("[INFO] System info copied to clipboard.");
        };
        inner.Children.Add(copyBtn);

        RootPanel.Children.Add(MakeCard(inner));
    }

    private static List<(string Label, string Value)> GatherSpecs()
    {
        var specs = new List<(string, string)>();

        var edition = SystemInfoService.GetRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID") ?? "Pro";
        var build = SystemInfoService.GetRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber") ?? "0";
        var displayVer = SystemInfoService.GetRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion") ?? "";
        var ubr = SystemInfoService.GetRegValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR") ?? "0";
        var isWin11 = int.TryParse(build, out var buildNum) && buildNum >= 22000;
        var winVer = isWin11 ? "Windows 11" : "Windows 10";
        specs.Add(("OS", $"{winVer} {edition} {displayVer} (Build {build}.{ubr})".Trim()));

        var cpu = SystemInfoService.GetRegValue(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString")?.Trim() ?? "Unknown";
        var coreStr = SystemInfoService.GetWmiValue("Win32_Processor", "NumberOfCores");
        var lCoreStr = SystemInfoService.GetWmiValue("Win32_Processor", "NumberOfLogicalProcessors");
        var mhz = SystemInfoService.GetWmiValue("Win32_Processor", "MaxClockSpeed");
        var ghz = double.TryParse(mhz, out var mhzVal) ? $" @ {mhzVal / 1000.0:F2} GHz" : "";
        var cores = (!string.IsNullOrEmpty(coreStr) && !string.IsNullOrEmpty(lCoreStr)) ? $" ({coreStr}C / {lCoreStr}T)" : "";
        specs.Add(("CPU", $"{cpu}{ghz}{cores}"));

        var ramGb = GetRamGb();
        var ramSpeed = SystemInfoService.GetWmiValue("Win32_PhysicalMemory", "Speed");
        var ramType = GetRamType();
        var ramExtra = new List<string>();
        if (!string.IsNullOrEmpty(ramType)) ramExtra.Add(ramType);
        if (!string.IsNullOrEmpty(ramSpeed)) ramExtra.Add($"{ramSpeed} MHz");
        specs.Add(("RAM", $"{ramGb} GB" + (ramExtra.Count > 0 ? $" {string.Join(" ", ramExtra)}" : "")));

        var gpus = SystemInfoService.GetWmiValues("Win32_VideoController", "Name");
        foreach (var (gpu, i) in gpus.Select((g, i) => (g, i)))
            specs.Add((i == 0 ? "GPU" : $"GPU {i + 1}", gpu.Trim()));

        var moboMfr = SystemInfoService.GetWmiValue("Win32_BaseBoard", "Manufacturer");
        var moboModel = SystemInfoService.GetWmiValue("Win32_BaseBoard", "Product");
        if (!string.IsNullOrEmpty(moboMfr) || !string.IsNullOrEmpty(moboModel))
            specs.Add(("Motherboard", $"{moboMfr} {moboModel}".Trim()));

        foreach (var (drive, i) in GetDriveInfo().Select((d, i) => (d, i)))
            specs.Add((i == 0 ? "Storage" : $"Storage {i + 1}", drive));

        var monitors = SystemInfoService.GetWmiValues("Win32_DesktopMonitor", "Name");
        foreach (var (mon, i) in monitors.Where(m => !string.IsNullOrWhiteSpace(m)).Select((m, i) => (m, i)))
            specs.Add((i == 0 ? "Display" : $"Display {i + 1}", mon.Trim()));

        var nic = SystemInfoService.GetWmiValue("Win32_NetworkAdapter", "Name", "PhysicalAdapter = True");
        if (!string.IsNullOrEmpty(nic)) specs.Add(("Network", nic.Trim()));

        specs.Add(("Activation", GetActivationStatus()));
        return specs;
    }

    private static string GetRamGb()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            long total = 0;
            foreach (var obj in searcher.Get()) total += Convert.ToInt64(obj["Capacity"]);
            return (total / (1024L * 1024 * 1024)).ToString();
        }
        catch { return "?"; }
    }

    private static string GetRamType()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory");
            foreach (var obj in searcher.Get())
                return Convert.ToInt32(obj["SMBIOSMemoryType"]) switch { 26 => "DDR4", 34 => "DDR5", 24 => "DDR3", 20 => "DDR2", _ => "" };
        }
        catch { }
        return "";
    }

    private static List<string> GetDriveInfo()
    {
        var results = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model, Size, MediaType FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                var model = obj["Model"]?.ToString()?.Trim() ?? "Unknown";
                var sizeGb = Convert.ToInt64(obj["Size"] ?? 0L) / (1024L * 1024 * 1024);
                var mediaType = obj["MediaType"]?.ToString() ?? "";
                var typeLabel = mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ? "SSD"
                              : mediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase) ? "HDD"
                              : mediaType.Contains("Removable", StringComparison.OrdinalIgnoreCase) ? "USB" : "";
                results.Add(typeLabel.Length > 0 ? $"{model} ({sizeGb} GB, {typeLabel})" : $"{model} ({sizeGb} GB)");
            }
        }
        catch { }
        return results;
    }

    private static string GetActivationStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LicenseStatus FROM SoftwareLicensingProduct " +
                "WHERE PartialProductKey IS NOT NULL AND ApplicationID = '55c92734-d682-4d71-983e-d6ec3f16059f'");
            foreach (var obj in searcher.Get())
                return Convert.ToInt32(obj["LicenseStatus"]) == 1 ? "Activated" : "Not Activated";
        }
        catch { }
        return "Unknown";
    }

    // ── Repair / Network / Maintenance (RunScript) ────────────────────────────

    private void BuildRepair()
    {
        RootPanel.Children.Add(SectionHeading("Repair & Health"));
        var inner = new StackPanel();
        AddItem(inner, "SFC Scan", "Scan and repair corrupted Windows system files.", "SfcScan.ps1");
        AddDivider(inner);
        AddItem(inner, "DISM Repair", "Check and restore the Windows image component store.", "DismRepair.ps1");
        AddDivider(inner);
        AddItem(inner, "Create Restore Point", "Take a snapshot of your current system state.", "RestorePoint.ps1");
        RootPanel.Children.Add(MakeCard(inner));
    }

    private void BuildNetwork()
    {
        RootPanel.Children.Add(SectionHeading("Network"));
        var inner = new StackPanel();
        AddItem(inner, "Flush DNS Cache", "Clear the local DNS resolver cache.", "FlushDns.ps1");
        AddDivider(inner);
        AddItem(inner, "Reset Network Stack", "Reset Winsock, TCP/IP, release and renew IP. Reboot recommended.", "WinsockReset.ps1");
        AddDivider(inner);

        inner.Children.Add(new TextBlock { Text = "DNS Server", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Res("TextFillColorPrimaryBrush"), Margin = new Thickness(0, 4, 0, 2) });
        inner.Children.Add(new TextBlock { Text = "Switch the DNS provider for all active adapters.", FontSize = 13, Foreground = Res("TextFillColorSecondaryBrush"), Margin = new Thickness(0, 0, 0, 10) });

        var dnsRow = new StackPanel { Orientation = Orientation.Horizontal };
        dnsRow.Children.Add(MakeDnsButton("Cloudflare", "SetDnsCloudflare.ps1", "#ff5e6e"));
        dnsRow.Children.Add(MakeDnsButton("Google", "SetDnsGoogle.ps1", "#a6e3a1"));
        dnsRow.Children.Add(MakeDnsButton("Quad9", "SetDnsQuad9.ps1", "#fab387"));
        dnsRow.Children.Add(MakeDnsButton("Auto (DHCP)", "SetDnsAuto.ps1", "#9A9AA0"));
        inner.Children.Add(dnsRow);
        RootPanel.Children.Add(MakeCard(inner));
    }

    private void BuildMaintenance()
    {
        RootPanel.Children.Add(SectionHeading("Maintenance"));
        var inner = new StackPanel();
        AddItem(inner, "Clear Temp Files", "Delete contents of %TEMP%, Windows\\Temp, and Prefetch.", "TempFiles.ps1");
        AddDivider(inner);
        AddItem(inner, "Disk Cleanup", "Run cleanmgr and DISM component cleanup on C: drive.", "DiskCleanup.ps1");
        AddDivider(inner);
        AddItem(inner, "Rebuild Icon Cache", "Force Explorer to regenerate the icon cache database.", "IconCacheRebuild.ps1");
        RootPanel.Children.Add(MakeCard(inner));
    }

    // ── Quick Shortcuts (shell-execute) ───────────────────────────────────────

    private void BuildShortcuts()
    {
        RootPanel.Children.Add(SectionHeading("Quick Shortcuts"));
        var items = new FrameworkElement[]
        {
            MakeShortcut("Task Manager",     () => Launch("taskmgr")),
            MakeShortcut("Startup Apps",     () => Launch("ms-settings:startupapps")),
            MakeShortcut("MSConfig",         () => Launch("msconfig")),
            MakeShortcut("Device Manager",   () => Launch("devmgmt.msc")),
            MakeShortcut("Event Viewer",     () => Launch("eventvwr.msc")),
            MakeShortcut("Windows Update",   () => Launch("ms-settings:windowsupdate")),
            MakeShortcut("Disk Management",  () => Launch("diskmgmt.msc")),
            MakeShortcut("Services",         () => Launch("services.msc")),
            MakeShortcut("Resource Monitor", () => Launch("resmon")),
            MakeShortcut("Registry Editor",  () => Launch("regedit")),
        };

        const int columns = 2;
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        for (int c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int rows = (items.Length + columns - 1) / columns;
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < items.Length; i++)
        {
            Grid.SetColumn(items[i], i % columns);
            Grid.SetRow(items[i], i / columns);
            grid.Children.Add(items[i]);
        }
        RootPanel.Children.Add(MakeCard(grid));
    }

    private static void Launch(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch { }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private static readonly FontFamily Mono = new("Consolas");

    private static TextBlock SectionHeading(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontFamily = Mono,
        FontSize = 10.5,
        FontWeight = FontWeights.Medium,
        Foreground = Res("TextFillColorTertiaryBrush"),
        Margin = new Thickness(4, 16, 0, 6),
    };

    private static Border MakeCard(UIElement child) => new()
    {
        Background = Res("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = Res("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(20, 16, 20, 16),
        Margin = new Thickness(0, 0, 0, 16),
        Child = child,
    };

    private static void AddDivider(StackPanel panel) => panel.Children.Add(new Border
    {
        Background = Res("CardStrokeColorDefaultBrush"),
        Margin = new Thickness(0, 8, 0, 8),
        Height = 1,
    });

    private static Button MakeButton(string label, string? color = null) => new()
    {
        Content = label,
        Foreground = color is null ? Res("TextFillColorPrimaryBrush") : Hex(color),
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        BorderBrush = Res("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(12, 6, 12, 6),
        Margin = new Thickness(0, 0, 8, 0),
        FontSize = 13,
    };

    private Button MakeDnsButton(string label, string script, string color)
    {
        var btn = MakeButton(label, color);
        btn.Margin = new Thickness(0, 0, 8, 8);
        btn.Click += async (_, _) => await _tool.RunScript(script);
        return btn;
    }

    private static Button MakeShortcut(string label, Action action)
    {
        var btn = new Button
        {
            Content = label,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderBrush = Res("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 8, 8),
            FontSize = 13,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        btn.Click += (_, _) => action();
        return btn;
    }

    private void AddItem(StackPanel panel, string title, string description, string scriptName)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Res("TextFillColorPrimaryBrush") });
        info.Children.Add(new TextBlock { Text = description, FontSize = 13, Foreground = Res("TextFillColorSecondaryBrush"), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(info, 0);

        var runBtn = new Button { Content = "Run", Style = (Style)Application.Current.Resources["AccentButtonStyle"], VerticalAlignment = VerticalAlignment.Center };
        runBtn.Click += async (_, _) => await _tool.RunScript(scriptName);
        Grid.SetColumn(runBtn, 1);

        grid.Children.Add(info);
        grid.Children.Add(runBtn);
        panel.Children.Add(grid);
    }

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    private static Brush Hex(string hex)
    {
        var s = hex.TrimStart('#');
        return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF,
            Convert.ToByte(s.Substring(0, 2), 16), Convert.ToByte(s.Substring(2, 2), 16), Convert.ToByte(s.Substring(4, 2), 16)));
    }
}
