using System.Windows.Controls;
using AkariTool.Services;

namespace AkariTool.Tabs.Power
{
    public partial class PowerTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // GPU POWER
        //
        // These subgroups exist only when the matching vendor driver registered
        // them, so each row is gated on its own probe and the section header is
        // only emitted once at least one row survives gating.
        //
        // Option labels are NOT hardcoded: the friendly name for each index varies
        // by driver version, so they are parsed from the same probe's powercfg /q
        // output. A driver exposing extra indices therefore self-sizes the row.
        // ══════════════════════════════════════════════════════════════════════

        private const string SG_INTEL_GRAPHICS = "44f3beca-a7c0-460e-9df2-bb8b99e0cba6";
        private const string SET_INTEL_GRAPHICS = "3619c3f2-afb2-4afc-b0e9-e7fef372de36";

        private const string SG_AMD_SLIDER  = "c763b4ec-0e50-4b6b-9bed-2b92a6ee884e";
        private const string SET_AMD_SLIDER = "7ec1751b-60ed-4588-afb5-9819d3d77d90";

        private const string SG_ATI_POWERPLAY  = "f693fb01-e858-4f00-b20f-f30e12ac06d6";
        private const string SET_ATI_POWERPLAY = "191f65b5-d45c-4a4f-8aae-1ab8bfd980e6";

        private const string SG_SWITCHABLE  = "e276e160-7cb0-43c6-b20b-73f5dce39954";
        private const string SET_SWITCHABLE = "a1662ab2-9d34-4e53-ba8b-2639b9e20857";

        private void BuildGpuPower(StackPanel panel)
        {
            var rows = new (string Id, string Name, string Desc, string Sg, string Set)[]
            {
                ("gpu-intel-graphics-power-plan", "Intel Graphics Power Plan",
                 "Balance Intel integrated graphics performance against power draw",
                 SG_INTEL_GRAPHICS, SET_INTEL_GRAPHICS),

                ("gpu-amd-power-slider", "AMD Power Slider",
                 "AMD's power/performance slider position for the graphics adapter",
                 SG_AMD_SLIDER, SET_AMD_SLIDER),

                ("gpu-ati-powerplay", "ATI PowerPlay",
                 "ATI/AMD PowerPlay graphics power management level",
                 SG_ATI_POWERPLAY, SET_ATI_POWERPLAY),

                ("gpu-switchable-graphics", "Switchable Graphics",
                 "How Windows picks between the integrated and discrete GPU",
                 SG_SWITCHABLE, SET_SWITCHABLE),
            };

            // Probe first so the section header is only added when something renders.
            var present = rows.Where(r => PowerSettingExists(r.Sg, r.Set)).ToArray();
            if (present.Length == 0)
            {
                Service?.Log("Power: no vendor GPU power subgroups on this system — GPU Power section skipped.");
                return;
            }

            var section = TweakHelpers.BuildSection(panel, "GPU Power");

            foreach (var row in present)
            {
                var probe = ProbePowerSetting(row.Sg, row.Set);

                // Driver registered the setting but reported no possible indices —
                // there is nothing meaningful to choose from, so skip the row.
                if (probe.Options.Length == 0)
                {
                    Service?.Log($"Power: {row.Name} exposed no possible settings — row skipped.");
                    continue;
                }

                var values = probe.Options.Select(o => o.Index).ToArray();
                var options = probe.Options
                    .Select(o => new TweakDropdownOption(o.Label, o.Index))
                    .ToArray();

                var (sg, set, name) = (row.Sg, row.Set, row.Name);

                _refreshActions.Add(TweakHelpers.AddTweakRow(section, new TweakDefinition
                {
                    Id = row.Id, Name = row.Name, Description = row.Desc,
                    IsPreference = true, InputKind = TweakInputKind.Dropdown,
                    Options = options,
                    ReadCurrentIndex = () => ExactValueIndex(QueryPowerCfg(sg, set, ac: true), values),
                    ApplyIndex = idx => SetPowerCfg(sg, set, values[idx], values[idx], name)
                }));
            }
        }
    }
}
