using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs
{
    public static partial class TweakHelpers
    {
        /// <summary>
        /// The one place a Toggle tweak is applied. Every call site — row click,
        /// quick-set button, bulk apply, settings import — routes through here so
        /// the drift baseline sees every write without nine separate hooks.
        /// Records only after Apply succeeds, so a throwing Apply records nothing.
        /// </summary>
        public static void ApplyToggle(TweakDefinition def, bool value)
        {
            if (def.Apply == null) return;
            def.Apply(value);
            DriftBaseline.Record(def, value);
        }
        /// <summary>Dropdown equivalent of <see cref="ApplyToggle"/>.</summary>
        public static void ApplyOption(TweakDefinition def, int index)
        {
            if (def.ApplyIndex == null) return;
            def.ApplyIndex(index);
            DriftBaseline.Record(def, index);
        }

        /// <summary>
        /// Fire-and-forget process runner used by a few tweak Apply delegates
        /// (netsh, etc.). MVVM PORT: carried over verbatim from the net8
        /// TweakHelpers.cs — it is pure logic (ProcessStartInfo + WaitForExit) that
        /// merely happened to live in the rendering factory file.
        /// </summary>
        public static void RunCommand(string exe, string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi)?.WaitForExit();
            }
            catch { /* best-effort */ }
        }
    }
}
