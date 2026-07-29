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
        // DEFENDER
        // ══════════════════════════════════════════════════════════════════════

        private static readonly string WinNoDefenderCab = @"C:\Windows\NoDefender.cab";

        private static readonly string[] DefenderServices =
        {
            "MsSecCore","MsSecFlt","MsSecWfp","SecurityHealthService",
            "Sense","WdBoot","WdFilter","WdNisDrv","WdNisSvc",
            "WinDefend","wscsvc","MDCoreSvc","SgrmAgent","SgrmBroker",
            "webthreatdefsvc","webthreatdefusersvc",
        };

        private static readonly string[] DefenderScheduledTasks =
        {
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Cleanup",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan",
            @"\Microsoft\Windows\Windows Defender\Windows Defender Verification",
        };

        private void SetDefenderToggle(bool disable) => _ = SetDefenderAsync(disable);

        private async Task SetDefenderAsync(bool disable)
        {
            try
            {
                bool filesReady = await PostInstallService.EnsureDefenderFilesAsync(Service!);
                if (!filesReady) { Service?.Log("[DEFENDER] Cannot proceed — required files unavailable."); return; }

                string minSudo  = PostInstallService.MinSudoPath;
                string powerRun = PostInstallService.PowerRunPath;
                string cab      = PostInstallService.NoDefenderPath;

                if (disable)
                {
                    if (TweakHelpers.HasState("DisableDefender")) return;
                    Service?.Log("[DEFENDER] Disabling Windows Defender...");
                    Service?.Log("[DEFENDER] Checking Tamper Protection status...");
                    if (IsTamperProtectionEnabled())
                    {
                        Service?.Log("[DEFENDER] ERROR: Tamper Protection is ON.");
                        Service?.Log("[DEFENDER] Go to: Windows Security → Virus & threat protection");
                        Service?.Log("[DEFENDER]   → Manage settings → Tamper Protection → Off");
                        Service?.Log("[DEFENDER] Then try again.");
                        return;
                    }
                    Service?.Log("[DEFENDER] Tamper Protection is off — proceeding.");
                    Service?.Log("[DEFENDER] Preparing NoDefender package...");
                    File.Copy(cab, WinNoDefenderCab, overwrite: true);
                    Service?.Log("[DEFENDER] Installing NoDefender (30–60s)...");
                    await RunElevatedPsFileAsync(Path.Combine(Path.GetDirectoryName(cab)!, "DisableDefender.ps1"));
                    Service?.Log("[DEFENDER] Scheduling post-reboot service cleanup...");
                    await SchedulePostRebootCleanup(powerRun, cab);
                    TweakHelpers.SaveState("DisableDefender");
                    Service?.Log("[DEFENDER] Phase 1 complete. Please restart now.");
                    Service?.Log("[DEFENDER] On next login, Phase 2 will run automatically to finish disabling Defender.");
                }
                else
                {
                    Service?.Log("[DEFENDER] Re-enabling Windows Defender...");
                    Service?.Log("[DEFENDER] Restoring Defender package (30–60s)...");
                    await RunElevatedPsAsync($"if (Test-Path '{WinNoDefenderCab}') {{ Remove-WindowsPackage -Online -PackagePath '{WinNoDefenderCab}' -NoRestart }}");
                    Service?.Log("[DEFENDER] Restoring Defender services...");
                    await RunAsTrustedInstallerBatAsync(minSudo, BuildServiceBat(startValue: 2));
                    TweakHelpers.ClearState("DisableDefender");
                    Service?.Log("[DEFENDER] Defender re-enabled. Restart required.");
                }
            }
            catch (Exception ex) { Service?.Log($"[DEFENDER] ERROR: {ex.Message}"); }
        }

        private async Task SchedulePostRebootCleanup(string powerRun, string cab)
        {
            var batPath = Path.Combine(Path.GetDirectoryName(cab)!, "AkariDefenderCleanup.bat");
            var sysCmd  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

            var lines = new List<string>
            {
                "@echo off", ":: AkariTool — Defender Phase 2 cleanup (runs once after reboot)", "",
                ":: Disable real-time monitoring first",
                $@"PowerShell -NonInteractive -NoLogo -NoProfile -C ""Set-MpPreference -DisableRealtimeMonitoring 1"" >NUL 2>nul",
                "", ":: Kill all 16 Defender service registry keys (ControlSet001)",
            };
            foreach (var line in BuildServiceBat(startValue: 4))
                lines.Add($@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c {line}");
            lines.AddRange(new[]
            {
                "", ":: Remove SecurityHealth from Run key",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v ""SecurityHealth"" /f",
                "", ":: Disable SmartScreen binary",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c taskkill /f /im smartscreen.exe",
                $@"if not exist ""%systemroot%\system32\smartscreen.exe.old"" if exist ""%systemroot%\system32\smartscreen.exe"" (",
                $@"  ""{powerRun}"" /SW:0 ""{sysCmd}"" /c takeown /F ""%systemroot%\system32\smartscreen.exe"" /A",
                $@"  ""{powerRun}"" /SW:0 ""{sysCmd}"" /c icacls ""%systemroot%\system32\smartscreen.exe"" /grant Administrators:F",
                $@"  ""{powerRun}"" /SW:0 ""{sysCmd}"" /c copy ""%systemroot%\system32\smartscreen.exe"" ""%systemroot%\system32\smartscreen.exe.old"" /v",
                $@"  ""{powerRun}"" /SW:0 ""{sysCmd}"" /c del ""%systemroot%\system32\smartscreen.exe""",
                $@")", "", ":: SmartScreen registry keys",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer"" /v ""SmartScreenEnabled"" /t REG_SZ /d ""Off"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\Software\Policies\Microsoft\System"" /v ""EnableSmartScreen"" /t REG_DWORD /d ""0"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\Software\Policies\Microsoft\Windows Defender\SmartScreen"" /v ""ConfigureAppInstallControlEnabled"" /t REG_DWORD /d ""0"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\Software\Policies\Microsoft\Windows Defender\SmartScreen"" /v ""ConfigureAppInstallControl"" /t REG_DWORD /d ""0"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\Software\Policies\Microsoft\Windows Defender\SmartScreen"" /v ""EnableSmartScreen"" /t REG_DWORD /d ""0"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKCU\Software\Microsoft\Windows\CurrentVersion\AppHost"" /v ""EnableWebContentEvaluation"" /t REG_DWORD /d ""0"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\Software\Microsoft\Windows\CurrentVersion\AppHost"" /v ""EnableWebContentEvaluation"" /t REG_DWORD /d ""0"" /f",
                "", ":: CI/Policy and DeviceGuard keys",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\SYSTEM\ControlSet001\Control\CI\Policy"" /v ""VerifiedAndReputablePolicyState"" /t REG_DWORD /d ""0"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\Software\Microsoft\Windows Defender"" /v ""PUAProtection"" /t REG_DWORD /d ""0"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\SYSTEM\ControlSet001\Control\CI\Config"" /v ""VulnerableDriverBlocklistEnable"" /t REG_DWORD /d ""0"" /f",
                $@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c Reg.exe add ""HKLM\SYSTEM\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"" /v ""Enabled"" /t REG_DWORD /d ""0"" /f",
                "", ":: Disable Defender scheduled tasks",
            });
            foreach (var task in DefenderScheduledTasks)
                lines.Add($@"""{powerRun}"" /SW:0 ""{sysCmd}"" /c schtasks.exe /change /disable /TN ""{task}""");
            lines.AddRange(new[]
            {
                "", ":: Self-cleanup — remove RunOnce entry and this bat",
                $@"Reg.exe delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"" /v ""AkariDefenderCleanup"" /f >NUL 2>nul",
                $@"(del /f /q ""%~f0"") >NUL 2>nul",
            });

            await File.WriteAllLinesAsync(batPath, lines);
            var cmdExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "AkariDefenderCleanup", $"\"{cmdExe}\" /c \"{batPath}\"", RegistryValueKind.String);
            Service?.Log($"[DEFENDER] Phase 2 cleanup scheduled at: {batPath}");
            Service?.Log($"[DEFENDER] RunOnce value: \"{cmdExe}\" /c \"{batPath}\"");
            Service?.Log("[DEFENDER] It will run automatically on next login.");
        }

        private static bool IsTamperProtectionEnabled()
        {
            try { var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features", "TamperProtection", null); return val is not int i || i != 4; }
            catch { return true; }
        }

        private static string[] BuildServiceBat(int startValue) =>
            DefenderServices.Select(svc => $@"Reg.exe add ""HKLM\SYSTEM\ControlSet001\Services\{svc}"" /v ""Start"" /t REG_DWORD /d ""{startValue}"" /f").ToArray();

        private static async Task RunElevatedPsFileAsync(string ps1Path)
        {
            var psi = new System.Diagnostics.ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-ExecutionPolicy Bypass -NonInteractive -NoLogo -NoProfile -File \"{ps1Path}\"", UseShellExecute = true, Verb = "runas", CreateNoWindow = false };
            await System.Diagnostics.Process.Start(psi)!.WaitForExitAsync();
        }

        private static async Task RunElevatedPsAsync(string command)
        {
            var psi = new System.Diagnostics.ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-NonInteractive -NoLogo -NoProfile -C \"{command}\"", UseShellExecute = false, CreateNoWindow = true };
            await System.Diagnostics.Process.Start(psi)!.WaitForExitAsync();
        }

        private async Task RunAsPowerRun(string powerRun, IEnumerable<string> commands)
        {
            var cmdExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            foreach (var command in commands)
            {
                var trimmed = command.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("::")) continue;
                var psi = new System.Diagnostics.ProcessStartInfo { FileName = powerRun, Arguments = $"/SW:0 \"{cmdExe}\" /c {trimmed}", UseShellExecute = true, CreateNoWindow = false };
                var proc = System.Diagnostics.Process.Start(psi)!;
                await proc.WaitForExitAsync();
                if (proc.ExitCode != 0) Service?.Log($"[DEFENDER] PowerRun exit code {proc.ExitCode} for: {trimmed}");
            }
        }

        private async Task RunAsTrustedInstallerBatAsync(string minSudo, IEnumerable<string> commands)
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"AkariDef-{Guid.NewGuid():N}.bat");
            try
            {
                var lines = new List<string> { "@echo off" };
                lines.AddRange(commands);
                await File.WriteAllLinesAsync(tmp, lines);
                var psi = new System.Diagnostics.ProcessStartInfo { FileName = minSudo, Arguments = $"--NoLogo --TrustedInstaller --Privileged cmd /c \"{tmp}\"", UseShellExecute = true, CreateNoWindow = false };
                var proc = System.Diagnostics.Process.Start(psi)!;
                await proc.WaitForExitAsync();
                if (proc.ExitCode != 0) Service?.Log($"[DEFENDER] MinSudo exit code: {proc.ExitCode}");
            }
            finally { try { File.Delete(tmp); } catch { } }
        }
    }
}
