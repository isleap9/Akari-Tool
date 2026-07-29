using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.OSTweaks
{
    public partial class OSTweaksTab
    {
        // ══════════════════════════════════════════════════════════════════════
        // TIMER RESOLUTION (session-only)
        // ══════════════════════════════════════════════════════════════════════

        private const uint _timerResGaming  = 5000;
        private const uint _timerResDefault = 156250;

        [System.Runtime.InteropServices.DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);

        private Action<bool>? _timerResSetter;

        private void BuildTimerResolution(StackPanel panel)
        {
            var section = TweakHelpers.BuildSection(panel, "Advanced");
            _timerResSetter = TweakHelpers.AddToggleRow(section,
                "Timer Resolution (0.5ms)",
                "Sets system scheduler tick to 0.5ms via NtSetTimerResolution — lowers input latency and frame time variance. Active for this session only.",
                SetTimerResolution);

            if (TweakHelpers.HasState("TimerResolution"))
                _timerResSetter?.Invoke(true);
        }

        private void SetTimerResolution(bool enable)
        {
            try
            {
                if (enable)
                {
                    int status = NtSetTimerResolution(_timerResGaming, true, out uint actual);
                    if (status == 0) { TweakHelpers.SaveState("TimerResolution"); Log($"Timer resolution set to 0.5ms (actual: {actual / 10000.0:F3}ms)."); }
                    else Log($"NtSetTimerResolution failed (NTSTATUS 0x{status:X8}).");
                }
                else
                {
                    NtSetTimerResolution(_timerResDefault, false, out _);
                    TweakHelpers.ClearState("TimerResolution");
                    Log("Timer resolution restored to Windows default (15.625ms).");
                }
            }
            catch (Exception ex) { Log($"ERROR TimerResolution: {ex.Message}"); }
        }

    }
}
