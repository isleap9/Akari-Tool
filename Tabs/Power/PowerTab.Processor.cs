using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Power
{
    public partial class PowerTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // PROCESSOR POWER MANAGEMENT
        // ══════════════════════════════════════════════════════════════════════

        private void BuildProcessorPower(StackPanel panel)
        {
            const string SG = "54533251-82be-4824-96c1-47b60b740d00";
            var section = TweakHelpers.BuildSection(panel, "Processor Power Management");

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-min-state", Name = "Minimum processor state",
                Description = "Set the lowest CPU speed allowed as a percentage of maximum frequency",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("0% (Default DC)",             0u,   IsDefault: true),
                    new TweakDropdownOption("5%",                          5u),
                    new TweakDropdownOption("100% (Recommended AC)",       100u, IsRecommended: true),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "893dee8e-2bef-41e0-89c6-b55d0929964c", ac: true); return v switch { 100 => 2, 5 => 1, _ => 0 }; },
                ApplyIndex = idx => { uint[] vals = { 0u, 5u, 100u }; SetPowerCfg(SG, "893dee8e-2bef-41e0-89c6-b55d0929964c", vals[Math.Min(idx,2)], 5u, "CPU min state"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-max-state", Name = "Maximum processor state",
                Description = "Set the highest CPU speed allowed as a percentage of maximum frequency",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("100% (Recommended & Default)", 100u, IsRecommended: true, IsDefault: true),
                    new TweakDropdownOption("99%",  99u),
                    new TweakDropdownOption("50%",  50u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "bc5038f7-23e0-4960-96da-33abaf5935ec", ac: true); return v switch { 99 => 1, 50 => 2, _ => 0 }; },
                ApplyIndex = idx => { uint[] vals = { 100u, 99u, 50u }; SetPowerCfg(SG, "bc5038f7-23e0-4960-96da-33abaf5935ec", vals[Math.Min(idx,2)], vals[Math.Min(idx,2)], "CPU max state"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "system-cooling-policy", Name = "System cooling policy",
                Description = "Choose whether to slow down the processor first (passive) or speed up fans first (active) when hot",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Passive (Default DC)",    0u, IsDefault: true),
                    new TweakDropdownOption("Active (Recommended)",    1u, IsRecommended: true),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "94d3a615-a899-4ac5-ae2b-e4d8f634367f", ac: true); return v == 0 ? 0 : 1; },
                ApplyIndex = idx => SetPowerCfg(SG, "94d3a615-a899-4ac5-ae2b-e4d8f634367f", (uint)idx, 0u, "Cooling policy")
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-performance-boost-mode", Name = "Processor performance boost mode",
                Description = "Control how aggressively your CPU boosts above base frequency for demanding tasks",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Disabled",                                 0u),
                    new TweakDropdownOption("Enabled (Recommended DC)",                 1u),
                    new TweakDropdownOption("Aggressive (Recommended AC & Default)",    2u, IsRecommended: true, IsDefault: true),
                    new TweakDropdownOption("Efficient Enabled",                        4u),
                    new TweakDropdownOption("Efficient Aggressive",                     6u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "be337238-0d82-4146-a960-4f3749d470c7", ac: true); return v switch { 0 => 0, 1 => 1, 2 => 2, 4 => 3, 6 => 4, _ => 2 }; },
                ApplyIndex = idx => { uint[] vals = { 0u, 1u, 2u, 4u, 6u }; SetPowerCfg(SG, "be337238-0d82-4146-a960-4f3749d470c7", vals[Math.Min(idx,4)], 1u, "CPU boost mode"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "power-throttling", Name = "Power Throttling",
                Description = "Allow Windows to reduce CPU performance for background processes to save power",
                RecommendedState = false, DefaultState = true,
                ReadState = () => { var v = ReadDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff"); return v.HasValue ? v == 0 : true; },
                Apply = on =>
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                        "PowerThrottlingOff", on ? 0 : 1, RegistryValueKind.DWord);
                    Service?.Log($"Power Throttling {(on ? "enabled" : "disabled")}.");
                }
            }));
        }

        // ══════════════════════════════════════════════════════════════════════
        // PROCESSOR ADVANCED
        // ══════════════════════════════════════════════════════════════════════

        private void BuildProcessorAdvanced(StackPanel panel)
        {
            const string SG = "54533251-82be-4824-96c1-47b60b740d00";
            var section = TweakHelpers.BuildSection(panel, "Processor Advanced Settings");

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-energy-performance-preference", Name = "Processor Energy Performance Preference (EPP)",
                Description = "Balance power efficiency and performance for modern CPUs with HWP (0 = max performance, 100 = max efficiency)",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("0% — Max Performance (Recommended AC)", 0u,  IsRecommended: true),
                    new TweakDropdownOption("25% (Default AC)",                       25u, IsDefault: true),
                    new TweakDropdownOption("50% — Balanced (Default DC)",            50u),
                    new TweakDropdownOption("75%",                                    75u),
                    new TweakDropdownOption("100% — Max Efficiency",                  100u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "36687f9e-e3a5-4dbf-b1dc-15eb381c6863", ac: true); return v switch { 0 => 0, 25 => 1, 50 => 2, 75 => 3, 100 => 4, _ => 1 }; },
                ApplyIndex = idx => { uint[] vals = { 0u, 25u, 50u, 75u, 100u }; SetPowerCfg(SG, "36687f9e-e3a5-4dbf-b1dc-15eb381c6863", vals[Math.Min(idx,4)], 50u, "EPP"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-performance-increase-policy", Name = "Processor Performance Increase Policy",
                Description = "Control how quickly CPU ramps up speed when workload increases (legacy non-HWP CPUs)",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Ideal (Default DC)",               0u, IsDefault: true),
                    new TweakDropdownOption("Single",                           1u),
                    new TweakDropdownOption("Rocket (Recommended & Default AC)",2u, IsRecommended: true),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "465e1f50-b610-473a-ab58-00d1077dc418", ac: true); return v switch { 0 => 0, 1 => 1, _ => 2 }; },
                ApplyIndex = idx => { uint[] vals = { 0u, 1u, 2u }; SetPowerCfg(SG, "465e1f50-b610-473a-ab58-00d1077dc418", vals[Math.Min(idx,2)], 0u, "CPU perf increase policy"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-performance-decrease-policy", Name = "Processor Performance Decrease Policy",
                Description = "Control how quickly CPU reduces speed when workload decreases (legacy non-HWP CPUs)",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("Ideal",                      0u),
                    new TweakDropdownOption("Single (Recommended AC)",    1u, IsRecommended: true, IsDefault: true),
                    new TweakDropdownOption("Rocket (Recommended DC)",    2u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "40fbefc7-2e9d-4d25-a185-0cfd8574bac6", ac: true); return v switch { 0 => 0, 1 => 1, _ => 2 }; },
                ApplyIndex = idx => { uint[] vals = { 0u, 1u, 2u }; SetPowerCfg(SG, "40fbefc7-2e9d-4d25-a185-0cfd8574bac6", vals[Math.Min(idx,2)], 2u, "CPU perf decrease policy"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-core-parking-min-cores", Name = "CPU Core Parking Minimum Cores",
                Description = "Set the minimum percentage of CPU cores that must remain active and responsive",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("0% (Recommended — let OS manage)",  0u, IsRecommended: true),
                    new TweakDropdownOption("5% (Default DC)",                   5u, IsDefault: true),
                    new TweakDropdownOption("25%",                               25u),
                    new TweakDropdownOption("50%",                               50u),
                    new TweakDropdownOption("100% (Default AC — no parking)",    100u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "0cc5b647-c1df-4637-891a-dec35c318583", ac: true); return v switch { 0 => 0, 5 => 1, 25 => 2, 50 => 3, 100 => 4, _ => 0 }; },
                ApplyIndex = idx => { uint[] vals = { 0u, 5u, 25u, 50u, 100u }; SetPowerCfg(SG, "0cc5b647-c1df-4637-891a-dec35c318583", vals[Math.Min(idx,4)], 5u, "CPU core parking min"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-core-parking-max-cores", Name = "CPU Core Parking Maximum Cores",
                Description = "Set the maximum percentage of CPU cores allowed to be active (100% for best performance)",
                IsPreference = true, InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("100% (Recommended & Default)", 100u, IsRecommended: true, IsDefault: true),
                    new TweakDropdownOption("75%",  75u),
                    new TweakDropdownOption("50%",  50u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "ea062031-0e34-4ff1-9b6d-eb1059334028", ac: true); return v switch { 100 => 0, 75 => 1, _ => 2 }; },
                ApplyIndex = idx => { uint[] vals = { 100u, 75u, 50u }; SetPowerCfg(SG, "ea062031-0e34-4ff1-9b6d-eb1059334028", vals[Math.Min(idx,2)], vals[Math.Min(idx,2)], "CPU core parking max"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-performance-increase-threshold", Name = "Processor Performance Increase Threshold",
                Description = "CPU usage % that triggers speed increase (lower = more responsive, legacy non-HWP CPUs)",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("10% (Recommended AC)", 10u, IsRecommended: true),
                    new TweakDropdownOption("30% (Default AC)",      30u, IsDefault: true),
                    new TweakDropdownOption("50%",                   50u),
                    new TweakDropdownOption("90% (Default DC)",      90u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "06cadf0e-64ed-448a-8927-ce7bf90eb35d", ac: true); return v switch { 10 => 0, 30 => 1, 50 => 2, 90 => 3, _ => 1 }; },
                ApplyIndex = idx => { uint[] vals = { 10u, 30u, 50u, 90u }; SetPowerCfg(SG, "06cadf0e-64ed-448a-8927-ce7bf90eb35d", vals[Math.Min(idx,3)], 90u, "CPU perf increase threshold"); }
            }));

            _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
            {
                Id = "processor-performance-decrease-threshold", Name = "Processor Performance Decrease Threshold",
                Description = "CPU usage % that triggers speed reduction (lower = maintains performance longer, legacy non-HWP CPUs)",
                InputKind = TweakInputKind.Dropdown,
                Options = new[]
                {
                    new TweakDropdownOption("8% (Recommended AC)",  8u,  IsRecommended: true),
                    new TweakDropdownOption("10% (Default AC)",      10u, IsDefault: true),
                    new TweakDropdownOption("20% (Recommended DC)", 20u),
                    new TweakDropdownOption("30% (Default DC)",      30u),
                },
                ReadCurrentIndex = () => { var v = QueryPowerCfg(SG, "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6", ac: true); return v switch { 8 => 0, 10 => 1, 20 => 2, 30 => 3, _ => 1 }; },
                ApplyIndex = idx => { uint[] vals = { 8u, 10u, 20u, 30u }; SetPowerCfg(SG, "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6", vals[Math.Min(idx,3)], 30u, "CPU perf decrease threshold"); }
            }));
        }

    }
}
