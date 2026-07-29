namespace AkariTool.Tabs.Power
{
    public partial class PowerTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // HARDWARE / DRIVER SUPPORT PROBES
        //
        // A power setting only appears under `powercfg /q` when the platform or the
        // vendor driver registered it. Probing gates conditional rows: absent →
        // the row is never added (no disabled ghost rows). Cached per session so
        // building the tab does not re-shell powercfg for every row.
        //
        // Note: presence in powercfg is NOT proof of hardware — Windows registers
        // the battery subgroup on desktops too, which is why BuildBattery leads
        // with GetSystemPowerStatus rather than trusting a probe alone.
        // ══════════════════════════════════════════════════════════════════════

        private sealed record PowerSettingProbe(bool Exists, (uint Index, string Label)[] Options);

        private static readonly Dictionary<string, PowerSettingProbe> _probeCache =
            new(StringComparer.OrdinalIgnoreCase);

        private static PowerSettingProbe ProbePowerSetting(string subgroupGuid, string settingGuid)
        {
            var cacheKey = $"{subgroupGuid}|{settingGuid}";
            if (_probeCache.TryGetValue(cacheKey, out var cached)) return cached;

            string target = ResolveSchemeTarget() ?? "SCHEME_CURRENT";
            var (exit, output) = RunPowerCfgCapture($"/QUERY {target} {subgroupGuid} {settingGuid}");

            bool exists = exit == 0 && output.Contains(settingGuid, StringComparison.OrdinalIgnoreCase);
            var probe = new PowerSettingProbe(
                exists,
                exists ? ParsePossibleSettings(output) : Array.Empty<(uint, string)>());

            _probeCache[cacheKey] = probe;
            return probe;
        }

        /// <summary>True when the platform/driver exposes this power setting.</summary>
        private static bool PowerSettingExists(string subgroupGuid, string settingGuid) =>
            ProbePowerSetting(subgroupGuid, settingGuid).Exists;

        /// <summary>
        /// Parses the "Possible Setting Index / Possible Setting Friendly Name" pairs
        /// from powercfg /q output. Friendly names vary by driver version, so vendor
        /// dropdowns are built from what the machine actually reports rather than
        /// from hardcoded guesses. A missing name falls back to "Level {n}".
        /// </summary>
        private static (uint Index, string Label)[] ParsePossibleSettings(string output)
        {
            const string idxTag  = "Possible Setting Index:";
            const string nameTag = "Possible Setting Friendly Name:";

            var result = new List<(uint, string)>();
            uint? pending = null;

            foreach (var raw in output.Split('\n'))
            {
                var line = raw.Trim();

                if (line.StartsWith(idxTag, StringComparison.OrdinalIgnoreCase))
                {
                    // Flush an index whose friendly name never arrived.
                    if (pending.HasValue) result.Add((pending.Value, $"Level {pending.Value}"));
                    pending = ParseIndexToken(line.Substring(idxTag.Length).Trim());
                }
                else if (line.StartsWith(nameTag, StringComparison.OrdinalIgnoreCase) && pending.HasValue)
                {
                    var label = line.Substring(nameTag.Length).Trim();
                    result.Add((pending.Value, label.Length > 0 ? label : $"Level {pending.Value}"));
                    pending = null;
                }
            }
            if (pending.HasValue) result.Add((pending.Value, $"Level {pending.Value}"));

            return result.ToArray();
        }

        // powercfg prints possible indices as either "0x00000000" or "000".
        private static uint? ParseIndexToken(string token)
        {
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber,
                    null, out var hex) ? hex : null;
            return uint.TryParse(token, out var dec) ? dec : null;
        }
    }
}
