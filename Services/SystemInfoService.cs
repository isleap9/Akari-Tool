using System;
using System.Collections.Generic;
using System.Management;

namespace AkariTool.Services
{
    /// <summary>
    /// Shared registry/WMI system-information lookups. Used by the Home banner and the
    /// Tools tab's System Information card so both read through one implementation.
    /// </summary>
    public static class SystemInfoService
    {
        private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        public readonly record struct SystemInfo(
            string Edition,   // "Windows 11 Pro"
            string Version,   // "Version 25H2 • Build 26200.8037"
            string Cpu,       // "AMD Ryzen 9 9950X3D"
            string Gpu,       // "NVIDIA GeForce RTX 4080"
            string Memory);   // "32 GB @ 6000 MHz"

        /// <summary>Gathers system info. WMI is slow — call from a background thread.</summary>
        public static SystemInfo Gather() =>
            new(GetEdition(), GetVersion(), GetCpu(), GetGpu(), GetMemory());

        // ── Individual fields ─────────────────────────────────────────────

        /// <summary>
        /// Windows family + edition. The registry "ProductName" value still reports
        /// "Windows 10 …" on Windows 11, so the family comes from the build number instead.
        /// </summary>
        public static string GetEdition()
        {
            try
            {
                var build  = GetRegValue(CurrentVersionKey, "CurrentBuildNumber");
                var family = int.TryParse(build, out var n) && n >= 22000 ? "Windows 11" : "Windows 10";

                var editionId = GetRegValue(CurrentVersionKey, "EditionID");
                if (string.IsNullOrWhiteSpace(editionId)) return family;

                var edition = editionId switch
                {
                    "Professional" => "Pro",
                    "Core"         => "Home",
                    "Enterprise"   => "Enterprise",
                    "Education"    => "Education",
                    _              => editionId,
                };
                return $"{family} {edition}";
            }
            catch { return "Unknown"; }
        }

        /// <summary>"Version 25H2 • Build 26200.8037" — each part omitted gracefully when missing.</summary>
        public static string GetVersion()
        {
            try
            {
                var display = GetRegValue(CurrentVersionKey, "DisplayVersion");
                var build   = GetRegValue(CurrentVersionKey, "CurrentBuildNumber");
                var ubr     = GetRegValue(CurrentVersionKey, "UBR");

                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(display)) parts.Add($"Version {display}");
                if (!string.IsNullOrWhiteSpace(build))
                    parts.Add(string.IsNullOrWhiteSpace(ubr) ? $"Build {build}" : $"Build {build}.{ubr}");

                return parts.Count > 0 ? string.Join(" • ", parts) : "Unknown";
            }
            catch { return "Unknown"; }
        }

        public static string GetCpu()
        {
            try
            {
                var cpu = GetRegValue(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString")?.Trim();
                return string.IsNullOrWhiteSpace(cpu) ? "Unknown" : cpu;
            }
            catch { return "Unknown"; }
        }

        /// <summary>First adapter name, with " (+N more)" when the machine has several.</summary>
        public static string GetGpu()
        {
            try
            {
                var gpus = GetWmiValues("Win32_VideoController", "Name");
                if (gpus.Count == 0) return "Unknown";

                var first = gpus[0].Trim();
                return gpus.Count > 1 ? $"{first} (+{gpus.Count - 1} more)" : first;
            }
            catch { return "Unknown"; }
        }

        /// <summary>Total physical memory in whole GB, plus the module speed when available.</summary>
        public static string GetMemory()
        {
            try
            {
                var totalRaw = GetWmiValue("Win32_ComputerSystem", "TotalPhysicalMemory");
                if (!double.TryParse(totalRaw, out var bytes) || bytes <= 0) return "Unknown";

                var gb = (int)Math.Round(bytes / (1024.0 * 1024 * 1024));
                var speed = GetWmiValues("Win32_PhysicalMemory", "Speed")
                    .Find(s => long.TryParse(s, out var v) && v > 0);

                return string.IsNullOrEmpty(speed) ? $"{gb} GB" : $"{gb} GB @ {speed} MHz";
            }
            catch { return "Unknown"; }
        }

        // ── Shared lookup helpers ─────────────────────────────────────────

        public static string? GetRegValue(string subKey, string valueName)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(subKey);
                return key?.GetValue(valueName)?.ToString();
            }
            catch { return null; }
        }

        /// <summary>First value of <paramref name="property"/>, or "" when unavailable.</summary>
        public static string GetWmiValue(string wmiClass, string property, string? where = null)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(BuildQuery(wmiClass, property, where));
                foreach (var obj in searcher.Get())
                    return obj[property]?.ToString() ?? "";
            }
            catch { }
            return "";
        }

        /// <summary>Every non-empty value of <paramref name="property"/> across all instances.</summary>
        public static List<string> GetWmiValues(string wmiClass, string property, string? where = null)
        {
            var results = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(BuildQuery(wmiClass, property, where));
                foreach (var obj in searcher.Get())
                {
                    var val = obj[property]?.ToString();
                    if (!string.IsNullOrWhiteSpace(val)) results.Add(val);
                }
            }
            catch { }
            return results;
        }

        private static string BuildQuery(string wmiClass, string property, string? where) =>
            string.IsNullOrEmpty(where)
                ? $"SELECT {property} FROM {wmiClass}"
                : $"SELECT {property} FROM {wmiClass} WHERE {where}";
    }
}
