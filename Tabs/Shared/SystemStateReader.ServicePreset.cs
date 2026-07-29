using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AkariTool.Tabs
{
    public static partial class SystemStateReader
    {
        // ═════════════════════════════════════════════════════════════════════
        // SERVICE PRESET DETECTION
        // ═════════════════════════════════════════════════════════════════════

        public enum ServicePreset { Stock, AkariGaming, AkariDaily, Mixed, Unknown }

        /// <summary>Detection result plus the probes that disagree with the winner.</summary>
        public sealed record ServicePresetResult(
            ServicePreset Preset,
            int Matched,
            int Total,
            IReadOnlyList<(string Service, int Actual, int Expected)> Drift);

        // (serviceName, stockValue, akariGamingValue, akariDailyValue)
        // MUST stay in sync with ServicesPreset.Stock.cs and ServicesPreset.AkariOs.cs.
        // Non-discriminating probes (DiagTrack, XblAuthManager — identical across all
        // three presets) were removed: they inflate every score equally and only
        // loosened the total-relative tolerance.
        private static readonly (string Service, int Stock, int Gaming, int Daily)[] _presetProbes =
        {
            // ── Shared Gaming/Daily optimizations ─────────────────────────────
            ("SysMain",             2, 4, 4),
            ("WSearch",             2, 4, 4),
            ("BcastDVRUserService", 3, 4, 4),
            ("WerSvc",              3, 4, 4),
            ("LanmanServer",        2, 4, 4),
            ("LanmanWorkstation",   2, 4, 4),
            ("FontCache",           2, 4, 4),
            ("TrkWks",              2, 4, 4),
            ("BTHPORT",             3, 4, 4),
            ("WpnService",          2, 3, 3), // FIXED: presets clamp to Manual, not Disabled

            // ── Daily-discriminating probes ───────────────────────────────────
            ("ShellHWDetection",    2, 3, 2), // Daily restores Explorer ISO mount
            ("UsoSvc",              2, 4, 2), // Daily restores WU orchestrator
            ("WaaSMedicSvc",        3, 4, 3), // Daily restores WU medic
            ("BITS",                2, 3, 2),
            ("DoSvc",               2, 4, 2),
            ("cdrom",               1, 4, 1),
            ("EventSystem",         2, 4, 2), // FIXED: Daily restores COM+ (SENS / network status)
            ("DusmSvc",             2, 4, 2), // Daily restores Data Usage (Settings connectivity chip)
        };

        /// <summary>Thin wrapper — existing call sites keep working.</summary>
        public static ServicePreset DetectServicePreset() => DetectServicePresetDetailed().Preset;

        /// <summary>
        /// Samples a representative subset of services and determines which preset
        /// is active, reporting which probes drifted so "Mixed" is actionable.
        /// </summary>
        public static ServicePresetResult DetectServicePresetDetailed()
        {
            var probes = new List<(string Service, int Actual, int Stock, int Gaming, int Daily)>();

            foreach (var (service, stockVal, gamingVal, dailyVal) in _presetProbes)
            {
                var actual = ReadDword(RegistryHive.LocalMachine,
                    $@"SYSTEM\CurrentControlSet\Services\{service}", "Start");

                if (actual is null) continue; // service absent on this machine
                probes.Add((service, actual.Value, stockVal, gamingVal, dailyVal));
            }

            int total = probes.Count;
            if (total == 0)
                return new ServicePresetResult(
                    ServicePreset.Unknown, 0, 0, Array.Empty<(string, int, int)>());

            int stockMatches  = probes.Count(p => p.Actual == p.Stock);
            int gamingMatches = probes.Count(p => p.Actual == p.Gaming);
            int dailyMatches  = probes.Count(p => p.Actual == p.Daily);

            var ranked = new[]
            {
                (Preset: ServicePreset.Stock,       Score: stockMatches),
                (Preset: ServicePreset.AkariGaming, Score: gamingMatches),
                (Preset: ServicePreset.AkariDaily,  Score: dailyMatches),
            }
            .OrderByDescending(x => x.Score)
            .ToArray();

            var winner   = ranked[0].Preset;
            int best     = ranked[0].Score;
            int runnerUp = ranked[1].Score;

            static int Expected(ServicePreset p,
                (string Service, int Actual, int Stock, int Gaming, int Daily) probe) => p switch
            {
                ServicePreset.Stock       => probe.Stock,
                ServicePreset.AkariGaming => probe.Gaming,
                _                         => probe.Daily,
            };

            var drift = probes
                .Where(p => p.Actual != Expected(winner, p))
                .Select(p => (p.Service, p.Actual, Expected(winner, p)))
                .ToList();

            // Tolerate up to 2 hand-tweaked services, and require the winner to
            // clear the runner-up by 2 probes so Gaming/Daily can never tie.
            bool decided = best >= total - 2 && best - runnerUp >= 2;

            return new ServicePresetResult(
                decided ? winner : ServicePreset.Mixed, best, total, drift);
        }
    }
}