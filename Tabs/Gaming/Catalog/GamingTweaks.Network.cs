using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public static partial class GamingTweaks
    {
        public static TweakDefinition[] Network(Action<string> Log) => new[]
            {
                new TweakDefinition
                {
                    Id               = "gaming-network-throttling",
                    Name             = "Network Throttling",
                    Description      = "Controls network packet rate limiting for multimedia applications. Keeping throttling enabled (default: 10 packets/ms) provides better DPC latency for gaming",
                    IsPreference     = true,
                    // EnabledValue=[10], DisabledValue=[-1], DefaultValue=10 → toggle ON = throttling ON (10)
                    RecommendedState = true,
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        var v = ReadDword(RegistryHive.LocalMachine,
                            @"Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            "NetworkThrottlingIndex");
                        return v.HasValue ? v == 10 : true;
                    },
                    Apply = on =>
                    {
                        // -1 as uint = 0xFFFFFFFF
                        Registry.SetValue(
                            @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            "NetworkThrottlingIndex", on ? 10 : unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                        Log($"Network Throttling {(on ? "enabled (10)" : "disabled (0xFFFFFFFF)")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-nagle-algorithm",
                    Name             = "Nagle's Algorithm",
                    Description      = "Buffers small network packets before sending to reduce overhead. Turn off to lower latency in online games, or keep on for general-purpose network efficiency",
                    IsPreference     = true,
                    // EnabledValue=[null], DisabledValue=[1], DefaultValue=null; ApplyPerNetworkInterface=true; RecommendedToggleState=true
                    RecommendedState = true,   // recommended=ON (key absent = Nagle enabled)
                    DefaultState     = true,
                    ReadState        = () =>
                    {
                        using var interfacesKey = Registry.LocalMachine.OpenSubKey(
                            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
                        if (interfacesKey == null) return true;
                        foreach (var sub in interfacesKey.GetSubKeyNames())
                        {
                            using var k = interfacesKey.OpenSubKey(sub);
                            if (k?.GetValue("TcpAckFrequency") is int v && v == 1) return false;
                        }
                        return true; // all absent = enabled (default)
                    },
                    Apply = on =>
                    {
                        using var interfacesKey = Registry.LocalMachine.OpenSubKey(
                            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true);
                        if (interfacesKey != null)
                        {
                            foreach (var sub in interfacesKey.GetSubKeyNames())
                            {
                                using var k = interfacesKey.OpenSubKey(sub, true);
                                if (k == null) continue;
                                if (on) { k.DeleteValue("TcpAckFrequency", false); k.DeleteValue("TCPNoDelay", false); }
                                else    { k.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord); k.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord); }
                            }
                        }
                        Log($"Nagle's Algorithm {(on ? "enabled (default)" : "disabled")}. Restart to apply.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "gaming-dns-server",
                    Name             = "DNS Server",
                    Description      = "Select a DNS server for all network adapters (Wi-Fi and Ethernet). DNS over HTTPS options encrypt lookups so your ISP can't inspect them. Automatic restores ISP/router DNS",
                    IsPreference     = true,
                    InputKind        = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Automatic — ISP/Router (Default)",   0, IsDefault: true, IsRecommended: true),
                        new TweakDropdownOption("Cloudflare (1.1.1.1)",               1),
                        new TweakDropdownOption("Cloudflare Malware Block (1.1.1.2)", 2),
                        new TweakDropdownOption("Google (8.8.8.8)",                   3),
                        new TweakDropdownOption("Quad9 (9.9.9.9)",                    4),
                        new TweakDropdownOption("OpenDNS (208.67.222.222)",           5),
                        new TweakDropdownOption("Cloudflare — DNS over HTTPS",        6),
                        new TweakDropdownOption("Google — DNS over HTTPS",            7),
                        new TweakDropdownOption("Quad9 — DNS over HTTPS",             8),
                    },
                    ReadCurrentIndex = () =>
                    {
                        // Check first active adapter DNS
                        using var ifKey = Registry.LocalMachine.OpenSubKey(
                            @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
                        if (ifKey == null) return 0;
                        bool doh = TweakHelpers.HasState("AkariDnsDoH");
                        foreach (var sub in ifKey.GetSubKeyNames())
                        {
                            using var k = ifKey.OpenSubKey(sub);
                            var dns = k?.GetValue("NameServer") as string;
                            if (string.IsNullOrEmpty(dns)) continue;
                            if (dns.StartsWith("1.1.1.1") && dns.Contains("1.0.0.2")) return 2; // malware block
                            if (dns.StartsWith("1.1.1.1")) return doh ? 6 : 1;
                            if (dns.StartsWith("8.8.8.8")) return doh ? 7 : 3;
                            if (dns.StartsWith("9.9.9.9")) return doh ? 8 : 4;
                            if (dns.StartsWith("208.67")) return 5;
                            return 0;
                        }
                        // No adapter has a manual NameServer → DNS was reset outside
                        // Akari Tool (e.g. Windows Settings); drop any stale DoH flag
                        if (doh) TweakHelpers.ClearState("AkariDnsDoH");
                        return 0; // no DNS set = automatic
                    },
                    ApplyIndex = idx =>
                    {
                        // (primary, secondary, DoH template) per option — DoH templates
                        // registered via `netsh dns add encryption` (Winhance approach)
                        (string p, string s, string? doh)? cfg = idx switch
                        {
                            1 => ("1.1.1.1", "1.0.0.1", null),
                            2 => ("1.1.1.2", "1.0.0.2", null),
                            3 => ("8.8.8.8", "8.8.4.4", null),
                            4 => ("9.9.9.9", "149.112.112.112", null),
                            5 => ("208.67.222.222", "208.67.220.220", null),
                            6 => ("1.1.1.1", "1.0.0.1", "https://cloudflare-dns.com/dns-query"),
                            7 => ("8.8.8.8", "8.8.4.4", "https://dns.google/dns-query"),
                            8 => ("9.9.9.9", "149.112.112.112", "https://dns.quad9.net/dns-query"),
                            _ => null
                        };

                        // Always sweep the DoH encryption table for every server we may
                        // have previously registered — keeps netsh state clean when
                        // switching between options or back to Automatic.
                        const string sweep =
                            "$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); " +
                            "foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; ";

                        string script;
                        if (cfg == null)
                        {
                            script = sweep +
                                "Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ResetServerAddresses }; " +
                                "Clear-DnsClientCache";
                        }
                        else
                        {
                            var (p, sec, doh) = cfg.Value;
                            script = sweep +
                                (doh != null
                                    ? $"netsh dns add encryption server={p} dohtemplate={doh} autoupgrade=yes udpfallback=no | Out-Null; " +
                                      $"netsh dns add encryption server={sec} dohtemplate={doh} autoupgrade=yes udpfallback=no | Out-Null; "
                                    : "") +
                                $"Get-NetAdapter | ForEach-Object {{ Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('{p}','{sec}') }}; " +
                                "Clear-DnsClientCache";
                        }

                        TweakHelpers.RunCommand("powershell.exe",
                            $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"");

                        if (cfg?.doh != null) TweakHelpers.SaveState("AkariDnsDoH");
                        else                  TweakHelpers.ClearState("AkariDnsDoH");

                        Log(cfg == null
                            ? "DNS reset to Automatic (DHCP) on all adapters."
                            : $"DNS set to {cfg.Value.p}, {cfg.Value.s}{(cfg.Value.doh != null ? " with DNS over HTTPS" : "")}. " +
                              "Requires running as administrator to take effect.");
                    }
                },
            };
    }
}
