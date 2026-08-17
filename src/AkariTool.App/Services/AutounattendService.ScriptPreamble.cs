using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using AkariTool.Tabs;

namespace AkariTool.Services
{
    public partial class AutounattendService
    {
        // ── Preamble ───────────────────────────────────────────────────────

        private static void AppendHeader(StringBuilder sb)
        {
            sb.AppendLine(@"<#
.SYNOPSIS
    Akari Tool Windows 10/11 Customization and Optimization Script
.DESCRIPTION
    Applies UWP app removals, optimizations and customizations selected in Akari Tool
.NOTES
    Requires Administrator privileges
    Compatible with Windows 10 and Windows 11
    Logs all activities to C:\ProgramData\AkariTool\Unattend\Logs\AkariTweaks.txt
.PARAMETER UserCustomizations
    When specified, applies ONLY user-specific (HKCU) tweaks.
    When not specified, applies all system-wide tweaks.
    User customizations are tracked and only apply once per user.
    To re-apply, delete: HKCU\Software\AkariTool\UserCustomizationsApplied
#>

param(
    [switch]$UserCustomizations
)");
        }

        private static void AppendLoggingSetup(StringBuilder sb)
        {
            sb.AppendLine(@"
# ============================================================================
# LOGGING SETUP
# ============================================================================

$LogPath = '" + LogPath + @"'
$null = New-Item -Path (Split-Path $LogPath) -ItemType Directory -Force

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet(""INFO"", ""SUCCESS"", ""WARNING"", ""ERROR"")]
        [string]$Level = ""INFO""
    )
    $Timestamp = Get-Date -Format ""yyyy-MM-dd HH:mm:ss""
    Add-Content -Path $LogPath -Value ""[$Timestamp] [$Level] $Message"" -Encoding UTF8
}

Write-Log ""================================================================================"" ""INFO""
Write-Log ""Akari Tool Windows Optimization & Customization Script Started"" ""INFO""
Write-Log ""Script Path: $($MyInvocation.MyCommand.Path)"" ""INFO""
if ($UserCustomizations) {
    Write-Log ""MODE: User Customizations Only (per-user tweaks)"" ""INFO""
} else {
    Write-Log ""MODE: System Customizations"" ""INFO""
}
Write-Log ""================================================================================"" ""INFO""
");
        }

        private static void AppendHelperFunctions(StringBuilder sb)
        {
            sb.AppendLine(@"
function Get-TargetUser {
    try {
        $user = Get-WmiObject Win32_ComputerSystem | Select-Object -ExpandProperty UserName
        if ($user -and $user -ne ""NT AUTHORITY\SYSTEM"") {
            $username = $user.Split('\')[1]
            if ($username -ne ""defaultuser0"") { return $username }
        }
    } catch { }
    try {
        $explorer = Get-Process explorer -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($explorer) {
            $owner = $explorer.GetOwner()
            if ($owner.User -ne ""defaultuser0"") { return $owner.User }
        }
    } catch { }
    return $null
}

function Get-UserSID {
    param($Username)
    try {
        $profListPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'
        foreach ($key in Get-ChildItem $profListPath -ErrorAction SilentlyContinue) {
            $profPath = (Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue).ProfileImagePath
            if ($profPath -and $profPath.EndsWith(""\$Username"")) { return $key.PSChildName }
        }
        return $null
    } catch { return $null }
}

function Write-EmbeddedScript {
    # Decodes a Base64-encoded script and writes it as UTF-8 (with BOM, for PS 5.1)
    param([string]$Path, [string]$Base64)
    try {
        $bytes = [byte[]](0xEF,0xBB,0xBF) + [Convert]::FromBase64String($Base64)
        [System.IO.File]::WriteAllBytes($Path, $bytes)
        Write-Log ""Created: $(Split-Path $Path -Leaf)"" ""SUCCESS""
    } catch {
        Write-Log ""Failed to create $(Split-Path $Path -Leaf): $($_.Exception.Message)"" ""ERROR""
    }
}

function Invoke-TweakScript {
    # Runs a tweak in a child powershell with a hard timeout so a misbehaving
    # script can never hang Windows Setup. -NonInteractive turns any hidden
    # prompt into an immediate failure instead of an infinite wait.
    param([string]$Path, [string]$Name, [int]$TimeoutSec = 300)
    if (-not (Test-Path $Path)) { Write-Log ""Tweak script missing: $Path"" ""WARNING""; return }
    Write-Log ""Applying tweak: $Name..."" ""INFO""
    try {
        $psExe = ""$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe""
        $p = Start-Process -FilePath $psExe -ArgumentList ""-ExecutionPolicy Bypass -NoProfile -NonInteractive -WindowStyle Hidden -File `""$Path`"""" -PassThru -WindowStyle Hidden
        if ($p.WaitForExit($TimeoutSec * 1000)) {
            Write-Log ""Tweak applied: $Name (exit $($p.ExitCode))"" ""SUCCESS""
        } else {
            taskkill /PID $p.Id /T /F 2>&1 | Out-Null
            Write-Log ""Tweak timed out after ${TimeoutSec}s and was terminated: $Name"" ""ERROR""
        }
    } catch {
        Write-Log ""Tweak failed: $Name - $($_.Exception.Message)"" ""ERROR""
    }
}

function Set-RegistryValue {
    param([string]$Path, [string]$Name, [string]$Type, $Value, [string]$Description)
    try {
        if (-not (Test-Path $Path)) { New-Item -Path $Path -Force | Out-Null }
        Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type $Type -Force
        Write-Log ""$Description | $Path\$Name = $Value"" ""SUCCESS""
    } catch {
        Write-Log ""Failed to set $Path\$Name : $($_.Exception.Message)"" ""ERROR""
    }
}");
            AppendStartProcessAsUser(sb);
        }

        private static void AppendStartProcessAsUser(StringBuilder sb)
        {
            sb.AppendLine(@"
function Start-ProcessAsUser {
    param([string]$CommandLine)

    if (-not ([System.Management.Automation.PSTypeName]'Akari.TL'.Type)) {
        Add-Type -MemberDefinition @'
[DllImport(""advapi32.dll"",SetLastError=true)]public static extern bool OpenProcessToken(IntPtr h,uint a,out IntPtr t);
[DllImport(""advapi32.dll"",SetLastError=true)]public static extern bool GetTokenInformation(IntPtr t,int c,IntPtr i,int l,out int r);
[DllImport(""advapi32.dll"",SetLastError=true)]public static extern bool DuplicateTokenEx(IntPtr t,uint a,IntPtr s,int il,int tt,out IntPtr n);
[DllImport(""advapi32.dll"",SetLastError=true,CharSet=CharSet.Unicode)]public static extern bool CreateProcessAsUserW(IntPtr t,string app,string cmd,IntPtr pa,IntPtr ta,bool ih,int cf,IntPtr env,string dir,ref SI si,out PI pi);
[DllImport(""kernel32.dll"",SetLastError=true)]public static extern bool CloseHandle(IntPtr h);
[DllImport(""kernel32.dll"")]public static extern uint WTSGetActiveConsoleSessionId();
[DllImport(""kernel32.dll"",SetLastError=true)]public static extern bool ProcessIdToSessionId(uint p,out uint s);
[DllImport(""kernel32.dll"",SetLastError=true)]public static extern uint WaitForSingleObject(IntPtr h,uint ms);
[DllImport(""kernel32.dll"",SetLastError=true)]public static extern bool GetExitCodeProcess(IntPtr h,out uint c);
[DllImport(""userenv.dll"",SetLastError=true)]public static extern bool CreateEnvironmentBlock(out IntPtr env,IntPtr token,bool inherit);
[DllImport(""userenv.dll"",SetLastError=true)]public static extern bool DestroyEnvironmentBlock(IntPtr env);
[StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]public struct SI{public int cb;public string r1,d,t;public int x,y,w,h,cc,cr,fa,fl;public short sw,r2;public IntPtr r3,i,o,e;}
[StructLayout(LayoutKind.Sequential)]public struct PI{public IntPtr hp,ht;public int pid,tid;}
'@ -Name TL -Namespace Akari -ErrorAction Stop
    }

    $T = [Akari.TL]
    $tok = $dup = $envBlock = [IntPtr]::Zero
    $pi = New-Object Akari.TL+PI
    $launched = $false

    try {
        $cs = $T::WTSGetActiveConsoleSessionId()
        if ($cs -eq 0xFFFFFFFF) { Write-Log ""No active console session"" ""ERROR""; return $false }

        $ep = $null
        foreach ($p in (Get-Process explorer -ErrorAction SilentlyContinue)) {
            $s = [uint32]0
            if ($T::ProcessIdToSessionId([uint32]$p.Id, [ref]$s) -and $s -eq $cs) { $ep = $p; break }
        }
        if (-not $ep) { Write-Log ""No explorer.exe in session $cs"" ""ERROR""; return $false }

        if (-not $T::OpenProcessToken($ep.Handle, 0x000A, [ref]$tok)) {
            Write-Log ""OpenProcessToken failed (err $([Runtime.InteropServices.Marshal]::GetLastWin32Error()))"" ""ERROR""; return $false
        }

        # If user is admin with UAC, get the linked elevated token (TokenElevationType=18, TokenLinkedToken=19)
        $dupSrc = $tok; $linked = [IntPtr]::Zero
        $eb = [Runtime.InteropServices.Marshal]::AllocHGlobal(4); $rl = 0
        if ($T::GetTokenInformation($tok, 18, $eb, 4, [ref]$rl) -and [Runtime.InteropServices.Marshal]::ReadInt32($eb) -eq 3) {
            $lb = [Runtime.InteropServices.Marshal]::AllocHGlobal([IntPtr]::Size)
            if ($T::GetTokenInformation($tok, 19, $lb, [IntPtr]::Size, [ref]$rl)) {
                $linked = [Runtime.InteropServices.Marshal]::ReadIntPtr($lb)
                $dupSrc = $linked
                Write-Log ""Using elevated linked token for admin user"" ""INFO""
            }
            [Runtime.InteropServices.Marshal]::FreeHGlobal($lb)
        }
        [Runtime.InteropServices.Marshal]::FreeHGlobal($eb)

        if (-not $T::DuplicateTokenEx($dupSrc, 0xF01FF, [IntPtr]::Zero, 2, 1, [ref]$dup)) {
            Write-Log ""DuplicateTokenEx failed (err $([Runtime.InteropServices.Marshal]::GetLastWin32Error()))"" ""ERROR""; return $false
        }
        if ($linked -ne [IntPtr]::Zero) { $null = $T::CloseHandle($linked) }

        $null = $T::CreateEnvironmentBlock([ref]$envBlock, $dup, $false)

        $si = New-Object Akari.TL+SI
        $si.cb = [Runtime.InteropServices.Marshal]::SizeOf($si)
        $si.d = ""winsta0\default""
        $psExe = ""$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe""

        if (-not $T::CreateProcessAsUserW($dup, $psExe, $CommandLine, [IntPtr]::Zero, [IntPtr]::Zero, $false, 0x08000400, $envBlock, $env:SystemRoot, [ref]$si, [ref]$pi)) {
            Write-Log ""CreateProcessAsUserW failed (err $([Runtime.InteropServices.Marshal]::GetLastWin32Error()))"" ""ERROR""; return $false
        }

        $launched = $true
        Write-Log ""Launched child process as user (PID $($pi.pid))"" ""INFO""

        $wait = $T::WaitForSingleObject($pi.hp, 600000)
        if ($wait -ne 0) { Write-Log ""Child process timed out"" ""ERROR""; return $false }

        $ec = [uint32]0
        $null = $T::GetExitCodeProcess($pi.hp, [ref]$ec)
        Write-Log ""Child process exited with code $ec"" ""INFO""
        return ($ec -eq 0)
    }
    catch { Write-Log ""Start-ProcessAsUser: $($_.Exception.Message)"" ""ERROR""; return $false }
    finally {
        if ($launched) {
            if ($pi.ht -ne [IntPtr]::Zero) { $null = $T::CloseHandle($pi.ht) }
            if ($pi.hp -ne [IntPtr]::Zero) { $null = $T::CloseHandle($pi.hp) }
        }
        if ($envBlock -ne [IntPtr]::Zero) { $null = $T::DestroyEnvironmentBlock($envBlock) }
        if ($dup -ne [IntPtr]::Zero) { $null = $T::CloseHandle($dup) }
        if ($tok -ne [IntPtr]::Zero) { $null = $T::CloseHandle($tok) }
    }
}");
        }

    }
}
