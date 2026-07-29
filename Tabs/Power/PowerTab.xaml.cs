using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Power
{
    public partial class PowerTab : BaseTab
    {
        private readonly List<Action> _refreshActions = new();
        private const string UltimatePerfGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        private const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

        // Plan card state — border swaps + "ACTIVE" tag follow the real active plan.
        private readonly List<Border> _planCards = new();
        private readonly List<TextBlock> _planActiveTags = new();
        private readonly List<Func<string?, string?, bool>> _planMatchers = new();

        // Dynamic 4th card, shown only when a custom / OEM plan is active.
        private Border? _customPlanCard;
        private TextBlock? _customPlanName;
        private TextBlock? _customPlanDesc;
        private TextBlock? _customPlanActiveTag;
        private ColumnDefinition? _customPlanColumn;
        private string? _customPlanGuid;   // active custom plan GUID, for click-to-reactivate

        // README plan-card palette
        private static readonly Brush CardBg = TweakHelpers.CardBg;
        private static readonly Brush CardBorderIdle = TweakHelpers.Hairline; // rgba(255,60,80,0.2)
        private static readonly Brush CardBorderActive = ToolService.BrushFrom("#80E0142A"); // rgba(224,20,42,0.5)

        public PowerTab() => InitializeComponent();

        public override string NavTag   => "Power";
        public override string NavLabel => "Power";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Power",
            "Power plan management and advanced power configuration.",
            withActions: true, RootPanel));

            BuildPlanSelector(RootPanel);

            BuildPersistIndicator(RootPanel);

            BuildDisplay(RootPanel);
            BuildHardDisk(RootPanel);
            BuildInternetExplorer(RootPanel);
            BuildDesktopBackground(RootPanel);
            BuildWirelessAdapter(RootPanel);
            BuildSleep(RootPanel);
            BuildBattery(RootPanel);
            BuildUSB(RootPanel);
            BuildPciExpress(RootPanel);
            BuildGpuPower(RootPanel);
            BuildProcessorPower(RootPanel);
            BuildProcessorAdvanced(RootPanel);
            BuildMultimedia(RootPanel);
            BuildPowerButtons(RootPanel);
            BuildStartMenuPower(RootPanel);

            foreach (var r in _refreshActions)
                try { r(); } catch { }
        }
        // ── powercfg helpers ──────────────────────────────────────────────────

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

        private void SetPowerCfg(string subgroupGuid, string settingGuid, uint acValue, uint dcValue, string label)
        {
            // Writes go to the persistent Akari Performance scheme (created on
            // first write) so they survive reboots and Windows updates.
            string target = EnsureAkariScheme();
            RunPowerCfg($"/SETACVALUEINDEX {target} {subgroupGuid} {settingGuid} {acValue}");
            RunPowerCfg($"/SETDCVALUEINDEX {target} {subgroupGuid} {settingGuid} {dcValue}");
            RunPowerCfg($"/SETACTIVE {target}");
            Service?.Log($"Power: {label} set (AC={acValue}, DC={dcValue}).");

            // /SETACTIVE above made Akari Performance the active plan (creating it on first
            // change). Always refresh the card grid + persistence indicator so the custom
            // card appears immediately — not only when prior drift existed.
            ClearSchemeDrift();
            RefreshPersistIndicator();
            RefreshActiveCard();
        }

        // ── Time interval helpers ─────────────────────────────────────────────

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

        private (uint ac, uint dc) TimeIntervalPair(int idx)
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

        // ── Registry helper ───────────────────────────────────────────────────

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) is int i ? i : (int?)null; }
            catch { return null; }
        }
    }
}
