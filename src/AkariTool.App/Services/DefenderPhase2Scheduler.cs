using System;
using Microsoft.Win32;

namespace AkariTool.Services
{
    /// <summary>
    /// Manages the HKLM RunOnce entry that re-launches AkariTool in headless
    /// <c>--defender-phase2</c> mode at the next interactive login. RunOnce fires once
    /// with the logging-in admin's token and Windows removes the value before running it;
    /// phase-2 also clears it explicitly (<see cref="ClearRunOnce"/>) as a belt-and-braces
    /// guard so it can never run twice.
    /// </summary>
    public static class DefenderPhase2Scheduler
    {
        private const string RunOnceKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
        private const string ValueName  = "AkariDefenderPhase2";

        /// <summary>Schedules AkariTool.exe --defender-phase2 to run once at next login (HKLM RunOnce).</summary>
        public static void ScheduleRunOnce()
        {
            var exe = Environment.ProcessPath!;   // full path to the running AkariTool.exe
            using var k = Registry.LocalMachine.CreateSubKey(RunOnceKey, true);
            k!.SetValue(ValueName, $"\"{exe}\" --defender-phase2", RegistryValueKind.String);
        }

        /// <summary>Removes the RunOnce entry (called by phase-2 itself after it runs).</summary>
        public static void ClearRunOnce()
        {
            using var k = Registry.LocalMachine.OpenSubKey(RunOnceKey, true);
            k?.DeleteValue(ValueName, false);
        }

        /// <summary>True when the phase-2 RunOnce entry is currently scheduled.</summary>
        public static bool IsScheduled()
        {
            using var k = Registry.LocalMachine.OpenSubKey(RunOnceKey, false);
            return k?.GetValue(ValueName) != null;
        }
    }
}
