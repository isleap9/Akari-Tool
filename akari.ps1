# ── Admin elevation ──────────────────────────────────────────────────────────
If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator")) {
    Start-Process PowerShell.exe -ArgumentList ("-NoProfile -ExecutionPolicy Bypass -File `"{0}`"" -f $PSCommandPath) -Verb RunAs
    Exit
}

# ── WPF assemblies ───────────────────────────────────────────────────────────
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms

# ── DWM P/Invoke  (Mica backdrop + dark title bar on Win11) ─────────────────
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class DwmApi {
    // Mica / Acrylic backdrop
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // Extend frame into client area (required for Mica)
    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS { public int Left, Right, Top, Bottom; }
}
"@

# ── Shared state across runspaces ────────────────────────────────────────────
$sync             = [Hashtable]::Synchronized(@{})
$sync.configs     = @{}
$sync.runspaces   = [System.Collections.Generic.List[hashtable]]::new()

function Start-Elevated {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string]$ArgumentList,
        [switch]$Wait
    )
    $isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator")
    $params = @{ FilePath = $FilePath }
    if ($ArgumentList) { $params.ArgumentList = $ArgumentList }
    if ($Wait)         { $params.Wait = $true }
    if (-not $isElevated) { $params.Verb = "RunAs" }
    Start-Process @params
}

function Invoke-RunInBackground {
    <#
    .SYNOPSIS
        Runs a scriptblock on a background runspace so the WPF UI stays responsive.
    .PARAMETER ScriptBlock
        The code to run. Has access to $sync (already passed in).
    .PARAMETER StatusStart
        Text shown in the status bar while the job is running.
    .PARAMETER StatusDone
        Text shown when the job completes successfully.
    #>
    param(
        [Parameter(Mandatory)][scriptblock]$ScriptBlock,
        [string]$StatusStart = "Running...",
        [string]$StatusDone  = "Done."
    )

    Set-Status $StatusStart "#AAAAAA"

    $rs = [runspacefactory]::CreateRunspace()
    $rs.ApartmentState = "STA"
    $rs.ThreadOptions  = "ReuseThread"
    $rs.Open()
    $rs.SessionStateProxy.SetVariable("sync", $sync)

    $ps = [powershell]::Create().AddScript($ScriptBlock)
    $ps.Runspace = $rs

    $handle = $ps.BeginInvoke()

    # Track runspace for cleanup
    $sync.runspaces.Add(@{ ps = $ps; handle = $handle; rs = $rs })

    # Completion watcher on a timer
    $timer          = New-Object System.Windows.Threading.DispatcherTimer
    $timer.Interval = [timespan]::FromMilliseconds(300)
    $done           = $StatusDone

    $timer.Add_Tick({
        if ($handle.IsCompleted) {
            $timer.Stop()
            try   { $ps.EndInvoke($handle) } catch {}
            $rs.Close()
            $rs.Dispose()
            Set-Status $done "#66BB6A"
        }
    }.GetNewClosure())

    $timer.Start()
}

function Invoke-UltimateScript {
    <#
    .SYNOPSIS
        Runs the CURRENT upstream FR33THY "Ultimate" script for a repo-relative
        path in an elevated PowerShell console (menu-driven, always latest).
    .DESCRIPTION
        Heavy / frequently-updated tweaks are not re-implemented inline in Akari.
        Instead they delegate to the live upstream script so they never need
        re-porting when Ultimate changes. To retarget in the future, change only
        the -Path passed by the button handler.
    .PARAMETER Path
        Repo-relative path, e.g. "8 Advanced/1 Defender.ps1". Spaces and symbols
        are URL-encoded per segment automatically.
    .PARAMETER Confirm
        Optional prompt text. When set, a Yes/No warning dialog is shown first and
        the script only runs on Yes.
    .PARAMETER Status
        Status-bar text shown while launching.
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [string]$Confirm,
        [string]$Status = "Launching Ultimate script..."
    )

    if ($Confirm) {
        $r = [System.Windows.MessageBox]::Show($Confirm, "Akari Tool", "YesNo", "Warning")
        if ($r -ne "Yes") { return }
    }

    # URL-encode each path segment but keep the separators
    $enc = ($Path -split '/' | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/'
    $url = "https://raw.githubusercontent.com/FR33THYFR33THY/Ultimate/main/$enc"

    Set-Status $Status "#AAAAAA"
    Start-Elevated -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"& { iwr '$url' -UseBasicParsing | iex }`""
    Set-Status "Ultimate script launched (menu-driven console)." "#66BB6A"
}

# Defender disable/enable (launches a PS window — needs TrustedInstaller)
function Invoke-BtnDefenderDisable {
    Invoke-UltimateScript -Path "8 Advanced/1 Defender.ps1" -Confirm "Disable Windows Defender? This is an advanced operation that restarts into Safe Mode." -Status "Opening Defender script..."
}
function Invoke-BtnDefenderEnable {
    Start-Elevated -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"Set-MpPreference -DisableRealtimeMonitoring `$false`""
    Set-Status "Defender re-enable initiated."
}

# Firewall
function Invoke-BtnFirewallDisable {
    Invoke-RunInBackground -StatusStart "Disabling firewall..." -StatusDone "Firewall disabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile`" /v `"EnableFirewall`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile`" /v `"EnableFirewall`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
    }
}
function Invoke-BtnFirewallEnable {
    Invoke-RunInBackground -StatusStart "Enabling firewall..." -StatusDone "Firewall enabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile`" /v `"EnableFirewall`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile`" /v `"EnableFirewall`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}

# Spectre / Meltdown
function Invoke-BtnSpectreDisable {
    Invoke-RunInBackground -StatusStart "Disabling Spectre/Meltdown mitigations..." -StatusDone "Mitigations disabled. Restart required." -ScriptBlock {
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Control\Session Manager\Memory Management`" /v `"FeatureSettingsOverrideMask`" /t REG_DWORD /d `"3`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Control\Session Manager\Memory Management`" /v `"FeatureSettingsOverride`" /t REG_DWORD /d `"3`" /f >nul 2>&1"
    }
}
function Invoke-BtnSpectreEnable {
    Invoke-RunInBackground -StatusStart "Enabling Spectre/Meltdown mitigations..." -StatusDone "Mitigations enabled. Restart required." -ScriptBlock {
        cmd /c "reg delete `"HKLM\SYSTEM\ControlSet001\Control\Session Manager\Memory Management`" /v `"FeatureSettingsOverrideMask`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\ControlSet001\Control\Session Manager\Memory Management`" /v `"FeatureSettingsOverride`" /f >nul 2>&1"
    }
}

# DEP
function Invoke-BtnDepDisable {
    Invoke-RunInBackground -StatusStart "Disabling DEP..." -StatusDone "DEP disabled. Restart required." -ScriptBlock {
        cmd /c "bcdedit /set nx AlwaysOff >nul 2>&1"
    }
}
function Invoke-BtnDepEnable {
    Invoke-RunInBackground -StatusStart "Enabling DEP..." -StatusDone "DEP enabled. Restart required." -ScriptBlock {
        cmd /c "bcdedit /deletevalue nx >nul 2>&1"
    }
}

# File download security warning
function Invoke-BtnDlWarnDisable {
    Invoke-RunInBackground -StatusStart "Disabling download warnings..." -StatusDone "Download warnings disabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Internet Explorer\Security`" /v `"DisableSecuritySettingsCheck`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3`" /v `"1806`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3`" /v `"1806`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
    }
}
function Invoke-BtnDlWarnEnable {
    Invoke-RunInBackground -StatusStart "Enabling download warnings..." -StatusDone "Download warnings enabled." -ScriptBlock {
        cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Internet Explorer`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3`" /v `"1806`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3`" /v `"1806`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}

# Services
function Invoke-BtnServicesOff {
    Invoke-UltimateScript -Path "8 Advanced/17 Services.ps1" -Confirm "Disable non-essential services? A restore point will be created first." -Status "Opening services script..."
}
function Invoke-BtnServicesDefault {
    Invoke-RunInBackground -StatusStart "Restoring default services..." -StatusDone "Services restored." -ScriptBlock {
        $services = @("wuauserv","WSearch","SysMain","DiagTrack","PcaSvc","WerSvc","EventLog","Schedule","DPS","BITS")
        foreach ($s in $services) { try { Set-Service -Name $s -StartupType Automatic -ErrorAction SilentlyContinue } catch {} }
    }
}

# MMAgent
function Invoke-BtnMMAgentOff {
    Invoke-RunInBackground -StatusStart "Disabling MMAgent features..." -StatusDone "MMAgent features disabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters`" /v `"EnablePrefetcher`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Disable-MMAgent -ApplicationLaunchPrefetching -ErrorAction SilentlyContinue | Out-Null
        Disable-MMAgent -ApplicationPreLaunch -ErrorAction SilentlyContinue | Out-Null
        Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue | Out-Null
        Disable-MMAgent -OperationAPI -ErrorAction SilentlyContinue | Out-Null
        Disable-MMAgent -PageCombining -ErrorAction SilentlyContinue | Out-Null
        Set-MMAgent -MaxOperationAPIFiles 1 -ErrorAction SilentlyContinue | Out-Null
    }
}
function Invoke-BtnMMAgentDefault {
    Invoke-RunInBackground -StatusStart "Restoring MMAgent defaults..." -StatusDone "MMAgent restored." -ScriptBlock {
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters`" /v `"EnablePrefetcher`" /t REG_DWORD /d `"3`" /f >nul 2>&1"
        Enable-MMAgent -ApplicationLaunchPrefetching -ErrorAction SilentlyContinue | Out-Null
        Enable-MMAgent -ApplicationPreLaunch -ErrorAction SilentlyContinue | Out-Null
        Set-MMAgent -MaxOperationAPIFiles 512 -ErrorAction SilentlyContinue | Out-Null
        Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue | Out-Null
        Enable-MMAgent -OperationAPI -ErrorAction SilentlyContinue | Out-Null
        Disable-MMAgent -PageCombining -ErrorAction SilentlyContinue | Out-Null
    }
}

# NVME Faster Driver
function Invoke-BtnNvmeOn {
    Invoke-RunInBackground -StatusStart "Enabling NVME faster driver..." -StatusDone "NVME driver set. Restart required." -ScriptBlock {
        $base = "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Policies\Microsoft\FeatureManagement\Overrides"
        foreach ($v in @("735209102","3244671118","1853569164","156965516")) {
            cmd /c "reg add `"$base`" /v `"$v`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        }
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\Network\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}`" /ve /d `"Storage disks`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}`" /ve /d `"Storage disks`" /f >nul 2>&1"
    }
}
function Invoke-BtnNvmeDefault {
    Invoke-RunInBackground -StatusStart "Restoring NVME default driver..." -StatusDone "NVME driver restored. Restart required." -ScriptBlock {
        cmd /c "reg delete `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Policies\Microsoft`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\Network\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}`" /f >nul 2>&1"
    }
}

# Shell / Mobsync
function Invoke-BtnShellOff {
    Invoke-UltimateScript -Path "8 Advanced/18 Start Search Shell Mobsync.ps1" -Status "Opening Shell/Search/Mobsync script..."
}
function Invoke-BtnShellDefault {
    Invoke-RunInBackground -StatusStart "Restoring shell defaults..." -StatusDone "Shell restored. Restart required." -ScriptBlock {
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\WSearch`" /v `"Start`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
        Start-Process explorer
    }
}

# MPO
function Invoke-BtnMpoOn {
    Invoke-RunInBackground -StatusStart "Enabling MPO..." -StatusDone "MPO enabled." -ScriptBlock {
        cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\Dwm`" /v `"OverlayTestMode`" /f >nul 2>&1"
        reg add "HKCU\Software\Microsoft\DirectX\UserGpuPreferences" /v "DirectXUserGlobalSettings" /t REG_SZ /d "VRROptimizeEnable=0;SwapEffectUpgradeEnable=1;" /f | Out-Null
    }
}
function Invoke-BtnMpoOff {
    Invoke-RunInBackground -StatusStart "Disabling MPO..." -StatusDone "MPO disabled." -ScriptBlock {
        reg add "HKLM\SOFTWARE\Microsoft\Windows\Dwm" /v "OverlayTestMode" /t REG_DWORD /d "5" /f | Out-Null
        reg add "HKCU\Software\Microsoft\DirectX\UserGpuPreferences" /v "DirectXUserGlobalSettings" /t REG_SZ /d "VRROptimizeEnable=0;SwapEffectUpgradeEnable=0;" /f | Out-Null
    }
}

# Flip modes
function Invoke-BtnFlipFSO {
    Invoke-RunInBackground -StatusStart "Setting FSO flip mode..." -StatusDone "FSO set." -ScriptBlock {
        reg add "HKCU\System\GameConfigStore" /v "GameDVR_DXGIHonorFSEWindowsCompatible" /t REG_DWORD /d "0" /f | Out-Null
        reg add "HKCU\System\GameConfigStore" /v "GameDVR_FSEBehaviorMode" /t REG_DWORD /d "0" /f | Out-Null
        cmd /c "reg delete `"HKCU\System\GameConfigStore`" /v `"GameDVR_FSEBehavior`" /f >nul 2>&1"
        reg add "HKCU\System\GameConfigStore" /v "GameDVR_HonorUserFSEBehaviorMode" /t REG_DWORD /d "0" /f | Out-Null
    }
}
function Invoke-BtnFlipFSE {
    Invoke-RunInBackground -StatusStart "Setting FSE flip mode..." -StatusDone "FSE set." -ScriptBlock {
        reg add "HKCU\System\GameConfigStore" /v "GameDVR_DXGIHonorFSEWindowsCompatible" /t REG_DWORD /d "1" /f | Out-Null
        reg add "HKCU\System\GameConfigStore" /v "GameDVR_FSEBehaviorMode" /t REG_DWORD /d "2" /f | Out-Null
        reg add "HKCU\System\GameConfigStore" /v "GameDVR_FSEBehavior" /t REG_DWORD /d "2" /f | Out-Null
        reg add "HKCU\System\GameConfigStore" /v "GameDVR_HonorUserFSEBehaviorMode" /t REG_DWORD /d "1" /f | Out-Null
    }
}

# HCIF
function Invoke-BtnHcif {
    Invoke-RunInBackground -StatusStart "Setting HCIF flip..." -StatusDone "HCIF set." -ScriptBlock {
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler`" /v `"ForceFlipTrueImmediateMode`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}
function Invoke-BtnHif {
    Invoke-RunInBackground -StatusStart "Setting HIF flip (default)..." -StatusDone "HIF set." -ScriptBlock {
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler`" /f >nul 2>&1"
    }
}

# ULPS
function Invoke-BtnUlpsOn {
    Invoke-RunInBackground -StatusStart "Enabling AMD ULPS..." -StatusDone "ULPS enabled." -ScriptBlock {
        $subkeys = (Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue).Name
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "EnableUlps" /t REG_DWORD /d "1" /f | Out-Null } }
    }
}
function Invoke-BtnUlpsOff {
    Invoke-RunInBackground -StatusStart "Disabling AMD ULPS..." -StatusDone "ULPS disabled." -ScriptBlock {
        $subkeys = (Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue).Name
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "EnableUlps" /t REG_DWORD /d "0" /f | Out-Null } }
    }
}

# ReBar — full upstream NVIDIA Profile Inspector + DRS flow (Default / Force On / Force Off / To Bios)
function Invoke-BtnReBarDefault { Invoke-UltimateScript -Path "8 Advanced/7 ReBar Force.ps1" -Status "ReBar Force opened — pick Default (driver whitelist)." }
function Invoke-BtnReBarOn      { Invoke-UltimateScript -Path "8 Advanced/7 ReBar Force.ps1" -Status "ReBar Force opened — pick Force On." }

# SMT / Core — real handlers live in Invoke-Missing.ps1 (delegate to upstream)

# WHQL Bypass
function Invoke-BtnWhqlBypass {
    Invoke-RunInBackground -StatusStart "Enabling WHQL bypass..." -StatusDone "WHQL bypass enabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy`" /v `"WHQLSettings`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}

# Invoke-BtnSpaceCheck has moved to Invoke-Check.ps1 (its logical home on the Check tab).
# This file is intentionally left as a stub to avoid a duplicate function definition.
# Safe to delete.

# Check tab mirrors Ultimate 1:1 — PC check (OCCT) and BIOS.
function Invoke-BtnCheckPC   { Invoke-UltimateScript -Path "1 Check/2 PC.ps1" -Status "PC Check opened (OCCT + guidance)." }
function Invoke-BtnCheckBios { Invoke-UltimateScript -Path "1 Check/1 Bios.ps1" -Status "BIOS check opened." }

# DDU (Driver Clean) — Auto / Manual menu, delegates to live upstream
function Invoke-BtnDduAuto   { Invoke-UltimateScript -Path "5 Graphics/1 Driver Clean.ps1" -Status "DDU (Driver Clean) opened — pick Auto." }
function Invoke-BtnDduManual { Invoke-UltimateScript -Path "5 Graphics/1 Driver Clean.ps1" -Status "DDU (Driver Clean) opened — pick Manual." }

# Driver updated install — NVIDIA / AMD / Intel menu, delegates to live upstream
function Invoke-BtnDriverNvidia { Invoke-UltimateScript -Path "5 Graphics/2 Driver Updated Install.ps1" -Status "Driver install opened — pick NVIDIA." }
function Invoke-BtnDriverAmd    { Invoke-UltimateScript -Path "5 Graphics/2 Driver Updated Install.ps1" -Status "Driver install opened — pick AMD." }
function Invoke-BtnDriverIntel  { Invoke-UltimateScript -Path "5 Graphics/2 Driver Updated Install.ps1" -Status "Driver install opened — pick Intel." }

# Debloat driver install & settings — delegates to live upstream
function Invoke-BtnDriverDebloatNvidia { Invoke-UltimateScript -Path "5 Graphics/4 Driver Debloat Install & Settings.ps1" -Status "Debloat driver install opened — pick NVIDIA." }
function Invoke-BtnDriverDebloatAmd    { Invoke-UltimateScript -Path "5 Graphics/4 Driver Debloat Install & Settings.ps1" -Status "Debloat driver install opened — pick AMD." }
function Invoke-BtnDriverDebloatIntel  { Invoke-UltimateScript -Path "5 Graphics/4 Driver Debloat Install & Settings.ps1" -Status "Debloat driver install opened — pick Intel." }

# GPU Settings — full upstream scripts (On/Default menu), delegates to live upstream
function Invoke-BtnNvidiaOn      { Invoke-UltimateScript -Path "5 Graphics/5 Nvidia Settings.ps1" -Status "NVIDIA settings opened — pick On." }
function Invoke-BtnNvidiaDefault { Invoke-UltimateScript -Path "5 Graphics/5 Nvidia Settings.ps1" -Status "NVIDIA settings opened — pick Default." }
function Invoke-BtnAmdOn         { Invoke-UltimateScript -Path "5 Graphics/6 Amd Settings.ps1" -Status "AMD settings opened — pick On." }
function Invoke-BtnAmdDefault    { Invoke-UltimateScript -Path "5 Graphics/6 Amd Settings.ps1" -Status "AMD settings opened — pick Default." }
function Invoke-BtnIntelOn       { Invoke-UltimateScript -Path "5 Graphics/7 Intel Settings.ps1" -Status "Intel settings opened — pick On." }
function Invoke-BtnIntelDefault  { Invoke-UltimateScript -Path "5 Graphics/7 Intel Settings.ps1" -Status "Intel settings opened — pick Default." }

# HDCP
function Invoke-BtnHdcpOff {
    Invoke-RunInBackground -StatusStart "Disabling HDCP..." -StatusDone "HDCP disabled." -ScriptBlock {
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RMHdcpKeyglobZero" /t REG_DWORD /d "1" /f | Out-Null } }
    }
}
function Invoke-BtnHdcpDefault {
    Invoke-RunInBackground -StatusStart "Enabling HDCP..." -StatusDone "HDCP enabled." -ScriptBlock {
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RMHdcpKeyglobZero" /t REG_DWORD /d "0" /f | Out-Null } }
    }
}

# P0 State
function Invoke-BtnP0On {
    Invoke-RunInBackground -StatusStart "Enabling P0 state..." -StatusDone "P0 state enabled." -ScriptBlock {
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "DisableDynamicPstate" /t REG_DWORD /d "1" /f | Out-Null } }
    }
}
function Invoke-BtnP0Default {
    Invoke-RunInBackground -StatusStart "Restoring P0 state..." -StatusDone "P0 state restored." -ScriptBlock {
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "DisableDynamicPstate" /t REG_DWORD /d "0" /f | Out-Null } }
    }
}

# MSI Mode
function Invoke-BtnMsiOn {
    Invoke-RunInBackground -StatusStart "Enabling MSI mode for GPU..." -StatusDone "MSI mode enabled." -ScriptBlock {
        $gpus = Get-PnpDevice -Class Display
        foreach ($gpu in $gpus) {
            cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$($gpu.InstanceId)\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        }
    }
}
function Invoke-BtnMsiOff {
    Invoke-RunInBackground -StatusStart "Disabling MSI mode for GPU..." -StatusDone "MSI mode disabled." -ScriptBlock {
        $gpus = Get-PnpDevice -Class Display
        foreach ($gpu in $gpus) {
            cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$($gpu.InstanceId)\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        }
    }
}

# DirectX & C++
function Invoke-BtnDirectX {
    Invoke-RunInBackground -StatusStart "Installing DirectX..." -StatusDone "DirectX installed." -ScriptBlock {
        $progresspreference='silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.DirectX`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
    }
}
function Invoke-BtnCppRuntime {
    Invoke-RunInBackground -StatusStart "Installing C++ runtimes..." -StatusDone "C++ runtimes installed." -ScriptBlock {
        $progresspreference='silentlycontinue'
        $packages = @(
            "Microsoft.VCRedist.2005.x86","Microsoft.VCRedist.2005.x64",
            "Microsoft.VCRedist.2008.x86","Microsoft.VCRedist.2008.x64",
            "Microsoft.VCRedist.2010.x86","Microsoft.VCRedist.2010.x64",
            "Microsoft.VCRedist.2012.x86","Microsoft.VCRedist.2012.x64",
            "Microsoft.VCRedist.2013.x86","Microsoft.VCRedist.2013.x64",
            "Microsoft.VCRedist.2015+.x86","Microsoft.VCRedist.2015+.x64"
        )
        foreach ($pkg in $packages) { try { Start-Process "winget" -ArgumentList "install `"$pkg`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {} }
    }
}

function Invoke-BtnResolution { Start-Process "ms-settings:display" }
function Invoke-BtnHags       { Start-Process "ms-settings:display-advancedgraphics" }

# Scaling Higher No Accel — 1:1 with Ultimate "7 Hardware/1 Scaling Higher No Accel.ps1"
# (per-scaling DPI + SmoothMouse compensation curves), driven by the CboScaling dropdown.
function Invoke-BtnScalingApply {
    # dpi, MouseSpeed, Threshold1/2, EnablePerProcessSystemDPI, SmoothMouseXCurve, SmoothMouseYCurve per level
    $map = @{
        "100%" = @{ dpi=96;  spd=0; t1=0; t2=0;  pp=0; x="0000000000000000c0cc0c00000000008099190000000000406626000000000000333300000000000"; y="0000000000000000000038000000000000007000000000000000a800000000000000e00000000000" }
        "125%" = @{ dpi=120; spd=1; t1=6; t2=10; pp=1; x="00000000000000000000100000000000000020000000000000003000000000000000400000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "150%" = @{ dpi=144; spd=1; t1=6; t2=10; pp=1; x="0000000000000000303313000000000060662600000000009099390000000000C0CC4C0000000000"; y="0000000000000000000038000000000000007000000000000000A800000000000000E00000000000" }
        "175%" = @{ dpi=168; spd=1; t1=6; t2=10; pp=1; x="00000000000000006066160000000000C0CC2C000000000020334300000000008099590000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "200%" = @{ dpi=192; spd=1; t1=6; t2=10; pp=1; x="00000000000000009099190000000000203333000000000B0CC4C000000000040666600000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "225%" = @{ dpi=216; spd=1; t1=6; t2=10; pp=1; x="0000000000000000C0CC1C0000000000809939000000000040665600000000000033730000000000"; y="0000000000000000000038000000000000007000000000000000A800000000000000E00000000000" }
        "250%" = @{ dpi=240; spd=1; t1=6; t2=10; pp=1; x="00000000000000000000200000000000000040000000000000006000000000000000800000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "300%" = @{ dpi=288; spd=1; t1=6; t2=10; pp=1; x="00000000000000006066260000000000C0CC4C000000000020337300000000008099990000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
        "350%" = @{ dpi=336; spd=1; t1=6; t2=10; pp=1; x="0000000000000000C0CC2C000000000080995900000000004066860000000000003B300000000000"; y="00000000000000000000380000000000000070000000000000A800000000000000E0000000000000" }
    }
    $selected = $sync.CboScaling.Dispatcher.Invoke([func[object]]{ $sync.CboScaling.Text })
    $s = $map[$selected]
    if (-not $s) { Set-Status "Select a scaling value first."; return }
    Invoke-RunInBackground -StatusStart "Applying $selected scaling..." -StatusDone "Scaling applied. Log out to apply." -ScriptBlock {
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSensitivity`" /t REG_SZ /d `"10`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSpeed`" /t REG_SZ /d `"$($s.spd)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold1`" /t REG_SZ /d `"$($s.t1)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold2`" /t REG_SZ /d `"$($s.t2)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseXCurve`" /t REG_BINARY /d `"$($s.x)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseYCurve`" /t REG_BINARY /d `"$($s.y)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"Win8DpiScaling`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"LogPixels`" /t REG_DWORD /d `"$($s.dpi)`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"EnablePerProcessSystemDPI`" /t REG_DWORD /d `"$($s.pp)`" /f >nul 2>&1"
    }.GetNewClosure()
}

function Invoke-BtnMonitorOpt { Invoke-UltimateScript -Path "7 Hardware/6 Monitor Optimization.ps1" -Status "Monitor Optimization opened." }

# Polling Rate
function Invoke-BtnPollingOff {
    Invoke-RunInBackground -StatusStart "Disabling background polling cap..." -StatusDone "Polling cap removed." -ScriptBlock {
        cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"RawMouseThrottleEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
    }
}
function Invoke-BtnPollingDefault {
    Invoke-RunInBackground -StatusStart "Restoring polling cap..." -StatusDone "Polling cap restored." -ScriptBlock {
        cmd /c "reg delete `"HKCU\Control Panel\Mouse`" /v `"RawMouseThrottleEnabled`" /f >nul 2>&1"
    }
}

function Invoke-BtnMouseTest       { Invoke-UltimateScript -Path "7 Hardware/3 Mouse Polling Rate Test.ps1" -Status "Mouse Polling Rate Test opened." }
function Invoke-BtnControllerOC     { Invoke-UltimateScript -Path "7 Hardware/4 Controller Overclock.ps1" -Status "Controller Overclock (hidusbf) opened." }
function Invoke-BtnControllerTest   { Invoke-UltimateScript -Path "7 Hardware/5 Controller Polling Rate Test.ps1" -Status "Controller Polling Rate Test opened." }

function Invoke-BtnBufferbloat  { Start-Process "https://www.waveform.com/tools/bufferbloat" }
function Invoke-BtnPcBuildGuide { Start-Process "https://pcpartpicker.com/user/fr33thy/saved" }

# Per-app installers — 1:1 with Ultimate "4 Installers/1 Installers.ps1"
# (winget install + debloat config + shortcut cleanup), wrapped in Akari's GUI runner.

# ── Steam ─────────────────────────────────────────────────────────────────────
function Invoke-BtnInstSteam {
    Invoke-RunInBackground -StatusStart "Installing Steam..." -StatusDone "Steam installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Valve.Steam`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"Steam`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Steam\Steam.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Steam" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Epic Games ────────────────────────────────────────────────────────────────
function Invoke-BtnInstEpic {
    Invoke-RunInBackground -StatusStart "Installing Epic Games..." -StatusDone "Epic Games installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"EpicGames.EpicGamesLauncher`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"EpicGamesLauncher`" /f >nul 2>&1"
    }
}

# ── Battle.net ────────────────────────────────────────────────────────────────
function Invoke-BtnInstBattlenet {
    Invoke-RunInBackground -StatusStart "Installing Battle.net..." -StatusDone "Battle.net installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Blizzard.BattleNet`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade --location `"$env:SystemDrive\Program Files (x86)\Battle.net`"" -Wait -WindowStyle Hidden } catch {}
        Start-Sleep -Seconds 10
        Get-Process -Name "getinstaller" -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"Battle.net`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`" /v `"Battle.net`" /f >nul 2>&1"
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Battle.net" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Battle.net.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files (x86)\Battle.net\Battle.net Launcher.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Battle.net"; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Battle.net.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files (x86)\Battle.net\Battle.net Launcher.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Battle.net"; $sc2.Save()
    }
}

# ── Electronic Arts ───────────────────────────────────────────────────────────
function Invoke-BtnInstEA {
    Invoke-RunInBackground -StatusStart "Installing EA App..." -StatusDone "EA App installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"ElectronicArts.EADesktop`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Start-Sleep -Seconds 10
        Get-Process -Name "EAappInstaller" -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"EADM`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`" /v `"EADM`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\EA\EA.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\EA" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Ubisoft Connect ───────────────────────────────────────────────────────────
function Invoke-BtnInstUbisoft {
    Invoke-RunInBackground -StatusStart "Installing Ubisoft Connect..." -StatusDone "Ubisoft Connect installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Ubisoft.Connect`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Ubisoft\Ubisoft Connect\Ubisoft Connect.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Ubisoft" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Rockstar Games ────────────────────────────────────────────────────────────
function Invoke-BtnInstRockstar {
    Invoke-RunInBackground -StatusStart "Installing Rockstar Games..." -StatusDone "Rockstar Games installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"RockstarGames.Launcher`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Rockstar Games\Rockstar Games Launcher.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Rockstar Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── League of Legends ─────────────────────────────────────────────────────────
function Invoke-BtnInstLOL {
    Invoke-RunInBackground -StatusStart "Installing League of Legends..." -StatusDone "League of Legends installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"RiotGames.LeagueOfLegends.NA`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"RiotClient`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Riot Games\*" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Riot Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Riot Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Valorant ──────────────────────────────────────────────────────────────────
function Invoke-BtnInstValorant {
    Invoke-RunInBackground -StatusStart "Installing Valorant..." -StatusDone "Valorant installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"RiotGames.Valorant.NA`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"RiotClient`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Riot Games\*" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Riot Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Riot Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Escape from Tarkov ────────────────────────────────────────────────────────
function Invoke-BtnInstEFT {
    Invoke-RunInBackground -StatusStart "Installing Escape From Tarkov..." -StatusDone "Escape From Tarkov installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        Invoke-WebRequest "https://launcher.escapefromtarkov.com/launcher/download" -OutFile "$env:SystemRoot\Temp\Escape From Tarkov.exe"
        Start-Process -Wait "$env:SystemRoot\Temp\Escape From Tarkov.exe" -ArgumentList "/VERYSILENT /NORESTART"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Battlestate Games Launcher.lnk"); $sc.TargetPath = "$env:SystemDrive\Battlestate Games\BsgLauncher\BsgLauncher.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Battlestate Games\BsgLauncher"; $sc.Save()
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Battlestate Games\Battlestate Games Launcher.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Battlestate Games" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Roblox ────────────────────────────────────────────────────────────────────
function Invoke-BtnInstRoblox {
    Invoke-RunInBackground -StatusStart "Installing Roblox..." -StatusDone "Roblox installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Roblox.Roblox`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $sh = New-Object -ComObject WScript.Shell
        $sc = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Roblox.url"); $sc.TargetPath = "roblox://placeId=0"; $sc.Save()
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc2 = $sh.CreateShortcut("$Desktop\Roblox.url"); $sc2.TargetPath = "roblox://placeId=0"; $sc2.Save()
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Roblox" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Google Chrome ─────────────────────────────────────────────────────────────
function Invoke-BtnInstChrome {
    Invoke-RunInBackground -StatusStart "Installing Google Chrome..." -StatusDone "Chrome installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Google.Chrome`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist`" /v `"1`" /t REG_SZ /d `"ddkjiahejlhfcafbddmgiahcphecmpfh;https://clients2.google.com/service/update2/crx`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Google\Chrome`" /v `"HardwareAccelerationModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Google\Chrome`" /v `"BackgroundModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Google\Chrome`" /v `"HighEfficiencyModeEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $basePath = "HKLM:\Software\Microsoft\Active Setup\Installed Components"
        Get-ChildItem $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            $val = (Get-ItemProperty $_.PsPath)."(default)"
            if ($val -like "*Chrome*") { Remove-Item $_.PsPath -Force -ErrorAction SilentlyContinue }
        }
        Get-Service | Where-Object { $_.Name -match 'Google' } | ForEach-Object { cmd /c "sc stop `"$($_.Name)`" >nul 2>&1"; cmd /c "sc delete `"$($_.Name)`" >nul 2>&1" }
        Get-ScheduledTask | Where-Object { $_.TaskName -like '*Google*' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
    }
}

# ── Brave ─────────────────────────────────────────────────────────────────────
function Invoke-BtnInstBrave {
    Invoke-RunInBackground -StatusStart "Installing Brave..." -StatusDone "Brave installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Brave.Brave`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\BraveSoftware\Brave\ExtensionInstallForcelist`" /v `"1`" /t REG_SZ /d `"ddkjiahejlhfcafbddmgiahcphecmpfh;https://clients2.google.com/service/update2/crx`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\BraveSoftware\Brave`" /v `"HardwareAccelerationModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\BraveSoftware\Brave`" /v `"BackgroundModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\BraveSoftware\Brave`" /v `"HighEfficiencyModeEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $basePath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Get-Item $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            foreach ($valueName in $_.GetValueNames()) { if ($valueName -like "*Brave*") { Remove-ItemProperty -Path $_.PsPath -Name $valueName -Force -ErrorAction SilentlyContinue } }
        }
        Get-Service | Where-Object { $_.Name -match 'Brave' } | ForEach-Object { cmd /c "sc stop `"$($_.Name)`" >nul 2>&1"; cmd /c "sc delete `"$($_.Name)`" >nul 2>&1" }
        Get-ScheduledTask | Where-Object { $_.TaskName -like '*Brave*' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Brave.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Firefox ───────────────────────────────────────────────────────────────────
function Invoke-BtnInstFirefox {
    Invoke-RunInBackground -StatusStart "Installing Firefox..." -StatusDone "Firefox installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Mozilla.Firefox`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Start-Process -FilePath "C:\Program Files (x86)\Mozilla Maintenance Service\uninstall.exe" -ArgumentList "/S" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue
        Get-ScheduledTask | Where-Object { $_.Taskname -match 'Firefox' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
        $uBlockDir = "C:\Program Files\Mozilla Firefox\distribution\extensions"
        If (!(Test-Path $uBlockDir)) { New-Item -ItemType Directory -Path $uBlockDir -Force | Out-Null }
        Invoke-WebRequest "https://addons.mozilla.org/firefox/downloads/latest/ublock-origin/latest.xpi" -OutFile "$uBlockDir\uBlock0@raymondhill.net.xpi"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Mozilla\Firefox`" /v `"AppAutoUpdate`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Start-Process -FilePath "$env:SystemDrive\Program Files\Mozilla Firefox\firefox.exe" -ArgumentList "--headless"
        Start-Sleep -Seconds 5
        Stop-Process -Name "firefox" -Force -ErrorAction SilentlyContinue
        $JsFile = @'
user_pref("layers.acceleration.disabled", true);
user_pref("gfx.direct2d.disabled", true);
'@
        $FireFoxProfile = Get-ChildItem "$env:APPDATA\Mozilla\Firefox\Profiles" -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '\.default-release$' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($FireFoxProfile) { [System.IO.File]::WriteAllText("$($FireFoxProfile.FullName)\user.js", $JsFile, [System.Text.UTF8Encoding]::new($false)) }
        $basePath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Get-Item $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            foreach ($valueName in $_.GetValueNames()) { if ($valueName -like "*Firefox*") { Remove-ItemProperty -Path $_.PsPath -Name $valueName -Force -ErrorAction SilentlyContinue } }
        }
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Firefox Private Browsing.lnk" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Firefox.lnk" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Discord ───────────────────────────────────────────────────────────────────
function Invoke-BtnInstDiscord {
    Invoke-RunInBackground -StatusStart "Installing Discord..." -StatusDone "Discord installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        New-Item -Path "$env:APPDATA\discord\settings.json" -ItemType File -Force | Out-Null
        $DiscordSettings = @'
{
    "SKIP_HOST_UPDATE": true,
    "DEVELOPER_MODE": true,
    "enableHardwareAcceleration": false,
    "MINIMIZE_TO_TRAY": true,
    "OPEN_ON_STARTUP": false,
    "START_MINIMIZED": false,
    "IS_MAXIMIZED": true,
    "IS_MINIMIZED": false,
    "debugLogging": false
}
'@
        Set-Content -Path "$env:APPDATA\discord\settings.json" -Value $DiscordSettings -Force | Out-Null
        try { Start-Process "winget" -ArgumentList "install `"Discord.Discord`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Start-Sleep -Seconds 10
        Get-Process -Name "Update" -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"Discord`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`" /v `"Discord`" /f >nul 2>&1"
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Discord.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\Discord Inc" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Spotify ───────────────────────────────────────────────────────────────────
function Invoke-BtnInstSpotify {
    Invoke-RunInBackground -StatusStart "Installing Spotify..." -StatusDone "Spotify installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        New-Item -Path "$env:APPDATA\Spotify\prefs" -ItemType File -Force | Out-Null
        $SpotifySettingsPrefs = @'
app.autostart-configured=true
app.autostart-mode="off"
ui.hardware_acceleration=false
'@
        Set-Content -Path "$env:APPDATA\Spotify\prefs" -Value $SpotifySettingsPrefs -Force | Out-Null
        $tempDir = (([System.IO.Path]::GetTempPath())).TrimEnd('\')
        Invoke-WebRequest "https://download.scdn.co/SpotifySetup.exe" -OutFile "$tempDir\Spotify.exe"
        Start-Process "explorer.exe" -ArgumentList "$tempDir\Spotify.exe"
        Start-Sleep -Seconds 5
        Get-Process -Name "Spotify" -ErrorAction SilentlyContinue | Wait-Process -ErrorAction SilentlyContinue
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Spotify.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── OBS Studio ────────────────────────────────────────────────────────────────
function Invoke-BtnInstOBS {
    Invoke-RunInBackground -StatusStart "Installing OBS Studio..." -StatusDone "OBS Studio installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"OBSProject.OBSStudio`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
    }
}

# ── Notepad++ ────────────────────────────────────────────────────────────────
function Invoke-BtnInstNotepad {
    Invoke-RunInBackground -StatusStart "Installing Notepad++..." -StatusDone "Notepad++ installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Notepad++.Notepad++`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        New-Item -Path "$env:AppData" -Name "Notepad++" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        $NotePadConfig = @'
<?xml version="1.0" encoding="UTF-8" ?>
<NotepadPlus>
    <GUIConfigs>
        <GUIConfig name="ToolBar" visible="yes">small</GUIConfig>
        <GUIConfig name="StatusBar">show</GUIConfig>
        <GUIConfig name="TabSetting" replaceBySpace="no" size="4" />
        <GUIConfig name="noUpdate" intervalDays="15" nextUpdateDate="20260401" autoUpdateMode="0">yes</GUIConfig>
        <GUIConfig name="Auto-detection">yes</GUIConfig>
        <GUIConfig name="TrayIcon">0</GUIConfig>
        <GUIConfig name="RememberLastSession">no</GUIConfig>
        <GUIConfig name="DetectEncoding">yes</GUIConfig>
        <GUIConfig name="NewDocDefaultSettings" format="0" encoding="4" lang="0" codepage="-1" openAnsiAsUTF8="yes" addNewDocumentOnStartup="no" />
        <GUIConfig name="DarkMode" enable="yes" colorTone="0" enableWindowsMode="no" darkThemeName="DarkModeDefault.xml" />
        <GUIConfig name="MenuBar">show</GUIConfig>
    </GUIConfigs>
    <History nbMaxFile="0" inSubMenu="no" customLength="-1" />
</NotepadPlus>
'@
        Set-Content -Path "$env:AppData\Notepad++\config.xml" -Value $NotePadConfig -Force
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Notepad++.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\Notepad++\notepad++.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\Notepad++"; $sc.Save()
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Notepad++\Notepad++.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Notepad++" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── 7-Zip ─────────────────────────────────────────────────────────────────────
function Invoke-BtnInst7Zip {
    Invoke-RunInBackground -StatusStart "Installing 7-Zip..." -StatusDone "7-Zip installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"7zip.7zip`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\7-Zip File Manager.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\7-Zip\7zFM.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\7-Zip"; $sc.Save()
    }
}

# ── GOG launcher ──────────────────────────────────────────────────────────────
function Invoke-BtnInstGOG {
    Invoke-RunInBackground -StatusStart "Installing GOG Galaxy..." -StatusDone "GOG Galaxy installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"GOG.Galaxy`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"GalaxyClient`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"GogGalaxy`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\GOG.com\GOG GALAXY\GOG GALAXY.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\GOG.com" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── PotPlayer ────────────────────────────────────────────────────────────────
function Invoke-BtnInstPotPlayer {
    Invoke-RunInBackground -StatusStart "Installing PotPlayer..." -StatusDone "PotPlayer installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Daum.PotPlayer`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\PotPlayer\PotPlayer 64 bit.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\PotPlayer" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Onboard Memory Manager ────────────────────────────────────────────────────
function Invoke-BtnInstOMM {
    Invoke-RunInBackground -StatusStart "Installing Onboard Memory Manager..." -StatusDone "Onboard Memory Manager installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Logitech.OnboardMemoryManager_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Logitech.OnboardMemoryManager`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Logitech.OnboardMemoryManager_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Onboard Memory Manager.lnk"); $sc.TargetPath = "$base\OnboardMemoryManager.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Onboard Memory Manager.lnk"); $sc2.TargetPath = "$base\OnboardMemoryManager.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
    }
}

# ── FrameView ────────────────────────────────────────────────────────────────
function Invoke-BtnInstFrameView {
    Invoke-RunInBackground -StatusStart "Installing FrameView..." -StatusDone "FrameView installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Nvidia.FrameView`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA FrameView\FrameView.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA FrameView" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── NVIDIA App ────────────────────────────────────────────────────────────────
function Invoke-BtnInstNvApp {
    Invoke-RunInBackground -StatusStart "Installing NVIDIA App..." -StatusDone "NVIDIA App installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"XP8CLZL93F5Z4P`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA Corporation\NVIDIA App.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA Corporation" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Helium ────────────────────────────────────────────────────────────────────
function Invoke-BtnInstHelium {
    Invoke-RunInBackground -StatusStart "Installing Helium..." -StatusDone "Helium installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"ImputNet.Helium`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Helium`" /v `"HardwareAccelerationModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Helium`" /v `"BackgroundModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Helium`" /v `"HighEfficiencyModeEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $basePath = "HKLM:\Software\Microsoft\Active Setup\Installed Components"
        Get-ChildItem $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            $val = (Get-ItemProperty $_.PsPath)."(default)"
            if ($val -like "*Helium*") { Remove-Item $_.PsPath -Force -ErrorAction SilentlyContinue }
        }
        Get-Service | Where-Object { $_.Name -match 'Helium' } | ForEach-Object { cmd /c "sc stop `"$($_.Name)`" >nul 2>&1"; cmd /c "sc delete `"$($_.Name)`" >nul 2>&1" }
        Get-ScheduledTask | Where-Object { $_.TaskName -like '*Helium*' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Helium.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Windows: Quick Settings ───────────────────────────────────────────────────
function Invoke-BtnGamemode        { Start-Process "ms-settings:gaming-gamemode" }
function Invoke-BtnPointerPrecision{ Start-Process "control.exe" -ArgumentList "main.cpl ,2" }
function Invoke-BtnScalingSettings { Start-Process "ms-settings:display-advanced" }

# ── Windows: Loudness EQ ─────────────────────────────────────────────────────
function Invoke-BtnLoudnessEQ { Invoke-UltimateScript -Path "6 Windows/24 Loudness EQ.ps1" -Status "Loudness EQ opened." }

# ── Windows: Control Panel Settings ──────────────────────────────────────────
# This script is too large and uses TrustedInstaller — launch a PS window
function Invoke-BtnCPOptimize {
    Invoke-UltimateScript -Path "6 Windows/22 Control Panel Settings.ps1" -Status "Opening Control Panel Settings (menu-driven console)..."
}
function Invoke-BtnCPDefault {
    Invoke-UltimateScript -Path "6 Windows/22 Control Panel Settings.ps1" -Status "Opening Control Panel Settings (menu-driven console)..."
}

# ── Windows: Bloatware Checks ─────────────────────────────────────────────────
function Invoke-BtnBloatwareLegacyCheck     { Start-Process "$env:SystemDrive\Windows\system32\appwiz.cpl" }
function Invoke-BtnBloatwareLegacyFeatCheck { Start-Process "$env:SystemDrive\Windows\system32\optionalfeatures.exe" }
function Invoke-BtnBloatwareUWPFeatCheck    { Start-Process "ms-settings:optionalfeatures" }
function Invoke-BtnBloatwareTaskmgr         { Start-Process "taskmgr" }

# ── Installers: GPU Tools (1:1 with Ultimate) ────────────────────────────────
# MSI Afterburner has a large upstream profile-setup script — delegate to it.
function Invoke-BtnInstAfterburner { Invoke-UltimateScript -Path "4 Installers/2 MSI Afterburner.ps1" -Status "MSI Afterburner installer opened." }

function Invoke-BtnInstNPI {
    Invoke-RunInBackground -StatusStart "Installing Nvidia Profile Inspector..." -StatusDone "NPI installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
    }
}

function Invoke-BtnInstMCT {
    Invoke-RunInBackground -StatusStart "Installing More Clock Tool..." -StatusDone "More Clock Tool installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        curl.exe -s -L -A "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" "https://www.igorslab.de/installer/MoreClockTool_v1111_2.zip" -o "$env:SystemRoot\Temp\mct.zip"
        Expand-Archive -Path "$env:SystemRoot\Temp\mct.zip" -DestinationPath "$env:SystemDrive\Program Files (x86)\More Clock Tool" -Force
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\More Clock Tool.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files (x86)\More Clock Tool\MoreClockTool.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\More Clock Tool"; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\More Clock Tool.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files (x86)\More Clock Tool\MoreClockTool.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\More Clock Tool"; $sc2.Save()
    }
}

function Invoke-BtnInstCRU {
    Invoke-RunInBackground -StatusStart "Installing CRU / SRE..." -StatusDone "CRU and SRE installed." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        $dir = "$env:SystemDrive\Program Files (x86)\CRUSRE"
        Invoke-WebRequest "https://www.monitortests.com/download/cru/cru-1.5.3.zip" -OutFile "$env:SystemRoot\Temp\cru.zip"
        Expand-Archive -Path "$env:SystemRoot\Temp\cru.zip" -DestinationPath $dir -Force
        Invoke-WebRequest "https://www.monitortests.com/download/sre/sre-1.0.zip" -OutFile "$env:SystemRoot\Temp\sre.zip"
        Expand-Archive -Path "$env:SystemRoot\Temp\sre.zip" -DestinationPath $dir -Force
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        foreach ($pair in @(
            @{ name="Custom Resolution Utility.lnk"; exe="CRU.exe" },
            @{ name="Scaled Resolution Editor.lnk";  exe="SRE.exe" }
        )) {
            $sc = $sh.CreateShortcut("$Desktop\$($pair.name)"); $sc.TargetPath = "$dir\$($pair.exe)"; $sc.WorkingDirectory = $dir; $sc.Save()
            $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\$($pair.name)"); $sc2.TargetPath = "$dir\$($pair.exe)"; $sc2.WorkingDirectory = $dir; $sc2.Save()
        }
    }
}

# ── Advanced: ReBar additional options (same upstream menu) ──────────────────
function Invoke-BtnReBarOff    { Invoke-UltimateScript -Path "8 Advanced/7 ReBar Force.ps1" -Status "ReBar Force opened — pick Force Off." }
function Invoke-BtnReBarToBios { Invoke-UltimateScript -Path "8 Advanced/7 ReBar Force.ps1" -Status "ReBar Force opened — pick To Bios." }

# ── Advanced: Keyboard Shortcuts ─────────────────────────────────────────────
function Invoke-BtnKbDefault {
    Invoke-RunInBackground -StatusStart "Restoring keyboard shortcuts..." -StatusDone "Keyboard shortcuts restored." -ScriptBlock {
        reg add "HKLM\SYSTEM\ControlSet001\Services\hidserv" /v "Start" /t REG_DWORD /d "3" /f | Out-Null
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer`" /v `"NoWinKeys`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`" /v `"DisabledHotkeys`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout`" /v `"Scancode Map`" /f >nul 2>&1"
    }
}
function Invoke-BtnKbOff {
    $r = [System.Windows.MessageBox]::Show("This disables Win key, media keys, and hotkeys. ESC rebinds to =. Cut/copy/paste still work. Continue?","Keyboard Shortcuts Off","YesNo","Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Disabling keyboard shortcuts..." -StatusDone "Keyboard shortcuts disabled." -ScriptBlock {
        reg add "HKLM\SYSTEM\ControlSet001\Services\hidserv" /v "Start" /t REG_DWORD /d "4" /f | Out-Null
        reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer" /v "NoWinKeys" /t REG_DWORD /d "1" /f | Out-Null
        reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" /v "DisabledHotkeys" /t REG_DWORD /d "1" /f | Out-Null
        reg add "HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout" /v "Scancode Map" /t REG_BINARY /d "00000000000000000700000000005be000005ce000003800000038e00000010001000d0000000000" /f | Out-Null
    }
}

# ── Advanced: WHQL Default ────────────────────────────────────────────────────
function Invoke-BtnWhqlDefault {
    Invoke-RunInBackground -StatusStart "Restoring WHQL signing..." -StatusDone "WHQL signing restored." -ScriptBlock {
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy`" /v `"WHQLSettings`" /f >nul 2>&1"
    }
}

# ── Advanced: Interactive — SMT/HT, Core 1 Thread 1, Priority ────────────────
# These need interactive terminal I/O — launch PS windows
function Invoke-BtnSmtOff {
    Invoke-UltimateScript -Path "8 Advanced/8 Smt Ht.ps1" -Status "SMT/HT tool opened (menu-driven console)."
}

function Invoke-BtnCore1Thread1 {
    Invoke-UltimateScript -Path "8 Advanced/9 Core 1 Thread 1.ps1" -Status "Core 1 Thread 1 tool opened (menu-driven console)."
}

function Invoke-BtnPriority {
    Invoke-UltimateScript -Path "8 Advanced/10 Priority.ps1" -Status "Priority tool opened (menu-driven console)."
}

function Invoke-BtnFactoryReset { Start-Process "ms-settings:recovery" }
function Invoke-BtnAccountLocal { Start-Process "netplwiz" }
function Invoke-BtnReinstallW10 {
    Invoke-RunInBackground -StatusStart "Downloading W10 Media Creation Tool..." -StatusDone "W10 MCT launched." -ScriptBlock {
        $progresspreference='silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Microsoft.MediaCreationTool.Windows10_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.MediaCreationTool.Windows10`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Start-Process "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.MediaCreationTool.Windows10_Microsoft.Winget.Source_8wekyb3d8bbwe\MediaCreationTool10.exe"
    }
}
function Invoke-BtnReinstallW11 {
    Invoke-RunInBackground -StatusStart "Downloading W11 Media Creation Tool..." -StatusDone "W11 MCT launched." -ScriptBlock {
        $progresspreference='silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Microsoft.MediaCreationTool_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.MediaCreationTool`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        Start-Process "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.MediaCreationTool_Microsoft.Winget.Source_8wekyb3d8bbwe\MediaCreationTool.exe"
    }
}
function Invoke-BtnAutounattend { Start-Process "https://schneegans.de/windows/unattend-generator/" }
function Invoke-BtnBlockDrivers {
    Invoke-RunInBackground -StatusStart "Blocking driver updates..." -StatusDone "Driver updates blocked." -ScriptBlock {
        reg add "HKLM\Software\Policies\Microsoft\Windows\Device Metadata" /v "PreventDeviceMetadataFromNetwork" /t REG_DWORD /d 1 /f | Out-Null
        reg add "HKLM\Software\Policies\Microsoft\Windows\DeviceInstall\Settings" /v "DisableSendGenericDriverNotFoundToWER" /t REG_DWORD /d 1 /f | Out-Null
        reg add "HKLM\Software\Policies\Microsoft\Windows\DeviceInstall\Settings" /v "DisableSendRequestAdditionalSoftwareToWER" /t REG_DWORD /d 1 /f | Out-Null
        reg add "HKLM\Software\Policies\Microsoft\Windows\DriverSearching" /v "SearchOrderConfig" /t REG_DWORD /d 0 /f | Out-Null
        reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "SetAllowOptionalContent" /t REG_DWORD /d 0 /f | Out-Null
        reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "AllowTemporaryEnterpriseFeatureControl" /t REG_DWORD /d 0 /f | Out-Null
        reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "ExcludeWUDriversInQualityUpdate" /t REG_DWORD /d 1 /f | Out-Null
        reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "IncludeRecommendedUpdates" /t REG_DWORD /d 0 /f | Out-Null
        reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "EnableFeaturedSoftware" /t REG_DWORD /d 0 /f | Out-Null
    }
}
function Invoke-BtnUnblockDrivers {
    Invoke-RunInBackground -StatusStart "Unblocking driver updates..." -StatusDone "Driver updates unblocked." -ScriptBlock {
        reg delete "HKLM\Software\Policies\Microsoft\Windows\Device Metadata" /v "PreventDeviceMetadataFromNetwork" /f | Out-Null
        reg delete "HKLM\Software\Policies\Microsoft\Windows\DeviceInstall\Settings" /v "DisableSendGenericDriverNotFoundToWER" /f | Out-Null
        reg delete "HKLM\Software\Policies\Microsoft\Windows\DeviceInstall\Settings" /v "DisableSendRequestAdditionalSoftwareToWER" /f | Out-Null
        reg delete "HKLM\Software\Policies\Microsoft\Windows\DriverSearching" /v "SearchOrderConfig" /f | Out-Null
        reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "SetAllowOptionalContent" /f | Out-Null
        reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "AllowTemporaryEnterpriseFeatureControl" /f | Out-Null
        reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "ExcludeWUDriversInQualityUpdate" /f | Out-Null
        reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "IncludeRecommendedUpdates" /f | Out-Null
        reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "EnableFeaturedSoftware" /f | Out-Null
    }
}
function Invoke-BtnNetworkDriver {
    $q = [uri]::EscapeDataString((Get-CimInstance Win32_BaseBoard).Product)
    Start-Process "https://www.google.com/search?q=$q+network+driver"
}
function Invoke-BtnToBios {
    $r = [System.Windows.MessageBox]::Show("Restart to BIOS now?","Akari Tool","YesNo","Warning")
    if ($r -eq "Yes") { cmd /c "C:\Windows\System32\shutdown.exe /r /fw /t 0" }
}

# Complex Setup tweaks delegate to their upstream Ultimate script (1:1, always current).
# Verified-identical toggles, read-only checks, and openers stay inline.

# BitLocker
function Invoke-BtnBitlockerOff { Invoke-UltimateScript -Path "3 Setup/1 BitLocker.ps1" -Status "BitLocker opened — pick Off." }
function Invoke-BtnBitlockerOn  { Invoke-UltimateScript -Path "3 Setup/1 BitLocker.ps1" -Status "BitLocker opened — pick On." }

# Memory Compression (Off/On delegate; Check is a read-only status display)
function Invoke-BtnMemCompOff { Invoke-UltimateScript -Path "3 Setup/2 Memory Compression.ps1" -Status "Memory Compression opened — pick Off." }
function Invoke-BtnMemCompOn  { Invoke-UltimateScript -Path "3 Setup/2 Memory Compression.ps1" -Status "Memory Compression opened — pick On." }
function Invoke-BtnMemCompCheck {
    $status = Get-MMAgent | Out-String
    [System.Windows.MessageBox]::Show($status,"MMAgent Status","OK","Information")
}

# Convert Home to Pro
function Invoke-BtnConvertToPro { Invoke-UltimateScript -Path "3 Setup/3 Convert Home To Pro.ps1" -Status "Convert Home to Pro opened." }

# Background Apps — verified 1:1 with upstream
function Invoke-BtnBgAppsOff {
    Invoke-RunInBackground -StatusStart "Disabling background apps..." -StatusDone "Background apps disabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy`" /v `"LetAppsRunInBackground`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search`" /v `"BackgroundAppGlobalToggle`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications`" /v `"GlobalUserDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}
function Invoke-BtnBgAppsDefault {
    Invoke-RunInBackground -StatusStart "Restoring background apps..." -StatusDone "Background apps restored." -ScriptBlock {
        cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy`" /v `"LetAppsRunInBackground`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search`" /v `"BackgroundAppGlobalToggle`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications`" /v `"GlobalUserDisabled`" /f >nul 2>&1"
    }
}

# Edge Settings (Optimize/Default menu)
function Invoke-BtnEdgeOptimize { Invoke-UltimateScript -Path "3 Setup/10 Edge Settings.ps1" -Status "Edge Settings opened — pick Optimize." }
function Invoke-BtnEdgeDefault  { Invoke-UltimateScript -Path "3 Setup/10 Edge Settings.ps1" -Status "Edge Settings opened — pick Default." }

# Store Settings (Optimize/Default menu)
function Invoke-BtnStoreOptimize { Invoke-UltimateScript -Path "3 Setup/11 Store Settings.ps1" -Status "Store Settings opened — pick Optimize." }
function Invoke-BtnStoreDefault  { Invoke-UltimateScript -Path "3 Setup/11 Store Settings.ps1" -Status "Store Settings opened — pick Default." }

# Updates Pause — verified 1:1 with upstream
function Invoke-BtnUpdatesPause {
    Invoke-RunInBackground -StatusStart "Pausing updates for 1 year..." -StatusDone "Updates paused." -ScriptBlock {
        $pause = (Get-Date).AddDays(365).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $today = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $p = "HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings"
        Set-ItemProperty $p "PauseUpdatesExpiryTime"      $pause -Force
        Set-ItemProperty $p "PauseFeatureUpdatesEndTime"  $pause -Force
        Set-ItemProperty $p "PauseFeatureUpdatesStartTime" $today -Force
        Set-ItemProperty $p "PauseQualityUpdatesEndTime"  $pause -Force
        Set-ItemProperty $p "PauseQualityUpdatesStartTime" $today -Force
        Set-ItemProperty $p "PauseUpdatesStartTime"        $today -Force
        Start-Process "ms-settings:windowsupdate"
    }
}

# Openers
function Invoke-BtnKeys        { Start-Process "https://github.com/massgravel/Microsoft-Activation-Scripts" }
function Invoke-BtnActivation  { Start-Process "ms-settings:activation" }
function Invoke-BtnDateLang    { Start-Process "ms-settings:dateandtime" }
function Invoke-BtnStartupApps { Start-Process "ms-settings:startupapps" }

# All non-trivial Windows tweaks delegate to their exact upstream Ultimate script
# (1:1 by construction, always current). Pure openers stay inline.

# Taskbar / Start Menu
function Invoke-BtnTaskbarClean   { Invoke-UltimateScript -Path "6 Windows/1 Start Menu Taskbar.ps1" -Status "Start Menu Taskbar opened — pick Clean." }
function Invoke-BtnTaskbarDefault { Invoke-UltimateScript -Path "6 Windows/1 Start Menu Taskbar.ps1" -Status "Start Menu Taskbar opened — pick Default." }

# Start Menu Layout
function Invoke-BtnStartMenu25H2 { Invoke-UltimateScript -Path "6 Windows/2 Start Menu Layout.ps1" -Status "Start Menu Layout opened." }
function Invoke-BtnStartMenu24H2 { Invoke-UltimateScript -Path "6 Windows/2 Start Menu Layout.ps1" -Status "Start Menu Layout opened." }

# Start Menu Shortcuts
function Invoke-BtnStartShortcuts { Invoke-UltimateScript -Path "6 Windows/3 Start Menu Shortcuts.ps1" -Status "Start Menu Shortcuts opened." }

# Context Menu
function Invoke-BtnContextClean   { Invoke-UltimateScript -Path "6 Windows/4 Context Menu.ps1" -Status "Context Menu opened — pick Clean." }
function Invoke-BtnContextDefault { Invoke-UltimateScript -Path "6 Windows/4 Context Menu.ps1" -Status "Context Menu opened — pick Default." }

# Theme / Black cosmetics
function Invoke-BtnThemeBlack     { Invoke-UltimateScript -Path "6 Windows/5 Theme Black.ps1" -Status "Theme Black opened." }
function Invoke-BtnWallpaperBlack { Invoke-UltimateScript -Path "6 Windows/6 Signout Lockscreen Wallpaper Black.ps1" -Status "Signout/Lockscreen/Wallpaper Black opened." }
function Invoke-BtnAccountBlack   { Invoke-UltimateScript -Path "6 Windows/7 User Account Pictures Black.ps1" -Status "User Account Pictures Black opened." }

# Widgets
function Invoke-BtnWidgetsOff     { Invoke-UltimateScript -Path "6 Windows/8 Widgets.ps1" -Status "Widgets opened — pick Off." }
function Invoke-BtnWidgetsDefault { Invoke-UltimateScript -Path "6 Windows/8 Widgets.ps1" -Status "Widgets opened — pick Default." }

# Copilot
function Invoke-BtnCopilotOff     { Invoke-UltimateScript -Path "6 Windows/9 Copilot.ps1" -Status "Copilot opened — pick Off." }
function Invoke-BtnCopilotDefault { Invoke-UltimateScript -Path "6 Windows/9 Copilot.ps1" -Status "Copilot opened — pick Default." }

# Bloatware
function Invoke-BtnBloatwareRemove { Invoke-UltimateScript -Path "6 Windows/13 Bloatware.ps1" -Status "Bloatware opened." }
function Invoke-BtnBloatwareCheck  { Start-Process "ms-settings:appsfeatures" }

# Game Bar
function Invoke-BtnGamebarOff     { Invoke-UltimateScript -Path "6 Windows/19 Gamebar.ps1" -Status "Game Bar opened — pick Off." }
function Invoke-BtnGamebarDefault { Invoke-UltimateScript -Path "6 Windows/19 Gamebar.ps1" -Status "Game Bar opened — pick Default." }

# Edge & WebView
function Invoke-BtnEdgeUninstall { Invoke-UltimateScript -Path "6 Windows/20 Edge & WebView.ps1" -Confirm "This will uninstall Microsoft Edge. Continue?" -Status "Uninstalling Edge... this may take a minute." }
function Invoke-BtnEdgeRestore   { Start-Process "https://www.microsoft.com/en-us/edge/download" }

# Notepad Settings
function Invoke-BtnNotepad { Invoke-UltimateScript -Path "6 Windows/21 Notepad Settings.ps1" -Status "Notepad Settings opened." }

# Device Manager / Network power savings & wake
function Invoke-BtnDevPowerOff     { Invoke-UltimateScript -Path "6 Windows/25 Device Manager Power Savings & Wake.ps1" -Status "Device Manager Power opened — pick Off." }
function Invoke-BtnDevPowerDefault { Invoke-UltimateScript -Path "6 Windows/25 Device Manager Power Savings & Wake.ps1" -Status "Device Manager Power opened — pick Default." }
function Invoke-BtnNetPowerOff     { Invoke-UltimateScript -Path "6 Windows/26 Network Adapter Power Savings & Wake.ps1" -Status "Network Adapter Power opened — pick Off." }
function Invoke-BtnNetPowerDefault { Invoke-UltimateScript -Path "6 Windows/26 Network Adapter Power Savings & Wake.ps1" -Status "Network Adapter Power opened — pick Default." }

# Network IPv4 Only
function Invoke-BtnIPv4Only  { Invoke-UltimateScript -Path "6 Windows/27 Network IPv4 Only.ps1" -Status "Network IPv4 Only opened — pick On." }
function Invoke-BtnIPDefault { Invoke-UltimateScript -Path "6 Windows/27 Network IPv4 Only.ps1" -Status "Network IPv4 Only opened — pick Default." }

# Write Cache Buffer Flushing
function Invoke-BtnWriteCacheOff     { Invoke-UltimateScript -Path "6 Windows/28 Write Cache Buffer Flushing.ps1" -Status "Write Cache opened — pick Off." }
function Invoke-BtnWriteCacheDefault { Invoke-UltimateScript -Path "6 Windows/28 Write Cache Buffer Flushing.ps1" -Status "Write Cache opened — pick Default." }

# Power Plan
function Invoke-BtnPowerPlanOn      { Invoke-UltimateScript -Path "6 Windows/29 Power Plan.ps1" -Status "Power Plan opened — pick On." }
function Invoke-BtnPowerPlanDefault { Invoke-UltimateScript -Path "6 Windows/29 Power Plan.ps1" -Status "Power Plan opened — pick Default." }

# Timer Resolution
function Invoke-BtnTimerOn      { Invoke-UltimateScript -Path "6 Windows/30 Timer Resolution.ps1" -Status "Timer Resolution opened — pick On." }
function Invoke-BtnTimerDefault { Invoke-UltimateScript -Path "6 Windows/30 Timer Resolution.ps1" -Status "Timer Resolution opened — pick Default." }

# UAC
function Invoke-BtnUacOff     { Invoke-UltimateScript -Path "6 Windows/31 UAC.ps1" -Status "UAC opened — pick Off." }
function Invoke-BtnUacDefault { Invoke-UltimateScript -Path "6 Windows/31 UAC.ps1" -Status "UAC opened — pick Default." }

# Core Isolation
function Invoke-BtnCoreIsolation { Invoke-UltimateScript -Path "6 Windows/32 Core Isolation.ps1" -Status "Core Isolation opened." }

# Defender Optimize
function Invoke-BtnDefenderOptimize { Invoke-UltimateScript -Path "6 Windows/33 Defender Optimize.ps1" -Status "Defender Optimize opened — pick Optimize." }
function Invoke-BtnDefenderDefault  { Invoke-UltimateScript -Path "6 Windows/33 Defender Optimize.ps1" -Status "Defender Optimize opened — pick Default." }

# Autoruns (Startup Tasks & Apps Check)
function Invoke-BtnAutoruns { Invoke-UltimateScript -Path "6 Windows/34 Autoruns Startup Tasks & Apps Check.ps1" -Status "Autoruns opened." }

# Cleanup
function Invoke-BtnCleanup { Invoke-UltimateScript -Path "6 Windows/35 Cleanup.ps1" -Status "Cleanup opened." }

# Restore Point
function Invoke-BtnRestorePoint { Invoke-UltimateScript -Path "6 Windows/36 Restore Point.ps1" -Status "Restore Point opened." }

# Pure openers
function Invoke-BtnControlPanel { Start-Process control.exe }
function Invoke-BtnSound        { Start-Process "mmsys.cpl" }

$sync.assets = @{}
$sync.assets.logo = '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'

$inputXML = @'
<Window Name="AkariWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Akari Tool"
        Width="1120" Height="740"
        MinWidth="900" MinHeight="580"
        WindowStartupLocation="CenterScreen"
        Background="#1C1C1C"
        Foreground="White"
        FontFamily="Segoe UI Variable Text, Segoe UI"
        FontSize="13">

    <!-- WindowChrome: custom title bar area, keep system resize + caption buttons -->
    <WindowChrome.WindowChrome>
        <WindowChrome CaptionHeight="40"
                      ResizeBorderThickness="5"
                      UseAeroCaptionButtons="True"
                      GlassFrameThickness="-1"/>
    </WindowChrome.WindowChrome>

    <Window.Resources>

        <!-- ── Brushes ─────────────────────────────────────────────────────── -->
        <SolidColorBrush x:Key="SidebarBg"        Color="#11000000"/>
        <SolidColorBrush x:Key="TitleBarBg"       Color="#0D000000"/>
        <SolidColorBrush x:Key="CardBg"           Color="#1AFFFFFF"/>
        <SolidColorBrush x:Key="CardBorder"       Color="#18FFFFFF"/>
        <SolidColorBrush x:Key="SeparatorBrush"   Color="#14FFFFFF"/>
        <SolidColorBrush x:Key="AccentBrush"      Color="#CC2828"/>
        <SolidColorBrush x:Key="AccentHover"      Color="#E03535"/>
        <SolidColorBrush x:Key="AccentPress"      Color="#BB2020"/>
        <SolidColorBrush x:Key="TextPrimary"      Color="#FFFFFF"/>
        <SolidColorBrush x:Key="TextSecondary"    Color="#999999"/>
        <SolidColorBrush x:Key="BtnDefaultBg"     Color="#17FFFFFF"/>
        <SolidColorBrush x:Key="BtnDefaultHover"  Color="#26FFFFFF"/>
        <SolidColorBrush x:Key="BtnDefaultPress"  Color="#0DFFFFFF"/>
        <SolidColorBrush x:Key="BtnDefaultBorder" Color="#20FFFFFF"/>
        <SolidColorBrush x:Key="NavHover"         Color="#12FFFFFF"/>
        <SolidColorBrush x:Key="NavSelected"      Color="#1AFFFFFF"/>
        <SolidColorBrush x:Key="DangerBg"         Color="#1AFF3333"/>
        <SolidColorBrush x:Key="DangerBorder"     Color="#25FF4444"/>

        <!-- ── ScrollBar (thin, Win11-style) ──────────────────────────────── -->
        <Style TargetType="ScrollBar">
            <Setter Property="Width"  Value="6"/>
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                        <Grid Background="Transparent">
                            <Track Name="PART_Track" IsDirectionReversed="True">
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType="Thumb">
                                                <Border Background="#35FFFFFF" CornerRadius="3" Margin="1,4"/>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType="ScrollViewer">
            <Setter Property="VerticalScrollBarVisibility" Value="Auto"/>
            <Setter Property="HorizontalScrollBarVisibility" Value="Disabled"/>
        </Style>

        <!-- ── Default button ─────────────────────────────────────────────── -->
        <Style x:Key="Btn" TargetType="Button">
            <Setter Property="Background"       Value="{StaticResource BtnDefaultBg}"/>
            <Setter Property="Foreground"       Value="{StaticResource TextPrimary}"/>
            <Setter Property="BorderBrush"      Value="{StaticResource BtnDefaultBorder}"/>
            <Setter Property="BorderThickness"  Value="1"/>
            <Setter Property="Padding"          Value="16,7"/>
            <Setter Property="MinWidth"         Value="112"/>
            <Setter Property="FontFamily"       Value="Segoe UI Variable Text, Segoe UI"/>
            <Setter Property="FontSize"         Value="13"/>
            <Setter Property="Cursor"           Value="Hand"/>
            <Setter Property="VerticalAlignment" Value="Center"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Root"
                                Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="5"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Root" Property="Background" Value="{StaticResource BtnDefaultHover}"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="Root" Property="Background" Value="{StaticResource BtnDefaultPress}"/>
                                <Setter TargetName="Root" Property="Opacity" Value="0.8"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="Root" Property="Opacity" Value="0.35"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- ── Accent (red) button ────────────────────────────────────────── -->
        <Style x:Key="BtnAccent" TargetType="Button" BasedOn="{StaticResource Btn}">
            <Setter Property="Background"  Value="{StaticResource AccentBrush}"/>
            <Setter Property="BorderBrush" Value="{StaticResource AccentHover}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Root"
                                Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="1" CornerRadius="5"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Root" Property="Background" Value="{StaticResource AccentHover}"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="Root" Property="Background" Value="{StaticResource AccentPress}"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="Root" Property="Opacity" Value="0.35"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- ── Nav radio button ───────────────────────────────────────────── -->
        <Style x:Key="NavBtn" TargetType="RadioButton">
            <Setter Property="Background"       Value="Transparent"/>
            <Setter Property="Foreground"       Value="#AAAAAA"/>
            <Setter Property="Margin"           Value="8,1"/>
            <Setter Property="Height"           Value="40"/>
            <Setter Property="Cursor"           Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="RadioButton">
                        <Border x:Name="Root" Background="Transparent" CornerRadius="6" Padding="10,0">
                            <Grid>
                                <!-- Left accent pill (visible when selected) -->
                                <Border x:Name="Pill" Width="3" Height="16"
                                        Background="{StaticResource AccentBrush}"
                                        CornerRadius="2" HorizontalAlignment="Left"
                                        VerticalAlignment="Center" Margin="-2,0,0,0"
                                        Visibility="Collapsed"/>
                                <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="10,0,0,0">
                                    <TextBlock x:Name="Icon"
                                               FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                               FontSize="15"
                                               Text="{TemplateBinding Tag}"
                                               Foreground="#888888"
                                               VerticalAlignment="Center"
                                               Width="22"/>
                                    <TextBlock Text="{TemplateBinding Content}"
                                               FontFamily="Segoe UI Variable Text, Segoe UI"
                                               FontSize="13"
                                               Foreground="{TemplateBinding Foreground}"
                                               VerticalAlignment="Center"
                                               Margin="10,0,0,0"/>
                                </StackPanel>
                            </Grid>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Root" Property="Background" Value="{StaticResource NavHover}"/>
                                <Setter Property="Foreground" Value="White"/>
                                <Setter TargetName="Icon"     Property="Foreground" Value="#CCCCCC"/>
                            </Trigger>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="Root" Property="Background" Value="{StaticResource NavSelected}"/>
                                <Setter Property="Foreground" Value="White"/>
                                <Setter TargetName="Icon" Property="Foreground" Value="White"/>
                                <Setter TargetName="Pill" Property="Visibility" Value="Visible"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- ── Card (section grouping box) ───────────────────────────────── -->
        <Style x:Key="Card" TargetType="Border">
            <Setter Property="Background"       Value="{StaticResource CardBg}"/>
            <Setter Property="BorderBrush"      Value="{StaticResource CardBorder}"/>
            <Setter Property="BorderThickness"  Value="1"/>
            <Setter Property="CornerRadius"     Value="8"/>
            <Setter Property="Padding"          Value="16,14"/>
            <Setter Property="Margin"           Value="0,0,0,10"/>
        </Style>

        <!-- ── Danger card (for risky Advanced tweaks) ───────────────────── -->
        <Style x:Key="CardDanger" TargetType="Border" BasedOn="{StaticResource Card}">
            <Setter Property="Background"  Value="{StaticResource DangerBg}"/>
            <Setter Property="BorderBrush" Value="{StaticResource DangerBorder}"/>
        </Style>

        <!-- ── Text styles ────────────────────────────────────────────────── -->
        <Style x:Key="H1" TargetType="TextBlock">
            <Setter Property="FontFamily"  Value="Segoe UI Variable Display, Segoe UI"/>
            <Setter Property="FontSize"    Value="22"/>
            <Setter Property="FontWeight"  Value="SemiBold"/>
            <Setter Property="Foreground"  Value="{StaticResource TextPrimary}"/>
            <Setter Property="Margin"      Value="0,0,0,18"/>
        </Style>
        <Style x:Key="CardTitle" TargetType="TextBlock">
            <Setter Property="FontFamily"  Value="Segoe UI Variable Text, Segoe UI"/>
            <Setter Property="FontSize"    Value="13"/>
            <Setter Property="FontWeight"  Value="SemiBold"/>
            <Setter Property="Foreground"  Value="{StaticResource TextPrimary}"/>
        </Style>
        <Style x:Key="CardDesc" TargetType="TextBlock">
            <Setter Property="FontFamily"   Value="Segoe UI Variable Text, Segoe UI"/>
            <Setter Property="FontSize"     Value="12"/>
            <Setter Property="Foreground"   Value="{StaticResource TextSecondary}"/>
            <Setter Property="TextWrapping" Value="Wrap"/>
            <Setter Property="Margin"       Value="0,2,0,0"/>
        </Style>
        <Style x:Key="CardGroupHeader" TargetType="TextBlock">
            <Setter Property="FontFamily"  Value="Segoe UI Variable Text, Segoe UI"/>
            <Setter Property="FontSize"    Value="11"/>
            <Setter Property="FontWeight"  Value="SemiBold"/>
            <Setter Property="Foreground"  Value="{StaticResource TextSecondary}"/>
            <Setter Property="Margin"      Value="0,4,0,8"/>
        </Style>

        <!-- ── Separator ──────────────────────────────────────────────────── -->
        <Style x:Key="Sep" TargetType="Separator">
            <Setter Property="Background" Value="{StaticResource SeparatorBrush}"/>
            <Setter Property="Height"     Value="1"/>
            <Setter Property="Margin"     Value="0,8"/>
        </Style>

        <!-- ── ComboBox (dark, Win11-style) — full template so it isn't white ── -->
        <ControlTemplate x:Key="ComboToggle" TargetType="ToggleButton">
            <Border x:Name="bd" Background="{StaticResource BtnDefaultBg}" BorderBrush="{StaticResource BtnDefaultBorder}"
                    BorderThickness="1" CornerRadius="6">
                <Path HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,10,0"
                      Data="M0,0 L4,4 L8,0" Stroke="{StaticResource TextSecondary}" StrokeThickness="1.4"/>
            </Border>
            <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter TargetName="bd" Property="Background" Value="{StaticResource BtnDefaultHover}"/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>

        <Style TargetType="ComboBox">
            <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
            <Setter Property="FontFamily" Value="Segoe UI Variable Text, Segoe UI"/>
            <Setter Property="FontSize"   Value="13"/>
            <Setter Property="Height"     Value="32"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ComboBox">
                        <Grid>
                            <ToggleButton Template="{StaticResource ComboToggle}" Focusable="False"
                                          IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"/>
                            <ContentPresenter Margin="12,0,28,0" VerticalAlignment="Center" HorizontalAlignment="Left"
                                              Content="{TemplateBinding SelectionBoxItem}"
                                              ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                              IsHitTestVisible="False"/>
                            <Popup IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom"
                                   AllowsTransparency="True" Focusable="False" PopupAnimation="Slide">
                                <Border Background="#26262A" BorderBrush="{StaticResource BtnDefaultBorder}"
                                        BorderThickness="1" CornerRadius="6" Margin="0,4,0,0"
                                        MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}">
                                    <ScrollViewer MaxHeight="260">
                                        <ItemsPresenter/>
                                    </ScrollViewer>
                                </Border>
                            </Popup>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType="ComboBoxItem">
            <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
            <Setter Property="Padding"    Value="10,7"/>
            <Setter Property="FontSize"   Value="13"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ComboBoxItem">
                        <Border x:Name="ib" Background="Transparent" Padding="{TemplateBinding Padding}" CornerRadius="4" Margin="3,1">
                            <ContentPresenter/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="ib" Property="Background" Value="{StaticResource NavHover}"/>
                            </Trigger>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="ib" Property="Background" Value="{StaticResource NavSelected}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

    </Window.Resources>

    <!-- ═══════════════════════════════════════════════════════════════════════
         ROOT GRID: title bar row + content row
    ═══════════════════════════════════════════════════════════════════════════ -->
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="40"/>    <!-- title bar -->
            <RowDefinition Height="*"/>     <!-- body -->
            <RowDefinition Height="28"/>    <!-- status bar -->
        </Grid.RowDefinitions>

        <!-- ── Title bar ───────────────────────────────────────────────────── -->
        <Border Grid.Row="0" Background="{StaticResource TitleBarBg}">
            <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="16,0,0,0">
                <!-- Brand logo (Source assigned in main.ps1 from embedded base64) -->
                <Image Name="TitleLogo" Height="22" VerticalAlignment="Center" Margin="0,0,8,0"
                       RenderOptions.BitmapScalingMode="HighQuality"/>
                <TextBlock Text="Akari Tool"
                           FontFamily="Segoe UI Variable Display, Segoe UI"
                           FontSize="13" FontWeight="SemiBold"
                           Foreground="White" VerticalAlignment="Center"/>
            </StackPanel>
        </Border>

        <!-- ── Body: sidebar + content ─────────────────────────────────────── -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="210"/>
                <ColumnDefinition Width="1"/>     <!-- divider -->
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Sidebar -->
            <Grid Grid.Column="0" Background="{StaticResource SidebarBg}">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <StackPanel Grid.Row="0" Margin="0,10,0,0">
                    <RadioButton Name="NavCheck"      Style="{StaticResource NavBtn}" GroupName="Nav" Tag="&#xE8B3;" Content="Check"      IsChecked="True"/>
                    <RadioButton Name="NavRefresh"    Style="{StaticResource NavBtn}" GroupName="Nav" Tag="&#xE72C;" Content="Refresh"/>
                    <RadioButton Name="NavSetup"      Style="{StaticResource NavBtn}" GroupName="Nav" Tag="&#xE713;" Content="Setup"/>
                    <RadioButton Name="NavInstallers" Style="{StaticResource NavBtn}" GroupName="Nav" Tag="&#xE7B8;" Content="Installers"/>
                    <RadioButton Name="NavGraphics"   Style="{StaticResource NavBtn}" GroupName="Nav" Tag="&#xE7F4;" Content="Graphics"/>
                    <RadioButton Name="NavWindows"    Style="{StaticResource NavBtn}" GroupName="Nav" Tag="&#xE770;" Content="Windows"/>
                    <RadioButton Name="NavHardware"   Style="{StaticResource NavBtn}" GroupName="Nav" Tag="&#xEBD2;" Content="Hardware"/>
                    <RadioButton Name="NavAdvanced"   Style="{StaticResource NavBtn}" GroupName="Nav" Tag="&#xE756;" Content="Advanced"/>
                </StackPanel>

                <!-- Version footer -->
                <StackPanel Grid.Row="1" Margin="16,12">
                    <TextBlock Text="Akari Tool  v1.0" FontSize="11" Foreground="#444444"/>
                    <TextBlock Text="by isleap · Fr33thy scripts" FontSize="11" Foreground="#444444"/>
                </StackPanel>
            </Grid>

            <!-- Sidebar divider -->
            <Border Grid.Column="1" Background="{StaticResource SeparatorBrush}"/>

            <!-- ══ CONTENT AREA ════════════════════════════════════════════ -->
            <Grid Grid.Column="2" Background="Transparent">

                <!-- ── 1 · CHECK ─────────────────────────────────────────── -->
                <ScrollViewer Name="PanelCheck" Padding="24,20,24,16">
                    <StackPanel>
                        <TextBlock Text="Check" Style="{StaticResource H1}"/>

                        <!-- PC -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="PC" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="PC Check (OCCT)" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Install OCCT and run CPU, RAM &amp; GPU stability tests, with drive / RAM / GPU checklist guidance." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnCheckPC" Grid.Column="1" Content="Run" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <!-- BIOS -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="BIOS" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="BIOS Update &amp; Settings" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Enable password sign-in, search your motherboard, review BIOS tips, then restart to BIOS." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnCheckBios" Grid.Column="1" Content="Run" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>

                <!-- ── 2 · REFRESH ───────────────────────────────────────── -->
                <ScrollViewer Name="PanelRefresh" Visibility="Collapsed" Padding="24,20,24,16">
                    <StackPanel>
                        <TextBlock Text="Refresh" Style="{StaticResource H1}"/>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="RESET &amp; REINSTALL" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Factory Reset" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Open Windows recovery settings." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnFactoryReset" Grid.Column="1" Content="Open" Style="{StaticResource BtnAccent}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Reinstall Windows" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Download W10 or W11 installation media." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnReinstallW10" Grid.Column="1" Content="W10" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnReinstallW11" Grid.Column="3" Content="W11" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Autounattend" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Generate an autounattend.xml for unattended installs." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnAutounattend" Grid.Column="1" Content="Open" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="ACCOUNT &amp; DRIVERS" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Local Account" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Switch to a local account without a Microsoft login." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnAccountLocal" Grid.Column="1" Content="Open" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Block Update Drivers" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Stop Windows Update from auto-installing drivers." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnBlockDrivers"   Grid.Column="1" Content="Block"   Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnUnblockDrivers" Grid.Column="3" Content="Unblock" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Network Driver / To BIOS" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Open Device Manager for network driver, or reboot to BIOS." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnNetworkDriver" Grid.Column="1" Content="Network" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnToBios"        Grid.Column="3" Content="To BIOS" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>

                <!-- ── 3 · SETUP ─────────────────────────────────────────── -->
                <ScrollViewer Name="PanelSetup" Visibility="Collapsed" Padding="24,20,24,16">
                    <StackPanel>
                        <TextBlock Text="Setup" Style="{StaticResource H1}"/>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="SYSTEM" Style="{StaticResource CardGroupHeader}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="BitLocker" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Enable or disable BitLocker drive encryption." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnBitlockerOff" Grid.Column="1" Content="Off ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnBitlockerOn"  Grid.Column="3" Content="On"    Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Memory Compression" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Compresses RAM pages. Off reduces CPU overhead for gaming." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnMemCompOff"   Grid.Column="1" Content="Off ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnMemCompOn"    Grid.Column="3" Content="On"    Style="{StaticResource Btn}"/>
                                    <Button Name="BtnMemCompCheck" Grid.Column="5" Content="Check" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Background Apps" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Prevent apps from running in the background." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnBgAppsOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnBgAppsDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="WINDOWS &amp; APPS" Style="{StaticResource CardGroupHeader}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Edge Settings" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Apply optimized Edge browser settings." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnEdgeOptimize" Grid.Column="1" Content="Optimize ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnEdgeDefault"  Grid.Column="3" Content="Default"    Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Store Settings" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Apply optimized Microsoft Store settings." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnStoreOptimize" Grid.Column="1" Content="Optimize ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnStoreDefault"  Grid.Column="3" Content="Default"    Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Pause Updates" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Open Windows Update settings to pause updates." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnUpdatesPause" Grid.Column="1" Content="Open" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="ACTIVATION &amp; LICENSING" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Keys / Activation / Convert to Pro" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Manage product keys, activate Windows, or upgrade Home to Pro." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnKeys"          Grid.Column="1" Content="Keys"       Style="{StaticResource Btn}"/>
                                    <Button Name="BtnActivation"    Grid.Column="3" Content="Activate"   Style="{StaticResource Btn}"/>
                                    <Button Name="BtnConvertToPro"  Grid.Column="5" Content="→ Pro"      Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Date / Language / Region / Time" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Open system locale and date/time settings." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnDateLang"    Grid.Column="1" Content="Open"    Style="{StaticResource Btn}"/>
                                    <Button Name="BtnStartupApps" Grid.Column="3" Content="Startup" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>

                <!-- ── 4 · INSTALLERS ────────────────────────────────────── -->
                <ScrollViewer Name="PanelInstallers" Visibility="Collapsed" Padding="24,20,24,16">
                    <StackPanel>
                        <TextBlock Text="Installers" Style="{StaticResource H1}"/>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="GAME LAUNCHERS &amp; BROWSERS" Style="{StaticResource CardGroupHeader}"/>
                                <TextBlock Text="Tip: disable cloud sync, hardware acceleration, startup, and overlays in each app after installing." Foreground="#888888" FontSize="12" Margin="0,0,0,12" TextWrapping="Wrap"/>
                                <UniformGrid Columns="3" Margin="0,4,0,0">
                                    <Button Name="BtnInstSteam"    Content="Steam"            Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstEpic"     Content="Epic Games"        Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstBattlenet" Content="Battle.net"       Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstEA"       Content="EA App"            Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstUbisoft"  Content="Ubisoft Connect"   Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstRockstar" Content="Rockstar Games"    Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstLOL"      Content="League of Legends" Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstValorant" Content="Valorant"          Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstEFT"      Content="Escape from Tarkov" Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstRoblox"   Content="Roblox"            Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstChrome"   Content="Google Chrome"     Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstBrave"    Content="Brave"             Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstFirefox"  Content="Firefox"           Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstDiscord"  Content="Discord"           Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstSpotify"  Content="Spotify"           Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstOBS"      Content="OBS Studio"        Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstNotepad"  Content="Notepad++"         Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInst7Zip"     Content="7-Zip"             Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstGOG"      Content="GOG"               Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstPotPlayer" Content="PotPlayer"        Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstOMM"      Content="Onboard Mem Mgr"   Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstFrameView" Content="FrameView"        Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstNvApp"    Content="Nvidia App"        Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstHelium"   Content="Helium"            Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                </UniformGrid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="GPU TOOLS" Style="{StaticResource CardGroupHeader}"/>
                                <UniformGrid Columns="3">
                                    <Button Name="BtnInstAfterburner" Content="MSI Afterburner"      Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstNPI"         Content="Nvidia Profile Insp." Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstMCT"         Content="More Clock Tool"      Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                    <Button Name="BtnInstCRU"         Content="CRU / SRE"            Style="{StaticResource Btn}" Margin="0,0,8,8"/>
                                </UniformGrid>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>

                <!-- ── 5 · GRAPHICS ──────────────────────────────────────── -->
                <ScrollViewer Name="PanelGraphics" Visibility="Collapsed" Padding="24,20,24,16">
                    <StackPanel>
                        <TextBlock Text="Graphics" Style="{StaticResource H1}"/>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="DRIVER MANAGEMENT" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="DDU — Driver Clean" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Download DDU and wipe your current GPU driver cleanly." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnDduAuto"   Grid.Column="1" Content="Auto ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnDduManual" Grid.Column="3" Content="Manual"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Install Latest Driver" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Download and install the latest GPU driver for your brand." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnDriverNvidia" Grid.Column="1" Content="NVIDIA" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnDriverAmd"    Grid.Column="3" Content="AMD"    Style="{StaticResource Btn}"/>
                                    <Button Name="BtnDriverIntel"  Grid.Column="5" Content="Intel"  Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Debloat Driver + Apply Settings" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Install driver without bloat and apply optimized GPU settings." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnDriverDebloatNvidia" Grid.Column="1" Content="NVIDIA" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnDriverDebloatAmd"    Grid.Column="3" Content="AMD"    Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnDriverDebloatIntel"  Grid.Column="5" Content="Intel"  Style="{StaticResource BtnAccent}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="GPU SETTINGS" Style="{StaticResource CardGroupHeader}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="NVIDIA Settings" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Apply optimized NVIDIA control panel settings." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnNvidiaOn"      Grid.Column="1" Content="Apply ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnNvidiaDefault" Grid.Column="3" Content="Default"  Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="AMD Settings" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Apply optimized AMD Radeon settings." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnAmdOn"      Grid.Column="1" Content="Apply ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnAmdDefault" Grid.Column="3" Content="Default"  Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Intel Settings" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Apply optimized Intel Arc / iGPU settings." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnIntelOn"      Grid.Column="1" Content="Apply ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnIntelDefault" Grid.Column="3" Content="Default"  Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="ADVANCED GPU TWEAKS" Style="{StaticResource CardGroupHeader}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="HDCP" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Disable HDCP — reduces GPU overhead in some games." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnHdcpOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnHdcpDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="P0 State" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Force GPU to stay in highest performance state." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnP0On"      Grid.Column="1" Content="On ★"   Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnP0Default" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="MSI Mode" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Enable Message Signaled Interrupts for the GPU." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnMsiOn"  Grid.Column="1" Content="On ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnMsiOff" Grid.Column="3" Content="Off"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="DirectX / C++ Runtimes" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Install or update DirectX and Visual C++ redistributables." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnDirectX"   Grid.Column="1" Content="DirectX" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnCppRuntime" Grid.Column="3" Content="C++"    Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Resolution / Refresh Rate  ·  HAGS + Windowed Opt." Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Open display settings for resolution and HAGS controls." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <Button Name="BtnResolution" Grid.Column="1" Content="Resolution" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnHags"       Grid.Column="3" Content="HAGS"       Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>

                <!-- ── 6 · WINDOWS ───────────────────────────────────────── -->
                <ScrollViewer Name="PanelWindows" Visibility="Collapsed" Padding="24,20,24,16">
                    <StackPanel>
                        <TextBlock Text="Windows" Style="{StaticResource H1}"/>

                        <!-- Appearance -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="APPEARANCE" Style="{StaticResource CardGroupHeader}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Start Menu / Taskbar" Style="{StaticResource CardTitle}"/><TextBlock Text="Clean taskbar — remove search, widgets, chat." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnTaskbarClean"   Grid.Column="1" Content="Clean ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnTaskbarDefault" Grid.Column="3" Content="Default"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Start Menu Layout" Style="{StaticResource CardTitle}"/><TextBlock Text="Set the recommended Start Menu layout for your Windows version." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnStartMenu25H2" Grid.Column="1" Content="25H2 ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnStartMenu24H2" Grid.Column="3" Content="24H2"    Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Context Menu" Style="{StaticResource CardTitle}"/><TextBlock Text="Restore the classic Windows 10-style right-click context menu." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnContextClean"   Grid.Column="1" Content="Clean ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnContextDefault" Grid.Column="3" Content="Default"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Black Theme + Wallpaper + Account Picture" Style="{StaticResource CardTitle}"/><TextBlock Text="Apply a full black theme across the OS." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnThemeBlack"     Grid.Column="1" Content="Theme ★"    Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnWallpaperBlack" Grid.Column="3" Content="Wallpaper ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnAccountBlack"   Grid.Column="5" Content="Picture ★"   Style="{StaticResource BtnAccent}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Start Menu Shortcuts" Style="{StaticResource CardTitle}"/><TextBlock Text="Open pinned shortcuts settings." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnStartShortcuts" Grid.Column="1" Content="Open" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <!-- Performance -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="PERFORMANCE" Style="{StaticResource CardGroupHeader}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Power Plan" Style="{StaticResource CardTitle}"/><TextBlock Text="Apply a high-performance or custom Ultimate power plan." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnPowerPlanOn"      Grid.Column="1" Content="Apply ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnPowerPlanDefault" Grid.Column="3" Content="Default"  Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Timer Resolution" Style="{StaticResource CardTitle}"/><TextBlock Text="Enable high-resolution system timer (lower latency)." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnTimerOn"      Grid.Column="1" Content="On ★"   Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnTimerDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Write Cache Buffer Flushing" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable write cache buffer flushing for drives." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnWriteCacheOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnWriteCacheDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Device Manager Power Savings" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable power saving for devices in Device Manager." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnDevPowerOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnDevPowerDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Network Adapter Power Savings" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable power saving on the network adapter." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnNetPowerOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnNetPowerDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Network IPv4 Only" Style="{StaticResource CardTitle}"/><TextBlock Text="Prefer IPv4 over IPv6 to reduce latency on some networks." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnIPv4Only"    Grid.Column="1" Content="IPv4 ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnIPDefault"   Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <!-- Debloat / Privacy -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="DEBLOAT &amp; PRIVACY" Style="{StaticResource CardGroupHeader}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Bloatware" Style="{StaticResource CardTitle}"/><TextBlock Text="Remove all pre-installed bloat apps from Windows." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnBloatwareRemove" Grid.Column="1" Content="Remove All ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnBloatwareCheck"  Grid.Column="3" Content="Check"         Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Widgets" Style="{StaticResource CardTitle}"/><TextBlock Text="Remove the Windows Widgets panel." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnWidgetsOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnWidgetsDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Copilot" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable Windows Copilot." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnCopilotOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnCopilotDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Game Bar / Xbox DVR" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable Game Bar and Xbox Game DVR recording overlay." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnGamebarOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnGamebarDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Edge &amp; WebView" Style="{StaticResource CardTitle}"/><TextBlock Text="Uninstall Microsoft Edge and WebView2." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnEdgeUninstall" Grid.Column="1" Content="Uninstall ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnEdgeRestore"   Grid.Column="3" Content="Restore"     Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <!-- Bloatware Checks -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="BLOATWARE CHECKS" Style="{StaticResource CardGroupHeader}"/>
                                <UniformGrid Columns="4">
                                    <Button Name="BtnBloatwareLegacyCheck"       Content="Legacy Apps"     Style="{StaticResource Btn}" Margin="0,0,8,0"/>
                                    <Button Name="BtnBloatwareLegacyFeatCheck"   Content="Legacy Features" Style="{StaticResource Btn}" Margin="0,0,8,0"/>
                                    <Button Name="BtnBloatwareUWPFeatCheck"      Content="UWP Features"    Style="{StaticResource Btn}" Margin="0,0,8,0"/>
                                    <Button Name="BtnBloatwareTaskmgr"           Content="Task Manager"    Style="{StaticResource Btn}" Margin="0"/>
                                </UniformGrid>
                            </StackPanel>
                        </Border>

                        <!-- Quick Settings -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="QUICK SETTINGS" Style="{StaticResource CardGroupHeader}"/>
                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Game Mode  ·  Pointer Precision  ·  Scaling" Style="{StaticResource CardTitle}"/><TextBlock Text="Open Game Mode, Mouse settings, or Display scaling settings." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnGamemode"         Grid.Column="1" Content="Game Mode"        Style="{StaticResource Btn}"/>
                                    <Button Name="BtnPointerPrecision" Grid.Column="3" Content="Pointer Precision" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnScalingSettings"  Grid.Column="5" Content="Scaling"           Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <!-- Misc Windows settings -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="SYSTEM SETTINGS" Style="{StaticResource CardGroupHeader}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="UAC" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable User Account Control prompts." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnUacOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnUacDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Defender Optimize" Style="{StaticResource CardTitle}"/><TextBlock Text="Add game folders to Defender exclusions for better perf." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnDefenderOptimize" Grid.Column="1" Content="Optimize ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnDefenderDefault"  Grid.Column="3" Content="Default"     Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Control Panel Settings" Style="{StaticResource CardTitle}"/><TextBlock Text="Apply full optimized Control Panel + privacy + appearance settings." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnCPOptimize" Grid.Column="1" Content="Optimize ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnCPDefault"  Grid.Column="3" Content="Default"     Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Tools" Style="{StaticResource CardTitle}"/><TextBlock Text="Autoruns, Cleanup, Restore Point, Core Isolation." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnAutoruns"     Grid.Column="1" Content="Autoruns"      Style="{StaticResource Btn}"/>
                                    <Button Name="BtnCleanup"      Grid.Column="3" Content="Cleanup"       Style="{StaticResource Btn}"/>
                                    <Button Name="BtnRestorePoint" Grid.Column="5" Content="Restore Point" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnCoreIsolation" Grid.Column="7" Content="Core Isolation" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Notepad / Control Panel / Sound / Loudness EQ" Style="{StaticResource CardTitle}"/><TextBlock Text="Optimize Notepad, open Control Panel, configure audio, enable Loudness EQ." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnNotepad"     Grid.Column="1" Content="Notepad"     Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnControlPanel" Grid.Column="3" Content="Control Panel" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnSound"        Grid.Column="5" Content="Sound"         Style="{StaticResource Btn}"/>
                                    <Button Name="BtnLoudnessEQ"   Grid.Column="7" Content="Loudness EQ"   Style="{StaticResource BtnAccent}"/>
                                </Grid>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>

                <!-- ── 7 · HARDWARE ──────────────────────────────────────── -->
                <ScrollViewer Name="PanelHardware" Visibility="Collapsed" Padding="24,20,24,16">
                    <StackPanel>
                        <TextBlock Text="Hardware" Style="{StaticResource H1}"/>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="DISPLAY" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Higher Scaling (No Acceleration)" Style="{StaticResource CardTitle}"/>
                                        <TextBlock Text="Set DPI scaling without blurry GPU scaling." Style="{StaticResource CardDesc}"/>
                                    </StackPanel>
                                    <ComboBox Name="CboScaling" Grid.Column="1" Width="100" VerticalAlignment="Center">
                                        <ComboBoxItem Content="100%" IsSelected="True"/>
                                        <ComboBoxItem Content="125%"/>
                                        <ComboBoxItem Content="150%"/>
                                        <ComboBoxItem Content="175%"/>
                                        <ComboBoxItem Content="200%"/>
                                        <ComboBoxItem Content="225%"/>
                                        <ComboBoxItem Content="250%"/>
                                        <ComboBoxItem Content="300%"/>
                                        <ComboBoxItem Content="350%"/>
                                    </ComboBox>
                                    <Button Name="BtnScalingApply" Grid.Column="3" Content="Apply" Style="{StaticResource BtnAccent}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Monitor Optimization" Style="{StaticResource CardTitle}"/><TextBlock Text="Tips and steps for monitor overdrive and overclock mode." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnMonitorOpt" Grid.Column="1" Content="Open" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="MOUSE &amp; CONTROLLER" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Background Polling Rate Cap" Style="{StaticResource CardTitle}"/><TextBlock Text="Prevent background apps from capping mouse polling rate." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnPollingOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnPollingDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Mouse / Controller Polling Rate Test" Style="{StaticResource CardTitle}"/><TextBlock Text="Run polling rate measurement tools." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnMouseTest"      Grid.Column="1" Content="Mouse Test"      Style="{StaticResource Btn}"/>
                                    <Button Name="BtnControllerTest" Grid.Column="3" Content="Controller Test"  Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Controller Overclock" Style="{StaticResource CardTitle}"/><TextBlock Text="Install hidusbf to overclock controller polling rate." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnControllerOC" Grid.Column="1" Content="Run" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="NETWORK &amp; GUIDES" Style="{StaticResource CardGroupHeader}"/>
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Network Bufferbloat Test  ·  PC Build Guide" Style="{StaticResource CardTitle}"/><TextBlock Text="Open bufferbloat test or the recommended PC build guide." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnBufferbloat"  Grid.Column="1" Content="Bufferbloat" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnPcBuildGuide" Grid.Column="3" Content="Build Guide" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>

                <!-- ── 8 · ADVANCED ──────────────────────────────────────── -->
                <ScrollViewer Name="PanelAdvanced" Visibility="Collapsed" Padding="24,20,24,16">
                    <StackPanel>
                        <TextBlock Text="Advanced" Style="{StaticResource H1}"/>

                        <!-- Security (danger card) -->
                        <Border Style="{StaticResource CardDanger}">
                            <StackPanel>
                                <TextBlock Text="⚠  SECURITY — USE WITH CARE" Style="{StaticResource CardGroupHeader}" Foreground="#FF6B6B"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Windows Defender" Style="{StaticResource CardTitle}"/><TextBlock Text="Fully disable or re-enable Windows Defender AV." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnDefenderDisable" Grid.Column="1" Content="Disable" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnDefenderEnable"  Grid.Column="3" Content="Enable"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Firewall" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable or re-enable the Windows Firewall." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnFirewallDisable" Grid.Column="1" Content="Disable" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnFirewallEnable"  Grid.Column="3" Content="Enable"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Spectre / Meltdown Mitigations" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable CPU vulnerability mitigations for a performance boost." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnSpectreDisable" Grid.Column="1" Content="Disable" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnSpectreEnable"  Grid.Column="3" Content="Enable"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Data Execution Prevention (DEP)" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable or re-enable DEP memory protection." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnDepDisable" Grid.Column="1" Content="Disable" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnDepEnable"  Grid.Column="3" Content="Enable"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="File Download Security Warning" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable the 'this file may be harmful' warning on downloaded files." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnDlWarnDisable" Grid.Column="1" Content="Disable" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnDlWarnEnable"  Grid.Column="3" Content="Enable"   Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <!-- Performance tweaks -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="PERFORMANCE TWEAKS" Style="{StaticResource CardGroupHeader}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Services" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable non-essential Windows background services." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnServicesOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnServicesDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="MMAgent Features" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable Memory Manager Agent features (prefetch, superfetch)." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnMMAgentOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnMMAgentDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="NVME Faster Driver" Style="{StaticResource CardTitle}"/><TextBlock Text="Use the faster inbox NVMe StorNVMe driver." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnNvmeOn"      Grid.Column="1" Content="Apply ★" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnNvmeDefault" Grid.Column="3" Content="Default"  Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Start / Search / Shell / Mobsync" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable shell bloat for faster Start and Search." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnShellOff"     Grid.Column="1" Content="Off ★"  Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnShellDefault" Grid.Column="3" Content="Default" Style="{StaticResource Btn}"/>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <!-- Rendering tweaks -->
                        <Border Style="{StaticResource Card}">
                            <StackPanel>
                                <TextBlock Text="RENDERING &amp; CPU" Style="{StaticResource CardGroupHeader}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="MPO (Multiplane Overlay)" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable MPO to fix flickering and tearing in some setups." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnMpoOn"  Grid.Column="1" Content="On (Default)" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnMpoOff" Grid.Column="3" Content="Off"           Style="{StaticResource BtnAccent}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Hardware Flip Mode" Style="{StaticResource CardTitle}"/><TextBlock Text="FSO (default) vs FSE (Hardware Legacy Flip)." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnFlipFSO" Grid.Column="1" Content="FSO (Default)" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnFlipFSE" Grid.Column="3" Content="FSE ★"          Style="{StaticResource BtnAccent}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="HCIF (Hardware Composed Independent Flip)" Style="{StaticResource CardTitle}"/><TextBlock Text="Switch between HCIF and HIF flip modes." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnHcif"  Grid.Column="1" Content="HCIF ★"     Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnHif"   Grid.Column="3" Content="HIF (Default)" Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="ULPS (AMD Ultra Low Power State)" Style="{StaticResource CardTitle}"/><TextBlock Text="Disable ULPS to prevent AMD GPU stutters on multi-GPU." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnUlpsOn"  Grid.Column="1" Content="On (Default)" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnUlpsOff" Grid.Column="3" Content="Off ★"         Style="{StaticResource BtnAccent}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="ReBar Force (NVIDIA)" Style="{StaticResource CardTitle}"/><TextBlock Text="Force Resizable BAR on/off via Inspector, or use driver default. Option 4 restarts to BIOS." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnReBarDefault" Grid.Column="1" Content="Default" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnReBarOn"      Grid.Column="3" Content="Force On" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnReBarOff"     Grid.Column="5" Content="Force Off" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnReBarToBios"  Grid.Column="7" Content="To BIOS"   Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Keyboard Shortcuts" Style="{StaticResource CardTitle}"/><TextBlock Text="Off disables Win key, media keys, hotkeys. ESC rebinds to =. Cut/copy/paste still work." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnKbDefault" Grid.Column="1" Content="Default" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnKbOff"     Grid.Column="3" Content="Off ★"   Style="{StaticResource BtnAccent}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="SMT/HT  ·  Core 1 Thread 1  ·  Priority  ·  WHQL Bypass" Style="{StaticResource CardTitle}"/><TextBlock Text="Interactive tools for per-game CPU affinity, priority, and driver signing." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnSmtOff"         Grid.Column="1" Content="SMT/HT Off"      Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnCore1Thread1"   Grid.Column="3" Content="Core 1 Thread 1" Style="{StaticResource BtnAccent}"/>
                                    <Button Name="BtnPriority"       Grid.Column="5" Content="Priority"         Style="{StaticResource Btn}"/>
                                </Grid>
                                <Separator Style="{StaticResource Sep}"/>

                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="8"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0"><TextBlock Text="Driver WHQL / Secure Boot Bypass" Style="{StaticResource CardTitle}"/><TextBlock Text="Bypass WHQL driver signing check (requires Secure Boot off)." Style="{StaticResource CardDesc}"/></StackPanel>
                                    <Button Name="BtnWhqlDefault" Grid.Column="1" Content="Default (Off)" Style="{StaticResource Btn}"/>
                                    <Button Name="BtnWhqlBypass"  Grid.Column="3" Content="Bypass On ★"   Style="{StaticResource BtnAccent}"/>
                                </Grid>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>

            </Grid><!-- end content area -->
        </Grid><!-- end body -->

        <!-- ── Status bar ───────────────────────────────────────────────────── -->
        <Border Grid.Row="2" Background="{StaticResource TitleBarBg}"
                BorderBrush="{StaticResource SeparatorBrush}" BorderThickness="0,1,0,0">
            <Grid Margin="16,0">
                <TextBlock Name="StatusText" Text="Ready"
                           FontSize="11" Foreground="#666666"
                           VerticalAlignment="Center"/>
            </Grid>
        </Border>

    </Grid>
</Window>
'@

# ── Parse XAML ───────────────────────────────────────────────────────────────
try {
    $sync.window = [Windows.Markup.XamlReader]::Parse($inputXML)
} catch {
    [System.Windows.MessageBox]::Show("XAML Error: $_", "Akari Tool")
    exit
}

# Store every named control in $sync
([xml]$inputXML).SelectNodes("//*[@Name]") | ForEach-Object {
    $sync[$_.Name] = $sync.window.FindName($_.Name)
}

# ── Brand logo (decode embedded base64 → title bar image + window/taskbar icon) ─
function ConvertFrom-Base64Image([string]$b64) {
    $bytes  = [Convert]::FromBase64String($b64)
    $stream = New-Object System.IO.MemoryStream(,$bytes)
    $img    = New-Object System.Windows.Media.Imaging.BitmapImage
    $img.BeginInit()
    $img.CacheOption  = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $img.StreamSource = $stream
    $img.EndInit()
    $img.Freeze()
    return $img
}
if ($sync.assets -and $sync.assets.logo) {
    try {
        $logoImg = ConvertFrom-Base64Image $sync.assets.logo
        if ($sync.TitleLogo) { $sync.TitleLogo.Source = $logoImg }
        $sync.window.Icon = $logoImg
    } catch {}
}

# ── Mica + dark title bar on window load ─────────────────────────────────────
$sync.window.Add_Loaded({
    $helper = New-Object System.Windows.Interop.WindowInteropHelper($sync.window)
    $hwnd   = $helper.Handle

    try {
        # Dark title bar (DWMWA_USE_IMMERSIVE_DARK_MODE = 20)
        $dark = 1
        [DwmApi]::DwmSetWindowAttribute($hwnd, 20, [ref]$dark, 4) | Out-Null

        # Extend frame so Mica reaches the title bar
        $m = New-Object DwmApi+MARGINS
        $m.Left = -1; $m.Right = -1; $m.Top = -1; $m.Bottom = -1
        [DwmApi]::DwmExtendFrameIntoClientArea($hwnd, [ref]$m) | Out-Null

        # Mica Alt (DWMWA_SYSTEMBACKDROP_TYPE = 38, value 4 = MicaAlt / 2 = Mica)
        $mica = 2
        [DwmApi]::DwmSetWindowAttribute($hwnd, 38, [ref]$mica, 4) | Out-Null

        # Make the WPF background transparent so Mica shows through
        $sync.window.Background = [System.Windows.Media.Brushes]::Transparent
    } catch {
        # Mica unavailable (Win10 / older build) — keep fallback dark background from XAML
    }
})

# ── Navigation switching ──────────────────────────────────────────────────────
$panels = @(
    "PanelCheck", "PanelRefresh", "PanelSetup", "PanelInstallers",
    "PanelGraphics", "PanelWindows", "PanelHardware", "PanelAdvanced"
)

$navMap = @{
    NavCheck     = "PanelCheck"
    NavRefresh   = "PanelRefresh"
    NavSetup     = "PanelSetup"
    NavInstallers= "PanelInstallers"
    NavGraphics  = "PanelGraphics"
    NavWindows   = "PanelWindows"
    NavHardware  = "PanelHardware"
    NavAdvanced  = "PanelAdvanced"
}

foreach ($navName in $navMap.Keys) {
    $target = $navMap[$navName]
    $sync[$navName].Add_Checked({
        param($s, $e)
        $t = $navMap[$s.Name]
        $panels | ForEach-Object {
            $sync[$_].Visibility = [System.Windows.Visibility]::Collapsed
        }
        $sync[$t].Visibility = [System.Windows.Visibility]::Visible
    }.GetNewClosure())
}

# ── Wire all buttons to their Invoke-* functions ──────────────────────────────
$sync.Keys | Where-Object { $_ -like "Btn*" } | ForEach-Object {
    $btnName = $_
    if ($sync[$btnName] -and $sync[$btnName].GetType().Name -eq "Button") {
        $sync[$btnName].Add_Click({
            $fn = "Invoke-$($btnName)"
            if (Get-Command $fn -ErrorAction SilentlyContinue) {
                & $fn
            }
        }.GetNewClosure())
    }
}

# ── Status bar helper (call from runspaces via Dispatcher) ───────────────────
function Set-Status {
    param([string]$Text, [string]$Color = "White")
    $sync.window.Dispatcher.Invoke([action]{
        $sync.StatusText.Text       = $Text
        $sync.StatusText.Foreground = $Color
    }, "Normal")
}

# ── Show window ───────────────────────────────────────────────────────────────
$sync.window.ShowDialog() | Out-Null


