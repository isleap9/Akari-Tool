using System.Runtime.InteropServices;
using System.Windows.Controls;
using AkariTool.Services;

namespace AkariTool.Tabs.Power
{
    public partial class PowerTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // BATTERY
        //
        // Gated on BOTH real battery hardware and the setting being exposed.
        // The powercfg probe alone is not sufficient: Windows registers the
        // battery subgroup on desktops too (verified — a battery-less desktop
        // returns exit 0 with 4 possible indices for the critical action), so
        // probing alone would render the section on machines with no battery.
        // GetSystemPowerStatus is the authoritative hardware check.
        //
        // Every row is a pure preference: no Recommended / Windows Default badge.
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

        private const string SG_BATTERY = "e73a048d-bf27-4f12-9731-8b2076e8891f";

        private const string SET_CRITICAL_ACTION       = "637ea02f-bbcb-4015-8e2c-a1c7b9c0b546";
        private const string SET_LOW_ACTION            = "d8742dcb-3e6a-4b3c-b3fe-374623cdcf06";
        private const string SET_CRITICAL_LEVEL        = "9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469";
        private const string SET_LOW_LEVEL             = "8183ba9a-e910-48da-8769-14ae6dc1170a";
        private const string SET_RESERVE_LEVEL         = "f3c5027d-cd16-4930-aa6b-90db844a8f00";
        private const string SET_LOW_NOTIFICATION      = "bcded951-187b-4d05-bccc-f7e51960c258";
        private const string SET_CRITICAL_NOTIFICATION = "5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f";

        private static readonly uint[] BatteryActionValues   = { 0u, 1u, 2u, 3u };
        private static readonly uint[] CriticalLevelValues   = { 5u, 7u, 10u, 12u, 15u };
        private static readonly uint[] LowLevelValues        = { 10u, 15u, 20u, 25u, 30u };
        private static readonly uint[] ReserveLevelValues    = { 5u, 7u, 9u, 10u, 15u };

        private static TweakDropdownOption[] BatteryActionOptions() => new[]
        {
            new TweakDropdownOption("Do nothing", 0u),
            new TweakDropdownOption("Sleep",      1u),
            new TweakDropdownOption("Hibernate",  2u),
            new TweakDropdownOption("Shut down",  3u),
        };

        private static TweakDropdownOption[] PercentOptions(uint[] values) =>
            values.Select(v => new TweakDropdownOption($"{v}%", v)).ToArray();

        private void BuildBattery(StackPanel panel)
        {
            // No battery hardware → skip the section entirely (the powercfg probe
            // would pass here even on a desktop, so the hardware check leads).
            if (!BatteryPresent())
            {
                Service?.Log("Power: no battery detected on this system — Battery section skipped.");
                return;
            }
            if (!PowerSettingExists(SG_BATTERY, SET_CRITICAL_ACTION))
            {
                Service?.Log("Power: battery subgroup not exposed by powercfg — Battery section skipped.");
                return;
            }

            var section = TweakHelpers.BuildSection(panel, "Battery");

            // ── Actions ──────────────────────────────────────────────────────
            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "battery-critical-action", Name = "Critical battery action",
                Description = "What Windows does when the battery reaches the critical level",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = BatteryActionOptions(),
                ReadCurrentIndex = () => ExactValueIndex(QueryPowerCfg(SG_BATTERY, SET_CRITICAL_ACTION, ac: true), BatteryActionValues),
                ApplyIndex = idx => SetPowerCfg(SG_BATTERY, SET_CRITICAL_ACTION, BatteryActionValues[idx], BatteryActionValues[idx], "Critical battery action")
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "battery-low-action", Name = "Low battery action",
                Description = "What Windows does when the battery reaches the low level",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = BatteryActionOptions(),
                ReadCurrentIndex = () => ExactValueIndex(QueryPowerCfg(SG_BATTERY, SET_LOW_ACTION, ac: true), BatteryActionValues),
                ApplyIndex = idx => SetPowerCfg(SG_BATTERY, SET_LOW_ACTION, BatteryActionValues[idx], BatteryActionValues[idx], "Low battery action")
            }));

            // ── Levels ───────────────────────────────────────────────────────
            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "battery-critical-level", Name = "Critical battery level",
                Description = "Charge percentage at which the critical battery action runs",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = PercentOptions(CriticalLevelValues),
                ReadCurrentIndex = () => ExactValueIndex(QueryPowerCfg(SG_BATTERY, SET_CRITICAL_LEVEL, ac: true), CriticalLevelValues),
                ApplyIndex = idx => SetPowerCfg(SG_BATTERY, SET_CRITICAL_LEVEL, CriticalLevelValues[idx], CriticalLevelValues[idx], "Critical battery level")
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "battery-low-level", Name = "Low battery level",
                Description = "Charge percentage at which Windows considers the battery low",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = PercentOptions(LowLevelValues),
                ReadCurrentIndex = () => ExactValueIndex(QueryPowerCfg(SG_BATTERY, SET_LOW_LEVEL, ac: true), LowLevelValues),
                ApplyIndex = idx => SetPowerCfg(SG_BATTERY, SET_LOW_LEVEL, LowLevelValues[idx], LowLevelValues[idx], "Low battery level")
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "battery-reserve-level", Name = "Reserve battery level",
                Description = "Charge percentage at which Windows switches to reserve power",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = PercentOptions(ReserveLevelValues),
                ReadCurrentIndex = () => ExactValueIndex(QueryPowerCfg(SG_BATTERY, SET_RESERVE_LEVEL, ac: true), ReserveLevelValues),
                ApplyIndex = idx => SetPowerCfg(SG_BATTERY, SET_RESERVE_LEVEL, ReserveLevelValues[idx], ReserveLevelValues[idx], "Reserve battery level")
            }));

            // ── Notifications ────────────────────────────────────────────────
            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "battery-low-notification", Name = "Low battery notification",
                Description = "Show a notification when the battery reaches the low level",
                IsPreference = true,
                ReadState = () => { var v = QueryPowerCfg(SG_BATTERY, SET_LOW_NOTIFICATION, ac: true); return v.HasValue ? v == 1 : null; },
                Apply = on => SetPowerCfg(SG_BATTERY, SET_LOW_NOTIFICATION, on ? 1u : 0u, on ? 1u : 0u, "Low battery notification")
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "battery-critical-notification", Name = "Critical battery notification",
                Description = "Show a notification when the battery reaches the critical level",
                IsPreference = true,
                ReadState = () => { var v = QueryPowerCfg(SG_BATTERY, SET_CRITICAL_NOTIFICATION, ac: true); return v.HasValue ? v == 1 : null; },
                Apply = on => SetPowerCfg(SG_BATTERY, SET_CRITICAL_NOTIFICATION, on ? 1u : 0u, on ? 1u : 0u, "Critical battery notification")
            }));
        }
    }
}
