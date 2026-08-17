using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Win32;

namespace AkariTool.Services
{
    /// <summary>
    /// In-process privilege escalation: runs native C# code while impersonating
    /// SYSTEM or TrustedInstaller, replacing the external MinSudo/PowerRun helpers.
    ///
    /// The pattern extends the token code in TweakHelpers.GetRealUserSid
    /// (OpenProcessToken + WindowsIdentity): here the borrowed token is additionally
    /// duplicated into an impersonation token and attached to the calling thread, so
    /// every <see cref="Registry"/> call inside the supplied action performs its
    /// access check against the elevated identity instead of the logged-on user.
    ///
    /// The process must already be running elevated (Administrator) — impersonation
    /// borrows an existing identity, it does not bypass UAC.
    ///
    /// IMPORTANT: impersonation is a per-thread state. The action must be fully
    /// synchronous; awaiting inside it can resume on another thread that carries the
    /// original (unelevated) identity.
    /// </summary>
    public static class ElevationService
    {
        // ── Win32 constants ──────────────────────────────────────────────────────
        private const uint TOKEN_DUPLICATE                    = 0x0002;
        private const uint TOKEN_QUERY                        = 0x0008;
        private const uint TOKEN_ADJUST_PRIVILEGES            = 0x0020;
        private const uint MAXIMUM_ALLOWED                    = 0x02000000;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION  = 0x1000;

        private const int SecurityImpersonation = 2;   // SECURITY_IMPERSONATION_LEVEL
        private const int TokenImpersonation    = 2;   // TOKEN_TYPE

        private const string SE_DEBUG_NAME       = "SeDebugPrivilege";
        // SeBackup/SeRestore give the token DACL-bypass on registry keys via Windows'
        // backup/restore semantics — the same privileges regedit.exe enables when it
        // imports a .reg under sufficient rights. SYSTEM holds both but they are DISABLED
        // by default; enabling them is what lets our native writes hit the handful of
        // unusually-locked keys a normal SYSTEM access check (no bypass) can't clear.
        private const string SE_BACKUP_NAME      = "SeBackupPrivilege";
        private const string SE_RESTORE_NAME     = "SeRestorePrivilege";
        private const uint   SE_PRIVILEGE_ENABLED = 0x0002;

        private const uint SERVICE_QUERY_STATUS = 0x0004;
        private const uint SC_MANAGER_CONNECT   = 0x0001;
        private const int  SC_STATUS_PROCESS_INFO = 0;

        // ── P/Invoke ─────────────────────────────────────────────────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DuplicateTokenEx(
            IntPtr existingToken, uint desiredAccess, IntPtr tokenAttributes,
            int impersonationLevel, int tokenType, out IntPtr newToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ImpersonateLoggedOnUser(IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RevertToSelf();

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr tokenHandle, [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
            ref TOKEN_PRIVILEGES newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenService(IntPtr scManager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryServiceStatusEx(
            IntPtr service, int infoLevel, IntPtr buffer, uint bufferSize, out uint bytesNeeded);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privilege; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS_PROCESS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
            public uint dwProcessId;
            public uint dwServiceFlags;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs <paramref name="action"/> while impersonating the SYSTEM account, using a
        /// token duplicated from winlogon.exe. All registry writes inside the action go
        /// through <see cref="Registry"/> and inherit SYSTEM rights.
        /// </summary>
        /// <param name="enableBackupRestore">
        /// When true, enables SeBackupPrivilege + SeRestorePrivilege on the impersonated
        /// SYSTEM token before the action runs, giving registry writes DACL-bypass (the same
        /// bypass regedit.exe uses on .reg import). OPT-IN — off by default so existing callers
        /// (Defender disable, built-in preset locked-service writes) are untouched; only paths
        /// that write a full uncurated key dump (the AkariOS playbook import) need it.
        /// </param>
        /// <returns>true when the impersonation was established and the action ran to completion.</returns>
        public static bool RunAsSystem(Action action, Action<string>? log = null, bool enableBackupRestore = false)
        {
            try
            {
                // SeDebugPrivilege is required to open winlogon's process token.
                if (!EnablePrivilege(SE_DEBUG_NAME, log))
                    return false;

                var winlogon = Process.GetProcessesByName("winlogon");
                if (winlogon.Length == 0)
                {
                    log?.Invoke("Elevation: winlogon.exe not found — cannot acquire a SYSTEM token.");
                    return false;
                }

                return RunAsProcessToken(winlogon[0].Id, action, "SYSTEM", log, enableBackupRestore);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Elevation: RunAsSystem failed — {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> while impersonating TrustedInstaller. Starts the
        /// TrustedInstaller service if it is stopped, resolves its process id, duplicates
        /// its token and impersonates it. Needed for ACL-protected keys under
        /// HKLM\SYSTEM\...\Services that even Administrator cannot write.
        /// </summary>
        /// <returns>true when the impersonation was established and the action ran to completion.</returns>
        public static bool RunAsTrustedInstaller(Action action, Action<string>? log = null)
        {
            try
            {
                if (!EnablePrivilege(SE_DEBUG_NAME, log))
                    return false;

                // The service is demand-start; its process only exists while running.
                using (var sc = new ServiceController("TrustedInstaller"))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        log?.Invoke("Elevation: starting TrustedInstaller service…");
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    }
                }

                var pid = GetServiceProcessId("TrustedInstaller", log);
                if (pid <= 0)
                {
                    log?.Invoke("Elevation: could not resolve the TrustedInstaller process id.");
                    return false;
                }

                return RunAsProcessToken(pid, action, "TrustedInstaller", log);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Elevation: RunAsTrustedInstaller failed — {ex.Message}");
                return false;
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // CORE
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Opens the target process, duplicates its access token into an impersonation
        /// token, attaches it to the current thread and runs the action. Every handle is
        /// released and <c>RevertToSelf</c> is called in a finally block — a leaked
        /// impersonation token would leave the whole thread running elevated.
        /// </summary>
        private static bool RunAsProcessToken(int pid, Action action, string label, Action<string>? log,
                                              bool enableBackupRestore = false)
        {
            IntPtr process = IntPtr.Zero, token = IntPtr.Zero, dup = IntPtr.Zero;
            bool impersonating = false;

            try
            {
                process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (process == IntPtr.Zero)
                {
                    log?.Invoke($"Elevation: OpenProcess({label}, pid {pid}) failed — {Win32Error()}");
                    return false;
                }

                if (!OpenProcessToken(process, TOKEN_DUPLICATE | TOKEN_QUERY, out token))
                {
                    log?.Invoke($"Elevation: OpenProcessToken({label}) failed — {Win32Error()}");
                    return false;
                }

                // A borrowed token cannot be attached directly; it must be duplicated
                // into a token of our own with impersonation semantics.
                if (!DuplicateTokenEx(token, MAXIMUM_ALLOWED, IntPtr.Zero,
                                      SecurityImpersonation, TokenImpersonation, out dup))
                {
                    log?.Invoke($"Elevation: DuplicateTokenEx({label}) failed — {Win32Error()}");
                    return false;
                }

                if (!ImpersonateLoggedOnUser(dup))
                {
                    log?.Invoke($"Elevation: ImpersonateLoggedOnUser({label}) failed — {Win32Error()}");
                    return false;
                }
                impersonating = true;

                // Enable SeBackup/SeRestore on the SAME token now attached to the thread
                // (dup) — NOT the process token — so the action's registry writes get the
                // DACL bypass. Logged rather than fatal: if enablement fails we still want
                // the action to run and surface which specific writes then fail.
                if (enableBackupRestore)
                {
                    bool b = EnablePrivilegeOnToken(dup, SE_BACKUP_NAME, log);
                    bool r = EnablePrivilegeOnToken(dup, SE_RESTORE_NAME, log);
                    log?.Invoke($"Elevation: DACL-bypass on {label} token — " +
                                $"SeBackupPrivilege={(b ? "enabled" : "FAILED")}, " +
                                $"SeRestorePrivilege={(r ? "enabled" : "FAILED")}.");
                }

                action();
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Elevation: action under {label} threw — {ex.Message}");
                return false;
            }
            finally
            {
                // Drop the elevated identity before anything else can run on this thread.
                if (impersonating && !RevertToSelf())
                    log?.Invoke($"Elevation: RevertToSelf failed — {Win32Error()}");

                if (dup     != IntPtr.Zero) CloseHandle(dup);
                if (token   != IntPtr.Zero) CloseHandle(token);
                if (process != IntPtr.Zero) CloseHandle(process);
            }
        }

        /// <summary>
        /// Enables a named privilege on the current process token (SeDebugPrivilege is
        /// present-but-disabled by default for elevated processes, so it only needs to be
        /// switched on, not granted). Opens the process token then delegates the actual
        /// LookupPrivilegeValue/AdjustTokenPrivileges work to <see cref="EnablePrivilegeOnToken"/>.
        /// </summary>
        private static bool EnablePrivilege(string privilege, Action<string>? log)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                {
                    log?.Invoke($"Elevation: OpenProcessToken(self) failed — {Win32Error()}");
                    return false;
                }

                return EnablePrivilegeOnToken(token, privilege, log);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Elevation: EnablePrivilege({privilege}) threw — {ex.Message}");
                return false;
            }
            finally
            {
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }

        /// <summary>
        /// Enables a named privilege on an ALREADY-OPEN token handle (which must carry
        /// TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY). This is the shared core used by both
        /// <see cref="EnablePrivilege"/> (process token) and the SeBackup/SeRestore
        /// enablement inside <see cref="RunAsProcessToken"/> (the duplicated impersonation
        /// token attached to the thread) — one mechanism, two token sources. The caller owns
        /// the handle's lifetime; this method does not close it.
        /// </summary>
        private static bool EnablePrivilegeOnToken(IntPtr token, string privilege, Action<string>? log)
        {
            if (!LookupPrivilegeValue(null, privilege, out var luid))
            {
                log?.Invoke($"Elevation: LookupPrivilegeValue({privilege}) failed — {Win32Error()}");
                return false;
            }

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED }
            };

            // AdjustTokenPrivileges reports success even when the privilege was not held,
            // so the last error has to be checked explicitly.
            if (!AdjustTokenPrivileges(token, false, ref tp, (uint)Marshal.SizeOf<TOKEN_PRIVILEGES>(),
                                       IntPtr.Zero, IntPtr.Zero))
            {
                log?.Invoke($"Elevation: AdjustTokenPrivileges({privilege}) failed — {Win32Error()}");
                return false;
            }

            int err = Marshal.GetLastWin32Error();
            if (err != 0) // ERROR_NOT_ALL_ASSIGNED (1300) lands here
            {
                log?.Invoke($"Elevation: {privilege} not assigned (error {err}).");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves the process id hosting a running service via QueryServiceStatusEx.
        /// More reliable than matching on process name.
        /// </summary>
        private static int GetServiceProcessId(string serviceName, Action<string>? log)
        {
            IntPtr scm = IntPtr.Zero, svc = IntPtr.Zero, buffer = IntPtr.Zero;
            try
            {
                scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
                if (scm == IntPtr.Zero)
                {
                    log?.Invoke($"Elevation: OpenSCManager failed — {Win32Error()}");
                    return 0;
                }

                svc = OpenService(scm, serviceName, SERVICE_QUERY_STATUS);
                if (svc == IntPtr.Zero)
                {
                    log?.Invoke($"Elevation: OpenService({serviceName}) failed — {Win32Error()}");
                    return 0;
                }

                int size = Marshal.SizeOf<SERVICE_STATUS_PROCESS>();
                buffer = Marshal.AllocHGlobal(size);
                if (!QueryServiceStatusEx(svc, SC_STATUS_PROCESS_INFO, buffer, (uint)size, out _))
                {
                    log?.Invoke($"Elevation: QueryServiceStatusEx({serviceName}) failed — {Win32Error()}");
                    return 0;
                }

                return (int)Marshal.PtrToStructure<SERVICE_STATUS_PROCESS>(buffer).dwProcessId;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Elevation: GetServiceProcessId({serviceName}) threw — {ex.Message}");
                return 0;
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                if (svc    != IntPtr.Zero) CloseServiceHandle(svc);
                if (scm    != IntPtr.Zero) CloseServiceHandle(scm);
            }
        }

        private static string Win32Error()
        {
            int code = Marshal.GetLastWin32Error();
            return $"Win32 error {code} ({new System.ComponentModel.Win32Exception(code).Message})";
        }
    }
}
