using System;
using System.Runtime.InteropServices;

namespace AkariTool.Services
{
    /// <summary>
    /// Whole-process suspend/resume via the undocumented-but-stable ntdll pair.
    /// Both methods swallow everything and return false: a dead PID on resume is
    /// the normal case (the user closed Discord during the game), and an
    /// exception there would abort the rest of the restore loop.
    /// </summary>
    public static class ProcessSuspender
    {
        private const int PROCESS_SUSPEND_RESUME = 0x0800;

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        public static bool Suspend(int pid) => Invoke(pid, NtSuspendProcess);

        public static bool Resume(int pid) => Invoke(pid, NtResumeProcess);

        private static bool Invoke(int pid, Func<IntPtr, int> call)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
                if (handle == IntPtr.Zero) return false;

                // NTSTATUS: negative is failure, 0 is success.
                return call(handle) >= 0;
            }
            catch { return false; }
            finally
            {
                if (handle != IntPtr.Zero)
                    try { CloseHandle(handle); } catch { }
            }
        }
    }
}
