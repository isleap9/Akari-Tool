using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs.Power
{
    // MVVM PORT: base partial for the extracted Power catalog. Holds the powercfg
    // read/write logic, the persistent "Akari Performance" scheme machinery, the
    // hardware/driver probes, and the shared time/registry helpers — all lifted
    // verbatim from the net8 PowerTab.* code-behind, minus the rendering scaffolding.
    //
    // ONE behavioural adaptation, mirroring the Phase-1 TweakRegistry.SectionsNeedRefresh
    // precedent: the net8 SetPowerCfg / EnsureAkariScheme write path ended by calling
    // RefreshPersistIndicator() + RefreshActiveCard() directly on the tab's plan-card
    // controls. Those are view concerns that cannot live in a data-only catalog, so the
    // powercfg writes, scheme persistence, drift clear, and logging are preserved exactly
    // and the trailing UI repaint is raised as the static PowerSchemeChanged event instead.
    // The rendering layer (rebuilt in a later wave) subscribes to repaint the plan cards.
    // The powercfg apply/read logic, registry paths, and GUIDs are unchanged.
    public static partial class PowerTweaks
    {
        /// <summary>
        /// Raised after a write reactivates / creates the Akari Performance scheme, so the
        /// (future) rendering layer can repaint the plan cards + persistence indicator.
        /// Replaces the net8 RefreshPersistIndicator()/RefreshActiveCard() UI calls.
        /// </summary>
        public static event Action? PowerSchemeChanged;

        // Plan GUIDs. BalancedGuid was already here; HighPerf/Ultimate added for the
        // bespoke Plan Selector (Phase 21) — pure data, byte-identical to net8's
        // PowerTab constants. internal so the plan-section VM can reference them.
        internal const string BalancedGuid     = "381b4222-f694-41f0-9685-ff5bb260df2e";
        internal const string HighPerfGuid     = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        internal const string UltimatePerfGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        // ══════════════════════════════════════════════════════════════════════
        // PERSISTENT "AKARI PERFORMANCE" SCHEME
        // ══════════════════════════════════════════════════════════════════════

        // internal: the bespoke Plan Selector / Persist Indicator VM (Phase 21) reads
        // the stored-scheme name + revert path. Values unchanged.
        internal const string StateKeyPath    = @"HKEY_CURRENT_USER\Software\AkariTool";
        internal const string SchemeGuidValue = "AkariPowerSchemeGuid";
        internal const string AkariPlanName   = "Akari Performance";

        // Resolved once per session; null = no valid Akari scheme (→ SCHEME_CURRENT).
        private static string? _schemeTarget;
        private static bool _schemeResolved;

        // True when the stored Akari scheme still exists but Windows (or an OEM tool)
        // has since made a different plan active. Writes still land in the Akari
        // scheme by GUID, and SetPowerCfg's trailing /SETACTIVE reactivates it.
        private static bool _schemeInactive;

        /// <summary>Read-only accessor for the drift flag — the Persist Indicator VM
        /// reads it (visibility change only; the flag's logic is unchanged).</summary>
        internal static bool SchemeInactive => _schemeInactive;

        internal static string? ReadStoredSchemeGuid() =>
            Registry.GetValue(StateKeyPath, SchemeGuidValue, null) as string;

        /// <summary>
        /// Returns the Akari scheme GUID if the stored one still exists on the
        /// system, else null. Validated once per session (powercfg /list is slow).
        /// internal: read by the Persist Indicator VM (visibility change only).
        /// </summary>
        internal static string? ResolveSchemeTarget()
        {
            if (_schemeResolved) return _schemeTarget;
            _schemeResolved = true;

            var stored = ReadStoredSchemeGuid();
            if (stored != null && ListPowerPlans().Any(p =>
                    p.Guid.Equals(stored, StringComparison.OrdinalIgnoreCase)))
            {
                _schemeTarget = stored;

                // A valid GUID is not necessarily the ACTIVE one — Windows updates and
                // OEM tools switch plans behind our back. ReadActivePowerPlan is a
                // registry read, so this costs no extra powercfg /list invocation.
                var (_, activeGuid) = SystemStateReader.ReadActivePowerPlan();
                _schemeInactive = !stored.Equals(activeGuid, StringComparison.OrdinalIgnoreCase);
            }

            return _schemeTarget;
        }

        /// <summary>
        /// Relocation of net8 RevertToBalanced's inline scheme-cache reset
        /// (<c>_schemeTarget = null; _schemeResolved = true;</c>) — after the Akari
        /// scheme is deleted, reads must fall back to SCHEME_CURRENT and must NOT
        /// re-resolve the now-deleted GUID. Called only by the Plan Selector VM's
        /// Revert path. Byte-identical effect to net8; touches only the two cache
        /// fields (no powercfg, no write).
        /// </summary>
        internal static void ResetSchemeCacheAfterRevert()
        {
            _schemeTarget = null;
            _schemeResolved = true;
        }

        /// <summary>
        /// Clears the drift flag after a write has reactivated the Akari scheme.
        /// Returns true when the flag actually changed, so callers only repaint then.
        /// </summary>
        private static bool ClearSchemeDrift()
        {
            if (!_schemeInactive) return false;
            _schemeInactive = false;
            return true;
        }

        /// <summary>
        /// Ensures the Akari Performance scheme exists (creating it from the
        /// currently active plan if the stored GUID is missing or was removed
        /// by Windows) and returns its GUID. Falls back to "SCHEME_CURRENT"
        /// if creation fails so writes still land somewhere.
        /// </summary>
        private static string EnsureAkariScheme()
        {
            var existing = ResolveSchemeTarget();
            if (existing != null) return existing;

            var (_, activeGuid) = SystemStateReader.ReadActivePowerPlan();
            string baseGuid = activeGuid ?? BalancedGuid;

            var before = ListPowerPlans()
                .Select(p => p.Guid)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            RunPowerCfg($"/duplicatescheme {baseGuid}");

            string? created = ListPowerPlans()
                .FirstOrDefault(p => !before.Contains(p.Guid)).Guid;
            if (created == null)
            {
                ToolService.Current?.Log("Power: could not create the Akari Performance scheme — writing to the active plan instead (may not persist).");
                return "SCHEME_CURRENT";
            }

            RunPowerCfg($"/changename {created} \"{AkariPlanName}\" \"Persistent power plan managed by Akari Tool\"");
            RunPowerCfg($"/setactive {created}");
            Registry.SetValue(StateKeyPath, SchemeGuidValue, created);

            _schemeTarget = created;
            _schemeResolved = true;
            ToolService.Current?.Log($"Power: created persistent '{AkariPlanName}' scheme (GUID: {created}) and set it active.");

            PowerSchemeChanged?.Invoke();
            return created;
        }

        // ══════════════════════════════════════════════════════════════════════
        // powercfg helpers
        // ══════════════════════════════════════════════════════════════════════

        private static string RunPowerCfg(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", args)
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var p = Process.Start(psi)!;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10_000);
                return output;
            }
            catch { return ""; }
        }

        /// <summary>
        /// RunPowerCfg variant that also surfaces the exit code — needed by the
        /// hardware probes, which must distinguish "setting absent" (non-zero exit)
        /// from "setting present but unreadable".
        /// </summary>
        private static (int Exit, string Output) RunPowerCfgCapture(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg", args)
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                using var p = Process.Start(psi)!;
                var output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(10_000)) return (-1, output);
                return (p.ExitCode, output);
            }
            catch { return (-1, ""); }
        }

        /// <summary>
        /// Index of <paramref name="current"/> in <paramref name="values"/>, or null
        /// when it matches none — null drives the dropdown's unselected (-1) state
        /// instead of silently clamping to the nearest option.
        /// </summary>
        private static int? ExactValueIndex(uint? current, uint[] values)
        {
            if (!current.HasValue) return null;
            int i = Array.IndexOf(values, current.Value);
            return i >= 0 ? i : null;
        }

        private static uint? QueryPowerCfg(string subgroupGuid, string settingGuid, bool ac)
        {
            // Read from the persistent Akari scheme when it exists so dropdown
            // states restore correctly on launch even if another plan is active.
            string target = ResolveSchemeTarget() ?? "SCHEME_CURRENT";
            var output = RunPowerCfg($"/QUERY {target} {subgroupGuid} {settingGuid}");
            var tag = ac ? "Current AC Power Setting Index:" : "Current DC Power Setting Index:";
            foreach (var line in output.Split('\n'))
            {
                var l = line.Trim();
                if (l.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
                {
                    var hex = l.Substring(tag.Length).Trim();
                    if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                        uint.TryParse(hex.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var v))
                        return v;
                }
            }
            return null;
        }

        private static void SetPowerCfg(string subgroupGuid, string settingGuid, uint acValue, uint dcValue, string label)
        {
            // Writes go to the persistent Akari Performance scheme (created on
            // first write) so they survive reboots and Windows updates.
            string target = EnsureAkariScheme();
            RunPowerCfg($"/SETACVALUEINDEX {target} {subgroupGuid} {settingGuid} {acValue}");
            RunPowerCfg($"/SETDCVALUEINDEX {target} {subgroupGuid} {settingGuid} {dcValue}");
            RunPowerCfg($"/SETACTIVE {target}");
            ToolService.Current?.Log($"Power: {label} set (AC={acValue}, DC={dcValue}).");

            // /SETACTIVE above made Akari Performance the active plan (creating it on first
            // change). Clear any prior drift, then let the rendering layer repaint the card
            // grid + persistence indicator (net8 called RefreshPersistIndicator/RefreshActiveCard
            // directly here — now raised as PowerSchemeChanged).
            ClearSchemeDrift();
            PowerSchemeChanged?.Invoke();
        }

        // Parses `powercfg /list` into (GUID, FriendlyName) pairs.
        // internal: the Plan Selector VM enumerates plans (friendly-name resolution +
        // Ultimate-plan lookup). Logic unchanged.
        internal static List<(string Guid, string Name)> ListPowerPlans()
        {
            var result = new List<(string, string)>();
            foreach (var line in RunPowerCfg("/list").Split('\n'))
            {
                int gi = line.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
                if (gi < 0) continue;
                var rest = line.Substring(gi + 5).Trim();
                var guid = rest.Split(' ')[0].Trim();
                if (guid.Length != 36) continue;

                string name = "";
                int p1 = rest.IndexOf('(');
                int p2 = rest.LastIndexOf(')');
                if (p1 >= 0 && p2 > p1) name = rest.Substring(p1 + 1, p2 - p1 - 1).Trim();

                result.Add((guid, name));
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        // HARDWARE / DRIVER SUPPORT PROBES
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

        // ══════════════════════════════════════════════════════════════════════
        // Time interval helpers
        // ══════════════════════════════════════════════════════════════════════

        private static uint[] TimeIntervalSeconds() => new uint[]
            { 0, 60, 120, 180, 300, 600, 900, 1200, 1800, 2700, 3600, 7200 };

        private static string[] TimeIntervalLabels() => new[]
            { "Never", "1 minute", "2 minutes", "3 minutes", "5 minutes", "10 minutes",
              "15 minutes", "20 minutes", "30 minutes", "45 minutes", "1 hour", "2 hours" };

        private static int FindTimeIndex(uint? seconds, uint[] table)
        {
            if (!seconds.HasValue) return 0;
            for (int i = 0; i < table.Length; i++)
                if (table[i] == seconds.Value) return i;
            return 0;
        }

        private static (uint ac, uint dc) TimeIntervalPair(int idx)
        {
            var t = TimeIntervalSeconds();
            uint v = t[Math.Clamp(idx, 0, t.Length - 1)];
            return (v, v);
        }

        private static TweakDropdownOption[] TimeIntervalOptions(uint recAC, uint recDC, uint defAC, uint defDC)
        {
            var labels = TimeIntervalLabels();
            var seconds = TimeIntervalSeconds();
            var opts = new TweakDropdownOption[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                bool isRec = seconds[i] == recAC;
                bool isDef = seconds[i] == defAC;
                opts[i] = new TweakDropdownOption(labels[i], (int)seconds[i], IsRecommended: isRec, IsDefault: isDef);
            }
            return opts;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Registry helper
        // ══════════════════════════════════════════════════════════════════════

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) is int i ? i : (int?)null; }
            catch { return null; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Battery hardware detection (used by the Battery section gate)
        // ══════════════════════════════════════════════════════════════════════

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemPowerStatus
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int  BatteryLifeTime;
            public int  BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

        /// <summary>
        /// True when the machine actually has a battery. BatteryFlag bit 128 means
        /// "No system battery"; 255 means the driver could not report a state, which
        /// is treated as absent so desktops never grow a Battery section.
        /// </summary>
        private static bool BatteryPresent()
        {
            try
            {
                if (!GetSystemPowerStatus(out var status)) return false;
                if (status.BatteryFlag == 255) return false; // unknown → assume none
                return (status.BatteryFlag & 128) == 0;
            }
            catch { return false; }
        }
    }
}
