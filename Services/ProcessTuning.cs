using System;
using System.Runtime.InteropServices;
using AkariTool.Core.Competitive;

namespace AkariTool.Services
{
    /// <summary>
    /// Per-process performance tuning through standard Windows APIs. Nothing here
    /// reads or writes another process's memory — every call is a documented (or,
    /// for I/O priority, long-stable ntdll) information class.
    ///
    /// Every method returns bool and swallows all failures. Anti-cheat drivers
    /// routinely deny handle access to a protected game process; that is a soft
    /// warning for the user, never a session failure.
    /// </summary>
    public static class ProcessTuning
    {
        // ── Access rights ─────────────────────────────────────────────────────
        private const int PROCESS_SET_INFORMATION         = 0x0200;
        private const int PROCESS_SET_LIMITED_INFORMATION = 0x2000;
        private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private const int TuningAccess =
            PROCESS_SET_INFORMATION | PROCESS_SET_LIMITED_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION;

        // ── Information classes ───────────────────────────────────────────────
        private const int ProcessIoPriority             = 33;   // NtSetInformationProcess
        private const int ProcessPowerThrottling        = 4;    // SetProcessInformation
        private const int SystemMemoryListInformation   = 80;   // NtSetSystemInformation
        private const int MemoryPurgeStandbyList        = 4;

        private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("ntdll.dll")]
        private static extern int NtSetInformationProcess(
            IntPtr processHandle, int processInformationClass, ref int processInformation, int length);

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(
            int systemInformationClass, ref int systemInformation, int length);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDefaultCpuSets(
            IntPtr process, IntPtr cpuSetIds, uint cpuSetIdCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessInformation(
            IntPtr process, int processInformationClass, ref PROCESS_POWER_THROTTLING_STATE processInformation, uint informationSize);

        // ── Privilege plumbing (for ClearStandbyList) ─────────────────────────

        private const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const int TOKEN_QUERY             = 0x0008;
        private const int SE_PRIVILEGE_ENABLED    = 0x0002;
        private const string SeProfileSingleProcessPrivilege = "SeProfileSingleProcessPrivilege";

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public int Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public int PrivilegeCount; public LUID_AND_ATTRIBUTES Privilege; }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process, int desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr tokenHandle, [MarshalAs(UnmanagedType.Bool)] bool disableAll,
            ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr priorState, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        // ── Public surface ────────────────────────────────────────────────────

        /// <summary>
        /// Sets the game process's I/O priority. Normal = 2, High = 3 — the values
        /// the IO_PRIORITY_HINT enum uses. Critical (4) is never offered; it is
        /// reserved for the memory manager.
        /// </summary>
        public static bool SetIoPriority(int pid, GameIoPriority priority)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(TuningAccess, false, pid);
                if (handle == IntPtr.Zero) return false;

                int value = priority == GameIoPriority.High ? 3 : 2;
                return NtSetInformationProcess(handle, ProcessIoPriority, ref value, sizeof(int)) >= 0;
            }
            catch { return false; }
            finally { SafeClose(handle); }
        }

        /// <summary>
        /// Applies the CPU set policy. AllCores passes a null set, which CLEARS any
        /// default CPU set restriction a launcher or a previous session left behind
        /// — that is the point of the call, not a no-op.
        ///
        /// PCoresOnly / PreferredCcd are not implemented: they need CPU topology
        /// detection (GetSystemCpuSetInformation + efficiency-class grouping) that
        /// this pass does not do. They are unreachable while CpuSetMode has one
        /// member, and the default branch keeps that honest if one is added.
        /// </summary>
        public static bool SetDefaultCpuSets(int pid, CpuSetMode mode)
        {
            if (mode != CpuSetMode.AllCores) return false;   // stub — see summary

            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(TuningAccess, false, pid);
                if (handle == IntPtr.Zero) return false;

                return SetProcessDefaultCpuSets(handle, IntPtr.Zero, 0);
            }
            catch { return false; }
            finally { SafeClose(handle); }
        }

        /// <summary>
        /// Opts the game OUT of CPU power throttling (EcoQoS). ControlMask selects
        /// the execution-speed policy; StateMask = 0 means "do not throttle".
        /// </summary>
        public static bool DisablePowerThrottling(int pid)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(TuningAccess, false, pid);
                if (handle == IntPtr.Zero) return false;

                var state = new PROCESS_POWER_THROTTLING_STATE
                {
                    Version     = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask   = 0,   // 0 = opt OUT of throttling
                };

                return SetProcessInformation(handle, ProcessPowerThrottling, ref state,
                    (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
            }
            catch { return false; }
            finally { SafeClose(handle); }
        }

        /// <summary>
        /// Purges the system standby list. System-wide, unlike everything else
        /// here. Requires SeProfileSingleProcessPrivilege, which is enabled first;
        /// if it cannot be acquired the call returns false rather than throwing.
        /// </summary>
        public static bool ClearStandbyList()
        {
            try
            {
                if (!EnablePrivilege(SeProfileSingleProcessPrivilege)) return false;

                int command = MemoryPurgeStandbyList;
                return NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int)) >= 0;
            }
            catch { return false; }
        }

        private static bool EnablePrivilege(string name)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                    return false;
                if (!LookupPrivilegeValue(null, name, out LUID luid))
                    return false;

                var privileges = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED },
                };

                if (!AdjustTokenPrivileges(token, false, ref privileges,
                        Marshal.SizeOf<TOKEN_PRIVILEGES>(), IntPtr.Zero, IntPtr.Zero))
                    return false;

                // AdjustTokenPrivileges reports success even when it assigned nothing
                // — ERROR_NOT_ALL_ASSIGNED is the "privilege not held" case.
                return Marshal.GetLastWin32Error() == 0;
            }
            catch { return false; }
            finally { SafeClose(token); }
        }

        private static void SafeClose(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
                try { CloseHandle(handle); } catch { }
        }
    }
}
