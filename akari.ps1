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

function Invoke-ConsoleScript {
    <#
    .SYNOPSIS
        Runs an EMBEDDED menu-driven script in an elevated PowerShell console.
        Fully self-contained — nothing on disk to carry, nothing downloaded.
    .DESCRIPTION
        Some tweaks are interactive menu-driven consoles that aren't worth
        re-implementing as native WPF flows (FR33THY's Ultimate menus, and your
        own scripts). Their .ps1 files live in assets/text/ and get baked into
        akari.ps1 as base64 at compile time. This helper decodes the chosen one
        to a temp file and launches it elevated.

        To add or update one: drop a file at assets/text/<name>.ps1, recompile,
        and call `Invoke-ConsoleScript -Asset "<name>"` from a button handler.
    .PARAMETER Asset
        Embedded asset key (the assets/text file's base name), e.g. "bloatware".
    .PARAMETER Confirm
        Optional prompt text. When set, a Yes/No warning dialog is shown first and
        the script only runs on Yes.
    .PARAMETER Status
        Status-bar text shown while launching.
    #>
    param(
        [Parameter(Mandatory)][string]$Asset,
        [string]$Confirm,
        [string]$Status = "Launching script..."
    )

    if ($Confirm) {
        $r = [System.Windows.MessageBox]::Show($Confirm, "Akari Tool", "YesNo", "Warning")
        if ($r -ne "Yes") { return }
    }

    if (-not ($sync.assets -and $sync.assets.$Asset)) {
        Set-Status "Embedded script not found: $Asset" "#EF5350"
        [System.Windows.MessageBox]::Show(
            "Embedded script '$Asset' is missing. Recompile akari.ps1 after adding assets\text\$Asset.ps1.",
            "Akari Tool", "OK", "Error") | Out-Null
        return
    }

    # Decode the embedded script to a temp file, then launch it elevated
    $dest = Join-Path $env:SystemRoot "Temp\akari_$Asset.ps1"
    $text = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$Asset))
    [IO.File]::WriteAllText($dest, $text.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))

    Set-Status $Status "#AAAAAA"
    Start-Elevated -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$dest`""
    Set-Status "Script launched (menu-driven console)." "#66BB6A"
}

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

# Defender disable/enable — full inline. Writes the safe-mode helper script
# (embedded asset), RunOnce's it, disables Defender tasks/Edge smartscreen,
# then safeboots and restarts. The helper undoes safeboot and reboots back.
function Invoke-BtnDefenderDisable {
    $r = [System.Windows.MessageBox]::Show("Disable Windows Defender? This reboots into Safe Mode, runs the disable script, and reboots back. Requires a working setup to undo. Continue?", "Defender: Disable", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Disabling Defender (Safe Mode wizard)..." -StatusDone "Defender disable scheduled — restarting." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        Write-Asset "defenderdisable" "$env:SystemRoot\Temp\defenderdisable.ps1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*defenderdisable`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\defenderdisable.ps1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        schtasks /Change /TN "Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cleanup" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Verification" /Disable 2>$null | Out-Null
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}
function Invoke-BtnDefenderEnable {
    Invoke-RunInBackground -StatusStart "Re-enabling Defender (Safe Mode wizard)..." -StatusDone "Defender enable scheduled — restarting." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        Write-Asset "defenderenable" "$env:SystemRoot\Temp\defenderenable.ps1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*defenderenable`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\defenderenable.ps1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        schtasks /Change /TN "Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cleanup" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Verification" /Enable 2>$null | Out-Null
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
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

# Services — full inline. Writes the embedded services off/on script, RunOnce's it
# with a safeboot reboot. The helper imports its reg defs in Safe Mode and reboots back.
function Invoke-BtnServicesOff {
    $r = [System.Windows.MessageBox]::Show("Disable non-essential services? This reboots into Safe Mode to apply them, then reboots back. Continue?", "Services: Off", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Disabling non-essential services (Safe Mode wizard)..." -StatusDone "Services off scheduled — restarting." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        Write-Asset "servicesoff" "$env:SystemRoot\Temp\servicesoff.ps1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*servicesoff`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\servicesoff.ps1`" /f >nul 2>&1"
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}
function Invoke-BtnServicesDefault {
    $r = [System.Windows.MessageBox]::Show("Restore default services? This reboots into Safe Mode to apply them, then reboots back. Continue?", "Services: Default", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Restoring default services (Safe Mode wizard)..." -StatusDone "Services default scheduled — restarting." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        Write-Asset "serviceson" "$env:SystemRoot\Temp\serviceson.ps1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*serviceson`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\serviceson.ps1`" /f >nul 2>&1"
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
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

# Shell / Search / Mobsync — full inline (mirrors upstream "18 Start Search Shell Mobsync")
function Invoke-BtnShellOff {
    $r = [System.Windows.MessageBox]::Show("Disable Start/Search shell, ShellExperienceHost and mobsync? Explorer restarts and search is disabled. Continue?", "Start Search Shell Mobsync: Off", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Disabling shell & search..." -StatusDone "Shell & search disabled." -ScriptBlock {
        cmd /c "takeown.exe /f $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
        cmd /c "takeown.exe /f $env:SystemRoot\SystemApps\ShellExperienceHost_cw5n1h2txyewy >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\SystemApps\ShellExperienceHost_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
        cmd /c "takeown.exe /f $env:SystemRoot\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
        cmd /c "takeown.exe /f $env:SystemRoot\System32\mobsync.exe >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\System32\mobsync.exe /grant *S-1-3-4:F /t /q >nul 2>&1"
        $stop = "AccountsServiceProduct","AppActions","Copilot","CrossDeviceResume","DesktopSpotlightProduct","DesktopStickerEditorWin32Exe","DiscoveryHubApp","FESearchHost","GameBar","IrisServiceProduct","LogonWebHostProduct","MicrosoftEdgeUpdate","MiniSearchHost","OneDrive","OneDrive.Sync.Service","OneDriveStandaloneUpdater","Resume","RulesEngineProduct","RuntimeBroker","ScreenClippingHost","Search","SearchApp","SearchHost","Setup","ShellExperienceHost","SoftLandingTask","StartMenuExperienceHost","StoreDesktopExtension","TextInputHost","VisualAssistExe","WebExperienceHostApp","WidgetService","Widgets","WindowsBackupClient","WindowsMigration","backgroundTaskHost","explorer","mobsync","msedge","msedgewebview2","smartscreen"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        cmd /c "move /y $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy $env:SystemRoot >nul 2>&1"
        cmd /c "move /y $env:SystemRoot\SystemApps\ShellExperienceHost_cw5n1h2txyewy $env:SystemRoot >nul 2>&1"
        cmd /c "move /y $env:SystemRoot\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy $env:SystemRoot >nul 2>&1"
        cmd /c "move /y $env:SystemRoot\System32\mobsync.exe $env:SystemRoot >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PolicyManager\default\Search\DisableSearch`" /v `"value`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search`" /v `"DisableSearch`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search`" /v `"SearchboxTaskbarMode`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\WSearch`" /v `"Start`" /t REG_DWORD /d `"4`" /f >nul 2>&1"
        cmd /c "taskkill /F /IM explorer.exe >nul 2>&1"
        cmd /c "start explorer.exe >nul 2>&1"
        Start-Sleep 15
        cmd /c "sc stop WSearch >nul 2>&1"
        cmd /c "takeown.exe /f $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
        cmd /c "taskkill /F /IM SearchApp.exe >nul 2>&1"
        cmd /c "taskkill /F /IM SearchHost.exe >nul 2>&1"
        cmd /c "move /y $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy $env:SystemRoot >nul 2>&1"
    }
}
function Invoke-BtnShellDefault {
    Invoke-RunInBackground -StatusStart "Restoring shell & search..." -StatusDone "Shell & search restored." -ScriptBlock {
        cmd /c "takeown.exe /f $env:SystemRoot\Microsoft.Windows.Search_cw5n1h2txyewy >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\Microsoft.Windows.Search_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
        cmd /c "takeown.exe /f $env:SystemRoot\ShellExperienceHost_cw5n1h2txyewy >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\ShellExperienceHost_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
        cmd /c "takeown.exe /f $env:SystemRoot\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
        cmd /c "takeown.exe /f $env:SystemRoot\mobsync.exe >nul 2>&1"
        cmd /c "icacls.exe $env:SystemRoot\mobsync.exe /grant *S-1-3-4:F /t /q >nul 2>&1"
        cmd /c "move /y $env:SystemRoot\Microsoft.Windows.Search_cw5n1h2txyewy $env:SystemRoot\SystemApps >nul 2>&1"
        cmd /c "move /y $env:SystemRoot\ShellExperienceHost_cw5n1h2txyewy $env:SystemRoot\SystemApps >nul 2>&1"
        cmd /c "move /y $env:SystemRoot\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy $env:SystemRoot\SystemApps >nul 2>&1"
        cmd /c "move /y $env:SystemRoot\mobsync.exe $env:SystemRoot\System32 >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PolicyManager\default\Search\DisableSearch`" /v `"value`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search`" /v `"SearchboxTaskbarMode`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\WSearch`" /v `"Start`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
        cmd /c "taskkill /F /IM explorer.exe >nul 2>&1"
        cmd /c "start explorer.exe >nul 2>&1"
        Start-Sleep 15
        cmd /c "sc stop WSearch >nul 2>&1"
        cmd /c "start explorer.exe >nul 2>&1"
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

# ReBar — full inline (upstream "7 ReBar Force"): installs NPI then applies the
# Default / Force On profile via silent .nip import.
function Invoke-BtnReBarDefault {
    Invoke-RunInBackground -StatusStart "ReBar: installing NPI + applying driver whitelist..." -StatusDone "ReBar default profile applied." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        Write-Asset "inspector_default" "$env:SystemRoot\Temp\default.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -silent $env:SystemRoot\Temp\default.nip"
        Start-Process "$base\nvidiaProfileInspector.exe"
    }
}
function Invoke-BtnReBarOn {
    Invoke-RunInBackground -StatusStart "ReBar: installing NPI + forcing ON..." -StatusDone "ReBar force-on applied." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        Write-Asset "inspector_forceon" "$env:SystemRoot\Temp\forceon.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -mergeImport -silent $env:SystemRoot\Temp\forceon.nip"
        Start-Process "$base\nvidiaProfileInspector.exe"
    }
}

# SMT / Core — real handlers live in Invoke-Missing.ps1 (keep delegated, interactive)

# WHQL Bypass
function Invoke-BtnWhqlBypass {
    Invoke-RunInBackground -StatusStart "Enabling WHQL bypass..." -StatusDone "WHQL bypass enabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy`" /v `"WHQLSettings`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}

# Invoke-BtnSpaceCheck has moved to Invoke-Check.ps1 (its logical home on the Check tab).
# This file is intentionally left as a stub to avoid a duplicate function definition.
# Safe to delete.

# Check PC — install OCCT, create shortcuts, launch, show guidance
function Invoke-BtnCheckPC {
    Invoke-RunInBackground -StatusStart "Installing OCCT..." -StatusDone "OCCT installed & launched." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code OCBase.OCCT.Personal_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"OCBase.OCCT.Personal`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\OCBase.OCCT.Personal_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\OCCT.lnk"); $sc.TargetPath = "$base\OCCT.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\OCCT.lnk"); $sc2.TargetPath = "$base\OCCT.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        Start-Process "$base\OCCT.exe"
    }
    $guidance = @"
DRIVES
- Keep drives at least 10% free
- Check drive errors and device health

RAM
- Check RAM profile is enabled
- Verify RAM is in the correct slots
- Confirm there is no mismatch in RAM modules
- At least two RAM sticks (dual channel) is ideal

GPU
- Check Video Bus is at maximum
- Check Resizable BAR is enabled
- Verify monitor cable is connected to the GPU
- Confirm GPU is in the top PCIe motherboard slot

TEST
Run a CPU, RAM & GPU stress test to check for errors.
Keep an eye on temps and WHEA errors during this test.
Errors should not be ignored as they can lead to stutters, hitches, corrupted Windows, poor performance, black/blue screens, input lag, and shutdowns.

TROUBLESHOOTING
- RAM overheating? Typically over 55deg (fix case flow/ram fan)
- Unlucky CPU memory controller? (lower RAM speed)
- CPU overheating? (repaste/retighten/RMA cooler)
- Overclock? (turn it off/dial it down)
- BIOS bugged out? (clear CMOS)
- Incompatible RAM? (check QVL)
- Faulty RAM stick or motherboard? (RMA)
"@
    [System.Windows.MessageBox]::Show($guidance, "PC Check Guidance", "OK", "Information")
}

# Check BIOS — enable password sign-in, search motherboard, show BIOS tips
function Invoke-BtnCheckBios {
    # Allow password sign-in (disables passwordless requirement)
    cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device`" /v `"DevicePasswordLessBuildVersion`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
    # Search motherboard ID in web browser
    $instanceID = (Get-CimInstance Win32_BaseBoard).Product
    $query = [uri]::EscapeDataString($instanceID)
    Start-Process "https://www.google.com/search?q=$query"
    $tips = @"
UPDATE BIOS & OPTIMIZE SETTINGS

INTEL CPU
- ENABLE ram profile (XMP DOCP EXPO)
- DISABLE c-state (K CHIPS ONLY)
- ENABLE resizable bar (REBAR C.A.M)

AMD CPU
- ENABLE ram profile (XMP DOCP EXPO)
- ENABLE precision boost overdrive (PBO)
- ENABLE resizable bar (REBAR C.A.M)

DISABLE unused features (BT/WIFI/IGPU/ETC)
DISABLE driver installer software (Armory Crate / MSI Utility / Gigabyte Update / Asrock Utility)
MAX pump and set fans to performance
"@
    $r = [System.Windows.MessageBox]::Show("$tips`n`nRestart to BIOS now?", "BIOS Settings", "YesNo", "Information")
    if ($r -eq "Yes") { shutdown /r /fw /t 0 }
}

# DDU (Driver Clean) — Auto / Manual (1:1 with "5 Graphics/1 Driver Clean.ps1")
function Invoke-BtnDduAuto {
    $r = [System.Windows.MessageBox]::Show("Clean ALL graphics drivers with DDU and restart? Runs in Safe Mode, auto-cleans with DDU's CleanAllGpus, and restarts into Windows. Continue?", "DDU: Auto", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "DDU Auto: preparing safe-mode clean..." -StatusDone "DDU Auto scheduled — restarting into Safe Mode." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Wagnardsoft.DisplayDriverUninstaller`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $dduDir = "$env:SystemDrive\Program Files (x86)\Display Driver Uninstaller"
        New-Item -Path $dduDir -Name "Settings" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "ddusettings" "$dduDir\Settings\Settings.xml"
        Set-ItemProperty -Path "$dduDir\Settings\Settings.xml" -Name IsReadOnly -Value $true
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\DriverSearching`" /v `"SearchOrderConfig`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Display Driver Uninstaller\Display Driver Uninstaller.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Display Driver Uninstaller" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "ddu" "$env:SystemRoot\Temp\ddu.ps1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*ddu`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\ddu.ps1`" /f >nul 2>&1"
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}
function Invoke-BtnDduManual {
    $r = [System.Windows.MessageBox]::Show("Open DDU in Safe Mode for a manual clean? Runs in Safe Mode and opens the DDU GUI so you pick the options. Continue?", "DDU: Manual", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "DDU Manual: preparing safe-mode clean..." -StatusDone "DDU Manual scheduled — restarting into Safe Mode." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"Wagnardsoft.DisplayDriverUninstaller`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $dduDir = "$env:SystemDrive\Program Files (x86)\Display Driver Uninstaller"
        New-Item -Path $dduDir -Name "Settings" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "ddusettings" "$dduDir\Settings\Settings.xml"
        Set-ItemProperty -Path "$dduDir\Settings\Settings.xml" -Name IsReadOnly -Value $true
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\DriverSearching`" /v `"SearchOrderConfig`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Display Driver Uninstaller\Display Driver Uninstaller.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Display Driver Uninstaller" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "ddumanual" "$env:SystemRoot\Temp\ddumanual.ps1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*ddumanual`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\ddumanual.ps1`" /f >nul 2>&1"
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}

# Driver updated install — NVIDIA / AMD / Intel (1:1 with "5 Graphics/2 Driver Updated Install.ps1")
function Invoke-BtnDriverNvidia {
    Invoke-RunInBackground -StatusStart "Downloading latest NVIDIA driver..." -StatusDone "NVIDIA driver downloaded — installer opened." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        $uri = 'https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=120&pfid=929&osID=57&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1'
        try {
            $response = Invoke-WebRequest -Uri $uri -Method GET -UseBasicParsing
            $payload = $response.Content | ConvertFrom-Json
            $version = $payload.IDS[0].downloadInfo.Version
            $windowsVersion = if ([Environment]::OSVersion.Version -ge (New-Object 'Version' 9, 1)) { 'win10-win11' } else { 'win8-win7' }
            $windowsArchitecture = if ([Environment]::Is64BitOperatingSystem) { '64bit' } else { '32bit' }
            $url = "https://international.download.nvidia.com/Windows/$version/$version-desktop-$windowsVersion-$windowsArchitecture-international-dch-whql.exe"
            Invoke-WebRequest $url -OutFile "$env:SystemRoot\Temp\nvidiadriver.exe"
        } catch {
            Start-Process "https://www.nvidia.com/en-us/drivers"
            return
        }
        Start-Process "$env:SystemRoot\Temp\nvidiadriver.exe"
    }
}
function Invoke-BtnDriverAmd {
    Invoke-RunInBackground -StatusStart "Downloading latest AMD driver..." -StatusDone "AMD driver downloaded — installer opened." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        try {
            $DownloadAmd = Invoke-WebRequest "https://www.amd.com/en/support/download/drivers.html" -UseBasicParsing |
                Select-Object -ExpandProperty Links |
                Where-Object { $_.href -match "drivers\.amd\.com/drivers/installer/.*/whql/amd-software-adrenalin-edition-.*-minimalsetup-.*_web\.exe" } | Select-Object href
            $spoofwebbrowser = @{
                "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
                "Accept"     = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
                "Referer"    = "https://www.amd.com/"
            }
            Invoke-WebRequest $DownloadAmd.href -UseBasicParsing -Headers $spoofwebbrowser -OutFile "$env:SystemRoot\Temp\amddriver.exe" -ErrorAction SilentlyContinue | Out-Null
            Start-Process "$env:SystemRoot\Temp\amddriver.exe"
        } catch {
            Start-Process "https://www.amd.com/en/support/download/drivers.html"
        }
    }
}
function Invoke-BtnDriverIntel {
    Start-Process "https://www.intel.com/content/www/us/en/search.html#sortCriteria=%40lastmodifieddt%20descending&f-operatingsystem_en=Windows%2011%20Family*&f-downloadtype=Drivers&cf-tabfilter=Downloads&cf-downloadsppth=Graphics"
}

# ── Debloat driver install & settings (1:1 with "5 Graphics/4 Driver Debloat Install & Settings.ps1") ──
# Common tail used by all three vendors: AutoColorManagementEnabled, MSI mode,
# show all hidden taskbar icons, then restart.
function Invoke-BtnDriverDebloatNvidia {
    $r = [System.Windows.MessageBox]::Show("Download the NVIDIA driver installer first (browser opens). Click OK when the download finishes, then pick the downloaded .exe. Driver will be debloated, installed, settings applied, and the PC restarts. Continue?", "Debloat Driver: NVIDIA", "OKCancel", "Information")
    if ($r -ne "OK") { return }
    Start-Sleep -Seconds 5
    Start-Process "https://www.nvidia.com/en-us/drivers"
    $r2 = [System.Windows.MessageBox]::Show("Select the downloaded NVIDIA driver file.", "Select Driver", "OKCancel", "Information")
    if ($r2 -ne "OK") { return }
    Add-Type -AssemblyName System.Windows.Forms
    $Dialog = New-Object System.Windows.Forms.OpenFileDialog
    $Dialog.Filter = "All Files (*.*)|*.*"
    if ($Dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
    $file = $Dialog.FileName
    Invoke-RunInBackground -StatusStart "Debloating & installing NVIDIA driver..." -StatusDone "NVIDIA driver installed with settings. Restarting." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        $InstallFile = $file
        # download and install 7-zip
        try { Start-Process "winget" -ArgumentList "install `"7zip.7zip`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\7-Zip File Manager.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\7-Zip\7zFM.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\7-Zip"; $sc.Save()
        # extract driver
        & "$env:SystemDrive\Program Files\7-Zip\7z.exe" x "$InstallFile" -o"$env:SystemRoot\Temp\nvidiadriver" -y | Out-Null
        # debloat
        $RemoveList = "Display.Nview","FrameViewSDK","HDAudio","MSVCRT","NvApp.MessageBus","NvBackend","NvContainer","NvCpl","NvDLISR","NVPCF","NvTelemetry","NvVAD","PhysX","PPC","ShadowPlay"
        foreach ($d in $RemoveList) { Remove-Item "$env:SystemRoot\Temp\nvidiadriver\$d" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null }
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\CEF" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\osc" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\Plugins" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\UpgradeConsent" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\www" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\7z.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\7z.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\DarkModeCheck.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\InstallerExtension.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\NvApp.nvi" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\NvAppApi.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\NvAppExt.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemRoot\Temp\nvidiadriver\NvApp\NvConfigGenerator.dll" -Force -ErrorAction SilentlyContinue | Out-Null
        # install driver
        Start-Process "$env:SystemRoot\Temp\nvidiadriver\setup.exe" -ArgumentList "-s -noreboot -noeula -clean" -Wait -NoNewWindow
        # nvidia control panel
        try { Start-Process "winget" -ArgumentList "install `"9NF8H0H7WMLT`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $sc2 = $sh.CreateShortcut("$Desktop\NVIDIA Control Panel.lnk"); $sc2.TargetPath = "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"; $sc2.WorkingDirectory = "shell:appsFolder"; $sc2.Save()
        Remove-Item "$InstallFile" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\NVIDIA" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        # settings
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "DisableDynamicPstate" /t REG_DWORD /d "1" /f | Out-Null } }
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RMHdcpKeyglobZero" /t REG_DWORD /d "1" /f | Out-Null } }
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvCplPhysxAuto`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvDevToolsVisible`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RmProfilingAdminOnly" /t REG_DWORD /d "0" /f | Out-Null } }
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"RmProfilingAdminOnly`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\NVIDIA Corporation\NvTray`" /v `"StartOnLogin`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        # nvidia profile inspector + profile
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sc3 = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc3.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc3.WorkingDirectory = $base; $sc3.Save()
        $sc4 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc4.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc4.WorkingDirectory = $base; $sc4.Save()
        Write-Asset "inspector_default" "$env:SystemRoot\Temp\inspector.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -silent $env:SystemRoot\Temp\inspector.nip"
        # common final: color mgmt, MSI, taskbar icons, restart
        $basePath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore"
        $monitorKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue
        foreach ($key in $monitorKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"AutoColorManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        $gpuDevices = Get-PnpDevice -Class Display
        foreach ($gpu in $gpuDevices) { $instanceID = $gpu.InstanceId; cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$instanceID\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"1`" /f >nul 2>&1" }
        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 1 -Force }
        }
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }.GetNewClosure()
}
function Invoke-BtnDriverDebloatAmd {
    $r = [System.Windows.MessageBox]::Show("Download the AMD driver installer first (browser opens). Click OK when the download finishes, then pick the downloaded .exe. Driver will be debloated, installed, settings applied, and the PC restarts. Continue?", "Debloat Driver: AMD", "OKCancel", "Information")
    if ($r -ne "OK") { return }
    Start-Sleep -Seconds 5
    Start-Process "https://www.amd.com/en/support/download/drivers.html"
    $r2 = [System.Windows.MessageBox]::Show("Select the downloaded AMD driver file.", "Select Driver", "OKCancel", "Information")
    if ($r2 -ne "OK") { return }
    Add-Type -AssemblyName System.Windows.Forms
    $Dialog = New-Object System.Windows.Forms.OpenFileDialog
    $Dialog.Filter = "All Files (*.*)|*.*"
    if ($Dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
    $file = $Dialog.FileName
    Invoke-RunInBackground -StatusStart "Debloating & installing AMD driver..." -StatusDone "AMD driver installed with settings. Restarting." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        $InstallFile = $file
        try { Start-Process "winget" -ArgumentList "install `"7zip.7zip`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\7-Zip File Manager.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\7-Zip\7zFM.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\7-Zip"; $sc.Save()
        & "$env:SystemDrive\Program Files\7-Zip\7z.exe" x "$InstallFile" -o"$env:SystemRoot\Temp\amddriver" -y | Out-Null
        # edit xml files, set enabled & hidden to false
        $xmlFiles = @(
            "$env:SystemRoot\Temp\amddriver\Config\AMDAUEPInstaller.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDCOMPUTE.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDLinkDriverUpdate.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDRELAUNCHER.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDScoSupportTypeUpdate.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDUpdater.xml",
            "$env:SystemRoot\Temp\amddriver\Config\AMDUWPLauncher.xml",
            "$env:SystemRoot\Temp\amddriver\Config\EnableWindowsDriverSearch.xml",
            "$env:SystemRoot\Temp\amddriver\Config\InstallUEP.xml",
            "$env:SystemRoot\Temp\amddriver\Config\ModifyLinkUpdate.xml"
        )
        foreach ($file2 in $xmlFiles) {
            if (Test-Path $file2) {
                $content = Get-Content $file2 -Raw
                $content = $content -replace '<Enabled>true</Enabled>', '<Enabled>false</Enabled>'
                $content = $content -replace '<Hidden>true</Hidden>', '<Hidden>false</Hidden>'
                Set-Content $file2 -Value $content -NoNewline
            }
        }
        # edit json files, set installbydefault to no
        $jsonFiles = @(
            "$env:SystemRoot\Temp\amddriver\Config\InstallManifest.json",
            "$env:SystemRoot\Temp\amddriver\Bin64\cccmanifest_64.json"
        )
        foreach ($file2 in $jsonFiles) {
            if (Test-Path $file2) {
                $content = Get-Content $file2 -Raw
                $content = $content -replace '"InstallByDefault"\s*:\s*"Yes"', '"InstallByDefault" : "No"'
                Set-Content $file2 -Value $content -NoNewline
            }
        }
        # install driver
        Start-Process -Wait "$env:SystemRoot\Temp\amddriver\Bin64\ATISetup.exe" -ArgumentList "-INSTALL -VIEW:2" -WindowStyle Hidden
        Start-Sleep -Seconds 90
        # cleanup
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /v `"AMDNoiseSuppression`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"StartRSX`" /f >nul 2>&1"
        Unregister-ScheduledTask -TaskName "StartCN" -Confirm:$false -ErrorAction SilentlyContinue
        cmd /c "sc stop `"AMD Crash Defender Service`" >nul 2>&1"; cmd /c "sc delete `"AMD Crash Defender Service`" >nul 2>&1"
        cmd /c "sc stop `"amdfendr`" >nul 2>&1"; cmd /c "sc delete `"amdfendr`" >nul 2>&1"
        cmd /c "sc stop `"amdfendrmgr`" >nul 2>&1"; cmd /c "sc delete `"amdfendrmgr`" >nul 2>&1"
        cmd /c "sc stop `"amdacpbus`" >nul 2>&1"; cmd /c "sc delete `"amdacpbus`" >nul 2>&1"
        cmd /c "sc stop `"AMDSAFD`" >nul 2>&1"; cmd /c "sc delete `"AMDSAFD`" >nul 2>&1"
        cmd /c "sc stop `"AtiHDAudioService`" >nul 2>&1"; cmd /c "sc delete `"AtiHDAudioService`" >nul 2>&1"
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\AMD Bug Report Tool" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\Windows\SysWOW64\AMDBugReportTool.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        $findamdinstallmanager = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
        $amdinstallmanager = Get-ItemProperty $findamdinstallmanager -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like "*AMD Install Manager*" }
        if ($amdinstallmanager) { $guid = $amdinstallmanager.PSChildName; Start-Process "msiexec.exe" -ArgumentList "/x $guid /qn /norestart" -Wait -NoNewWindow }
        Remove-Item "$InstallFile" -Force -ErrorAction SilentlyContinue | Out-Null
        $sc2 = $sh.CreateShortcut("$Desktop\AMD Radeon Software.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files\AMD\CNext\CNext\RadeonSoftware.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files\AMD\CNext\CNext"; $sc2.Save()
        $folderName = "AMD Software$([char]0xA789) Adrenalin Edition"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\$folderName\$folderName.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\$folderName" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:SystemDrive\AMD" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        # import settings
        Start-Process "$env:SystemDrive\Program Files\AMD\CNext\CNext\RadeonSoftware.exe"
        Start-Sleep -Seconds 15
        Stop-Process -Name "RadeonSoftware" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AutoUpdate`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"WizardProfile`" /t REG_SZ /d `"PROFILE_CUSTOM`" /f >nul 2>&1"
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"VSyncControl`" /t REG_BINARY /d `"3000`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"TFQ`" /t REG_BINARY /d `"3200`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) {
            $regPath = $key.Name
            cmd /c "reg add `"$regPath`" /v `"Tessellation`" /t REG_BINARY /d `"3100`" /f >nul 2>&1"
            cmd /c "reg add `"$regPath`" /v `"Tessellation_OPTION`" /t REG_BINARY /d `"3200`" /f >nul 2>&1"
        }
        cmd /c "reg add `"HKCU\Software\AMD\CN\CustomResolutions`" /v `"EulaAccepted`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\DisplayOverride`" /v `"EulaAccepted`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "power_v1" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"abmlevel`" /t REG_BINARY /d `"00000000`" /f >nul 2>&1" }
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"SystemTray`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"CN_Hide_Toast_Notification`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AnimationEffect`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\FreeSync`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\OverlayNotification`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\VirtualSuperResolution`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        # common final: color mgmt, MSI, taskbar icons, restart
        $basePath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore"
        $monitorKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue
        foreach ($key in $monitorKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"AutoColorManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        $gpuDevices = Get-PnpDevice -Class Display
        foreach ($gpu in $gpuDevices) { $instanceID = $gpu.InstanceId; cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$instanceID\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"1`" /f >nul 2>&1" }
        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 1 -Force }
        }
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }.GetNewClosure()
}
function Invoke-BtnDriverDebloatIntel {
    $r = [System.Windows.MessageBox]::Show("Download the Intel driver installer first (browser opens). Click OK when the download finishes, then pick the downloaded file. Driver will be debloated, installed, settings applied, and the PC restarts. Continue?", "Debloat Driver: Intel", "OKCancel", "Information")
    if ($r -ne "OK") { return }
    Start-Sleep -Seconds 5
    Start-Process "https://www.intel.com/content/www/us/en/search.html#sortCriteria=%40lastmodifieddt%20descending&f-operatingsystem_en=Windows%2011%20Family*&f-downloadtype=Drivers&cf-tabfilter=Downloads&cf-downloadsppth=Graphics"
    $r2 = [System.Windows.MessageBox]::Show("Select the downloaded Intel driver file.", "Select Driver", "OKCancel", "Information")
    if ($r2 -ne "OK") { return }
    Add-Type -AssemblyName System.Windows.Forms
    $Dialog = New-Object System.Windows.Forms.OpenFileDialog
    $Dialog.Filter = "All Files (*.*)|*.*"
    if ($Dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
    $file = $Dialog.FileName
    Invoke-RunInBackground -StatusStart "Debloating & installing Intel driver..." -StatusDone "Intel driver installed with settings. Restarting." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        $InstallFile = $file
        try { Start-Process "winget" -ArgumentList "install `"7zip.7zip`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"ContextMenu`" /t REG_DWORD /d `"259`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\7-Zip\Options`" /v `"CascadedMenu`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip\7-Zip File Manager.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\7-Zip" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\7-Zip File Manager.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files\7-Zip\7zFM.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files\7-Zip"; $sc.Save()
        & "$env:SystemDrive\Program Files\7-Zip\7z.exe" x "$InstallFile" -o"$env:SystemDrive\inteldriver" -y | Out-Null
        Start-Process "cmd.exe" -ArgumentList "/c `"$env:SystemDrive\inteldriver\Installer.exe`" -f --noExtras --terminateProcesses -s" -WindowStyle Hidden -Wait
        $IntelGraphicsSoftware = Get-ChildItem "$env:SystemDrive\inteldriver\Resources\Extras\IntelGraphicsSoftware_*.exe" | Select-Object -First 1 -ExpandProperty Name
        if ($IntelGraphicsSoftware) { Start-Process "$env:SystemDrive\inteldriver\Resources\Extras\$IntelGraphicsSoftware" -ArgumentList "/s" -Wait -NoNewWindow }
        $sc2 = $sh.CreateShortcut("$Desktop\Intel Graphics Software.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files\Intel\Intel Graphics Software\IntelGraphicsSoftware.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files\Intel\Intel Graphics Software"; $sc2.Save()
        $FileName = "Intel$([char]0xAE) Graphics Software"
        cmd /c "reg delete `"HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /v `"$FileName`" /f >nul 2>&1"
        cmd /c "sc stop `"IntelGFXFWupdateTool`" >nul 2>&1"; cmd /c "sc delete `"IntelGFXFWupdateTool`" >nul 2>&1"
        cmd /c "sc stop `"cplspcon`" >nul 2>&1"; cmd /c "sc delete `"cplspcon`" >nul 2>&1"
        cmd /c "sc stop `"CtaChildDriver`" >nul 2>&1"; cmd /c "sc delete `"CtaChildDriver`" >nul 2>&1"
        cmd /c "sc stop `"GSCAuxDriver`" >nul 2>&1"; cmd /c "sc delete `"GSCAuxDriver`" >nul 2>&1"
        cmd /c "sc stop `"GSCx64`" >nul 2>&1"; cmd /c "sc delete `"GSCx64`" >nul 2>&1"
        $stop = "IntelGraphicsSoftware", "PresentMonService"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Remove-Item "$env:SystemDrive\Program Files\Intel\Intel Graphics Software\PresentMonService.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$InstallFile" -Force -ErrorAction SilentlyContinue | Out-Null
        Move-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Intel\Intel Graphics Software\$FileName.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Intel" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item "$env:SystemDrive\Intel" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\inteldriver" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        # create 3dkeys + settings
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath\3DKeys`" /f >nul 2>&1" } }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "3DKeys" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_AsyncFlipMode`" /t REG_DWORD /d `"2`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "3DKeys" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_LowLatency`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences`" /v `"DirectXUserGlobalSettings`" /t REG_SZ /d `"SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;`" /f >nul 2>&1"
        # common final: color mgmt, MSI, taskbar icons, restart
        $basePath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore"
        $monitorKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue
        foreach ($key in $monitorKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"AutoColorManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        $gpuDevices = Get-PnpDevice -Class Display
        foreach ($gpu in $gpuDevices) { $instanceID = $gpu.InstanceId; cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Enum\$instanceID\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties`" /v `"MSISupported`" /t REG_DWORD /d `"1`" /f >nul 2>&1" }
        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 1 -Force }
        }
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }.GetNewClosure()
}

# GPU Settings — On / Default (1:1 with "5 Graphics/5 Nvidia Settings.ps1")
function Invoke-BtnNvidiaOn {
    Invoke-RunInBackground -StatusStart "Applying NVIDIA settings (On)..." -StatusDone "NVIDIA settings applied." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "install `"9NF8H0H7WMLT`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\NVIDIA Control Panel.lnk"); $sc.TargetPath = "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"; $sc.WorkingDirectory = "shell:appsFolder"; $sc.Save()
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvCplPhysxAuto`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvDevToolsVisible`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { reg add "$key" /v "RmProfilingAdminOnly" /t REG_DWORD /d "0" /f | Out-Null } }
        cmd /c "reg add `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"RmProfilingAdminOnly`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\NVIDIA Corporation\NvTray`" /v `"StartOnLogin`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sc2 = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        $sc3 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc3.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc3.WorkingDirectory = $base; $sc3.Save()
        Write-Asset "inspector_default" "$env:SystemRoot\Temp\inspector.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -silent $env:SystemRoot\Temp\inspector.nip"
        Start-Process "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"
    }
}
function Invoke-BtnNvidiaDefault {
    Invoke-RunInBackground -StatusStart "Applying NVIDIA settings (Default)..." -StatusDone "NVIDIA settings restored." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvCplPhysxAuto`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"NvDevToolsVisible`" /f >nul 2>&1"
        $subkeys = Get-ChildItem -Path "Registry::HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -Force -ErrorAction SilentlyContinue
        foreach ($key in $subkeys) { if ($key -notlike '*Configuration') { cmd /c "reg delete `"$key`" /v `"RmProfilingAdminOnly`" /f >nul 2>&1" } }
        cmd /c "reg delete `"HKLM\System\ControlSet001\Services\nvlddmkm\Parameters\Global\NVTweak`" /v `"RmProfilingAdminOnly`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\NVIDIA Corporation\NvTray`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Parameters\FTS`" /v `"EnableGR535`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc2 = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        $sc3 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc3.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc3.WorkingDirectory = $base; $sc3.Save()
        Write-Asset "inspector_empty" "$env:SystemRoot\Temp\inspector.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -silent $env:SystemRoot\Temp\inspector.nip"
        Start-Process "shell:appsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIACorp.NVIDIAControlPanel"
    }
}

# GPU Settings — AMD On / Default (1:1 with "5 Graphics/6 Amd Settings.ps1")
function Invoke-BtnAmdOn {
    Invoke-RunInBackground -StatusStart "Applying AMD settings (On)..." -StatusDone "AMD settings applied." -ScriptBlock {
        Start-Process "$env:SystemDrive\Program Files\AMD\CNext\CNext\RadeonSoftware.exe"
        Start-Sleep -Seconds 30
        Stop-Process -Name "RadeonSoftware" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AutoUpdate`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\AIM`" /v `"LaunchBugTool`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\DVR`" /v `"HotkeysDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"SystemTray`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\DVR`" /v `"ShowRSOverlay`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"RSXBrowserUnavailable`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AllowWebContent`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"CN_Hide_Toast_Notification`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"AnimationEffect`" /t REG_SZ /d `"false`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN`" /v `"WizardProfile`" /t REG_SZ /d `"PROFILE_CUSTOM`" /f >nul 2>&1"
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"VSyncControl`" /t REG_BINARY /d `"3000`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"TFQ`" /t REG_BINARY /d `"3200`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) {
            $regPath = $key.Name
            cmd /c "reg add `"$regPath`" /v `"Tessellation`" /t REG_BINARY /d `"3100`" /f >nul 2>&1"
            cmd /c "reg add `"$regPath`" /v `"Tessellation_OPTION`" /t REG_BINARY /d `"3200`" /f >nul 2>&1"
        }
        cmd /c "reg add `"HKCU\Software\AMD\CN\CustomResolutions`" /v `"EulaAccepted`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\DisplayOverride`" /v `"EulaAccepted`" /t REG_SZ /d `"true`" /f >nul 2>&1"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "power_v1" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"abmlevel`" /t REG_BINARY /d `"00000000`" /f >nul 2>&1" }
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"IsAutoDefault`" /t REG_BINARY /d `"00000000`" /f >nul 2>&1" } }
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"IsComponentControl`" /t REG_BINARY /d `"0f000000`" /f >nul 2>&1" } }
        cmd /c "reg delete `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\FreeSync`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\OverlayNotification`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\CN\VirtualSuperResolution`" /v `"AlreadyNotified`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}
function Invoke-BtnAmdDefault {
    Invoke-RunInBackground -StatusStart "Restoring AMD settings (Default)..." -StatusDone "AMD settings restored." -ScriptBlock {
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"AutoUpdate`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\AMD\AIM`" /v `"LaunchBugTool`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\DVR`" /v `"HotkeysDisabled`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"SystemTray`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\DVR`" /v `"ShowRSOverlay`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"RSXBrowserUnavailable`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"AllowWebContent`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"CN_Hide_Toast_Notification`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"AnimationEffect`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN`" /v `"WizardProfile`" /f >nul 2>&1"
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"VSyncControl`" /t REG_BINARY /d `"31000000`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg delete `"$regPath`" /v `"TFQ`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "UMD" }
        foreach ($key in $optionKeys) {
            $regPath = $key.Name
            cmd /c "reg add `"$regPath`" /v `"Tessellation`" /t REG_BINARY /d `"360034000000`" /f >nul 2>&1"
            cmd /c "reg add `"$regPath`" /v `"Tessellation_OPTION`" /t REG_BINARY /d `"30000000`" /f >nul 2>&1"
        }
        cmd /c "reg delete `"HKCU\Software\AMD\CN\CustomResolutions`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\DisplayOverride`" /f >nul 2>&1"
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "power_v1" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg delete `"$regPath`" /v `"abmlevel`" /f >nul 2>&1" }
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"IsAutoDefault`" /t REG_DWORD /d `"1`" /f >nul 2>&1" } }
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"IsComponentControl`" /t REG_BINARY /d `"00000000`" /f >nul 2>&1" } }
        cmd /c "reg delete `"HKCU\Software\AMD\CN\Notification`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\FreeSync`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\OverlayNotification`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\AMD\CN\VirtualSuperResolution`" /f >nul 2>&1"
    }
}

# GPU Settings — Intel On / Default (1:1 with "5 Graphics/7 Intel Settings.ps1")
function Invoke-BtnIntelOn {
    Invoke-RunInBackground -StatusStart "Applying Intel settings (On)..." -StatusDone "Intel settings applied." -ScriptBlock {
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg add `"$regPath\3DKeys`" /f >nul 2>&1" } }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "3DKeys" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_AsyncFlipMode`" /t REG_DWORD /d `"2`" /f >nul 2>&1" }
        $optionKeys = Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "3DKeys" }
        foreach ($key in $optionKeys) { $regPath = $key.Name; cmd /c "reg add `"$regPath`" /v `"Global_LowLatency`" /t REG_DWORD /d `"0`" /f >nul 2>&1" }
        cmd /c "reg add `"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences`" /v `"DirectXUserGlobalSettings`" /t REG_SZ /d `"SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;`" /f >nul 2>&1"
    }
}
function Invoke-BtnIntelDefault {
    Invoke-RunInBackground -StatusStart "Restoring Intel settings (Default)..." -StatusDone "Intel settings restored." -ScriptBlock {
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) { if ($key.PSChildName -match '^\d{4}$') { $regPath = $key.Name; cmd /c "reg delete `"$regPath\3DKeys`" /f >nul 2>&1" } }
    }
}

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

# Monitor Optimization — opens the UFO frame-rate test then lists the checklist
function Invoke-BtnMonitorOpt {
    Invoke-RunInBackground -StatusStart "Opening monitor test..." -StatusDone "Monitor test opened." -ScriptBlock {
        Start-Process "https://www.testufo.com/framerates#count=6&background=none&pps=1920"
    }
    [System.Windows.MessageBox]::Show("Monitor optimizations:`n- Enable overclock mode`n- Run highest refresh rate`n- Disable adaptive brightness and variable back light`n- Turn off variable refresh rate, adaptive sync and g-sync`n- Adjust color, brightness and sharpening to your preference`n- Max overdrive without causing overshoot or reducing motion clarity", "Monitor Optimizations", "OK", "Information") | Out-Null
}

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

# Mouse Polling Rate Test — opens the polling test then shows the checklist
function Invoke-BtnMouseTest {
    Invoke-RunInBackground -StatusStart "Opening mouse polling test..." -StatusDone "Mouse test opened." -ScriptBlock {
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) { return }
        Start-Process "https://cpstest.org/polling-rate-test"
    }
    [System.Windows.MessageBox]::Show("Mouse optimizations:`n- Turn off motion sync`n- Keep dongle close to mouse`n- Disable angle snapping`n- Set lowest debounce time`n- Use maximum polling rate`n- USB port closest to the CPU`n`nExtreme polling may affect lower end CPU's & certain game engine framerates`n`nSet a comfortable DPI - increased DPI reduces pixel skipping & latency`nSuggested minimal DPI:`n- 400dpi for 1080p`n- 800dpi for 1440p`n- 1600dpi for 4k`n`nTo prevent mouse acceleration when gaming:`n- Use 100% scaling`n- Set 6/11 & pointer precision off`n- Enable raw input in games when possible`n`nFor higher scaling with no acceleration see the Scaling section.", "Mouse Optimizations", "OK", "Information") | Out-Null
}

# Controller Overclock — installs hidusbf (driver + Setup) and shortcuts
function Invoke-BtnControllerOC {
    Invoke-RunInBackground -StatusStart "Installing hidusbf..." -StatusDone "hidusbf installed. Run Setup.exe from the desktop/Start menu." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) { return }
        IWR "https://github.com/LordOfMice/hidusbf/raw/refs/heads/master/hidusbf.zip" -OutFile "$env:SystemRoot\Temp\hidusbf.zip"
        Expand-Archive -Path "$env:SystemRoot\Temp\hidusbf.zip" -DestinationPath "$env:SystemDrive\Program Files (x86)\hidusbf" -Force
        Start-Process -FilePath "rundll32.exe" -ArgumentList "setupapi.dll,InstallHinfSection DefaultInstall 132 $env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\HIDUSBF_AS.INF" -Wait
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Setup.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\Setup.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER"; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Setup.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\Setup.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER"; $sc2.Save()
    }
}

# Controller Polling Rate Test — installs Polling app and opens it
function Invoke-BtnControllerTest {
    Invoke-RunInBackground -StatusStart "Installing Polling..." -StatusDone "Polling installed & opened." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) { return }
        New-Item -Path "$env:SystemDrive\Program Files (x86)\Polling" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
        IWR "https://github.com/cakama3a/Polling/releases/download/1.3.1.4/Polling.exe" -OutFile "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Polling.lnk"); $sc.TargetPath = "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"; $sc.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Polling"; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Polling.lnk"); $sc2.TargetPath = "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"; $sc2.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Polling"; $sc2.Save()
        Start-Process "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"
    }
}

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

# ── Windows: Loudness EQ (1:1 with "6 Windows/24 Loudness EQ.ps1") ───────────
function Invoke-BtnLoudnessEQ {
    Invoke-RunInBackground -StatusStart "Unhiding Enhancements tab..." -StatusDone "Enhancements tab unhidden for all sound devices." -ScriptBlock {
        Stop-Service audiosrv -Force
        Stop-Service AudioEndpointBuilder -Force
        $basePath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render"
        $guids = Get-ChildItem -Path $basePath -Force -ErrorAction SilentlyContinue
        $regContent = "Windows Registry Editor Version 5.00`n"
        foreach ($guid in $guids) {
            $regPath = $guid.Name
            $regContent += "`n[$regPath\FxProperties]`n"
            $regContent += "`"{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},3`"=`"{5860E1C5-F95C-4a7a-8EC8-8AEF24F379A1}`"`n"
        }
        [IO.File]::WriteAllText("$env:SystemRoot\Temp\loudnesseq.reg", $regContent, (New-Object Text.UTF8Encoding($false)))
        regedit /s "$env:SystemRoot\Temp\loudnesseq.reg"
        Start-Service audiosrv
        Start-Service AudioEndpointBuilder
        Start-Process mmsys.cpl
    }
}

# ── Windows: Control Panel Settings (1:1 with "6 Windows/22 Control Panel
#    Settings.ps1" Optimize / Default branches) ───────────────────────────────
# Local Run-Trusted copy used inside runspaces below (TrustedInstaller swap trick).
function Invoke-BtnCPOptimize {
    $r = [System.Windows.MessageBox]::Show("Apply all Control Panel optimizations? Includes the registry tweaks (write/import two reg files), TrustedInstaller app-permission reset, scheduled defrag disable, console lock, priority notifications, and disabling app actions. Continue?", "Control Panel Settings: Optimize", "YesNo", "Warning")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Applying Control Panel optimizations..." -StatusDone "Control Panel optimizations applied. Log out to apply fully." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        function Run-Trusted([String]$command) {
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
            $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
            $DefaultBinPath = $service.PathName
            $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
            if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
            $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
            $base64Command = [Convert]::ToBase64String($bytes)
            sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
            sc.exe start TrustedInstaller | Out-Null
            sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
        }
        # fix 1 for turn off privacy & security app permissions
        Stop-Service -Name 'camsvc' -Force -ErrorAction SilentlyContinue
        $capabilityconsentstoragedb = "Remove-item `"$env:ProgramData\Microsoft\Windows\CapabilityAccessManager\CapabilityConsentStorage.db*`" -Force"
        Run-Trusted -command $capabilityconsentstoragedb
        # fix for disable windows backup
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\CDPUserSvc`" /v `"Start`" /t REG_DWORD /d `"4`" /f >nul 2>&1"
        # registry optimizations
        Write-Asset "registryoptimize" "$env:SystemRoot\Temp\registryoptimize.reg"
        Regedit.exe /S "$env:SystemRoot\Temp\registryoptimize.reg"
        # fix 2 for turn off privacy & security app permissions
        Stop-Service -Name 'camsvc' -Force -ErrorAction SilentlyContinue
        Run-Trusted -command $capabilityconsentstoragedb
        # disable defragment and optimize your drives scheduled task
        Get-ScheduledTask | Where-Object { $_.TaskName -match 'ScheduledDefrag' } | Disable-ScheduledTask | Out-Null
        # disable if you've been away sign-in prompt
        powercfg /setdcvalueindex scheme_current sub_none consolelock 0 2>$null
        powercfg /setacvalueindex scheme_current sub_none consolelock 0 2>$null
        # disable set priority notifications (per-GUID blob)
        $disableprioritynotificationsregcontent = @"
Windows Registry Editor Version 5.00

; disable set priority notifications
"@
        $disableprioritynotificationsguid = Get-ChildItem "HKCU:\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current" -ErrorAction SilentlyContinue |
            Where-Object { $_.PSChildName -match '^\{[a-f0-9-]+\}\$' } |
            ForEach-Object { ($_.PSChildName -split '\$')[0] } |
            Select-Object -Unique
        foreach ($guid in $disableprioritynotificationsguid) {
            $disableprioritynotificationsregcontent += "`n`n[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\$guid`$windows.data.donotdisturb.quiethoursprofile`$quiethoursprofilelist\windows.data.donotdisturb.quiethoursprofile`$microsoft.quiethoursprofile.priorityonly]`n"
            $disableprioritynotificationsregcontent += '"Data"=hex(3):43,42,01,00,0A,02,01,00,2A,06,DF,B8,B4,CC,06,2A,2B,0E,D0,03,\' + "`n"
            $disableprioritynotificationsregcontent += '  43,42,01,00,C2,0A,01,CD,14,06,02,05,00,00,01,01,02,00,03,01,04,00,CC,32,12,\' + "`n"
            $disableprioritynotificationsregcontent += '  05,28,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,53,00,63,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,72,00,65,00,65,00,6E,00,53,00,6B,00,65,00,74,00,63,00,68,00,5F,00,38,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  77,00,65,00,6B,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,00,65,00,21,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,41,00,70,00,70,00,29,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,2E,00,57,00,69,00,6E,00,64,00,6F,00,77,00,73,00,41,00,6C,00,61,00,72,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  6D,00,73,00,5F,00,38,00,77,00,65,00,6B,00,79,00,62,00,33,00,64,00,38,00,62,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,62,00,77,00,65,00,21,00,41,00,70,00,70,00,31,4D,00,69,00,63,00,72,00,6F,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,73,00,6F,00,66,00,74,00,2E,00,58,00,62,00,6F,00,78,00,41,00,70,00,70,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  5F,00,38,00,77,00,65,00,6B,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,65,00,21,00,4D,00,69,00,63,00,72,00,6F,00,73,00,6F,00,66,00,74,00,2E,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  58,00,62,00,6F,00,78,00,41,00,70,00,70,00,2D,4D,00,69,00,63,00,72,00,6F,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  73,00,6F,00,66,00,74,00,2E,00,58,00,62,00,6F,00,78,00,47,00,61,00,6D,00,69,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,6E,00,67,00,4F,00,76,00,65,00,72,00,6C,00,61,00,79,00,5F,00,38,00,77,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  65,00,6B,00,79,00,62,00,33,00,64,00,38,00,62,00,62,00,77,00,65,00,21,00,41,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,70,00,70,00,29,57,00,69,00,6E,00,64,00,6F,00,77,00,73,00,2E,00,53,00,79,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,73,00,74,00,65,00,6D,00,2E,00,4E,00,65,00,61,00,72,00,53,00,68,00,61,00,\' + "`n"
            $disableprioritynotificationsregcontent += '  72,00,65,00,45,00,78,00,70,00,65,00,72,00,69,00,65,00,6E,00,63,00,65,00,52,\' + "`n"
            $disableprioritynotificationsregcontent += '  00,65,00,63,00,65,00,69,00,76,00,65,00,00,00,00,00'
        }
        $disableprioritynotificationsregfile = "$env:SystemRoot\Temp\disablesetprioritynotifications.reg"
        $disableprioritynotificationsregcontent | Out-File -FilePath $disableprioritynotificationsregfile -Encoding ASCII
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$disableprioritynotificationsregfile`"" -WindowStyle Hidden
        # disable app actions
        $stop = "AppActions", "CrossDeviceResume", "DesktopStickerEditorWin32Exe", "DiscoveryHubApp", "FESearchHost", "SearchHost", "SoftLandingTask", "TextInputHost", "VisualAssistExe", "WebExperienceHostApp", "WindowsBackupClient", "WindowsMigration"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Write-Asset "appactions" "$env:SystemRoot\Temp\appactions.reg"
        $settingsdat = "$env:LOCALAPPDATA\Packages\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\Settings\settings.dat"
        $regfileappactions = "$env:SystemRoot\Temp\appactions.reg"
        reg load "HKLM\Settings" $settingsdat >$null 2>&1
        if ($LASTEXITCODE -eq 0) {
            reg import $regfileappactions >$null 2>&1
            [gc]::Collect()
            Start-Sleep -Seconds 2
            reg unload "HKLM\Settings" >$null 2>&1
        }
    }
}
function Invoke-BtnCPDefault {
    Invoke-RunInBackground -StatusStart "Restoring Control Panel defaults..." -StatusDone "Control Panel defaults restored. Log out to apply fully." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        function Run-Trusted([String]$command) {
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
            $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
            $DefaultBinPath = $service.PathName
            $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
            if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
            $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
            $base64Command = [Convert]::ToBase64String($bytes)
            sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
            sc.exe start TrustedInstaller | Out-Null
            sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
        }
        Stop-Service -Name 'camsvc' -Force -ErrorAction SilentlyContinue
        $capabilityconsentstoragedb = "Remove-item `"$env:ProgramData\Microsoft\Windows\CapabilityAccessManager\CapabilityConsentStorage.db*`" -Force"
        Run-Trusted -command $capabilityconsentstoragedb
        cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\CDPUserSvc`" /v `"Start`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
        Write-Asset "registrydefaults" "$env:SystemRoot\Temp\registrydefaults.reg"
        Regedit.exe /S "$env:SystemRoot\Temp\registrydefaults.reg"
        Stop-Service -Name 'camsvc' -Force -ErrorAction SilentlyContinue
        Run-Trusted -command $capabilityconsentstoragedb
        Get-ScheduledTask | Where-Object { $_.TaskName -match 'ScheduledDefrag' } | Enable-ScheduledTask | Out-Null
        powercfg /setdcvalueindex scheme_current sub_none consolelock 1 2>$null
        powercfg /setacvalueindex scheme_current sub_none consolelock 1 2>$null
        cmd /c "reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current /f >nul 2>&1"
        $stop = "AppActions", "CrossDeviceResume", "DesktopStickerEditorWin32Exe", "DiscoveryHubApp", "FESearchHost", "SearchHost", "SoftLandingTask", "TextInputHost", "VisualAssistExe", "WebExperienceHostApp", "WindowsBackupClient", "WindowsMigration"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Remove-Item "$env:LOCALAPPDATA\Packages\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\Settings\settings.dat" -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

# ── Windows: Bloatware Checks ─────────────────────────────────────────────────
function Invoke-BtnBloatwareLegacyCheck     { Start-Process "$env:SystemDrive\Windows\system32\appwiz.cpl" }
function Invoke-BtnBloatwareLegacyFeatCheck { Start-Process "$env:SystemDrive\Windows\system32\optionalfeatures.exe" }
function Invoke-BtnBloatwareUWPFeatCheck    { Start-Process "ms-settings:optionalfeatures" }
function Invoke-BtnBloatwareTaskmgr         { Start-Process "taskmgr" }

# ── Installers: GPU Tools (1:1 with Ultimate) ────────────────────────────────
# MSI Afterburner profile setup (1:1 with "4 Installers/2 MSI Afterburner.ps1"):
# winget install, then write the baked profile configs into Program Files locations.
function Invoke-BtnInstAfterburner {
    Invoke-RunInBackground -StatusStart "Installing MSI Afterburner & applying profile..." -StatusDone "MSI Afterburner installed & configured." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        Start-Process "winget" -ArgumentList "install `"Guru3D.Afterburner.Beta`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden
        $dir = "$env:SystemDrive\Program Files (x86)"
        New-Item -Path "$dir\MSI Afterburner" -Name "Profiles" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "msiafterburnercfg" "$dir\MSI Afterburner\Profiles\MSIAfterburner.cfg"
        New-Item -Path "$dir\RivaTuner Statistics Server" -Name "Profiles" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        Write-Asset "msiab_config" "$dir\RivaTuner Statistics Server\Profiles\Config"
        Write-Asset "msiab_global" "$dir\RivaTuner Statistics Server\Profiles\Global"
        Write-Asset "msiab_overlayeditorcfg" "$dir\RivaTuner Statistics Server\Plugins\Client\OverlayEditor.cfg"
        Write-Asset "msiab_hotkeyhandlercfg" "$dir\RivaTuner Statistics Server\Plugins\Client\HotkeyHandler.cfg"
        Write-Asset "msiab_fr33thyovl" "$dir\RivaTuner Statistics Server\Plugins\Client\Overlays\fr33thy.ovl"
        Write-Asset "msiab_desktopoverlayhostcfg" "$dir\RivaTuner Statistics Server\DesktopOverlayHost.cfg"
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\MSI Afterburner\MSI Afterburner.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\MSI Afterburner" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Move-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\RivaTuner Statistics Server\RivaTuner Statistics Server.lnk" -Destination "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:AppData\Microsoft\Windows\Start Menu\Programs\RivaTuner Statistics Server" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

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

# ── Advanced: ReBar additional options (1:1 with "8 Advanced/7 ReBar Force.ps1") ─
function Invoke-BtnReBarOff {
    Invoke-RunInBackground -StatusStart "ReBar: applying FORCE OFF profile..." -StatusDone "ReBar force-off applied." -ScriptBlock {
        function Write-Asset([string]$n, [string]$d) {
            $t = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($sync.assets.$n))
            [IO.File]::WriteAllText($d, $t.TrimStart([char]0xFEFF), (New-Object Text.UTF8Encoding($false)))
        }
        $progresspreference = 'silentlycontinue'
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch {}
        try { Start-Process "winget" -ArgumentList "install `"Orbmu2k.nvidiaProfileInspector`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
        $base = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Orbmu2k.nvidiaProfileInspector_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $sh = New-Object -ComObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $sc = $sh.CreateShortcut("$Desktop\Nvidia Profile Inspector.lnk"); $sc.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc.WorkingDirectory = $base; $sc.Save()
        $sc2 = $sh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Nvidia Profile Inspector.lnk"); $sc2.TargetPath = "$base\nvidiaProfileInspector.exe"; $sc2.WorkingDirectory = $base; $sc2.Save()
        Get-ChildItem -Path "C:\ProgramData\NVIDIA Corporation\Drs" -Recurse | Unblock-File
        Write-Asset "inspector_forceoff" "$env:SystemRoot\Temp\forceoff.nip"
        Start-Process -Wait "$base\nvidiaProfileInspector.exe" -ArgumentList "-silentImport -mergeImport -silent $env:SystemRoot\Temp\forceoff.nip"
        Start-Process "$base\nvidiaProfileInspector.exe"
    }
}
function Invoke-BtnReBarToBios {
    Invoke-RunInBackground -StatusStart "Restarting to BIOS..." -StatusDone "Restarting to BIOS." -ScriptBlock {
        cmd /c "C:\Windows\System32\shutdown.exe /r /fw /t 0"
    }
}

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
    Invoke-ConsoleScript -Asset "smtht" -Status "SMT/HT tool opened (menu-driven console)."
}

function Invoke-BtnCore1Thread1 {
    Invoke-ConsoleScript -Asset "core1thread1" -Status "Core 1 Thread 1 tool opened (menu-driven console)."
}

function Invoke-BtnPriority {
    Invoke-ConsoleScript -Asset "priority" -Status "Priority tool opened (menu-driven console)."
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

# BitLocker
function Invoke-BtnBitlockerOff {
    Invoke-RunInBackground -StatusStart "Disabling BitLocker..." -StatusDone "BitLocker disabled." -ScriptBlock {
        try {
            Get-BitLockerVolume | Where-Object { $_.ProtectionStatus -eq "On" -or $_.VolumeStatus -ne "FullyDecrypted" } | ForEach-Object {
                Disable-BitLocker -MountPoint $_.MountPoint -ErrorAction SilentlyContinue | Out-Null
            }
        } catch { }
        Start-Process control.exe -ArgumentList "/name microsoft.bitlockerdriveencryption"
    }
}
function Invoke-BtnBitlockerOn { Start-Process control.exe -ArgumentList "/name microsoft.bitlockerdriveencryption" }

# Memory Compression (Off/On delegate; Check is a read-only status display)
function Invoke-BtnMemCompOff {
    Invoke-RunInBackground -StatusStart "Disabling memory compression..." -StatusDone "Memory compression disabled." -ScriptBlock {
        Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue | Out-Null
    }
}
function Invoke-BtnMemCompOn {
    Invoke-RunInBackground -StatusStart "Enabling memory compression..." -StatusDone "Memory compression enabled." -ScriptBlock {
        Enable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue | Out-Null
    }
}
function Invoke-BtnMemCompCheck {
    $status = Get-MMAgent | Out-String
    [System.Windows.MessageBox]::Show($status,"MMAgent Status","OK","Information")
}

# Convert Home to Pro
function Invoke-BtnConvertToPro {
    Invoke-RunInBackground -StatusStart "Preparing product key..." -StatusDone "Activation opened. Paste key VK7JG-NPHTM-C97JM-9MPGT-3V66T." -ScriptBlock {
        Set-Clipboard -Value "VK7JG-NPHTM-C97JM-9MPGT-3V66T"
        Start-Process ms-settings:activation
        & "$env:windir\System32\SystemSettingsAdminFlows.exe" 'EnterProductKey'
    }
}

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

# Edge Settings — Optimize
function Invoke-BtnEdgeOptimize {
    $r = [System.Windows.MessageBox]::Show("Optimize Edge settings? (adds uBlock Origin, disables HW accel/background mode, removes Edge services/tasks)","Edge Optimize","YesNo","Question")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Optimizing Edge..." -StatusDone "Edge optimized." -ScriptBlock {
        # install ublock origin
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist`" /v `"1`" /t REG_SZ /d `"odfafepnkmbhccpbejgmiehpchacaeak;https://edge.microsoft.com/extensionwebstorebase/v1/crx`" /f >nul 2>&1"
        # add edge policies
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Edge`" /v `"HardwareAccelerationModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Edge`" /v `"BackgroundModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Edge`" /v `"StartupBoostEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        # remove logon edge
        $basePath = "HKLM:\Software\Microsoft\Active Setup\Installed Components"
        Get-ChildItem $basePath -ErrorAction SilentlyContinue | ForEach-Object {
            $val = (Get-ItemProperty $_.PsPath)."(default)"
            if ($val -like "*Edge*") { Remove-Item $_.PsPath -Force -ErrorAction SilentlyContinue }
        }
        # remove runonce edge
        $runOncePath = "HKLM:\Software\Microsoft\Windows\CurrentVersion\RunOnce"
        Get-Item $runOncePath -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Property | Where-Object { $_ -like "*msedge*" } | ForEach-Object {
            Remove-ItemProperty -Path $runOncePath -Name $_ -Force -ErrorAction SilentlyContinue
        }
        # remove edge services
        Get-Service | Where-Object { $_.Name -match 'Edge' } | ForEach-Object {
            cmd /c "sc stop `"$($_.Name)`" >nul 2>&1"
            cmd /c "sc delete `"$($_.Name)`" >nul 2>&1"
        }
        # remove edge scheduled tasks
        Get-ScheduledTask | Where-Object { $_.TaskName -like '*Edge*' } | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
        # remove ietoedge bho
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\{1FD49718-1D00-4B19-AF5F-070AF6D5D54C}`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\{1FD49718-1D00-4B19-AF5F-070AF6D5D54C}`" /f >nul 2>&1"
    }
}

# Edge Settings — Default
function Invoke-BtnEdgeDefault {
    $r = [System.Windows.MessageBox]::Show("Restore Edge to default settings?","Edge Default","YesNo","Question")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Restoring Edge defaults..." -StatusDone "Edge restored." -ScriptBlock {
        $progresspreference = 'silentlycontinue'
        cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Edge`" /f >nul 2>&1"
        Stop-Process -Name "msedge" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Start-Process "msedge.exe" -ArgumentList "--restore-last-session --disable-extensions"
        Start-Sleep -Seconds 2
        Stop-Process -Name "msedge" -Force -ErrorAction SilentlyContinue
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.Edge`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch {}
    }
}

# Store Settings — Optimize
function Invoke-BtnStoreOptimize {
    $r = [System.Windows.MessageBox]::Show("Optimize Store settings? (disable app updates, video autoplay, personalized experiences)","Store Optimize","YesNo","Question")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Optimizing Store settings..." -StatusDone "Store optimized." -ScriptBlock {
        try { Start-Process "ms-windows-store:settings" } catch { }
        Start-Sleep -Seconds 5
        $stop = "WinStore.App", "backgroundTaskHost", "StoreDesktopExtension"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate`" /v `"AutoDownload`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
        # create reg file for store settings
        $storesettings = @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Settings\LocalState]
"VideoAutoplay"=hex(5f5e10b):00,96,9d,69,8d,cd,93,dc,01
"EnableAppInstallNotifications"=hex(5f5e10b):00,36,d0,88,8e,cd,93,dc,01

[HKEY_LOCAL_MACHINE\Settings\LocalState\PersistentSettings]
"PersonalizationEnabled"=hex(5f5e10b):00,0d,56,a1,8a,cd,93,dc,01
'@
        Set-Content -Path "$env:SystemRoot\Temp\windowsstore.reg" -Value $storesettings -Force
        $settingsdat = "$env:LocalAppData\Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\Settings\settings.dat"
        $regfilewindowsstore = "$env:SystemRoot\Temp\windowsstore.reg"
        reg load "HKLM\Settings" $settingsdat >$null 2>&1
        if ($LASTEXITCODE -eq 0) {
            reg import $regfilewindowsstore >$null 2>&1
            [gc]::Collect()
            Start-Sleep -Seconds 2
            reg unload "HKLM\Settings" >$null 2>&1
        }
        Start-Sleep -Seconds 2
        Start-Process "ms-windows-store:settings"
    }
}

# Store Settings — Default
function Invoke-BtnStoreDefault {
    $r = [System.Windows.MessageBox]::Show("Restore Store to default settings?","Store Default","YesNo","Question")
    if ($r -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Restoring Store defaults..." -StatusDone "Store restored." -ScriptBlock {
        cmd /c "reg delete HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore /f >nul 2>&1"
        $stop = "WinStore.App", "backgroundTaskHost", "StoreDesktopExtension"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Start-Process "wsreset.exe" -WindowStyle Hidden
        Start-Sleep -Seconds 2
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        Start-Process "ms-windows-store:settings"
    }
}

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

# Windows tweaks run inline (no menu console). Logic mirrors the upstream
# Ultimate scripts; each button applies exactly one option. Pure openers inline.

# Taskbar / Start Menu
function Invoke-BtnTaskbarClean {
    Invoke-RunInBackground -StatusStart "Cleaning Start Menu & Taskbar..." -StatusDone "Start Menu & Taskbar cleaned." -ScriptBlock {
        $reg = @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh]
"AllowNewsAndInterests"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarAl"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search]
"SearchboxTaskbarMode"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowTaskViewButton"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarMn"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowCopilotButton"=dword:00000000

[HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds]
"EnableFeeds"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer]
"HideSCAMeetNow"=dword:00000001

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run]
"SecurityHealth"=hex:07,00,00,00,05,db,8a,69,8a,49,d9,01

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"EnableAutoTray"=dword:00000000

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start]
"HideRecommendedSection"=dword:00000001

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education]
"IsEducationEnvironment"=dword:00000001

[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"HideRecommendedSection"=dword:00000001

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=dword:00000002

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000002
'@
        Set-Content -Path "$env:SystemRoot\Temp\taskbarclean.reg" -Value $reg -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\taskbarclean.reg`"" -WindowStyle Hidden

        cmd /c "reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Taskband /f >nul 2>&1"
        Remove-Item -Recurse -Force "$env:USERPROFILE\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch" -ErrorAction SilentlyContinue | Out-Null

        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 1 -Force }
        }

        $folders = @(
            "$env:USERPROFILE\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Accessibility",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Accessibility",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Accessories"
        )
        foreach ($folder in $folders) {
            if (Test-Path $folder) {
                cmd /c "attrib +h `"$folder`" >nul 2>&1"
                cmd /c "attrib +h `"$folder\*.*`" /s /d >nul 2>&1"
            }
        }

        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\StartMenuLayout.xml" -ErrorAction SilentlyContinue | Out-Null
        $xml = @'
<LayoutModificationTemplate xmlns:defaultlayout="http://schemas.microsoft.com/Start/2014/FullDefaultLayout" xmlns:start="http://schemas.microsoft.com/Start/2014/StartLayout" Version="1" xmlns:taskbar="http://schemas.microsoft.com/Start/2014/TaskbarLayout" xmlns="http://schemas.microsoft.com/Start/2014/LayoutModification">
    <LayoutOptions StartTileGroupCellWidth="6" />
    <DefaultLayoutOverride>
        <StartLayoutCollection>
            <defaultlayout:StartLayout GroupCellWidth="6" />
        </StartLayoutCollection>
    </DefaultLayoutOverride>
</LayoutModificationTemplate>
'@
        Set-Content -Path "C:\Windows\StartMenuLayout.xml" -Value $xml -Force -Encoding ASCII

        $layoutFile = "C:\Windows\StartMenuLayout.xml"
        $regAliases = @("HKLM", "HKCU")
        foreach ($regAlias in $regAliases) {
            $basePath = $regAlias + ":\SOFTWARE\Policies\Microsoft\Windows"
            $keyPath = $basePath + "\Explorer"
            IF(!(Test-Path -Path $keyPath)) { New-Item -Path $basePath -Name "Explorer" | Out-Null }
            Set-ItemProperty -Path $keyPath -Name "LockedStartLayout" -Value 1 | Out-Null
            Set-ItemProperty -Path $keyPath -Name "StartLayoutFile" -Value $layoutFile | Out-Null
        }

        Stop-Process -Force -Name explorer -ErrorAction SilentlyContinue | Out-Null
        Start-Sleep -Seconds 5

        foreach ($regAlias in $regAliases) {
            $basePath = $regAlias + ":\SOFTWARE\Policies\Microsoft\Windows"
            $keyPath = $basePath + "\Explorer"
            Set-ItemProperty -Path $keyPath -Name "LockedStartLayout" -Value 0
        }

        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\StartMenuLayout.xml" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:USERPROFILE\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin" -ErrorAction SilentlyContinue | Out-Null
        $start2 = '-----BEGIN CERTIFICATE-----
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
TSz1QZdkum5Y3uC7wc7paXh1rm11nwluCC7jiA==
-----END CERTIFICATE-----
'
        New-Item "$env:SystemRoot\Temp\start2.txt" -Value $start2 -Force -ErrorAction SilentlyContinue | Out-Null
        certutil.exe -decode "$env:SystemRoot\Temp\start2.txt" "$env:SystemRoot\Temp\start2.bin" >$null
        Copy-Item "$env:SystemRoot\Temp\start2.bin" -Destination "$env:USERPROFILE\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState" -Force -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\Start`" /v `"AllAppsViewMode`" /t REG_DWORD /d `"2`" /f >nul 2>&1"
        Stop-Process -Force -Name explorer -ErrorAction SilentlyContinue | Out-Null
    }
}
function Invoke-BtnTaskbarDefault {
    Invoke-RunInBackground -StatusStart "Restoring taskbar defaults..." -StatusDone "Taskbar defaults restored." -ScriptBlock {
        $reg = @'
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Taskband\AuxilliaryPins]
"MailPin"=dword:00000001

[-HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Dsh]

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarAl"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search]
"SearchboxTaskbarMode"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowTaskViewButton"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"TaskbarMn"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowCopilotButton"=-

[-HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\Windows Feeds]

[-HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer]

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run]
"SecurityHealth"=hex:04,00,00,00,00,00,00,00,00,00,00,00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer]
"EnableAutoTray"=-

[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Start]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PolicyManager\current\device\Education]

[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer]
"HideRecommendedSection"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000000
'@
        Set-Content -Path "$env:SystemRoot\Temp\taskbardefault.reg" -Value $reg -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\taskbardefault.reg`"" -WindowStyle Hidden

        $notifyiconsettings = Get-ChildItem -Path 'registry::HKEY_CURRENT_USER\Control Panel\NotifyIconSettings' -Recurse -Force
        foreach ($setreg in $notifyiconsettings) {
            if ((Get-ItemProperty -Path "registry::$setreg").IsPromoted -eq 0) { }
            else { Set-ItemProperty -Path "registry::$setreg" -Name 'IsPromoted' -Value 0 -Force }
        }

        $folders = @(
            "$env:USERPROFILE\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Accessibility",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Accessibility",
            "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Accessories"
        )
        foreach ($folder in $folders) {
            if (Test-Path $folder) {
                cmd /c "attrib -h `"$folder`" >nul 2>&1"
                cmd /c "attrib -h `"$folder\*.*`" /s /d >nul 2>&1"
            }
        }

        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\StartMenuLayout.xml" -ErrorAction SilentlyContinue | Out-Null
        $xml = @'
<LayoutModificationTemplate xmlns:defaultlayout="http://schemas.microsoft.com/Start/2014/FullDefaultLayout" xmlns:start="http://schemas.microsoft.com/Start/2014/StartLayout" Version="1" xmlns="http://schemas.microsoft.com/Start/2014/LayoutModification">
  <LayoutOptions StartTileGroupCellWidth="6" />
  <DefaultLayoutOverride>
    <StartLayoutCollection>
      <defaultlayout:StartLayout GroupCellWidth="6">
        <start:Group Name="Productivity">
          <start:Folder Name="" Size="2x2" Column="2" Row="0">
            <start:Tile Size="2x2" Column="4" Row="2" AppUserModelID="Microsoft.Office.OneNote_8wekyb3d8bbwe!microsoft.onenoteim" />
            <start:DesktopApplicationTile Size="2x2" Column="0" Row="2" DesktopApplicationLinkPath="%APPDATA%\Microsoft\Windows\Start Menu\Programs\OneDrive.lnk" />
            <start:Tile Size="2x2" Column="0" Row="4" AppUserModelID="Microsoft.SkypeApp_kzf8qxf38zg5c!App" />
          </start:Folder>
          <start:Tile Size="2x2" Column="0" Row="0" AppUserModelID="Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe!Microsoft.MicrosoftOfficeHub" />
          <start:DesktopApplicationTile Size="2x2" Column="0" Row="2" DesktopApplicationLinkPath="%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs\Microsoft Edge.lnk" />
          <start:Tile Size="2x2" Column="4" Row="2" AppUserModelID="7EE7776C.LinkedInforWindows_w1wdnht996qgy!App" />
          <start:Tile Size="2x2" Column="4" Row="0" AppUserModelID="microsoft.windowscommunicationsapps_8wekyb3d8bbwe!Microsoft.WindowsLive.Mail" />
          <start:Tile Size="2x2" Column="2" Row="2" AppUserModelID="Microsoft.Windows.Photos_8wekyb3d8bbwe!App" />
        </start:Group>
        <start:Group Name="Explore">
          <start:Folder Name="Play" Size="2x2" Column="4" Row="2">
            <start:Tile Size="2x2" Column="2" Row="0" AppUserModelID="Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" />
            <start:Tile Size="2x2" Column="0" Row="0" AppUserModelID="Clipchamp.Clipchamp_yxz26nhyzhsrt!App" />
          </start:Folder>
          <start:Tile Size="2x2" Column="4" Row="0" AppUserModelID="Microsoft.Todos_8wekyb3d8bbwe!App" />
          <start:Tile Size="2x2" Column="2" Row="2" AppUserModelID="Microsoft.MicrosoftSolitaireCollection_8wekyb3d8bbwe!App" />
          <start:Tile Size="2x2" Column="2" Row="0" AppUserModelID="SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" />
          <start:Tile Size="2x2" Column="0" Row="2" AppUserModelID="Microsoft.ZuneVideo_8wekyb3d8bbwe!Microsoft.ZuneVideo" />
          <start:Tile Size="2x2" Column="0" Row="0" AppUserModelID="Microsoft.WindowsStore_8wekyb3d8bbwe!App" />
        </start:Group>
      </defaultlayout:StartLayout>
    </StartLayoutCollection>
  </DefaultLayoutOverride>
</LayoutModificationTemplate>
'@
        Set-Content -Path "C:\Windows\StartMenuLayout.xml" -Value $xml -Force -Encoding ASCII

        $layoutFile = "C:\Windows\StartMenuLayout.xml"
        $regAliases = @("HKLM", "HKCU")
        foreach ($regAlias in $regAliases) {
            $basePath = $regAlias + ":\SOFTWARE\Policies\Microsoft\Windows"
            $keyPath = $basePath + "\Explorer"
            IF(!(Test-Path -Path $keyPath)) { New-Item -Path $basePath -Name "Explorer" | Out-Null }
            Set-ItemProperty -Path $keyPath -Name "LockedStartLayout" -Value 1 | Out-Null
            Set-ItemProperty -Path $keyPath -Name "StartLayoutFile" -Value $layoutFile | Out-Null
        }

        Stop-Process -Force -Name explorer -ErrorAction SilentlyContinue | Out-Null
        Start-Sleep -Seconds 5

        foreach ($regAlias in $regAliases) {
            $basePath = $regAlias + ":\SOFTWARE\Policies\Microsoft\Windows"
            $keyPath = $basePath + "\Explorer"
            Set-ItemProperty -Path $keyPath -Name "LockedStartLayout" -Value 0
        }

        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\StartMenuLayout.xml" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:USERPROFILE\AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin" -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\Start`" /v `"AllAppsViewMode`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Stop-Process -Force -Name explorer -ErrorAction SilentlyContinue | Out-Null
    }
}

# Start Menu Layout
function Invoke-BtnStartMenu25H2 {
    Invoke-RunInBackground -StatusStart "Applying 25H2 Start Menu layout..." -StatusDone "25H2 Start Menu layout applied." -ScriptBlock {
        Set-Content -Path "$env:SystemRoot\Temp\newstartmenu.reg" -Value @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=dword:00000002

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000002
'@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\newstartmenu.reg`"" -WindowStyle Hidden
    }
}
function Invoke-BtnStartMenu24H2 {
    Invoke-RunInBackground -StatusStart "Applying 24H2 Start Menu layout..." -StatusDone "24H2 Start Menu layout applied." -ScriptBlock {
        Set-Content -Path "$env:SystemRoot\Temp\oldstartmenu.reg" -Value @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=-

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000000
'@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\oldstartmenu.reg`"" -WindowStyle Hidden
    }
}

# Start Menu Shortcuts
function Invoke-BtnStartShortcuts {
    Invoke-RunInBackground -StatusStart "Creating Start Menu & Startup shortcuts..." -StatusDone "Shortcuts created." -ScriptBlock {
        $Wsh = New-Object -comObject WScript.Shell
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Start Menu Shortcuts 1.lnk")
        $s.TargetPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
        $s.Save()
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Start Menu Shortcuts 2.lnk")
        $s.TargetPath = "$env:AppData\Microsoft\Windows\Start Menu\Programs"
        $s.Save()
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup Programs 1.lnk")
        $s.TargetPath = "$env:AppData\Microsoft\Windows\Start Menu\Programs\Startup"
        $s.Save()
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup Programs 2.lnk")
        $s.TargetPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp"
        $s.Save()
        $s = $Wsh.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Recycle Bin.lnk")
        $s.TargetPath = '::{645ff040-5081-101b-9f08-00aa002f954e}'
        $s.Save()
        Start-Process "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
        Start-Process "$env:AppData\Microsoft\Windows\Start Menu\Programs"
    }
}

# Context Menu
function Invoke-BtnContextClean {
    Invoke-RunInBackground -StatusStart "Cleaning context menu..." -StatusDone "Context menu cleaned." -ScriptBlock {
        cmd /c "reg add `"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32`" /ve /t REG_SZ /d `"`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer`" /v `"NoCustomizeThisFolder`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\Folder\shell\pintohome`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\*\shell\pintohomefile`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\exefile\shellex\ContextMenuHandlers\Compatibility`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /v `"{9F156763-7844-4DC4-B2B1-901F640F5155}`" /t REG_SZ /d `"`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /v `"{09A47860-11B0-4DA5-AFA5-26D86198A780}`" /t REG_SZ /d `"`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /v `"{f81e9010-6ea4-11ce-a7ff-00aa003ca9f6}`" /t REG_SZ /d `"`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\Folder\ShellEx\ContextMenuHandlers\Library Location`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\ModernSharing`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"NoPreviousVersionsPage`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCR\UserLibraryFolder\shellex\ContextMenuHandlers\SendTo`" /f >nul 2>&1"
    }
}
function Invoke-BtnContextDefault {
    Invoke-RunInBackground -StatusStart "Restoring context menu defaults..." -StatusDone "Context menu restored." -ScriptBlock {
        cmd /c "reg delete `"HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer`" /v `"NoCustomizeThisFolder`" /f >nul 2>&1"
        $reg = @"
Windows Registry Editor Version 5.00

[HKEY_CLASSES_ROOT\Folder\shell\pintohome]
"AppliesTo"="System.ParsingName:<>\"::{f874310e-b6b7-47dc-bc84-b9e6b38f5903}\" AND System.ParsingName:<>\"::{679f85cb-0220-4080-b29b-5540cc05aab6}\" AND System.IsFolder:=System.StructuredQueryType.Boolean#True"
"CommandStateHandler"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"CommandStateSync"=""
"MUIVerb"="@shell32.dll,-51601"
"SkipCloudDownload"=dword:00000000

[HKEY_CLASSES_ROOT\Folder\shell\pintohome\command]
"DelegateExecute"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"

[HKEY_CLASSES_ROOT\*\shell\pintohomefile]
"CommandStateHandler"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"CommandStateSync"=""
"MUIVerb"="@shell32.dll,-51608"
"NeverDefault"=""
"SkipCloudDownload"=dword:00000000

[HKEY_CLASSES_ROOT\*\shell\pintohomefile\command]
"DelegateExecute"="{b455f46e-e4af-4035-b0a4-cf18d2f6f28e}"
"@
        Set-Content -Path "$env:SystemRoot\Temp\contextmenudefault.reg" -Value $reg -Force
        Regedit.exe /S "$env:SystemRoot\Temp\contextmenudefault.reg"
        cmd /c "reg add `"HKCR\exefile\shellex\ContextMenuHandlers\Compatibility`" /ve /t REG_SZ /d `"{1d27f844-3a1f-4410-85ac-14651078412d}`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`" /f >nul 2>&1"
        cmd /c "reg add `"HKCR\Folder\ShellEx\ContextMenuHandlers\Library Location`" /ve /t REG_SZ /d `"{3dad6c5d-2167-4cae-9914-f99e41c12cfa}`" /f >nul 2>&1"
        cmd /c "reg add `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\ModernSharing`" /ve /t REG_SZ /d `"{e2bf9676-5f8f-435c-97eb-11607a5bedf7}`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"NoPreviousVersionsPage`" /f >nul 2>&1"
        cmd /c "reg add `"HKCR\AllFilesystemObjects\shellex\ContextMenuHandlers\SendTo`" /ve /t REG_SZ /d `"{7BA4C740-9E81-11CF-99D3-00AA004AE837}`" /f >nul 2>&1"
        cmd /c "reg add `"HKCR\UserLibraryFolder\shellex\ContextMenuHandlers\SendTo`" /ve /t REG_SZ /d `"{7BA4C740-9E81-11CF-99D3-00AA004AE837}`" /f >nul 2>&1"
    }
}

# Theme / Black cosmetics
function Invoke-BtnThemeBlack {
    Invoke-RunInBackground -StatusStart "Applying black theme..." -StatusDone "Black theme applied." -ScriptBlock {
        Set-Content -Path "$env:SystemRoot\Temp\blacktheme.reg" -Value @"
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize]
"AppsUseLightTheme"=dword:00000000
"ColorPrevalence"=dword:00000001
"EnableTransparency"=dword:00000000
"SystemUsesLightTheme"=dword:00000000

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize]
"AppsUseLightTheme"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent]
"AccentPalette"=hex:64,64,64,00,6b,6b,6b,00,00,00,00,00,00,00,00,00,00,00,00,\
  00,00,00,00,00,00,00,00,00,00,00,00,00
"StartColorMenu"=dword:00000000
"AccentColorMenu"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM]
"EnableWindowColorization"=dword:00000001
"AccentColor"=dword:ff191919
"ColorizationColor"=dword:c4191919
"ColorizationAfterglow"=dword:c4191919

[HKEY_CURRENT_USER\Control Panel\Colors]
"Background"="0 0 0"
"@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\blacktheme.reg`"" -WindowStyle Hidden
    }
}
function Invoke-BtnWallpaperBlack {
    Invoke-RunInBackground -StatusStart "Making wallpaper / lockscreen black..." -StatusDone "Black wallpaper applied." -ScriptBlock {
        Add-Type -AssemblyName System.Windows.Forms
        $screenWidth = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize.Width
        $screenHeight = [System.Windows.Forms.SystemInformation]::PrimaryMonitorSize.Height
        Add-Type -AssemblyName System.Drawing
        $file = "C:\Windows\Black.jpg"
        $edit = New-Object System.Drawing.Bitmap $screenWidth, $screenHeight
        $graphics = [System.Drawing.Graphics]::FromImage($edit)
        $graphics.FillRectangle([System.Drawing.Brushes]::Black, 0, 0, $edit.Width, $edit.Height)
        $graphics.Dispose()
        $edit.Save($file)
        $edit.Dispose()
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP`" /v `"LockScreenImagePath`" /t REG_SZ /d `"C:\Windows\Black.jpg`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP`" /v `"LockScreenImageStatus`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"Wallpaper`" /t REG_SZ /d `"C:\Windows\Black.jpg`" /f >nul 2>&1"
        rundll32.exe user32.dll, UpdatePerUserSystemParameters
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Windows\System`" /v `"DisableAcrylicBackgroundOnLogon`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}
function Invoke-BtnAccountBlack {
    Invoke-RunInBackground -StatusStart "Making account pictures black..." -StatusDone "Account pictures black." -ScriptBlock {
        if (!(Test-Path "$env:SystemDrive\ProgramData\User Account Pictures")) {
            Copy-Item "$env:SystemDrive\ProgramData\Microsoft\User Account Pictures" -Destination "$env:SystemDrive\ProgramData" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        }
        $accountPicturesPath = "$env:SystemDrive\ProgramData\Microsoft\User Account Pictures"
        $images = Get-ChildItem $accountPicturesPath -Include *.png,*.bmp -Recurse
        Add-Type -AssemblyName System.Drawing
        foreach ($image in $images) {
            try {
                $bitmap = [System.Drawing.Bitmap]::FromFile($image.FullName)
                $width = $bitmap.Width
                $height = $bitmap.Height
                $bitmap.Dispose()
                $newBitmap = New-Object System.Drawing.Bitmap($width, $height)
                $graphics = [System.Drawing.Graphics]::FromImage($newBitmap)
                $graphics.Clear([System.Drawing.Color]::Black)
                $graphics.Dispose()
                $newBitmap.Save($image.FullName)
                $newBitmap.Dispose()
            } catch { }
        }
    }
}

# Widgets
function Invoke-BtnWidgetsOff {
    Invoke-RunInBackground -StatusStart "Disabling Widgets..." -StatusDone "Widgets disabled." -ScriptBlock {
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PolicyManager\default\NewsAndInterests\AllowNewsAndInterests`" /v `"value`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Dsh`" /v `"AllowNewsAndInterests`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        Stop-Process -Force -Name Widgets -ErrorAction SilentlyContinue | Out-Null
        Stop-Process -Force -Name WidgetService -ErrorAction SilentlyContinue | Out-Null
    }
}
function Invoke-BtnWidgetsDefault {
    Invoke-RunInBackground -StatusStart "Restoring Widgets..." -StatusDone "Widgets restored." -ScriptBlock {
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PolicyManager\default\NewsAndInterests\AllowNewsAndInterests`" /v `"value`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Dsh`" /f >nul 2>&1"
    }
}

# Copilot
function Invoke-BtnCopilotOff {
    Invoke-RunInBackground -StatusStart "Disabling Copilot..." -StatusDone "Copilot disabled." -ScriptBlock {
        $stop = "backgroundTaskHost","Copilot","CrossDeviceResume","GameBar","MicrosoftEdgeUpdate","msedge","msedgewebview2","OneDrive","OneDrive.Sync.Service","OneDriveStandaloneUpdater","Resume","RuntimeBroker","Search","SearchHost","Setup","StoreDesktopExtension","WidgetService","Widgets"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Get-Process | Where-Object { $_.ProcessName -like "*edge*" } | Stop-Process -Force -ErrorAction SilentlyContinue
        Get-AppXPackage -AllUsers | Where-Object { $_.Name -like '*Copilot*' } | Remove-AppxPackage -ErrorAction SilentlyContinue
        cmd /c "reg add `"HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot`" /v `"TurnOffWindowsCopilot`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot`" /v `"TurnOffWindowsCopilot`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}
function Invoke-BtnCopilotDefault {
    Invoke-RunInBackground -StatusStart "Restoring Copilot..." -StatusDone "Copilot restored." -ScriptBlock {
        Get-AppXPackage -AllUsers | Where-Object { $_.Name -like '*Copilot*' } | ForEach-Object {
            Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\AppXManifest.xml" -ErrorAction SilentlyContinue
        }
        cmd /c "reg delete `"HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot`" /f >nul 2>&1"
    }
}

# Bloatware (kept as upstream menu — multiple options)
function Invoke-BtnBloatwareRemove { Invoke-ConsoleScript -Asset "bloatware" -Status "Bloatware opened." }
function Invoke-BtnBloatwareCheck  { Start-Process "ms-settings:appsfeatures" }

# Game Bar
function Invoke-BtnGamebarOff {
    Invoke-RunInBackground -StatusStart "Disabling Game Bar / Xbox..." -StatusDone "Game Bar disabled." -ScriptBlock {
        function Run-Trusted([String]$command) {
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
            $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
            $DefaultBinPath = $service.PathName
            $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
            if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
            $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
            $base64Command = [Convert]::ToBase64String($bytes)
            sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
            sc.exe start TrustedInstaller | Out-Null
            sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
        }
        Stop-Process -Force -Name GameBar -ErrorAction SilentlyContinue | Out-Null
        Get-AppXPackage -AllUsers | Where-Object {
            $_.Name -like '*Gaming*' -or $_.Name -like '*Xbox*'
        } | Remove-AppxPackage -ErrorAction SilentlyContinue
        cmd /c "sc stop `"GameInputSvc`" >nul 2>&1"
        $stop = "gamingservices", "gamingservicesnet", "GameInputRedistService"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        $findmicrosoftgameinput = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
        $microsoftgameinput = Get-ItemProperty $findmicrosoftgameinput -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "*Microsoft GameInput*" }
        if ($microsoftgameinput) {
            $guid = $microsoftgameinput.PSChildName
            Start-Process "msiexec.exe" -ArgumentList "/x $guid /qn /norestart" -Wait -NoNewWindow
        }
        cmd /c "sc stop `"GameInputSvc`" >nul 2>&1"
        $stop = "gamingservices", "gamingservicesnet", "GameInputRedistService"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Set-Content -Path "$env:SystemRoot\Temp\gamebaroff.reg" -Value @"
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\System\GameConfigStore]
"GameDVR_Enabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR]
"AppCaptureEnabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"UseNexusForGameBarEnabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"GamepadNexusChordEnabled"=dword:00000000

[HKEY_CLASSES_ROOT\ms-gamebar]
"(Default)"="URL:ms-gamebar"
"URL Protocol"=""
"NoOpenWith"=""

[HKEY_CLASSES_ROOT\ms-gamebar\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"

[HKEY_CLASSES_ROOT\ms-gamebarservices]
"(Default)"="URL:ms-gamebarservices"
"URL Protocol"=""
"NoOpenWith"=""

[HKEY_CLASSES_ROOT\ms-gamebarservices\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"

[HKEY_CLASSES_ROOT\ms-gamingoverlay]
"(Default)"="URL:ms-gamingoverlay"
"URL Protocol"=""
"NoOpenWith"=""

[HKEY_CLASSES_ROOT\ms-gamingoverlay\shell\open\command]
"(Default)"="%SystemRoot%\\System32\\systray.exe"

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter]
"ActivationType"=dword:00000000
"@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\gamebaroff.reg`"" -WindowStyle Hidden
        Run-Trusted -command "reg add `"HKLM\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter`" /v `"ActivationType`" /t REG_DWORD /d `"0`" /f"
    }
}
function Invoke-BtnGamebarDefault {
    Invoke-RunInBackground -StatusStart "Restoring Game Bar / Xbox..." -StatusDone "Game Bar restored." -ScriptBlock {
        function Run-Trusted([String]$command) {
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
            $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
            $DefaultBinPath = $service.PathName
            $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
            if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
            $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
            $base64Command = [Convert]::ToBase64String($bytes)
            sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
            sc.exe start TrustedInstaller | Out-Null
            sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
        }
        Set-Content -Path "$env:SystemRoot\Temp\gamebaron.reg" -Value @"
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\System\GameConfigStore]
"GameDVR_Enabled"=dword:00000000

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR]
"AppCaptureEnabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"UseNexusForGameBarEnabled"=-

[HKEY_CURRENT_USER\Software\Microsoft\GameBar]
"GamepadNexusChordEnabled"=-

[-HKEY_CLASSES_ROOT\ms-gamebar]

[HKEY_CLASSES_ROOT\ms-gamebar]
"URL Protocol"=""
@="URL:ms-gamebar"

[-HKEY_CLASSES_ROOT\ms-gamebar\shell\open\command]

[-HKEY_CLASSES_ROOT\ms-gamebarservices]

[-HKEY_CLASSES_ROOT\ms-gamebarservices\shell\open\command]

[-HKEY_CLASSES_ROOT\ms-gamingoverlay]

[HKEY_CLASSES_ROOT\ms-gamingoverlay]
"URL Protocol"=""
@="URL:ms-gamingoverlay"

[-HKEY_CLASSES_ROOT\ms-gamingoverlay\shell\open\command]

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter]
"ActivationType"=dword:00000001

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\GameInputSvc]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\BcastDVRUserService]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\XboxGipSvc]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\XblAuthManager]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\XblGameSave]
"Start"=dword:00000003

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Services\XboxNetApiSvc]
"Start"=dword:00000003
"@ -Force
        Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\gamebaron.reg`"" -WindowStyle Hidden
        Run-Trusted -command "reg add `"HKLM\SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId\Windows.Gaming.GameBar.PresenceServer.Internal.PresenceWriter`" /v `"ActivationType`" /t REG_DWORD /d `"1`" /f"
        Get-AppXPackage -AllUsers | Where-Object {
            $_.Name -like '*Gaming*' -or $_.Name -like '*Xbox*' -or $_.Name -like '*Store*'
        } | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register -ErrorAction SilentlyContinue "$($_.InstallLocation)\AppXManifest.xml" }
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.EdgeWebView2Runtime`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch { }
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Microsoft.Gaming.GamingServicesRepairTool_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch { }
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.Gaming.GamingServicesRepairTool`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch { }
        Start-Process "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Gaming.GamingServicesRepairTool_Microsoft.Winget.Source_8wekyb3d8bbwe\gamingrepairtool.exe"
    }
}

# Edge & WebView
function Invoke-BtnEdgeUninstall {
    if ([System.Windows.MessageBox]::Show("This will uninstall Microsoft Edge. Continue?", "Akari Tool", "YesNo", "Warning") -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Uninstalling Edge & WebView... this may take a minute." -StatusDone "Edge & WebView uninstalled." -ScriptBlock {
        $reg1 = "$env:SystemRoot\Temp\reg1.exe"
        $Region = Get-ItemPropertyValue 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\DeviceRegion' -Name DeviceRegion -ErrorAction SilentlyContinue
        Copy-Item (Get-Command reg.exe).Source $reg1 -Force -EA 0
        & $reg1 add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\DeviceRegion' /v DeviceRegion /t REG_DWORD /d 244 /f >$null

        $stop = "backgroundTaskHost", "Copilot", "CrossDeviceResume", "GameBar", "MicrosoftEdgeUpdate", "msedge", "msedgewebview2", "OneDrive", "OneDrive.Sync.Service", "OneDriveStandaloneUpdater", "Resume", "RuntimeBroker", "Search", "SearchHost", "Setup", "StoreDesktopExtension", "WidgetService", "Widgets"
        $stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }
        Get-Process | Where-Object { $_.ProcessName -like "*edge*" } | Stop-Process -Force -ErrorAction SilentlyContinue

        $edgeupdate = @(); "LocalApplicationData", "ProgramFilesX86", "ProgramFiles" | ForEach-Object {
            $folder = [Environment]::GetFolderPath($_)
            $edgeupdate += Get-ChildItem "$folder\Microsoft\EdgeUpdate\*.*.*.*\MicrosoftEdgeUpdate.exe" -rec -ea 0
        }
        $REG = "HKCU:\SOFTWARE", "HKLM:\SOFTWARE", "HKCU:\SOFTWARE\Policies", "HKLM:\SOFTWARE\Policies", "HKCU:\SOFTWARE\WOW6432Node", "HKLM:\SOFTWARE\WOW6432Node", "HKCU:\SOFTWARE\WOW6432Node\Policies", "HKLM:\SOFTWARE\WOW6432Node\Policies"
        foreach ($location in $REG) { Remove-Item "$location\Microsoft\EdgeUpdate" -recurse -force -ErrorAction SilentlyContinue }

        foreach ($path in $edgeupdate) {
            if (Test-Path $path) { Start-Process -Wait $path -Args "/unregsvc" | Out-Null }
            do { Start-Sleep 3 } while ((Get-Process -Name "setup", "MicrosoftEdge*" -ErrorAction SilentlyContinue).Path -like "*\Microsoft\Edge*")
            if (Test-Path $path) { Start-Process -Wait $path -Args "/uninstall" | Out-Null }
            do { Start-Sleep 3 } while ((Get-Process -Name "setup", "MicrosoftEdge*" -ErrorAction SilentlyContinue).Path -like "*\Microsoft\Edge*")
        }

        New-Item -Path "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        New-Item -Path "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe" -ItemType File -Name "MicrosoftEdge.exe" -ErrorAction SilentlyContinue | Out-Null

        $regview = [Microsoft.Win32.RegistryView]::Registry32
        $microsoft = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $regview).OpenSubKey("SOFTWARE\Microsoft", $true)
        $uninstallregkey = $microsoft.OpenSubKey("Windows\CurrentVersion\Uninstall\Microsoft Edge")
        try { $uninstallstring = $uninstallregkey.GetValue("UninstallString") + " --force-uninstall" } catch { }

        Start-Process cmd.exe "/c $uninstallstring" -WindowStyle Hidden -Wait
        Remove-Item -Recurse -Force "$env:SystemRoot\SystemApps\Microsoft.MicrosoftEdge_8wekyb3d8bbwe" -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft EdgeWebView`" /f >nul 2>&1"
        $findmicrosoftedge = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
        $microsoftedge = Get-ItemProperty $findmicrosoftedge -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like "*Microsoft Edge*" }
        if ($microsoftedge) {
            $guid = $microsoftedge.PSChildName
            cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$guid`" /f >nul 2>&1"
        }
        Remove-Item -Recurse -Force "$env:SystemDrive\Windows\System32\config\systemprofile\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\Microsoft Edge.lnk" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:SystemDrive\Users\Public\Desktop\Microsoft Edge.lnk" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:SystemDrive\Program Files (x86)\Microsoft" -ErrorAction SilentlyContinue | Out-Null

        $services = Get-Service | Where-Object { $_.Name -match 'Edge' }
        foreach ($service in $services) {
            cmd /c "sc stop `"$($service.Name)`" >nul 2>&1"
            cmd /c "sc delete `"$($service.Name)`" >nul 2>&1"
        }

        $EdgeLegacyPackage = (Get-ChildItem "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages" -ErrorAction SilentlyContinue |
            Where-Object { $_.PSChildName -like "*Microsoft-Windows-Internet-Browser-Package*~~*" }).PSChildName
        if ($EdgeLegacyPackage) {
            $regPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages\$EdgeLegacyPackage"
            cmd /c "reg add `"$($regPath.Replace('HKLM:\', 'HKLM\'))`" /v Visibility /t REG_DWORD /d 1 /f >nul 2>&1"
            cmd /c "reg delete `"$($regPath.Replace('HKLM:\', 'HKLM\'))\Owners`" /va /f >nul 2>&1"
            dism /online /Remove-Package /PackageName:$EdgeLegacyPackage /quiet /norestart 2>$null | Out-Null
        }

        if ($Region) { & $reg1 add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\DeviceRegion' /v DeviceRegion /t REG_DWORD /d $Region /f >$null }
        Remove-Item $reg1 -ErrorAction SilentlyContinue
    }
}
function Invoke-BtnEdgeRestore   { Start-Process "https://www.microsoft.com/en-us/edge/download" }

# Notepad Settings
function Invoke-BtnNotepad {
    Invoke-RunInBackground -StatusStart "Optimizing Notepad..." -StatusDone "Notepad optimized." -ScriptBlock {
        Stop-Process -Name "Notepad" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Set-Content -Path "$env:SystemRoot\Temp\notepadsettings.reg" -Value @'
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Settings\LocalState]
"OpenFile"=hex(5f5e104):01,00,00,00,d1,55,24,57,d1,84,db,01
"GhostFile"=hex(5f5e10b):00,42,60,f1,5a,d1,84,db,01
"RewriteEnabled"=hex(5f5e10b):00,12,4a,7f,5f,d1,84,db,01
'@ -Force
        $SettingsDat = "$env:LocalAppData\Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\Settings\settings.dat"
        $RegFile = "$env:SystemRoot\Temp\notepadsettings.reg"
        reg load "HKLM\Settings" $SettingsDat >$null 2>&1
        if ($LASTEXITCODE -eq 0) {
            reg import $RegFile >$null 2>&1
            [gc]::Collect()
            Start-Sleep -Seconds 2
            reg unload "HKLM\Settings" >$null 2>&1
        }
    }
}

# Device Manager / Network power savings & wake
function Invoke-BtnDevPowerOff {
    Invoke-RunInBackground -StatusStart "Disabling Device Manager power savings..." -StatusDone "Device Manager power savings disabled." -ScriptBlock {
        foreach ($bus in @("ACPI","HID","PCI","USB")) {
            $usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\$bus" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -eq "Device Parameters" }
            foreach ($key in $usbKeys) {
                $regPath = $key.Name
                cmd /c "reg add `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendEnabled`" /t REG_BINARY /d `"00`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"SelectiveSuspendOn`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"WaitWakeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
            }
            $usbWdf = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\$bus" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -eq "WDF" }
            foreach ($key in $usbWdf) {
                $regPath = $key.Name
                cmd /c "reg add `"$regPath`" /v `"IdleInWorkingState`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
            }
        }
    }
}
function Invoke-BtnDevPowerDefault {
    Invoke-RunInBackground -StatusStart "Restoring Device Manager power savings..." -StatusDone "Device Manager power defaults restored." -ScriptBlock {
        foreach ($bus in @("ACPI","HID","PCI","USB")) {
            $usbKeys = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\$bus" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -eq "Device Parameters" }
            foreach ($key in $usbKeys) {
                $regPath = $key.Name
                cmd /c "reg delete `"$regPath`" /v `"EnhancedPowerManagementEnabled`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"SeleactiveSuspendEnabled`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"SelectiveSuspendOn`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"WaitWakeEnabled`" /f >nul 2>&1"
            }
            $usbWdf = Get-ChildItem -Path "HKLM:\SYSTEM\ControlSet001\Enum\$bus" -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -eq "WDF" }
            foreach ($key in $usbWdf) {
                $regPath = $key.Name
                cmd /c "reg delete `"$regPath`" /v `"IdleInWorkingState`" /f >nul 2>&1"
            }
        }
    }
}
function Invoke-BtnNetPowerOff {
    Invoke-RunInBackground -StatusStart "Disabling network adapter power savings..." -StatusDone "Network adapter power savings disabled." -ScriptBlock {
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) {
            if ($key.PSChildName -match '^\d{4}$') {
                $regPath = $key.Name
                cmd /c "reg add `"$regPath`" /v `"PnPCapabilities`" /t REG_DWORD /d `"24`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"AdvancedEEE`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"*EEE`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"EEELinkAdvertisement`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"SipsEnabled`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"ULPMode`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"GigaLite`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"EnableGreenEthernet`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"PowerSavingMode`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"S5WakeOnLan`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"*WakeOnMagicPacket`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"*ModernStandbyWoLMagicPacket`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"*WakeOnPattern`" /t REG_SZ /d `"0`" /f >nul 2>&1"
                cmd /c "reg add `"$regPath`" /v `"WakeOnLink`" /t REG_SZ /d `"0`" /f >nul 2>&1"
            }
        }
    }
}
function Invoke-BtnNetPowerDefault {
    Invoke-RunInBackground -StatusStart "Restoring network adapter power savings..." -StatusDone "Network adapter power defaults restored." -ScriptBlock {
        $basePath = "HKLM:\System\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}"
        $adapterKeys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue
        foreach ($key in $adapterKeys) {
            if ($key.PSChildName -match '^\d{4}$') {
                $regPath = $key.Name
                cmd /c "reg delete `"$regPath`" /v `"PnPCapabilities`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"AdvancedEEE`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"*EEE`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"EEELinkAdvertisement`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"SipsEnabled`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"ULPMode`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"GigaLite`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"EnableGreenEthernet`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"PowerSavingMode`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"S5WakeOnLan`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"*WakeOnMagicPacket`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"*ModernStandbyWoLMagicPacket`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"*WakeOnPattern`" /f >nul 2>&1"
                cmd /c "reg delete `"$regPath`" /v `"WakeOnLink`" /f >nul 2>&1"
            }
        }
    }
}

# Network IPv4 Only
function Invoke-BtnIPv4Only {
    Invoke-RunInBackground -StatusStart "Setting network to IPv4 only..." -StatusDone "Network set to IPv4 only." -ScriptBlock {
        $adapterstodisable = @('ms_lldp', 'ms_lltdio', 'ms_implat', 'ms_rspndr', 'ms_tcpip6', 'ms_server', 'ms_msclient', 'ms_pacer')
        foreach ($adapterbinding in $adapterstodisable) {
            Disable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
        }
        foreach ($adapterbinding in $adapterstodisable) {
            Disable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
        }
    }
}
function Invoke-BtnIPDefault {
    Invoke-RunInBackground -StatusStart "Restoring network bindings..." -StatusDone "Network bindings restored." -ScriptBlock {
        $adapterstoenable = @('ms_lldp', 'ms_lltdio', 'ms_implat', 'ms_tcpip', 'ms_rspndr', 'ms_tcpip6', 'ms_server', 'ms_msclient', 'ms_pacer')
        foreach ($adapterbinding in $adapterstoenable) {
            Enable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
        }
        foreach ($adapterbinding in $adapterstoenable) {
            Enable-NetAdapterBinding -Name "*" -ComponentID $adapterbinding -ErrorAction SilentlyContinue
        }
    }
}

# Write Cache Buffer Flushing
function Invoke-BtnWriteCacheOff {
    Invoke-RunInBackground -StatusStart "Disabling write-cache buffer flushing..." -StatusDone "Write-cache flushing disabled." -ScriptBlock {
        foreach ($bus in @("SCSI","NVME")) {
            $basePath = "HKLM:\SYSTEM\ControlSet001\Enum\$bus"
            Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "Device Parameters" } | ForEach-Object {
                $diskPath = Join-Path $_.PSPath "Disk"
                cmd /c "reg add `"$(($diskPath -replace 'Microsoft.PowerShell.Core\\Registry::',''))`" /v `"CacheIsPowerProtected`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
            }
        }
    }
}
function Invoke-BtnWriteCacheDefault {
    Invoke-RunInBackground -StatusStart "Restoring write-cache buffer flushing..." -StatusDone "Write-cache flushing restored." -ScriptBlock {
        foreach ($bus in @("SCSI","NVME")) {
            $basePath = "HKLM:\SYSTEM\ControlSet001\Enum\$bus"
            Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "Disk" } | ForEach-Object {
                $diskPath = $_.PSPath -replace 'Microsoft.PowerShell.Core\\Registry::', ''
                cmd /c "reg delete `"$diskPath`" /f >nul 2>&1"
            }
        }
    }
}

# Power Plan
function Invoke-BtnPowerPlanOn {
    Invoke-RunInBackground -StatusStart "Applying Ultimate power plan..." -StatusDone "Ultimate power plan applied." -ScriptBlock {
        cmd /c "powercfg /duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 99999999-9999-9999-9999-999999999999 >nul 2>&1"
        cmd /c "powercfg /SETACTIVE 99999999-9999-9999-9999-999999999999 >nul 2>&1"
        $output = powercfg /L
        $powerPlans = @()
        foreach ($line in $output) {
            if ($line -match ':') {
                $parse = $line -split ':'
                $index = $parse[1].Trim().indexof('(')
                $guid = $parse[1].Trim().Substring(0, $index)
                $powerPlans += $guid
            }
        }
        foreach ($plan in $powerPlans) { cmd /c "powercfg /delete $plan 2>nul" | Out-Null }
        powercfg /hibernate off
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabledDefault`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings`" /v `"ShowLockOption`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings`" /v `"ShowSleepOption`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power`" /v `"HiberbootEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling`" /v `"PowerThrottlingOff`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        $P = "99999999-9999-9999-9999-999999999999"
        powercfg /setacvalueindex $P 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000 2>$null
        powercfg /setdcvalueindex $P 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0x00000000 2>$null
        powercfg /setacvalueindex $P 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 001 2>$null
        powercfg /setdcvalueindex $P 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 001 2>$null
        powercfg /setacvalueindex $P 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000 2>$null
        powercfg /setdcvalueindex $P 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 000 2>$null
        powercfg /setacvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000 2>$null
        powercfg /setdcvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0x00000000 2>$null
        powercfg /setacvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000 2>$null
        powercfg /setdcvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 000 2>$null
        powercfg /setacvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000 2>$null
        powercfg /setdcvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0x00000000 2>$null
        powercfg /setacvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000 2>$null
        powercfg /setdcvalueindex $P 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 000 2>$null
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\0853a681-27c8-4100-a2fd-82013e970683`" /v `"Attributes`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        powercfg /setacvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000 2>$null
        powercfg /setdcvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 0853a681-27c8-4100-a2fd-82013e970683 0x00000000 2>$null
        powercfg /setacvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000 2>$null
        powercfg /setdcvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 000 2>$null
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\d4e98f31-5ffe-4ce1-be31-1b38b384c009`" /v `"Attributes`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        powercfg /setacvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000 2>$null
        powercfg /setdcvalueindex $P 2a737441-1930-4402-8d77-b2bebba308a3 d4e98f31-5ffe-4ce1-be31-1b38b384c009 000 2>$null
        powercfg /setacvalueindex $P 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002 2>$null
        powercfg /setdcvalueindex $P 4f971e89-eebd-4455-a8de-9e59040e7347 a7066653-8d6c-40a8-910e-a1f54b84c7e5 002 2>$null
        powercfg /setacvalueindex $P 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000 2>$null
        powercfg /setdcvalueindex $P 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 000 2>$null
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000064 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c 0x00000064 2>$null
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 001 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 001 2>$null
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 0x00000064 2>$null
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583`" /v `"Attributes`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 0cc5b647-c1df-4637-891a-dec35c318583 0x00000064 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 0cc5b647-c1df-4637-891a-dec35c318583 0x00000064 2>$null
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\ea062031-0e34-4ff1-9b6d-eb1059334028`" /v `"Attributes`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        powercfg /setacvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 ea062031-0e34-4ff1-9b6d-eb1059334028 0x00000064 2>$null
        powercfg /setdcvalueindex $P 54533251-82be-4824-96c1-47b60b740d00 ea062031-0e34-4ff1-9b6d-eb1059334028 0x00000064 2>$null
        powercfg /setacvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 600 2>$null
        powercfg /setdcvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 600 2>$null
        powercfg /requestsoverride PROCESS "chrome.exe" DISPLAY SYSTEM AWAYMODE 2>$null
        powercfg /requestsoverride PROCESS "Discord.exe" DISPLAY SYSTEM AWAYMODE 2>$null
        powercfg /setacvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000064 2>$null
        powercfg /setdcvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 aded5e82-b909-4619-9949-f5d71dac0bcb 0x00000064 2>$null
        powercfg /setacvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 f1fbfde2-a960-4165-9f88-50667911ce96 0x00000064 2>$null
        powercfg /setdcvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 f1fbfde2-a960-4165-9f88-50667911ce96 0x00000064 2>$null
        powercfg /setacvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000 2>$null
        powercfg /setdcvalueindex $P 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 000 2>$null
        powercfg /setacvalueindex $P 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001 2>$null
        powercfg /setdcvalueindex $P 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 001 2>$null
        powercfg /setacvalueindex $P 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000 2>$null
        powercfg /setdcvalueindex $P 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 000 2>$null
        powercfg /setacvalueindex $P 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002 2>$null
        powercfg /setdcvalueindex $P 44f3beca-a7c0-460e-9df2-bb8b99e0cba6 3619c3f2-afb2-4afc-b0e9-e7fef372de36 002 2>$null
        powercfg /setacvalueindex $P c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003 2>$null
        powercfg /setdcvalueindex $P c763b4ec-0e50-4b6b-9bed-2b92a6ee884e 7ec1751b-60ed-4588-afb5-9819d3d77d90 003 2>$null
        powercfg /setacvalueindex $P f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001 2>$null
        powercfg /setdcvalueindex $P f693fb01-e858-4f00-b20f-f30e12ac06d6 191f65b5-d45c-4a4f-8aae-1ab8bfd980e6 001 2>$null
        powercfg /setacvalueindex $P e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003 2>$null
        powercfg /setdcvalueindex $P e276e160-7cb0-43c6-b20b-73f5dce39954 a1662ab2-9d34-4e53-ba8b-2639b9e20857 003 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f 000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 637ea02f-bbcb-4015-8e2c-a1c7b9c0b546 000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 8183ba9a-e910-48da-8769-14ae6dc1170a 0x00000000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469 0x00000000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f 9a66d8d7-4ff7-4ef9-b5a2-5a326ca2a469 0x00000000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f bcded951-187b-4d05-bccc-f7e51960c258 000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f d8742dcb-3e6a-4b3c-b3fe-374623cdcf06 000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f d8742dcb-3e6a-4b3c-b3fe-374623cdcf06 000 2>$null
        powercfg /setacvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f f3c5027d-cd16-4930-aa6b-90db844a8f00 0x00000000 2>$null
        powercfg /setdcvalueindex $P e73a048d-bf27-4f12-9731-8b2076e8891f f3c5027d-cd16-4930-aa6b-90db844a8f00 0x00000000 2>$null
        powercfg /setacvalueindex $P de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064 2>$null
        powercfg /setdcvalueindex $P de830923-a562-41af-a086-e3a2c6bad2da 13d09884-f74e-474a-a852-b6bde8ad03a8 0x00000064 2>$null
        powercfg /setacvalueindex $P de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000 2>$null
        powercfg /setdcvalueindex $P de830923-a562-41af-a086-e3a2c6bad2da e69653ca-cf7f-4f05-aa73-cb833fa90ad4 0x00000000 2>$null
    }
}
function Invoke-BtnPowerPlanDefault {
    Invoke-RunInBackground -StatusStart "Restoring default power plans..." -StatusDone "Default power plans restored." -ScriptBlock {
        powercfg -restoredefaultschemes
        powercfg /requestsoverride PROCESS "chrome.exe" 2>$null
        powercfg /requestsoverride PROCESS "Discord.exe" 2>$null
        cmd /c "powercfg /hibernate on >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabled`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabledDefault`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power`" /v `"HiberbootEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\0853a681-27c8-4100-a2fd-82013e970683`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\d4e98f31-5ffe-4ce1-be31-1b38b384c009`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\ea062031-0e34-4ff1-9b6d-eb1059334028`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}

# Timer Resolution
function Invoke-BtnTimerOn {
    Invoke-RunInBackground -StatusStart "Enabling high timer resolution..." -StatusDone "Timer resolution enabled." -ScriptBlock {
        $csfile = @'
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.ComponentModel;
using System.Configuration.Install;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Management;
using System.Threading;
using System.Diagnostics;
[assembly: AssemblyVersion("2.1")]
[assembly: AssemblyProduct("Set Timer Resolution service")]
namespace WindowsService
{
    class WindowsService : ServiceBase
    {
        public WindowsService()
        {
            this.ServiceName = "STR";
            this.EventLog.Log = "Application";
            this.CanStop = true;
            this.CanHandlePowerEvent = false;
            this.CanHandleSessionChangeEvent = false;
            this.CanPauseAndContinue = false;
            this.CanShutdown = false;
        }
        static void Main()
        {
            ServiceBase.Run(new WindowsService());
        }
        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            ReadProcessList();
            NtQueryTimerResolution(out this.MinimumResolution, out this.MaximumResolution, out this.DefaultResolution);
            if(null != this.EventLog)
                try { this.EventLog.WriteEntry(String.Format("Minimum={0}; Maximum={1}; Default={2}; Processes='{3}'", this.MinimumResolution, this.MaximumResolution, this.DefaultResolution, null != this.ProcessesNames ? String.Join("','", this.ProcessesNames) : "")); }
                catch {}
            if(null == this.ProcessesNames)
            {
                SetMaximumResolution();
                return;
            }
            if(0 == this.ProcessesNames.Count)
            {
                return;
            }
            this.ProcessStartDelegate = new OnProcessStart(this.ProcessStarted);
            try
            {
                String query = String.Format("SELECT * FROM __InstanceCreationEvent WITHIN 0.5 WHERE (TargetInstance isa \"Win32_Process\") AND (TargetInstance.Name=\"{0}\")", String.Join("\" OR TargetInstance.Name=\"", this.ProcessesNames));
                this.startWatch = new ManagementEventWatcher(query);
                this.startWatch.EventArrived += this.startWatch_EventArrived;
                this.startWatch.Start();
            }
            catch(Exception ee)
            {
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(ee.ToString(), EventLogEntryType.Error); }
                    catch {}
            }
        }
        protected override void OnStop()
        {
            if(null != this.startWatch)
            {
                this.startWatch.Stop();
            }

            base.OnStop();
        }
        ManagementEventWatcher startWatch;
        void startWatch_EventArrived(object sender, EventArrivedEventArgs e) 
        {
            try
            {
                ManagementBaseObject process = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value;
                UInt32 processId = (UInt32)process.Properties["ProcessId"].Value;
                this.ProcessStartDelegate.BeginInvoke(processId, null, null);
            } 
            catch(Exception ee) 
            {
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(ee.ToString(), EventLogEntryType.Warning); }
                    catch {}

            }
        }
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern Int32 WaitForSingleObject(IntPtr Handle, Int32 Milliseconds);
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern IntPtr OpenProcess(UInt32 DesiredAccess, Int32 InheritHandle, UInt32 ProcessId);
        [DllImport("kernel32.dll", SetLastError=true)]
        static extern Int32 CloseHandle(IntPtr Handle);
        const UInt32 SYNCHRONIZE = 0x00100000;
        delegate void OnProcessStart(UInt32 processId);
        OnProcessStart ProcessStartDelegate = null;
        void ProcessStarted(UInt32 processId)
        {
            SetMaximumResolution();
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                processHandle = OpenProcess(SYNCHRONIZE, 0, processId);
                if(processHandle != IntPtr.Zero)
                    WaitForSingleObject(processHandle, -1);
            } 
            catch(Exception ee) 
            {
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(ee.ToString(), EventLogEntryType.Warning); }
                    catch {}
            }
            finally
            {
                if(processHandle != IntPtr.Zero)
                    CloseHandle(processHandle); 
            }
            SetDefaultResolution();
        }
        List<String> ProcessesNames = null;
        void ReadProcessList()
        {
            String iniFilePath = Assembly.GetExecutingAssembly().Location + ".ini";
            if(File.Exists(iniFilePath))
            {
                this.ProcessesNames = new List<String>();
                String[] iniFileLines = File.ReadAllLines(iniFilePath);
                foreach(var line in iniFileLines)
                {
                    String[] names = line.Split(new char[] {',', ' ', ';'} , StringSplitOptions.RemoveEmptyEntries);
                    foreach(var name in names)
                    {
                        String lwr_name = name.ToLower();
                        if(!lwr_name.EndsWith(".exe"))
                            lwr_name += ".exe";
                        if(!this.ProcessesNames.Contains(lwr_name))
                            this.ProcessesNames.Add(lwr_name);
                    }
                }
            }
        }
        [DllImport("ntdll.dll", SetLastError=true)]
        static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);
        [DllImport("ntdll.dll", SetLastError=true)]
        static extern int NtQueryTimerResolution(out uint MinimumResolution, out uint MaximumResolution, out uint ActualResolution);
        uint DefaultResolution = 0;
        uint MinimumResolution = 0;
        uint MaximumResolution = 0;
        long processCounter = 0;
        void SetMaximumResolution()
        {
            long counter = Interlocked.Increment(ref this.processCounter);
            if(counter <= 1)
            {
                uint actual = 0;
                NtSetTimerResolution(this.MaximumResolution, true, out actual);
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(String.Format("Actual resolution = {0}", actual)); }
                    catch {}
            }
        }
        void SetDefaultResolution()
        {
            long counter = Interlocked.Decrement(ref this.processCounter);
            if(counter < 1)
            {
                uint actual = 0;
                NtSetTimerResolution(this.DefaultResolution, true, out actual);
                if(null != this.EventLog)
                    try { this.EventLog.WriteEntry(String.Format("Actual resolution = {0}", actual)); }
                    catch {}
            }
        }
    }
    [RunInstaller(true)]
    public class WindowsServiceInstaller : Installer
    {
        public WindowsServiceInstaller()
        {
            ServiceProcessInstaller serviceProcessInstaller = 
                               new ServiceProcessInstaller();
            ServiceInstaller serviceInstaller = new ServiceInstaller();
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            serviceProcessInstaller.Username = null;
            serviceProcessInstaller.Password = null;
            serviceInstaller.DisplayName = "Set Timer Resolution Service";
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "STR";
            this.Installers.Add(serviceProcessInstaller);
            this.Installers.Add(serviceInstaller);
        }
    }
}
'@
        Set-Content -Path "$env:SystemDrive\Windows\SetTimerResolutionService.cs" -Value $csfile -Force
        Start-Process -Wait "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" -ArgumentList "-out:C:\Windows\SetTimerResolutionService.exe C:\Windows\SetTimerResolutionService.cs" -WindowStyle Hidden
        Remove-Item "$env:SystemDrive\Windows\SetTimerResolutionService.cs" -ErrorAction SilentlyContinue | Out-Null
        if (Get-Service -Name "Set Timer Resolution Service" -ErrorAction SilentlyContinue) {
            sc.exe delete "Set Timer Resolution Service" | Out-Null
            Start-Sleep -Seconds 2
        }
        New-Service -Name "Set Timer Resolution Service" -BinaryPathName "$env:SystemDrive\Windows\SetTimerResolutionService.exe" -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "Set Timer Resolution Service" -StartupType Auto -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "Set Timer Resolution Service" -Status Running -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel`" /v `"GlobalTimerResolutionRequests`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        Start-Process taskmgr.exe
    }
}
function Invoke-BtnTimerDefault {
    Invoke-RunInBackground -StatusStart "Disabling timer resolution service..." -StatusDone "Timer resolution service removed." -ScriptBlock {
        Set-Service -Name "Set Timer Resolution Service" -StartupType Disabled -ErrorAction SilentlyContinue | Out-Null
        Set-Service -Name "Set Timer Resolution Service" -Status Stopped -ErrorAction SilentlyContinue | Out-Null
        sc.exe delete "Set Timer Resolution Service" | Out-Null
        Remove-Item "$env:SystemDrive\Windows\SetTimerResolutionService.exe" -Force -ErrorAction SilentlyContinue | Out-Null
        cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel`" /v `"GlobalTimerResolutionRequests`" /f >nul 2>&1"
        Start-Process taskmgr.exe
    }
}

# UAC
function Invoke-BtnUacOff {
    Invoke-RunInBackground -StatusStart "Disabling UAC..." -StatusDone "UAC disabled. Restart required." -ScriptBlock {
        cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System`" /v `"EnableLUA`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
    }
}
function Invoke-BtnUacDefault {
    Invoke-RunInBackground -StatusStart "Enabling UAC..." -StatusDone "UAC enabled. Restart required." -ScriptBlock {
        cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System`" /v `"EnableLUA`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
    }
}

# Core Isolation
function Invoke-BtnCoreIsolation {
    Start-Process msinfo32
    Start-Process "windowsdefender://coreisolation/"
}

# Defender Optimize
function Invoke-BtnDefenderOptimize {
    if ([System.Windows.MessageBox]::Show("Restart required: this runs in Safe Mode and reboots your PC. Continue?", "Akari Tool", "YesNo", "Warning") -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Preparing Defender Optimize (will reboot into Safe Mode)..." -StatusDone "Defender Optimize scheduled." -ScriptBlock {
        function Run-Trusted([String]$command) {
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
            $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
            $DefaultBinPath = $service.PathName
            $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
            if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
            $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
            $base64Command = [Convert]::ToBase64String($bytes)
            sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
            sc.exe start TrustedInstaller | Out-Null
            sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
        }
        $job = @'
function Run-Trusted([String]$command) {
try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
catch { taskkill /im trustedinstaller.exe /f >$null }
$service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
$DefaultBinPath = $service.PathName
$trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
$bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
$base64Command = [Convert]::ToBase64String($bytes)
sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
sc.exe start TrustedInstaller | Out-Null
sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
catch { taskkill /im trustedinstaller.exe /f >$null }
}

$windowssecuritysettings = @(
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection`" /v `"DisableRealtimeMonitoring`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection`" /v `"DisableAsyncScanOnOpen`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Spynet`" /v `"SpyNetReporting`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Spynet`" /v `"SubmitSamplesConsent`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features`" /v `"TamperProtection`" /t REG_DWORD /d `"4`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access`" /v `"EnableControlledFolderAccess`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Notifications`" /v `"DisableEnhancedNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"NoActionNotificationDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"SummaryNotificationDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"FilesBlockedNotificationDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableDynamiclockNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableWindowsHelloNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Epoch`" /v `"Epoch`" /t REG_DWORD /d `"1231`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"VerifiedAndReputableTrustModeEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"SmartLockerMode`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"PUAProtection`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\AppID\Configuration\SMARTLOCKER`" /v `"START_PENDING`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\AppID\Configuration\SMARTLOCKER`" /v `"ENABLED`" /t REG_BINARY /d `"0000000000000000`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\CI\Policy`" /v `"VerifiedAndReputablePolicyState`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"SmartScreenEnabled`" /t REG_SZ /d `"Off`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled`" /ve /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"CaptureThreatWindow`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyMalicious`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyPasswordReuse`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyUnsafeApp`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"ServiceEnabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"PUAProtection`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\Session Manager\kernel`" /v `"MitigationOptions`" /t REG_BINARY /d `"222222000002000000020000000000000000000000000000`" /f >nul 2>&1"',
'cmd /c "reg delete `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"ChangedInBootCycle`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"Enabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg delete `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"WasEnabledBy`" /f >nul 2>&1"',
'cmd /c "bcdedit /deletevalue allowedinmemorysettings >nul 2>&1"',
'cmd /c "bcdedit /deletevalue isolatedcontext >nul 2>&1"',
'cmd /c "bcdedit /deletevalue hypervisorlaunchtype >nul 2>&1"',
'cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard`" /v `"EnableVirtualizationBasedSecurity`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa`" /v `"RunAsPPL`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\CI\Config`" /v `"VulnerableDriverBlocklistEnable`" /t REG_DWORD /d `"0`" /f >nul 2>&1"'
)
foreach ($command in $windowssecuritysettings) { Run-Trusted $command }
foreach ($command in $windowssecuritysettings) { Invoke-Expression $command }
cmd /c "bcdedit /deletevalue {current} safeboot >nul 2>&1"
Start-Sleep -Seconds 5
shutdown -r -t 00
'@
        Set-Content -Path "$env:SystemRoot\Temp\defenderoptimize.ps1" -Value $job -Force
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*defenderoptimize`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\defenderoptimize.ps1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"0`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
        schtasks /Change /TN "Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cleanup" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan" /Disable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Verification" /Disable 2>$null | Out-Null
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}
function Invoke-BtnDefenderDefault {
    if ([System.Windows.MessageBox]::Show("Restart required: this runs in Safe Mode and reboots your PC. Continue?", "Akari Tool", "YesNo", "Warning") -ne "Yes") { return }
    Invoke-RunInBackground -StatusStart "Preparing Defender Default (will reboot into Safe Mode)..." -StatusDone "Defender Default scheduled." -ScriptBlock {
        $job = @'
function Run-Trusted([String]$command) {
try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
catch { taskkill /im trustedinstaller.exe /f >$null }
$service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
$DefaultBinPath = $service.PathName
$trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
$bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
$base64Command = [Convert]::ToBase64String($bytes)
sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
sc.exe start TrustedInstaller | Out-Null
sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
catch { taskkill /im trustedinstaller.exe /f >$null }
}

$windowssecuritysettings = @(
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection`" /v `"DisableRealtimeMonitoring`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Real-Time Protection`" /v `"DisableAsyncScanOnOpen`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Spynet`" /v `"SpyNetReporting`" /t REG_DWORD /d `"2`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Spynet`" /v `"SubmitSamplesConsent`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features`" /v `"TamperProtection`" /t REG_DWORD /d `"5`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access`" /v `"EnableControlledFolderAccess`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Notifications`" /v `"DisableEnhancedNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"NoActionNotificationDisabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"SummaryNotificationDisabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender Security Center\Virus and threat protection`" /v `"FilesBlockedNotificationDisabled`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableDynamiclockNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows Defender Security Center\Account protection`" /v `"DisableWindowsHelloNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Epoch`" /v `"Epoch`" /t REG_DWORD /d `"1228`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile`" /v `"DisableNotifications`" /t REG_DWORD /d `"0`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"VerifiedAndReputableTrustModeEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"SmartLockerMode`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"PUAProtection`" /t REG_DWORD /d `"2`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\AppID\Configuration\SMARTLOCKER`" /v `"START_PENDING`" /t REG_DWORD /d `"4`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\AppID\Configuration\SMARTLOCKER`" /v `"ENABLED`" /t REG_BINARY /d `"0400000000000000`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\CI\Policy`" /v `"VerifiedAndReputablePolicyState`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer`" /v `"SmartScreenEnabled`" /t REG_SZ /d `"Warn`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenPuaEnabled`" /ve /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"CaptureThreatWindow`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyMalicious`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyPasswordReuse`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"NotifyUnsafeApp`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components`" /v `"ServiceEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender`" /v `"PUAProtection`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\Session Manager\kernel`" /v `"MitigationOptions`" /t REG_BINARY /d `"111111000001000000000000000000000000000000000000`" /f >nul 2>&1"',
'cmd /c "reg delete `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"ChangedInBootCycle`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"Enabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity`" /v `"WasEnabledBy`" /t REG_DWORD /d `"2`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Lsa`" /v `"RunAsPPL`" /t REG_DWORD /d `"2`" /f >nul 2>&1"',
'cmd /c "reg add `"HKEY_LOCAL_MACHINE\System\ControlSet001\Control\CI\Config`" /v `"VulnerableDriverBlocklistEnable`" /t REG_DWORD /d `"1`" /f >nul 2>&1"'
)
foreach ($command in $windowssecuritysettings) { Run-Trusted $command }
foreach ($command in $windowssecuritysettings) { Invoke-Expression $command }
cmd /c "bcdedit /deletevalue {current} safeboot >nul 2>&1"
Start-Sleep -Seconds 5
shutdown -r -t 00
'@
        Set-Content -Path "$env:SystemRoot\Temp\defenderdefault.ps1" -Value $job -Force
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`" /v `"*defenderdefault`" /t REG_SZ /d `"powershell.exe -nop -ep bypass -WindowStyle Maximized -f $env:SystemRoot\Temp\defenderdefault.ps1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Edge\SmartScreenEnabled`" /ve /t REG_DWORD /d `"1`" /f >nul 2>&1"
        cmd /c "reg add `"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost`" /v `"EnableWebContentEvaluation`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
        schtasks /Change /TN "Microsoft\Windows\ExploitGuard\ExploitGuard MDM policy Refresh" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cache Maintenance" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Cleanup" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Scheduled Scan" /Enable 2>$null | Out-Null
        schtasks /Change /TN "Microsoft\Windows\Windows Defender\Windows Defender Verification" /Enable 2>$null | Out-Null
        cmd /c "bcdedit /set {current} safeboot minimal >nul 2>&1"
        Start-Sleep -Seconds 5
        shutdown -r -t 00
    }
}

# Autoruns (Startup Tasks & Apps Check)
function Invoke-BtnAutoruns {
    Invoke-RunInBackground -StatusStart "Running Autoruns startup check..." -StatusDone "Autoruns launched." -ScriptBlock {
        function Run-Trusted([String]$command) {
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
            $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='TrustedInstaller'"
            $DefaultBinPath = $service.PathName
            $trustedInstallerPath = "$env:SystemRoot\servicing\TrustedInstaller.exe"
            if ($DefaultBinPath -ne $trustedInstallerPath) { $DefaultBinPath = $trustedInstallerPath }
            $bytes = [System.Text.Encoding]::Unicode.GetBytes($command)
            $base64Command = [Convert]::ToBase64String($bytes)
            sc.exe config TrustedInstaller binPath= "cmd.exe /c powershell.exe -encodedcommand $base64Command" | Out-Null
            sc.exe start TrustedInstaller | Out-Null
            sc.exe config TrustedInstaller binpath= "`"$DefaultBinPath`"" | Out-Null
            try { Stop-Service -Name TrustedInstaller -Force -ErrorAction Stop -WarningAction Stop }
            catch { taskkill /im trustedinstaller.exe /f >$null }
        }
        try {
            cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore`" /v `"SystemRestorePointCreationFrequency`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
            Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue | Out-Null
            Checkpoint-Computer -Description "beforeautoruns" -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue | Out-Null
            cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore`" /v `"SystemRestorePointCreationFrequency`" /f >nul 2>&1"
        } catch { }
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunNotification`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunNotification`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg delete `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg add `"HKCU\Software\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\Software\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\Software\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce`" /f >nul 2>&1"
        cmd /c "reg delete `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        cmd /c "reg add `"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`" /f >nul 2>&1"
        Remove-Item -Recurse -Force "$env:AppData\Microsoft\Windows\Start Menu\Programs\Startup" -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Recurse -Force "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp" -ErrorAction SilentlyContinue | Out-Null
        New-Item -Path "$env:AppData\Microsoft\Windows\Start Menu\Programs\Startup" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        New-Item -Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp" -ItemType Directory -ErrorAction SilentlyContinue | Out-Null
        $treePath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree"
        Get-ChildItem $treePath | Where-Object { $_.PSChildName -ne "Microsoft" } | ForEach-Object {
            Run-Trusted "Remove-Item '$($_.PSPath)' -Recurse -Force"
        }
        $tasksPath = "$env:SystemRoot\System32\Tasks"
        Get-ChildItem $tasksPath | Where-Object { $_.Name -ne "Microsoft" } | ForEach-Object {
            Remove-Item $_.FullName -Recurse -Force
        }
        try { Start-Process "winget" -ArgumentList "uninstall --product-code Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe --silent" -Wait -WindowStyle Hidden } catch { }
        try { Start-Process "winget" -ArgumentList "install `"Microsoft.Sysinternals.Autoruns`" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --no-upgrade" -Wait -WindowStyle Hidden } catch { }
        $WshShell = New-Object -comObject WScript.Shell
        $Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
        $Shortcut = $WshShell.CreateShortcut("$Desktop\Autoruns.lnk")
        $Shortcut.TargetPath = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe\Autoruns64.exe"
        $Shortcut.WorkingDirectory = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $Shortcut.Save()
        $Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Autoruns.lnk")
        $Shortcut.TargetPath = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe\Autoruns64.exe"
        $Shortcut.WorkingDirectory = "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe"
        $Shortcut.Save()
        Start-Process "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Microsoft.Sysinternals.Autoruns_Microsoft.Winget.Source_8wekyb3d8bbwe\Autoruns64.exe"
    }
}

# Cleanup
function Invoke-BtnCleanup {
    Invoke-RunInBackground -StatusStart "Cleaning temporary files..." -StatusDone "Cleanup done." -ScriptBlock {
        Remove-Item -Path "$env:USERPROFILE\AppData\Local\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item -Path "$env:SystemDrive\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\DumpStack.log" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\Output.txt" -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\PerfLogs" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\Windows.old" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\XboxGames" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Remove-Item "$env:SystemDrive\inetpub" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        cmd /c "sc stop `"wuauserv`" >nul 2>&1"
        Remove-Item "$env:SystemDrive\Windows\SoftwareDistribution\*" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
        Start-Process cleanmgr.exe
    }
}

# Restore Point
function Invoke-BtnRestorePoint {
    Invoke-RunInBackground -StatusStart "Creating restore point..." -StatusDone "Restore point created." -ScriptBlock {
        try {
            cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore`" /v `"SystemRestorePointCreationFrequency`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
            Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue | Out-Null
            Checkpoint-Computer -Description "backup" -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue | Out-Null
        } catch { }
        Start-Process "$env:SystemRoot\system32\control.exe" -ArgumentList "sysdm.cpl,,4"
        Start-Process "rstrui"
    }
}

# Pure openers
function Invoke-BtnControlPanel { Start-Process control.exe }
function Invoke-BtnSound        { Start-Process "mmsys.cpl" }

$sync.assets = @{}
$sync.assets.logo = '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'
$sync.assets.appactions = '77u/V2luZG93cyBSZWdpc3RyeSBFZGl0b3IgVmVyc2lvbiA1LjAwCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNldHRpbmdzXExvY2FsU3RhdGVcRGlzYWJsZWRBcHBzXQoiTWljcm9zb2Z0LlBhaW50Xzh3ZWt5YjNkOGJid2UiPWhleCg1ZjVlMTBiKTowMSw2MSxlZCwxMSwzNCxmNyw5ZixkYywwMQoiTWljcm9zb2Z0LldpbmRvd3MuUGhvdG9zXzh3ZWt5YjNkOGJid2UiPWhleCg1ZjVlMTBiKTowMSw2MSxlZCwxMSwzNCxmNyw5ZixkYywwMQoiTWljcm9zb2Z0V2luZG93cy5DbGllbnQuQ0JTX2N3NW4xaDJ0eHlld3kiPWhleCg1ZjVlMTBiKTowMSw2MSxlZCwxMSwzNCxmNyw5ZixkYywwMQo='
$sync.assets.bloatware = 'ICAgICAgICAjIFNDUklQVCBSVU4gQVMgQURNSU4KICAgICAgICBJZiAoIShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NQcmluY2lwYWxdW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzSWRlbnRpdHldOjpHZXRDdXJyZW50KCkpLklzSW5Sb2xlKFtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0J1aWx0SW5Sb2xlXSJBZG1pbmlzdHJhdG9yIikpCiAgICAgICAge1N0YXJ0LVByb2Nlc3MgUG93ZXJTaGVsbC5leGUgLUFyZ3VtZW50TGlzdCAoIi1Ob1Byb2ZpbGUgLUV4ZWN1dGlvblBvbGljeSBCeXBhc3MgLUZpbGUgYCJ7MH1gIiIgLWYgJFBTQ29tbWFuZFBhdGgpIC1WZXJiIFJ1bkFzCiAgICAgICAgRXhpdH0KICAgICAgICAkSG9zdC5VSS5SYXdVSS5XaW5kb3dUaXRsZSA9ICRteUludm9jYXRpb24uTXlDb21tYW5kLkRlZmluaXRpb24gKyAiIChBZG1pbmlzdHJhdG9yKSIKICAgICAgICAkSG9zdC5VSS5SYXdVSS5CYWNrZ3JvdW5kQ29sb3IgPSAiQmxhY2siCiAgICAgICAgJEhvc3QuUHJpdmF0ZURhdGEuUHJvZ3Jlc3NCYWNrZ3JvdW5kQ29sb3IgPSAiQmxhY2siCiAgICAgICAgJEhvc3QuUHJpdmF0ZURhdGEuUHJvZ3Jlc3NGb3JlZ3JvdW5kQ29sb3IgPSAiV2hpdGUiCiAgICAgICAgQ2xlYXItSG9zdAoKICAgICAgICAjIFNDUklQVCBDSEVDSyBJTlRFUk5FVAogICAgICAgIGlmICghKFRlc3QtQ29ubmVjdGlvbiAtQ29tcHV0ZXJOYW1lICI4LjguOC44IiAtQ291bnQgMSAtUXVpZXQgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUpKSB7CiAgICAgICAgV3JpdGUtSG9zdCAiSW50ZXJuZXQgQ29ubmVjdGlvbiBSZXF1aXJlZGBuIiAtRm9yZWdyb3VuZENvbG9yIFJlZAogICAgICAgIFBhdXNlCiAgICAgICAgZXhpdAogICAgICAgIH0KCiAgICAgICAgIyBTQ1JJUFQgU0lMRU5UCiAgICAgICAgJHByb2dyZXNzcHJlZmVyZW5jZSA9ICdzaWxlbnRseWNvbnRpbnVlJwoKICAgICAgICAjIEFMTE9XIFBBU1NXT1JEIFNJR04gSU4KICAgICAgICBjbWQgL2MgInJlZyBhZGQgYCJIS0xNXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIE5UXEN1cnJlbnRWZXJzaW9uXFBhc3N3b3JkTGVzc1xEZXZpY2VgIiAvdiBgIkRldmljZVBhc3N3b3JkTGVzc0J1aWxkVmVyc2lvbmAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiCgogICAgICAgIGZ1bmN0aW9uIHNob3ctbWVudSB7CgkgICAgQ2xlYXItSG9zdAogICAgICAgIFdyaXRlLUhvc3QgIiAxLiBFeGl0IgoJICAgIFdyaXRlLUhvc3QgIiAyLiBSZW1vdmUgOiBBbGwgQmxvYXR3YXJlIChSZWNvbW1lbmRlZCkiCiAgICAgICAgV3JpdGUtSG9zdCAiIDMuIEluc3RhbGw6IFN0b3JlIgoJICAgIFdyaXRlLUhvc3QgIiA0LiBJbnN0YWxsOiBBbGwgVVdQIEFwcHMiCiAgICAgICAgV3JpdGUtSG9zdCAiIDUuIEluc3RhbGw6IFVXUCBGZWF0dXJlcyIKICAgICAgICBXcml0ZS1Ib3N0ICIgNi4gSW5zdGFsbDogTGVnYWN5IEZlYXR1cmVzIgoJICAgIFdyaXRlLUhvc3QgIiA3LiBJbnN0YWxsOiBPbmUgRHJpdmUiCiAgICAgICAgV3JpdGUtSG9zdCAiIDguIEluc3RhbGw6IFJlbW90ZSBEZXNrdG9wIENvbm5lY3Rpb24iCiAgICAgICAgV3JpdGUtSG9zdCAiIDkuIEluc3RhbGw6IFNuaXBwaW5nIFRvb2xgbiIKCSAgICAJICAgICAgICAgICAgICB9CgkgICAgc2hvdy1tZW51CiAgICAgICAgd2hpbGUgKCR0cnVlKSB7CiAgICAgICAgJGNob2ljZSA9IFJlYWQtSG9zdCAiICIKICAgICAgICBpZiAoJGNob2ljZSAtbWF0Y2ggJ15bMS05XSQnKSB7CiAgICAgICAgc3dpdGNoICgkY2hvaWNlKSB7CiAgICAgICAgMSB7CgpDbGVhci1Ib3N0CgpleGl0CgogICAgICAgICAgfQogICAgICAgIDIgewoKQ2xlYXItSG9zdAoKV3JpdGUtSG9zdCAiVW5pbnN0YWxsaW5nOiBVV1AgQXBwcy4gUGxlYXNlIHdhaXQuLi5gbiIKCkdldC1BcHBYUGFja2FnZSAtQWxsVXNlcnMgfCBXaGVyZS1PYmplY3QgewojIGJyZWFrcyBmaWxlIGV4cGxvcmVyCiRfLk5hbWUgLW5vdGxpa2UgJypDQlMqJyAtYW5kCiRfLk5hbWUgLW5vdGxpa2UgJypNaWNyb3NvZnQuQVYxVmlkZW9FeHRlbnNpb24qJyAtYW5kCiRfLk5hbWUgLW5vdGxpa2UgJypNaWNyb3NvZnQuQVZDRW5jb2RlclZpZGVvRXh0ZW5zaW9uKicgLWFuZAokXy5OYW1lIC1ub3RsaWtlICcqTWljcm9zb2Z0LkhFSUZJbWFnZUV4dGVuc2lvbionIC1hbmQKJF8uTmFtZSAtbm90bGlrZSAnKk1pY3Jvc29mdC5IRVZDVmlkZW9FeHRlbnNpb24qJyAtYW5kCiRfLk5hbWUgLW5vdGxpa2UgJypNaWNyb3NvZnQuTVBFRzJWaWRlb0V4dGVuc2lvbionIC1hbmQKJF8uTmFtZSAtbm90bGlrZSAnKk1pY3Jvc29mdC5QYWludConIC1hbmQKJF8uTmFtZSAtbm90bGlrZSAnKk1pY3Jvc29mdC5SYXdJbWFnZUV4dGVuc2lvbionIC1hbmQKIyBicmVha3Mgd2luZG93cyBzZXJ2ZXIgZGVmZW5kZXIKJF8uTmFtZSAtbm90bGlrZSAnKk1pY3Jvc29mdC5TZWNIZWFsdGhVSSonIC1hbmQKJF8uTmFtZSAtbm90bGlrZSAnKk1pY3Jvc29mdC5WUDlWaWRlb0V4dGVuc2lvbnMqJyAtYW5kCiRfLk5hbWUgLW5vdGxpa2UgJypNaWNyb3NvZnQuV2ViTWVkaWFFeHRlbnNpb25zKicgLWFuZAokXy5OYW1lIC1ub3RsaWtlICcqTWljcm9zb2Z0LldlYnBJbWFnZUV4dGVuc2lvbionIC1hbmQKJF8uTmFtZSAtbm90bGlrZSAnKk1pY3Jvc29mdC5XaW5kb3dzLlBob3RvcyonIC1hbmQKIyBicmVha3Mgd2luZG93cyBzZXJ2ZXIgdGFzayBiYXIKJF8uTmFtZSAtbm90bGlrZSAnKk1pY3Jvc29mdC5XaW5kb3dzLlNoZWxsRXhwZXJpZW5jZUhvc3QqJyAtYW5kCiMgYnJlYWtzIHdpbmRvd3Mgc2VydmVyIHN0YXJ0IG1lbnUKJF8uTmFtZSAtbm90bGlrZSAnKk1pY3Jvc29mdC5XaW5kb3dzLlN0YXJ0TWVudUV4cGVyaWVuY2VIb3N0KicgLWFuZAokXy5OYW1lIC1ub3RsaWtlICcqTWljcm9zb2Z0LldpbmRvd3NOb3RlcGFkKicgLWFuZAokXy5OYW1lIC1ub3RsaWtlICcqTlZJRElBQ29ycC5OVklESUFDb250cm9sUGFuZWwqJyAtYW5kCiMgYnJlYWtzIHdpbmRvd3Mgc2VydmVyIGltbWVyc2l2ZSBjb250cm9sIHBhbmVsCiRfLk5hbWUgLW5vdGxpa2UgJyp3aW5kb3dzLmltbWVyc2l2ZWNvbnRyb2xwYW5lbConCn0gfCBSZW1vdmUtQXBweFBhY2thZ2UgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUKCkNsZWFyLUhvc3QKCldyaXRlLUhvc3QgIlVuaW5zdGFsbGluZzogVVdQIEZlYXR1cmVzLiBQbGVhc2Ugd2FpdC4uLmBuIgoKR2V0LVdpbmRvd3NDYXBhYmlsaXR5IC1PbmxpbmUgfCBXaGVyZS1PYmplY3QgewokXy5OYW1lIC1ub3RsaWtlICcqTWljcm9zb2Z0LldpbmRvd3MuRXRoZXJuZXQqJyAtYW5kCiMgd2luZG93cyAxMAokXy5OYW1lIC1ub3RsaWtlICcqTWljcm9zb2Z0LldpbmRvd3MuTVNQYWludConIC1hbmQKIyB3aW5kb3dzIDEwCiRfLk5hbWUgLW5vdGxpa2UgJypNaWNyb3NvZnQuV2luZG93cy5Ob3RlcGFkKicgLWFuZAokXy5OYW1lIC1ub3RsaWtlICcqTWljcm9zb2Z0LldpbmRvd3MuTm90ZXBhZC5TeXN0ZW0qJyAtYW5kCiRfLk5hbWUgLW5vdGxpa2UgJypNaWNyb3NvZnQuV2luZG93cy5XaWZpKicgLWFuZAokXy5OYW1lIC1ub3RsaWtlICcqTmV0RlgzKicgLWFuZAojIHdpbmRvd3MgMTEgYnJlYWtzIG1zaSBpbnN0YWxsZXJzIGlmIHJlbW92ZWQKJF8uTmFtZSAtbm90bGlrZSAnKlZCU0NSSVBUKicgLWFuZAojIGJyZWFrcyBtb25pdG9yaW5nIHByb2dyYW1zCiRfLk5hbWUgLW5vdGxpa2UgJypXTUlDKicgLWFuZAojIHdpbmRvd3MgMTAgYnJlYWtzIHV3cCBzbmlwcGluZ3Rvb2wgaWYgcmVtb3ZlZAokXy5OYW1lIC1ub3RsaWtlICcqV2luZG93cy5DbGllbnQuU2hlbGxDb21wb25lbnRzKicKfSB8IEZvckVhY2gtT2JqZWN0IHsKdHJ5IHsKUmVtb3ZlLVdpbmRvd3NDYXBhYmlsaXR5IC1PbmxpbmUgLU5hbWUgJF8uTmFtZSB8IE91dC1OdWxsCn0gY2F0Y2ggeyB9Cn0KCkNsZWFyLUhvc3QKCldyaXRlLUhvc3QgIlVuaW5zdGFsbGluZzogTGVnYWN5IEZlYXR1cmVzLiBQbGVhc2Ugd2FpdC4uLmBuIgoKR2V0LVdpbmRvd3NPcHRpb25hbEZlYXR1cmUgLU9ubGluZSB8IFdoZXJlLU9iamVjdCB7CiRfLkZlYXR1cmVOYW1lIC1ub3RsaWtlICcqRGlyZWN0UGxheSonIC1hbmQKJF8uRmVhdHVyZU5hbWUgLW5vdGxpa2UgJypMZWdhY3lDb21wb25lbnRzKicgLWFuZAokXy5GZWF0dXJlTmFtZSAtbm90bGlrZSAnKk5ldEZ4MyonIC1hbmQKIyBicmVha3Mgd2luZG93cyBzZXJ2ZXIgdHVybiB3aW5kb3dzIGZlYXR1cmVzIG9uIG9yIG9mZgokXy5GZWF0dXJlTmFtZSAtbm90bGlrZSAnKk5ldEZ4NConIC1hbmQKJF8uRmVhdHVyZU5hbWUgLW5vdGxpa2UgJypOZXRGeDQtQWR2U3J2cyonIC1hbmQKIyBicmVha3Mgd2luZG93cyBzZXJ2ZXIgdHVybiB3aW5kb3dzIGZlYXR1cmVzIG9uIG9yIG9mZgokXy5GZWF0dXJlTmFtZSAtbm90bGlrZSAnKk5ldEZ4NFNlcnZlckZlYXR1cmVzKicgLWFuZAojIGJyZWFrcyBzZWFyY2gKJF8uRmVhdHVyZU5hbWUgLW5vdGxpa2UgJypTZWFyY2hFbmdpbmUtQ2xpZW50LVBhY2thZ2UqJyAtYW5kCiMgYnJlYWtzIHdpbmRvd3Mgc2VydmVyIGRlc2t0b3AKJF8uRmVhdHVyZU5hbWUgLW5vdGxpa2UgJypTZXJ2ZXItU2hlbGwqJyAtYW5kCiMgYnJlYWtzIHdpbmRvd3Mgc2VydmVyIGRlZmVuZGVyCiRfLkZlYXR1cmVOYW1lIC1ub3RsaWtlICcqV2luZG93cy1EZWZlbmRlcionIC1hbmQKIyBicmVha3Mgd2luZG93cyBzZXJ2ZXIgaW50ZXJuZXQKJF8uRmVhdHVyZU5hbWUgLW5vdGxpa2UgJypTZXJ2ZXItRHJpdmVycy1HZW5lcmFsKicgLWFuZAojIGJyZWFrcyB3aW5kb3dzIHNlcnZlciBpbnRlcm5ldAokXy5GZWF0dXJlTmFtZSAtbm90bGlrZSAnKlNlcnZlckNvcmUtRHJpdmVycy1HZW5lcmFsKicgLWFuZAojIGJyZWFrcyB3aW5kb3dzIHNlcnZlciBpbnRlcm5ldAokXy5GZWF0dXJlTmFtZSAtbm90bGlrZSAnKlNlcnZlckNvcmUtRHJpdmVycy1HZW5lcmFsLVdPVzY0KicgLWFuZAojIGJyZWFrcyB3aW5kb3dzIHNlcnZlciB0dXJuIHdpbmRvd3MgZmVhdHVyZXMgb24gb3Igb2ZmCiRfLkZlYXR1cmVOYW1lIC1ub3RsaWtlICcqU2VydmVyLUd1aS1NZ210KicgLWFuZAojIGJyZWFrcyB3aW5kb3dzIHNlcnZlciBudmlkaWEgYXBwCiRfLkZlYXR1cmVOYW1lIC1ub3RsaWtlICcqV2lyZWxlc3NOZXR3b3JraW5nKicKfSB8IEZvckVhY2gtT2JqZWN0IHsKdHJ5IHsKRGlzYWJsZS1XaW5kb3dzT3B0aW9uYWxGZWF0dXJlIC1PbmxpbmUgLUZlYXR1cmVOYW1lICRfLkZlYXR1cmVOYW1lIC1Ob1Jlc3RhcnQgLVdhcm5pbmdBY3Rpb24gU2lsZW50bHlDb250aW51ZSB8IE91dC1OdWxsCn0gY2F0Y2ggeyB9Cn0KCkNsZWFyLUhvc3QKCldyaXRlLUhvc3QgIlVuaW5zdGFsbGluZzogTGVnYWN5IEFwcHMuIFBsZWFzZSB3YWl0Li4uYG4iCgojIHVuaW5zdGFsbCBicmxhcGkKY21kIC9jICJzYyBzdG9wIGAiYnJsYXBpYCIgPm51bCAyPiYxIgpjbWQgL2MgInNjIGRlbGV0ZSBgImJybGFwaWAiID5udWwgMj4mMSIKY21kIC9jICJ0YWtlb3duIC9mIGAiJGVudjpTeXN0ZW1Sb290XGJybHR0eWAiIC9yIC9kIHkgPm51bCAyPiYxIgpjbWQgL2MgImljYWNscyBgIiRlbnY6U3lzdGVtUm9vdFxicmx0dHlgIiAvZ3JhbnQgKlMtMS01LTMyLTU0NDpGIC90ID5udWwgMj4mMSIKUmVtb3ZlLUl0ZW0gIiRlbnY6U3lzdGVtUm9vdFxicmx0dHkiIC1SZWN1cnNlIC1Gb3JjZSAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB8IE91dC1OdWxsCgojIHVuaW5zdGFsbCBtaWNyb3NvZnQgZ2FtZWlucHV0CiRmaW5kbWljcm9zb2Z0Z2FtZWlucHV0ID0gIkhLTE06XFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFVuaW5zdGFsbFwqIgokbWljcm9zb2Z0Z2FtZWlucHV0ID0gR2V0LUl0ZW1Qcm9wZXJ0eSAkZmluZG1pY3Jvc29mdGdhbWVpbnB1dCAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB8CldoZXJlLU9iamVjdCB7ICRfLkRpc3BsYXlOYW1lIC1saWtlICIqTWljcm9zb2Z0IEdhbWVJbnB1dCoiIH0KaWYgKCRtaWNyb3NvZnRnYW1laW5wdXQpIHsKJGd1aWQgPSAkbWljcm9zb2Z0Z2FtZWlucHV0LlBTQ2hpbGROYW1lClN0YXJ0LVByb2Nlc3MgIm1zaWV4ZWMuZXhlIiAtQXJndW1lbnRMaXN0ICIveCAkZ3VpZCAvcW4gL25vcmVzdGFydCIgLVdhaXQgLU5vTmV3V2luZG93Cn0KCiMgc3RvcCBvbmVkcml2ZSBydW5uaW5nClN0b3AtUHJvY2VzcyAtRm9yY2UgLU5hbWUgT25lRHJpdmUgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUgfCBPdXQtTnVsbAoKIyB1bmluc3RhbGwgb25lZHJpdmUKY21kIC9jICJDOlxXaW5kb3dzXFN5c3RlbTMyXE9uZURyaXZlU2V0dXAuZXhlIC11bmluc3RhbGwgPm51bCAyPiYxIgojIHVuaW5zdGFsbCBvZmZpY2UgMzY1IG9uZWRyaXZlCkdldC1DaGlsZEl0ZW0gLVBhdGggIkM6XFByb2dyYW0gRmlsZXMqXE1pY3Jvc29mdCBPbmVEcml2ZSIsICIkZW52OkxPQ0FMQVBQREFUQVxNaWNyb3NvZnRcT25lRHJpdmUiIC1GaWx0ZXIgIk9uZURyaXZlU2V0dXAuZXhlIiAtUmVjdXJzZSAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB8CkZvckVhY2gtT2JqZWN0IHsgU3RhcnQtUHJvY2VzcyAtV2FpdCAkXy5GdWxsTmFtZSAtQXJndW1lbnRMaXN0ICIvdW5pbnN0YWxsIC9hbGx1c2VycyIgLVdpbmRvd1N0eWxlIEhpZGRlbiAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB9CiMgd2luZG93cyAxMCB1bmluc3RhbGwgb25lZHJpdmUKY21kIC9jICJDOlxXaW5kb3dzXFN5c1dPVzY0XE9uZURyaXZlU2V0dXAuZXhlIC11bmluc3RhbGwgPm51bCAyPiYxIgojIHdpbmRvd3MgMTAgcmVtb3ZlIG9uZWRyaXZlIHNjaGVkdWxlZCB0YXNrcwpHZXQtU2NoZWR1bGVkVGFzayB8IFdoZXJlLU9iamVjdCB7JF8uVGFza25hbWUgLW1hdGNoICdPbmVEcml2ZSd9IHwgVW5yZWdpc3Rlci1TY2hlZHVsZWRUYXNrIC1Db25maXJtOiRmYWxzZQoKIyB1bmluc3RhbGwgcmVtb3RlIGRlc2t0b3AgY29ubmVjdGlvbgp0cnkgewpTdGFydC1Qcm9jZXNzICJtc3RzYyIgLUFyZ3VtZW50TGlzdCAiL1VuaW5zdGFsbCIgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUKfSBjYXRjaCB7IH0KIyBzaWxlbnQgd2luZG93IGZvciByZW1vdGUgZGVza3RvcCBjb25uZWN0aW9uCiRwcm9jZXNzRXhpc3RzID0gR2V0LVByb2Nlc3MgLU5hbWUgbXN0c2MgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUKaWYgKCRwcm9jZXNzRXhpc3RzKSB7CiRydW5uaW5nID0gJHRydWUKJHRpbWVvdXQgPSAwCmRvIHsKJG1zdHNjUHJvY2VzcyA9IEdldC1Qcm9jZXNzIC1OYW1lIG1zdHNjIC1FcnJvckFjdGlvbiBTaWxlbnRseUNvbnRpbnVlCmlmICgkbXN0c2NQcm9jZXNzIC1hbmQgJG1zdHNjUHJvY2Vzcy5NYWluV2luZG93SGFuZGxlIC1uZSAwKSB7ClN0b3AtUHJvY2VzcyAtRm9yY2UgLU5hbWUgbXN0c2MgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUgfCBPdXQtTnVsbAokcnVubmluZyA9ICRmYWxzZQp9ClN0YXJ0LVNsZWVwIC1NaWxsaXNlY29uZHMgMTAwCiR0aW1lb3V0KysKaWYgKCR0aW1lb3V0IC1ndCAxMDApIHsKU3RvcC1Qcm9jZXNzIC1OYW1lIG1zdHNjIC1Gb3JjZSAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZQokcnVubmluZyA9ICRmYWxzZQp9Cn0gd2hpbGUgKCRydW5uaW5nKQp9ClN0YXJ0LVNsZWVwIC1TZWNvbmRzIDEKCiMgd2luZG93cyAxMCB1bmluc3RhbGwgb2xkIHNuaXBwaW5nIHRvb2wKdHJ5IHsKU3RhcnQtUHJvY2VzcyAiQzpcV2luZG93c1xTeXN0ZW0zMlxTbmlwcGluZ1Rvb2wuZXhlIiAtQXJndW1lbnRMaXN0ICIvVW5pbnN0YWxsIiAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZQp9IGNhdGNoIHsgfQojIHNpbGVudCB3aW5kb3cgZm9yIHVuaW5zdGFsbCBvbGQgc25pcHBpbmcgdG9vbAokcHJvY2Vzc0V4aXN0cyA9IEdldC1Qcm9jZXNzIC1OYW1lIFNuaXBwaW5nVG9vbCAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZQppZiAoJHByb2Nlc3NFeGlzdHMpIHsKJHJ1bm5pbmcgPSAkdHJ1ZQokdGltZW91dCA9IDAKZG8gewokc25pcFByb2Nlc3MgPSBHZXQtUHJvY2VzcyAtTmFtZSBTbmlwcGluZ1Rvb2wgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUKaWYgKCRzbmlwUHJvY2VzcyAtYW5kICRzbmlwUHJvY2Vzcy5NYWluV2luZG93SGFuZGxlIC1uZSAwKSB7ClN0b3AtUHJvY2VzcyAtRm9yY2UgLU5hbWUgU25pcHBpbmdUb29sIC1FcnJvckFjdGlvbiBTaWxlbnRseUNvbnRpbnVlIHwgT3V0LU51bGwKJHJ1bm5pbmcgPSAkZmFsc2UKfQpTdGFydC1TbGVlcCAtTWlsbGlzZWNvbmRzIDEwMAokdGltZW91dCsrCmlmICgkdGltZW91dCAtZ3QgMTAwKSB7ClN0b3AtUHJvY2VzcyAtTmFtZSBTbmlwcGluZ1Rvb2wgLUZvcmNlIC1FcnJvckFjdGlvbiBTaWxlbnRseUNvbnRpbnVlCiRydW5uaW5nID0gJGZhbHNlCn0KfSB3aGlsZSAoJHJ1bm5pbmcpCn0KU3RhcnQtU2xlZXAgLVNlY29uZHMgMQoKIyB3aW5kb3dzIDEwIHVuaW5zdGFsbCB1cGRhdGUgZm9yIHdpbmRvd3MgMTAgZm9yIHg2NC1iYXNlZCBzeXN0ZW1zCiRmaW5kdXBkYXRlZm9yd2luZG93cyA9ICJIS0xNOlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxVbmluc3RhbGxcKiIKJHVwZGF0ZWZvcndpbmRvd3MgPSBHZXQtSXRlbVByb3BlcnR5ICRmaW5kdXBkYXRlZm9yd2luZG93cyAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB8CldoZXJlLU9iamVjdCB7ICRfLkRpc3BsYXlOYW1lIC1saWtlICIqVXBkYXRlIGZvciB4NjQtYmFzZWQgV2luZG93cyBTeXN0ZW1zKiIgfQppZiAoJHVwZGF0ZWZvcndpbmRvd3MpIHsKJGd1aWQgPSAkdXBkYXRlZm9yd2luZG93cy5QU0NoaWxkTmFtZQpTdGFydC1Qcm9jZXNzICJtc2lleGVjLmV4ZSIgLUFyZ3VtZW50TGlzdCAiL3ggJGd1aWQgL3FuIC9ub3Jlc3RhcnQiIC1XYWl0IC1Ob05ld1dpbmRvdwp9CgojIHdpbmRvd3MgMTAgdW5pbnN0YWxsIG1pY3Jvc29mdCB1cGRhdGUgaGVhbHRoIHRvb2xzCiRmaW5kdXBkYXRlaGVhbHRodG9vbHMgPSAiSEtMTTpcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cVW5pbnN0YWxsXCoiCiR1cGRhdGVoZWFsdGh0b29scyA9IEdldC1JdGVtUHJvcGVydHkgJGZpbmR1cGRhdGVoZWFsdGh0b29scyAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB8CldoZXJlLU9iamVjdCB7ICRfLkRpc3BsYXlOYW1lIC1saWtlICIqTWljcm9zb2Z0IFVwZGF0ZSBIZWFsdGggVG9vbHMqIiB9CmlmICgkdXBkYXRlaGVhbHRodG9vbHMpIHsKJGd1aWQgPSAkdXBkYXRlaGVhbHRodG9vbHMuUFNDaGlsZE5hbWUKU3RhcnQtUHJvY2VzcyAibXNpZXhlYy5leGUiIC1Bcmd1bWVudExpc3QgIi94ICRndWlkIC9xbiAvbm9yZXN0YXJ0IiAtV2FpdCAtTm9OZXdXaW5kb3cKfQpjbWQgL2MgInJlZyBkZWxldGUgYCJIS0xNXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHVoc3N2Y2AiIC9mID5udWwgMj4mMSIKVW5yZWdpc3Rlci1TY2hlZHVsZWRUYXNrIC1UYXNrTmFtZSBQTFVHU2NoZWR1bGVyIC1Db25maXJtOiRmYWxzZSAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB8IE91dC1OdWxsCgpzaG93LW1lbnUKCiAgICAgICAgICB9CiAgICAgICAgMyB7CgpDbGVhci1Ib3N0CgpXcml0ZS1Ib3N0ICJJbnN0YWxsaW5nOiBTdG9yZS4gUGxlYXNlIHdhaXQuLi4iCgojIGluc3RhbGwgc3RvcmUKR2V0LUFwcFhQYWNrYWdlIC1BbGxVc2VycyB8IFdoZXJlLU9iamVjdCB7CiRfLk5hbWUgLWxpa2UgJypTdG9yZSonCn0gfCBGb3JlYWNoIHtBZGQtQXBweFBhY2thZ2UgLURpc2FibGVEZXZlbG9wbWVudE1vZGUgLVJlZ2lzdGVyIC1FcnJvckFjdGlvbiBTaWxlbnRseUNvbnRpbnVlICIkKCRfLkluc3RhbGxMb2NhdGlvbilcQXBwWE1hbmlmZXN0LnhtbCJ9CgpTdGFydC1TbGVlcCAtU2Vjb25kcyA1CgpDbGVhci1Ib3N0CgpXcml0ZS1Ib3N0ICJTdG9yZSBTZXR0aW5nczogT3B0aW1pemUuLi4iCgojIG9wZW4gc3RvcmUgc2V0dGluZ3MgcGFnZSBzbyBkaXNhYmxlIHBlcnNvbmFsaXplZCBleHBlcmllbmNlcyBvbiBtcyBhY2NvdW50IHN0aWNrcwp0cnkgewpTdGFydC1Qcm9jZXNzICJtcy13aW5kb3dzLXN0b3JlOnNldHRpbmdzIgp9IGNhdGNoIHsgfQpTdGFydC1TbGVlcCAtU2Vjb25kcyA1CgojIHN0b3Agc3RvcmUgcnVubmluZwokc3RvcCA9ICJXaW5TdG9yZS5BcHAiLCAiYmFja2dyb3VuZFRhc2tIb3N0IiwgIlN0b3JlRGVza3RvcEV4dGVuc2lvbiIKJHN0b3AgfCBGb3JFYWNoLU9iamVjdCB7IFN0b3AtUHJvY2VzcyAtTmFtZSAkXyAtRm9yY2UgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUgfQpTdGFydC1TbGVlcCAtU2Vjb25kcyAyCgojIGRpc2FibGUgYXBwcyB1cGRhdGVzCmNtZCAvYyAicmVnIGFkZCBgIkhLTE1cU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cV2luZG93c1N0b3JlXFdpbmRvd3NVcGRhdGVgIiAvdiBgIkF1dG9Eb3dubG9hZGAiIC90IFJFR19EV09SRCAvZCBgIjJgIiAvZiA+bnVsIDI+JjEiCgojIGNyZWF0ZSByZWcgZmlsZQokc3RvcmVzZXR0aW5ncyA9IEAnCldpbmRvd3MgUmVnaXN0cnkgRWRpdG9yIFZlcnNpb24gNS4wMAoKW0hLRVlfTE9DQUxfTUFDSElORVxTZXR0aW5nc1xMb2NhbFN0YXRlXQo7IGRpc2FibGUgdmlkZW8gYXV0b3BsYXkKIlZpZGVvQXV0b3BsYXkiPWhleCg1ZjVlMTBiKTowMCw5Niw5ZCw2OSw4ZCxjZCw5MyxkYywwMQo7IGRpc2FibGUgbm90aWZpY2F0aW9ucyBmb3IgYXBwIGluc3RhbGxhdGlvbnMKIkVuYWJsZUFwcEluc3RhbGxOb3RpZmljYXRpb25zIj1oZXgoNWY1ZTEwYik6MDAsMzYsZDAsODgsOGUsY2QsOTMsZGMsMDEKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU2V0dGluZ3NcTG9jYWxTdGF0ZVxQZXJzaXN0ZW50U2V0dGluZ3NdCjsgZGlzYWJsZSBwZXJzb25hbGl6ZWQgZXhwZXJpZW5jZXMKIlBlcnNvbmFsaXphdGlvbkVuYWJsZWQiPWhleCg1ZjVlMTBiKTowMCwwZCw1NixhMSw4YSxjZCw5MyxkYywwMQonQApTZXQtQ29udGVudCAtUGF0aCAiJGVudjpTeXN0ZW1Sb290XFRlbXBcd2luZG93c3N0b3JlLnJlZyIgLVZhbHVlICRzdG9yZXNldHRpbmdzIC1Gb3JjZQokc2V0dGluZ3NkYXQgPSAiJGVudjpMb2NhbEFwcERhdGFcUGFja2FnZXNcTWljcm9zb2Z0LldpbmRvd3NTdG9yZV84d2VreWIzZDhiYndlXFNldHRpbmdzXHNldHRpbmdzLmRhdCIKJHJlZ2ZpbGV3aW5kb3dzc3RvcmUgPSAiJGVudjpTeXN0ZW1Sb290XFRlbXBcd2luZG93c3N0b3JlLnJlZyIKCiMgbG9hZCBoaXZlCnJlZyBsb2FkICJIS0xNXFNldHRpbmdzIiAkc2V0dGluZ3NkYXQgPiRudWxsIDI+JjEKCiMgaW1wb3J0IHJlZyBmaWxlCmlmICgkTEFTVEVYSVRDT0RFIC1lcSAwKSB7CnJlZyBpbXBvcnQgJHJlZ2ZpbGV3aW5kb3dzc3RvcmUgPiRudWxsIDI+JjEKCiMgdW5sb2FkIGhpdmUKW2djXTo6Q29sbGVjdCgpClN0YXJ0LVNsZWVwIC1TZWNvbmRzIDIKcmVnIHVubG9hZCAiSEtMTVxTZXR0aW5ncyIgPiRudWxsIDI+JjEKfQpTdGFydC1TbGVlcCAtU2Vjb25kcyAyCgojIG9wZW4gc3RvcmUgc2V0dGluZ3MKU3RhcnQtUHJvY2VzcyAibXMtd2luZG93cy1zdG9yZTpzZXR0aW5ncyIKCnNob3ctbWVudQoKICAgICAgICAgIH0KICAgICAgICA0IHsKCkNsZWFyLUhvc3QKCldyaXRlLUhvc3QgIkluc3RhbGxpbmc6IEFsbCBVV1AgQXBwcy4gUGxlYXNlIHdhaXQuLi4iCgojIGluc3RhbGwgYWxsIHV3cCBhcHBzCkdldC1BcHB4UGFja2FnZSAtQWxsVXNlcnMgfCBGb3JlYWNoIHtBZGQtQXBweFBhY2thZ2UgLURpc2FibGVEZXZlbG9wbWVudE1vZGUgLVJlZ2lzdGVyIC1FcnJvckFjdGlvbiBTaWxlbnRseUNvbnRpbnVlICIkKCRfLkluc3RhbGxMb2NhdGlvbilcQXBwWE1hbmlmZXN0LnhtbCJ9IDI+JG51bGwKCnNob3ctbWVudQoKICAgICAgICAgIH0KICAgICAgICA1IHsKCkNsZWFyLUhvc3QKCldyaXRlLUhvc3QgIkluc3RhbGw6IFVXUCBGZWF0dXJlcy4uLmBuIgpXcml0ZS1Ib3N0ICJJbnN0YWxsaW5nIG11bHRpcGxlIGZlYXR1cmVzIGF0IG9uY2UgbWF5IGZhaWwiCldyaXRlLUhvc3QgIklmIHNvLCByZXN0YXJ0IFBDIGJldHdlZW4gZWFjaCBmZWF0dXJlIGluc3RhbGxgbiIKCiMgb3BlbiB1d3Agb3B0aW9uYWwgZmVhdHVyZXMKU3RhcnQtUHJvY2VzcyAibXMtc2V0dGluZ3M6b3B0aW9uYWxmZWF0dXJlcyIKCiMgdXdwIGxpc3QKV3JpdGUtSG9zdCAiIgpXcml0ZS1Ib3N0ICItLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0iCldyaXRlLUhvc3QgIiAgICAgIERlZmF1bHQgV2luZG93cyBJbnN0YWxsIExpc3QgVzExIgpXcml0ZS1Ib3N0ICItLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0iCldyaXRlLUhvc3QgIiIKV3JpdGUtSG9zdCAiLSBFeHRlbmRlZCBUaGVtZSBDb250ZW50IgpXcml0ZS1Ib3N0ICItIEZhY2lhbCBSZWNvZ25pdGlvbiAoV2luZG93cyBIZWxsbykiCldyaXRlLUhvc3QgIi0gSW50ZXJuZXQgRXhwbG9yZXIgbW9kZSIKV3JpdGUtSG9zdCAiLSBNYXRoIFJlY29nbml6ZXIiCldyaXRlLUhvc3QgIi0gTm90ZXBhZCAoc3lzdGVtKSIKV3JpdGUtSG9zdCAiLSBPcGVuU1NIIENsaWVudCIKV3JpdGUtSG9zdCAiLSBQcmludCBNYW5hZ2VtZW50IgpXcml0ZS1Ib3N0ICItIFN0ZXBzIFJlY29yZGVyIgpXcml0ZS1Ib3N0ICItIFdNSUMiCldyaXRlLUhvc3QgIi0gV2luZG93cyBNZWRpYSBQbGF5ZXIgTGVnYWN5IChBcHApIgpXcml0ZS1Ib3N0ICItIFdpbmRvd3MgUG93ZXJTaGVsbCBJU0UiCldyaXRlLUhvc3QgIi0gV29yZFBhZCIKV3JpdGUtSG9zdCAiIgpXcml0ZS1Ib3N0ICItLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0iCldyaXRlLUhvc3QgIiAgICAgIERlZmF1bHQgV2luZG93cyBJbnN0YWxsIExpc3QgVzEwIgpXcml0ZS1Ib3N0ICItLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0iCldyaXRlLUhvc3QgIiIKV3JpdGUtSG9zdCAiLSBJbnRlcm5ldCBFeHBsb3JlciAxMSIKV3JpdGUtSG9zdCAiLSBNYXRoIFJlY29nbml6ZXIiCldyaXRlLUhvc3QgIi0gTWljcm9zb2Z0IFF1aWNrIEFzc2lzdCAoQXBwKSIKV3JpdGUtSG9zdCAiLSBOb3RlcGFkIChzeXN0ZW0pIgpXcml0ZS1Ib3N0ICItIE9wZW5TU0ggQ2xpZW50IgpXcml0ZS1Ib3N0ICItIFByaW50IE1hbmFnZW1lbnQgQ29uc29sZSIKV3JpdGUtSG9zdCAiLSBTdGVwcyBSZWNvcmRlciIKV3JpdGUtSG9zdCAiLSBXaW5kb3dzIEZheCBhbmQgU2NhbiIKV3JpdGUtSG9zdCAiLSBXaW5kb3dzIEhlbGxvIEZhY2UiCldyaXRlLUhvc3QgIi0gV2luZG93cyBNZWRpYSBQbGF5ZXIgTGVnYWN5IChBcHApIgpXcml0ZS1Ib3N0ICItIFdpbmRvd3MgUG93ZXJTaGVsbCBJbnRlZ3JhdGVkIFNjcmlwdGluZyBFbnZpcm9ubWVudCIKV3JpdGUtSG9zdCAiLSBXb3JkUGFkIgpXcml0ZS1Ib3N0ICIiCgpQYXVzZQoKc2hvdy1tZW51CgogICAgICAgICAgfQogICAgICAgIDYgewoKQ2xlYXItSG9zdAoKV3JpdGUtSG9zdCAiSW5zdGFsbDogTGVnYWN5IEZlYXR1cmVzLi4uIgoKIyBvcGVuIGxlZ2FjeSBvcHRpb25hbCBmZWF0dXJlcwpTdGFydC1Qcm9jZXNzICJDOlxXaW5kb3dzXFN5c3RlbTMyXE9wdGlvbmFsRmVhdHVyZXMuZXhlIgoKIyBsZWdhY3kgbGlzdApXcml0ZS1Ib3N0ICIiCldyaXRlLUhvc3QgIi0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLSIKV3JpdGUtSG9zdCAiICAgICAgRGVmYXVsdCBXaW5kb3dzIEluc3RhbGwgTGlzdCBXMTEiCldyaXRlLUhvc3QgIi0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLSIKV3JpdGUtSG9zdCAiIgpXcml0ZS1Ib3N0ICItIC5OZXQgRnJhbWV3b3JrIDQuOCBBZHZhbmNlZCBTZXJ2aWNlcyArIgpXcml0ZS1Ib3N0ICItIFdDRiBTZXJ2aWNlcyArIgpXcml0ZS1Ib3N0ICItIFRDUCBQb3J0IFNoYXJpbmciCldyaXRlLUhvc3QgIi0gTWVkaWEgRmVhdHVyZXMgKyIKV3JpdGUtSG9zdCAiLSBXaW5kb3dzIE1lZGlhIFBsYXllciBMZWdhY3kgKEFwcCkiCldyaXRlLUhvc3QgIi0gTWljcm9zb2Z0IFByaW50IHRvIFBERiIKV3JpdGUtSG9zdCAiLSBQcmludCBhbmQgRG9jdW1lbnQgU2VydmljZXMgKyIKV3JpdGUtSG9zdCAiLSBJbnRlcm5ldCBQcmludGluZyBDbGllbnQiCldyaXRlLUhvc3QgIi0gUmVtb3RlIERpZmZlcmVudGlhbCBDb21wcmVzc2lvbiBBUEkgU3VwcG9ydCIKV3JpdGUtSG9zdCAiLSBTTUIgRGlyZWN0IgpXcml0ZS1Ib3N0ICItIFdpbmRvd3MgUG93ZXJTaGVsbCAyLjAgKyIKV3JpdGUtSG9zdCAiLSBXaW5kb3dzIFBvd2VyU2hlbGwgMi4wIEVuZ2luZSIKV3JpdGUtSG9zdCAiLSBXb3JrIEZvbGRlcnMgQ2xpZW50IgpXcml0ZS1Ib3N0ICIiCldyaXRlLUhvc3QgIi0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLSIKV3JpdGUtSG9zdCAiICAgICAgRGVmYXVsdCBXaW5kb3dzIEluc3RhbGwgTGlzdCBXMTAiCldyaXRlLUhvc3QgIi0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLSIKV3JpdGUtSG9zdCAiIgpXcml0ZS1Ib3N0ICItIC5OZXQgRnJhbWV3b3JrIDQuOCBBZHZhbmNlZCBTZXJ2aWNlcyArIgpXcml0ZS1Ib3N0ICItIFdDRiBTZXJ2aWNlcyArIgpXcml0ZS1Ib3N0ICItIFRDUCBQb3J0IFNoYXJpbmciCldyaXRlLUhvc3QgIi0gSW50ZXJuZXQgRXhwbG9yZXIgMTEiCldyaXRlLUhvc3QgIi0gTWVkaWEgRmVhdHVyZXMgKyIKV3JpdGUtSG9zdCAiLSBXaW5kb3dzIE1lZGlhIFBsYXllciIKV3JpdGUtSG9zdCAiLSBNaWNyb3NvZnQgUHJpbnQgdG8gUERGIgpXcml0ZS1Ib3N0ICItIE1pY3Jvc29mdCBYUFMgRG9jdW1lbnQgV3JpdGVyIgpXcml0ZS1Ib3N0ICItIFByaW50IGFuZCBEb2N1bWVudCBTZXJ2aWNlcyArIgpXcml0ZS1Ib3N0ICItIEludGVybmV0IFByaW50aW5nIENsaWVudCIKV3JpdGUtSG9zdCAiLSBSZW1vdGUgRGlmZmVyZW50aWFsIENvbXByZXNzaW9uIEFQSSBTdXBwb3J0IgpXcml0ZS1Ib3N0ICItIFNNQiAxLjAvQ0lGUyBGaWxlIFNoYXJpbmcgU3VwcG9ydCArIgpXcml0ZS1Ib3N0ICItIFNNQiAxLjAvQ0lGUyBBdXRvbWF0aWMgUmVtb3ZhbCIKV3JpdGUtSG9zdCAiLSBTTUIgMS4wL0NJRlMgQ2xpZW50IgpXcml0ZS1Ib3N0ICItIFNNQiBEaXJlY3QiCldyaXRlLUhvc3QgIi0gV2luZG93cyBQb3dlclNoZWxsIDIuMCArIgpXcml0ZS1Ib3N0ICItIFdpbmRvd3MgUG93ZXJTaGVsbCAyLjAgRW5naW5lIgpXcml0ZS1Ib3N0ICItIFdvcmsgRm9sZGVycyBDbGllbnQiCldyaXRlLUhvc3QgIiIKClBhdXNlCgpzaG93LW1lbnUKCiAgICAgICAgICB9CiAgICAgICAgNyB7CgpDbGVhci1Ib3N0CgpXcml0ZS1Ib3N0ICJJbnN0YWxsaW5nOiBPbmUgRHJpdmUuIFBsZWFzZSB3YWl0Li4uIgoKIyBpbnN0YWxsIG9uZWRyaXZlIHcxMApjbWQgL2MgIkM6XFdpbmRvd3NcU3lzV09XNjRcT25lRHJpdmVTZXR1cC5leGUgPm51bCAyPiYxIgoKIyBpbnN0YWxsIG9uZWRyaXZlIHcxMQpjbWQgL2MgIkM6XFdpbmRvd3NcU3lzdGVtMzJcT25lRHJpdmVTZXR1cC5leGUgPm51bCAyPiYxIgoKc2hvdy1tZW51CgogICAgICAgICAgfQogICAgICAgIDggewoKQ2xlYXItSG9zdAoKV3JpdGUtSG9zdCAiSW5zdGFsbGluZzogUmVtb3RlIERlc2t0b3AgQ29ubmVjdGlvbi4gUGxlYXNlIHdhaXQuLi4iCgojIGRvd25sb2FkIHJlbW90ZSBkZXNrdG9wIGNvbm5lY3Rpb24KSVdSICJodHRwczovL2dvLm1pY3Jvc29mdC5jb20vZndsaW5rLz9saW5raWQ9MjI0NzY1OSIgLU91dEZpbGUgIiRlbnY6U3lzdGVtUm9vdFxUZW1wXFJlbW90ZURlc2t0b3BDb25uZWN0aW9uLmV4ZSIKCiMgaW5zdGFsbCByZW1vdGUgZGVza3RvcCBjb25uZWN0aW9uIApjbWQgL2MgIiRlbnY6U3lzdGVtUm9vdFxUZW1wXFJlbW90ZURlc2t0b3BDb25uZWN0aW9uLmV4ZSA+bnVsIDI+JjEiCgpzaG93LW1lbnUKCiAgICAgICAgICB9CiAgICAgICAgOSB7CgpDbGVhci1Ib3N0CgpXcml0ZS1Ib3N0ICJJbnN0YWxsaW5nOiBTbmlwcGluZyBUb29sLiBQbGVhc2Ugd2FpdC4uLiIKV3JpdGUtSG9zdCAiIgpXcml0ZS1Ib3N0ICJJZ25vcmUgaW5zdGFsbGVyIGVycm9yIFcxMSIKV3JpdGUtSG9zdCAiSWYgaW5zdGFsbGVyIGZhaWxzIG9uIFcxMCwgcmVzdGFydCBQQyBhbmQgcmVydW4gc2NyaXB0IgpXcml0ZS1Ib3N0ICIiCgojIGRvd25sb2FkIHcxMCBzbmlwcGluZyB0b29sCklXUiAiaHR0cHM6Ly9kb3dubG9hZC5taWNyb3NvZnQuY29tL2Rvd25sb2FkL2YvNC9lL2Y0ZTAzNDY1LTM0ZDEtNDliNi1hZjFhLTI4MTZjYTRhMjQwMi9pbnN0YWxsZXJzX3NpZ25lZC9zbmlwcGluZ3Rvb2xfc2V0dXBfeDY0LmV4ZSIgLU91dEZpbGUgIiRlbnY6U3lzdGVtUm9vdFxUZW1wXFNuaXBwaW5nVG9vbC5leGUiCgojIGluc3RhbGwgdzEwIHNuaXBwaW5nIHRvb2wKY21kIC9jICIkZW52OlN5c3RlbVJvb3RcVGVtcFxTbmlwcGluZ1Rvb2wuZXhlID5udWwgMj4mMSIKCiMgaW5zdGFsbCB3MTEgc25pcHBpbmcgdG9vbApHZXQtQXBwWFBhY2thZ2UgLUFsbFVzZXJzICpNaWNyb3NvZnQuU2NyZWVuU2tldGNoKiB8IEZvcmVhY2gge0FkZC1BcHB4UGFja2FnZSAtRGlzYWJsZURldmVsb3BtZW50TW9kZSAtUmVnaXN0ZXIgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUgIiQoJF8uSW5zdGFsbExvY2F0aW9uKVxBcHBYTWFuaWZlc3QueG1sIn0KCnNob3ctbWVudQoKICAgICAgICAgIH0KICAgICAgICB9IH0gZWxzZSB7IFdyaXRlLUhvc3QgIkludmFsaWQgaW5wdXQuIFBsZWFzZSBzZWxlY3QgYSB2YWxpZCBvcHRpb24gKDEtOSkuIiB9IH0='
$sync.assets.core1thread1 = 'ICAgICAgICAjIFNDUklQVCBSVU4gQVMgQURNSU4KICAgICAgICBJZiAoIShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NQcmluY2lwYWxdW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzSWRlbnRpdHldOjpHZXRDdXJyZW50KCkpLklzSW5Sb2xlKFtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0J1aWx0SW5Sb2xlXSJBZG1pbmlzdHJhdG9yIikpCiAgICAgICAge1N0YXJ0LVByb2Nlc3MgUG93ZXJTaGVsbC5leGUgLUFyZ3VtZW50TGlzdCAoIi1Ob1Byb2ZpbGUgLUV4ZWN1dGlvblBvbGljeSBCeXBhc3MgLUZpbGUgYCJ7MH1gIiIgLWYgJFBTQ29tbWFuZFBhdGgpIC1WZXJiIFJ1bkFzCiAgICAgICAgRXhpdH0KICAgICAgICAkSG9zdC5VSS5SYXdVSS5XaW5kb3dUaXRsZSA9ICRteUludm9jYXRpb24uTXlDb21tYW5kLkRlZmluaXRpb24gKyAiIChBZG1pbmlzdHJhdG9yKSIKICAgICAgICAkSG9zdC5VSS5SYXdVSS5CYWNrZ3JvdW5kQ29sb3IgPSAiQmxhY2siCiAgICAgICAgJEhvc3QuUHJpdmF0ZURhdGEuUHJvZ3Jlc3NCYWNrZ3JvdW5kQ29sb3IgPSAiQmxhY2siCiAgICAgICAgJEhvc3QuUHJpdmF0ZURhdGEuUHJvZ3Jlc3NGb3JlZ3JvdW5kQ29sb3IgPSAiV2hpdGUiCiAgICAgICAgQ2xlYXItSG9zdAoKCQlXcml0ZS1Ib3N0ICJURU1QT1JBUklMWSBESVNBQkxFIENQVSBDT1JFIDEgJiBUSFJFQUQgMSBGT1IgVEVTVElORyBQRVIgQVBQL0dBTUVgbiIKICAgICAgICBXcml0ZS1Ib3N0ICJDT1JFIDEgVEhSRUFEIDE6IgogICAgICAgIFdyaXRlLUhvc3QgIjEuIE9mZjogQWxyZWFkeSBSdW5uaW5nIgogICAgICAgIFdyaXRlLUhvc3QgIjIuIE9mZjogU3RhcnR1cGBuIgogICAgICAgIHdoaWxlICgkdHJ1ZSkgewogICAgICAgICRjaG9pY2UgPSBSZWFkLUhvc3QgIiAiCiAgICAgICAgaWYgKCRjaG9pY2UgLW1hdGNoICdeWzEtMl0kJykgewogICAgICAgIHN3aXRjaCAoJGNob2ljZSkgewogICAgICAgIDEgewoKQ2xlYXItSG9zdAoKIyBnZXQgbnVtYmVyIG9mIGxvZ2ljYWwgcHJvY2Vzc29ycwokTk9MUCA9IChHZXQtV21pT2JqZWN0IFdpbjMyX0NvbXB1dGVyU3lzdGVtKS5OdW1iZXJPZkxvZ2ljYWxQcm9jZXNzb3JzCgojIGNvbnZlcnQgaW5wdXQgdG8gaW50ZWdlcgokTk9MUCA9IFtpbnRdJE5PTFAKCiMgc2V0IGFmZmluaXR5IG1hc2sgd2l0aCBjb3JlIDEgYW5kIHRocmVhZCAxIGRpc2FibGVkIChleGNsdWRlIGJpdCAwIGFuZCBiaXQgMSkKJGhleGFkZWNpbWFsID0gW2ludF0oW21hdGhdOjpQb3coMiwgJE5PTFApIC0gMSkgLSAzCgojIGNvcHkgZ2FtZSBleGUgaWQKKEdldC1Qcm9jZXNzIHwgV2hlcmUtT2JqZWN0IHskXy5Xb3JraW5nU2V0NjQgLWd0IDUwME1CfSB8IFNlbGVjdC1PYmplY3QgTmFtZSwgSWQpIHwgRm9ybWF0LVRhYmxlIC1BdXRvU2l6ZQokZXhlaWQgPSBSZWFkLUhvc3QgLVByb21wdCAiRU5URVIgR0FNRSBFWEUgSUQiCgpDbGVhci1Ib3N0CgojIHNldCBnYW1lIGV4ZSBjb3JlMS90aHJlYWQxIG9mZgokc210aHRvZmYgPSBHZXQtUHJvY2VzcyAtSWQgJGV4ZWlkCiRzbXRodG9mZi5Qcm9jZXNzb3JBZmZpbml0eSA9ICRoZXhhZGVjaW1hbAoKIyBjaGVjayBuZXcgdmFsdWUKJHJlbG9hZGV4ZWlkID0gR2V0LVByb2Nlc3MgLUlkICRleGVpZAoKIyBzaG93IG5ldyB2YWx1ZQokc2hvd3ZhbHVlID0gW0NvbnZlcnRdOjpUb1N0cmluZyhbaW50XSRyZWxvYWRleGVpZC5Qcm9jZXNzb3JBZmZpbml0eSwgMikuUGFkTGVmdCgkTk9MUCwgJzAnKQpXcml0ZS1Ib3N0ICJJRCAtICRleGVpZCA9ICRzaG93dmFsdWVgbiIKClBhdXNlCgpleGl0CgogICAgICAgICAgfQogICAgICAgIDIgewoKQ2xlYXItSG9zdAoKIyBzdG9wIGdhbWUgbGF1bmNoZXJzIHJ1bm5pbmcKJHN0b3AgPSAiQmF0dGxlLm5ldCIsICJCc2dMYXVuY2hlciIsICJFQURlc2t0b3AiLCAiRXBpY0dhbWVzTGF1bmNoZXIiLCAiR2FsYXh5Q2xpZW50IiwgIlJvYmxveFBsYXllckJldGEiLCAiUmlvdENsaWVudFNlcnZpY2VzIiwgIkxhdW5jaGVyIiwgInN0ZWFtIiwgInVwYyIKJHN0b3AgfCBGb3JFYWNoLU9iamVjdCB7IFN0b3AtUHJvY2VzcyAtTmFtZSAkXyAtRm9yY2UgLUVycm9yQWN0aW9uIFNpbGVudGx5Q29udGludWUgfQoKIyBnZXQgbnVtYmVyIG9mIGxvZ2ljYWwgcHJvY2Vzc29ycwokTk9MUCA9IChHZXQtV21pT2JqZWN0IFdpbjMyX0NvbXB1dGVyU3lzdGVtKS5OdW1iZXJPZkxvZ2ljYWxQcm9jZXNzb3JzCgojIGNvbnZlcnQgaW5wdXQgdG8gaW50ZWdlcgokTk9MUCA9IFtpbnRdJE5PTFAKCiMgc2V0IGFmZmluaXR5IG1hc2sgd2l0aCBjb3JlIDEgYW5kIHRocmVhZCAxIGRpc2FibGVkIChleGNsdWRlIGJpdCAwIGFuZCBiaXQgMSkKJGFmZmluaXR5ID0gW2ludF0oW21hdGhdOjpQb3coMiwgJE5PTFApIC0gMSkgLSAzCiRoZXhhZGVjaW1hbCA9ICJ7MDpYfSIgLWYgJGFmZmluaXR5CgojIHNlbGVjdCBnYW1lIGxhdW5jaGVyIGxuayBvciBleGUKV3JpdGUtSG9zdCAiU0VMRUNUIExBVU5DSEVSL0dBTUUvU0hPUlRDVVQvRVhFOiIKQWRkLVR5cGUgLUFzc2VtYmx5TmFtZSBTeXN0ZW0uV2luZG93cy5Gb3JtcwokRGlhbG9nID0gTmV3LU9iamVjdCBTeXN0ZW0uV2luZG93cy5Gb3Jtcy5PcGVuRmlsZURpYWxvZwokRGlhbG9nLkZpbHRlciA9ICJBbGwgRmlsZXMgKCouKil8Ki4qIgokRGlhbG9nLlNob3dEaWFsb2coKSB8IE91dC1OdWxsCiRnYW1lbGF1bmNoZXIgPSAkRGlhbG9nLkZpbGVOYW1lCgpDbGVhci1Ib3N0CgojIHN0YXJ0IGdhbWUgbGF1bmNoZXIgbG5rIG9yIGV4ZSB3aXRoIGNvcmUxL3RocmVhZDEgb2ZmCmNtZCAvYyAic3RhcnQgYCJgIiAvYWZmaW5pdHkgJGhleGFkZWNpbWFsIGAiJGdhbWVsYXVuY2hlcmAiIgoKV3JpdGUtSG9zdCAiR0VUVElORyBWQUxVRS4uLiIKClN0YXJ0LVNsZWVwIC1TZWNvbmRzIDEwCgojIGNvbnZlcnQgZGlyZWN0b3J5IHRvIGZpbGUgbmFtZSB3aXRob3V0IGV4ZQokZ2FtZWxhdW5jaGVyID0gW1N5c3RlbS5JTy5QYXRoXTo6R2V0RmlsZU5hbWVXaXRob3V0RXh0ZW5zaW9uKCRnYW1lbGF1bmNoZXIpCgojIGNoZWNrIHZhbHVlCiRyZWxvYWRnYW1lbGF1bmNoZXIgPSAoR2V0LVByb2Nlc3MgLU5hbWUgIiRnYW1lbGF1bmNoZXIiKS5Qcm9jZXNzb3JBZmZpbml0eQoKIyBjb252ZXJ0IHZhbHVlCiRzaG93dmFsdWUgPSBbQ29udmVydF06OlRvU3RyaW5nKFtpbnRdJHJlbG9hZGdhbWVsYXVuY2hlciwgMikKCkNsZWFyLUhvc3QKCiMgc2hvdyBuZXcgdmFsdWUKJHNob3d2YWx1ZSA9ICRzaG93dmFsdWUuUGFkTGVmdCgkTk9MUCwgIjAiKQpXcml0ZS1Ib3N0ICJFWEUgLSAkZ2FtZWxhdW5jaGVyID0gJHNob3d2YWx1ZWBuIgoKUGF1c2UKCmV4aXQKCiAgICAgICAgICB9CiAgICAgICAgfSB9IGVsc2UgeyBXcml0ZS1Ib3N0ICJJbnZhbGlkIGlucHV0LiBQbGVhc2Ugc2VsZWN0IGEgdmFsaWQgb3B0aW9uICgxLTIpLiIgfSB9'
$sync.assets.ddu = '77u/IyBTQ1JJUFQgUlVOIEFTIEFETUlOCiAgICAgICAgSWYgKCEoW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzUHJpbmNpcGFsXVtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0lkZW50aXR5XTo6R2V0Q3VycmVudCgpKS5Jc0luUm9sZShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NCdWlsdEluUm9sZV0iQWRtaW5pc3RyYXRvciIpKQogICAgICAgIHtTdGFydC1Qcm9jZXNzIFBvd2VyU2hlbGwuZXhlIC1Bcmd1bWVudExpc3QgKCItTm9Qcm9maWxlIC1FeGVjdXRpb25Qb2xpY3kgQnlwYXNzIC1GaWxlIGAiezB9YCIiIC1mICRQU0NvbW1hbmRQYXRoKSAtVmVyYiBSdW5BcwogICAgICAgIEV4aXR9CiAgICAgICAgJEhvc3QuVUkuUmF3VUkuV2luZG93VGl0bGUgPSAkbXlJbnZvY2F0aW9uLk15Q29tbWFuZC5EZWZpbml0aW9uICsgIiAoQWRtaW5pc3RyYXRvcikiCiAgICAgICAgJEhvc3QuVUkuUmF3VUkuQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzRm9yZWdyb3VuZENvbG9yID0gIldoaXRlIgogICAgICAgIENsZWFyLUhvc3QKCiMgcmVtb3ZlIHNhZmUgbW9kZSBib290CmNtZCAvYyAiYmNkZWRpdCAvZGVsZXRldmFsdWUge2N1cnJlbnR9IHNhZmVib290ID5udWwgMj4mMSIKCldyaXRlLUhvc3QgIkREVSAmIFJFU1RBUlRJTkdgbiIgLUZvcmVncm91bmRDb2xvciBSZWQKCiMgdW5pbnN0YWxsIHNvdW5kYmxhc3RlciByZWFsdGVrIGludGVsIGFtZCBudmlkaWEgZHJpdmVycyAmIHJlc3RhcnQKU3RhcnQtUHJvY2VzcyAiJGVudjpTeXN0ZW1Ecml2ZVxQcm9ncmFtIEZpbGVzICh4ODYpXERpc3BsYXkgRHJpdmVyIFVuaW5zdGFsbGVyXERpc3BsYXkgRHJpdmVyIFVuaW5zdGFsbGVyLmV4ZSIgLUFyZ3VtZW50TGlzdCAiLUNsZWFuU291bmRCbGFzdGVyIC1DbGVhblJlYWx0ZWsgLUNsZWFuQWxsR3B1cyAtUmVzdGFydCIgLVdhaXQK'
$sync.assets.ddumanual = '77u/IyBTQ1JJUFQgUlVOIEFTIEFETUlOCiAgICAgICAgSWYgKCEoW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzUHJpbmNpcGFsXVtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0lkZW50aXR5XTo6R2V0Q3VycmVudCgpKS5Jc0luUm9sZShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NCdWlsdEluUm9sZV0iQWRtaW5pc3RyYXRvciIpKQogICAgICAgIHtTdGFydC1Qcm9jZXNzIFBvd2VyU2hlbGwuZXhlIC1Bcmd1bWVudExpc3QgKCItTm9Qcm9maWxlIC1FeGVjdXRpb25Qb2xpY3kgQnlwYXNzIC1GaWxlIGAiezB9YCIiIC1mICRQU0NvbW1hbmRQYXRoKSAtVmVyYiBSdW5BcwogICAgICAgIEV4aXR9CiAgICAgICAgJEhvc3QuVUkuUmF3VUkuV2luZG93VGl0bGUgPSAkbXlJbnZvY2F0aW9uLk15Q29tbWFuZC5EZWZpbml0aW9uICsgIiAoQWRtaW5pc3RyYXRvcikiCiAgICAgICAgJEhvc3QuVUkuUmF3VUkuQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzRm9yZWdyb3VuZENvbG9yID0gIldoaXRlIgogICAgICAgIENsZWFyLUhvc3QKCiMgcmVtb3ZlIHNhZmUgbW9kZSBib290CmNtZCAvYyAiYmNkZWRpdCAvZGVsZXRldmFsdWUge2N1cnJlbnR9IHNhZmVib290ID5udWwgMj4mMSIKCldyaXRlLUhvc3QgIkREVSBNQU5VQUxgbiIKCiMgb3BlbiBkZHUKU3RhcnQtUHJvY2VzcyAtV2FpdCAiJGVudjpTeXN0ZW1Ecml2ZVxQcm9ncmFtIEZpbGVzICh4ODYpXERpc3BsYXkgRHJpdmVyIFVuaW5zdGFsbGVyXERpc3BsYXkgRHJpdmVyIFVuaW5zdGFsbGVyLmV4ZSIK'
$sync.assets.ddusettings = '77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz4KPERpc3BsYXlEcml2ZXJVbmluc3RhbGxlciBWZXJzaW9uPSIxOC4xLjQuMiI+Cgk8U2V0dGluZ3M+CgkJPFNlbGVjdGVkTGFuZ3VhZ2U+ZW4tVVM8L1NlbGVjdGVkTGFuZ3VhZ2U+CgkJPFJlbW92ZU1vbml0b3JzPlRydWU8L1JlbW92ZU1vbml0b3JzPgoJCTxSZW1vdmVDcmltc29uQ2FjaGU+VHJ1ZTwvUmVtb3ZlQ3JpbXNvbkNhY2hlPgoJCTxSZW1vdmVBTUREaXJzPlRydWU8L1JlbW92ZUFNRERpcnM+CgkJPFJlbW92ZUF1ZGlvQnVzPlRydWU8L1JlbW92ZUF1ZGlvQnVzPgoJCTxSZW1vdmVBTURLTVBGRD5UcnVlPC9SZW1vdmVBTURLTVBGRD4KCQk8UmVtb3ZlTnZpZGlhRGlycz5UcnVlPC9SZW1vdmVOdmlkaWFEaXJzPgoJCTxSZW1vdmVQaHlzWD5UcnVlPC9SZW1vdmVQaHlzWD4KCQk8UmVtb3ZlM0RUVlBsYXk+VHJ1ZTwvUmVtb3ZlM0RUVlBsYXk+CgkJPFJlbW92ZUdGRT5UcnVlPC9SZW1vdmVHRkU+CgkJPFJlbW92ZU5WQlJPQURDQVNUPlRydWU8L1JlbW92ZU5WQlJPQURDQVNUPgoJCTxSZW1vdmVOVkNQPlRydWU8L1JlbW92ZU5WQ1A+CgkJPFJlbW92ZUlOVEVMQ1A+VHJ1ZTwvUmVtb3ZlSU5URUxDUD4KCQk8UmVtb3ZlSU5URUxJR1M+VHJ1ZTwvUmVtb3ZlSU5URUxJR1M+CgkJPFJlbW92ZU9uZUFQST5UcnVlPC9SZW1vdmVPbmVBUEk+CgkJPFJlbW92ZUVuZHVyYW5jZUdhbWluZz5UcnVlPC9SZW1vdmVFbmR1cmFuY2VHYW1pbmc+CgkJPFJlbW92ZUludGVsTnB1PlRydWU8L1JlbW92ZUludGVsTnB1PgoJCTxSZW1vdmVBTURDUD5UcnVlPC9SZW1vdmVBTURDUD4KCQk8VXNlUm9hbWluZ0NvbmZpZz5GYWxzZTwvVXNlUm9hbWluZ0NvbmZpZz4KCQk8Q2hlY2tVcGRhdGVzPkZhbHNlPC9DaGVja1VwZGF0ZXM+CgkJPENyZWF0ZVJlc3RvcmVQb2ludD5GYWxzZTwvQ3JlYXRlUmVzdG9yZVBvaW50PgoJCTxTYXZlTG9ncz5GYWxzZTwvU2F2ZUxvZ3M+CgkJPFJlbW92ZVZ1bGthbj5UcnVlPC9SZW1vdmVWdWxrYW4+CgkJPFNob3dPZmZlcj5GYWxzZTwvU2hvd09mZmVyPgoJCTxFbmFibGVTYWZlTW9kZURpYWxvZz5GYWxzZTwvRW5hYmxlU2FmZU1vZGVEaWFsb2c+CgkJPFByZXZlbnRXaW5VcGRhdGU+VHJ1ZTwvUHJldmVudFdpblVwZGF0ZT4KCQk8VXNlZEJDRD5GYWxzZTwvVXNlZEJDRD4KCQk8S2VlcE5WQ1BvcHQ+RmFsc2U8L0tlZXBOVkNQb3B0PgoJCTxSZW1lbWJlckxhc3RDaG9pY2U+RmFsc2U8L1JlbWVtYmVyTGFzdENob2ljZT4KCQk8TGFzdFNlbGVjdGVkR1BVSW5kZXg+MDwvTGFzdFNlbGVjdGVkR1BVSW5kZXg+CgkJPExhc3RTZWxlY3RlZFR5cGVJbmRleD4wPC9MYXN0U2VsZWN0ZWRUeXBlSW5kZXg+Cgk8L1NldHRpbmdzPgo8L0Rpc3BsYXlEcml2ZXJVbmluc3RhbGxlcj4K'
$sync.assets.defenderdisable = '77u/IyBTQ1JJUFQgUlVOIEFTIEFETUlOCiAgICAgICAgSWYgKCEoW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzUHJpbmNpcGFsXVtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0lkZW50aXR5XTo6R2V0Q3VycmVudCgpKS5Jc0luUm9sZShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NCdWlsdEluUm9sZV0iQWRtaW5pc3RyYXRvciIpKQogICAgICAgIHtTdGFydC1Qcm9jZXNzIFBvd2VyU2hlbGwuZXhlIC1Bcmd1bWVudExpc3QgKCItTm9Qcm9maWxlIC1FeGVjdXRpb25Qb2xpY3kgQnlwYXNzIC1GaWxlIGAiezB9YCIiIC1mICRQU0NvbW1hbmRQYXRoKSAtVmVyYiBSdW5BcwogICAgICAgIEV4aXR9CiAgICAgICAgJEhvc3QuVUkuUmF3VUkuV2luZG93VGl0bGUgPSAkbXlJbnZvY2F0aW9uLk15Q29tbWFuZC5EZWZpbml0aW9uICsgIiAoQWRtaW5pc3RyYXRvcikiCiAgICAgICAgJEhvc3QuVUkuUmF3VUkuQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzRm9yZWdyb3VuZENvbG9yID0gIldoaXRlIgogICAgICAgIENsZWFyLUhvc3QKCiAgICAgICAgIyBGVU5DVElPTiBSVU4gQVMgVFJVU1RFRCBJTlNUQUxMRVIKICAgICAgICBmdW5jdGlvbiBSdW4tVHJ1c3RlZChbU3RyaW5nXSRjb21tYW5kKSB7CiAgICAgICAgdHJ5IHsKICAgIAlTdG9wLVNlcnZpY2UgLU5hbWUgVHJ1c3RlZEluc3RhbGxlciAtRm9yY2UgLUVycm9yQWN0aW9uIFN0b3AgLVdhcm5pbmdBY3Rpb24gU3RvcAogIAkJfQogIAkJY2F0Y2ggewogICAgCXRhc2traWxsIC9pbSB0cnVzdGVkaW5zdGFsbGVyLmV4ZSAvZiA+JG51bGwKICAJCX0KICAgICAgICAkc2VydmljZSA9IEdldC1DaW1JbnN0YW5jZSAtQ2xhc3NOYW1lIFdpbjMyX1NlcnZpY2UgLUZpbHRlciAiTmFtZT0nVHJ1c3RlZEluc3RhbGxlciciCiAgICAgICAgJERlZmF1bHRCaW5QYXRoID0gJHNlcnZpY2UuUGF0aE5hbWUKICAJCSR0cnVzdGVkSW5zdGFsbGVyUGF0aCA9ICIkZW52OlN5c3RlbVJvb3Rcc2VydmljaW5nXFRydXN0ZWRJbnN0YWxsZXIuZXhlIgogIAkJaWYgKCREZWZhdWx0QmluUGF0aCAtbmUgJHRydXN0ZWRJbnN0YWxsZXJQYXRoKSB7CiAgICAJJERlZmF1bHRCaW5QYXRoID0gJHRydXN0ZWRJbnN0YWxsZXJQYXRoCiAgCQl9CiAgICAgICAgJGJ5dGVzID0gW1N5c3RlbS5UZXh0LkVuY29kaW5nXTo6VW5pY29kZS5HZXRCeXRlcygkY29tbWFuZCkKICAgICAgICAkYmFzZTY0Q29tbWFuZCA9IFtDb252ZXJ0XTo6VG9CYXNlNjRTdHJpbmcoJGJ5dGVzKQogICAgICAgIHNjLmV4ZSBjb25maWcgVHJ1c3RlZEluc3RhbGxlciBiaW5QYXRoPSAiY21kLmV4ZSAvYyBwb3dlcnNoZWxsLmV4ZSAtZW5jb2RlZGNvbW1hbmQgJGJhc2U2NENvbW1hbmQiIHwgT3V0LU51bGwKICAgICAgICBzYy5leGUgc3RhcnQgVHJ1c3RlZEluc3RhbGxlciB8IE91dC1OdWxsCiAgICAgICAgc2MuZXhlIGNvbmZpZyBUcnVzdGVkSW5zdGFsbGVyIGJpbnBhdGg9ICJgIiREZWZhdWx0QmluUGF0aGAiIiB8IE91dC1OdWxsCiAgICAgICAgdHJ5IHsKICAgIAlTdG9wLVNlcnZpY2UgLU5hbWUgVHJ1c3RlZEluc3RhbGxlciAtRm9yY2UgLUVycm9yQWN0aW9uIFN0b3AgLVdhcm5pbmdBY3Rpb24gU3RvcAogIAkJfQogIAkJY2F0Y2ggewogICAgCXRhc2traWxsIC9pbSB0cnVzdGVkaW5zdGFsbGVyLmV4ZSAvZiA+JG51bGwKICAJCX0KICAgICAgICB9CgpXcml0ZS1Ib3N0ICJEZWZlbmRlcjogRGlzYWJsZS4uLmBuIgoKJHdpbmRvd3NzZWN1cml0eXNldHRpbmdzID0gQCgKIyB2aXJ1cyAmIHRocmVhdCBwcm90ZWN0aW9uIC0gbWFuYWdlIHNldHRpbmdzCiMgcmVhbCB0aW1lIHByb3RlY3Rpb24gLSBuZWVkcyBzYWZlIGJvb3QgYXMgdHJ1c3RlZCBpbnN0YWxsZXIgLSB3aW5kb3dzIHR1cm5zIHRoaXMgYmFjayBvbiBhdXRvbWF0aWNhbGx5CidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJcUmVhbC1UaW1lIFByb3RlY3Rpb25gIiAvdiBgIkRpc2FibGVSZWFsdGltZU1vbml0b3JpbmdgIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCgojIGRldiBkcml2ZSBwcm90ZWN0aW9uIC0gbmVlZHMgc2FmZSBib290IGFzIHRydXN0ZWQgaW5zdGFsbGVyCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJcUmVhbC1UaW1lIFByb3RlY3Rpb25gIiAvdiBgIkRpc2FibGVBc3luY1NjYW5Pbk9wZW5gIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCgojIGNsb3VkIGRlbGl2ZXJlZCBwcm90ZWN0aW9uIC0gbmVlZHMgc2FmZSBib290IGFzIHRydXN0ZWQgaW5zdGFsbGVyCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJcU3B5bmV0YCIgL3YgYCJTcHlOZXRSZXBvcnRpbmdgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCgojIGF1dG9tYXRpYyBzYW1wbGUgc3VibWlzc2lvbgonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIERlZmVuZGVyXFNweW5ldGAiIC92IGAiU3VibWl0U2FtcGxlc0NvbnNlbnRgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCgojIHRhbXBlciBwcm90ZWN0aW9uIC0gbmVlZHMgc2FmZSBib290IGFzIHRydXN0ZWQgaW5zdGFsbGVyCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJcRmVhdHVyZXNgIiAvdiBgIlRhbXBlclByb3RlY3Rpb25gIiAvdCBSRUdfRFdPUkQgL2QgYCI0YCIgL2YgPm51bCAyPiYxIicsCgojIHZpcnVzICYgdGhyZWF0IHByb3RlY3Rpb24gLSBtYW5hZ2UgcmFuc29td2FyZSBwcm90ZWN0aW9uCiMgY29udHJvbGxlZCBmb2xkZXIgYWNjZXNzCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJcV2luZG93cyBEZWZlbmRlciBFeHBsb2l0IEd1YXJkXENvbnRyb2xsZWQgRm9sZGVyIEFjY2Vzc2AiIC92IGAiRW5hYmxlQ29udHJvbGxlZEZvbGRlckFjY2Vzc2AiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgZmlyZXdhbGwgJiBuZXR3b3JrIHByb3RlY3Rpb24gLSBmaXJld2FsbCBub3RpZmljYXRpb24gc2V0dGluZ3MgLSBtYW5hZ2Ugbm90aWZpY2F0aW9ucwonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIERlZmVuZGVyIFNlY3VyaXR5IENlbnRlclxOb3RpZmljYXRpb25zYCIgL3YgYCJEaXNhYmxlRW5oYW5jZWROb3RpZmljYXRpb25zYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIERlZmVuZGVyIFNlY3VyaXR5IENlbnRlclxWaXJ1cyBhbmQgdGhyZWF0IHByb3RlY3Rpb25gIiAvdiBgIk5vQWN0aW9uTm90aWZpY2F0aW9uRGlzYWJsZWRgIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXIgU2VjdXJpdHkgQ2VudGVyXFZpcnVzIGFuZCB0aHJlYXQgcHJvdGVjdGlvbmAiIC92IGAiU3VtbWFyeU5vdGlmaWNhdGlvbkRpc2FibGVkYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIERlZmVuZGVyIFNlY3VyaXR5IENlbnRlclxWaXJ1cyBhbmQgdGhyZWF0IHByb3RlY3Rpb25gIiAvdiBgIkZpbGVzQmxvY2tlZE5vdGlmaWNhdGlvbkRpc2FibGVkYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXIgU2VjdXJpdHkgQ2VudGVyXEFjY291bnQgcHJvdGVjdGlvbmAiIC92IGAiRGlzYWJsZU5vdGlmaWNhdGlvbnNgIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlciBTZWN1cml0eSBDZW50ZXJcQWNjb3VudCBwcm90ZWN0aW9uYCIgL3YgYCJEaXNhYmxlRHluYW1pY2xvY2tOb3RpZmljYXRpb25zYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXIgU2VjdXJpdHkgQ2VudGVyXEFjY291bnQgcHJvdGVjdGlvbmAiIC92IGAiRGlzYWJsZVdpbmRvd3NIZWxsb05vdGlmaWNhdGlvbnNgIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU3lzdGVtXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2hhcmVkQWNjZXNzXEVwb2NoYCIgL3YgYCJFcG9jaGAiIC90IFJFR19EV09SRCAvZCBgIjEyMzFgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTaGFyZWRBY2Nlc3NcUGFyYW1ldGVyc1xGaXJld2FsbFBvbGljeVxEb21haW5Qcm9maWxlYCIgL3YgYCJEaXNhYmxlTm90aWZpY2F0aW9uc2AiIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTaGFyZWRBY2Nlc3NcUGFyYW1ldGVyc1xGaXJld2FsbFBvbGljeVxQdWJsaWNQcm9maWxlYCIgL3YgYCJEaXNhYmxlTm90aWZpY2F0aW9uc2AiIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTaGFyZWRBY2Nlc3NcUGFyYW1ldGVyc1xGaXJld2FsbFBvbGljeVxTdGFuZGFyZFByb2ZpbGVgIiAvdiBgIkRpc2FibGVOb3RpZmljYXRpb25zYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAoKIyBhcHAgJiBicm93c2VyIGNvbnRyb2wgLSBzbWFydCBhcHAgY29udHJvbCBzZXR0aW5ncwonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIERlZmVuZGVyYCIgL3YgYCJWZXJpZmllZEFuZFJlcHV0YWJsZVRydXN0TW9kZUVuYWJsZWRgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJgIiAvdiBgIlNtYXJ0TG9ja2VyTW9kZWAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlcmAiIC92IGAiUFVBUHJvdGVjdGlvbmAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxDb250cm9sXEFwcElEXENvbmZpZ3VyYXRpb25cU01BUlRMT0NLRVJgIiAvdiBgIlNUQVJUX1BFTkRJTkdgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU3lzdGVtXENvbnRyb2xTZXQwMDFcQ29udHJvbFxBcHBJRFxDb25maWd1cmF0aW9uXFNNQVJUTE9DS0VSYCIgL3YgYCJFTkFCTEVEYCIgL3QgUkVHX0JJTkFSWSAvZCBgIjAwMDAwMDAwMDAwMDAwMDBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxDb250cm9sXENJXFBvbGljeWAiIC92IGAiVmVyaWZpZWRBbmRSZXB1dGFibGVQb2xpY3lTdGF0ZWAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgYXBwICYgYnJvd3NlciBjb250cm9sIC0gcmVwdXRhdGlvbiBiYXNlZCBwcm90ZWN0aW9uIHNldHRpbmdzCiMgY2hlY2sgYXBwcyBhbmQgZmlsZXMKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlcmAiIC92IGAiU21hcnRTY3JlZW5FbmFibGVkYCIgL3QgUkVHX1NaIC9kIGAiT2ZmYCIgL2YgPm51bCAyPiYxIicsCgojIHNtYXJ0c2NyZWVuIGZvciBtaWNyb3NvZnQgZWRnZSAtIG5lZWRzIG5vcm1hbCBib290IGFzIGFkbWluCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcRWRnZVxTbWFydFNjcmVlbkVuYWJsZWRgIiAvdmUgL3QgUkVHX0RXT1JEIC9kIGAiMGAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XEVkZ2VcU21hcnRTY3JlZW5QdWFFbmFibGVkYCIgL3ZlIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgcGhpc2hpbmcgcHJvdGVjdGlvbgonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFdURFNcQ29tcG9uZW50c2AiIC92IGAiQ2FwdHVyZVRocmVhdFdpbmRvd2AiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxXVERTXENvbXBvbmVudHNgIiAvdiBgIk5vdGlmeU1hbGljaW91c2AiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxXVERTXENvbXBvbmVudHNgIiAvdiBgIk5vdGlmeVBhc3N3b3JkUmV1c2VgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cV1REU1xDb21wb25lbnRzYCIgL3YgYCJOb3RpZnlVbnNhZmVBcHBgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cV1REU1xDb21wb25lbnRzYCIgL3YgYCJTZXJ2aWNlRW5hYmxlZGAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgcG90ZW50aWFsbHkgdW53YW50ZWQgYXBwIGJsb2NraW5nCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJgIiAvdiBgIlBVQVByb3RlY3Rpb25gIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCgojIHNtYXJ0c2NyZWVuIGZvciBtaWNyb3NvZnQgc3RvcmUgYXBwcyAtIG5lZWRzIG5vcm1hbCBib290IGFzIGFkbWluCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxBcHBIb3N0YCIgL3YgYCJFbmFibGVXZWJDb250ZW50RXZhbHVhdGlvbmAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgYXBwICYgYnJvd3NlciBjb250cm9sIC0gZXhwbG9pdCBwcm90ZWN0aW9uIHNldHRpbmdzLCBsZWF2aW5nIGNmZyBjb250cm9sIGZsb3cgZ3VhcmQgb24gZm9yIHZhbmd1YXJkIGFudGljaGVhdAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXENvbnRyb2xcU2Vzc2lvbiBNYW5hZ2VyXGtlcm5lbGAiIC92IGAiTWl0aWdhdGlvbk9wdGlvbnNgIiAvdCBSRUdfQklOQVJZIC9kIGAiMjIyMjIyMDAwMDAxMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwYCIgL2YgPm51bCAyPiYxIicsCgojIGRldmljZSBzZWN1cml0eSAtIGNvcmUgaXNvbGF0aW9uIGRldGFpbHMKIyBtZW1vcnkgaW50ZWdyaXR5CidjbWQgL2MgInJlZyBkZWxldGUgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU3lzdGVtXENvbnRyb2xTZXQwMDFcQ29udHJvbFxEZXZpY2VHdWFyZFxTY2VuYXJpb3NcSHlwZXJ2aXNvckVuZm9yY2VkQ29kZUludGVncml0eWAiIC92IGAiQ2hhbmdlZEluQm9vdEN5Y2xlYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU3lzdGVtXENvbnRyb2xTZXQwMDFcQ29udHJvbFxEZXZpY2VHdWFyZFxTY2VuYXJpb3NcSHlwZXJ2aXNvckVuZm9yY2VkQ29kZUludGVncml0eWAiIC92IGAiRW5hYmxlZGAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGRlbGV0ZSBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxDb250cm9sXERldmljZUd1YXJkXFNjZW5hcmlvc1xIeXBlcnZpc29yRW5mb3JjZWRDb2RlSW50ZWdyaXR5YCIgL3YgYCJXYXNFbmFibGVkQnlgIiAvZiA+bnVsIDI+JjEiJywKCiMgdHVybiBvZmYgdmJzIHZpcnR1YWxpemF0aW9uIGJhc2VkIHNlY3VyaXR5CiMgZmFjZWl0IGFudGkgY2hlYXQgZm9yY2VzIHRoaXMgb24sIGV2ZW4gYWZ0ZXIgdW5pbnN0YWxsCidjbWQgL2MgImJjZGVkaXQgL2RlbGV0ZXZhbHVlIGFsbG93ZWRpbm1lbW9yeXNldHRpbmdzID5udWwgMj4mMSInLAonY21kIC9jICJiY2RlZGl0IC9kZWxldGV2YWx1ZSBpc29sYXRlZGNvbnRleHQgPm51bCAyPiYxIicsCidjbWQgL2MgImJjZGVkaXQgL2RlbGV0ZXZhbHVlIGh5cGVydmlzb3JsYXVuY2h0eXBlID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgZGVsZXRlIGAiSEtMTVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcQ29udHJvbFxEZXZpY2VHdWFyZGAiIC92IGAiRW5hYmxlVmlydHVhbGl6YXRpb25CYXNlZFNlY3VyaXR5YCIgL2YgPm51bCAyPiYxIicsCgojIGxvY2FsIHNlY3VyaXR5IGF1dGhvcml0eSBwcm90ZWN0aW9uCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXEN1cnJlbnRDb250cm9sU2V0XENvbnRyb2xcTHNhYCIgL3YgYCJSdW5Bc1BQTGAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgbWljcm9zb2Z0IHZ1bG5lcmFibGUgZHJpdmVyIGJsb2NrbGlzdAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXENvbnRyb2xcQ0lcQ29uZmlnYCIgL3YgYCJWdWxuZXJhYmxlRHJpdmVyQmxvY2tsaXN0RW5hYmxlYCIgL3QgUkVHX0RXT1JEIC9kIGAiMGAiIC9mID5udWwgMj4mMSInLAoKIyBkZWZlbmRlciBzZXJ2aWNlcwojIG1pY3Jvc29mdCBkZWZlbmRlciBhbnRpdmlydXMgbmV0d29yayBpbnNwZWN0aW9uIHNlcnZpY2UKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXZE5pc1N2Y2AiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCI0YCIgL2YgPm51bCAyPiYxIicsCgojIG1pY3Jvc29mdCBkZWZlbmRlciBhbnRpdmlydXMgc2VydmljZQonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdpbkRlZmVuZGAiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCI0YCIgL2YgPm51bCAyPiYxIicsCgojIG1pY3Jvc29mdCBkZWZlbmRlciBjb3JlIHNlcnZpY2UKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xNRENvcmVTdmNgIiAvdiBgIlN0YXJ0YCIgL3QgUkVHX0RXT1JEIC9kIGAiNGAiIC9mID5udWwgMj4mMSInLAoKIyBzZWN1cml0eSBjZW50ZXIKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x3c2NzdmNgIiAvdiBgIlN0YXJ0YCIgL3QgUkVHX0RXT1JEIC9kIGAiNGAiIC9mID5udWwgMj4mMSInLAoKIyB3ZWIgdGhyZWF0IGRlZmVuc2Ugc2VydmljZQonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdlYnRocmVhdGRlZnN2Y2AiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCI0YCIgL2YgPm51bCAyPiYxIicsCgojIHdlYiB0aHJlYXQgZGVmZW5zZSB1c2VyIHNlcnZpY2VfWFhYWFgKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x3ZWJ0aHJlYXRkZWZ1c2Vyc3ZjYCIgL3YgYCJTdGFydGAiIC90IFJFR19EV09SRCAvZCBgIjRgIiAvZiA+bnVsIDI+JjEiJywKCiMgd2luZG93cyBkZWZlbmRlciBhZHZhbmNlZCB0aHJlYXQgcHJvdGVjdGlvbiBzZXJ2aWNlCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2Vuc2VgIiAvdiBgIlN0YXJ0YCIgL3QgUkVHX0RXT1JEIC9kIGAiNGAiIC9mID5udWwgMj4mMSInLAoKIyB3aW5kb3dzIHNlY3VyaXR5IHNlcnZpY2UKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTZWN1cml0eUhlYWx0aFNlcnZpY2VgIiAvdiBgIlN0YXJ0YCIgL3QgUkVHX0RXT1JEIC9kIGAiNGAiIC9mID5udWwgMj4mMSInLAoKIyBkZWZlbmRlciBkcml2ZXJzCiMgbWljcm9zb2Z0IGRlZmVuZGVyIGFudGl2aXJ1cyBib290IGRyaXZlcgonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdkQm9vdGAiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCI0YCIgL2YgPm51bCAyPiYxIicsCgojIG1pY3Jvc29mdCBkZWZlbmRlciBhbnRpdmlydXMgbWluaS1maWx0ZXIgZHJpdmVyCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2RGaWx0ZXJgIiAvdiBgIlN0YXJ0YCIgL3QgUkVHX0RXT1JEIC9kIGAiNGAiIC9mID5udWwgMj4mMSInLAoKIyBtaWNyb3NvZnQgZGVmZW5kZXIgYW50aXZpcnVzIG5ldHdvcmsgaW5zcGVjdGlvbiBzeXN0ZW0gZHJpdmVyCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2ROaXNEcnZgIiAvdiBgIlN0YXJ0YCIgL3QgUkVHX0RXT1JEIC9kIGAiNGAiIC9mID5udWwgMj4mMSInCikKCiMgcnVuICR3aW5kb3dzc2VjdXJpdHlzZXR0aW5ncyBhcyBmdW5jdGlvbiB3aXRoIHRydXN0ZWQgaW5zdGFsbGVyCmZvcmVhY2ggKCRjb21tYW5kIGluICR3aW5kb3dzc2VjdXJpdHlzZXR0aW5ncykgewogICAgUnVuLVRydXN0ZWQgJGNvbW1hbmQKfQoKIyBydW4gJHdpbmRvd3NzZWN1cml0eXNldHRpbmdzIGFzIGFkbWluCmZvcmVhY2ggKCRjb21tYW5kIGluICR3aW5kb3dzc2VjdXJpdHlzZXR0aW5ncykgewogICAgSW52b2tlLUV4cHJlc3Npb24gJGNvbW1hbmQKfQoKIyBzdG9wIHNtYXJ0c2NyZWVuIHJ1bm5pbmcKU3RvcC1Qcm9jZXNzIC1Gb3JjZSAtTmFtZSBzbWFydHNjcmVlbiAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB8IE91dC1OdWxsCgojIG1vdmUgc21hcnRzY3JlZW4KUnVuLVRydXN0ZWQgImNtZCAvYyBtb3ZlIC95IGAiQzpcV2luZG93c1xTeXN0ZW0zMlxzbWFydHNjcmVlbi5leGVgIiBgIkM6XFdpbmRvd3Ncc21hcnRzY3JlZW4uZXhlYCIiCgojIHdpbmRvd3MgZGVmZW5kZXIgZGVmYXVsdCBkZWZpbml0aW9ucwpEaXNtIC9PbmxpbmUgL05vUmVzdGFydCAvRGlzYWJsZS1GZWF0dXJlIC9GZWF0dXJlTmFtZTpXaW5kb3dzLURlZmVuZGVyLURlZmF1bHQtRGVmaW5pdGlvbnMgfCBPdXQtTnVsbAoKIyBkZWZlbmRlciBjb250ZXh0IG1lbnUgaGFuZGxlcnMKY21kIC9jICJyZWcgZGVsZXRlIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXENsYXNzZXNcRGlyZWN0b3J5XHNoZWxsZXhcQ29udGV4dE1lbnVIYW5kbGVyc1xFUFBgIiAvZiA+bnVsIDI+JjEiCmNtZCAvYyAicmVnIGRlbGV0ZSBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxDbGFzc2VzXERyaXZlXHNoZWxsZXhcQ29udGV4dE1lbnVIYW5kbGVyc1xFUFBgIiAvZiA+bnVsIDI+JjEiCmNtZCAvYyAicmVnIGRlbGV0ZSBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxDbGFzc2VzXCpcc2hlbGxleFxDb250ZXh0TWVudUhhbmRsZXJzXEVQUGAiIC9mID5udWwgMj4mMSIKCiMgc2VjdXJpdHkgaGVhbHRoIHN5c3RlbSB0cmF5CmNtZCAvYyAicmVnIGRlbGV0ZSBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxSdW5gIiAvdiBgIlNlY3VyaXR5SGVhbHRoYCIgL2YgPm51bCAyPiYxIgoKIyByZW1vdmUgc2FmZSBtb2RlIGJvb3QKY21kIC9jICJiY2RlZGl0IC9kZWxldGV2YWx1ZSB7Y3VycmVudH0gc2FmZWJvb3QgPm51bCAyPiYxIgoKV3JpdGUtSG9zdCAiUmVzdGFydGluZ2BuIiAtRm9yZWdyb3VuZENvbG9yIFJlZAoKIyByZXN0YXJ0ClN0YXJ0LVNsZWVwIC1TZWNvbmRzIDUKc2h1dGRvd24gLXIgLXQgMDAK'
$sync.assets.defenderenable = '77u/IyBTQ1JJUFQgUlVOIEFTIEFETUlOCiAgICAgICAgSWYgKCEoW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzUHJpbmNpcGFsXVtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0lkZW50aXR5XTo6R2V0Q3VycmVudCgpKS5Jc0luUm9sZShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NCdWlsdEluUm9sZV0iQWRtaW5pc3RyYXRvciIpKQogICAgICAgIHtTdGFydC1Qcm9jZXNzIFBvd2VyU2hlbGwuZXhlIC1Bcmd1bWVudExpc3QgKCItTm9Qcm9maWxlIC1FeGVjdXRpb25Qb2xpY3kgQnlwYXNzIC1GaWxlIGAiezB9YCIiIC1mICRQU0NvbW1hbmRQYXRoKSAtVmVyYiBSdW5BcwogICAgICAgIEV4aXR9CiAgICAgICAgJEhvc3QuVUkuUmF3VUkuV2luZG93VGl0bGUgPSAkbXlJbnZvY2F0aW9uLk15Q29tbWFuZC5EZWZpbml0aW9uICsgIiAoQWRtaW5pc3RyYXRvcikiCiAgICAgICAgJEhvc3QuVUkuUmF3VUkuQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzRm9yZWdyb3VuZENvbG9yID0gIldoaXRlIgogICAgICAgIENsZWFyLUhvc3QKCiAgICAgICAgIyBGVU5DVElPTiBSVU4gQVMgVFJVU1RFRCBJTlNUQUxMRVIKICAgICAgICBmdW5jdGlvbiBSdW4tVHJ1c3RlZChbU3RyaW5nXSRjb21tYW5kKSB7CiAgICAgICAgdHJ5IHsKICAgIAlTdG9wLVNlcnZpY2UgLU5hbWUgVHJ1c3RlZEluc3RhbGxlciAtRm9yY2UgLUVycm9yQWN0aW9uIFN0b3AgLVdhcm5pbmdBY3Rpb24gU3RvcAogIAkJfQogIAkJY2F0Y2ggewogICAgCXRhc2traWxsIC9pbSB0cnVzdGVkaW5zdGFsbGVyLmV4ZSAvZiA+JG51bGwKICAJCX0KICAgICAgICAkc2VydmljZSA9IEdldC1DaW1JbnN0YW5jZSAtQ2xhc3NOYW1lIFdpbjMyX1NlcnZpY2UgLUZpbHRlciAiTmFtZT0nVHJ1c3RlZEluc3RhbGxlciciCiAgICAgICAgJERlZmF1bHRCaW5QYXRoID0gJHNlcnZpY2UuUGF0aE5hbWUKICAJCSR0cnVzdGVkSW5zdGFsbGVyUGF0aCA9ICIkZW52OlN5c3RlbVJvb3Rcc2VydmljaW5nXFRydXN0ZWRJbnN0YWxsZXIuZXhlIgogIAkJaWYgKCREZWZhdWx0QmluUGF0aCAtbmUgJHRydXN0ZWRJbnN0YWxsZXJQYXRoKSB7CiAgICAJJERlZmF1bHRCaW5QYXRoID0gJHRydXN0ZWRJbnN0YWxsZXJQYXRoCiAgCQl9CiAgICAgICAgJGJ5dGVzID0gW1N5c3RlbS5UZXh0LkVuY29kaW5nXTo6VW5pY29kZS5HZXRCeXRlcygkY29tbWFuZCkKICAgICAgICAkYmFzZTY0Q29tbWFuZCA9IFtDb252ZXJ0XTo6VG9CYXNlNjRTdHJpbmcoJGJ5dGVzKQogICAgICAgIHNjLmV4ZSBjb25maWcgVHJ1c3RlZEluc3RhbGxlciBiaW5QYXRoPSAiY21kLmV4ZSAvYyBwb3dlcnNoZWxsLmV4ZSAtZW5jb2RlZGNvbW1hbmQgJGJhc2U2NENvbW1hbmQiIHwgT3V0LU51bGwKICAgICAgICBzYy5leGUgc3RhcnQgVHJ1c3RlZEluc3RhbGxlciB8IE91dC1OdWxsCiAgICAgICAgc2MuZXhlIGNvbmZpZyBUcnVzdGVkSW5zdGFsbGVyIGJpbnBhdGg9ICJgIiREZWZhdWx0QmluUGF0aGAiIiB8IE91dC1OdWxsCiAgICAgICAgdHJ5IHsKICAgIAlTdG9wLVNlcnZpY2UgLU5hbWUgVHJ1c3RlZEluc3RhbGxlciAtRm9yY2UgLUVycm9yQWN0aW9uIFN0b3AgLVdhcm5pbmdBY3Rpb24gU3RvcAogIAkJfQogIAkJY2F0Y2ggewogICAgCXRhc2traWxsIC9pbSB0cnVzdGVkaW5zdGFsbGVyLmV4ZSAvZiA+JG51bGwKICAJCX0KICAgICAgICB9CgpXcml0ZS1Ib3N0ICJEZWZlbmRlcjogRW5hYmxlLi4uYG4iCgokd2luZG93c3NlY3VyaXR5c2V0dGluZ3MgPSBAKAojIHZpcnVzICYgdGhyZWF0IHByb3RlY3Rpb24gLSBtYW5hZ2Ugc2V0dGluZ3MKIyByZWFsIHRpbWUgcHJvdGVjdGlvbiAtIG5lZWRzIHNhZmUgYm9vdCBhcyB0cnVzdGVkIGluc3RhbGxlciAtIHdpbmRvd3MgdHVybnMgdGhpcyBiYWNrIG9uIGF1dG9tYXRpY2FsbHkKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlclxSZWFsLVRpbWUgUHJvdGVjdGlvbmAiIC92IGAiRGlzYWJsZVJlYWx0aW1lTW9uaXRvcmluZ2AiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgZGV2IGRyaXZlIHByb3RlY3Rpb24gLSBuZWVkcyBzYWZlIGJvb3QgYXMgdHJ1c3RlZCBpbnN0YWxsZXIKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlclxSZWFsLVRpbWUgUHJvdGVjdGlvbmAiIC92IGAiRGlzYWJsZUFzeW5jU2Nhbk9uT3BlbmAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgY2xvdWQgZGVsaXZlcmVkIHByb3RlY3Rpb24gLSBuZWVkcyBzYWZlIGJvb3QgYXMgdHJ1c3RlZCBpbnN0YWxsZXIKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlclxTcHluZXRgIiAvdiBgIlNweU5ldFJlcG9ydGluZ2AiIC90IFJFR19EV09SRCAvZCBgIjJgIiAvZiA+bnVsIDI+JjEiJywKCiMgYXV0b21hdGljIHNhbXBsZSBzdWJtaXNzaW9uCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJcU3B5bmV0YCIgL3YgYCJTdWJtaXRTYW1wbGVzQ29uc2VudGAiIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKCiMgdGFtcGVyIHByb3RlY3Rpb24gLSBuZWVkcyBzYWZlIGJvb3QgYXMgdHJ1c3RlZCBpbnN0YWxsZXIKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlclxGZWF0dXJlc2AiIC92IGAiVGFtcGVyUHJvdGVjdGlvbmAiIC90IFJFR19EV09SRCAvZCBgIjVgIiAvZiA+bnVsIDI+JjEiJywKCiMgdmlydXMgJiB0aHJlYXQgcHJvdGVjdGlvbiAtIG1hbmFnZSByYW5zb213YXJlIHByb3RlY3Rpb24KIyBjb250cm9sbGVkIGZvbGRlciBhY2Nlc3MKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlclxXaW5kb3dzIERlZmVuZGVyIEV4cGxvaXQgR3VhcmRcQ29udHJvbGxlZCBGb2xkZXIgQWNjZXNzYCIgL3YgYCJFbmFibGVDb250cm9sbGVkRm9sZGVyQWNjZXNzYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAoKIyBmaXJld2FsbCAmIG5ldHdvcmsgcHJvdGVjdGlvbiAtIGZpcmV3YWxsIG5vdGlmaWNhdGlvbiBzZXR0aW5ncyAtIG1hbmFnZSBub3RpZmljYXRpb25zCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXIgU2VjdXJpdHkgQ2VudGVyXE5vdGlmaWNhdGlvbnNgIiAvdiBgIkRpc2FibGVFbmhhbmNlZE5vdGlmaWNhdGlvbnNgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXIgU2VjdXJpdHkgQ2VudGVyXFZpcnVzIGFuZCB0aHJlYXQgcHJvdGVjdGlvbmAiIC92IGAiTm9BY3Rpb25Ob3RpZmljYXRpb25EaXNhYmxlZGAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlciBTZWN1cml0eSBDZW50ZXJcVmlydXMgYW5kIHRocmVhdCBwcm90ZWN0aW9uYCIgL3YgYCJTdW1tYXJ5Tm90aWZpY2F0aW9uRGlzYWJsZWRgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXIgU2VjdXJpdHkgQ2VudGVyXFZpcnVzIGFuZCB0aHJlYXQgcHJvdGVjdGlvbmAiIC92IGAiRmlsZXNCbG9ja2VkTm90aWZpY2F0aW9uRGlzYWJsZWRgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlciBTZWN1cml0eSBDZW50ZXJcQWNjb3VudCBwcm90ZWN0aW9uYCIgL3YgYCJEaXNhYmxlTm90aWZpY2F0aW9uc2AiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIERlZmVuZGVyIFNlY3VyaXR5IENlbnRlclxBY2NvdW50IHByb3RlY3Rpb25gIiAvdiBgIkRpc2FibGVEeW5hbWljbG9ja05vdGlmaWNhdGlvbnNgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlciBTZWN1cml0eSBDZW50ZXJcQWNjb3VudCBwcm90ZWN0aW9uYCIgL3YgYCJEaXNhYmxlV2luZG93c0hlbGxvTm90aWZpY2F0aW9uc2AiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTaGFyZWRBY2Nlc3NcRXBvY2hgIiAvdiBgIkVwb2NoYCIgL3QgUkVHX0RXT1JEIC9kIGAiMTIyOGAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNoYXJlZEFjY2Vzc1xQYXJhbWV0ZXJzXEZpcmV3YWxsUG9saWN5XERvbWFpblByb2ZpbGVgIiAvdiBgIkRpc2FibGVOb3RpZmljYXRpb25zYCIgL3QgUkVHX0RXT1JEIC9kIGAiMGAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNoYXJlZEFjY2Vzc1xQYXJhbWV0ZXJzXEZpcmV3YWxsUG9saWN5XFB1YmxpY1Byb2ZpbGVgIiAvdiBgIkRpc2FibGVOb3RpZmljYXRpb25zYCIgL3QgUkVHX0RXT1JEIC9kIGAiMGAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNoYXJlZEFjY2Vzc1xQYXJhbWV0ZXJzXEZpcmV3YWxsUG9saWN5XFN0YW5kYXJkUHJvZmlsZWAiIC92IGAiRGlzYWJsZU5vdGlmaWNhdGlvbnNgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCgojIGFwcCAmIGJyb3dzZXIgY29udHJvbCAtIHNtYXJ0IGFwcCBjb250cm9sIHNldHRpbmdzCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJgIiAvdiBgIlZlcmlmaWVkQW5kUmVwdXRhYmxlVHJ1c3RNb2RlRW5hYmxlZGAiIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBEZWZlbmRlcmAiIC92IGAiU21hcnRMb2NrZXJNb2RlYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIERlZmVuZGVyYCIgL3YgYCJQVUFQcm90ZWN0aW9uYCIgL3QgUkVHX0RXT1JEIC9kIGAiMmAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXENvbnRyb2xcQXBwSURcQ29uZmlndXJhdGlvblxTTUFSVExPQ0tFUmAiIC92IGAiU1RBUlRfUEVORElOR2AiIC90IFJFR19EV09SRCAvZCBgIjRgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxDb250cm9sXEFwcElEXENvbmZpZ3VyYXRpb25cU01BUlRMT0NLRVJgIiAvdiBgIkVOQUJMRURgIiAvdCBSRUdfQklOQVJZIC9kIGAiMDQwMDAwMDAwMDAwMDAwMGAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXENvbnRyb2xcQ0lcUG9saWN5YCIgL3YgYCJWZXJpZmllZEFuZFJlcHV0YWJsZVBvbGljeVN0YXRlYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAoKIyBhcHAgJiBicm93c2VyIGNvbnRyb2wgLSByZXB1dGF0aW9uIGJhc2VkIHByb3RlY3Rpb24gc2V0dGluZ3MKIyBjaGVjayBhcHBzIGFuZCBmaWxlcwonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyYCIgL3YgYCJTbWFydFNjcmVlbkVuYWJsZWRgIiAvdCBSRUdfU1ogL2QgYCJXYXJuYCIgL2YgPm51bCAyPiYxIicsCgojIHNtYXJ0c2NyZWVuIGZvciBtaWNyb3NvZnQgZWRnZSAtIG5lZWRzIG5vcm1hbCBib290IGFzIGFkbWluCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcRWRnZVxTbWFydFNjcmVlbkVuYWJsZWRgIiAvdmUgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XEVkZ2VcU21hcnRTY3JlZW5QdWFFbmFibGVkYCIgL3ZlIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKCiMgcGhpc2hpbmcgcHJvdGVjdGlvbgonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFdURFNcQ29tcG9uZW50c2AiIC92IGAiQ2FwdHVyZVRocmVhdFdpbmRvd2AiIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxXVERTXENvbXBvbmVudHNgIiAvdiBgIk5vdGlmeU1hbGljaW91c2AiIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxXVERTXENvbXBvbmVudHNgIiAvdiBgIk5vdGlmeVBhc3N3b3JkUmV1c2VgIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cV1REU1xDb21wb25lbnRzYCIgL3YgYCJOb3RpZnlVbnNhZmVBcHBgIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cV1REU1xDb21wb25lbnRzYCIgL3YgYCJTZXJ2aWNlRW5hYmxlZGAiIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKCiMgcG90ZW50aWFsbHkgdW53YW50ZWQgYXBwIGJsb2NraW5nCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgRGVmZW5kZXJgIiAvdiBgIlBVQVByb3RlY3Rpb25gIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCgojIHNtYXJ0c2NyZWVuIGZvciBtaWNyb3NvZnQgc3RvcmUgYXBwcyAtIG5lZWRzIG5vcm1hbCBib290IGFzIGFkbWluCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxBcHBIb3N0YCIgL3YgYCJFbmFibGVXZWJDb250ZW50RXZhbHVhdGlvbmAiIC90IFJFR19EV09SRCAvZCBgIjFgIiAvZiA+bnVsIDI+JjEiJywKCiMgYXBwICYgYnJvd3NlciBjb250cm9sIC0gZXhwbG9pdCBwcm90ZWN0aW9uIHNldHRpbmdzCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU3lzdGVtXENvbnRyb2xTZXQwMDFcQ29udHJvbFxTZXNzaW9uIE1hbmFnZXJca2VybmVsYCIgL3YgYCJNaXRpZ2F0aW9uT3B0aW9uc2AiIC90IFJFR19CSU5BUlkgL2QgYCIxMTExMTEwMDAwMDEwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDBgIiAvZiA+bnVsIDI+JjEiJywKCiMgZGV2aWNlIHNlY3VyaXR5IC0gY29yZSBpc29sYXRpb24gZGV0YWlscwojIG1lbW9yeSBpbnRlZ3JpdHkKJ2NtZCAvYyAicmVnIGRlbGV0ZSBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxDb250cm9sXERldmljZUd1YXJkXFNjZW5hcmlvc1xIeXBlcnZpc29yRW5mb3JjZWRDb2RlSW50ZWdyaXR5YCIgL3YgYCJDaGFuZ2VkSW5Cb290Q3ljbGVgIiAvZiA+bnVsIDI+JjEiJywKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTeXN0ZW1cQ29udHJvbFNldDAwMVxDb250cm9sXERldmljZUd1YXJkXFNjZW5hcmlvc1xIeXBlcnZpc29yRW5mb3JjZWRDb2RlSW50ZWdyaXR5YCIgL3YgYCJFbmFibGVkYCIgL3QgUkVHX0RXT1JEIC9kIGAiMWAiIC9mID5udWwgMj4mMSInLAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXENvbnRyb2xcRGV2aWNlR3VhcmRcU2NlbmFyaW9zXEh5cGVydmlzb3JFbmZvcmNlZENvZGVJbnRlZ3JpdHlgIiAvdiBgIldhc0VuYWJsZWRCeWAiIC90IFJFR19EV09SRCAvZCBgIjJgIiAvZiA+bnVsIDI+JjEiJywKCiMgbG9jYWwgc2VjdXJpdHkgYXV0aG9yaXR5IHByb3RlY3Rpb24KJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcQ29udHJvbFxMc2FgIiAvdiBgIlJ1bkFzUFBMYCIgL3QgUkVHX0RXT1JEIC9kIGAiMmAiIC9mID5udWwgMj4mMSInLAoKIyBtaWNyb3NvZnQgdnVsbmVyYWJsZSBkcml2ZXIgYmxvY2tsaXN0CidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU3lzdGVtXENvbnRyb2xTZXQwMDFcQ29udHJvbFxDSVxDb25maWdgIiAvdiBgIlZ1bG5lcmFibGVEcml2ZXJCbG9ja2xpc3RFbmFibGVgIiAvdCBSRUdfRFdPUkQgL2QgYCIxYCIgL2YgPm51bCAyPiYxIicsCgojIGRlZmVuZGVyIHNlcnZpY2VzCiMgbWljcm9zb2Z0IGRlZmVuZGVyIGFudGl2aXJ1cyBuZXR3b3JrIGluc3BlY3Rpb24gc2VydmljZQonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdkTmlzU3ZjYCIgL3YgYCJTdGFydGAiIC90IFJFR19EV09SRCAvZCBgIjNgIiAvZiA+bnVsIDI+JjEiJywKCiMgbWljcm9zb2Z0IGRlZmVuZGVyIGFudGl2aXJ1cyBzZXJ2aWNlCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2luRGVmZW5kYCIgL3YgYCJTdGFydGAiIC90IFJFR19EV09SRCAvZCBgIjJgIiAvZiA+bnVsIDI+JjEiJywKCiMgbWljcm9zb2Z0IGRlZmVuZGVyIGNvcmUgc2VydmljZQonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE1EQ29yZVN2Y2AiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCIyYCIgL2YgPm51bCAyPiYxIicsCgojIHNlY3VyaXR5IGNlbnRlcgonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdzY3N2Y2AiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCIyYCIgL2YgPm51bCAyPiYxIicsCgojIHdlYiB0aHJlYXQgZGVmZW5zZSBzZXJ2aWNlCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd2VidGhyZWF0ZGVmc3ZjYCIgL3YgYCJTdGFydGAiIC90IFJFR19EV09SRCAvZCBgIjNgIiAvZiA+bnVsIDI+JjEiJywKCiMgd2ViIHRocmVhdCBkZWZlbnNlIHVzZXIgc2VydmljZV9YWFhYWAonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdlYnRocmVhdGRlZnVzZXJzdmNgIiAvdiBgIlN0YXJ0YCIgL3QgUkVHX0RXT1JEIC9kIGAiMmAiIC9mID5udWwgMj4mMSInLAoKIyB3aW5kb3dzIGRlZmVuZGVyIGFkdmFuY2VkIHRocmVhdCBwcm90ZWN0aW9uIHNlcnZpY2UKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTZW5zZWAiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCIzYCIgL2YgPm51bCAyPiYxIicsCgojIHdpbmRvd3Mgc2VjdXJpdHkgc2VydmljZQonY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNlY3VyaXR5SGVhbHRoU2VydmljZWAiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCIyYCIgL2YgPm51bCAyPiYxIicsCgojIGRlZmVuZGVyIGRyaXZlcnMKIyBtaWNyb3NvZnQgZGVmZW5kZXIgYW50aXZpcnVzIGJvb3QgZHJpdmVyCidjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2RCb290YCIgL3YgYCJTdGFydGAiIC90IFJFR19EV09SRCAvZCBgIjBgIiAvZiA+bnVsIDI+JjEiJywKCiMgbWljcm9zb2Z0IGRlZmVuZGVyIGFudGl2aXJ1cyBtaW5pLWZpbHRlciBkcml2ZXIKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXZEZpbHRlcmAiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCIwYCIgL2YgPm51bCAyPiYxIicsCgojIG1pY3Jvc29mdCBkZWZlbmRlciBhbnRpdmlydXMgbmV0d29yayBpbnNwZWN0aW9uIHN5c3RlbSBkcml2ZXIKJ2NtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXZE5pc0RydmAiIC92IGAiU3RhcnRgIiAvdCBSRUdfRFdPUkQgL2QgYCIzYCIgL2YgPm51bCAyPiYxIicKKQoKIyBydW4gJHdpbmRvd3NzZWN1cml0eXNldHRpbmdzIGFzIGZ1bmN0aW9uIHdpdGggdHJ1c3RlZCBpbnN0YWxsZXIKZm9yZWFjaCAoJGNvbW1hbmQgaW4gJHdpbmRvd3NzZWN1cml0eXNldHRpbmdzKSB7CiAgICBSdW4tVHJ1c3RlZCAkY29tbWFuZAp9CgojIHJ1biAkd2luZG93c3NlY3VyaXR5c2V0dGluZ3MgYXMgYWRtaW4KZm9yZWFjaCAoJGNvbW1hbmQgaW4gJHdpbmRvd3NzZWN1cml0eXNldHRpbmdzKSB7CiAgICBJbnZva2UtRXhwcmVzc2lvbiAkY29tbWFuZAp9CgojIHN0b3Agc21hcnRzY3JlZW4gcnVubmluZwpTdG9wLVByb2Nlc3MgLUZvcmNlIC1OYW1lIHNtYXJ0c2NyZWVuIC1FcnJvckFjdGlvbiBTaWxlbnRseUNvbnRpbnVlIHwgT3V0LU51bGwKCiMgbW92ZSBzbWFydHNjcmVlbgpSdW4tVHJ1c3RlZCAiY21kIC9jIG1vdmUgL3kgYCJDOlxXaW5kb3dzXHNtYXJ0c2NyZWVuLmV4ZWAiIGAiQzpcV2luZG93c1xTeXN0ZW0zMlxzbWFydHNjcmVlbi5leGVgIiIKCiMgd2luZG93cyBkZWZlbmRlciBkZWZhdWx0IGRlZmluaXRpb25zIChjYW4ndCB0dXJuIGJhY2sgb24pCiMgRGlzbSAvT25saW5lIC9Ob1Jlc3RhcnQgL0VuYWJsZS1GZWF0dXJlIC9GZWF0dXJlTmFtZTpXaW5kb3dzLURlZmVuZGVyLURlZmF1bHQtRGVmaW5pdGlvbnMgfCBPdXQtTnVsbAoKIyBkZWZlbmRlciBjb250ZXh0IG1lbnUgaGFuZGxlcnMKY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXENsYXNzZXNcRGlyZWN0b3J5XHNoZWxsZXhcQ29udGV4dE1lbnVIYW5kbGVyc1xFUFBgIiAvdmUgL2QgYCJ7MDlBNDc4NjAtMTFCMC00REE1LUFGQTUtMjZEODYxOThBNzgwfWAiIC9mID5udWwgMj4mMSIKY21kIC9jICJyZWcgYWRkIGAiSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXENsYXNzZXNcRHJpdmVcc2hlbGxleFxDb250ZXh0TWVudUhhbmRsZXJzXEVQUGAiIC92ZSAvZCBgInswOUE0Nzg2MC0xMUIwLTREQTUtQUZBNS0yNkQ4NjE5OEE3ODB9YCIgL2YgPm51bCAyPiYxIgpjbWQgL2MgInJlZyBhZGQgYCJIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcQ2xhc3Nlc1wqXHNoZWxsZXhcQ29udGV4dE1lbnVIYW5kbGVyc1xFUFBgIiAvdmUgL2QgYCJ7MDlBNDc4NjAtMTFCMC00REE1LUFGQTUtMjZEODYxOThBNzgwfWAiIC9mID5udWwgMj4mMSIKCiMgc2VjdXJpdHkgaGVhbHRoIHN5c3RlbSB0cmF5CmNtZCAvYyAicmVnIGFkZCBgIkhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxSdW5gIiAvdiBgIlNlY3VyaXR5SGVhbHRoYCIgL3QgUkVHX0VYUEFORF9TWiAvZCBgIiV3aW5kaXIlXHN5c3RlbTMyXFNlY3VyaXR5SGVhbHRoU3lzdHJheS5leGVgIiAvZiA+bnVsIDI+JjEiCgojIHJlbW92ZSBzYWZlIG1vZGUgYm9vdApjbWQgL2MgImJjZGVkaXQgL2RlbGV0ZXZhbHVlIHtjdXJyZW50fSBzYWZlYm9vdCA+bnVsIDI+JjEiCgpXcml0ZS1Ib3N0ICJSZXN0YXJ0aW5nYG4iIC1Gb3JlZ3JvdW5kQ29sb3IgUmVkCgojIHJlc3RhcnQKU3RhcnQtU2xlZXAgLVNlY29uZHMgNQpzaHV0ZG93biAtciAtdCAwMAo='
$sync.assets.inspector_default = '77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTE2Ij8+CjxBcnJheU9mUHJvZmlsZT4KICA8UHJvZmlsZT4KICAgIDxQcm9maWxlTmFtZT5CYXNlIFByb2ZpbGU8L1Byb2ZpbGVOYW1lPgogICAgPEV4ZWN1dGFibGVzLz4KICAgIDxTZXR0aW5ncz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+RnJhbWUgUmF0ZSBMaW1pdGVyIFYzPC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD4yNzcwNDExNTQ8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjA8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPkdTWU5DIC0gQXBwbGljYXRpb24gTW9kZTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+Mjk0OTczNzg0PC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4wPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPgogICAgICA8UHJvZmlsZVNldHRpbmc+CiAgICAgICAgPFNldHRpbmdOYW1lSW5mbz5HU1lOQyAtIEFwcGxpY2F0aW9uIFN0YXRlPC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD4yNzk0NzY2ODc8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjQ8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPkdTWU5DIC0gR2xvYmFsIEZlYXR1cmU8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjI3ODE5NjU2NzwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MDwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+R1NZTkMgLSBHbG9iYWwgTW9kZTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+Mjc4MTk2NzI3PC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4wPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPgogICAgICA8UHJvZmlsZVNldHRpbmc+CiAgICAgICAgPFNldHRpbmdOYW1lSW5mbz5HU1lOQyAtIEluZGljYXRvciBPdmVybGF5PC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD4yNjg2MDQ3Mjg8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjA8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPk1heGltdW0gUHJlLVJlbmRlcmVkIEZyYW1lczwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+ODEwMjA0NjwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MTwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+UHJlZmVycmVkIFJlZnJlc2ggUmF0ZTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+NjYwMDAwMTwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MTwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+VWx0cmEgTG93IExhdGVuY3kgLSBDUEwgU3RhdGU8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjM5MDQ2NzwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MjwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+VWx0cmEgTG93IExhdGVuY3kgLSBFbmFibGVkPC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD4yNzcwNDExNTI8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjE8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPlZlcnRpY2FsIFN5bmM8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjExMDQxMjMxPC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4xMzg1MDQwMDc8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPlZlcnRpY2FsIFN5bmMgLSBTbW9vdGggQUZSIEJlaGF2aW9yPC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD4yNzAxOTg2Mjc8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjA8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPlZlcnRpY2FsIFN5bmMgLSBUZWFyIENvbnRyb2w8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjU5MTI0MTI8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjI1MjUzNjg0Mzk8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPlZ1bGthbi9PcGVuR0wgUHJlc2VudCBNZXRob2Q8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjU1MDkzMjcyODwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MDwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+QW50aWFsaWFzaW5nIC0gR2FtbWEgQ29ycmVjdGlvbjwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+Mjc2NjUyOTU3PC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4wPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPgogICAgICA8UHJvZmlsZVNldHRpbmc+CiAgICAgICAgPFNldHRpbmdOYW1lSW5mbz5BbnRpYWxpYXNpbmcgLSBNb2RlPC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD4yNzY3NTc1OTU8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjE8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPkFudGlhbGlhc2luZyAtIFNldHRpbmc8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjI4MjU1NTM0NjwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MDwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+QW5pc290cm9waWMgRmlsdGVyIC0gT3B0aW1pemF0aW9uPC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD44NzAzMzQ0PC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4xPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPgogICAgICA8UHJvZmlsZVNldHRpbmc+CiAgICAgICAgPFNldHRpbmdOYW1lSW5mbz5Bbmlzb3Ryb3BpYyBGaWx0ZXIgLSBTYW1wbGUgT3B0aW1pemF0aW9uPC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD4xNTE1MTYzMzwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MTwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+QW5pc290cm9waWMgRmlsdGVyaW5nIC0gTW9kZTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+MjgyMjQ1OTEwPC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4xPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPgogICAgICA8UHJvZmlsZVNldHRpbmc+CiAgICAgICAgPFNldHRpbmdOYW1lSW5mbz5Bbmlzb3Ryb3BpYyBGaWx0ZXJpbmcgLSBTZXR0aW5nPC9TZXR0aW5nTmFtZUluZm8+CiAgICAgICAgPFNldHRpbmdJRD4yNzA0MjY1Mzc8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjE8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPlRleHR1cmUgRmlsdGVyaW5nIC0gTmVnYXRpdmUgTE9EIEJpYXM8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjE2ODYzNzY8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjA8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPlRleHR1cmUgRmlsdGVyaW5nIC0gUXVhbGl0eTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+MTM1MTAyODk8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjIwPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPgogICAgICA8UHJvZmlsZVNldHRpbmc+CiAgICAgICAgPFNldHRpbmdOYW1lSW5mbz5UZXh0dXJlIEZpbHRlcmluZyAtIFRyaWxpbmVhciBPcHRpbWl6YXRpb248L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjMwNjY2MTA8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjA8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPkNVREEgLSBGb3JjZSBQMiBTdGF0ZTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+MTM0MzY0NjgxNDwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MDwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KCSAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+Q1VEQSAtIFN5c21lbSBGYWxsYmFjayBQb2xpY3k8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjI4Mzk2MjU2OTwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MTwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+UG93ZXIgTWFuYWdlbWVudCAtIE1vZGU8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjI3NDE5NzM2MTwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MTwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+U2hhZGVyIENhY2hlIC0gQ2FjaGUgU2l6ZTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+MTEzMDYxMzU8L1NldHRpbmdJRD4KICAgICAgICA8U2V0dGluZ1ZhbHVlPjQyOTQ5NjcyOTU8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPkR3b3JkPC9WYWx1ZVR5cGU+CiAgICAgIDwvUHJvZmlsZVNldHRpbmc+CiAgICAgIDxQcm9maWxlU2V0dGluZz4KICAgICAgICA8U2V0dGluZ05hbWVJbmZvPlRocmVhZGVkIE9wdGltaXphdGlvbjwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+NTQ5NTI4MDk0PC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4xPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPgogICAgICA8UHJvZmlsZVNldHRpbmc+CiAgICAgICAgPFNldHRpbmdOYW1lSW5mbz5PcGVuR0wgR0RJIENvbXBhdGliaWxpdHk8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjU0NDM5MjYxMTwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+MDwvU2V0dGluZ1ZhbHVlPgogICAgICAgIDxWYWx1ZVR5cGU+RHdvcmQ8L1ZhbHVlVHlwZT4KICAgICAgPC9Qcm9maWxlU2V0dGluZz4KICAgICAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+UHJlZmVycmVkIE9wZW5HTCBHUFU8L1NldHRpbmdOYW1lSW5mbz4KICAgICAgICA8U2V0dGluZ0lEPjU1MDU2NDgzODwvU2V0dGluZ0lEPgogICAgICAgIDxTZXR0aW5nVmFsdWU+aWQsMi4wOjI2ODQxMERFLDAwMDAwMTAwLEdGIC0gKDQwMCwyLDE2MSwyNDU2NCkgQCAoMCk8L1NldHRpbmdWYWx1ZT4KICAgICAgICA8VmFsdWVUeXBlPlN0cmluZzwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPgogICAgPC9TZXR0aW5ncz4KICA8L1Byb2ZpbGU+CjwvQXJyYXlPZlByb2ZpbGU+Cg=='
$sync.assets.inspector_empty = '77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTE2Ij8+CjxBcnJheU9mUHJvZmlsZT4KICA8UHJvZmlsZT4KICAgIDxQcm9maWxlTmFtZT5CYXNlIFByb2ZpbGU8L1Byb2ZpbGVOYW1lPgogICAgPEV4ZWN1dGVhYmxlcy8+CiAgICA8U2V0dGluZ3MvPgogIDwvUHJvZmlsZT4KPC9BcnJheU9mUHJvZmlsZT4K'
$sync.assets.inspector_forceoff = '77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTE2Ij8+CjxBcnJheU9mUHJvZmlsZT4KICA8UHJvZmlsZT4KICAgIDxQcm9maWxlTmFtZT5CYXNlIFByb2ZpbGU8L1Byb2ZpbGVOYW1lPgogICAgPEV4ZWN1dGFibGVzLz4KICAgIDxTZXR0aW5ncz4KCSAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+ckJBUiAtIEVuYWJsZTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+OTgzMjI2PC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4wPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPiAgCiAgICA8L1NldHRpbmdzPgogIDwvUHJvZmlsZT4KPC9BcnJheU9mUHJvZmlsZT4K'
$sync.assets.inspector_forceon = '77u/PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTE2Ij8+CjxBcnJheU9mUHJvZmlsZT4KICA8UHJvZmlsZT4KICAgIDxQcm9maWxlTmFtZT5CYXNlIFByb2ZpbGU8L1Byb2ZpbGVOYW1lPgogICAgPEV4ZWN1dGFibGVzLz4KICAgIDxTZXR0aW5ncz4KCSAgPFByb2ZpbGVTZXR0aW5nPgogICAgICAgIDxTZXR0aW5nTmFtZUluZm8+ckJBUiAtIEVuYWJsZTwvU2V0dGluZ05hbWVJbmZvPgogICAgICAgIDxTZXR0aW5nSUQ+OTgzMjI2PC9TZXR0aW5nSUQ+CiAgICAgICAgPFNldHRpbmdWYWx1ZT4xPC9TZXR0aW5nVmFsdWU+CiAgICAgICAgPFZhbHVlVHlwZT5Ed29yZDwvVmFsdWVUeXBlPgogICAgICA8L1Byb2ZpbGVTZXR0aW5nPiAgCiAgICA8L1NldHRpbmdzPgogIDwvUHJvZmlsZT4KPC9BcnJheU9mUHJvZmlsZT4K'
$sync.assets.msiab_config = '77u/W0ZuT2Zmc2V0Q2FjaGU2NF0KT1M9Ni4yIEJ1aWxkIDkyMDAKRERSQVcuRExMPTAwMDlBMDAwIEFCRTMwQkIxCklEaXJlY3REcmF3U3VyZmFjZTc6OkZsaXA9MDAwMkQ1RjAKRDNEOS5ETEw9MDAxQjMyMzggNzcyRDhGRTUKSURpcmVjdDNERGV2aWNlOTo6UHJlc2VudD0wMDA2NDA2MApJRGlyZWN0M0REZXZpY2U5OjpSZWxlYXNlPTAwMDEzN0MwCklEaXJlY3QzRERldmljZTk6OlJlc2V0PTAwMEUxN0UwCklEaXJlY3QzRFN3YXBDaGFpbjk6OlByZXNlbnQ9MDAwODFBNjAKSURpcmVjdDNERGV2aWNlOUV4OjpQcmVzZW50RXg9MDAwMUM2QTAKSURpcmVjdDNERGV2aWNlOUV4OjpSZXNldEV4PTAwMDI4NEEwCkRYR0kuRExMPTAwMTNEMUUwIDFBQTRENkYwCklEWEdJU3dhcENoYWluOjpQcmVzZW50PTAwMDAyQ0EwCklEWEdJU3dhcENoYWluOjpSZXNpemVCdWZmZXJzPTAwMDM4QzYwCklEWEdJU3dhcENoYWluOjpSZWxlYXNlPTAwMDMwNzkwCklEWEdJU3dhcENoYWluMTo6UHJlc2VudDE9MDAwMDMxNDAKSURYR0lGYWN0b3J5OjpDcmVhdGVTd2FwQ2hhaW49MDAwMUYyRDAKSURYR0lGYWN0b3J5Mjo6Q3JlYXRlU3dhcENoYWluRm9ySHduZD0wMDAyMTE3MApJRFhHSUZhY3RvcnkyOjpDcmVhdGVTd2FwQ2hhaW5Gb3JDb3JlV2luZG93PTAwMDkzNTgwCmtlcm5lbDMyLmRsbD0wMDBDQzIxOCBGMTEwRDJFRQpMb2FkTGlicmFyeUE9MDAwNDJEODAKTG9hZExpYnJhcnlXPTAwMDNGN0MwClZlcnNpb249MDAwMDAwMEEKRDNEMTIuRExMPTAwMDFDOUUwIEUzMjkxNDc0CklEM0QxMkNvbW1hbmRRdWV1ZTo6RXhlY3V0ZUNvbW1hbmRMaXN0cz0wMDAwOTQ3MApJRFhHSVN3YXBDaGFpbjM6OlJlc2l6ZUJ1ZmZlcnMxPTAwMDlEQzAwCklEWEdJU3dhcENoYWluOjpTZXRGdWxsc2NyZWVuU3RhdGU9MDAwMzk0NTAKRDNEMTJDb3JlLkRMTD0wMDM1OUQzOCBDMUVEQzYyMQpJRFhHSVN3YXBDaGFpbjE6Om1fcENvbW1hbmRRdWV1ZT0wMDAwMDEzOApbRm5PZmZzZXRDYWNoZV0KT1M9Ni4yIEJ1aWxkIDkyMDAKRERSQVcuRExMPTAwMDg0MjAwIDA5OENEQjlDCklEaXJlY3REcmF3U3VyZmFjZTc6OkZsaXA9MDAwMzdFODAKRDNEOC5ETEw9MDAwQjRDMDAgQjM2QzNGRTkKSURpcmVjdDNERGV2aWNlODo6UHJlc2VudD0wMDAyQUJFMApJRGlyZWN0M0REZXZpY2U4OjpSZWxlYXNlPTAwMDJBMjAwCklEaXJlY3QzRERldmljZTg6OlJlc2V0PTAwMDJBN0MwCkQzRDkuRExMPTAwMTc3ODgwIEJGNkYzOUE5CklEaXJlY3QzRERldmljZTk6OlByZXNlbnQ9MDAwRTJEMjAKSURpcmVjdDNERGV2aWNlOTo6UmVsZWFzZT0wMDA2NTkyMApJRGlyZWN0M0REZXZpY2U5OjpSZXNldD0wMDBFMzEyMApJRGlyZWN0M0RTd2FwQ2hhaW45OjpQcmVzZW50PTAwMDQzRTMwCklEaXJlY3QzRERldmljZTlFeDo6UHJlc2VudEV4PTAwMEUyREIwCklEaXJlY3QzRERldmljZTlFeDo6UmVzZXRFeD0wMDBFMzIyMApEWEdJLkRMTD0wMDEwMzY5MCBGMTJCRUM2QgpJRFhHSVN3YXBDaGFpbjo6UHJlc2VudD0wMDBBRkFBMApJRFhHSVN3YXBDaGFpbjo6UmVzaXplQnVmZmVycz0wMDAyNjc1MApJRFhHSVN3YXBDaGFpbjo6UmVsZWFzZT0wMDA0QjBDMApJRFhHSVN3YXBDaGFpbjE6OlByZXNlbnQxPTAwMDQ2NEIwCklEWEdJRmFjdG9yeTo6Q3JlYXRlU3dhcENoYWluPTAwMEE2OTkwCklEWEdJRmFjdG9yeTI6OkNyZWF0ZVN3YXBDaGFpbkZvckh3bmQ9MDAwQTcwNjAKSURYR0lGYWN0b3J5Mjo6Q3JlYXRlU3dhcENoYWluRm9yQ29yZVdpbmRvdz0wMDBBNkVFMAprZXJuZWwzMi5kbGw9MDAwQTZDRDAgQ0M3RTE4RTEKTG9hZExpYnJhcnlBPTAwMDMxQjQwCkxvYWRMaWJyYXJ5Vz0wMDAxRDgyMApWZXJzaW9uPTAwMDAwMDBBCkQzRDEyLkRMTD0wMDAxNDRDMCBERkEwRjFBMgpJRDNEMTJDb21tYW5kUXVldWU6OkV4ZWN1dGVDb21tYW5kTGlzdHM9MDAwODUwQTAKSURYR0lTd2FwQ2hhaW4zOjpSZXNpemVCdWZmZXJzMT0wMDBCMTBFMApJRFhHSVN3YXBDaGFpbjo6U2V0RnVsbHNjcmVlblN0YXRlPTAwMDI4MjUwCkQzRDEyQ29yZS5ETEw9MDAyQzk5RDAgM0M4QTQ3ODgKSURYR0lTd2FwQ2hhaW4xOjptX3BDb21tYW5kUXVldWU9MDAwMDAwQjgKW1NldHRpbmdzXQpMYXN0VXBkYXRlQ2hlY2s9NjY2RTk4QzFoClNraW49ZGVmYXVsdC51c2YKV2luZG93WD0xMjM4CldpbmRvd1k9MzE2CkZpcnN0UnVuPTAKU3RhcnRNaW5pbWl6ZWQ9MQpTdGFydFdpdGhXaW5kb3dzPTAKU2hvd1Rvb2x0aXBzPTAKRW5hYmxlRW5jb2RlclNlcnZlcj0xCkVuYWJsZTY0Qml0PTEKVXNlNjRCaXRFbmNvZGVyU2VydmVyPTEKSGlkZVByZUNyZWF0ZWRQcm9maWxlcz0xClVwZGF0ZUNoZWNraW5nUGVyaW9kPTAKTGFuZ3VhZ2U9CkxheWVyZWRXaW5kb3dNb2RlPTAKTGF5ZXJlZFdpbmRvd0FscGhhPTI1NQpTY2FsZUZhY3Rvcj0xMDAKW1NoYXJlZF0KRmxhZ3M9MDAwMDAwMDUKW1BsdWdpbnNdCk92ZXJsYXlFZGl0b3IuZGxsPTEKSG90a2V5SGFuZGxlci5kbGw9MQoKCg=='
$sync.assets.msiab_desktopoverlayhostcfg = '77u/W1NldHRpbmdzXQpXaW5kb3dYPTAKV2luZG93WT0wCldpbmRvd1c9MTAyNApXaW5kb3dIPTc2OApUcmFuc3BhcmVudD0xClRvcG1vc3Q9MQpMb2NrUG9zPTEKQ29sb3JLZXk9MQpCZ25kQ29sb3I9MDAwMDAwMDAKQWxwaGE9MDAwMDAwRkYKUmVuZGVyZXI9MQpTdXNwZW5kSW5JZGxlPTAKU2NhbGVUb0ZpdD0wCk1heGltaXplZD0xCgoK'
$sync.assets.msiab_fr33thyovl = '77u/W01hc3Rlcl0KSW1wbGVtZW50YXRpb249MgpGb250RmFjZT1VbmlzcGFjZQpGb250SGVpZ2h0PS05CkZvbnRXZWlnaHQ9NDAwClpvb21SYXRpbz00CltTZXR0aW5nc10KTmFtZT0KRW52VmFycz0KUmVmcmVzaFBlcmlvZD0xMDAwCkxvY2tVc2VyU2V0dGluZ3M9MApFbWJlZGRlZEltYWdlPQpQaW5nQWRkcj0KW0dlbmVyYWxdClNvdXJjZXM9NjYKVGFibGVzPTAKTGF5ZXJzPTY5CltTb3VyY2UwXQpOYW1lPVJBTSB1c2FnZQpVbml0cz1NQgpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA5MQpHcHU9MDAwMDAwMDAKU3JjTmFtZT0KW1NvdXJjZTFdCk5hbWU9TWVtb3J5IHVzYWdlClVuaXRzPU1CCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDMxCkdwdT0wMDAwMDAwMApTcmNOYW1lPQpbU291cmNlMl0KTmFtZT1NZW1vcnkgY2xvY2sKVW5pdHM9TUh6CkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDIyCkdwdT0wMDAwMDAwMApTcmNOYW1lPQpbU291cmNlM10KTmFtZT1DUFUxIGNsb2NrClVuaXRzPU1IegpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDBBMApHcHU9MDAwMDAwMDAKU3JjTmFtZT0KW1NvdXJjZTRdCk5hbWU9Q1BVMiBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDAxClNyY05hbWU9CltTb3VyY2U1XQpOYW1lPUNQVTMgY2xvY2sKVW5pdHM9TUh6CkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMEEwCkdwdT0wMDAwMDAwMgpTcmNOYW1lPQpbU291cmNlNl0KTmFtZT1DUFU0IGNsb2NrClVuaXRzPU1IegpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDBBMApHcHU9MDAwMDAwMDMKU3JjTmFtZT0KW1NvdXJjZTddCk5hbWU9Q1BVNSBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDA0ClNyY05hbWU9CltTb3VyY2U4XQpOYW1lPUNQVTYgY2xvY2sKVW5pdHM9TUh6CkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMEEwCkdwdT0wMDAwMDAwNQpTcmNOYW1lPQpbU291cmNlOV0KTmFtZT1DUFU3IGNsb2NrClVuaXRzPU1IegpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDBBMApHcHU9MDAwMDAwMDYKU3JjTmFtZT0KW1NvdXJjZTEwXQpOYW1lPUNQVTggY2xvY2sKVW5pdHM9TUh6CkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMEEwCkdwdT0wMDAwMDAwNwpTcmNOYW1lPQpbU291cmNlMTFdCk5hbWU9Q1BVOSBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDA4ClNyY05hbWU9CltTb3VyY2UxMl0KTmFtZT1DUFUxMCBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDA5ClNyY05hbWU9CltTb3VyY2UxM10KTmFtZT1DUFUxMSBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDBBClNyY05hbWU9CltTb3VyY2UxNF0KTmFtZT1DUFUxMiBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDBCClNyY05hbWU9CltTb3VyY2UxNV0KTmFtZT1DUFUxMyBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDBDClNyY05hbWU9CltTb3VyY2UxNl0KTmFtZT1DUFUxNCBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDBEClNyY05hbWU9CltTb3VyY2UxN10KTmFtZT1DUFUxNSBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDBFClNyY05hbWU9CltTb3VyY2UxOF0KTmFtZT1DUFUxNiBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwQTAKR3B1PTAwMDAwMDBGClNyY05hbWU9CltTb3VyY2UxOV0KTmFtZT1DUFUxIHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDAwClNyY05hbWU9CltTb3VyY2UyMF0KTmFtZT1DUFUyIHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDAxClNyY05hbWU9CltTb3VyY2UyMV0KTmFtZT1DUFUzIHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDAyClNyY05hbWU9CltTb3VyY2UyMl0KTmFtZT1DUFU0IHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDAzClNyY05hbWU9CltTb3VyY2UyM10KTmFtZT1DUFU1IHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDA0ClNyY05hbWU9CltTb3VyY2UyNF0KTmFtZT1DUFU2IHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDA1ClNyY05hbWU9CltTb3VyY2UyNV0KTmFtZT1DUFU3IHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDA2ClNyY05hbWU9CltTb3VyY2UyNl0KTmFtZT1DUFU4IHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDA3ClNyY05hbWU9CltTb3VyY2UyN10KTmFtZT1DUFU5IHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDA4ClNyY05hbWU9CltTb3VyY2UyOF0KTmFtZT1DUFUxMCB1c2FnZQpVbml0cz0lCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDkwCkdwdT0wMDAwMDAwOQpTcmNOYW1lPQpbU291cmNlMjldCk5hbWU9Q1BVMTEgdXNhZ2UKVW5pdHM9JQpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA5MApHcHU9MDAwMDAwMEEKU3JjTmFtZT0KW1NvdXJjZTMwXQpOYW1lPUNQVTEyIHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDBCClNyY05hbWU9CltTb3VyY2UzMV0KTmFtZT1DUFUxMyB1c2FnZQpVbml0cz0lCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDkwCkdwdT0wMDAwMDAwQwpTcmNOYW1lPQpbU291cmNlMzJdCk5hbWU9Q1BVMTQgdXNhZ2UKVW5pdHM9JQpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA5MApHcHU9MDAwMDAwMEQKU3JjTmFtZT0KW1NvdXJjZTMzXQpOYW1lPUNQVTE1IHVzYWdlClVuaXRzPSUKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwOTAKR3B1PTAwMDAwMDBFClNyY05hbWU9CltTb3VyY2UzNF0KTmFtZT1DUFUxNiB1c2FnZQpVbml0cz0lCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDkwCkdwdT0wMDAwMDAwRgpTcmNOYW1lPQpbU291cmNlMzVdCk5hbWU9Q1BVMSB0ZW1wZXJhdHVyZQpVbml0cz3CsEMKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwODAKR3B1PTAwMDAwMDAwClNyY05hbWU9CltTb3VyY2UzNl0KTmFtZT1DUFUyIHRlbXBlcmF0dXJlClVuaXRzPcKwQwpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA4MApHcHU9MDAwMDAwMDEKU3JjTmFtZT0KW1NvdXJjZTM3XQpOYW1lPUNQVTMgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwMgpTcmNOYW1lPQpbU291cmNlMzhdCk5hbWU9Q1BVNCB0ZW1wZXJhdHVyZQpVbml0cz3CsEMKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwODAKR3B1PTAwMDAwMDAzClNyY05hbWU9CltTb3VyY2UzOV0KTmFtZT1DUFU1IHRlbXBlcmF0dXJlClVuaXRzPcKwQwpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA4MApHcHU9MDAwMDAwMDQKU3JjTmFtZT0KW1NvdXJjZTQwXQpOYW1lPUNQVTYgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwNQpTcmNOYW1lPQpbU291cmNlNDFdCk5hbWU9Q1BVNyB0ZW1wZXJhdHVyZQpVbml0cz3CsEMKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwODAKR3B1PTAwMDAwMDA2ClNyY05hbWU9CltTb3VyY2U0Ml0KTmFtZT1DUFU4IHRlbXBlcmF0dXJlClVuaXRzPcKwQwpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA4MApHcHU9MDAwMDAwMDcKU3JjTmFtZT0KW1NvdXJjZTQzXQpOYW1lPUNQVTkgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwOApTcmNOYW1lPQpbU291cmNlNDRdCk5hbWU9Q1BVMTAgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwOQpTcmNOYW1lPQpbU291cmNlNDVdCk5hbWU9Q1BVMTEgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwQQpTcmNOYW1lPQpbU291cmNlNDZdCk5hbWU9Q1BVMTIgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwQgpTcmNOYW1lPQpbU291cmNlNDddCk5hbWU9Q1BVMTMgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwQwpTcmNOYW1lPQpbU291cmNlNDhdCk5hbWU9Q1BVMTQgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwRApTcmNOYW1lPQpbU291cmNlNDldCk5hbWU9Q1BVMTUgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwRQpTcmNOYW1lPQpbU291cmNlNTBdCk5hbWU9Q1BVMTYgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDgwCkdwdT0wMDAwMDAwRgpTcmNOYW1lPQpbU291cmNlNTFdCk5hbWU9Q1BVIHBvd2VyClVuaXRzPVcKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAxMDAKR3B1PUZGRkZGRkZGClNyY05hbWU9CltTb3VyY2U1Ml0KTmFtZT1DUFUgdXNhZ2UKVW5pdHM9JQpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA5MApHcHU9RkZGRkZGRkYKU3JjTmFtZT0KW1NvdXJjZTUzXQpOYW1lPUdQVSB1c2FnZQpVbml0cz0lCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDMwCkdwdT0wMDAwMDAwMApTcmNOYW1lPQpbU291cmNlNTRdCk5hbWU9Q29yZSBjbG9jawpVbml0cz1NSHoKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1NU0kgQWZ0ZXJidXJuZXIKU3JjSWQ9MDAwMDAwMjAKR3B1PTAwMDAwMDAwClNyY05hbWU9CltTb3VyY2U1NV0KTmFtZT1HUFUgdGVtcGVyYXR1cmUKVW5pdHM9wrBDCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDAwCkdwdT0wMDAwMDAwMApTcmNOYW1lPQpbU291cmNlNTZdCk5hbWU9RnJhbWVyYXRlClVuaXRzPUZQUwpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA1MApHcHU9RkZGRkZGRkYKU3JjTmFtZT0KW1NvdXJjZTU3XQpOYW1lPUlzQmVuY2htYXJrQWN0aXZlClVuaXRzPQpGb3JtYXQ9CkZvcm11bGE9KHJ0c3NmbGFncyAmIDB4MTAwKSAhPSAwClByb3ZpZGVyPUhBTApJRD1TdHViCltTb3VyY2U1OF0KTmFtZT1GcmFtZXJhdGUgQXZnClVuaXRzPUZQUwpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA1MwpHcHU9RkZGRkZGRkYKU3JjTmFtZT0KW1NvdXJjZTU5XQpOYW1lPUZyYW1lcmF0ZSAxJSBMb3cKVW5pdHM9RlBTCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9TVNJIEFmdGVyYnVybmVyClNyY0lkPTAwMDAwMDU1CkdwdT1GRkZGRkZGRgpTcmNOYW1lPQpbU291cmNlNjBdCk5hbWU9RnJhbWVyYXRlIDAuMSUgTG93ClVuaXRzPUZQUwpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPU1TSSBBZnRlcmJ1cm5lcgpTcmNJZD0wMDAwMDA1NgpHcHU9RkZGRkZGRkYKU3JjTmFtZT0KW1NvdXJjZTYxXQpOYW1lPURpc3BsYXkxIHJlZnJlc2ggcmF0ZQpVbml0cz1IegpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPUhBTApJRD1EaXNwbGF5MSByZWZyZXNoIHJhdGUKW1NvdXJjZTYyXQpOYW1lPVByZXNlbnRNb2RlClVuaXRzPQpGb3JtYXQ9CkZvcm11bGE9ClByb3ZpZGVyPVByZXNlbnRNb24KSUQ9UHJlc2VudE1vZGUKW1NvdXJjZTYzXQpOYW1lPW1zR3B1QWN0aXZlClVuaXRzPW1zCkZvcm1hdD0KRm9ybXVsYT0KUHJvdmlkZXI9UHJlc2VudE1vbgpJRD1tc0dwdUFjdGl2ZQpbU291cmNlNjRdCk5hbWU9bXNCZXR3ZWVuUHJlc2VudHMKVW5pdHM9bXMKRm9ybWF0PQpGb3JtdWxhPQpQcm92aWRlcj1QcmVzZW50TW9uCklEPW1zQmV0d2VlblByZXNlbnRzCltTb3VyY2U2NV0KTmFtZT1Jc0dwdUxpbWl0ZWQKVW5pdHM9CkZvcm1hdD0KRm9ybXVsYT0obXNHcHVBY3RpdmUgLyBtc0JldHdlZW5QcmVzZW50cykgPj0gMC43NQpQcm92aWRlcj1IQUwKSUQ9U3R1YgpbTGF5ZXIwXQpOYW1lPUJveCBIZWFkZXIgR2FtZQpUZXh0PQpQb3NpdGlvblg9MApQb3NpdGlvblk9MApFeHRlbnRYPTIwNwpFeHRlbnRZPTgKRXh0ZW50T3JpZ2luPTAKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCkJnbmRDb2xvcj1EQjJCMkIyQgpbTGF5ZXIxXQpOYW1lPUdhbWUgVGV4dApUZXh0PUdBTUUKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTEKRXh0ZW50WD0tMQpFeHRlbnRZPS0xCkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXIyXQpOYW1lPUdhbWUgVGl0bGUKVGV4dD08RVhFPgpQb3NpdGlvblg9MTgKUG9zaXRpb25ZPTEKRXh0ZW50WD0xODgKRXh0ZW50WT05CkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIzXQpOYW1lPUJveCBJbmZvClRleHQ9ClBvc2l0aW9uWD0xMDQKUG9zaXRpb25ZPTkKRXh0ZW50WD0xMDMKRXh0ZW50WT0xMjcKRXh0ZW50T3JpZ2luPTAKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCkJnbmRDb2xvcj04MjJCMkIyQgpbTGF5ZXI0XQpOYW1lPUJveCBIZWFkZXIgSW5mbyBDb25maWcKVGV4dD0KUG9zaXRpb25YPTEwNApQb3NpdGlvblk9OQpFeHRlbnRYPTEwMwpFeHRlbnRZPTEwCkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpCZ25kQ29sb3I9REIyQjJCMkIKW0xheWVyNV0KTmFtZT1Db25maWcgVGV4dApUZXh0PUNPTkZJRwpQb3NpdGlvblg9MTA1ClBvc2l0aW9uWT0xMQpFeHRlbnRYPTI0CkV4dGVudFk9MQpFeHRlbnRPcmlnaW49MApTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyNl0KTmFtZT1Db25maWcKVGV4dD08UkVTPlxuJURpc3BsYXkxIHJlZnJlc2ggcmF0ZSVoelxuPEFQUD5cbjxTV0lUQ0ggUHJlc2VudE1vZGU+PENBU0UgMT5IVzpMZWdhY3kgRmxpcDxDQVNFIDI+SFc6TGVnYWN5IENvcHk8Q0FTRSAzPkhXOkluZGVwZW5kZW50IEZsaXA8Q0FTRSA0PkNPTVA6RmxpcDxDQVNFIDU+Q09NUDpDUFUgQ29weTxDQVNFIDY+Q09NUDpHUFUgQ29weTxDQVNFIDg+SFcgQ09NUDpJbmRlcGVuZGVudCBGbGlwClBvc2l0aW9uWD0xMDUKUG9zaXRpb25ZPTIxCkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj0xMUM1MTEKW0xheWVyN10KTmFtZT1Cb3ggSGVhZGVyIEluZm8gRHJpdmVyClRleHQ9ClBvc2l0aW9uWD0xMDQKUG9zaXRpb25ZPTUxCkV4dGVudFg9MTAzCkV4dGVudFk9MTAKRXh0ZW50T3JpZ2luPTAKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCkJnbmRDb2xvcj1EQjJCMkIyQgpbTGF5ZXI4XQpOYW1lPURyaXZlciBUZXh0ClRleHQ9RFJJVkVSClBvc2l0aW9uWD0xMDUKUG9zaXRpb25ZPTUyCkV4dGVudFg9MTIKRXh0ZW50WT03CkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCltMYXllcjldCk5hbWU9RHJpdmVyClRleHQ9JURyaXZlciUKUG9zaXRpb25YPTEwNQpQb3NpdGlvblk9NjMKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIxMF0KTmFtZT1Cb3ggSGVhZGVyIEluZm8gVGltZQpUZXh0PQpQb3NpdGlvblg9MTA0ClBvc2l0aW9uWT03MQpFeHRlbnRYPTEwMwpFeHRlbnRZPTgKRXh0ZW50T3JpZ2luPTAKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCkJnbmRDb2xvcj1EQjJCMkIyQgpbTGF5ZXIxMV0KTmFtZT1UaW1lIFRleHQKVGV4dD1USU1FClBvc2l0aW9uWD0xMDUKUG9zaXRpb25ZPTcyCkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyMTJdCk5hbWU9VGltZQpUZXh0PSVUaW1lMTIlXG4lRGF0ZSUKUG9zaXRpb25YPTEwNQpQb3NpdGlvblk9ODIKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIxM10KTmFtZT1Cb3ggSGVhZGVyIEluZm8gTGltaXQKVGV4dD0KUG9zaXRpb25YPTEwNApQb3NpdGlvblk9OTcKRXh0ZW50WD0xMDMKRXh0ZW50WT0xMApFeHRlbnRPcmlnaW49MApTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKQmduZENvbG9yPURCMkIyQjJCCltMYXllcjE0XQpOYW1lPUxpbWl0IFRleHQKVGV4dD1MSU1JVApQb3NpdGlvblg9MTA1ClBvc2l0aW9uWT05OApFeHRlbnRYPTIwCkV4dGVudFk9NgpFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXIxNV0KTmFtZT1MaW1pdApUZXh0PTxJRiBJc0dwdUxpbWl0ZWQ+R1BVPEVMU0U+Q1BVClBvc2l0aW9uWD0xMDUKUG9zaXRpb25ZPTEwOQpFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9MTFDNTExCltMYXllcjE2XQpOYW1lPUJveCBIZWFkZXIgSW5mbyBGUFMKVGV4dD0KUG9zaXRpb25YPTEwNApQb3NpdGlvblk9MTE3CkV4dGVudFg9MTAzCkV4dGVudFk9MTAKRXh0ZW50T3JpZ2luPTAKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCkJnbmRDb2xvcj1EQjJCMkIyQgpbTGF5ZXIxN10KTmFtZT1GUFMgVGV4dApUZXh0PUZQUwpQb3NpdGlvblg9MTA1ClBvc2l0aW9uWT0xMTgKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXIxOF0KTmFtZT1GUFMKVGV4dD08RlI+IGZwcwpQb3NpdGlvblg9MTA1ClBvc2l0aW9uWT0xMjgKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIxOV0KTmFtZT1Cb3ggQ1BVClRleHQ9ClBvc2l0aW9uWD0wClBvc2l0aW9uWT05CkV4dGVudFg9MTAzCkV4dGVudFk9MTQ3CkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpCZ25kQ29sb3I9ODIyQjJCMkIKW0xheWVyMjBdCk5hbWU9Qm94IEhlYWRlciBDUFUKVGV4dD0KUG9zaXRpb25YPTAKUG9zaXRpb25ZPTkKRXh0ZW50WD0xMDMKRXh0ZW50WT0xMApFeHRlbnRPcmlnaW49MApTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKQmduZENvbG9yPURCMkIyQjJCCltMYXllcjIxXQpOYW1lPUNQVSBUZXh0ClRleHQ9JUNQVVNob3J0JSAlUkFNJQpQb3NpdGlvblg9MQpQb3NpdGlvblk9MTAKRXh0ZW50WD02MApFeHRlbnRZPTYKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyMjJdCk5hbWU9Q1BVClRleHQ9JVJBTSB1c2FnZSVtYgpQb3NpdGlvblg9MzQKUG9zaXRpb25ZPTIwCkV4dGVudFg9NjAKRXh0ZW50WT03CkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIyM10KTmFtZT1DUFUgUG93ZXIKVGV4dD0lQ1BVIHBvd2VyJXcKUG9zaXRpb25YPTY2ClBvc2l0aW9uWT0yMApFeHRlbnRYPTUKRXh0ZW50WT03CkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIyNF0KTmFtZT1DUFUgVXNhZ2UKVGV4dD0lQ1BVIHVzYWdlJSUKUG9zaXRpb25YPTg2ClBvc2l0aW9uWT0yMApFeHRlbnRYPTEKRXh0ZW50WT03CkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIyNV0KTmFtZT1DUFUgMSBUZXh0ClRleHQ9Q1BVIDEKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTI4CkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyMjZdCk5hbWU9Q1BVIDEKVGV4dD0lQ1BVMSBjbG9jayVtaHogJUNQVTEgdGVtcGVyYXR1cmUlwrBDICVDUFUxIHVzYWdlJSUKUG9zaXRpb25YPTM0ClBvc2l0aW9uWT0yOApFeHRlbnRYPTYwCkV4dGVudFk9OApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIyN10KTmFtZT1DUFUgMiBUZXh0ClRleHQ9Q1BVIDIKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTM2CkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyMjhdCk5hbWU9Q1BVIDIKVGV4dD0lQ1BVMiBjbG9jayVtaHogJUNQVTIgdGVtcGVyYXR1cmUlwrBDICVDUFUyIHVzYWdlJSUKUG9zaXRpb25YPTM0ClBvc2l0aW9uWT0zNgpFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9MTFDNTExCltMYXllcjI5XQpOYW1lPUNQVSAzIFRleHQKVGV4dD1DUFUgMwpQb3NpdGlvblg9MQpQb3NpdGlvblk9NDQKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXIzMF0KTmFtZT1DUFUgMwpUZXh0PSVDUFUzIGNsb2NrJW1oeiAlQ1BVMyB0ZW1wZXJhdHVyZSXCsEMgJUNQVTMgdXNhZ2UlJQpQb3NpdGlvblg9MzQKUG9zaXRpb25ZPTQ0CkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj0xMUM1MTEKW0xheWVyMzFdCk5hbWU9Q1BVIDQgVGV4dApUZXh0PUNQVSA0ClBvc2l0aW9uWD0xClBvc2l0aW9uWT01MgpFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCltMYXllcjMyXQpOYW1lPUNQVSA0ClRleHQ9JUNQVTQgY2xvY2slbWh6ICVDUFU0IHRlbXBlcmF0dXJlJcKwQyAlQ1BVNCB1c2FnZSUlClBvc2l0aW9uWD0zNApQb3NpdGlvblk9NTIKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIzM10KTmFtZT1DUFUgNSBUZXh0ClRleHQ9Q1BVIDUKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTYwCkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyMzRdCk5hbWU9Q1BVIDUKVGV4dD0lQ1BVNSBjbG9jayVtaHogJUNQVTUgdGVtcGVyYXR1cmUlwrBDICVDUFU1IHVzYWdlJSUKUG9zaXRpb25YPTM0ClBvc2l0aW9uWT02MApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9MTFDNTExCltMYXllcjM1XQpOYW1lPUNQVSA2IFRleHQKVGV4dD1DUFUgNgpQb3NpdGlvblg9MQpQb3NpdGlvblk9NjgKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXIzNl0KTmFtZT1DUFUgNgpUZXh0PSVDUFU2IGNsb2NrJW1oeiAlQ1BVNiB0ZW1wZXJhdHVyZSXCsEMgJUNQVTYgdXNhZ2UlJQpQb3NpdGlvblg9MzQKUG9zaXRpb25ZPTY4CkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj0xMUM1MTEKW0xheWVyMzddCk5hbWU9Q1BVIDcgVGV4dApUZXh0PUNQVSA3ClBvc2l0aW9uWD0xClBvc2l0aW9uWT03NgpFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCltMYXllcjM4XQpOYW1lPUNQVSA3ClRleHQ9JUNQVTcgY2xvY2slbWh6ICVDUFU3IHRlbXBlcmF0dXJlJcKwQyAlQ1BVNyB1c2FnZSUlClBvc2l0aW9uWD0zNApQb3NpdGlvblk9NzYKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXIzOV0KTmFtZT1DUFUgOCBUZXh0ClRleHQ9Q1BVIDgKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTg0CkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyNDBdCk5hbWU9Q1BVIDgKVGV4dD0lQ1BVOCBjbG9jayVtaHogJUNQVTggdGVtcGVyYXR1cmUlwrBDICVDUFU4IHVzYWdlJSUKUG9zaXRpb25YPTM0ClBvc2l0aW9uWT04NApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9MTFDNTExCltMYXllcjQxXQpOYW1lPUNQVSA5IFRleHQKVGV4dD1DUFUgOQpQb3NpdGlvblg9MQpQb3NpdGlvblk9OTIKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXI0Ml0KTmFtZT1DUFUgOQpUZXh0PSVDUFU5IGNsb2NrJW1oeiAlQ1BVOSB0ZW1wZXJhdHVyZSXCsEMgJUNQVTkgdXNhZ2UlJQpQb3NpdGlvblg9MzQKUG9zaXRpb25ZPTkyCkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj0xMUM1MTEKW0xheWVyNDNdCk5hbWU9Q1BVIDEwIFRleHQKVGV4dD1DUFUgMTAKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTEwMApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCltMYXllcjQ0XQpOYW1lPUNQVSAxMApUZXh0PSVDUFUxMCBjbG9jayVtaHogJUNQVTEwIHRlbXBlcmF0dXJlJcKwQyAlQ1BVMTAgdXNhZ2UlJQpQb3NpdGlvblg9MzQKUG9zaXRpb25ZPTEwMApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9MTFDNTExCltMYXllcjQ1XQpOYW1lPUNQVSAxMSBUZXh0ClRleHQ9Q1BVIDExClBvc2l0aW9uWD0xClBvc2l0aW9uWT0xMDgKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXI0Nl0KTmFtZT1DUFUgMTEKVGV4dD0lQ1BVMTEgY2xvY2slbWh6ICVDUFUxMSB0ZW1wZXJhdHVyZSXCsEMgJUNQVTExIHVzYWdlJSUKUG9zaXRpb25YPTM0ClBvc2l0aW9uWT0xMDgKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXI0N10KTmFtZT1DUFUgMTIgVGV4dApUZXh0PUNQVSAxMgpQb3NpdGlvblg9MQpQb3NpdGlvblk9MTE2CkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyNDhdCk5hbWU9Q1BVIDEyClRleHQ9JUNQVTEyIGNsb2NrJW1oeiAlQ1BVMTIgdGVtcGVyYXR1cmUlwrBDICVDUFUxMiB1c2FnZSUlClBvc2l0aW9uWD0zNApQb3NpdGlvblk9MTE2CkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj0xMUM1MTEKW0xheWVyNDldCk5hbWU9Q1BVIDEzIFRleHQKVGV4dD1DUFUgMTMKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTEyNApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCltMYXllcjUwXQpOYW1lPUNQVSAxMwpUZXh0PSVDUFUxMyBjbG9jayVtaHogJUNQVTEzIHRlbXBlcmF0dXJlJcKwQyAlQ1BVMTMgdXNhZ2UlJQpQb3NpdGlvblg9MzQKUG9zaXRpb25ZPTEyNApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9MTFDNTExCltMYXllcjUxXQpOYW1lPUNQVSAxNCBUZXh0ClRleHQ9Q1BVIDE0ClBvc2l0aW9uWD0xClBvc2l0aW9uWT0xMzIKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXI1Ml0KTmFtZT1DUFUgMTQKVGV4dD0lQ1BVMTQgY2xvY2slbWh6ICVDUFUxNCB0ZW1wZXJhdHVyZSXCsEMgJUNQVTE0IHVzYWdlJSUKUG9zaXRpb25YPTM0ClBvc2l0aW9uWT0xMzIKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXI1M10KTmFtZT1DUFUgMTUgVGV4dApUZXh0PUNQVSAxNQpQb3NpdGlvblg9MQpQb3NpdGlvblk9MTQwCkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKW0xheWVyNTRdCk5hbWU9Q1BVIDE1ClRleHQ9JUNQVTE1IGNsb2NrJW1oeiAlQ1BVMTUgdGVtcGVyYXR1cmUlwrBDICVDUFUxNSB1c2FnZSUlClBvc2l0aW9uWD0zNApQb3NpdGlvblk9MTQwCkV4dGVudFg9MApFeHRlbnRZPTAKRXh0ZW50T3JpZ2luPTAKRml4ZWRBbGlnbm1lbnQ9MQpTaXplPTcwClRleHRDb2xvcj0xMUM1MTEKW0xheWVyNTVdCk5hbWU9Q1BVIDE2IFRleHQKVGV4dD1DUFUgMTYKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTE0OApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCltMYXllcjU2XQpOYW1lPUNQVSAxNgpUZXh0PSVDUFUxNiBjbG9jayVtaHogJUNQVTE2IHRlbXBlcmF0dXJlJcKwQyAlQ1BVMTYgdXNhZ2UlJQpQb3NpdGlvblg9MzQKUG9zaXRpb25ZPTE0OApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9MTFDNTExCltMYXllcjU3XQpOYW1lPUJveCBHUFUKVGV4dD0KUG9zaXRpb25YPTAKUG9zaXRpb25ZPTE1NwpFeHRlbnRYPTEwMwpFeHRlbnRZPTM4CkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpCZ25kQ29sb3I9ODIyQjJCMkIKW0xheWVyNThdCk5hbWU9Qm94IEhlYWRlciBHUFUKVGV4dD0KUG9zaXRpb25YPTAKUG9zaXRpb25ZPTE1NwpFeHRlbnRYPTEwMwpFeHRlbnRZPTEwCkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpCZ25kQ29sb3I9REIyQjJCMkIKW0xheWVyNTldCk5hbWU9R1BVIFRleHQKVGV4dD0lR1BVJSAlVlJBTSUKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTE1OApFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9RkZGRkZGCltMYXllcjYwXQpOYW1lPUdQVSBUZXh0ClRleHQ9R1BVClBvc2l0aW9uWD0xClBvc2l0aW9uWT0xNzgKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXI2MV0KTmFtZT1HUFUKVGV4dD0lTWVtb3J5IHVzYWdlJW1iClBvc2l0aW9uWD0zNApQb3NpdGlvblk9MTcwCkV4dGVudFg9NDgKRXh0ZW50WT03CkV4dGVudE9yaWdpbj0wClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXI2Ml0KTmFtZT1HUFUgVXNhZ2UKVGV4dD0lR1BVIHVzYWdlJSUKUG9zaXRpb25YPTY2ClBvc2l0aW9uWT0xNzAKRXh0ZW50WD0yCkV4dGVudFk9NwpFeHRlbnRPcmlnaW49MApTaXplPTcwClRleHRDb2xvcj0xMUM1MTEKW0xheWVyNjNdCk5hbWU9R1BVIENsb2NrcwpUZXh0PSVDb3JlIGNsb2NrJW1oeiAlR1BVIHRlbXBlcmF0dXJlJcKwQ1xuJU1lbW9yeSBjbG9jayVtaHoKUG9zaXRpb25YPTM0ClBvc2l0aW9uWT0xNzgKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPTExQzUxMQpbTGF5ZXI2NF0KTmFtZT1Cb3ggQmVuY2htYXJrClZpc2liaWxpdHlTb3VyY2U9SXNCZW5jaG1hcmtBY3RpdmUKVGV4dD0KUG9zaXRpb25YPTEwNApQb3NpdGlvblk9MTM3CkV4dGVudFg9NjcKRXh0ZW50WT02NApFeHRlbnRPcmlnaW49MApTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKQmduZENvbG9yPTgyMkIyQjJCCltMYXllcjY1XQpOYW1lPUJveCBIZWFkZXIgQmVuY2htYXJrClZpc2liaWxpdHlTb3VyY2U9SXNCZW5jaG1hcmtBY3RpdmUKVGV4dD0KUG9zaXRpb25YPTEwNApQb3NpdGlvblk9MTM3CkV4dGVudFg9NjcKRXh0ZW50WT0xNwpFeHRlbnRPcmlnaW49MApTaXplPTcwClRleHRDb2xvcj1GRkZGRkYKQmduZENvbG9yPURCMkIyQjJCCltMYXllcjY2XQpOYW1lPUJlbmNobWFyayBUaW1lIFRleHQKVmlzaWJpbGl0eVNvdXJjZT1Jc0JlbmNobWFya0FjdGl2ZQpUZXh0PSIgICBCZW5jaG1hcmtcbiAgICA8QlRJTUU+IgpQb3NpdGlvblg9MTA3ClBvc2l0aW9uWT0xMzkKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXI2N10KTmFtZT1CZW5jaG1hcmsgVGV4dApWaXNpYmlsaXR5U291cmNlPUlzQmVuY2htYXJrQWN0aXZlClRleHQ9RlBTXG5BVkdcbk1BWFxuTUlOXG4xJVxuMC4xJQpQb3NpdGlvblg9MTA1ClBvc2l0aW9uWT0xNTYKRXh0ZW50WD0wCkV4dGVudFk9MApFeHRlbnRPcmlnaW49MApGaXhlZEFsaWdubWVudD0xClNpemU9NzAKVGV4dENvbG9yPUZGRkZGRgpbTGF5ZXI2OF0KTmFtZT1CZW5jaG1hcmsKVmlzaWJpbGl0eVNvdXJjZT1Jc0JlbmNobWFya0FjdGl2ZQpUZXh0PTxGUj4gZnBzXG48RlJBVkc+IGZwc1xuPEZSTUFYPiBmcHNcbjxGUk1JTj4gZnBzXG48RlIxMEw+ICAlXG48RlIwMUw+ICAlClBvc2l0aW9uWD0xMzgKUG9zaXRpb25ZPTE1NgpFeHRlbnRYPTAKRXh0ZW50WT0wCkV4dGVudE9yaWdpbj0wCkZpeGVkQWxpZ25tZW50PTEKU2l6ZT03MApUZXh0Q29sb3I9MTFDNTExCgo='
$sync.assets.msiab_global = '77u/W09TRF0KRW5hYmxlT1NEPTEKRW5hYmxlQmduZD0wCkVuYWJsZUZpbGw9MApFbmFibGVTdGF0PTAKQmFzZUNvbG9yPUZGRkZGRkZGCkJnbmRDb2xvcj0wMDAwMDAwMApGaWxsQ29sb3I9ODAwMDAwMDAKUG9zaXRpb25YPTEKUG9zaXRpb25ZPTEKWm9vbVJhdGlvPTIKQ29vcmRpbmF0ZVNwYWNlPTAKRW5hYmxlRnJhbWVDb2xvckJhcj0wCkZyYW1lQ29sb3JCYXJNb2RlPTAKUmVmcmVzaFBlcmlvZD01MDAKSW50ZWdlckZyYW1lcmF0ZT0xCk1heGltdW1GcmFtZXRpbWU9MApFbmFibGVGcmFtZXRpbWVIaXN0b3J5PTAKRnJhbWV0aW1lSGlzdG9yeVdpZHRoPS0zMgpGcmFtZXRpbWVIaXN0b3J5SGVpZ2h0PS00CkZyYW1ldGltZUhpc3RvcnlTdHlsZT0wClNjYWxlVG9GaXQ9MApbU3RhdGlzdGljc10KRnJhbWVyYXRlQXZlcmFnaW5nSW50ZXJ2YWw9MTAwMApQZWFrRnJhbWVyYXRlQ2FsYz0wClBlcmNlbnRpbGVDYWxjPTAKRnJhbWV0aW1lQ2FsYz0wClBlcmNlbnRpbGVCdWZmZXI9MApbRnJhbWVyYXRlXQpMaW1pdD0wCkxpbWl0RGVub21pbmF0b3I9MQpMaW1pdFRpbWU9MApMaW1pdFRpbWVEZW5vbWluYXRvcj0xClN5bmNEaXNwbGF5PTAKU3luY1NjYW5saW5lMD0wClN5bmNTY2FubGluZTE9MApTeW5jUGVyaW9kcz0wClN5bmNMaW1pdGVyPTAKUGFzc2l2ZVdhaXQ9MQpSZWZsZXhTbGVlcD0wClJlZmxleFNldExhdGVuY3lNYXJrZXI9MApbSG9va2luZ10KRW5hYmxlSG9va2luZz0xCkVuYWJsZUZsb2F0aW5nSW5qZWN0aW9uQWRkcmVzcz0wCkVuYWJsZUR5bmFtaWNPZmZzZXREZXRlY3Rpb249MApIb29rTG9hZExpYnJhcnk9MApIb29rRGlyZWN0RHJhdz0wCkhvb2tEaXJlY3QzRDg9MQpIb29rRGlyZWN0M0Q5PTEKSG9va0RpcmVjdDNEU3dhcENoYWluOVByZXNlbnQ9MQpIb29rRFhHST0xCkhvb2tEaXJlY3QzRDEyPTEKSG9va09wZW5HTD0xCkhvb2tWdWxrYW49MQpJbmplY3Rpb25EZWxheT0xNTAwMApVc2VEZXRvdXJzPTEKW0ZvbnRdCkhlaWdodD0tOQpXZWlnaHQ9NDAwCkZhY2U9VW5pc3BhY2UKTG9hZD0KW1JlbmRlcmVyRGlyZWN0M0Q4XQpJbXBsZW1lbnRhdGlvbj0yCltSZW5kZXJlckRpcmVjdDNEOV0KSW1wbGVtZW50YXRpb249MgpbUmVuZGVyZXJEaXJlY3QzRDEwXQpJbXBsZW1lbnRhdGlvbj0yCltSZW5kZXJlckRpcmVjdDNEMTFdCkltcGxlbWVudGF0aW9uPTIKW1JlbmRlcmVyRGlyZWN0M0QxMl0KSW1wbGVtZW50YXRpb249MgpbUmVuZGVyZXJPcGVuR0xdCkltcGxlbWVudGF0aW9uPTIKW1JlbmRlcmVyVnVsa2FuXQpJbXBsZW1lbnRhdGlvbj0yCltJbmZvXQoKVGltZXN0YW1wPTE5LTAzLTIwMjYsIDA5OjI2OjA2CgoK'
$sync.assets.msiab_hotkeyhandlercfg = '77u/W1NldHRpbmdzXQpPU0RPbkhvdGtleT0wMDAwMDAwMApPU0RPZmZIb3RrZXk9MDAwMDAwMDAKT1NEVG9nZ2xlSG90a2V5PTAwMDAwMDc5CkxpbWl0ZXJPbkhvdGtleT0wMDAwMDAwMApMaW1pdGVyT2ZmSG90a2V5PTAwMDAwMDAwCkxpbWl0ZXJUb2dnbGVIb3RrZXk9MDAwMDAwMDAKU2NyZWVuQ2FwdHVyZUhvdGtleT0wMDAwMDAwMApWaWRlb0NhcHR1cmVIb3RrZXk9MDAwMDAwMDAKUFRUSG90a2V5PTAwMDAwMDAwClBUVDJIb3RrZXk9MDAwMDAwMDAKVmlkZW9QcmVyZWNvcmRIb3RrZXk9MDAwMDAwMDAKQmVuY2htYXJrQmVnaW5Ib3RrZXk9MDAwMDAwN0EKQmVuY2htYXJrRW5kSG90a2V5PTAwMDAwMDdCClBQTTFIb3RrZXk9MDAwMDAwMDAKUFBNMkhvdGtleT0wMDAwMDAwMApQUE0zSG90a2V5PTAwMDAwMDAwClBQTTRIb3RrZXk9MDAwMDAwMDAKT1ZNMUhvdGtleT0wMDAwMDAwMApPVk0ySG90a2V5PTAwMDAwMDAwCk9WTTNIb3RrZXk9MDAwMDAwMDAKT1ZNNEhvdGtleT0wMDAwMDAwMApTY3JlZW5DYXB0dXJlRm9ybWF0PXBuZwpTY3JlZW5DYXB0dXJlUXVhbGl0eT0xMDAKU2NyZWVuQ2FwdHVyZUZvbGRlcj1DOlwKQ2FwdHVyZU9TRD0wClZpZGVvQ2FwdHVyZUNvbnRhaW5lcj1ta3YKVmlkZW9DYXB0dXJlRm9ybWF0PU5WMTIKVmlkZW9DYXB0dXJlUXVhbGl0eT0xMDAKVmlkZW9DYXB0dXJlRm9sZGVyPUM6XApWaWRlb0NhcHR1cmVGcmFtZXNpemU9MDAwMDAwMDEKVmlkZW9DYXB0dXJlRnJhbWVyYXRlPTYwCkF1ZGlvQ2FwdHVyZUZsYWdzPTAwMDAwMDAxCkF1ZGlvQ2FwdHVyZUZsYWdzMj0wMDAwMDAwMApWaWRlb0NhcHR1cmVGbGFnc0V4PTAwMDAwMDAwClByZXJlY29yZFNpemVMaW1pdD0yNTYKUHJlcmVjb3JkVGltZUxpbWl0PTYwMApBdXRvUHJlcmVjb3JkPTAKQmVuY2htYXJrUGF0aD1DOlxCZW5ja21hcmsudHh0CkFwcGVuZEJlbmNobWFyaz0xClBQTTFEZXNjPQpQUE0xUHJvZmlsZT0KUFBNMVByb3BlcnR5PQpQUE0xVHlwZT0wClBQTTFWYWx1ZT0wClBQTTJEZXNjPQpQUE0yUHJvZmlsZT0KUFBNMlByb3BlcnR5PQpQUE0yVHlwZT0wClBQTTJWYWx1ZT0wClBQTTNEZXNjPQpQUE0zUHJvZmlsZT0KUFBNM1Byb3BlcnR5PQpQUE0zVHlwZT0wClBQTTNWYWx1ZT0wClBQTTREZXNjPQpQUE00UHJvZmlsZT0KUFBNNFByb3BlcnR5PQpQUE00VHlwZT0wClBQTTRWYWx1ZT0wCk9WTTFEZXNjPQpPVk0xTWVzc2FnZT0KT1ZNMUxheWVyPQpPVk0xUGFyYW1zPQpPVk0yRGVzYz0KT1ZNMk1lc3NhZ2U9Ck9WTTJMYXllcj0KT1ZNMlBhcmFtcz0KT1ZNM0Rlc2M9Ck9WTTNNZXNzYWdlPQpPVk0zTGF5ZXI9Ck9WTTNQYXJhbXM9Ck9WTTREZXNjPQpPVk00TWVzc2FnZT0KT1ZNNExheWVyPQpPVk00UGFyYW1zPQoKCg=='
$sync.assets.msiab_overlayeditorcfg = '77u/W1NldHRpbmdzXQpMYXlvdXQ9ZnIzM3RoeS5vdmwK'
$sync.assets.msiafterburnercfg = '77u/W1NldHRpbmdzXQpWaWV3cz0KTGFzdFVwZGF0ZUNoZWNrPTVDRDBCOUE1aApTa2luPU1TSU15c3RpYy51c2YKU3RhcnRXaXRoV2luZG93cz0wClN0YXJ0TWluaW1pemVkPTAKSHdQb2xsUGVyaW9kPTEwMDAKTG9ja1Byb2ZpbGVzPTAKU2hvd0hpbnRzPTAKU2hvd1Rvb2x0aXBzPTAKTENERm9udD1mb250NHg2LmRhdApSZW1lbWJlclNldHRpbmdzPTEKRmlyc3RSdW49MApGaXJzdFVzZXJEZWZpbmVDbGljaz0xCkZpcnN0U2VydmVyUnVuPTAKQ3VycmVudEdwdT0wClN5bmM9MQpMaW5rPTEKTGlua1RoZXJtYWw9MQpTaG93T1NEVGltZT0wCkNhcHR1cmVPU0Q9MApQcm9maWxlMUhvdGtleT0wMDAwMDAwMGgKUHJvZmlsZTJIb3RrZXk9MDAwMDAwMDBoClByb2ZpbGUzSG90a2V5PTAwMDAwMDAwaApQcm9maWxlNEhvdGtleT0wMDAwMDAwMGgKUHJvZmlsZTVIb3RrZXk9MDAwMDAwMDBoCk9TRFRvZ2dsZUhvdGtleT0wMDAwMDAwMGgKT1NET25Ib3RrZXk9MDAwMDAwMDBoCk9TRE9mZkhvdGtleT0wMDAwMDAwMGgKT1NEU2VydmVyQmxvY2tIb3RrZXk9MDAwMDAwMDBoCkxpbWl0ZXJUb2dnbGVIb3RrZXk9MDAwMDAwMDBoCkxpbWl0ZXJPbkhvdGtleT0wMDAwMDAwMGgKTGltaXRlck9mZkhvdGtleT0wMDAwMDAwMGgKU2NyZWVuQ2FwdHVyZUhvdGtleT0wMDAwMDAwMGgKVmlkZW9DYXB0dXJlSG90a2V5PTAwMDAwMDAwaApWaWRlb1ByZXJlY29yZEhvdGtleT0wMDAwMDAwMGgKUFRUSG90a2V5PTAwMDAwMDAwaApQVFQySG90a2V5PTAwMDAwMDAwaApCZWdpblJlY29yZEhvdGtleT0wMDAwMDAwMGgKRW5kUmVjb3JkSG90a2V5PTAwMDAwMDAwaApCZWdpbkxvZ2dpbmdIb3RrZXk9MDAwMDAwMDBoCkVuZExvZ2dpbmdIb3RrZXk9MDAwMDAwMDBoCkNsZWFySGlzdG9yeUhvdGtleT0wMDAwMDAwMGgKQmVuY2htYXJrUGF0aD1DOlxCZW5jaG1hcmsudHh0CkFwcGVuZEJlbmNobWFyaz0xClNjcmVlbkNhcHR1cmVGb3JtYXQ9cG5nClNjcmVlbkNhcHR1cmVGb2xkZXI9QzpcClNjcmVlbkNhcHR1cmVRdWFsaXR5PTEwMApWaWRlb0NhcHR1cmVGb2xkZXI9QzpcClZpZGVvQ2FwdHVyZUZvcm1hdD1OVjEyClZpZGVvQ2FwdHVyZVF1YWxpdHk9MTAwClZpZGVvQ2FwdHVyZUZyYW1lcmF0ZT02MApWaWRlb0NhcHR1cmVGcmFtZXNpemU9MDAwMDAwMDFoClZpZGVvQ2FwdHVyZVRocmVhZHM9RkZGRkZGRkZoCkF1ZGlvQ2FwdHVyZUZsYWdzPTAwMDAwMDA1aApWaWRlb0NhcHR1cmVGbGFnc0V4PTAwMDAwMDAwaApBdWRpb0NhcHR1cmVGbGFnczI9MDAwMDAwMDRoClZpZGVvQ2FwdHVyZUNvbnRhaW5lcj1ta3YKVmlkZW9QcmVyZWNvcmRTaXplTGltaXQ9MjU2ClZpZGVvUHJlcmVjb3JkVGltZUxpbWl0PTYwMApBdXRvUHJlcmVjb3JkPTAKV2luZG93WD0zOTgKV2luZG93WT0zMDkKUHJvZmlsZUNvbnRlbnRzPTEKUHJvZmlsZTJEPS0xClByb2ZpbGUzRD0tMQpTd0F1dG9GYW5Db250cm9sPTAKU3dBdXRvRmFuQ29udHJvbEZsYWdzPTAwMDAwMDAwaApTd0F1dG9GYW5Db250cm9sUGVyaW9kPTUwMDAKU3dBdXRvRmFuQ29udHJvbEN1cnZlPTAwMDAwMTAwMDQwMDAwMDAwMDAwMDAwMDAwMDBGMDQxMDAwMDIwNDIwMDAwNDg0MjAwMDA0ODQyMDAwMEEwNDIwMDAwQTA0MjAwMDBCNDQyMDAwMEM4NDIwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwClJlc3RvcmVBZnRlclN1c3BlbmRlZE1vZGU9MQpQYXVzZU1vbml0b3Jpbmc9MApTaG93UGVyZm9ybWFuY2VQcm9maWxlclN0YXR1cz0wClNob3dQZXJmb3JtYW5jZVByb2ZpbGVyUGFuZWw9MApBdHRhY2hNb25pdG9yaW5nV2luZG93PTEKTW9uaXRvcmluZ1dpbmRvd09uVG9wPTEKTG9nUGF0aD0lQUJEaXIlXEhhcmR3YXJlTW9uaXRvcmluZy5obWwKRW5hYmxlTG9nPTAKUmVjcmVhdGVMb2c9MApMb2dMaW1pdD0xMApPU0RMYXlvdXQ9MQpVbmxvY2tWb2x0YWdlQ29udHJvbD0xClVubG9ja1ZvbHRhZ2VNb25pdG9yaW5nPTEKT0VNPTAKRm9yY2VDb25zdGFudFZvbHRhZ2U9MQpTaW5nbGVUcmF5SWNvbk1vZGU9MApGYWhyZW5oZWl0PTAKVGltZTI0PTAKTENER3JhcGg9MApVbm9mZmljaWFsT3ZlcmNsb2NraW5nTW9kZT0xClVub2ZmaWNpYWxPdmVyY2xvY2tpbmdEcnZSZXNldD0xClVwZGF0ZUNoZWNraW5nUGVyaW9kPTAKTG93TGV2ZWxJbnRlcmZhY2U9MQpNTUlPVXNlck1vZGU9MQpIQUw9MQpEcml2ZXI9MQpMYW5ndWFnZT0KTGF5ZXJlZFdpbmRvd01vZGU9MApMYXllcmVkV2luZG93QWxwaGE9MjU1ClNjYWxlRmFjdG9yPTEwMApTb3VyY2VzPStSQU0gdXNhZ2UsK01lbW9yeSB1c2FnZSwrQ1BVMSBjbG9jaywrQ1BVMiBjbG9jaywrQ1BVMyBjbG9jaywrQ1BVNCBjbG9jaywrQ1BVNSBjbG9jaywrQ1BVNiBjbG9jaywrQ1BVNyBjbG9jaywrQ1BVOCBjbG9jaywrQ1BVOSBjbG9jaywrQ1BVMTAgY2xvY2ssK0NQVTExIGNsb2NrLCtDUFUxMiBjbG9jaywrQ1BVMTMgY2xvY2ssK0NQVTE0IGNsb2NrLCtDUFUxNSBjbG9jaywrQ1BVMTYgY2xvY2ssK0NQVTEgdXNhZ2UsK0NQVTIgdXNhZ2UsK0NQVTMgdXNhZ2UsK0NQVTQgdXNhZ2UsK0NQVTUgdXNhZ2UsK0NQVTYgdXNhZ2UsK0NQVTcgdXNhZ2UsK0NQVTggdXNhZ2UsK0NQVTkgdXNhZ2UsK0NQVTEwIHVzYWdlLCtDUFUxMSB1c2FnZSwrQ1BVMTIgdXNhZ2UsK0NQVTEzIHVzYWdlLCtDUFUxNCB1c2FnZSwrQ1BVMTUgdXNhZ2UsK0NQVTE2IHVzYWdlLCtDUFUxIHRlbXBlcmF0dXJlLCtDUFUyIHRlbXBlcmF0dXJlLCtDUFUzIHRlbXBlcmF0dXJlLCtDUFU0IHRlbXBlcmF0dXJlLCtDUFU1IHRlbXBlcmF0dXJlLCtDUFU2IHRlbXBlcmF0dXJlLCtDUFU3IHRlbXBlcmF0dXJlLCtDUFU4IHRlbXBlcmF0dXJlLCtDUFU5IHRlbXBlcmF0dXJlLCtDUFUxMCB0ZW1wZXJhdHVyZSwrQ1BVMTEgdGVtcGVyYXR1cmUsK0NQVTEyIHRlbXBlcmF0dXJlLCtDUFUxMyB0ZW1wZXJhdHVyZSwrQ1BVMTQgdGVtcGVyYXR1cmUsK0NQVTE1IHRlbXBlcmF0dXJlLCtDUFUxNiB0ZW1wZXJhdHVyZSwrQ1BVIHBvd2VyLCtDUFUgdXNhZ2UsK0dQVSB1c2FnZSwrQ29yZSBjbG9jaywrTWVtb3J5IGNsb2NrLCtHUFUgdGVtcGVyYXR1cmUsK0ZyYW1lcmF0ZSwrRnJhbWVyYXRlIEF2ZywrRnJhbWVyYXRlIDElIExvdywrRnJhbWVyYXRlIDAuMSUgTG93LC1Qb3dlciwtUG93ZXIgcGVyY2VudCwtR1BVIHZvbHRhZ2UsLUZhbiBzcGVlZCwtQ1BVIHRlbXBlcmF0dXJlLC1DUFUgY2xvY2ssLUZCIHVzYWdlLC1GYW4gdGFjaG9tZXRlciwtQ29tbWl0IGNoYXJnZSwtRnJhbWVyYXRlIE1pbiwtRnJhbWVyYXRlIE1heCwtRnJhbWV0aW1lLC1NZW1vcnkgdXNhZ2UgXCBwcm9jZXNzLC1SQU0gdXNhZ2UgXCBwcm9jZXNzLC1WSUQgdXNhZ2UsLUJVUyB1c2FnZSwtRmFuIHNwZWVkIDIsLUZhbiB0YWNob21ldGVyIDIsLVRlbXAgbGltaXQsLVBvd2VyIGxpbWl0LC1Wb2x0YWdlIGxpbWl0LC1ObyBsb2FkIGxpbWl0LC1DUFUxIHBvd2VyLC1DUFUyIHBvd2VyLC1DUFUzIHBvd2VyLC1DUFU0IHBvd2VyLC1DUFU1IHBvd2VyLC1DUFU2IHBvd2VyLC1DUFU3IHBvd2VyLC1DUFU4IHBvd2VyLC1DUFU5IHBvd2VyLC1DUFUxMCBwb3dlciwtQ1BVMTEgcG93ZXIsLUNQVTEyIHBvd2VyLC1DUFUxMyBwb3dlciwtQ1BVMTQgcG93ZXIsLUNQVTE1IHBvd2VyLC1DUFUxNiBwb3dlciwtRmFuIHNwZWVkIDMsLUZhbiB0YWNob21ldGVyIDMKTW9uaXRvcmluZ0dyYXBoQ29sdW1ucz0yClNob3dQcm9maWxlcz0wClNob3dNb25pdG9yaW5nPTEKU2hvd0ZyYW1lcmF0ZT0wClNob3dBZGRpdGlvbmFsUGFuZWw9MApQcm9maWxlNkhvdGtleT0wMDAwMDAwMGgKUHJvZmlsZTdIb3RrZXk9MDAwMDAwMDBoClByb2ZpbGU4SG90a2V5PTAwMDAwMDAwaApQcm9maWxlOUhvdGtleT0wMDAwMDAwMGgKUHJvZmlsZTBIb3RrZXk9MDAwMDAwMDBoCkZhblN5bmM9MQpDdXJyZW50RmFuPTAKU3dBdXRvRmFuQ29udHJvbEN1cnZlMj0wMDAwMDEwMDA0MDAwMDAwMDAwMDAwMDAwMDAwRjA0MTAwMDAyMDQyMDAwMDQ4NDIwMDAwNDg0MjAwMDBBMDQyMDAwMEEwNDIwMDAwQjQ0MjAwMDBDODQyMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMApIaWRlTW9uaXRvcmluZz0wClZGV2luZG93WD0xMDk1ClZGV2luZG93WT0yNzUKVkZXaW5kb3dXPTEwMzcKVkZXaW5kb3dIPTgyMgpWRldpbmRvd09uVG9wPTEKTW9uaXRvcmluZ1dpbmRvd1g9OTQ1Ck1vbml0b3JpbmdXaW5kb3dZPTEwOQpNb25pdG9yaW5nV2luZG93Vz04MDAKTW9uaXRvcmluZ1dpbmRvd0g9NTUwClN3QXV0b0ZhbkNvbnRyb2xDdXJ2ZTM9MDAwMDAxMDAwNDAwMDAwMDAwMDAwMDAwMDAwMEYwNDEwMDAwMjA0MjAwMDA0ODQyMDAwMDQ4NDIwMDAwQTA0MjAwMDBBMDQyMDAwMEI0NDIwMDAwQzg0MjAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAKW0FUSUFETEhBTF0KVW5vZmZpY2lhbE92ZXJjbG9ja2luZ01vZGU9MApVbm9mZmljaWFsT3ZlcmNsb2NraW5nRHJ2UmVzZXQ9MQpVbmlmaWVkQWN0aXZpdHlNb25pdG9yaW5nPTAKRXJhc2VTdGFydHVwU2V0dGluZ3M9MQpVbm9mZmljaWFsT3ZlcmNsb2NraW5nRVVMQT1JIGNvbmZpcm0gdGhhdCBJIGFtIGF3YXJlIG9mIHVub2ZmaWNpYWwgb3ZlcmNsb2NraW5nIGxpbWl0YXRpb25zIGFuZCBmdWxseSB1bmRlcnN0YW5kIHRoYXQgTVNJIHdpbGwgbm90IHByb3ZpZGUgbWUgYW55IHN1cHBvcnQgb24gaXQKW1NvdXJjZSBHUFUgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9XG5HUFUKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgRnJhbWVyYXRlIE1heF0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0yMDAKTWluTGltaXQ9MApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgR1BVIHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUdQVQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBGQiB1c2FnZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgVklEIHVzYWdlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBCVVMgdXNhZ2VdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIE1lbW9yeSB1c2FnZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD04MTkyCk1pbkxpbWl0PTAKR3JvdXA9R1BVIE1lbSAgICAgICAgICAgCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENvcmUgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MjUwMApNaW5MaW1pdD0wCkdyb3VwPVxuR1BVCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIE1lbW9yeSBjbG9ja10KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAwMApNaW5MaW1pdD0wCkdyb3VwPVxuR1BVCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIFBvd2VyXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTE1MApNaW5MaW1pdD0wCkdyb3VwPUdQVQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBHUFUgdm9sdGFnZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xLjIwCk1pbkxpbWl0PTAuMDAwCkdyb3VwPVxuXG5HUFUKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgRmFuIHNwZWVkXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBGYW4gc3BlZWQgMl0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgRmFuIHRhY2hvbWV0ZXJdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwMDAKTWluTGltaXQ9MApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgRmFuIHRhY2hvbWV0ZXIgMl0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAwMApNaW5MaW1pdD0wCkdyb3VwPQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBUZW1wIGxpbWl0XQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEKTWluTGltaXQ9MApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgUG93ZXIgbGltaXRdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MQpNaW5MaW1pdD0wCkdyb3VwPQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBWb2x0YWdlIGxpbWl0XQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEKTWluTGltaXQ9MApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgTm8gbG9hZCBsaW1pdF0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xCk1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTEgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9XG5DUFUgMQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyIHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTMgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDMKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVNCB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgNApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFU1IHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA1Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTYgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDYKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVIHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxIHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPVxuQ1BVIDEKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMiB1c2FnZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMgpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUzIHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAzCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTQgdXNhZ2VdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDQKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVNSB1c2FnZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgNQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFU2IHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA2Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVSB1c2FnZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1cbkNQVQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxIGNsb2NrXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1cbkNQVSAxCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTIgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTMgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAzCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTQgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA0Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTUgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA1Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTYgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA2Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVSBjbG9ja10KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD01MjAxCk1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVSBwb3dlcl0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0yMDAuMApNaW5MaW1pdD0wLjAKR3JvdXA9XG5DUFUKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgUkFNIHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTE2Mzg0Ck1pbkxpbWl0PTAKR3JvdXA9Q1BVIE1lbQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDb21taXQgY2hhcmdlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTE2Mzg0Ck1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIEZyYW1lcmF0ZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0yMDAKTWluTGltaXQ9MApHcm91cD1cbkZQUwpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBGcmFtZXRpbWVdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAuMApNaW5MaW1pdD0wLjAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIEZyYW1lcmF0ZSBNaW5dClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MjAwCk1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIEZyYW1lcmF0ZSBBdmddClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MjAwCk1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIEZyYW1lcmF0ZSAxJSBMb3ddClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MjAwCk1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIEZyYW1lcmF0ZSAwLjElIExvd10KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0yMDAKTWluTGltaXQ9MApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltPU0RMYXlvdXQxXQpGb3JtYXRIZWFkZXI9PEMwPTAwODA0MD48QzE9MDA4MEMwPjxDMj1DMDgwODA+PEMzPUZGMDAwMD48QzQ9RkZGRkZGPjxDMjUwPTAwQUQwMD48QTA9LTQ+PEExPTU+PFMwPS01MD48UzE9NTA+ClZhbHVlQWxpZ25tZW50VGFnPTxBMD47PEE+OjcwLDcxLDcyLDc0LDc1OzxBMD5ccjo1MCw1MSw1Miw1Myw1NCw1NSw1NgpVbml0c0FsaWdubWVudFRhZz08QTE+Ckdyb3VwQ29sb3JUYWc9PEM0Pjs8QzQ+OjgwLDkwLDkxLDkyLEEwLEYxLEYyLEYzLEY0LEY1LEY2LEY3LEZGLDEwMCw1MCw1MSw1Miw1Myw1NCw1NSw1NgpWYWx1ZUNvbG9yVGFnPTxDMjUwPjo1MCw1MSw1Miw1Myw1NCw1NSw1NgpBbGFybUNvbG9yVGFnPTxDMz4KVW5pdHNDb2xvclRhZz08QzI1MD46NTAsNTEsNTIsNTMsNTQsNTUsNTYKR3JhcGhDb2xvclRhZz08QzQ+OzxDND46ODAsOTAsOTEsOTIsQTAsRjEsRjIsRjMsRjQsRjUsRjYsRjcsRkYsMTAwOzxDMj46NTAsNTEsNTIsNTMsNTQsNTUsNTYKR3JvdXBTaXplVGFnPQpJbmRleFNpemVUYWc9PFMwPgpWYWx1ZVNpemVUYWc9PFMxPjo3MCw3MSw3Miw3NCw3NQpVbml0c1NpemVUYWc9PFMxPgpHcmFwaFNpemVUYWc9PFMxPgpQcm9sb2dTZXBhcmF0b3I9IiIKUHJvbG9nMFNlcGFyYXRvcj0iIgpQcm9sb2cxU2VwYXJhdG9yPSIiClByb2xvZzJTZXBhcmF0b3I9IiIKR3JvdXBEYXRhU2VwYXJhdG9yPSIiCkdyb3VwTmFtZVNlcGFyYXRvcj0iXHQiCkVwaWxvZ1NlcGFyYXRvcj0iIgpHcm91cFNlcGFyYXRvcj0KR3JhcGhTZXBhcmF0b3I9CkdyYXBoV2lkdGg9LTMyCkdyYXBoV2lkdGhFbWJlZGRlZD0tNApHcmFwaEhlaWdodD0tMgpHcmFwaE1hcmdpbj0xCkdyYXBoU3R5bGU9MApHcmFwaExhYmVsPTMKR3JhcGhQbGFjZW1lbnQ9MgpbU291cmNlIENQVTcgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDcKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVOCB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgOApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFU5IHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA5Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTEwIHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxMApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxMSB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTEKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTIgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDEyCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTcgdXNhZ2VdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDcKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVOCB1c2FnZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgOApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFU5IHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA5Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTEwIHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxMApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxMSB1c2FnZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTEKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTIgdXNhZ2VdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDEyCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTcgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA3Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTggY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA4Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTkgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSA5Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTEwIGNsb2NrXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTAKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTEgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxMQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxMiBjbG9ja10KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD01MDAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDEyCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTEzIHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxMwpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxNCB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTQKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTUgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDE1Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTE2IHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxNgpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxNyB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTcKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTggdGVtcGVyYXR1cmVdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDE4Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTE5IHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxOQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyMCB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjAKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjEgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDIxCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTIyIHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyMgpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyMyB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjMKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjQgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI0Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTEzIHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxMwpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxNCB1c2FnZV0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTQKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTUgdXNhZ2VdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDE1Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTE2IHVzYWdlXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxNgpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxNyB1c2FnZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTcKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTggdXNhZ2VdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDE4Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTE5IHVzYWdlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxOQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyMCB1c2FnZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjAKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjEgdXNhZ2VdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDIxCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTIyIHVzYWdlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyMgpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyMyB1c2FnZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjMKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjQgdXNhZ2VdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI0Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTEzIGNsb2NrXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTMKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTQgY2xvY2tdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxNApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxNSBjbG9ja10KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD01MDAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDE1Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTE2IGNsb2NrXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTYKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTcgY2xvY2tdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAxNwpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUxOCBjbG9ja10KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD01MDAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDE4Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTE5IGNsb2NrXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMTkKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjAgY2xvY2tdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyMApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyMSBjbG9ja10KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD01MDAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDIxCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTIyIGNsb2NrXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjIKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjMgY2xvY2tdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyMwpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyNCBjbG9ja10KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD01MDAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI0Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIFBvd2VyIHBlcmNlbnRdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTUwCk1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTI0IHBvd2VyXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTIwMC4wCk1pbkxpbWl0PTAuMApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltPU0RMYXlvdXQwXQpGb3JtYXRIZWFkZXI9ClZhbHVlQWxpZ25tZW50VGFnPQpVbml0c0FsaWdubWVudFRhZz0KR3JvdXBDb2xvclRhZz0KVmFsdWVDb2xvclRhZz0KQWxhcm1Db2xvclRhZz0KVW5pdHNDb2xvclRhZz0KR3JhcGhDb2xvclRhZz0KR3JvdXBTaXplVGFnPQpJbmRleFNpemVUYWc9ClZhbHVlU2l6ZVRhZz0KVW5pdHNTaXplVGFnPQpHcmFwaFNpemVUYWc9ClByb2xvZ1NlcGFyYXRvcj0iIgpQcm9sb2cwU2VwYXJhdG9yPSIiClByb2xvZzFTZXBhcmF0b3I9IiIKUHJvbG9nMlNlcGFyYXRvcj0iIgpHcm91cERhdGFTZXBhcmF0b3I9IiwgIgpHcm91cE5hbWVTZXBhcmF0b3I9IiBcdDogIgpFcGlsb2dTZXBhcmF0b3I9IiIKR3JvdXBTZXBhcmF0b3I9CkdyYXBoU2VwYXJhdG9yPQpHcmFwaFdpZHRoPS0zMgpHcmFwaFdpZHRoRW1iZWRkZWQ9LTQKR3JhcGhIZWlnaHQ9LTIKR3JhcGhNYXJnaW49MQpHcmFwaFN0eWxlPTAKR3JhcGhMYWJlbD0zCkdyYXBoUGxhY2VtZW50PTIKW09TRExheW91dDNdCkZvcm1hdEhlYWRlcj08QzA9MDA4MDQwPjxDMT0wMDgwQzA+PEMyPUMwODA4MD48QzM9RkYwMDAwPjxDND1GRkZGRkY+PEM1PTgwMDAwMDAwPjxDNj04MEZGRkZGRj48QzI1MD1GRjgwMDA+PEEwPS00PjxBMT01PjxTMD0tNTA+PFMxPTUwPjxTMj0yMDA+ClZhbHVlQWxpZ25tZW50VGFnPTxBMD47PEE+OjcwLDcxLDcyLDc0LDc1OzxBMD5ccjo1MCw1MSw1Miw1Myw1NCw1NSw1NgpVbml0c0FsaWdubWVudFRhZz08QTE+Ckdyb3VwQ29sb3JUYWc9PEMwPjs8QzE+OjgwLDkwLDkxLDkyLEEwLEYxLEYyLEYzLEY0LEY1LEY2LEY3LEZGLDEwMDs8QzI+OjUwLDUxLDUyLDUzLDU0LDU1LDU2ClZhbHVlQ29sb3JUYWc9PEM0Pjo1MCw1MSw1Miw1Myw1NCw1NSw1NgpBbGFybUNvbG9yVGFnPTxDMz4KVW5pdHNDb2xvclRhZz08QzQ+OjUwLDUxLDUyLDUzLDU0LDU1LDU2CkdyYXBoQ29sb3JUYWc9PEMwPjs8QzE+OjgwLDkwLDkxLDkyLEEwLEYxLEYyLEYzLEY0LEY1LEY2LEY3LEZGLDEwMDs8QzI+OjUwLDUxLDUyLDUzLDU0LDU1LDU2Ckdyb3VwU2l6ZVRhZz0KSW5kZXhTaXplVGFnPTxTMD4KVmFsdWVTaXplVGFnPTxTMT46NzAsNzEsNzIsNzQsNzUKVW5pdHNTaXplVGFnPTxTMT4KR3JhcGhTaXplVGFnPTxTMT4KUHJvbG9nU2VwYXJhdG9yPSI8QzU+PEI9MCwwPlxiPEM2PjxCPTAsLTE+XGI8QT0yNT53d3cuR3VydTNELmNvbTxBPS0yNT48QlRJTUU+ICAgICVUaW1lJTxBPjxDPlxuXG48QzQ+PFMyPjxGUj48Uz48Qz5cbiIKUHJvbG9nMFNlcGFyYXRvcj0iXG4iClByb2xvZzFTZXBhcmF0b3I9IlxuIgpQcm9sb2cyU2VwYXJhdG9yPSJcbiIKR3JvdXBEYXRhU2VwYXJhdG9yPSIgIgpHcm91cE5hbWVTZXBhcmF0b3I9Ilx0ICIKRXBpbG9nU2VwYXJhdG9yPSJcbjxDNj48Qj0wLC0xPlxiJUNQVSUgfCAlUkFNJSB8ICVHUFUlIHwgJURyaXZlciU8Qz4iCkdyb3VwU2VwYXJhdG9yPVxuOjkwLDgwLEEwLDUwLDUxCkdyYXBoU2VwYXJhdG9yPQpHcmFwaFdpZHRoPS00NQpHcmFwaFdpZHRoRW1iZWRkZWQ9LTQKR3JhcGhIZWlnaHQ9LTIKR3JhcGhNYXJnaW49MQpHcmFwaFN0eWxlPTA7MjozMCw5MCw1Miw1Myw1NCw1NSw1NgpHcmFwaExhYmVsPTMKR3JhcGhQbGFjZW1lbnQ9MjsxOjMwLDkwOzA6NTAsNTEsNTIsNTMsNTQsNTUsNTYKW1NvdXJjZSBDUFUzMiB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMzIKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMzIgdXNhZ2VdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDMyCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTMyIGNsb2NrXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMzIKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjUgY2xvY2tdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyNQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyNiBjbG9ja10KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD01MDAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI2Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTI3IGNsb2NrXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjcKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjggY2xvY2tdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyOApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyOSBjbG9ja10KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD01MDAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI5Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTMwIGNsb2NrXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTUwMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMzAKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMzEgY2xvY2tdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9NTAwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAzMQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyNSB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjUKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjYgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI2Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTI3IHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyNwpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyOCB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjgKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjkgdGVtcGVyYXR1cmVdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI5Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTMwIHRlbXBlcmF0dXJlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAzMApOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUzMSB0ZW1wZXJhdHVyZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMzEKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjUgdXNhZ2VdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI1Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTI2IHVzYWdlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyNgpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUyNyB1c2FnZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMjcKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjggdXNhZ2VdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDI4Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTI5IHVzYWdlXQpTaG93SW5PU0Q9MQpTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPUNQVSAyOQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUzMCB1c2FnZV0KU2hvd0luT1NEPTEKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0xMDAKTWluTGltaXQ9MApHcm91cD1DUFUgMzAKTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMzEgdXNhZ2VdClNob3dJbk9TRD0xClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MTAwCk1pbkxpbWl0PTAKR3JvdXA9Q1BVIDMxCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIEdQVSB0ZW1wZXJhdHVyZSAyXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPVxuR1BVCk5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTE3IHBvd2VyXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTIwMC4wCk1pbkxpbWl0PTAuMApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMjkgcG93ZXJdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MjAwLjAKTWluTGltaXQ9MC4wCkdyb3VwPQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUzMiBwb3dlcl0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0yMDAuMApNaW5MaW1pdD0wLjAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTE2IHBvd2VyXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTIwMC4wCk1pbkxpbWl0PTAuMApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMSBwb3dlcl0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0yMDAuMApNaW5MaW1pdD0wLjAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTEzIHBvd2VyXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTIwMC4wCk1pbkxpbWl0PTAuMApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CltTb3VyY2UgQ1BVMTUgcG93ZXJdClNob3dJbk9TRD0wClNob3dJbkxDRD0wClNob3dJblRyYXk9MApBbGFybVRocmVzaG9sZE1pbj0KQWxhcm1UaHJlc2hvbGRNYXg9CkFsYXJtRmxhZ3M9MApBbGFybVRpbWVvdXQ9NTAwMApBbGFybUFwcD0KQWxhcm1BcHBDbWRMaW5lPQpFbmFibGVEYXRhRmlsdGVyaW5nPTAKTWF4TGltaXQ9MjAwLjAKTWluTGltaXQ9MC4wCkdyb3VwPQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBDUFUzMCBwb3dlcl0KU2hvd0luT1NEPTAKU2hvd0luTENEPTAKU2hvd0luVHJheT0wCkFsYXJtVGhyZXNob2xkTWluPQpBbGFybVRocmVzaG9sZE1heD0KQWxhcm1GbGFncz0wCkFsYXJtVGltZW91dD01MDAwCkFsYXJtQXBwPQpBbGFybUFwcENtZExpbmU9CkVuYWJsZURhdGFGaWx0ZXJpbmc9MApNYXhMaW1pdD0yMDAuMApNaW5MaW1pdD0wLjAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQpbU291cmNlIENQVTMxIHBvd2VyXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTIwMC4wCk1pbkxpbWl0PTAuMApHcm91cD0KTmFtZT0KVHJheVRleHRDb2xvcj1GRjAwMDBoClRyYXlJY29uVHlwZT0wCk9TREl0ZW1UeXBlPTAKR3JhcGhDb2xvcj0wMEZGMDBoCkZvcm11bGE9CgpbU291cmNlIEZhbiBzcGVlZCAzXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMApNaW5MaW1pdD0wCkdyb3VwPQpOYW1lPQpUcmF5VGV4dENvbG9yPUZGMDAwMGgKVHJheUljb25UeXBlPTAKT1NESXRlbVR5cGU9MApHcmFwaENvbG9yPTAwRkYwMGgKRm9ybXVsYT0KW1NvdXJjZSBGYW4gdGFjaG9tZXRlciAzXQpTaG93SW5PU0Q9MApTaG93SW5MQ0Q9MApTaG93SW5UcmF5PTAKQWxhcm1UaHJlc2hvbGRNaW49CkFsYXJtVGhyZXNob2xkTWF4PQpBbGFybUZsYWdzPTAKQWxhcm1UaW1lb3V0PTUwMDAKQWxhcm1BcHA9CkFsYXJtQXBwQ21kTGluZT0KRW5hYmxlRGF0YUZpbHRlcmluZz0wCk1heExpbWl0PTEwMDAwCk1pbkxpbWl0PTAKR3JvdXA9Ck5hbWU9ClRyYXlUZXh0Q29sb3I9RkYwMDAwaApUcmF5SWNvblR5cGU9MApPU0RJdGVtVHlwZT0wCkdyYXBoQ29sb3I9MDBGRjAwaApGb3JtdWxhPQoK'
$sync.assets.priority = 'ICAgICAgICAjIFNDUklQVCBSVU4gQVMgQURNSU4KICAgICAgICBJZiAoIShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NQcmluY2lwYWxdW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzSWRlbnRpdHldOjpHZXRDdXJyZW50KCkpLklzSW5Sb2xlKFtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0J1aWx0SW5Sb2xlXSJBZG1pbmlzdHJhdG9yIikpCiAgICAgICAge1N0YXJ0LVByb2Nlc3MgUG93ZXJTaGVsbC5leGUgLUFyZ3VtZW50TGlzdCAoIi1Ob1Byb2ZpbGUgLUV4ZWN1dGlvblBvbGljeSBCeXBhc3MgLUZpbGUgYCJ7MH1gIiIgLWYgJFBTQ29tbWFuZFBhdGgpIC1WZXJiIFJ1bkFzCiAgICAgICAgRXhpdH0KICAgICAgICAkSG9zdC5VSS5SYXdVSS5XaW5kb3dUaXRsZSA9ICRteUludm9jYXRpb24uTXlDb21tYW5kLkRlZmluaXRpb24gKyAiIChBZG1pbmlzdHJhdG9yKSIKICAgICAgICAkSG9zdC5VSS5SYXdVSS5CYWNrZ3JvdW5kQ29sb3IgPSAiQmxhY2siCiAgICAgICAgJEhvc3QuUHJpdmF0ZURhdGEuUHJvZ3Jlc3NCYWNrZ3JvdW5kQ29sb3IgPSAiQmxhY2siCiAgICAgICAgJEhvc3QuUHJpdmF0ZURhdGEuUHJvZ3Jlc3NGb3JlZ3JvdW5kQ29sb3IgPSAiV2hpdGUiCiAgICAgICAgQ2xlYXItSG9zdAoKCQlXcml0ZS1Ib3N0ICJURU1QT1JBUklMWSBDSEFOR0UgUFJJT1JJVFkgRk9SIFRFU1RJTkcgUEVSIEFQUC9HQU1FOmBuIgogICAgICAgIFdyaXRlLUhvc3QgIjEuIFByaW9yaXR5OiBBbHJlYWR5IFJ1bm5pbmciCiAgICAgICAgV3JpdGUtSG9zdCAiMi4gUHJpb3JpdHk6IFN0YXJ0dXBgbiIKICAgICAgICB3aGlsZSAoJHRydWUpIHsKICAgICAgICAkY2hvaWNlID0gUmVhZC1Ib3N0ICIgIgogICAgICAgIGlmICgkY2hvaWNlIC1tYXRjaCAnXlsxLTJdJCcpIHsKICAgICAgICBzd2l0Y2ggKCRjaG9pY2UpIHsKICAgICAgICAxIHsKCkNsZWFyLUhvc3QKCiMgc2hvdyBwcmlvcml0eSBvcHRpb25zCldyaXRlLUhvc3QgIjEuIFJlYWwgVGltZSIKV3JpdGUtSG9zdCAiMi4gSGlnaCIKV3JpdGUtSG9zdCAiMy4gQWJvdmUgTm9ybWFsIgpXcml0ZS1Ib3N0ICI0LiBOb3JtYWwiCldyaXRlLUhvc3QgIjUuIEJlbG93IE5vcm1hbCIKV3JpdGUtSG9zdCAiNi4gSWRsZWBuIgoKIyBzZWxlY3QgcHJpb3JpdHkKJHByaW9jaG9pY2UgPSBSZWFkLUhvc3QgLVByb21wdCAiUHJpb3JpdHkiCgpDbGVhci1Ib3N0CgojIG1hcCBjaG9pY2UgdG8gcHJpb3JpdHkKc3dpdGNoICgkcHJpb2Nob2ljZSkgewoiMSIgeyRwcmlvID0gIlJlYWxUaW1lIn0KIjIiIHskcHJpbyA9ICJIaWdoIn0KIjMiIHskcHJpbyA9ICJBYm92ZU5vcm1hbCJ9CiI0IiB7JHByaW8gPSAiTm9ybWFsIn0KIjUiIHskcHJpbyA9ICJCZWxvd05vcm1hbCJ9CiI2IiB7JHByaW8gPSAiSWRsZSJ9CmRlZmF1bHQgewpXcml0ZS1Ib3N0ICJJbnZhbGlkIGlucHV0Li4uIiAtRm9yZWdyb3VuZENvbG9yIFJlZAokbnVsbCA9ICRIb3N0LlVJLlJhd1VJLlJlYWRLZXkoIk5vRWNobyxJbmNsdWRlS2V5RG93biIpCmV4aXQKfQp9CgojIGNvcHkgZ2FtZSBleGUgaWQKKEdldC1Qcm9jZXNzIHwgV2hlcmUtT2JqZWN0IHskXy5Xb3JraW5nU2V0NjQgLWd0IDUwME1CfSB8IFNlbGVjdC1PYmplY3QgTmFtZSwgSWQpIHwgRm9ybWF0LVRhYmxlIC1BdXRvU2l6ZQokZXhlaWQgPSBSZWFkLUhvc3QgLVByb21wdCAiRU5URVIgR0FNRSBFWEUgSUQiCgpDbGVhci1Ib3N0CgojIHNldCBnYW1lIGV4ZSBwcmlvcml0eQokcHJvY2Vzc2lkID0gR2V0LVByb2Nlc3MgLUlkICRleGVpZCAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZQokcHJvY2Vzc2lkLlByaW9yaXR5Q2xhc3MgPSBbU3lzdGVtLkRpYWdub3N0aWNzLlByb2Nlc3NQcmlvcml0eUNsYXNzXTo6JHByaW8KCldyaXRlLUhvc3QgIkdFVFRJTkcgVkFMVUUuLi4iCgpTdGFydC1TbGVlcCAtU2Vjb25kcyAzCgpDbGVhci1Ib3N0CgojIHNob3cgbmV3IHZhbHVlCiRjdXJyZW50cHJpbyA9ICRwcm9jZXNzaWQuUHJpb3JpdHlDbGFzcwpXcml0ZS1Ib3N0ICJJRCAtICRleGVpZCA9ICRjdXJyZW50cHJpb2BuIgoKUGF1c2UKCmV4aXQKCiAgICAgICAgICB9CiAgICAgICAgMiB7CgpDbGVhci1Ib3N0CgojIHN0b3AgZ2FtZSBsYXVuY2hlcnMgcnVubmluZwokc3RvcCA9ICJCYXR0bGUubmV0IiwgIkJzZ0xhdW5jaGVyIiwgIkVBRGVza3RvcCIsICJFcGljR2FtZXNMYXVuY2hlciIsICJHYWxheHlDbGllbnQiLCAiUm9ibG94UGxheWVyQmV0YSIsICJSaW90Q2xpZW50U2VydmljZXMiLCAiTGF1bmNoZXIiLCAic3RlYW0iLCAidXBjIgokc3RvcCB8IEZvckVhY2gtT2JqZWN0IHsgU3RvcC1Qcm9jZXNzIC1OYW1lICRfIC1Gb3JjZSAtRXJyb3JBY3Rpb24gU2lsZW50bHlDb250aW51ZSB9CgpDbGVhci1Ib3N0CgojIHNob3cgcHJpb3JpdHkgb3B0aW9ucwpXcml0ZS1Ib3N0ICIxLiBSZWFsIFRpbWUiCldyaXRlLUhvc3QgIjIuIEhpZ2giCldyaXRlLUhvc3QgIjMuIEFib3ZlIE5vcm1hbCIKV3JpdGUtSG9zdCAiNC4gTm9ybWFsIgpXcml0ZS1Ib3N0ICI1LiBCZWxvdyBOb3JtYWwiCldyaXRlLUhvc3QgIjYuIExvd2BuIgoKIyBzZWxlY3QgcHJpb3JpdHkKJHByaW9jaG9pY2UgPSBSZWFkLUhvc3QgLVByb21wdCAiUHJpb3JpdHkiCgpDbGVhci1Ib3N0CgojIG1hcCBjaG9pY2UgdG8gcHJpb3JpdHkKc3dpdGNoICgkcHJpb2Nob2ljZSkgewoiMSIgeyRwcmlvID0gIlJlYWxUaW1lIn0KIjIiIHskcHJpbyA9ICJIaWdoIn0KIjMiIHskcHJpbyA9ICJBYm92ZU5vcm1hbCJ9CiI0IiB7JHByaW8gPSAiTm9ybWFsIn0KIjUiIHskcHJpbyA9ICJCZWxvd05vcm1hbCJ9CiI2IiB7JHByaW8gPSAiSWRsZSJ9CmRlZmF1bHQgewpXcml0ZS1Ib3N0ICJJbnZhbGlkIGlucHV0Li4uIiAtRm9yZWdyb3VuZENvbG9yIFJlZAokbnVsbCA9ICRIb3N0LlVJLlJhd1VJLlJlYWRLZXkoIk5vRWNobyxJbmNsdWRlS2V5RG93biIpCmV4aXQKfQp9CgojIHNlbGVjdCBnYW1lIGxhdW5jaGVyIGxuayBvciBleGUKV3JpdGUtSG9zdCAiU0VMRUNUIExBVU5DSEVSL0dBTUUvU0hPUlRDVVQvRVhFOiIKQWRkLVR5cGUgLUFzc2VtYmx5TmFtZSBTeXN0ZW0uV2luZG93cy5Gb3JtcwokRGlhbG9nID0gTmV3LU9iamVjdCBTeXN0ZW0uV2luZG93cy5Gb3Jtcy5PcGVuRmlsZURpYWxvZwokRGlhbG9nLkZpbHRlciA9ICJBbGwgRmlsZXMgKCouKil8Ki4qIgokRGlhbG9nLlNob3dEaWFsb2coKSB8IE91dC1OdWxsCiRnYW1lbGF1bmNoZXIgPSAkRGlhbG9nLkZpbGVOYW1lCgpDbGVhci1Ib3N0CgojIHNldCBnYW1lIGV4ZSBwcmlvcml0eQpjbWQgL2MgInN0YXJ0IGAiYCIgLyRwcmlvIGAiJGdhbWVsYXVuY2hlcmAiIgoKIyBjb252ZXJ0IGRpcmVjdG9yeSB0byBmaWxlIG5hbWUgd2l0aG91dCBleGUKJGdhbWVsYXVuY2hlciA9IFtTeXN0ZW0uSU8uUGF0aF06OkdldEZpbGVOYW1lV2l0aG91dEV4dGVuc2lvbigkZ2FtZWxhdW5jaGVyKQoKIyBjaGVjayB2YWx1ZQokcmVsb2FkZ2FtZWxhdW5jaGVyID0gKEdldC1Qcm9jZXNzIC1OYW1lICIkZ2FtZWxhdW5jaGVyIikuUHJpb3JpdHlDbGFzcwoKV3JpdGUtSG9zdCAiR0VUVElORyBWQUxVRS4uLiIKClN0YXJ0LVNsZWVwIC1TZWNvbmRzIDMKCkNsZWFyLUhvc3QKCiMgc2hvdyBuZXcgdmFsdWUKV3JpdGUtSG9zdCAiRVhFIC0gJGdhbWVsYXVuY2hlciA9ICRyZWxvYWRnYW1lbGF1bmNoZXJgbiIKClBhdXNlCgpleGl0CgogICAgICAgICAgfQogICAgICAgIH0gfSBlbHNlIHsgV3JpdGUtSG9zdCAiSW52YWxpZCBpbnB1dC4gUGxlYXNlIHNlbGVjdCBhIHZhbGlkIG9wdGlvbiAoMS0yKS4iIH0gfQ=='
$sync.assets.registrydefaults = '77u/V2luZG93cyBSZWdpc3RyeSBFZGl0b3IgVmVyc2lvbiA1LjAwCgo7IC0tTEVHQUNZIENPTlRST0wgUEFORUwtLQoKCgoKOyBFQVNFIE9GIEFDQ0VTUwo7IG5hcnJhdG9yCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcTmFycmF0b3JcTm9Sb2FtXQoiRHVja0F1ZGlvIj0tCiJXaW5FbnRlckxhdW5jaEVuYWJsZWQiPS0KIlNjcmlwdGluZ0VuYWJsZWQiPS0KIk9ubGluZVNlcnZpY2VzRW5hYmxlZCI9LQoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxOYXJyYXRvcl0KIk5hcnJhdG9yQ3Vyc29ySGlnaGxpZ2h0Ij0tCiJDb3VwbGVOYXJyYXRvckN1cnNvcktleWJvYXJkIj0tCgo7IGVhc2Ugb2YgYWNjZXNzIHNldHRpbmdzClstSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XEVhc2Ugb2YgQWNjZXNzXQoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eV0KIlNvdW5kIG9uIEFjdGl2YXRpb24iPS0KIldhcm5pbmcgU291bmRzIj0tCgpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxBY2Nlc3NpYmlsaXR5XEhpZ2hDb250cmFzdF0KIkZsYWdzIj0iMTI2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eVxLZXlib2FyZCBSZXNwb25zZV0KIkZsYWdzIj0iMTI2IgoiQXV0b1JlcGVhdFJhdGUiPSI1MDAiCiJBdXRvUmVwZWF0RGVsYXkiPSIxMDAwIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eVxNb3VzZUtleXNdCiJGbGFncyI9IjYyIgoiTWF4aW11bVNwZWVkIj0iODAiCiJUaW1lVG9NYXhpbXVtU3BlZWQiPSIzMDAwIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eVxTdGlja3lLZXlzXQoiRmxhZ3MiPSI1MTAiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxBY2Nlc3NpYmlsaXR5XFRvZ2dsZUtleXNdCiJGbGFncyI9IjYyIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eVxTb3VuZFNlbnRyeV0KIkZsYWdzIj0iMiIKIkZTVGV4dEVmZmVjdCI9IjAiCiJUZXh0RWZmZWN0Ij0iMCIKIldpbmRvd3NFZmZlY3QiPSIxIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eVxTbGF0ZUxhdW5jaF0KIkFUYXBwIj0ibmFycmF0b3IiCiJMYXVuY2hBVCI9ZHdvcmQ6MDAwMDAwMDEKCgoKCjsgQ0xPQ0sgQU5EIFJFR0lPTgo7IG5vdGlmeSBtZSB3aGVuIHRoZSBjbG9jayBjaGFuZ2VzClstSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxUaW1lRGF0ZV0KCgoKCjsgQVBQRUFSQU5DRSBBTkQgUEVSU09OQUxJWkFUSU9OCjsgb3BlbiBmaWxlIGV4cGxvcmVyIHRvIHRoaXMgcXVpY2sgYWNjZXNzCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIkxhdW5jaFRvIj0tCgo7IGZyZXF1ZW50IGZvbGRlcnMgaW4gcXVpY2sgYWNjZXNzCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3Jlcl0KIlNob3dGcmVxdWVudCI9LQoKOyBmaWxlIG5hbWUgZXh0ZW5zaW9ucwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJIaWRlRmlsZUV4dCI9ZHdvcmQ6MDAwMDAwMDEKCjsgc2VhcmNoIGhpc3RvcnkKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFNlYXJjaFNldHRpbmdzXQoiSXNEZXZpY2VTZWFyY2hIaXN0b3J5RW5hYmxlZCI9LQoKOyBzaG93IGZpbGVzIGZyb20gb2ZmaWNlLmNvbQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJdCiJTaG93Q2xvdWRGaWxlc0luUXVpY2tBY2Nlc3MiPS0KCjsgZGlzcGxheSBmaWxlIHNpemUgaW5mb3JtYXRpb24gaW4gZm9sZGVyIHRpcHMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiRm9sZGVyQ29udGVudHNJbmZvVGlwIj0tCgo7IGRpc3BsYXkgZnVsbCBwYXRoIGluIHRoZSB0aXRsZSBiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXENhYmluZXRTdGF0ZV0KIkZ1bGxQYXRoIj1kd29yZDowMDAwMDAwMAoKOyBzaG93IHBvcC11cCBkZXNjcmlwdGlvbiBmb3IgZm9sZGVyIGFuZCBkZXNrdG9wIGl0ZW1zCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNob3dJbmZvVGlwIj1kd29yZDowMDAwMDAwMQoKOyBzaG93IHByZXZpZXcgaGFuZGxlcnMgaW4gcHJldmlldyBwYW5lCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNob3dQcmV2aWV3SGFuZGxlcnMiPS0KCjsgc2hvdyBzdGF0dXMgYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNob3dTdGF0dXNCYXIiPWR3b3JkOjAwMDAwMDAxCgo7IHNob3cgc3luYyBwcm92aWRlciBub3RpZmljYXRpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNob3dTeW5jUHJvdmlkZXJOb3RpZmljYXRpb25zIj0tCgo7IHVzZSBzaGFyaW5nIHdpemFyZApbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJTaGFyaW5nV2l6YXJkT24iPS0KCjsgc2hvdyBuZXR3b3JrClstSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcQ2xhc3Nlc1xDTFNJRFx7RjAyQzFBMEQtQkUyMS00MzUwLTg4QjAtNzM2N0ZDOTZFRjNDfV0KCgoKCjsgSEFSRFdBUkUgQU5EIFNPVU5ECjsgbG9jawpbLUhLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxGbHlvdXRNZW51U2V0dGluZ3NdCgo7IHNsZWVwClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEZseW91dE1lbnVTZXR0aW5nc10KCjsgc291bmQgY29tbXVuaWNhdGlvbnMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxNdWx0aW1lZGlhXEF1ZGlvXQoiVXNlckR1Y2tpbmdQcmVmZXJlbmNlIj0tCgo7IHN0YXJ0dXAgc291bmQKW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxBdXRoZW50aWNhdGlvblxMb2dvblVJXEJvb3RBbmltYXRpb25dCiJEaXNhYmxlU3RhcnR1cFNvdW5kIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFZGl0aW9uT3ZlcnJpZGVzXQoiVXNlclNldHRpbmdfRGlzYWJsZVN0YXJ0dXBTb3VuZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgc291bmQgc2NoZW1lCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc10KQD0iLkRlZmF1bHQiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFwuRGVmYXVsdFwuQ3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxXaW5kb3dzIEJhY2tncm91bmQud2F2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcLkRlZmF1bHRcQ3JpdGljYWxCYXR0ZXJ5QWxhcm1cLkN1cnJlbnRdCkA9IkM6XFxXaW5kb3dzXFxtZWRpYVxcV2luZG93cyBGb3JlZ3JvdW5kLndhdiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XERldmljZUNvbm5lY3RcLkN1cnJlbnRdCkA9IkM6XFxXaW5kb3dzXFxtZWRpYVxcV2luZG93cyBIYXJkd2FyZSBJbnNlcnQud2F2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcLkRlZmF1bHRcRGV2aWNlRGlzY29ubmVjdFwuQ3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxXaW5kb3dzIEhhcmR3YXJlIFJlbW92ZS53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxEZXZpY2VGYWlsXC5DdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFdpbmRvd3MgSGFyZHdhcmUgRmFpbC53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxGYXhCZWVwXC5DdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFdpbmRvd3MgTm90aWZ5IEVtYWlsLndhdiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XExvd0JhdHRlcnlBbGFybVwuQ3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxXaW5kb3dzIEJhY2tncm91bmQud2F2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcLkRlZmF1bHRcTWFpbEJlZXBcLkN1cnJlbnRdCkA9IkM6XFxXaW5kb3dzXFxtZWRpYVxcV2luZG93cyBOb3RpZnkgRW1haWwud2F2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcLkRlZmF1bHRcTWVzc2FnZU51ZGdlXC5DdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFdpbmRvd3MgTWVzc2FnZSBOdWRnZS53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxOb3RpZmljYXRpb24uRGVmYXVsdFwuQ3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxXaW5kb3dzIE5vdGlmeSBTeXN0ZW0gR2VuZXJpYy53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxOb3RpZmljYXRpb24uSU1cLkN1cnJlbnRdCkA9IkM6XFxXaW5kb3dzXFxtZWRpYVxcV2luZG93cyBOb3RpZnkgTWVzc2FnaW5nLndhdiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XE5vdGlmaWNhdGlvbi5NYWlsXC5DdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFdpbmRvd3MgTm90aWZ5IEVtYWlsLndhdiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XE5vdGlmaWNhdGlvbi5Qcm94aW1pdHlcLkN1cnJlbnRdCkA9IkM6XFxXaW5kb3dzXFxtZWRpYVxcV2luZG93cyBQcm94aW1pdHkgTm90aWZpY2F0aW9uLndhdiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XE5vdGlmaWNhdGlvbi5SZW1pbmRlclwuQ3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxXaW5kb3dzIE5vdGlmeSBDYWxlbmRhci53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxOb3RpZmljYXRpb24uU01TXC5DdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFdpbmRvd3MgTm90aWZ5IE1lc3NhZ2luZy53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxQcm94aW1pdHlDb25uZWN0aW9uXC5DdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFdpbmRvd3MgUHJveGltaXR5IENvbm5lY3Rpb24ud2F2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcLkRlZmF1bHRcU3lzdGVtQXN0ZXJpc2tcLkN1cnJlbnRdCkA9IkM6XFxXaW5kb3dzXFxtZWRpYVxcV2luZG93cyBCYWNrZ3JvdW5kLndhdiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XFN5c3RlbUV4Y2xhbWF0aW9uXC5DdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFdpbmRvd3MgQmFja2dyb3VuZC53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxTeXN0ZW1IYW5kXC5DdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFdpbmRvd3MgRm9yZWdyb3VuZC53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxTeXN0ZW1Ob3RpZmljYXRpb25cLkN1cnJlbnRdCkA9IkM6XFxXaW5kb3dzXFxtZWRpYVxcV2luZG93cyBCYWNrZ3JvdW5kLndhdiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XFdpbmRvd3NVQUNcLkN1cnJlbnRdCkA9IkM6XFxXaW5kb3dzXFxtZWRpYVxcV2luZG93cyBVc2VyIEFjY291bnQgQ29udHJvbC53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1xzYXBpc3ZyXERpc051bWJlcnNTb3VuZFwuY3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxTcGVlY2ggRGlzYW1iaWd1YXRpb24ud2F2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcc2FwaXN2clxIdWJPZmZTb3VuZFwuY3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxTcGVlY2ggT2ZmLndhdiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXHNhcGlzdnJcSHViT25Tb3VuZFwuY3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxTcGVlY2ggT24ud2F2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcc2FwaXN2clxIdWJTbGVlcFNvdW5kXC5jdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFNwZWVjaCBTbGVlcC53YXYiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1xzYXBpc3ZyXE1pc3JlY29Tb3VuZFwuY3VycmVudF0KQD0iQzpcXFdpbmRvd3NcXG1lZGlhXFxTcGVlY2ggTWlzcmVjb2duaXRpb24ud2F2IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcc2FwaXN2clxQYW5lbFNvdW5kXC5jdXJyZW50XQpAPSJDOlxcV2luZG93c1xcbWVkaWFcXFNwZWVjaCBEaXNhbWJpZ3VhdGlvbi53YXYiCgo7IGF1dG9wbGF5CltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBdXRvcGxheUhhbmRsZXJzXQoiRGlzYWJsZUF1dG9wbGF5Ij1kd29yZDowMDAwMDAwMAoKOyBlbmhhbmNlIHBvaW50ZXIgcHJlY2lzaW9uCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXE1vdXNlXQoiTW91c2VTcGVlZCI9IjEiCiJNb3VzZVRocmVzaG9sZDEiPSI2IgoiTW91c2VUaHJlc2hvbGQyIj0iMTAiCgo7IG1vdXNlIHBvaW50ZXJzIHNjaGVtZQpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxDdXJzb3JzXQoiQXBwU3RhcnRpbmciPSJDOlxcV2luZG93c1xcY3Vyc29yc1xcYWVyb193b3JraW5nLmFuaSIKIkFycm93Ij0iQzpcXFdpbmRvd3NcXGN1cnNvcnNcXGFlcm9fYXJyb3cuY3VyIgoiQ29udGFjdFZpc3VhbGl6YXRpb24iPWR3b3JkOjAwMDAwMDAxCiJDcm9zc2hhaXIiPSIiCiJDdXJzb3JCYXNlU2l6ZSI9ZHdvcmQ6MDAwMDAwMjAKIkdlc3R1cmVWaXN1YWxpemF0aW9uIj1kd29yZDowMDAwMDAxZgoiSGFuZCI9IkM6XFxXaW5kb3dzXFxjdXJzb3JzXFxhZXJvX2xpbmsuY3VyIgoiSGVscCI9IkM6XFxXaW5kb3dzXFxjdXJzb3JzXFxhZXJvX2hlbHBzZWwuY3VyIgoiSUJlYW0iPSIiCiJObyI9IkM6XFxXaW5kb3dzXFxjdXJzb3JzXFxhZXJvX3VuYXZhaWwuY3VyIgoiTldQZW4iPSJDOlxcV2luZG93c1xcY3Vyc29yc1xcYWVyb19wZW4uY3VyIgoiU2NoZW1lIFNvdXJjZSI9ZHdvcmQ6MDAwMDAwMDIKIlNpemVBbGwiPSJDOlxcV2luZG93c1xcY3Vyc29yc1xcYWVyb19tb3ZlLmN1ciIKIlNpemVORVNXIj0iQzpcXFdpbmRvd3NcXGN1cnNvcnNcXGFlcm9fbmVzdy5jdXIiCiJTaXplTlMiPSJDOlxcV2luZG93c1xcY3Vyc29yc1xcYWVyb19ucy5jdXIiCiJTaXplTldTRSI9IkM6XFxXaW5kb3dzXFxjdXJzb3JzXFxhZXJvX253c2UuY3VyIgoiU2l6ZVdFIj0iQzpcXFdpbmRvd3NcXGN1cnNvcnNcXGFlcm9fZXcuY3VyIgoiVXBBcnJvdyI9IkM6XFxXaW5kb3dzXFxjdXJzb3JzXFxhZXJvX3VwLmN1ciIKIldhaXQiPSJDOlxcV2luZG93c1xcY3Vyc29yc1xcYWVyb19idXN5LmFuaSIKQD0iV2luZG93cyBEZWZhdWx0IgoKOyBkZXZpY2UgaW5zdGFsbGF0aW9uIHNldHRpbmdzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRGV2aWNlIE1ldGFkYXRhXQoiUHJldmVudERldmljZU1ldGFkYXRhRnJvbU5ldHdvcmsiPWR3b3JkOjAwMDAwMDAwCgoKCgo7IE5FVFdPUksgQU5EIElOVEVSTkVUCjsgYWxsb3cgb3RoZXIgbmV0d29yayB1c2VycyB0byBjb250cm9sIG9yIGRpc2FibGUgdGhlIHNoYXJlZCBpbnRlcm5ldCBjb25uZWN0aW9uCltIS0VZX0xPQ0FMX01BQ0hJTkVcU3lzdGVtXENvbnRyb2xTZXQwMDFcQ29udHJvbFxOZXR3b3JrXFNoYXJlZEFjY2Vzc0Nvbm5lY3Rpb25dCiJFbmFibGVDb250cm9sIj1kd29yZDowMDAwMDAwMQoKCgoKOyBTWVNURU0gQU5EIFNFQ1VSSVRZCjsgZGVmcmFnbWVudCBhbmQgb3B0aW1pemUgeW91ciBkcml2ZXMKWy1IS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XERmcmdcVGFza1NldHRpbmdzXQoKOyBzZXQgYXBwZWFyYW5jZSBvcHRpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxWaXN1YWxFZmZlY3RzXQoiVmlzdWFsRlhTZXR0aW5nIj0tCgo7IGFuaW1hdGUgY29udHJvbHMgYW5kIGVsZW1lbnRzIGluc2lkZSB3aW5kb3dzCjsgZmFkZSBvciBzbGlkZSBtZW51cyBpbnRvIHZpZXcKOyBmYWRlIG9yIHNsaWRlIHRvb2x0aXBzIGludG8gdmlldwo7IGZhZGUgb3V0IG1lbnUgaXRlbXMgYWZ0ZXIgY2xpY2tpbmcKOyBzaG93IHNoYWRvd3MgdW5kZXIgbW91c2UgcG9pbnRlcgo7IHNob3cgc2hhZG93cyB1bmRlciB3aW5kb3dzCjsgc2xpZGUgb3BlbiBjb21ibyBib3hlcwo7IHNtb290aC1zY3JvbGwgbGlzdCBib3hlcwpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxEZXNrdG9wXQoiVXNlclByZWZlcmVuY2VzTWFzayI9aGV4KDIpOjllLDFlLDA3LDgwLDEyLDAwLDAwLDAwCgo7IGFuaW1hdGUgd2luZG93cyB3aGVuIG1pbmltaXppbmcgYW5kIG1heGltaXppbmcKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcRGVza3RvcFxXaW5kb3dNZXRyaWNzXQoiTWluQW5pbWF0ZSI9IjEiCgo7IGFuaW1hdGlvbnMgaW4gdGhlIHRhc2tiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiVGFza2JhckFuaW1hdGlvbnMiPWR3b3JkOjEKCjsgZW5hYmxlIHBlZWsKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXERXTV0KIkVuYWJsZUFlcm9QZWVrIj1kd29yZDoxCgo7IHNhdmUgdGFza2JhciB0aHVtYm5haWwgcHJldmlld3MKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXERXTV0KIkFsd2F5c0hpYmVybmF0ZVRodW1ibmFpbHMiPWR3b3JkOjAKCjsgZGlzYWJsZSBzaG93IHRodW1ibmFpbHMgaW5zdGVhZCBvZiBpY29ucwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJJY29uc09ubHkiPWR3b3JkOjAKCjsgc2hvdyB0cmFuc2x1Y2VudCBzZWxlY3Rpb24gcmVjdGFuZ2xlCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIkxpc3R2aWV3QWxwaGFTZWxlY3QiPWR3b3JkOjEKCjsgc2hvdyB3aW5kb3cgY29udGVudHMgd2hpbGUgZHJhZ2dpbmcKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcRGVza3RvcF0KIkRyYWdGdWxsV2luZG93cyI9IjEiCgo7IHNtb290aCBlZGdlcyBvZiBzY3JlZW4gZm9udHMKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcRGVza3RvcF0KIkZvbnRTbW9vdGhpbmciPSIyIgoKOyB1c2UgZHJvcCBzaGFkb3dzIGZvciBpY29uIGxhYmVscyBvbiB0aGUgZGVza3RvcApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJMaXN0dmlld1NoYWRvdyI9ZHdvcmQ6MQoKOyBhZGp1c3QgZm9yIGJlc3QgcGVyZm9ybWFuY2Ugb2YKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcQ29udHJvbFxQcmlvcml0eUNvbnRyb2xdCiJXaW4zMlByaW9yaXR5U2VwYXJhdGlvbiI9ZHdvcmQ6MDAwMDAwMDIKCjsgcmVtb3RlIGFzc2lzdGFuY2UKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcQ29udHJvbFxSZW1vdGUgQXNzaXN0YW5jZV0KImZBbGxvd1RvR2V0SGVscCI9ZHdvcmQ6MDAwMDAwMDEKCgoKCjsgVFJPVUJMRVNIT09USU5HCjsgYXV0b21hdGljIG1haW50ZW5hbmNlCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3MgTlRcQ3VycmVudFZlcnNpb25cU2NoZWR1bGVcTWFpbnRlbmFuY2VdCiJNYWludGVuYW5jZURpc2FibGVkIj0tCgoKCgo7IFNFQ1VSSVRZIEFORCBNQUlOVEVOQU5DRQo7IHJlcG9ydCBwcm9ibGVtcwpbLUhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xXaW5kb3dzIEVycm9yIFJlcG9ydGluZ10KCgoKCjsgLS1JTU1FUlNJVkUgQ09OVFJPTCBQQU5FTC0tCgoKCgo7IFdJTkRPV1MgVVBEQVRFCjsgZGVsaXZlcnkgb3B0aW1pemF0aW9uCltIS0VZX1VTRVJTXFMtMS01LTIwXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXERlbGl2ZXJ5T3B0aW1pemF0aW9uXFNldHRpbmdzXQoiRG93bmxvYWRNb2RlIj0tCgoKCgo7IFBSSVZBQ1kKOyBmaW5kIG15IGRldmljZQpbSEtFWV9MT0NBTF9NQUNISU5FXFNvZnR3YXJlXE1pY3Jvc29mdFxNZG1Db21tb25cU2V0dGluZ1ZhbHVlc10KIkxvY2F0aW9uU3luY0VuYWJsZWQiPWR3b3JkOjAwMDAwMDAxCgo7IHNob3cgbWUgbm90aWZpY2F0aW9uIGluIHRoZSBzZXR0aW5ncyBhcHAKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFN5c3RlbVNldHRpbmdzXEFjY291bnROb3RpZmljYXRpb25zXQoiRW5hYmxlQWNjb3VudE5vdGlmaWNhdGlvbnMiPS0KCjsgdGFpbG9yZWQgZXhwZXJpZW5jZXMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENQU1NcU3RvcmVcVGFpbG9yZWRFeHBlcmllbmNlc1dpdGhEaWFnbm9zdGljRGF0YUVuYWJsZWRdCiJWYWx1ZSI9ZHdvcmQ6MDAwMDAwMDEKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxQcml2YWN5XQoiVGFpbG9yZWRFeHBlcmllbmNlc1dpdGhEaWFnbm9zdGljRGF0YUVuYWJsZWQiPWR3b3JkOjAwMDAwMDAxCgo7IGxvY2F0aW9uCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGxvY2F0aW9uXQoiVmFsdWUiPSJBbGxvdyIKCjsgYWxsb3cgbG9jYXRpb24gb3ZlcnJpZGUKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENQU1NcU3RvcmVcVXNlckxvY2F0aW9uT3ZlcnJpZGVQcml2YWN5U2V0dGluZ10KIlZhbHVlIj1kd29yZDowMDAwMDAwMQoKOyBub3RpZnkgd2hlbiBhcHBzIHJlcXVlc3QgbG9jYXRpb24KW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxsb2NhdGlvbl0KIlNob3dHbG9iYWxQcm9tcHRzIj0tCgo7IGNhbWVyYQpbSEtFWV9MT0NBTF9NQUNISU5FXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVx3ZWJjYW1dCiJWYWx1ZSI9IkFsbG93IgoKOyBtaWNyb3Bob25lIApbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxtaWNyb3Bob25lXQoiVmFsdWUiPSJBbGxvdyIKCjsgdm9pY2UgYWN0aXZhdGlvbgpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxTcGVlY2hfT25lQ29yZVxTZXR0aW5nc10KCjsgbm90aWZpY2F0aW9ucwpbSEtFWV9MT0NBTF9NQUNISU5FXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVx1c2VyTm90aWZpY2F0aW9uTGlzdGVuZXJdCiJWYWx1ZSI9IkFsbG93IgoKOyBhY2NvdW50IGluZm8KW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVcdXNlckFjY291bnRJbmZvcm1hdGlvbl0KIlZhbHVlIj0iQWxsb3ciCgo7IGNvbnRhY3RzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGNvbnRhY3RzXQoiVmFsdWUiPSJBbGxvdyIKCjsgY2FsZW5kYXIKW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVcYXBwb2ludG1lbnRzXQoiVmFsdWUiPSJBbGxvdyIKCjsgcGhvbmUgY2FsbHMKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVccGhvbmVDYWxsXQoiVmFsdWUiPSJBbGxvdyIKCjsgY2FsbCBoaXN0b3J5CltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHBob25lQ2FsbEhpc3RvcnldCiJWYWx1ZSI9IkFsbG93IgoKOyBlbWFpbApbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxlbWFpbF0KIlZhbHVlIj0iQWxsb3ciCgo7IHRhc2tzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHVzZXJEYXRhVGFza3NdCiJWYWx1ZSI9IkFsbG93IgoKOyBtZXNzYWdpbmcKW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVcY2hhdF0KIlZhbHVlIj0iQWxsb3ciCgo7IHJhZGlvcwpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxyYWRpb3NdCiJWYWx1ZSI9IkFsbG93IgoKOyBvdGhlciBkZXZpY2VzIApbLUhLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxibHVldG9vdGhTeW5jXQoKOyBhcHAgZGlhZ25vc3RpY3MgCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGFwcERpYWdub3N0aWNzXQoiVmFsdWUiPSJBbGxvdyIKCjsgZG9jdW1lbnRzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGRvY3VtZW50c0xpYnJhcnldCiJWYWx1ZSI9IkFsbG93IgoKOyBkb3dubG9hZHMgZm9sZGVyIApbLUhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVcZG93bmxvYWRzRm9sZGVyXQoKOyBtdXNpYyBsaWJyYXJ5CltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXG11c2ljTGlicmFyeV0KIlZhbHVlIj0iQWxsb3ciCgo7IHBpY3R1cmVzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHBpY3R1cmVzTGlicmFyeV0KIlZhbHVlIj0iRGVueSIKCjsgdmlkZW9zCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHZpZGVvc0xpYnJhcnldCiJWYWx1ZSI9IkFsbG93IgoKOyBmaWxlIHN5c3RlbQpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxicm9hZEZpbGVTeXN0ZW1BY2Nlc3NdCiJWYWx1ZSI9IkFsbG93IgoKOyB0ZXh0IGFuZCBpbWFnZSBnZW5lcmF0aW9uCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHN5c3RlbUFJTW9kZWxzXQoiVmFsdWUiPSJBbGxvdyIKCjsgcGFzc2tleSBhY2Nlc3MKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVccGFzc2tleXNdCiJWYWx1ZSI9IkFsbG93IgoKOyBwYXNza2V5IGF1dG9maWxsIGFjY2VzcwpbSEtFWV9MT0NBTF9NQUNISU5FXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxwYXNza2V5c0VudW1lcmF0aW9uXQoiVmFsdWUiPSJBbGxvdyIKCjsgbGV0IHdlYnNpdGVzIHNob3cgbWUgbG9jYWxseSByZWxldmFudCBjb250ZW50IGJ5IGFjY2Vzc2luZyBteSBsYW5ndWFnZSBsaXN0IApbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxJbnRlcm5hdGlvbmFsXFVzZXIgUHJvZmlsZV0KIkh0dHBBY2NlcHRMYW5ndWFnZU9wdE91dCI9LQoKOyBsZXQgd2luZG93cyBpbXByb3ZlIHN0YXJ0IGFuZCBzZWFyY2ggcmVzdWx0cyBieSB0cmFja2luZyBhcHAgbGF1bmNoZXMgIApbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXEVkZ2VVSV0KClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXEVkZ2VVSV0KCjsgcGVyc29uYWwgaW5raW5nIGFuZCB0eXBpbmcgZGljdGlvbmFyeQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XElucHV0UGVyc29uYWxpemF0aW9uXQoiUmVzdHJpY3RJbXBsaWNpdElua0NvbGxlY3Rpb24iPWR3b3JkOjAwMDAwMDAwCiJSZXN0cmljdEltcGxpY2l0VGV4dENvbGxlY3Rpb24iPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XElucHV0UGVyc29uYWxpemF0aW9uXFRyYWluZWREYXRhU3RvcmVdCiJIYXJ2ZXN0Q29udGFjdHMiPWR3b3JkOjAwMDAwMDAxCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFBlcnNvbmFsaXphdGlvblxTZXR0aW5nc10KIkFjY2VwdGVkUHJpdmFjeVBvbGljeSI9ZHdvcmQ6MDAwMDAwMDEKCjsgc2VuZGluZyByZXF1aXJlZCBkYXRhCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcRGF0YUNvbGxlY3Rpb25dCiJBbGxvd1RlbGVtZXRyeSI9LQoKOyBmZWVkYmFjayBmcmVxdWVuY3kKWy1IS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcU2l1Zl0KCjsgc3RvcmUgbXkgYWN0aXZpdHkgaGlzdG9yeSBvbiB0aGlzIGRldmljZSAKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xTeXN0ZW1dCiJQdWJsaXNoVXNlckFjdGl2aXRpZXMiPS0KCgoKCjsgU0VBUkNICjsgc2VhcmNoIGhpZ2hsaWdodHMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFNlYXJjaFNldHRpbmdzXQoiSXNEeW5hbWljU2VhcmNoQm94RW5hYmxlZCI9LQoKOyBzYWZlIHNlYXJjaApbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cU2VhcmNoU2V0dGluZ3NdCiJTYWZlU2VhcmNoTW9kZSI9LQoKOyBjbG91ZCBjb250ZW50IHNlYXJjaCBmb3Igd29yayBvciBzY2hvb2wgYWNjb3VudApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cU2VhcmNoU2V0dGluZ3NdCiJJc0FBRENsb3VkU2VhcmNoRW5hYmxlZCI9LQoKOyBjbG91ZCBjb250ZW50IHNlYXJjaCBmb3IgbWljcm9zb2Z0IGFjY291bnQKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFNlYXJjaFNldHRpbmdzXQoiSXNNU0FDbG91ZFNlYXJjaEVuYWJsZWQiPS0KCgoKCjsgRUFTRSBPRiBBQ0NFU1MKOyBtYWduaWZpZXIgc2V0dGluZ3MgCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcU2NyZWVuTWFnbmlmaWVyXQoiRm9sbG93Q2FyZXQiPS0KIkZvbGxvd05hcnJhdG9yIj0tCiJGb2xsb3dNb3VzZSI9LQoiRm9sbG93Rm9jdXMiPS0KCjsgbmFycmF0b3Igc2V0dGluZ3MKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxOYXJyYXRvcl0KIkludG9uYXRpb25QYXVzZSI9LQoiUmVhZEhpbnRzIj0tCiJFcnJvck5vdGlmaWNhdGlvblR5cGUiPS0KIkVjaG9DaGFycyI9LQoiRWNob1dvcmRzIj0tCgpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxOYXJyYXRvclxOYXJyYXRvckhvbWVdCgpbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XE5hcnJhdG9yXE5vUm9hbV0KIkVjaG9Ub2dnbGVLZXlzIj0tCgo7IHVzZSB0aGUgcHJpbnQgc2NyZWVuIGtleSB0byBvcGVuIHNjcmVlZW4gY2FwdHVyZQpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxLZXlib2FyZF0KIlByaW50U2NyZWVuS2V5Rm9yU25pcHBpbmdFbmFibGVkIj0tCgoKCgo7IEdBTUlORwo7IGdhbWUgYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTeXN0ZW1cR2FtZUNvbmZpZ1N0b3JlXQoiR2FtZURWUl9FbmFibGVkIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEdhbWVEVlJdCiJBcHBDYXB0dXJlRW5hYmxlZCI9LQoKOyBlbmFibGUgb3BlbiB4Ym94IGdhbWUgYmFyIHVzaW5nIGdhbWUgY29udHJvbGxlcgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XEdhbWVCYXJdCiJVc2VOZXh1c0ZvckdhbWVCYXJFbmFibGVkIj0tCgo7IGVuYWJsZSB1c2UgdmlldyArIG1lbnUgYXMgZ3VpZGUgYnV0dG9uIGluIGFwcHMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxHYW1lQmFyXQoiR2FtZXBhZE5leHVzQ2hvcmRFbmFibGVkIj0tCgo7IGdhbWUgbW9kZQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XEdhbWVCYXJdCiJBdXRvR2FtZU1vZGVFbmFibGVkIj0tCgo7IG90aGVyIHNldHRpbmdzCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxHYW1lRFZSXQoiQXVkaW9FbmNvZGluZ0JpdHJhdGUiPS0KIkF1ZGlvQ2FwdHVyZUVuYWJsZWQiPS0KIkN1c3RvbVZpZGVvRW5jb2RpbmdCaXRyYXRlIj0tCiJDdXN0b21WaWRlb0VuY29kaW5nSGVpZ2h0Ij0tCiJDdXN0b21WaWRlb0VuY29kaW5nV2lkdGgiPS0KIkhpc3RvcmljYWxCdWZmZXJMZW5ndGgiPS0KIkhpc3RvcmljYWxCdWZmZXJMZW5ndGhVbml0Ij0tCiJIaXN0b3JpY2FsQ2FwdHVyZUVuYWJsZWQiPS0KIkhpc3RvcmljYWxDYXB0dXJlT25CYXR0ZXJ5QWxsb3dlZCI9LQoiSGlzdG9yaWNhbENhcHR1cmVPbldpcmVsZXNzRGlzcGxheUFsbG93ZWQiPS0KIk1heGltdW1SZWNvcmRMZW5ndGgiPS0KIlZpZGVvRW5jb2RpbmdCaXRyYXRlTW9kZSI9LQoiVmlkZW9FbmNvZGluZ1Jlc29sdXRpb25Nb2RlIj0tCiJWaWRlb0VuY29kaW5nRnJhbWVSYXRlTW9kZSI9LQoiRWNob0NhbmNlbGxhdGlvbkVuYWJsZWQiPS0KIkN1cnNvckNhcHR1cmVFbmFibGVkIj0tCiJWS1RvZ2dsZUdhbWVCYXIiPS0KIlZLTVRvZ2dsZUdhbWVCYXIiPS0KIlZLU2F2ZUhpc3RvcmljYWxWaWRlbyI9LQoiVktNU2F2ZUhpc3RvcmljYWxWaWRlbyI9LQoiVktUb2dnbGVSZWNvcmRpbmciPS0KIlZLTVRvZ2dsZVJlY29yZGluZyI9LQoiVktUYWtlU2NyZWVuc2hvdCI9LQoiVktNVGFrZVNjcmVlbnNob3QiPS0KIlZLVG9nZ2xlUmVjb3JkaW5nSW5kaWNhdG9yIj0tCiJWS01Ub2dnbGVSZWNvcmRpbmdJbmRpY2F0b3IiPS0KIlZLVG9nZ2xlTWljcm9waG9uZUNhcHR1cmUiPS0KIlZLTVRvZ2dsZU1pY3JvcGhvbmVDYXB0dXJlIj0tCiJWS1RvZ2dsZUNhbWVyYUNhcHR1cmUiPS0KIlZLTVRvZ2dsZUNhbWVyYUNhcHR1cmUiPS0KIlZLVG9nZ2xlQnJvYWRjYXN0Ij0tCiJWS01Ub2dnbGVCcm9hZGNhc3QiPS0KIk1pY3JvcGhvbmVDYXB0dXJlRW5hYmxlZCI9LQoiU3lzdGVtQXVkaW9HYWluIj0tCiJNaWNyb3Bob25lR2FpbiI9LQoKCgoKOyBUSU1FICYgTEFOR1VBR0UgCjsgc2hvdyB0aGUgdm9pY2UgdHlwaW5nIG1pYyBidXR0b24KW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxpbnB1dFxTZXR0aW5nc10KIklzVm9pY2VUeXBpbmdLZXlFbmFibGVkIj0tCgo7IGNhcGl0YWxpemUgdGhlIGZpcnN0IGxldHRlciBvZiBlYWNoIHNlbnRlbmNlCjsgcGxheSBrZXkgc291bmRzIGFzIGkgdHlwZQo7IGFkZCBhIHBlcmlvZCBhZnRlciBpIGRvdWJsZS10YXAgdGhlIHNwYWNlYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcVGFibGV0VGlwXDEuN10KIkVuYWJsZUF1dG9TaGlmdEVuZ2FnZSI9LQoiRW5hYmxlS2V5QXVkaW9GZWVkYmFjayI9LQoiRW5hYmxlRG91YmxlVGFwU3BhY2UiPS0KCjsgdHlwaW5nIGluc2lnaHRzIApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XGlucHV0XFNldHRpbmdzXQoiSW5zaWdodHNFbmFibGVkIj0tCgo7IHNob3cgdGhlIHRvdWNoIGtleWJvYXJkIHdoZW4gbm8ga2V5Ym9hcmQgYXR0YWNoZWQKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxUYWJsZXRUaXBcMS43XQoiVG91Y2hLZXlib2FyZFRhcEludm9rZSI9LQoKOyBsYW5ndWFnZSBiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxDVEZcTGFuZ0Jhcl0KIkV4dHJhSWNvbnNPbk1pbmltaXplZCI9LQoiTGFiZWwiPS0KIlNob3dTdGF0dXMiPS0KIlRyYW5zcGFyZW5jeSI9LQoKOyBsYW5ndWFnZSBob3RrZXkKW0hLRVlfQ1VSUkVOVF9VU0VSXEtleWJvYXJkIExheW91dFxUb2dnbGVdCiJMYW5ndWFnZSBIb3RrZXkiPS0KIkhvdGtleSI9LQoiTGF5b3V0IEhvdGtleSI9LQoKOyBjYWxlbmRhciBldmVudHMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFNlYXJjaF0KIkdsZWFtRW5hYmxlZCI9LQoiV2VhdGhlckVuYWJsZWQiPS0KIkhvbGlkYXlFbmFibGVkIj0tCgoKCgo7IEFDQ09VTlRTCjsgZHluYW1pYyBsb2NrCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93cyBOVFxDdXJyZW50VmVyc2lvblxXaW5sb2dvbl0KIkVuYWJsZUdvb2RieWUiPS0KCjsgdXNlIG15IHNpZ24gaW4gaW5mbyBhZnRlciByZXN0YXJ0CltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcU3lzdGVtXQoiRGlzYWJsZUF1dG9tYXRpY1Jlc3RhcnRTaWduT24iPS0KCgo7IGZvciBpbXByb3ZlZCBzZWN1cml0eSwgb25seSBhbGxvdyB3aW5kb3dzIGhlbGxvIHNpZ24taW4KW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93cyBOVFxDdXJyZW50VmVyc2lvblxQYXNzd29yZExlc3NcRGV2aWNlXQoiRGV2aWNlUGFzc3dvcmRMZXNzQnVpbGRWZXJzaW9uIj1kd29yZDowMDAwMDAwMgoiRGV2aWNlUGFzc3dvcmRMZXNzVXBkYXRlVHlwZSI9LQoKOyB3aW5kb3dzIGJhY2t1cApbLUhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xTZXR0aW5nU3luY10KCgoKCjsgQVBQUwo7IGF1dG9tYXRpY2FsbHkgdXBkYXRlIG1hcHMKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cTWFwc10KIkF1dG9VcGRhdGVFbmFibGVkIj0tCgo7IGFyY2hpdmUgYXBwcwpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXEFwcHhdCiJBbGxvd0F1dG9tYXRpY0FwcEFyY2hpdmluZyI9LQoKCgoKOyBQRVJTT05BTElaQVRJT04KOyBwaWN0dXJlIHBlcnNvbmFsaXplIHlvdXIgYmFja2dyb3VuZApbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxEZXNrdG9wXQoiV2FsbFBhcGVyIj0iQzpcXFdpbmRvd3NcXHdlYlxcd2FsbHBhcGVyXFxXaW5kb3dzXFxpbWcwLmpwZyIKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxXYWxscGFwZXJzXQoiQmFja2dyb3VuZEhpc3RvcnlQYXRoMCI9IkM6XFxXaW5kb3dzXFx3ZWJcXHdhbGxwYXBlclxcV2luZG93c1xcaW1nMC5qcGciCiJDdXJyZW50V2FsbHBhcGVyUGF0aCI9IkM6XFxXaW5kb3dzXFx3ZWJcXHdhbGxwYXBlclxcV2luZG93c1xcaW1nMC5qcGciCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcV2FsbHBhcGVyc10KIkJhY2tncm91bmRUeXBlIj1kd29yZDowMDAwMDAwMAoKOyBsaWdodCB0aGVtZSAmIGVuYWJsZSB0cmFuc3BhcmVuY3kKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFRoZW1lc1xQZXJzb25hbGl6ZV0KIkFwcHNVc2VMaWdodFRoZW1lIj1kd29yZDowMDAwMDAwMQoiQ29sb3JQcmV2YWxlbmNlIj1kd29yZDowMDAwMDAwMAoiRW5hYmxlVHJhbnNwYXJlbmN5Ij1kd29yZDowMDAwMDAwMQoiU3lzdGVtVXNlc0xpZ2h0VGhlbWUiPWR3b3JkOjAwMDAwMDAxCgpbLUhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxUaGVtZXNcUGVyc29uYWxpemVdCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWNjZW50XQoiQWNjZW50UGFsZXR0ZSI9aGV4Ojk5LGViLGZmLDAwLDRjLGMyLGZmLDAwLDAwLDkxLGY4LDAwLDAwLDc4LGQ0LDAwLDAwLDY3LGMwLFwKICAwMCwwMCwzZSw5MiwwMCwwMCwxYSw2OCwwMCxmNyw2MywwYywwMAoiU3RhcnRDb2xvck1lbnUiPWR3b3JkOmZmYzA2NzAwCiJBY2NlbnRDb2xvck1lbnUiPWR3b3JkOmZmZDQ3ODAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcRFdNXQoiRW5hYmxlV2luZG93Q29sb3JpemF0aW9uIj1kd29yZDowMDAwMDAwMAoiQWNjZW50Q29sb3IiPWR3b3JkOmZmZDQ3ODAwCiJDb2xvcml6YXRpb25Db2xvciI9ZHdvcmQ6YzQwMDc4ZDQKIkNvbG9yaXphdGlvbkFmdGVyZ2xvdyI9ZHdvcmQ6YzQwMDc4ZDQKCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXENvbG9yc10KIkJhY2tncm91bmQiPSIwIDAgMCIKCjsgcmVjeWNsZSBiaW4gZnJvbSBkZXNrdG9wCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxIaWRlRGVza3RvcEljb25zXENsYXNzaWNTdGFydE1lbnVdCiJ7NjQ1RkYwNDAtNTA4MS0xMDFCLTlGMDgtMDBBQTAwMkY5NTRFfSI9LQoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEhpZGVEZXNrdG9wSWNvbnNcTmV3U3RhcnRQYW5lbF0KIns2NDVGRjA0MC01MDgxLTEwMUItOUYwOC0wMEFBMDAyRjk1NEV9Ij0tCgo7IGRvbid0IGhpZGUgbW9zdCB1c2VkIGxpc3QgaW4gc3RhcnQgbWVudQpbLUhLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xFeHBsb3Jlcl0KClstSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcRXhwbG9yZXJdCgpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFBvbGljaWVzXEV4cGxvcmVyXQoKOyByZXZlcnQgc3RhcnQgbWVudSBoaWRlIHJlY29tbWVuZGVkClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxQb2xpY3lNYW5hZ2VyXGN1cnJlbnRcZGV2aWNlXFN0YXJ0XQoKWy1IS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFBvbGljeU1hbmFnZXJcY3VycmVudFxkZXZpY2VcRWR1Y2F0aW9uXQoKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xFeHBsb3Jlcl0KIkhpZGVSZWNvbW1lbmRlZFNlY3Rpb24iPS0KCjsgZGVmYXVsdCBwaW5zIHBlcnNvbmFsaXphdGlvbiBzdGFydApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJTdGFydF9MYXlvdXQiPS0KCjsgc2hvdyByZWNlbnRseSBhZGRlZCBhcHBzClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXEV4cGxvcmVyXQoKOyBzaG93IGFjY291bnQtcmVsYXRlZCBub3RpZmljYXRpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlN0YXJ0X0FjY291bnROb3RpZmljYXRpb25zIj0tCgo7IGRpc2FibGUgc2hvdyB3ZWJzaXRlcyBmcm9tIHlvdXIgYnJvd3NpbmcgaGlzdG9yeQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJTdGFydF9SZWNvUGVyc29uYWxpemVkU2l0ZXMiPS0KCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcRXhwbG9yZXJdCiJIaWRlUmVjZW50bHlBZGRlZEFwcHMiPS0KCjsgc2hvdyByZWNlbnRseSBvcGVuZWQgaXRlbXMgaW4gc3RhcnQsIGp1bXAgbGlzdHMgYW5kIGZpbGUgZXhwbG9yZXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiU3RhcnRfVHJhY2tEb2NzIj0tCgo7IHRvdWNoIGtleWJvYXJkCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcVGFibGV0VGlwXDEuN10KIlRpcGJhbmREZXNpcmVkVmlzaWJpbGl0eSI9LQoKOyBzaG93IHNtYWxsZXIgdGFza2JhciBpY29ucyB3aGVuIHRhc2tiYXIgaXMgZnVsbApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJJY29uU2l6ZVByZWZlcmVuY2UiPS0KCjsgbm9ybWFsIHRhc2tiYXIgYWxpZ25tZW50CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlRhc2tiYXJBbCI9LQoKOyBkZXNrdG9wIHByZXZpZXcKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiVGFza2JhclNkIj0tCgo7IGNoYXQgZnJvbSB0YXNrYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlRhc2tiYXJNbiI9LQoKOyB0YXNrIHZpZXcgZnJvbSB0YXNrYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNob3dUYXNrVmlld0J1dHRvbiI9LQoKOyBzZWFyY2ggZnJvbSB0YXNrYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTZWFyY2hdCiJTZWFyY2hib3hUYXNrYmFyTW9kZSI9LQoKOyB3aW5kb3dzIHdpZGdldHMgZnJvbSB0YXNrYmFyClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxEc2hdCgo7IGNvcGlsb3QgZnJvbSB0YXNrYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNob3dDb3BpbG90QnV0dG9uIj0tCgo7IHJlc3VtZSBmcm9tIHRhc2tiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiSXNFbmFibGVkIj0tCgo7IG1lZXQgbm93ClstSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcRXhwbG9yZXJdCgo7IGFjdGlvbiBjZW50ZXIKWy1IS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xFeHBsb3Jlcl0KCjsgbmV3cyBhbmQgaW50ZXJlc3RzClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXFdpbmRvd3MgRmVlZHNdCgo7IGRvbid0IHNob3cgYWxsIHRhc2tiYXIgaWNvbnMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXQoiRW5hYmxlQXV0b1RyYXkiPS0KCjsgc2VjdXJpdHkgdGFza2JhciBpY29uCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcU3RhcnR1cEFwcHJvdmVkXFJ1bl0KIlNlY3VyaXR5SGVhbHRoIj1oZXg6MDQsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAKCjsgdXNlIGR5bmFtaWMgbGlnaHRpbmcgb24gbXkgZGV2aWNlcwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XExpZ2h0aW5nXQoiQW1iaWVudExpZ2h0aW5nRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDEKCjsgY29tcGF0aWJsZSBhcHBzIGluIHRoZSBmb3Jncm91bmQgYWx3YXlzIGNvbnRyb2wgbGlnaHRpbmcgCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcTGlnaHRpbmddCiJDb250cm9sbGVkQnlGb3JlZ3JvdW5kQXBwIj0tCgo7IG1hdGNoIG15IHdpbmRvd3MgYWNjZW50IGNvbG9yIApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XExpZ2h0aW5nXQoiVXNlU3lzdGVtQWNjZW50Q29sb3IiPWR3b3JkOjAwMDAwMDAxCgo7IHNob3cga2V5IGJhY2tncm91bmQKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxUYWJsZXRUaXBcMS43XQoiSXNLZXlCYWNrZ3JvdW5kRW5hYmxlZCI9LQoKOyBzaG93IHJlY29tbWVuZGF0aW9ucyBmb3IgdGlwcyBzaG9ydGN1dHMgbmV3IGFwcHMgYW5kIG1vcmUKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiU3RhcnRfSXJpc1JlY29tbWVuZGF0aW9ucyI9LQoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFN0YXJ0XQoiU2hvd1JlY2VudExpc3QiPS0KCjsgc2hhcmUgYW55IHdpbmRvdyBmcm9tIG15IHRhc2tiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiVGFza2JhclNuIj0tCgo7IGRpc2FibGUgc2hhcmUgYW55IHdpbmRvdyBmcm9tIG15IHRhc2tiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiVGFza2JhclNuIj1kd29yZDowMDAwMDAwMAoKOyBkZXZpY2UgdXNhZ2UKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkRXhwZXJpZW5jZUhvc3RcSW50ZW50XGRldmVsb3Blcl0KIkludGVudCI9ZHdvcmQ6MDAwMDAwMDAKIlByaW9yaXR5Ij1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkRXhwZXJpZW5jZUhvc3RcSW50ZW50XGdhbWluZ10KIkludGVudCI9ZHdvcmQ6MDAwMDAwMDAKIlByaW9yaXR5Ij1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkRXhwZXJpZW5jZUhvc3RcSW50ZW50XGZhbWlseV0KIkludGVudCI9ZHdvcmQ6MDAwMDAwMDAKIlByaW9yaXR5Ij1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkRXhwZXJpZW5jZUhvc3RcSW50ZW50XGNyZWF0aXZlXQoiSW50ZW50Ij1kd29yZDowMDAwMDAwMAoiUHJpb3JpdHkiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRFeHBlcmllbmNlSG9zdFxJbnRlbnRcc2Nob29sd29ya10KIkludGVudCI9ZHdvcmQ6MDAwMDAwMDAKIlByaW9yaXR5Ij1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkRXhwZXJpZW5jZUhvc3RcSW50ZW50XGVudGVydGFpbm1lbnRdCiJJbnRlbnQiPWR3b3JkOjAwMDAwMDAwCiJQcmlvcml0eSI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZEV4cGVyaWVuY2VIb3N0XEludGVudFxidXNpbmVzc10KIkludGVudCI9ZHdvcmQ6MDAwMDAwMDAKIlByaW9yaXR5Ij1kd29yZDowMDAwMDAwMAoKCgoKOyBERVZJQ0VTCjsgdXNiIGlzc3VlcyBub3RpZnkKWy1IS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcU2hlbGxdCgo7IGxldCB3aW5kb3dzIG1hbmFnZSBteSBkZWZhdWx0IHByaW50ZXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzIE5UXEN1cnJlbnRWZXJzaW9uXFdpbmRvd3NdCiJMZWdhY3lEZWZhdWx0UHJpbnRlck1vZGUiPWR3b3JkOmZmZmZmZmZmCgo7IHdyaXRlIHdpdGggeW91ciBmaW5nZXJ0aXAKWy1IS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcVGFibGV0VGlwXEVtYmVkZGVkSW5rQ29udHJvbF0KCgoKCjsgU1lTVEVNCjsgZHBpIHNjYWxpbmcKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcRGVza3RvcF0KIkxvZ1BpeGVscyI9LQoiV2luOERwaVNjYWxpbmciPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcRFdNXQoiVXNlRHBpU2NhbGluZyI9LQoKOyBmaXggc2NhbGluZyBmb3IgYXBwcwpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxEZXNrdG9wXQoiRW5hYmxlUGVyUHJvY2Vzc1N5c3RlbURQSSI9LQoKOyBoYXJkd2FyZSBhY2NlbGVyYXRlZCBncHUgc2NoZWR1bGluZwpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxDb250cm9sXEdyYXBoaWNzRHJpdmVyc10KIkh3U2NoTW9kZSI9LQoKOyB2YXJpYWJsZSByZWZyZXNoIHJhdGUgJiBvcHRpbWl6YXRpb25zIGZvciB3aW5kb3dlZCBnYW1lcwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XERpcmVjdFhcVXNlckdwdVByZWZlcmVuY2VzXQoiRGlyZWN0WFVzZXJHbG9iYWxTZXR0aW5ncyI9LQoKOyBub3RpZmljYXRpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxQdXNoTm90aWZpY2F0aW9uc10KIlRvYXN0RW5hYmxlZCI9LQoKOyBub3RpZmljYXRpb25zIHN1Z2dlc3RlZApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cTm90aWZpY2F0aW9uc1xTZXR0aW5nc1xXaW5kb3dzLlN5c3RlbVRvYXN0LlN1Z2dlc3RlZF0KIkVuYWJsZWQiPS0KCjsgbm90aWZpY2F0aW9ucwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cTm90aWZpY2F0aW9uc1xTZXR0aW5nc10KIk5PQ19HTE9CQUxfU0VUVElOR19BTExPV19OT1RJRklDQVRJT05fU09VTkQiPS0KIk5PQ19HTE9CQUxfU0VUVElOR19BTExPV19DUklUSUNBTF9UT0FTVFNfQUJPVkVfTE9DSyI9LQoiTk9DX0dMT0JBTF9TRVRUSU5HX0FMTE9XX1RPQVNUU19BQk9WRV9MT0NLIj0tCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cTm90aWZpY2F0aW9uc1xTZXR0aW5nc1xNaWNyb3NvZnQuU2t5RHJpdmUuRGVza3RvcF0KIkVuYWJsZWQiPS0KCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxOb3RpZmljYXRpb25zXFNldHRpbmdzXFdpbmRvd3MuU3lzdGVtVG9hc3QuQXV0b1BsYXldCiJFbmFibGVkIj0tCgpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXE5vdGlmaWNhdGlvbnNcU2V0dGluZ3NcV2luZG93cy5TeXN0ZW1Ub2FzdC5TZWN1cml0eUFuZE1haW50ZW5hbmNlXQoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXE5vdGlmaWNhdGlvbnNcU2V0dGluZ3Ncd2luZG93cy5pbW1lcnNpdmVjb250cm9scGFuZWxfY3c1bjFoMnR4eWV3eSFtaWNyb3NvZnQud2luZG93cy5pbW1lcnNpdmVjb250cm9scGFuZWxdCiJFbmFibGVkIj0tCgpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXE5vdGlmaWNhdGlvbnNcU2V0dGluZ3NcV2luZG93cy5TeXN0ZW1Ub2FzdC5DYXBhYmlsaXR5QWNjZXNzXQoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXE5vdGlmaWNhdGlvbnNcU2V0dGluZ3NcV2luZG93cy5TeXN0ZW1Ub2FzdC5TdGFydHVwQXBwXQoiRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCgpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFVzZXJQcm9maWxlRW5nYWdlbWVudF0KCjsgc3VnZ2VzdGVkIGFjdGlvbnMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFNtYXJ0QWN0aW9uUGxhdGZvcm1cU21hcnRDbGlwYm9hcmRdCiJEaXNhYmxlZCI9LQoKOyBmb2N1cyBhc3Npc3QKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkU3RvcmVcU3RvcmVcQ2FjaGVcRGVmYXVsdEFjY291bnRcJCR3aW5kb3dzLmRhdGEubm90aWZpY2F0aW9ucy5xdWlldGhvdXJzc2V0dGluZ3NcQ3VycmVudF0KIkRhdGEiPWhleDowMiwwMCwwMCwwMCw3NCxhOSw3MCw3MywwMyw4MixkYSwwMSwwMCwwMCwwMCwwMCw0Myw0MiwwMSwwMCxjMiwwYSxcCiAgMDEsZDIsMTQsMjgsNGQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNmYsMDAsNzMsMDAsNmYsMDAsNjYsMDAsNzQsMDAsMmUsMDAsNTEsXAogIDAwLDc1LDAwLDY5LDAwLDY1LDAwLDc0LDAwLDQ4LDAwLDZmLDAwLDc1LDAwLDcyLDAwLDczLDAwLDUwLDAwLDcyLDAwLDZmLDAwLFwKICA2NiwwMCw2OSwwMCw2YywwMCw2NSwwMCwyZSwwMCw1NSwwMCw2ZSwwMCw3MiwwMCw2NSwwMCw3MywwMCw3NCwwMCw3MiwwMCw2OSxcCiAgMDAsNjMsMDAsNzQsMDAsNjUsMDAsNjQsMDAsY2EsMjgsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXENhY2hlXERlZmF1bHRBY2NvdW50XCRxdWlldG1vbWVudGZ1bGxzY3JlZW4kd2luZG93cy5kYXRhLm5vdGlmaWNhdGlvbnMucXVpZXRtb21lbnRcQ3VycmVudF0KIkRhdGEiPWhleDowMiwwMCwwMCwwMCw4MixhMyw3MSw3MywwMyw4MixkYSwwMSwwMCwwMCwwMCwwMCw0Myw0MiwwMSwwMCxjMiwwYSxcCiAgMDEsYzIsMTQsMDEsZDIsMWUsMjYsNGQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNmYsMDAsNzMsMDAsNmYsMDAsNjYsMDAsNzQsMDAsXAogIDJlLDAwLDUxLDAwLDc1LDAwLDY5LDAwLDY1LDAwLDc0LDAwLDQ4LDAwLDZmLDAwLDc1LDAwLDcyLDAwLDczLDAwLDUwLDAwLDcyLFwKICAwMCw2ZiwwMCw2NiwwMCw2OSwwMCw2YywwMCw2NSwwMCwyZSwwMCw0MSwwMCw2YywwMCw2MSwwMCw3MiwwMCw2ZCwwMCw3MywwMCxcCiAgNGYsMDAsNmUsMDAsNmMsMDAsNzksMDAsY2EsNTAsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXENhY2hlXERlZmF1bHRBY2NvdW50XCRxdWlldG1vbWVudGdhbWUkd2luZG93cy5kYXRhLm5vdGlmaWNhdGlvbnMucXVpZXRtb21lbnRcQ3VycmVudF0KIkRhdGEiPWhleDowMiwwMCwwMCwwMCxhNSxjMSw3MSw3MywwMyw4MixkYSwwMSwwMCwwMCwwMCwwMCw0Myw0MiwwMSwwMCxjMiwwYSxcCiAgMDEsYzIsMTQsMDEsZDIsMWUsMjgsNGQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNmYsMDAsNzMsMDAsNmYsMDAsNjYsMDAsNzQsMDAsXAogIDJlLDAwLDUxLDAwLDc1LDAwLDY5LDAwLDY1LDAwLDc0LDAwLDQ4LDAwLDZmLDAwLDc1LDAwLDcyLDAwLDczLDAwLDUwLDAwLDcyLFwKICAwMCw2ZiwwMCw2NiwwMCw2OSwwMCw2YywwMCw2NSwwMCwyZSwwMCw1MCwwMCw3MiwwMCw2OSwwMCw2ZiwwMCw3MiwwMCw2OSwwMCxcCiAgNzQsMDAsNzksMDAsNGYsMDAsNmUsMDAsNmMsMDAsNzksMDAsY2EsNTAsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXENhY2hlXERlZmF1bHRBY2NvdW50XCRxdWlldG1vbWVudHBvc3Rvb2JlJHdpbmRvd3MuZGF0YS5ub3RpZmljYXRpb25zLnF1aWV0bW9tZW50XEN1cnJlbnRdCiJEYXRhIj1oZXg6MDIsMDAsMDAsMDAsODUsZGUsNzEsNzMsMDMsODIsZGEsMDEsMDAsMDAsMDAsMDAsNDMsNDIsMDEsMDAsYzIsMGEsXAogIDAxLGMyLDE0LDAxLGQyLDFlLDI4LDRkLDAwLDY5LDAwLDYzLDAwLDcyLDAwLDZmLDAwLDczLDAwLDZmLDAwLDY2LDAwLDc0LDAwLFwKICAyZSwwMCw1MSwwMCw3NSwwMCw2OSwwMCw2NSwwMCw3NCwwMCw0OCwwMCw2ZiwwMCw3NSwwMCw3MiwwMCw3MywwMCw1MCwwMCw3MixcCiAgMDAsNmYsMDAsNjYsMDAsNjksMDAsNmMsMDAsNjUsMDAsMmUsMDAsNTAsMDAsNzIsMDAsNjksMDAsNmYsMDAsNzIsMDAsNjksMDAsXAogIDc0LDAwLDc5LDAwLDRmLDAwLDZlLDAwLDZjLDAwLDc5LDAwLGNhLDUwLDAwLDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRTdG9yZVxTdG9yZVxDYWNoZVxEZWZhdWx0QWNjb3VudFwkcXVpZXRtb21lbnRwcmVzZW50YXRpb24kd2luZG93cy5kYXRhLm5vdGlmaWNhdGlvbnMucXVpZXRtb21lbnRcQ3VycmVudF0KIkRhdGEiPWhleDowMiwwMCwwMCwwMCxhNCxmYSw3MSw3MywwMyw4MixkYSwwMSwwMCwwMCwwMCwwMCw0Myw0MiwwMSwwMCxjMiwwYSxcCiAgMDEsYzIsMTQsMDEsZDIsMWUsMjYsNGQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNmYsMDAsNzMsMDAsNmYsMDAsNjYsMDAsNzQsMDAsXAogIDJlLDAwLDUxLDAwLDc1LDAwLDY5LDAwLDY1LDAwLDc0LDAwLDQ4LDAwLDZmLDAwLDc1LDAwLDcyLDAwLDczLDAwLDUwLDAwLDcyLFwKICAwMCw2ZiwwMCw2NiwwMCw2OSwwMCw2YywwMCw2NSwwMCwyZSwwMCw0MSwwMCw2YywwMCw2MSwwMCw3MiwwMCw2ZCwwMCw3MywwMCxcCiAgNGYsMDAsNmUsMDAsNmMsMDAsNzksMDAsY2EsNTAsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXENhY2hlXERlZmF1bHRBY2NvdW50XCRxdWlldG1vbWVudHNjaGVkdWxlZCR3aW5kb3dzLmRhdGEubm90aWZpY2F0aW9ucy5xdWlldG1vbWVudFxDdXJyZW50XQoiRGF0YSI9aGV4OjAyLDAwLDAwLDAwLGZlLDE3LDcyLDczLDAzLDgyLGRhLDAxLDAwLDAwLDAwLDAwLDQzLDQyLDAxLDAwLGMyLDBhLFwKICAwMSxkMiwxZSwyOCw0ZCwwMCw2OSwwMCw2MywwMCw3MiwwMCw2ZiwwMCw3MywwMCw2ZiwwMCw2NiwwMCw3NCwwMCwyZSwwMCw1MSxcCiAgMDAsNzUsMDAsNjksMDAsNjUsMDAsNzQsMDAsNDgsMDAsNmYsMDAsNzUsMDAsNzIsMDAsNzMsMDAsNTAsMDAsNzIsMDAsNmYsMDAsXAogIDY2LDAwLDY5LDAwLDZjLDAwLDY1LDAwLDJlLDAwLDUwLDAwLDcyLDAwLDY5LDAwLDZmLDAwLDcyLDAwLDY5LDAwLDc0LDAwLDc5LFwKICAwMCw0ZiwwMCw2ZSwwMCw2YywwMCw3OSwwMCxkMSwzMiw4MCxlMCxhYSw4YSw5OSwzMCxkMSwzYyw4MCxlMCxmNixjNSxkNSwwZSxcCiAgY2EsNTAsMDAsMDAKCjsgdHVybiBvbiBkbyBub3QgZGlzdHVyYiBhdXRvbWF0aWNhbGx5CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXERlZmF1bHRBY2NvdW50XEN1cnJlbnRcZGVmYXVsdCR3aW5kb3dzLmRhdGEuZG9ub3RkaXN0dXJiLnF1aWV0bW9tZW50JHF1aWV0bW9tZW50bGlzdFx3aW5kb3dzLmRhdGEuZG9ub3RkaXN0dXJiLnF1aWV0bW9tZW50JHF1aWV0bW9tZW50cHJlc2VudGF0aW9uXQoiRGF0YSI9aGV4OjQzLDQyLDAxLDAwLDBhLDAyLDAxLDAwLDJhLDJhLDAwLDAwLDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRTdG9yZVxTdG9yZVxEZWZhdWx0QWNjb3VudFxDdXJyZW50XGRlZmF1bHQkd2luZG93cy5kYXRhLmRvbm90ZGlzdHVyYi5xdWlldG1vbWVudCRxdWlldG1vbWVudGxpc3Rcd2luZG93cy5kYXRhLmRvbm90ZGlzdHVyYi5xdWlldG1vbWVudCRxdWlldG1vbWVudGdhbWVdCiJEYXRhIj1oZXg6NDMsNDIsMDEsMDAsMGEsMDIsMDEsMDAsMmEsMmEsMDAsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXERlZmF1bHRBY2NvdW50XEN1cnJlbnRcZGVmYXVsdCR3aW5kb3dzLmRhdGEuZG9ub3RkaXN0dXJiLnF1aWV0bW9tZW50JHF1aWV0bW9tZW50bGlzdFx3aW5kb3dzLmRhdGEuZG9ub3RkaXN0dXJiLnF1aWV0bW9tZW50JHF1aWV0bW9tZW50ZnVsbHNjcmVlbl0KIkRhdGEiPWhleDo0Myw0MiwwMSwwMCwwYSwwMiwwMSwwMCwyYSwyYSwwMCwwMCwwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkU3RvcmVcU3RvcmVcRGVmYXVsdEFjY291bnRcQ3VycmVudFxkZWZhdWx0JHdpbmRvd3MuZGF0YS5kb25vdGRpc3R1cmIucXVpZXRtb21lbnQkcXVpZXRtb21lbnRsaXN0XHdpbmRvd3MuZGF0YS5kb25vdGRpc3R1cmIucXVpZXRtb21lbnQkcXVpZXRtb21lbnRwb3N0b29iZV0KIkRhdGEiPWhleDo0Myw0MiwwMSwwMCwwYSwwMiwwMSwwMCwyYSwyYSwwMCwwMCwwMAoKOyBzZXQgcHJpb3JpdHkgbm90aWZpY2F0aW9ucwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRTdG9yZVxTdG9yZVxEZWZhdWx0QWNjb3VudFxDdXJyZW50XGRlZmF1bHQkd2luZG93cy5kYXRhLmRvbm90ZGlzdHVyYi5xdWlldGhvdXJzcHJvZmlsZSRxdWlldGhvdXJzcHJvZmlsZWxpc3Rcd2luZG93cy5kYXRhLmRvbm90ZGlzdHVyYi5xdWlldGhvdXJzcHJvZmlsZSRtaWNyb3NvZnQucXVpZXRob3Vyc3Byb2ZpbGUucHJpb3JpdHlvbmx5XQoiRGF0YSI9aGV4OjQzLDQyLDAxLDAwLDBhLDAyLDAxLDAwLDJhLDJhLDAwLDAwLDAwCgo7IGZvY3VzIHNldHRpbmdzClstSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRTdG9yZVxTdG9yZVxEZWZhdWx0QWNjb3VudFxDdXJyZW50XGRlZmF1bHQkd2luZG93cy5kYXRhLnNoZWxsLmZvY3Vzc2Vzc2lvbmFjdGl2ZXRoZW1lXHdpbmRvd3MuZGF0YS5zaGVsbC5mb2N1c3Nlc3Npb25hY3RpdmV0aGVtZSR7MWIwMTkzNjUtMjVhNS00ZmYxLWI1MGEtYzE1NTIyOWFmYzhmfV0KCjsgb3B0aW9ucyBvcHRpbWl6ZSBmb3IgdmlkZW8gcXVhbGl0eQpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFZpZGVvU2V0dGluZ3NdCgo7IHN0b3JhZ2Ugc2Vuc2UKWy1IS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcU3RvcmFnZVNlbnNlXQoKOyBrZWVwIHdpbmRvd3MgcnVubmluZyBzbW9vdGhseQpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFN0b3JhZ2VTZW5zZV0KCjsgZHJhZyB0cmF5CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDRFBdCiJEcmFnVHJheUVuYWJsZWQiPS0KCjsgc25hcCB3aW5kb3cgc2V0dGluZ3MKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiU25hcEFzc2lzdCI9LQoiRElUZXN0Ij0tCiJFbmFibGVTbmFwQmFyIj0tCiJFbmFibGVUYXNrR3JvdXBzIj0tCiJFbmFibGVTbmFwQXNzaXN0Rmx5b3V0Ij0tCiJTbmFwRmlsbCI9LQoiSm9pbnRSZXNpemUiPS0KCjsgZGlzYWJsZSBlbmR0YXNrIG1lbnUgdGFza2JhcgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRcVGFza2JhckRldmVsb3BlclNldHRpbmdzXQoiVGFza2JhckVuZFRhc2siPWR3b3JkOjAwMDAwMDAwCgo7IGxvbmcgcGF0aHMKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcQ29udHJvbFxGaWxlU3lzdGVtXQoiTG9uZ1BhdGhzRW5hYmxlZCI9LQoKOyBhbHQgdGFiIG9wZW4KW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiTXVsdGlUYXNraW5nQWx0VGFiRmlsdGVyIj0tCgo7IHNoYXJlIGFjcm9zcyBkZXZpY2VzCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDRFBdCiJSb21lU2RrQ2hhbm5lbFVzZXJBdXRoelBvbGljeSI9ZHdvcmQ6MDAwMDAwMDEKIkNkcFNlc3Npb25Vc2VyQXV0aHpQb2xpY3kiPS0KCjsgcmVjb21tZW5kZWQgdHJvdWJsZXNob290ZXIgcHJlZmVyZW5jZXMKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c01pdGlnYXRpb25dCiJVc2VyUHJlZmVyZW5jZSI9LQoKCgoKOyAtLU9USEVSLS0KCgoKCjsgU1RPUkUKOyB1cGRhdGUgYXBwcyBhdXRvbWF0aWNhbGx5ClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFdpbmRvd3NTdG9yZVxXaW5kb3dzVXBkYXRlXQoKCgoKOyAtLUNBTidUIERPIE5BVElWRUxZLS0KCgoKCjsgT0xEIFNUQVJUIE1FTlUKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxDb250cm9sXEZlYXR1cmVNYW5hZ2VtZW50XE92ZXJyaWRlc1wxNFwyNzkyNTYyODI5XQoiRW5hYmxlZFN0YXRlIj0tCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXENvbnRyb2xcRmVhdHVyZU1hbmFnZW1lbnRcT3ZlcnJpZGVzXDE0XDMwMzYyNDE1NDhdCiJFbmFibGVkU3RhdGUiPS0KCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcQ29udHJvbFxGZWF0dXJlTWFuYWdlbWVudFxPdmVycmlkZXNcMTRcNzM0NzMxNDA0XQoiRW5hYmxlZFN0YXRlIj0tCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXENvbnRyb2xcRmVhdHVyZU1hbmFnZW1lbnRcT3ZlcnJpZGVzXDE0XDc2MjI1NjUyNV0KIkVuYWJsZWRTdGF0ZSI9LQoKOyBzZXQgc3RhcnQgbWVudSBhcHBzIHZpZXcgdG8gY2F0ZWdvcnkKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFN0YXJ0XQoiQWxsQXBwc1ZpZXdNb2RlIj1kd29yZDowMDAwMDAwMAoKWy1IS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xFeHBsb3Jlcl0KCgoKCjsgVVdQIEFQUFMKOyBiYWNrZ3JvdW5kIGFwcHMKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xBcHBQcml2YWN5XQoiTGV0QXBwc1J1bkluQmFja2dyb3VuZCI9LQoKOyBiYWNrZ3JvdW5kIGFwcHMgZ2xvYmFsCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTZWFyY2hdCiJCYWNrZ3JvdW5kQXBwR2xvYmFsVG9nZ2xlIj0tCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQmFja2dyb3VuZEFjY2Vzc0FwcGxpY2F0aW9uc10KIkdsb2JhbFVzZXJEaXNhYmxlZCI9LQoKOyBkaXNhYmxlIHdpbmRvd3MgaW5wdXQgZXhwZXJpZW5jZSBwcmVsb2FkCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcaW5wdXRdCiJJc0lucHV0QXBwUHJlbG9hZEVuYWJsZWQiPS0KClstSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRHNoXQoKOyB3ZWIgc2VhcmNoIGluIHN0YXJ0IG1lbnUgCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xFeHBsb3Jlcl0KIkRpc2FibGVTZWFyY2hCb3hTdWdnZXN0aW9ucyI9LQoKOyBjb3BpbG90ICYgYWkKWy1IS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xXaW5kb3dzQ29waWxvdF0KClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXFdpbmRvd3NDb3BpbG90XQoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiU2hvd0NvcGlsb3RCdXR0b24iPS0KCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcV2luZG93c0FJXQoiRGlzYWJsZUFJRGF0YUFuYWx5c2lzIj0tCiJBbGxvd1JlY2FsbEVuYWJsZW1lbnQiPS0KIkRpc2FibGVDbGlja1RvRG8iPS0KCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcU2hlbGxcQ29waWxvdFxCaW5nQ2hhdF0KIklzVXNlckVsaWdpYmxlIj0tCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFBvbGljaWVzXFBhaW50XQoiRGlzYWJsZUdlbmVyYXRpdmVGaWxsIj0tCiJEaXNhYmxlQ29jcmVhdG9yIj0tCiJEaXNhYmxlSW1hZ2VDcmVhdG9yIj0tCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXFdpbmRvd3NOb3RlcGFkXQoiRGlzYWJsZUFJRmVhdHVyZXMiPS0KCjsgd2lkZ2V0cwpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxQb2xpY3lNYW5hZ2VyXGRlZmF1bHRcTmV3c0FuZEludGVyZXN0c1xBbGxvd05ld3NBbmRJbnRlcmVzdHNdCiJ2YWx1ZSI9ZHdvcmQ6MDAwMDAwMDEKCjsgbXMtZ2FtZWJhciBub3RpZmljYXRpb25zIHdpdGggeGJveCBjb250cm9sbGVyIHBsdWdnZWQgaW4gcmVnZWRpdApbLUhLRVlfQ0xBU1NFU19ST09UXG1zLWdhbWViYXJdCgpbSEtFWV9DTEFTU0VTX1JPT1RcbXMtZ2FtZWJhcl0KIlVSTCBQcm90b2NvbCI9IiIKQD0iVVJMOm1zLWdhbWViYXIiCgpbLUhLRVlfQ0xBU1NFU19ST09UXG1zLWdhbWViYXJcc2hlbGxcb3Blblxjb21tYW5kXQoKWy1IS0VZX0NMQVNTRVNfUk9PVFxtcy1nYW1lYmFyc2VydmljZXNdCgpbLUhLRVlfQ0xBU1NFU19ST09UXG1zLWdhbWViYXJzZXJ2aWNlc1xzaGVsbFxvcGVuXGNvbW1hbmRdCgpbLUhLRVlfQ0xBU1NFU19ST09UXG1zLWdhbWluZ292ZXJsYXldCgpbSEtFWV9DTEFTU0VTX1JPT1RcbXMtZ2FtaW5nb3ZlcmxheV0KIlVSTCBQcm90b2NvbCI9IiIKQD0iVVJMOm1zLWdhbWluZ292ZXJsYXkiCgpbLUhLRVlfQ0xBU1NFU19ST09UXG1zLWdhbWluZ292ZXJsYXlcc2hlbGxcb3Blblxjb21tYW5kXQoKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1J1bnRpbWVcQWN0aXZhdGFibGVDbGFzc0lkXFdpbmRvd3MuR2FtaW5nLkdhbWVCYXIuUHJlc2VuY2VTZXJ2ZXIuSW50ZXJuYWwuUHJlc2VuY2VXcml0ZXJdCiJBY3RpdmF0aW9uVHlwZSI9ZHdvcmQ6MDAwMDAwMDEKCgoKCjsgQURWRVJUSVNJTkcgJiBQUk9NT1RJT05BTApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ29udGVudERlbGl2ZXJ5TWFuYWdlcl0KIkNvbnRlbnREZWxpdmVyeUFsbG93ZWQiPWR3b3JkOjAwMDAwMDAxCiJGZWF0dXJlTWFuYWdlbWVudEVuYWJsZWQiPWR3b3JkOjAwMDAwMDAxCiJPZW1QcmVJbnN0YWxsZWRBcHBzRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDEKIlByZUluc3RhbGxlZEFwcHNFbmFibGVkIj1kd29yZDowMDAwMDAwMQoiUHJlSW5zdGFsbGVkQXBwc0V2ZXJFbmFibGVkIj1kd29yZDowMDAwMDAwMQoiUm90YXRpbmdMb2NrU2NyZWVuRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDEKIlJvdGF0aW5nTG9ja1NjcmVlbk92ZXJsYXlFbmFibGVkIj1kd29yZDowMDAwMDAwMQoiU2lsZW50SW5zdGFsbGVkQXBwc0VuYWJsZWQiPWR3b3JkOjAwMDAwMDAxCiJTbGlkZXNob3dFbmFibGVkIj1kd29yZDowMDAwMDAwMQoiU29mdExhbmRpbmdFbmFibGVkIj1kd29yZDowMDAwMDAwMQoiU3Vic2NyaWJlZENvbnRlbnQtMzEwMDkzRW5hYmxlZCI9LQoiU3Vic2NyaWJlZENvbnRlbnQtMzE0NTYzRW5hYmxlZCI9LQoiU3Vic2NyaWJlZENvbnRlbnQtMzM4Mzg4RW5hYmxlZCI9LQoiU3Vic2NyaWJlZENvbnRlbnQtMzM4Mzg5RW5hYmxlZCI9LQoiU3Vic2NyaWJlZENvbnRlbnQtMzM4MzkzRW5hYmxlZCI9LQoiU3Vic2NyaWJlZENvbnRlbnQtMzUzNjk0RW5hYmxlZCI9LQoiU3Vic2NyaWJlZENvbnRlbnQtMzUzNjk2RW5hYmxlZCI9LQoiU3Vic2NyaWJlZENvbnRlbnQtMzUzNjk4RW5hYmxlZCI9LQoiU3Vic2NyaWJlZENvbnRlbnRFbmFibGVkIj1kd29yZDowMDAwMDAwMQoiU3lzdGVtUGFuZVN1Z2dlc3Rpb25zRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDEKCgoKCjsgT1RIRVIKOyAzZCBvYmplY3RzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcTXlDb21wdXRlclxOYW1lU3BhY2VcezBEQjdFMDNGLUZDMjktNERDNi05MDIwLUZGNDFCNTlFNTEzQX1dCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFdPVzY0MzJOb2RlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXE15Q29tcHV0ZXJcTmFtZVNwYWNlXHswREI3RTAzRi1GQzI5LTREQzYtOTAyMC1GRjQxQjU5RTUxM0F9XQoKOyBxdWljayBhY2Nlc3MKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3Jlcl0KIkh1Yk1vZGUiPS0KCjsgaG9tZQpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXENsYXNzZXNcQ0xTSURce2Y4NzQzMTBlLWI2YjctNDdkYy1iYzg0LWI5ZTZiMzhmNTkwM31dCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXERlc2t0b3BcTmFtZVNwYWNlXHtmODc0MzEwZS1iNmI3LTQ3ZGMtYmM4NC1iOWU2YjM4ZjU5MDN9XQoiSGlkZGVuQnlEZWZhdWx0Ij0tCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFdPVzY0MzJOb2RlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXERlc2t0b3BcTmFtZVNwYWNlXHtmODc0MzEwZS1iNmI3LTQ3ZGMtYmM4NC1iOWU2YjM4ZjU5MDN9XQpAPSJDTFNJRF9NU0dyYXBoSG9tZUZvbGRlciIKCjsgZ2FsbGVyeQpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXENsYXNzZXNcQ0xTSURce2U4ODg2NWVhLTBlMWMtNGUyMC05YWE2LWVkY2QwMjEyYzg3Y31dCgo7IGNvbnRleHQgbWVudQpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXENsYXNzZXNcQ0xTSURcezg2Y2ExYWEwLTM0YWEtNGU4Yi1hNTA5LTUwYzkwNWJhZTJhMn1dCgo7IG1lbnUgc2hvdyBkZWxheQpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxEZXNrdG9wXQoiTWVudVNob3dEZWxheSI9IjQwMCIKCjsgZHJpdmVyIHNlYXJjaGluZyAmIHVwZGF0ZXMKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxEcml2ZXJTZWFyY2hpbmddCiJTZWFyY2hPcmRlckNvbmZpZyI9ZHdvcmQ6MDAwMDAwMDEKCjsgbW91c2UgKGRlZmF1bHQgYWNjZWwgd2l0aCBlcHAgb24pCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXE1vdXNlXQoiTW91c2VTZW5zaXRpdml0eSI9IjEwIgoiU21vb3RoTW91c2VYQ3VydmUiPWhleDowMCwwMCwwMCwwMCwwMCwwMCwwMCwwMCwxNSw2ZSwwMCwwMCwwMCwwMCwwMCwwMCwwMCw0MCxcCiAgMDEsMDAsMDAsMDAsMDAsMDAsMjksZGMsMDMsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMjgsMDAsMDAsMDAsMDAsMDAKIlNtb290aE1vdXNlWUN1cnZlIj1oZXg6MDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsZmQsMTEsMDEsMDAsMDAsMDAsMDAsMDAsMDAsMjQsXAogIDA0LDAwLDAwLDAwLDAwLDAwLDAwLGZjLDEyLDAwLDAwLDAwLDAwLDAwLDAwLGMwLGJiLDAxLDAwLDAwLDAwLDAwCgpbSEtFWV9VU0VSU1wuREVGQVVMVFxDb250cm9sIFBhbmVsXE1vdXNlXQoiTW91c2VTcGVlZCI9IjEiCiJNb3VzZVRocmVzaG9sZDEiPSI2IgoiTW91c2VUaHJlc2hvbGQyIj0iMTAiCgo7IHBob25lIGNvbXBhbmlvbiBpbiBzdGFydCBtZW51CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTdGFydF0KIlJpZ2h0Q29tcGFuaW9uVG9nZ2xlZE9wZW4iPS0KCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTdGFydFxDb21wYW5pb25zXE1pY3Jvc29mdC5Zb3VyUGhvbmVfOHdla3liM2Q4YmJ3ZV0KIklzRW5hYmxlZCI9LQoiSXNBdmFpbGFibGUiPS0KCjsgcmVtb3ZlIG1vcmUgaW5mbyBvbiBic29kCltIS0VZX0xPQ0FMX01BQ0hJTkVcU3lzdGVtXEN1cnJlbnRDb250cm9sU2V0XENvbnRyb2xcQ3Jhc2hDb250cm9sXQoiRGlzcGxheVBhcmFtZXRlcnMiPWR3b3JkOjAwMDAwMDAwCgo7IHdpbmRvd3MgcGxhdGZvcm0gYmluYXJ5IHRhYmxlCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXEN1cnJlbnRDb250cm9sU2V0XENvbnRyb2xcU2Vzc2lvbiBNYW5hZ2VyXQoiRGlzYWJsZVdwYnRFeGVjdXRpb24iPS0KCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcQ29udHJvbFxTZXNzaW9uIE1hbmFnZXJdCiJEaXNhYmxlV3BidEV4ZWN1dGlvbiI9LQoKOyB3ZWIgc2VydmljZXMgaW4gZXhwbG9yZXIKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxQb2xpY2llc1xFeHBsb3Jlcl0KIk5vV2ViU2VydmljZXMiPS0KCjsgY3Jvc3MgZGV2aWNlIHJlc3VtZQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ3Jvc3NEZXZpY2VSZXN1bWVcQ29uZmlndXJhdGlvbl0KIklzUmVzdW1lQWxsb3dlZCI9LQoiSXNPbmVEcml2ZVJlc3VtZUFsbG93ZWQiPS0KCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFBvbGljeU1hbmFnZXJcZGVmYXVsdFxDb25uZWN0aXZpdHlcRGlzYWJsZUNyb3NzRGV2aWNlUmVzdW1lXQoidmFsdWUiPS0KCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcQ29udHJvbFxGZWF0dXJlTWFuYWdlbWVudFxPdmVycmlkZXNcOFwxMzg3MDIwOTQzXQoiRW5hYmxlZFN0YXRlIj0tCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXENvbnRyb2xcRmVhdHVyZU1hbmFnZW1lbnRcT3ZlcnJpZGVzXDhcMTY5NDY2MTI2MF0KIkVuYWJsZWRTdGF0ZSI9LQoKOyBob21lIGluIHNldHRpbmdzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcRXhwbG9yZXJdCiJTZXR0aW5nc1BhZ2VWaXNpYmlsaXR5Ij0tCgo7IG9wZW4gdGVybWluYWwgYnkgZGVmYXVsdApbSEtFWV9DVVJSRU5UX1VTRVJcQ29uc29sZVwlJVN0YXJ0dXBdCiJEZWxlZ2F0aW9uQ29uc29sZSI9LQoiRGVsZWdhdGlvblRlcm1pbmFsIj0tCgo7IGRlZmF1bHQgcG93ZXJzaGVsbCBjb25zb2xlCltIS0VZX0NVUlJFTlRfVVNFUlxDb25zb2xlXCVTeXN0ZW1Sb290JV9TeXN0ZW0zMl9XaW5kb3dzUG93ZXJTaGVsbF92MS4wX3Bvd2Vyc2hlbGwuZXhlXQoiU2NyZWVuQ29sb3JzIj1kd29yZDowMDAwMDA1NgoKOyByZW1vdmUgZml4IGVudGVyIHlvdXIgcGluIGhlbGxvIGZhY2Ugc2lnbiBpbiBidWcgYWxsb3cgcGFzc3dvcmQgaW5zdGVhZApbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzIE5UXEN1cnJlbnRWZXJzaW9uXFBhc3N3b3JkTGVzc1xEZXZpY2VdCiJEZXZpY2VQYXNzd29yZExlc3NCdWlsZFZlcnNpb24iPWR3b3JkOjAwMDAwMDAyCgo7IHJldmVydCBmaW5pc2ggc2V0dGluZyB1cCB5b3VyIGRldmljZQpbLUhLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFVzZXJQcm9maWxlRW5nYWdlbWVudF0KCjsgcmV2ZXJ0IGJhY2tncm91bmQgYmx1ciBkdXJpbmcgc2lnbi1pbgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXFN5c3RlbV0KIkRpc2FibGVBY3J5bGljQmFja2dyb3VuZE9uTG9nb24iPS0K'
$sync.assets.registryoptimize = '77u/V2luZG93cyBSZWdpc3RyeSBFZGl0b3IgVmVyc2lvbiA1LjAwCgo7IC0tTEVHQUNZIENPTlRST0wgUEFORUwtLQoKCgoKOyBFQVNFIE9GIEFDQ0VTUwo7IGRpc2FibGUgbmFycmF0b3IKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxOYXJyYXRvclxOb1JvYW1dCiJEdWNrQXVkaW8iPWR3b3JkOjAwMDAwMDAwCiJXaW5FbnRlckxhdW5jaEVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJTY3JpcHRpbmdFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiT25saW5lU2VydmljZXNFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxOYXJyYXRvcl0KIk5hcnJhdG9yQ3Vyc29ySGlnaGxpZ2h0Ij1kd29yZDowMDAwMDAwMAoiQ291cGxlTmFycmF0b3JDdXJzb3JLZXlib2FyZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBlYXNlIG9mIGFjY2VzcyBzZXR0aW5ncyAKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxFYXNlIG9mIEFjY2Vzc10KInNlbGZ2b2ljZSI9ZHdvcmQ6MDAwMDAwMDAKInNlbGZzY2FuIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eV0KIlNvdW5kIG9uIEFjdGl2YXRpb24iPWR3b3JkOjAwMDAwMDAwCiJXYXJuaW5nIFNvdW5kcyI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXEFjY2Vzc2liaWxpdHlcSGlnaENvbnRyYXN0XQoiRmxhZ3MiPSI0MTk0IgoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eVxLZXlib2FyZCBSZXNwb25zZV0KIkZsYWdzIj0iMiIKIkF1dG9SZXBlYXRSYXRlIj0iMCIKIkF1dG9SZXBlYXREZWxheSI9IjAiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxBY2Nlc3NpYmlsaXR5XE1vdXNlS2V5c10KIkZsYWdzIj0iMTMwIgoiTWF4aW11bVNwZWVkIj0iMzkiCiJUaW1lVG9NYXhpbXVtU3BlZWQiPSIzMDAwIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eVxTdGlja3lLZXlzXQoiRmxhZ3MiPSIyIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQWNjZXNzaWJpbGl0eVxUb2dnbGVLZXlzXQoiRmxhZ3MiPSIzNCIKCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXEFjY2Vzc2liaWxpdHlcU291bmRTZW50cnldCiJGbGFncyI9IjAiCiJGU1RleHRFZmZlY3QiPSIwIgoiVGV4dEVmZmVjdCI9IjAiCiJXaW5kb3dzRWZmZWN0Ij0iMCIKCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXEFjY2Vzc2liaWxpdHlcU2xhdGVMYXVuY2hdCiJBVGFwcCI9IiIKIkxhdW5jaEFUIj1kd29yZDowMDAwMDAwMAoKCgoKOyBDTE9DSyBBTkQgUkVHSU9OCjsgZGlzYWJsZSBub3RpZnkgbWUgd2hlbiB0aGUgY2xvY2sgY2hhbmdlcwpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxUaW1lRGF0ZV0KIkRzdE5vdGlmaWNhdGlvbiI9ZHdvcmQ6MDAwMDAwMDAKCgoKCjsgQVBQRUFSQU5DRSBBTkQgUEVSU09OQUxJWkFUSU9OCjsgb3BlbiBmaWxlIGV4cGxvcmVyIHRvIHRoaXMgcGMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiTGF1bmNoVG8iPWR3b3JkOjAwMDAwMDAxCgo7IGhpZGUgZnJlcXVlbnQgZm9sZGVycyBpbiBxdWljayBhY2Nlc3MKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXQoiU2hvd0ZyZXF1ZW50Ij1kd29yZDowMDAwMDAwMAoKOyBzaG93IGZpbGUgbmFtZSBleHRlbnNpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIkhpZGVGaWxlRXh0Ij1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHNlYXJjaCBoaXN0b3J5CltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTZWFyY2hTZXR0aW5nc10KIklzRGV2aWNlU2VhcmNoSGlzdG9yeUVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgc2hvdyBmaWxlcyBmcm9tIG9mZmljZS5jb20KW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXQoiU2hvd0Nsb3VkRmlsZXNJblF1aWNrQWNjZXNzIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIGRpc3BsYXkgZmlsZSBzaXplIGluZm9ybWF0aW9uIGluIGZvbGRlciB0aXBzCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIkZvbGRlckNvbnRlbnRzSW5mb1RpcCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZW5hYmxlIGRpc3BsYXkgZnVsbCBwYXRoIGluIHRoZSB0aXRsZSBiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXENhYmluZXRTdGF0ZV0KIkZ1bGxQYXRoIj1kd29yZDowMDAwMDAwMQoKOyBkaXNhYmxlIHNob3cgcG9wLXVwIGRlc2NyaXB0aW9uIGZvciBmb2xkZXIgYW5kIGRlc2t0b3AgaXRlbXMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiU2hvd0luZm9UaXAiPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgc2hvdyBwcmV2aWV3IGhhbmRsZXJzIGluIHByZXZpZXcgcGFuZQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJTaG93UHJldmlld0hhbmRsZXJzIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHNob3cgc3RhdHVzIGJhcgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJTaG93U3RhdHVzQmFyIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHNob3cgc3luYyBwcm92aWRlciBub3RpZmljYXRpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNob3dTeW5jUHJvdmlkZXJOb3RpZmljYXRpb25zIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHVzZSBzaGFyaW5nIHdpemFyZApbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJTaGFyaW5nV2l6YXJkT24iPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgc2hvdyBuZXR3b3JrCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxDbGFzc2VzXENMU0lEXHtGMDJDMUEwRC1CRTIxLTQzNTAtODhCMC03MzY3RkM5NkVGM0N9XQoiU3lzdGVtLklzUGlubmVkVG9OYW1lU3BhY2VUcmVlIj1kd29yZDowMDAwMDAwMAoKCgoKOyBIQVJEV0FSRSBBTkQgU09VTkQKOyBkaXNhYmxlIGxvY2sKW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxGbHlvdXRNZW51U2V0dGluZ3NdCiJTaG93TG9ja09wdGlvbiI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBzbGVlcApbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEZseW91dE1lbnVTZXR0aW5nc10KIlNob3dTbGVlcE9wdGlvbiI9ZHdvcmQ6MDAwMDAwMDAKCjsgc291bmQgY29tbXVuaWNhdGlvbnMgZG8gbm90aGluZwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XE11bHRpbWVkaWFcQXVkaW9dCiJVc2VyRHVja2luZ1ByZWZlcmVuY2UiPWR3b3JkOjAwMDAwMDAzCgo7IGRpc2FibGUgc3RhcnR1cCBzb3VuZApbSEtFWV9MT0NBTF9NQUNISU5FXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEF1dGhlbnRpY2F0aW9uXExvZ29uVUlcQm9vdEFuaW1hdGlvbl0KIkRpc2FibGVTdGFydHVwU291bmQiPWR3b3JkOjAwMDAwMDAxCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEVkaXRpb25PdmVycmlkZXNdCiJVc2VyU2V0dGluZ19EaXNhYmxlU3RhcnR1cFNvdW5kIj1kd29yZDowMDAwMDAwMQoKOyBzb3VuZCBzY2hlbWUgbm9uZQpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNdCkA9Ii5Ob25lIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcLkRlZmF1bHRcLkRlZmF1bHRcLkN1cnJlbnRdCkA9IiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XENyaXRpY2FsQmF0dGVyeUFsYXJtXC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxEZXZpY2VDb25uZWN0XC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxEZXZpY2VEaXNjb25uZWN0XC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxEZXZpY2VGYWlsXC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxGYXhCZWVwXC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxMb3dCYXR0ZXJ5QWxhcm1cLkN1cnJlbnRdCkA9IiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XE1haWxCZWVwXC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxNZXNzYWdlTnVkZ2VcLkN1cnJlbnRdCkA9IiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XE5vdGlmaWNhdGlvbi5EZWZhdWx0XC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxOb3RpZmljYXRpb24uSU1cLkN1cnJlbnRdCkA9IiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XE5vdGlmaWNhdGlvbi5NYWlsXC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxOb3RpZmljYXRpb24uUHJveGltaXR5XC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxOb3RpZmljYXRpb24uUmVtaW5kZXJcLkN1cnJlbnRdCkA9IiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XE5vdGlmaWNhdGlvbi5TTVNcLkN1cnJlbnRdCkA9IiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XFByb3hpbWl0eUNvbm5lY3Rpb25cLkN1cnJlbnRdCkA9IiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXC5EZWZhdWx0XFN5c3RlbUFzdGVyaXNrXC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxTeXN0ZW1FeGNsYW1hdGlvblwuQ3VycmVudF0KQD0iIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcLkRlZmF1bHRcU3lzdGVtSGFuZFwuQ3VycmVudF0KQD0iIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcLkRlZmF1bHRcU3lzdGVtTm90aWZpY2F0aW9uXC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1wuRGVmYXVsdFxXaW5kb3dzVUFDXC5DdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1xzYXBpc3ZyXERpc051bWJlcnNTb3VuZFwuY3VycmVudF0KQD0iIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcc2FwaXN2clxIdWJPZmZTb3VuZFwuY3VycmVudF0KQD0iIgoKW0hLRVlfQ1VSUkVOVF9VU0VSXEFwcEV2ZW50c1xTY2hlbWVzXEFwcHNcc2FwaXN2clxIdWJPblNvdW5kXC5jdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1xzYXBpc3ZyXEh1YlNsZWVwU291bmRcLmN1cnJlbnRdCkA9IiIKCltIS0VZX0NVUlJFTlRfVVNFUlxBcHBFdmVudHNcU2NoZW1lc1xBcHBzXHNhcGlzdnJcTWlzcmVjb1NvdW5kXC5jdXJyZW50XQpAPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQXBwRXZlbnRzXFNjaGVtZXNcQXBwc1xzYXBpc3ZyXFBhbmVsU291bmRcLmN1cnJlbnRdCkA9IiIKCjsgZGlzYWJsZSBhdXRvcGxheQpbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQXV0b3BsYXlIYW5kbGVyc10KIkRpc2FibGVBdXRvcGxheSI9ZHdvcmQ6MDAwMDAwMDEKCjsgZGlzYWJsZSBlbmhhbmNlIHBvaW50ZXIgcHJlY2lzaW9uCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXE1vdXNlXQoiTW91c2VTcGVlZCI9IjAiCiJNb3VzZVRocmVzaG9sZDEiPSIwIgoiTW91c2VUaHJlc2hvbGQyIj0iMCIKCjsgbW91c2UgcG9pbnRlcnMgc2NoZW1lIG5vbmUKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcQ3Vyc29yc10KIkFwcFN0YXJ0aW5nIj1oZXgoMik6MDAsMDAKIkFycm93Ij1oZXgoMik6MDAsMDAKIkNvbnRhY3RWaXN1YWxpemF0aW9uIj1kd29yZDowMDAwMDAwMAoiQ3Jvc3NoYWlyIj1oZXgoMik6MDAsMDAKIkdlc3R1cmVWaXN1YWxpemF0aW9uIj1kd29yZDowMDAwMDAwMAoiSGFuZCI9aGV4KDIpOjAwLDAwCiJIZWxwIj1oZXgoMik6MDAsMDAKIklCZWFtIj1oZXgoMik6MDAsMDAKIk5vIj1oZXgoMik6MDAsMDAKIk5XUGVuIj1oZXgoMik6MDAsMDAKIlNjaGVtZSBTb3VyY2UiPWR3b3JkOjAwMDAwMDAwCiJTaXplQWxsIj1oZXgoMik6MDAsMDAKIlNpemVORVNXIj1oZXgoMik6MDAsMDAKIlNpemVOUyI9aGV4KDIpOjAwLDAwCiJTaXplTldTRSI9aGV4KDIpOjAwLDAwCiJTaXplV0UiPWhleCgyKTowMCwwMAoiVXBBcnJvdyI9aGV4KDIpOjAwLDAwCiJXYWl0Ij1oZXgoMik6MDAsMDAKQD0iIgoKOyBkaXNhYmxlIGRldmljZSBpbnN0YWxsYXRpb24gc2V0dGluZ3MKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxEZXZpY2UgTWV0YWRhdGFdCiJQcmV2ZW50RGV2aWNlTWV0YWRhdGFGcm9tTmV0d29yayI9ZHdvcmQ6MDAwMDAwMDEKCgoKCjsgTkVUV09SSyBBTkQgSU5URVJORVQKOyBkaXNhYmxlIGFsbG93IG90aGVyIG5ldHdvcmsgdXNlcnMgdG8gY29udHJvbCBvciBkaXNhYmxlIHRoZSBzaGFyZWQgaW50ZXJuZXQgY29ubmVjdGlvbgpbSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDb250cm9sU2V0MDAxXENvbnRyb2xcTmV0d29ya1xTaGFyZWRBY2Nlc3NDb25uZWN0aW9uXQoiRW5hYmxlQ29udHJvbCI9ZHdvcmQ6MDAwMDAwMDAKCgoKCjsgU1lTVEVNIEFORCBTRUNVUklUWQo7IGRpc2FibGUgZGVmcmFnbWVudCBhbmQgb3B0aW1pemUgeW91ciBkcml2ZXMKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcRGZyZ1xUYXNrU2V0dGluZ3NdCiJmQWxsVm9sdW1lcyI9ZHdvcmQ6MDAwMDAwMDEKImZEZWFkbGluZUVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJmRXhjbHVkZSI9ZHdvcmQ6MDAwMDAwMDAKImZUYXNrRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKImZVcGdyYWRlUmVzdG9yZWQiPWR3b3JkOjAwMDAwMDAxCiJUYXNrRnJlcXVlbmN5Ij1kd29yZDowMDAwMDAwNAoiVm9sdW1lcyI9IiAiCgo7IHNldCBhcHBlYXJhbmNlIG9wdGlvbnMgdG8gY3VzdG9tCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxWaXN1YWxFZmZlY3RzXQoiVmlzdWFsRlhTZXR0aW5nIj1kd29yZDozCgo7IGVuYWJsZSBhbmltYXRlIGNvbnRyb2xzIGFuZCBlbGVtZW50cyBpbnNpZGUgd2luZG93cyAoZGlzYWJsZWQgYnJlYWtzIGluc3RhZ3JhbSBzY3JvbGxpbmcpCjsgZGlzYWJsZSBmYWRlIG9yIHNsaWRlIG1lbnVzIGludG8gdmlldwo7IGRpc2FibGUgZmFkZSBvciBzbGlkZSB0b29sdGlwcyBpbnRvIHZpZXcKOyBkaXNhYmxlIGZhZGUgb3V0IG1lbnUgaXRlbXMgYWZ0ZXIgY2xpY2tpbmcKOyBkaXNhYmxlIHNob3cgc2hhZG93cyB1bmRlciBtb3VzZSBwb2ludGVyCjsgZGlzYWJsZSBzaG93IHNoYWRvd3MgdW5kZXIgd2luZG93cwo7IGRpc2FibGUgc2xpZGUgb3BlbiBjb21ibyBib3hlcwo7IGRpc2FibGUgc21vb3RoLXNjcm9sbCBsaXN0IGJveGVzCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXERlc2t0b3BdCiJVc2VyUHJlZmVyZW5jZXNNYXNrIj1oZXgoMik6OTAsMTIsMDMsODAsMTIsMDAsMDAsMDAKCjsgZGlzYWJsZSBhbmltYXRlIHdpbmRvd3Mgd2hlbiBtaW5pbWl6aW5nIGFuZCBtYXhpbWl6aW5nCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXERlc2t0b3BcV2luZG93TWV0cmljc10KIk1pbkFuaW1hdGUiPSIwIgoKOyBkaXNhYmxlIGFuaW1hdGlvbnMgaW4gdGhlIHRhc2tiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiVGFza2JhckFuaW1hdGlvbnMiPWR3b3JkOjAKCjsgZGlzYWJsZSBlbmFibGUgcGVlawpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcRFdNXQoiRW5hYmxlQWVyb1BlZWsiPWR3b3JkOjAKCjsgZGlzYWJsZSBzYXZlIHRhc2tiYXIgdGh1bWJuYWlsIHByZXZpZXdzCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xEV01dCiJBbHdheXNIaWJlcm5hdGVUaHVtYm5haWxzIj1kd29yZDowCgo7IGVuYWJsZSBzaG93IHRodW1ibmFpbHMgaW5zdGVhZCBvZiBpY29ucwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJJY29uc09ubHkiPWR3b3JkOjAKCjsgZGlzYWJsZSBzaG93IHRyYW5zbHVjZW50IHNlbGVjdGlvbiByZWN0YW5nbGUKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiTGlzdHZpZXdBbHBoYVNlbGVjdCI9ZHdvcmQ6MAoKOyBkaXNhYmxlIHNob3cgd2luZG93IGNvbnRlbnRzIHdoaWxlIGRyYWdnaW5nCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXERlc2t0b3BdCiJEcmFnRnVsbFdpbmRvd3MiPSIwIgoKOyBlbmFibGUgc21vb3RoIGVkZ2VzIG9mIHNjcmVlbiBmb250cwpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxEZXNrdG9wXQoiRm9udFNtb290aGluZyI9IjIiCgo7IGRpc2FibGUgdXNlIGRyb3Agc2hhZG93cyBmb3IgaWNvbiBsYWJlbHMgb24gdGhlIGRlc2t0b3AKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiTGlzdHZpZXdTaGFkb3ciPWR3b3JkOjAKCjsgYWRqdXN0IGZvciBiZXN0IHBlcmZvcm1hbmNlIG9mIHByb2dyYW1zCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXEN1cnJlbnRDb250cm9sU2V0XENvbnRyb2xcUHJpb3JpdHlDb250cm9sXQoiV2luMzJQcmlvcml0eVNlcGFyYXRpb24iPWR3b3JkOjAwMDAwMDI2Cgo7IGRpc2FibGUgcmVtb3RlIGFzc2lzdGFuY2UKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcQ29udHJvbFxSZW1vdGUgQXNzaXN0YW5jZV0KImZBbGxvd1RvR2V0SGVscCI9ZHdvcmQ6MDAwMDAwMDAKCgoKCjsgVFJPVUJMRVNIT09USU5HCjsgZGlzYWJsZSBhdXRvbWF0aWMgbWFpbnRlbmFuY2UKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBOVFxDdXJyZW50VmVyc2lvblxTY2hlZHVsZVxNYWludGVuYW5jZV0KIk1haW50ZW5hbmNlRGlzYWJsZWQiPWR3b3JkOjAwMDAwMDAxCgoKCgo7IFNFQ1VSSVRZIEFORCBNQUlOVEVOQU5DRQo7IGRpc2FibGUgcmVwb3J0IHByb2JsZW1zCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcV2luZG93cyBFcnJvciBSZXBvcnRpbmddCiJEaXNhYmxlZCI9ZHdvcmQ6MDAwMDAwMDEKCgoKCjsgLS1JTU1FUlNJVkUgQ09OVFJPTCBQQU5FTC0tCgoKCgo7IFdJTkRPV1MgVVBEQVRFCjsgZGlzYWJsZSBkZWxpdmVyeSBvcHRpbWl6YXRpb24KW0hLRVlfVVNFUlNcUy0xLTUtMjBcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRGVsaXZlcnlPcHRpbWl6YXRpb25cU2V0dGluZ3NdCiJEb3dubG9hZE1vZGUiPWR3b3JkOjAwMDAwMDAwCgoKCgo7IFBSSVZBQ1kKOyBkaXNhYmxlIGZpbmQgbXkgZGV2aWNlCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XE1kbUNvbW1vblxTZXR0aW5nVmFsdWVzXQoiTG9jYXRpb25TeW5jRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBzaG93IG1lIG5vdGlmaWNhdGlvbiBpbiB0aGUgc2V0dGluZ3MgYXBwCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTeXN0ZW1TZXR0aW5nc1xBY2NvdW50Tm90aWZpY2F0aW9uc10KIkVuYWJsZUFjY291bnROb3RpZmljYXRpb25zIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHRhaWxvcmVkIGV4cGVyaWVuY2VzCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDUFNTXFN0b3JlXFRhaWxvcmVkRXhwZXJpZW5jZXNXaXRoRGlhZ25vc3RpY0RhdGFFbmFibGVkXQoiVmFsdWUiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUHJpdmFjeV0KIlRhaWxvcmVkRXhwZXJpZW5jZXNXaXRoRGlhZ25vc3RpY0RhdGFFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIGxvY2F0aW9uCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGxvY2F0aW9uXQoiVmFsdWUiPSJEZW55IgoKOyBkaXNhYmxlIGFsbG93IGxvY2F0aW9uIG92ZXJyaWRlCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDUFNTXFN0b3JlXFVzZXJMb2NhdGlvbk92ZXJyaWRlUHJpdmFjeVNldHRpbmddCiJWYWx1ZSI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBub3RpZnkgd2hlbiBhcHBzIHJlcXVlc3QgbG9jYXRpb24KW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxsb2NhdGlvbl0KIlNob3dHbG9iYWxQcm9tcHRzIj1kd29yZDowMDAwMDAwMAoKOyBlbmFibGUgY2FtZXJhCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHdlYmNhbV0KIlZhbHVlIj0iQWxsb3ciCgo7IGVuYWJsZSBtaWNyb3Bob25lIApbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxtaWNyb3Bob25lXQoiVmFsdWUiPSJBbGxvdyIKCjsgZGlzYWJsZSB2b2ljZSBhY3RpdmF0aW9uCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcU3BlZWNoX09uZUNvcmVcU2V0dGluZ3NcVm9pY2VBY3RpdmF0aW9uXFVzZXJQcmVmZXJlbmNlRm9yQWxsQXBwc10KIkFnZW50QWN0aXZhdGlvbkVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFNwZWVjaF9PbmVDb3JlXFNldHRpbmdzXFZvaWNlQWN0aXZhdGlvblxVc2VyUHJlZmVyZW5jZUZvckFsbEFwcHNdCiJBZ2VudEFjdGl2YXRpb25MYXN0VXNlZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBub3RpZmljYXRpb25zCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHVzZXJOb3RpZmljYXRpb25MaXN0ZW5lcl0KIlZhbHVlIj0iRGVueSIKCjsgZGlzYWJsZSBhY2NvdW50IGluZm8KW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVcdXNlckFjY291bnRJbmZvcm1hdGlvbl0KIlZhbHVlIj0iRGVueSIKCjsgZGlzYWJsZSBjb250YWN0cwpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxjb250YWN0c10KIlZhbHVlIj0iRGVueSIKCjsgZGlzYWJsZSBjYWxlbmRhcgpbSEtFWV9MT0NBTF9NQUNISU5FXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxhcHBvaW50bWVudHNdCiJWYWx1ZSI9IkRlbnkiCgo7IGRpc2FibGUgcGhvbmUgY2FsbHMKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVccGhvbmVDYWxsXQoiVmFsdWUiPSJEZW55IgoKOyBkaXNhYmxlIGNhbGwgaGlzdG9yeQpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxwaG9uZUNhbGxIaXN0b3J5XQoiVmFsdWUiPSJEZW55IgoKOyBkaXNhYmxlIGVtYWlsCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGVtYWlsXQoiVmFsdWUiPSJEZW55IgoKOyBkaXNhYmxlIHRhc2tzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHVzZXJEYXRhVGFza3NdCiJWYWx1ZSI9IkRlbnkiCgo7IGRpc2FibGUgbWVzc2FnaW5nCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGNoYXRdCiJWYWx1ZSI9IkRlbnkiCgo7IGRpc2FibGUgcmFkaW9zCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHJhZGlvc10KIlZhbHVlIj0iRGVueSIKCjsgZGlzYWJsZSBvdGhlciBkZXZpY2VzIApbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGJsdWV0b290aFN5bmNdCiJWYWx1ZSI9IkRlbnkiCgo7IGRpc2FibGUgYXBwIGRpYWdub3N0aWNzIApbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxhcHBEaWFnbm9zdGljc10KIlZhbHVlIj0iRGVueSIKCjsgZGlzYWJsZSBkb2N1bWVudHMKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVcZG9jdW1lbnRzTGlicmFyeV0KIlZhbHVlIj0iRGVueSIKCjsgZGlzYWJsZSBkb3dubG9hZHMgZm9sZGVyIApbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxkb3dubG9hZHNGb2xkZXJdCiJWYWx1ZSI9IkRlbnkiCgo7IGRpc2FibGUgbXVzaWMgbGlicmFyeQpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVxtdXNpY0xpYnJhcnldCiJWYWx1ZSI9IkRlbnkiCgo7IGRpc2FibGUgcGljdHVyZXMKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDYXBhYmlsaXR5QWNjZXNzTWFuYWdlclxDb25zZW50U3RvcmVccGljdHVyZXNMaWJyYXJ5XQoiVmFsdWUiPSJEZW55IgoKOyBkaXNhYmxlIHZpZGVvcwpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENhcGFiaWxpdHlBY2Nlc3NNYW5hZ2VyXENvbnNlbnRTdG9yZVx2aWRlb3NMaWJyYXJ5XQoiVmFsdWUiPSJEZW55IgoKOyBkaXNhYmxlIGZpbGUgc3lzdGVtCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXGJyb2FkRmlsZVN5c3RlbUFjY2Vzc10KIlZhbHVlIj0iRGVueSIKCjsgZGlzYWJsZSB0ZXh0IGFuZCBpbWFnZSBnZW5lcmF0aW9uCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHN5c3RlbUFJTW9kZWxzXQoiVmFsdWUiPSJEZW55IgoKOyBkaXNhYmxlIHBhc3NrZXkgYWNjZXNzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHBhc3NrZXlzXQoiVmFsdWUiPSJEZW55IgoKOyBkaXNhYmxlIHBhc3NrZXkgYXV0b2ZpbGwgYWNjZXNzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2FwYWJpbGl0eUFjY2Vzc01hbmFnZXJcQ29uc2VudFN0b3JlXHBhc3NrZXlzRW51bWVyYXRpb25dCiJWYWx1ZSI9IkRlbnkiCgo7IGRpc2FibGUgbGV0IHdlYnNpdGVzIHNob3cgbWUgbG9jYWxseSByZWxldmFudCBjb250ZW50IGJ5IGFjY2Vzc2luZyBteSBsYW5ndWFnZSBsaXN0IApbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxJbnRlcm5hdGlvbmFsXFVzZXIgUHJvZmlsZV0KIkh0dHBBY2NlcHRMYW5ndWFnZU9wdE91dCI9ZHdvcmQ6MDAwMDAwMDEKCjsgZGlzYWJsZSBsZXQgd2luZG93cyBpbXByb3ZlIHN0YXJ0IGFuZCBzZWFyY2ggcmVzdWx0cyBieSB0cmFja2luZyBhcHAgbGF1bmNoZXMgIApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcRWRnZVVJXQoiRGlzYWJsZU1GVVRyYWNraW5nIj1kd29yZDowMDAwMDAwMQoKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xFZGdlVUldCiJEaXNhYmxlTUZVVHJhY2tpbmciPWR3b3JkOjAwMDAwMDAxCgo7IGRpc2FibGUgcGVyc29uYWwgaW5raW5nIGFuZCB0eXBpbmcgZGljdGlvbmFyeQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XElucHV0UGVyc29uYWxpemF0aW9uXQoiUmVzdHJpY3RJbXBsaWNpdElua0NvbGxlY3Rpb24iPWR3b3JkOjAwMDAwMDAxCiJSZXN0cmljdEltcGxpY2l0VGV4dENvbGxlY3Rpb24iPWR3b3JkOjAwMDAwMDAxCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XElucHV0UGVyc29uYWxpemF0aW9uXFRyYWluZWREYXRhU3RvcmVdCiJIYXJ2ZXN0Q29udGFjdHMiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFBlcnNvbmFsaXphdGlvblxTZXR0aW5nc10KIkFjY2VwdGVkUHJpdmFjeVBvbGljeSI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBzZW5kaW5nIHJlcXVpcmVkIGRhdGEKW0hLRVlfTE9DQUxfTUFDSElORVxTb2Z0d2FyZVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xEYXRhQ29sbGVjdGlvbl0KIkFsbG93VGVsZW1ldHJ5Ij1kd29yZDowMDAwMDAwMAoKOyBmZWVkYmFjayBmcmVxdWVuY3kgbmV2ZXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxTaXVmXFJ1bGVzXQoiTnVtYmVyT2ZTSVVGSW5QZXJpb2QiPWR3b3JkOjAwMDAwMDAwCiJQZXJpb2RJbk5hbm9TZWNvbmRzIj0tCgo7IGRpc2FibGUgc3RvcmUgbXkgYWN0aXZpdHkgaGlzdG9yeSBvbiB0aGlzIGRldmljZSAKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xTeXN0ZW1dCiJQdWJsaXNoVXNlckFjdGl2aXRpZXMiPWR3b3JkOjAwMDAwMDAwCgoKCgo7IFNFQVJDSAo7IGRpc2FibGUgc2VhcmNoIGhpZ2hsaWdodHMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFNlYXJjaFNldHRpbmdzXQoiSXNEeW5hbWljU2VhcmNoQm94RW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBzYWZlIHNlYXJjaApbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cU2VhcmNoU2V0dGluZ3NdCiJTYWZlU2VhcmNoTW9kZSI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBjbG91ZCBjb250ZW50IHNlYXJjaCBmb3Igd29yayBvciBzY2hvb2wgYWNjb3VudApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cU2VhcmNoU2V0dGluZ3NdCiJJc0FBRENsb3VkU2VhcmNoRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBjbG91ZCBjb250ZW50IHNlYXJjaCBmb3IgbWljcm9zb2Z0IGFjY291bnQKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFNlYXJjaFNldHRpbmdzXQoiSXNNU0FDbG91ZFNlYXJjaEVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgoKCgo7IEVBU0UgT0YgQUNDRVNTCjsgZGlzYWJsZSBtYWduaWZpZXIgc2V0dGluZ3MgCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcU2NyZWVuTWFnbmlmaWVyXQoiRm9sbG93Q2FyZXQiPWR3b3JkOjAwMDAwMDAwCiJGb2xsb3dOYXJyYXRvciI9ZHdvcmQ6MDAwMDAwMDAKIkZvbGxvd01vdXNlIj1kd29yZDowMDAwMDAwMAoiRm9sbG93Rm9jdXMiPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgbmFycmF0b3Igc2V0dGluZ3MKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxOYXJyYXRvcl0KIkludG9uYXRpb25QYXVzZSI9ZHdvcmQ6MDAwMDAwMDAKIlJlYWRIaW50cyI9ZHdvcmQ6MDAwMDAwMDAKIkVycm9yTm90aWZpY2F0aW9uVHlwZSI9ZHdvcmQ6MDAwMDAwMDAKIkVjaG9DaGFycyI9ZHdvcmQ6MDAwMDAwMDAKIkVjaG9Xb3JkcyI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcTmFycmF0b3JcTmFycmF0b3JIb21lXQoiTWluaW1pemVUeXBlIj1kd29yZDowMDAwMDAwMAoiQXV0b1N0YXJ0Ij1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxOYXJyYXRvclxOb1JvYW1dCiJFY2hvVG9nZ2xlS2V5cyI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSB1c2UgdGhlIHByaW50IHNjcmVlbiBrZXkgdG8gb3BlbiBzY3JlZW4gY2FwdHVyZQpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxLZXlib2FyZF0KIlByaW50U2NyZWVuS2V5Rm9yU25pcHBpbmdFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKCgoKOyBHQU1JTkcKOyBkaXNhYmxlIGdhbWUgYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTeXN0ZW1cR2FtZUNvbmZpZ1N0b3JlXQoiR2FtZURWUl9FbmFibGVkIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEdhbWVEVlJdCiJBcHBDYXB0dXJlRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBlbmFibGUgb3BlbiB4Ym94IGdhbWUgYmFyIHVzaW5nIGdhbWUgY29udHJvbGxlcgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XEdhbWVCYXJdCiJVc2VOZXh1c0ZvckdhbWVCYXJFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHVzZSB2aWV3ICsgbWVudSBhcyBndWlkZSBidXR0b24gaW4gYXBwcwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XEdhbWVCYXJdCiJHYW1lcGFkTmV4dXNDaG9yZEVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgo7IGVuYWJsZSBnYW1lIG1vZGUKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxHYW1lQmFyXQoiQXV0b0dhbWVNb2RlRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDEKCjsgb3RoZXIgc2V0dGluZ3MKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEdhbWVEVlJdCiJBdWRpb0VuY29kaW5nQml0cmF0ZSI9ZHdvcmQ6MDAwMWY0MDAKIkF1ZGlvQ2FwdHVyZUVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJDdXN0b21WaWRlb0VuY29kaW5nQml0cmF0ZSI9ZHdvcmQ6MDAzZDA5MDAKIkN1c3RvbVZpZGVvRW5jb2RpbmdIZWlnaHQiPWR3b3JkOjAwMDAwMmQwCiJDdXN0b21WaWRlb0VuY29kaW5nV2lkdGgiPWR3b3JkOjAwMDAwNTAwCiJIaXN0b3JpY2FsQnVmZmVyTGVuZ3RoIj1kd29yZDowMDAwMDAxZQoiSGlzdG9yaWNhbEJ1ZmZlckxlbmd0aFVuaXQiPWR3b3JkOjAwMDAwMDAxCiJIaXN0b3JpY2FsQ2FwdHVyZUVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJIaXN0b3JpY2FsQ2FwdHVyZU9uQmF0dGVyeUFsbG93ZWQiPWR3b3JkOjAwMDAwMDAxCiJIaXN0b3JpY2FsQ2FwdHVyZU9uV2lyZWxlc3NEaXNwbGF5QWxsb3dlZCI9ZHdvcmQ6MDAwMDAwMDEKIk1heGltdW1SZWNvcmRMZW5ndGgiPWhleChiKTowMCxEMCw4OCxDMywxMCwwMCwwMCwwMAoiVmlkZW9FbmNvZGluZ0JpdHJhdGVNb2RlIj1kd29yZDowMDAwMDAwMgoiVmlkZW9FbmNvZGluZ1Jlc29sdXRpb25Nb2RlIj1kd29yZDowMDAwMDAwMgoiVmlkZW9FbmNvZGluZ0ZyYW1lUmF0ZU1vZGUiPWR3b3JkOjAwMDAwMDAwCiJFY2hvQ2FuY2VsbGF0aW9uRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDEKIkN1cnNvckNhcHR1cmVFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiVktUb2dnbGVHYW1lQmFyIj1kd29yZDowMDAwMDAwMAoiVktNVG9nZ2xlR2FtZUJhciI9ZHdvcmQ6MDAwMDAwMDAKIlZLU2F2ZUhpc3RvcmljYWxWaWRlbyI9ZHdvcmQ6MDAwMDAwMDAKIlZLTVNhdmVIaXN0b3JpY2FsVmlkZW8iPWR3b3JkOjAwMDAwMDAwCiJWS1RvZ2dsZVJlY29yZGluZyI9ZHdvcmQ6MDAwMDAwMDAKIlZLTVRvZ2dsZVJlY29yZGluZyI9ZHdvcmQ6MDAwMDAwMDAKIlZLVGFrZVNjcmVlbnNob3QiPWR3b3JkOjAwMDAwMDAwCiJWS01UYWtlU2NyZWVuc2hvdCI9ZHdvcmQ6MDAwMDAwMDAKIlZLVG9nZ2xlUmVjb3JkaW5nSW5kaWNhdG9yIj1kd29yZDowMDAwMDAwMAoiVktNVG9nZ2xlUmVjb3JkaW5nSW5kaWNhdG9yIj1kd29yZDowMDAwMDAwMAoiVktUb2dnbGVNaWNyb3Bob25lQ2FwdHVyZSI9ZHdvcmQ6MDAwMDAwMDAKIlZLTVRvZ2dsZU1pY3JvcGhvbmVDYXB0dXJlIj1kd29yZDowMDAwMDAwMAoiVktUb2dnbGVDYW1lcmFDYXB0dXJlIj1kd29yZDowMDAwMDAwMAoiVktNVG9nZ2xlQ2FtZXJhQ2FwdHVyZSI9ZHdvcmQ6MDAwMDAwMDAKIlZLVG9nZ2xlQnJvYWRjYXN0Ij1kd29yZDowMDAwMDAwMAoiVktNVG9nZ2xlQnJvYWRjYXN0Ij1kd29yZDowMDAwMDAwMAoiTWljcm9waG9uZUNhcHR1cmVFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiU3lzdGVtQXVkaW9HYWluIj1oZXgoYik6MTAsMjcsMDAsMDAsMDAsMDAsMDAsMDAKIk1pY3JvcGhvbmVHYWluIj1oZXgoYik6MTAsMjcsMDAsMDAsMDAsMDAsMDAsMDAKCgoKCjsgVElNRSAmIExBTkdVQUdFIAo7IGRpc2FibGUgc2hvdyB0aGUgdm9pY2UgdHlwaW5nIG1pYyBidXR0b24KW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxpbnB1dFxTZXR0aW5nc10KIklzVm9pY2VUeXBpbmdLZXlFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIGNhcGl0YWxpemUgdGhlIGZpcnN0IGxldHRlciBvZiBlYWNoIHNlbnRlbmNlCjsgZGlzYWJsZSBwbGF5IGtleSBzb3VuZHMgYXMgaSB0eXBlCjsgZGlzYWJsZSBhZGQgYSBwZXJpb2QgYWZ0ZXIgaSBkb3VibGUtdGFwIHRoZSBzcGFjZWJhcgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFRhYmxldFRpcFwxLjddCiJFbmFibGVBdXRvU2hpZnRFbmdhZ2UiPWR3b3JkOjAwMDAwMDAwCiJFbmFibGVLZXlBdWRpb0ZlZWRiYWNrIj1kd29yZDowMDAwMDAwMAoiRW5hYmxlRG91YmxlVGFwU3BhY2UiPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgdHlwaW5nIGluc2lnaHRzCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcaW5wdXRcU2V0dGluZ3NdCiJJbnNpZ2h0c0VuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgo7IHNob3cgdGhlIHRvdWNoIGtleWJvYXJkIG5ldmVyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcVGFibGV0VGlwXDEuN10KIlRvdWNoS2V5Ym9hcmRUYXBJbnZva2UiPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgbGFuZ3VhZ2UgYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcQ1RGXExhbmdCYXJdCiJFeHRyYUljb25zT25NaW5pbWl6ZWQiPWR3b3JkOjAwMDAwMDAwCiJMYWJlbCI9ZHdvcmQ6MDAwMDAwMDAKIlNob3dTdGF0dXMiPWR3b3JkOjAwMDAwMDAzCiJUcmFuc3BhcmVuY3kiPWR3b3JkOjAwMDAwMGZmCgo7IGRpc2FibGUgbGFuZ3VhZ2UgaG90a2V5CltIS0VZX0NVUlJFTlRfVVNFUlxLZXlib2FyZCBMYXlvdXRcVG9nZ2xlXQoiTGFuZ3VhZ2UgSG90a2V5Ij0iMyIKIkhvdGtleSI9IjMiCiJMYXlvdXQgSG90a2V5Ij0iMyIKCjsgZGlzYWJsZSBjYWxlbmRhciBldmVudHMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFNlYXJjaF0KIkdsZWFtRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKIldlYXRoZXJFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiSG9saWRheUVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgoKCgo7IEFDQ09VTlRTCjsgZGlzYWJsZSBkeW5hbWljIGxvY2sKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzIE5UXEN1cnJlbnRWZXJzaW9uXFdpbmxvZ29uXQoiRW5hYmxlR29vZGJ5ZSI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSB1c2UgbXkgc2lnbiBpbiBpbmZvIGFmdGVyIHJlc3RhcnQKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxQb2xpY2llc1xTeXN0ZW1dCiJEaXNhYmxlQXV0b21hdGljUmVzdGFydFNpZ25PbiI9ZHdvcmQ6MDAwMDAwMDEKCjsgZGlzYWJsZSBmb3IgaW1wcm92ZWQgc2VjdXJpdHksIG9ubHkgYWxsb3cgd2luZG93cyBoZWxsbyBzaWduLWluCltIS0VZX0xPQ0FMX01BQ0hJTkVcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3MgTlRcQ3VycmVudFZlcnNpb25cUGFzc3dvcmRMZXNzXERldmljZV0KIkRldmljZVBhc3N3b3JkTGVzc0J1aWxkVmVyc2lvbiI9ZHdvcmQ6MDAwMDAwMDAKIkRldmljZVBhc3N3b3JkTGVzc1VwZGF0ZVR5cGUiPWR3b3JkOjAwMDAwMDAxCgo7IGRpc2FibGUgd2luZG93cyBiYWNrdXAKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xTZXR0aW5nU3luY10KIkRpc2FibGVBY2Nlc3NpYmlsaXR5U2V0dGluZ1N5bmMiPWR3b3JkOjAwMDAwMDAyCiJEaXNhYmxlQWNjZXNzaWJpbGl0eVNldHRpbmdTeW5jVXNlck92ZXJyaWRlIj1kd29yZDowMDAwMDAwMQoiRGlzYWJsZUFwcFN5bmNTZXR0aW5nU3luYyI9ZHdvcmQ6MDAwMDAwMDIKIkRpc2FibGVBcHBTeW5jU2V0dGluZ1N5bmNVc2VyT3ZlcnJpZGUiPWR3b3JkOjAwMDAwMDAxCiJEaXNhYmxlQXBwbGljYXRpb25TZXR0aW5nU3luYyI9ZHdvcmQ6MDAwMDAwMDIKIkRpc2FibGVBcHBsaWNhdGlvblNldHRpbmdTeW5jVXNlck92ZXJyaWRlIj1kd29yZDowMDAwMDAwMQoiRGlzYWJsZUNyZWRlbnRpYWxzU2V0dGluZ1N5bmMiPWR3b3JkOjAwMDAwMDAyCiJEaXNhYmxlQ3JlZGVudGlhbHNTZXR0aW5nU3luY1VzZXJPdmVycmlkZSI9ZHdvcmQ6MDAwMDAwMDEKIkRpc2FibGVEZXNrdG9wVGhlbWVTZXR0aW5nU3luYyI9ZHdvcmQ6MDAwMDAwMDIKIkRpc2FibGVEZXNrdG9wVGhlbWVTZXR0aW5nU3luY1VzZXJPdmVycmlkZSI9ZHdvcmQ6MDAwMDAwMDEKIkRpc2FibGVMYW5ndWFnZVNldHRpbmdTeW5jIj1kd29yZDowMDAwMDAwMgoiRGlzYWJsZUxhbmd1YWdlU2V0dGluZ1N5bmNVc2VyT3ZlcnJpZGUiPWR3b3JkOjAwMDAwMDAxCiJEaXNhYmxlUGVyc29uYWxpemF0aW9uU2V0dGluZ1N5bmMiPWR3b3JkOjAwMDAwMDAyCiJEaXNhYmxlUGVyc29uYWxpemF0aW9uU2V0dGluZ1N5bmNVc2VyT3ZlcnJpZGUiPWR3b3JkOjAwMDAwMDAxCiJEaXNhYmxlU2V0dGluZ1N5bmMiPWR3b3JkOjAwMDAwMDAyCiJEaXNhYmxlU2V0dGluZ1N5bmNVc2VyT3ZlcnJpZGUiPWR3b3JkOjAwMDAwMDAxCiJEaXNhYmxlU3RhcnRMYXlvdXRTZXR0aW5nU3luYyI9ZHdvcmQ6MDAwMDAwMDIKIkRpc2FibGVTdGFydExheW91dFNldHRpbmdTeW5jVXNlck92ZXJyaWRlIj1kd29yZDowMDAwMDAwMQoiRGlzYWJsZVN5bmNPblBhaWROZXR3b3JrIj1kd29yZDowMDAwMDAwMQoiRGlzYWJsZVdlYkJyb3dzZXJTZXR0aW5nU3luYyI9ZHdvcmQ6MDAwMDAwMDIKIkRpc2FibGVXZWJCcm93c2VyU2V0dGluZ1N5bmNVc2VyT3ZlcnJpZGUiPWR3b3JkOjAwMDAwMDAxCiJEaXNhYmxlV2luZG93c1NldHRpbmdTeW5jIj1kd29yZDowMDAwMDAwMgoiRGlzYWJsZVdpbmRvd3NTZXR0aW5nU3luY1VzZXJPdmVycmlkZSI9ZHdvcmQ6MDAwMDAwMDEKIkVuYWJsZVdpbmRvd3NCYWNrdXAiPWR3b3JkOjAwMDAwMDAwCgoKCgo7IEFQUFMKOyBkaXNhYmxlIGF1dG9tYXRpY2FsbHkgdXBkYXRlIG1hcHMKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cTWFwc10KIkF1dG9VcGRhdGVFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIGFyY2hpdmUgYXBwcwpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXEFwcHhdCiJBbGxvd0F1dG9tYXRpY0FwcEFyY2hpdmluZyI9ZHdvcmQ6MDAwMDAwMDAKCgoKCjsgUEVSU09OQUxJWkFUSU9OCjsgc29saWQgY29sb3IgcGVyc29uYWxpemUgeW91ciBiYWNrZ3JvdW5kCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXERlc2t0b3BdCiJXYWxscGFwZXIiPSIiCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcV2FsbHBhcGVyc10KIkJhY2tncm91bmRUeXBlIj1kd29yZDowMDAwMDAwMQoKOyBkYXJrIHRoZW1lICYgZGlzYWJsZSB0cmFuc3BhcmVuY3kKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFRoZW1lc1xQZXJzb25hbGl6ZV0KIkFwcHNVc2VMaWdodFRoZW1lIj1kd29yZDowMDAwMDAwMAoiQ29sb3JQcmV2YWxlbmNlIj1kd29yZDowMDAwMDAwMQoiRW5hYmxlVHJhbnNwYXJlbmN5Ij1kd29yZDowMDAwMDAwMAoiU3lzdGVtVXNlc0xpZ2h0VGhlbWUiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFRoZW1lc1xQZXJzb25hbGl6ZV0KIkFwcHNVc2VMaWdodFRoZW1lIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFjY2VudF0KIkFjY2VudFBhbGV0dGUiPWhleDo2NCw2NCw2NCwwMCw2Yiw2Yiw2YiwwMCwwMCwwMCwwMCwwMCwwMCwwMCwwMCwwMCwwMCwwMCwwMCxcCiAgMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAsMDAKIlN0YXJ0Q29sb3JNZW51Ij1kd29yZDowMDAwMDAwMAoiQWNjZW50Q29sb3JNZW51Ij1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXERXTV0KIkVuYWJsZVdpbmRvd0NvbG9yaXphdGlvbiI9ZHdvcmQ6MDAwMDAwMDEKIkFjY2VudENvbG9yIj1kd29yZDpmZjE5MTkxOQoiQ29sb3JpemF0aW9uQ29sb3IiPWR3b3JkOmM0MTkxOTE5CiJDb2xvcml6YXRpb25BZnRlcmdsb3ciPWR3b3JkOmM0MTkxOTE5CgpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxDb2xvcnNdCiJCYWNrZ3JvdW5kIj0iMCAwIDAiCgpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxEZXNrdG9wXQoiV2FsbFBhcGVyIj0iIgoKOyBoaWRlIHJlY3ljbGUgYmluIGZyb20gZGVza3RvcApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcSGlkZURlc2t0b3BJY29uc1xDbGFzc2ljU3RhcnRNZW51XQoiezY0NUZGMDQwLTUwODEtMTAxQi05RjA4LTAwQUEwMDJGOTU0RX0iPWR3b3JkOjAwMDAwMDAxCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcSGlkZURlc2t0b3BJY29uc1xOZXdTdGFydFBhbmVsXQoiezY0NUZGMDQwLTUwODEtMTAxQi05RjA4LTAwQUEwMDJGOTU0RX0iPWR3b3JkOjAwMDAwMDAxCgo7IGFsd2F5cyBoaWRlIG1vc3QgdXNlZCBsaXN0IGluIHN0YXJ0IG1lbnUKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xFeHBsb3Jlcl0KIlNob3dPckhpZGVNb3N0VXNlZEFwcHMiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcRXhwbG9yZXJdCiJTaG93T3JIaWRlTW9zdFVzZWRBcHBzIj0tCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcRXhwbG9yZXJdCiJOb1N0YXJ0TWVudU1GVXByb2dyYW1zTGlzdCI9LQoiTm9JbnN0cnVtZW50YXRpb24iPS0KCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcRXhwbG9yZXJdCiJOb1N0YXJ0TWVudU1GVXByb2dyYW1zTGlzdCI9LQoiTm9JbnN0cnVtZW50YXRpb24iPS0KCjsgc3RhcnQgbWVudSBoaWRlIHJlY29tbWVuZGVkCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFBvbGljeU1hbmFnZXJcY3VycmVudFxkZXZpY2VcU3RhcnRdCiJIaWRlUmVjb21tZW5kZWRTZWN0aW9uIj1kd29yZDowMDAwMDAwMQoKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcUG9saWN5TWFuYWdlclxjdXJyZW50XGRldmljZVxFZHVjYXRpb25dCiJJc0VkdWNhdGlvbkVudmlyb25tZW50Ij1kd29yZDowMDAwMDAwMQoKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xFeHBsb3Jlcl0KIkhpZGVSZWNvbW1lbmRlZFNlY3Rpb24iPWR3b3JkOjAwMDAwMDAxCgo7IG1vcmUgcGlucyBwZXJzb25hbGl6YXRpb24gc3RhcnQKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiU3RhcnRfTGF5b3V0Ij1kd29yZDowMDAwMDAwMQoKOyBkaXNhYmxlIHNob3cgcmVjZW50bHkgYWRkZWQgYXBwcwpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXEV4cGxvcmVyXQoiSGlkZVJlY2VudGx5QWRkZWRBcHBzIj1kd29yZDowMDAwMDAwMQoKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxQb2xpY2llc1xFeHBsb3Jlcl0KIkhpZGVSZWNlbnRseUFkZGVkQXBwcyI9ZHdvcmQ6MDAwMDAwMDEKCjsgZGlzYWJsZSBzaG93IGFjY291bnQtcmVsYXRlZCBub3RpZmljYXRpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlN0YXJ0X0FjY291bnROb3RpZmljYXRpb25zIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHNob3cgd2Vic2l0ZXMgZnJvbSB5b3VyIGJyb3dzaW5nIGhpc3RvcnkKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiU3RhcnRfUmVjb1BlcnNvbmFsaXplZFNpdGVzIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHNob3cgcmVjZW50bHkgb3BlbmVkIGl0ZW1zIGluIHN0YXJ0LCBqdW1wIGxpc3RzIGFuZCBmaWxlIGV4cGxvcmVyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlN0YXJ0X1RyYWNrRG9jcyI9ZHdvcmQ6MDAwMDAwMDAgCgo7IHRvdWNoIGtleWJvYXJkIG5ldmVyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcVGFibGV0VGlwXDEuN10KIlRpcGJhbmREZXNpcmVkVmlzaWJpbGl0eSI9ZHdvcmQ6MDAwMDAwMDAKCjsgc2hvdyBzbWFsbGVyIHRhc2tiYXIgaWNvbnMgbmV2ZXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiSWNvblNpemVQcmVmZXJlbmNlIj1kd29yZDowMDAwMDAwMQoKOyBsZWZ0IHRhc2tiYXIgYWxpZ25tZW50CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlRhc2tiYXJBbCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBkZXNrdG9wIHByZXZpZXcKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiVGFza2JhclNkIj1kd29yZDowMDAwMDAwMAoKOyByZW1vdmUgY2hhdCBmcm9tIHRhc2tiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiVGFza2Jhck1uIj1kd29yZDowMDAwMDAwMAoKOyByZW1vdmUgdGFzayB2aWV3IGZyb20gdGFza2JhcgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJTaG93VGFza1ZpZXdCdXR0b24iPWR3b3JkOjAwMDAwMDAwCgo7IHJlbW92ZSBzZWFyY2ggZnJvbSB0YXNrYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTZWFyY2hdCiJTZWFyY2hib3hUYXNrYmFyTW9kZSI9ZHdvcmQ6MDAwMDAwMDAKCjsgcmVtb3ZlIHdpbmRvd3Mgd2lkZ2V0cyBmcm9tIHRhc2tiYXIKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcRHNoXSAKIkFsbG93TmV3c0FuZEludGVyZXN0cyI9ZHdvcmQ6MDAwMDAwMDAKCjsgcmVtb3ZlIGNvcGlsb3QgZnJvbSB0YXNrYmFyCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNob3dDb3BpbG90QnV0dG9uIj1kd29yZDowMDAwMDAwMAoKOyByZW1vdmUgcmVzdW1lIGZyb20gdGFza2JhcgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJJc0VuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgo7IHJlbW92ZSBtZWV0IG5vdwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcRXhwbG9yZXJdCiJIaWRlU0NBTWVldE5vdyI9ZHdvcmQ6MDAwMDAwMDEKCjsgcmVtb3ZlIG5ld3MgYW5kIGludGVyZXN0cwpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXFdpbmRvd3MgRmVlZHNdCiJFbmFibGVGZWVkcyI9ZHdvcmQ6MDAwMDAwMDAKCjsgc2hvdyBhbGwgdGFza2JhciBpY29ucwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJdCiJFbmFibGVBdXRvVHJheSI9ZHdvcmQ6MDAwMDAwMDAKCjsgcmVtb3ZlIHNlY3VyaXR5IHRhc2tiYXIgaWNvbgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXFN0YXJ0dXBBcHByb3ZlZFxSdW5dCiJTZWN1cml0eUhlYWx0aCI9aGV4KDMpOjA3LDAwLDAwLDAwLDA1LERCLDhBLDY5LDhBLDQ5LEQ5LDAxCgo7IGRpc2FibGUgdXNlIGR5bmFtaWMgbGlnaHRpbmcgb24gbXkgZGV2aWNlcwpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XExpZ2h0aW5nXQoiQW1iaWVudExpZ2h0aW5nRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBjb21wYXRpYmxlIGFwcHMgaW4gdGhlIGZvcmVncm91bmQgYWx3YXlzIGNvbnRyb2wgbGlnaHRpbmcgCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcTGlnaHRpbmddCiJDb250cm9sbGVkQnlGb3JlZ3JvdW5kQXBwIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIG1hdGNoIG15IHdpbmRvd3MgYWNjZW50IGNvbG9yIApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XExpZ2h0aW5nXQoiVXNlU3lzdGVtQWNjZW50Q29sb3IiPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgc2hvdyBrZXkgYmFja2dyb3VuZApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFRhYmxldFRpcFwxLjddCiJJc0tleUJhY2tncm91bmRFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHNob3cgcmVjb21tZW5kYXRpb25zIGZvciB0aXBzIHNob3J0Y3V0cyBuZXcgYXBwcyBhbmQgbW9yZQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcQWR2YW5jZWRdCiJTdGFydF9JcmlzUmVjb21tZW5kYXRpb25zIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFN0YXJ0XQoiU2hvd1JlY2VudExpc3QiPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgc2hhcmUgYW55IHdpbmRvdyBmcm9tIG15IHRhc2tiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiVGFza2JhclNuIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIGRldmljZSB1c2FnZQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRFeHBlcmllbmNlSG9zdFxJbnRlbnRcZGV2ZWxvcGVyXQoiSW50ZW50Ij1kd29yZDowMDAwMDAwMAoiUHJpb3JpdHkiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRFeHBlcmllbmNlSG9zdFxJbnRlbnRcZ2FtaW5nXQoiSW50ZW50Ij1kd29yZDowMDAwMDAwMAoiUHJpb3JpdHkiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRFeHBlcmllbmNlSG9zdFxJbnRlbnRcZmFtaWx5XQoiSW50ZW50Ij1kd29yZDowMDAwMDAwMAoiUHJpb3JpdHkiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRFeHBlcmllbmNlSG9zdFxJbnRlbnRcY3JlYXRpdmVdCiJJbnRlbnQiPWR3b3JkOjAwMDAwMDAwCiJQcmlvcml0eSI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZEV4cGVyaWVuY2VIb3N0XEludGVudFxzY2hvb2x3b3JrXQoiSW50ZW50Ij1kd29yZDowMDAwMDAwMAoiUHJpb3JpdHkiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRFeHBlcmllbmNlSG9zdFxJbnRlbnRcZW50ZXJ0YWlubWVudF0KIkludGVudCI9ZHdvcmQ6MDAwMDAwMDAKIlByaW9yaXR5Ij1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkRXhwZXJpZW5jZUhvc3RcSW50ZW50XGJ1c2luZXNzXQoiSW50ZW50Ij1kd29yZDowMDAwMDAwMAoiUHJpb3JpdHkiPWR3b3JkOjAwMDAwMDAwCgoKCgo7IERFVklDRVMKOyBkaXNhYmxlIHVzYiBpc3N1ZXMgbm90aWZ5CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcU2hlbGxcVVNCXQoiTm90aWZ5T25Vc2JFcnJvcnMiPWR3b3JkOjAwMDAwMDAwCgo7IGRpc2FibGUgbGV0IHdpbmRvd3MgbWFuYWdlIG15IGRlZmF1bHQgcHJpbnRlcgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3MgTlRcQ3VycmVudFZlcnNpb25cV2luZG93c10KIkxlZ2FjeURlZmF1bHRQcmludGVyTW9kZSI9ZHdvcmQ6MDAwMDAwMDEKCjsgZGlzYWJsZSB3cml0ZSB3aXRoIHlvdXIgZmluZ2VydGlwCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcVGFibGV0VGlwXEVtYmVkZGVkSW5rQ29udHJvbF0KIkVuYWJsZUlua2luZ1dpdGhUb3VjaCI9ZHdvcmQ6MDAwMDAwMDAKCgoKCjsgU1lTVEVNCjsgMTAwJSBkcGkgc2NhbGluZwpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxEZXNrdG9wXQoiTG9nUGl4ZWxzIj1kd29yZDowMDAwMDA2MAoiV2luOERwaVNjYWxpbmciPWR3b3JkOjAwMDAwMDAxCgpbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcRFdNXQoiVXNlRHBpU2NhbGluZyI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBmaXggc2NhbGluZyBmb3IgYXBwcwpbSEtFWV9DVVJSRU5UX1VTRVJcQ29udHJvbCBQYW5lbFxEZXNrdG9wXQoiRW5hYmxlUGVyUHJvY2Vzc1N5c3RlbURQSSI9ZHdvcmQ6MDAwMDAwMDAKCjsgdHVybiBvbiBoYXJkd2FyZSBhY2NlbGVyYXRlZCBncHUgc2NoZWR1bGluZwpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxDb250cm9sXEdyYXBoaWNzRHJpdmVyc10KIkh3U2NoTW9kZSI9ZHdvcmQ6MDAwMDAwMDIKCjsgZGlzYWJsZSB2YXJpYWJsZSByZWZyZXNoIHJhdGUgJiBlbmFibGUgb3B0aW1pemF0aW9ucyBmb3Igd2luZG93ZWQgZ2FtZXMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxEaXJlY3RYXFVzZXJHcHVQcmVmZXJlbmNlc10KIkRpcmVjdFhVc2VyR2xvYmFsU2V0dGluZ3MiPSJTd2FwRWZmZWN0VXBncmFkZUVuYWJsZT0xO1ZSUk9wdGltaXplRW5hYmxlPTA7IgoKOyBkaXNhYmxlIG5vdGlmaWNhdGlvbnMKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFB1c2hOb3RpZmljYXRpb25zXQoiVG9hc3RFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIG5vdGlmaWNhdGlvbnMgc3VnZ2VzdGVkCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxOb3RpZmljYXRpb25zXFNldHRpbmdzXFdpbmRvd3MuU3lzdGVtVG9hc3QuU3VnZ2VzdGVkXQoiRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBub3RpZmljYXRpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxOb3RpZmljYXRpb25zXFNldHRpbmdzXQoiTk9DX0dMT0JBTF9TRVRUSU5HX0FMTE9XX05PVElGSUNBVElPTl9TT1VORCI9ZHdvcmQ6MDAwMDAwMDAKIk5PQ19HTE9CQUxfU0VUVElOR19BTExPV19DUklUSUNBTF9UT0FTVFNfQUJPVkVfTE9DSyI9ZHdvcmQ6MDAwMDAwMDAKIk5PQ19HTE9CQUxfU0VUVElOR19BTExPV19UT0FTVFNfQUJPVkVfTE9DSyI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxOb3RpZmljYXRpb25zXFNldHRpbmdzXE1pY3Jvc29mdC5Ta3lEcml2ZS5EZXNrdG9wXQoiRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxOb3RpZmljYXRpb25zXFNldHRpbmdzXFdpbmRvd3MuU3lzdGVtVG9hc3QuQXV0b1BsYXldCiJFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXE5vdGlmaWNhdGlvbnNcU2V0dGluZ3NcV2luZG93cy5TeXN0ZW1Ub2FzdC5TZWN1cml0eUFuZE1haW50ZW5hbmNlXQoiRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxOb3RpZmljYXRpb25zXFNldHRpbmdzXHdpbmRvd3MuaW1tZXJzaXZlY29udHJvbHBhbmVsX2N3NW4xaDJ0eHlld3khbWljcm9zb2Z0LndpbmRvd3MuaW1tZXJzaXZlY29udHJvbHBhbmVsXQoiRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxOb3RpZmljYXRpb25zXFNldHRpbmdzXFdpbmRvd3MuU3lzdGVtVG9hc3QuQ2FwYWJpbGl0eUFjY2Vzc10KIkVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cTm90aWZpY2F0aW9uc1xTZXR0aW5nc1xXaW5kb3dzLlN5c3RlbVRvYXN0LlN0YXJ0dXBBcHBdCiJFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFVzZXJQcm9maWxlRW5nYWdlbWVudF0KIlNjb29iZVN5c3RlbVNldHRpbmdFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHN1Z2dlc3RlZCBhY3Rpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTbWFydEFjdGlvblBsYXRmb3JtXFNtYXJ0Q2xpcGJvYXJkXQoiRGlzYWJsZWQiPWR3b3JkOjAwMDAwMDAxCgo7IGRpc2FibGUgZm9jdXMgYXNzaXN0CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXENhY2hlXERlZmF1bHRBY2NvdW50XCQkd2luZG93cy5kYXRhLm5vdGlmaWNhdGlvbnMucXVpZXRob3Vyc3NldHRpbmdzXEN1cnJlbnRdCiJEYXRhIj1oZXgoMyk6MDIsMDAsMDAsMDAsQjQsNjcsMkIsNjgsRjAsMEIsRDgsMDEsMDAsMDAsMDAsMDAsNDMsNDIsMDEsMDAsXApDMiwwQSwwMSxEMiwxNCwyOCw0RCwwMCw2OSwwMCw2MywwMCw3MiwwMCw2RiwwMCw3MywwMCw2RiwwMCw2NiwwMCw3NCwwMCwyRSxcCjAwLDUxLDAwLDc1LDAwLDY5LDAwLDY1LDAwLDc0LDAwLDQ4LDAwLDZGLDAwLDc1LDAwLDcyLDAwLDczLDAwLDUwLDAwLDcyLDAwLFwKNkYsMDAsNjYsMDAsNjksMDAsNkMsMDAsNjUsMDAsMkUsMDAsNTUsMDAsNkUsMDAsNzIsMDAsNjUsMDAsNzMsMDAsNzQsMDAsNzIsXAowMCw2OSwwMCw2MywwMCw3NCwwMCw2NSwwMCw2NCwwMCxDQSwyOCxEMCwxNCwwMiwwMCwwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkU3RvcmVcU3RvcmVcQ2FjaGVcRGVmYXVsdEFjY291bnRcJHF1aWV0bW9tZW50ZnVsbHNjcmVlbiR3aW5kb3dzLmRhdGEubm90aWZpY2F0aW9ucy5xdWlldG1vbWVudFxDdXJyZW50XQoiRGF0YSI9aGV4KDMpOjAyLDAwLDAwLDAwLDk3LDFELDJELDY4LEYwLDBCLEQ4LDAxLDAwLDAwLDAwLDAwLDQzLDQyLDAxLDAwLFwKQzIsMEEsMDEsRDIsMUUsMjYsNEQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNkYsMDAsNzMsMDAsNkYsMDAsNjYsMDAsNzQsMDAsMkUsXAowMCw1MSwwMCw3NSwwMCw2OSwwMCw2NSwwMCw3NCwwMCw0OCwwMCw2RiwwMCw3NSwwMCw3MiwwMCw3MywwMCw1MCwwMCw3MiwwMCxcCjZGLDAwLDY2LDAwLDY5LDAwLDZDLDAwLDY1LDAwLDJFLDAwLDQxLDAwLDZDLDAwLDYxLDAwLDcyLDAwLDZELDAwLDczLDAwLDRGLFwKMDAsNkUsMDAsNkMsMDAsNzksMDAsQzIsMjgsMDEsQ0EsNTAsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXENhY2hlXERlZmF1bHRBY2NvdW50XCRxdWlldG1vbWVudGdhbWUkd2luZG93cy5kYXRhLm5vdGlmaWNhdGlvbnMucXVpZXRtb21lbnRcQ3VycmVudF0KIkRhdGEiPWhleCgzKTowMiwwMCwwMCwwMCw2QywzOSwyRCw2OCxGMCwwQixEOCwwMSwwMCwwMCwwMCwwMCw0Myw0MiwwMSwwMCxcCkMyLDBBLDAxLEQyLDFFLDI4LDRELDAwLDY5LDAwLDYzLDAwLDcyLDAwLDZGLDAwLDczLDAwLDZGLDAwLDY2LDAwLDc0LDAwLDJFLFwKMDAsNTEsMDAsNzUsMDAsNjksMDAsNjUsMDAsNzQsMDAsNDgsMDAsNkYsMDAsNzUsMDAsNzIsMDAsNzMsMDAsNTAsMDAsNzIsMDAsXAo2RiwwMCw2NiwwMCw2OSwwMCw2QywwMCw2NSwwMCwyRSwwMCw1MCwwMCw3MiwwMCw2OSwwMCw2RiwwMCw3MiwwMCw2OSwwMCw3NCxcCjAwLDc5LDAwLDRGLDAwLDZFLDAwLDZDLDAwLDc5LDAwLEMyLDI4LDAxLENBLDUwLDAwLDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRTdG9yZVxTdG9yZVxDYWNoZVxEZWZhdWx0QWNjb3VudFwkcXVpZXRtb21lbnRwb3N0b29iZSR3aW5kb3dzLmRhdGEubm90aWZpY2F0aW9ucy5xdWlldG1vbWVudFxDdXJyZW50XQoiRGF0YSI9aGV4KDMpOjAyLDAwLDAwLDAwLDA2LDU0LDJELDY4LEYwLDBCLEQ4LDAxLDAwLDAwLDAwLDAwLDQzLDQyLDAxLDAwLFwKQzIsMEEsMDEsRDIsMUUsMjgsNEQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNkYsMDAsNzMsMDAsNkYsMDAsNjYsMDAsNzQsMDAsMkUsXAowMCw1MSwwMCw3NSwwMCw2OSwwMCw2NSwwMCw3NCwwMCw0OCwwMCw2RiwwMCw3NSwwMCw3MiwwMCw3MywwMCw1MCwwMCw3MiwwMCxcCjZGLDAwLDY2LDAwLDY5LDAwLDZDLDAwLDY1LDAwLDJFLDAwLDUwLDAwLDcyLDAwLDY5LDAwLDZGLDAwLDcyLDAwLDY5LDAwLDc0LFwKMDAsNzksMDAsNEYsMDAsNkUsMDAsNkMsMDAsNzksMDAsQzIsMjgsMDEsQ0EsNTAsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXENhY2hlXERlZmF1bHRBY2NvdW50XCRxdWlldG1vbWVudHByZXNlbnRhdGlvbiR3aW5kb3dzLmRhdGEubm90aWZpY2F0aW9ucy5xdWlldG1vbWVudFxDdXJyZW50XQoiRGF0YSI9aGV4KDMpOjAyLDAwLDAwLDAwLDgzLDZFLDJELDY4LEYwLDBCLEQ4LDAxLDAwLDAwLDAwLDAwLDQzLDQyLDAxLDAwLFwKQzIsMEEsMDEsRDIsMUUsMjYsNEQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNkYsMDAsNzMsMDAsNkYsMDAsNjYsMDAsNzQsMDAsMkUsXAowMCw1MSwwMCw3NSwwMCw2OSwwMCw2NSwwMCw3NCwwMCw0OCwwMCw2RiwwMCw3NSwwMCw3MiwwMCw3MywwMCw1MCwwMCw3MiwwMCxcCjZGLDAwLDY2LDAwLDY5LDAwLDZDLDAwLDY1LDAwLDJFLDAwLDQxLDAwLDZDLDAwLDYxLDAwLDcyLDAwLDZELDAwLDczLDAwLDRGLFwKMDAsNkUsMDAsNkMsMDAsNzksMDAsQzIsMjgsMDEsQ0EsNTAsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXENhY2hlXERlZmF1bHRBY2NvdW50XCRxdWlldG1vbWVudHNjaGVkdWxlZCR3aW5kb3dzLmRhdGEubm90aWZpY2F0aW9ucy5xdWlldG1vbWVudFxDdXJyZW50XQoiRGF0YSI9aGV4KDMpOjAyLDAwLDAwLDAwLDJFLDhBLDJELDY4LEYwLDBCLEQ4LDAxLDAwLDAwLDAwLDAwLDQzLDQyLDAxLDAwLFwKQzIsMEEsMDEsRDIsMUUsMjgsNEQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNkYsMDAsNzMsMDAsNkYsMDAsNjYsMDAsNzQsMDAsMkUsXAowMCw1MSwwMCw3NSwwMCw2OSwwMCw2NSwwMCw3NCwwMCw0OCwwMCw2RiwwMCw3NSwwMCw3MiwwMCw3MywwMCw1MCwwMCw3MiwwMCxcCjZGLDAwLDY2LDAwLDY5LDAwLDZDLDAwLDY1LDAwLDJFLDAwLDUwLDAwLDcyLDAwLDY5LDAwLDZGLDAwLDcyLDAwLDY5LDAwLDc0LFwKMDAsNzksMDAsNEYsMDAsNkUsMDAsNkMsMDAsNzksMDAsQzIsMjgsMDEsRDEsMzIsODAsRTAsQUEsOEEsOTksMzAsRDEsM0MsODAsXApFMCxGNixDNSxENSwwRSxDQSw1MCwwMCwwMAoKOyBkaXNhYmxlIHR1cm4gb24gZG8gbm90IGRpc3R1cmIgYXV0b21hdGljYWxseQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRTdG9yZVxTdG9yZVxEZWZhdWx0QWNjb3VudFxDdXJyZW50XGRlZmF1bHQkd2luZG93cy5kYXRhLmRvbm90ZGlzdHVyYi5xdWlldG1vbWVudCRxdWlldG1vbWVudGxpc3Rcd2luZG93cy5kYXRhLmRvbm90ZGlzdHVyYi5xdWlldG1vbWVudCRxdWlldG1vbWVudHByZXNlbnRhdGlvbl0KIkRhdGEiPWhleCgzKTo0Myw0MiwwMSwwMCwwQSwwMiwwMSwwMCwyQSwwNixFMixGMyxBQSxDQywwNiwyQSwyQiwwRSw1QSw0MyxcCjQyLDAxLDAwLEMyLDBBLDAxLEQyLDFFLDI2LDRELDAwLDY5LDAwLDYzLDAwLDcyLDAwLDZGLDAwLDczLDAwLDZGLDAwLDY2LDAwLFwKNzQsMDAsMkUsMDAsNTEsMDAsNzUsMDAsNjksMDAsNjUsMDAsNzQsMDAsNDgsMDAsNkYsMDAsNzUsMDAsNzIsMDAsNzMsMDAsNTAsXAowMCw3MiwwMCw2RiwwMCw2NiwwMCw2OSwwMCw2QywwMCw2NSwwMCwyRSwwMCw0MSwwMCw2QywwMCw2MSwwMCw3MiwwMCw2RCwwMCxcCjczLDAwLDRGLDAwLDZFLDAwLDZDLDAwLDc5LDAwLENBLDUwLDAwLDAwLDAwLDAwLDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ2xvdWRTdG9yZVxTdG9yZVxEZWZhdWx0QWNjb3VudFxDdXJyZW50XGRlZmF1bHQkd2luZG93cy5kYXRhLmRvbm90ZGlzdHVyYi5xdWlldG1vbWVudCRxdWlldG1vbWVudGxpc3Rcd2luZG93cy5kYXRhLmRvbm90ZGlzdHVyYi5xdWlldG1vbWVudCRxdWlldG1vbWVudGdhbWVdCiJEYXRhIj1oZXgoMyk6NDMsNDIsMDEsMDAsMEEsMDIsMDEsMDAsMkEsMDYsRTEsRjMsQUEsQ0MsMDYsMkEsMkIsMEUsNUUsNDMsXAo0MiwwMSwwMCxDMiwwQSwwMSxEMiwxRSwyOCw0RCwwMCw2OSwwMCw2MywwMCw3MiwwMCw2RiwwMCw3MywwMCw2RiwwMCw2NiwwMCxcCjc0LDAwLDJFLDAwLDUxLDAwLDc1LDAwLDY5LDAwLDY1LDAwLDc0LDAwLDQ4LDAwLDZGLDAwLDc1LDAwLDcyLDAwLDczLDAwLDUwLFwKMDAsNzIsMDAsNkYsMDAsNjYsMDAsNjksMDAsNkMsMDAsNjUsMDAsMkUsMDAsNTAsMDAsNzIsMDAsNjksMDAsNkYsMDAsNzIsMDAsXAo2OSwwMCw3NCwwMCw3OSwwMCw0RiwwMCw2RSwwMCw2QywwMCw3OSwwMCxDQSw1MCwwMCwwMCwwMCwwMCwwMAoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkU3RvcmVcU3RvcmVcRGVmYXVsdEFjY291bnRcQ3VycmVudFxkZWZhdWx0JHdpbmRvd3MuZGF0YS5kb25vdGRpc3R1cmIucXVpZXRtb21lbnQkcXVpZXRtb21lbnRsaXN0XHdpbmRvd3MuZGF0YS5kb25vdGRpc3R1cmIucXVpZXRtb21lbnQkcXVpZXRtb21lbnRmdWxsc2NyZWVuXQoiRGF0YSI9aGV4KDMpOjQzLDQyLDAxLDAwLDBBLDAyLDAxLDAwLDJBLDA2LEUwLEYzLEFBLENDLDA2LDJBLDJCLDBFLDVBLDQzLFwKNDIsMDEsMDAsQzIsMEEsMDEsRDIsMUUsMjYsNEQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNkYsMDAsNzMsMDAsNkYsMDAsNjYsMDAsXAo3NCwwMCwyRSwwMCw1MSwwMCw3NSwwMCw2OSwwMCw2NSwwMCw3NCwwMCw0OCwwMCw2RiwwMCw3NSwwMCw3MiwwMCw3MywwMCw1MCxcCjAwLDcyLDAwLDZGLDAwLDY2LDAwLDY5LDAwLDZDLDAwLDY1LDAwLDJFLDAwLDQxLDAwLDZDLDAwLDYxLDAwLDcyLDAwLDZELDAwLFwKNzMsMDAsNEYsMDAsNkUsMDAsNkMsMDAsNzksMDAsQ0EsNTAsMDAsMDAsMDAsMDAsMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXERlZmF1bHRBY2NvdW50XEN1cnJlbnRcZGVmYXVsdCR3aW5kb3dzLmRhdGEuZG9ub3RkaXN0dXJiLnF1aWV0bW9tZW50JHF1aWV0bW9tZW50bGlzdFx3aW5kb3dzLmRhdGEuZG9ub3RkaXN0dXJiLnF1aWV0bW9tZW50JHF1aWV0bW9tZW50cG9zdG9vYmVdCiJEYXRhIj1oZXgoMyk6NDMsNDIsMDEsMDAsMEEsMDIsMDEsMDAsMkEsMDYsREYsRjMsQUEsQ0MsMDYsMkEsMkIsMEUsNUUsNDMsXAo0MiwwMSwwMCxDMiwwQSwwMSxEMiwxRSwyOCw0RCwwMCw2OSwwMCw2MywwMCw3MiwwMCw2RiwwMCw3MywwMCw2RiwwMCw2NiwwMCxcCjc0LDAwLDJFLDAwLDUxLDAwLDc1LDAwLDY5LDAwLDY1LDAwLDc0LDAwLDQ4LDAwLDZGLDAwLDc1LDAwLDcyLDAwLDczLDAwLDUwLFwKMDAsNzIsMDAsNkYsMDAsNjYsMDAsNjksMDAsNkMsMDAsNjUsMDAsMkUsMDAsNTAsMDAsNzIsMDAsNjksMDAsNkYsMDAsNzIsMDAsXAo2OSwwMCw3NCwwMCw3OSwwMCw0RiwwMCw2RSwwMCw2QywwMCw3OSwwMCxDQSw1MCwwMCwwMCwwMCwwMCwwMAoKOyBkaXNhYmxlIHNldCBwcmlvcml0eSBub3RpZmljYXRpb25zCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDbG91ZFN0b3JlXFN0b3JlXERlZmF1bHRBY2NvdW50XEN1cnJlbnRcZGVmYXVsdCR3aW5kb3dzLmRhdGEuZG9ub3RkaXN0dXJiLnF1aWV0aG91cnNwcm9maWxlJHF1aWV0aG91cnNwcm9maWxlbGlzdFx3aW5kb3dzLmRhdGEuZG9ub3RkaXN0dXJiLnF1aWV0aG91cnNwcm9maWxlJG1pY3Jvc29mdC5xdWlldGhvdXJzcHJvZmlsZS5wcmlvcml0eW9ubHldCiJEYXRhIj1oZXg6NDMsNDIsMDEsMDAsMGEsMDIsMDEsMDAsMmEsMDYsYmUsODksYWIsY2MsMDYsMmEsMmIsMGUsZDAsMDMsNDMsNDIsXAogIDAxLDAwLGMyLDBhLDAxLGNkLDE0LDA2LDAyLDA1LDAwLDAwLDAxLDAxLDAyLDAwLDAzLDAxLDA0LDAwLGNjLDMyLDEyLDA1LDI4LFwKICA0ZCwwMCw2OSwwMCw2MywwMCw3MiwwMCw2ZiwwMCw3MywwMCw2ZiwwMCw2NiwwMCw3NCwwMCwyZSwwMCw1MywwMCw2MywwMCw3MixcCiAgMDAsNjUsMDAsNjUsMDAsNmUsMDAsNTMsMDAsNmIsMDAsNjUsMDAsNzQsMDAsNjMsMDAsNjgsMDAsNWYsMDAsMzgsMDAsNzcsMDAsXAogIDY1LDAwLDZiLDAwLDc5LDAwLDYyLDAwLDMzLDAwLDY0LDAwLDM4LDAwLDYyLDAwLDYyLDAwLDc3LDAwLDY1LDAwLDIxLDAwLDQxLFwKICAwMCw3MCwwMCw3MCwwMCwyOSw0ZCwwMCw2OSwwMCw2MywwMCw3MiwwMCw2ZiwwMCw3MywwMCw2ZiwwMCw2NiwwMCw3NCwwMCwyZSxcCiAgMDAsNTcsMDAsNjksMDAsNmUsMDAsNjQsMDAsNmYsMDAsNzcsMDAsNzMsMDAsNDEsMDAsNmMsMDAsNjEsMDAsNzIsMDAsNmQsMDAsXAogIDczLDAwLDVmLDAwLDM4LDAwLDc3LDAwLDY1LDAwLDZiLDAwLDc5LDAwLDYyLDAwLDMzLDAwLDY0LDAwLDM4LDAwLDYyLDAwLDYyLFwKICAwMCw3NywwMCw2NSwwMCwyMSwwMCw0MSwwMCw3MCwwMCw3MCwwMCwzMSw0ZCwwMCw2OSwwMCw2MywwMCw3MiwwMCw2ZiwwMCw3MyxcCiAgMDAsNmYsMDAsNjYsMDAsNzQsMDAsMmUsMDAsNTgsMDAsNjIsMDAsNmYsMDAsNzgsMDAsNDEsMDAsNzAsMDAsNzAsMDAsNWYsMDAsXAogIDM4LDAwLDc3LDAwLDY1LDAwLDZiLDAwLDc5LDAwLDYyLDAwLDMzLDAwLDY0LDAwLDM4LDAwLDYyLDAwLDYyLDAwLDc3LDAwLDY1LFwKICAwMCwyMSwwMCw0ZCwwMCw2OSwwMCw2MywwMCw3MiwwMCw2ZiwwMCw3MywwMCw2ZiwwMCw2NiwwMCw3NCwwMCwyZSwwMCw1OCwwMCxcCiAgNjIsMDAsNmYsMDAsNzgsMDAsNDEsMDAsNzAsMDAsNzAsMDAsMmQsNGQsMDAsNjksMDAsNjMsMDAsNzIsMDAsNmYsMDAsNzMsMDAsXAogIDZmLDAwLDY2LDAwLDc0LDAwLDJlLDAwLDU4LDAwLDYyLDAwLDZmLDAwLDc4LDAwLDQ3LDAwLDYxLDAwLDZkLDAwLDY5LDAwLDZlLFwKICAwMCw2NywwMCw0ZiwwMCw3NiwwMCw2NSwwMCw3MiwwMCw2YywwMCw2MSwwMCw3OSwwMCw1ZiwwMCwzOCwwMCw3NywwMCw2NSwwMCxcCiAgNmIsMDAsNzksMDAsNjIsMDAsMzMsMDAsNjQsMDAsMzgsMDAsNjIsMDAsNjIsMDAsNzcsMDAsNjUsMDAsMjEsMDAsNDEsMDAsNzAsXAogIDAwLDcwLDAwLDI5LDU3LDAwLDY5LDAwLDZlLDAwLDY0LDAwLDZmLDAwLDc3LDAwLDczLDAwLDJlLDAwLDUzLDAwLDc5LDAwLDczLFwKICAwMCw3NCwwMCw2NSwwMCw2ZCwwMCwyZSwwMCw0ZSwwMCw2NSwwMCw2MSwwMCw3MiwwMCw1MywwMCw2OCwwMCw2MSwwMCw3MiwwMCxcCiAgNjUsMDAsNDUsMDAsNzgsMDAsNzAsMDAsNjUsMDAsNzIsMDAsNjksMDAsNjUsMDAsNmUsMDAsNjMsMDAsNjUsMDAsNTIsMDAsNjUsXAogIDAwLDYzLDAwLDY1LDAwLDY5LDAwLDc2LDAwLDY1LDAwLDAwLDAwLDAwLDAwCgo7IGRpc2FibGUgZm9jdXMgc2V0dGluZ3MKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXENsb3VkU3RvcmVcU3RvcmVcRGVmYXVsdEFjY291bnRcQ3VycmVudFxkZWZhdWx0JHdpbmRvd3MuZGF0YS5zaGVsbC5mb2N1c3Nlc3Npb25hY3RpdmV0aGVtZVx3aW5kb3dzLmRhdGEuc2hlbGwuZm9jdXNzZXNzaW9uYWN0aXZldGhlbWUkezFiMDE5MzY1LTI1YTUtNGZmMS1iNTBhLWMxNTUyMjlhZmM4Zn1dCiJEYXRhIj1oZXgoMyk6NDMsNDIsMDEsMDAsMEEsMDAsMkEsMDYsRjQsRTIsQUEsQ0MsMDYsMkEsMkIsMEUsMDgsNDMsNDIsMDEsXAowMCxDMiwwQSwwMSwwMCwwMCwwMCwwMAoKOyBiYXR0ZXJ5IG9wdGlvbnMgb3B0aW1pemUgZm9yIHZpZGVvIHF1YWxpdHkKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFZpZGVvU2V0dGluZ3NdCiJWaWRlb1F1YWxpdHlPbkJhdHRlcnkiPWR3b3JkOjAwMDAwMDAxCgo7IGRpc2FibGUgc3RvcmFnZSBzZW5zZQpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXFN0b3JhZ2VTZW5zZV0KIkFsbG93U3RvcmFnZVNlbnNlR2xvYmFsIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIGtlZXAgd2luZG93cyBydW5uaW5nIHNtb290aGx5CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTdG9yYWdlU2Vuc2VdCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cU3RvcmFnZVNlbnNlXFBhcmFtZXRlcnNdCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cU3RvcmFnZVNlbnNlXFBhcmFtZXRlcnNcQ2FjaGVkU2l6ZXNdCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cU3RvcmFnZVNlbnNlXFBhcmFtZXRlcnNcU3RvcmFnZVBvbGljeV0KOyBkaXNhYmxlIHN0b3JhZ2Ugc2Vuc2UKIjA0Ij1kd29yZDowMDAwMDAwMAo7IGRvbid0IGF1dG8gZGVsZXRlIHRlbXAgZmlsZXMKIjIwNDgiPWR3b3JkOjAwMDAwMDAwCjsgZG9uJ3QgYXV0byBlbXB0eSByZWN5Y2xlIGJpbgoiMDgiPWR3b3JkOjAwMDAwMDAwCjsgZG9uJ3QgYXV0byBkZWxldGUgZG93bmxvYWRzCiIyNTYiPWR3b3JkOjAwMDAwMDAwCjsgbmV2ZXIgYXV0byBydW4gc3RvcmFnZSBzZW5zZQoiMzIiPWR3b3JkOjAwMDAwMDAwCjsgc2V0dGluZ3Mgc2V0CiJTdG9yYWdlUG9saWNpZXNDaGFuZ2VkIj1kd29yZDowMDAwMDAwMQoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFN0b3JhZ2VTZW5zZVxQYXJhbWV0ZXJzXFN0b3JhZ2VQb2xpY3lcU3BhY2VIaXN0b3J5XQoKOyBkaXNhYmxlIGRyYWcgdHJheQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ0RQXQoiRHJhZ1RyYXlFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHNuYXAgd2luZG93IHNldHRpbmdzCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIlNuYXBBc3Npc3QiPWR3b3JkOjAwMDAwMDAwCiJESVRlc3QiPWR3b3JkOjAwMDAwMDAwCiJFbmFibGVTbmFwQmFyIj1kd29yZDowMDAwMDAwMAoiRW5hYmxlVGFza0dyb3VwcyI9ZHdvcmQ6MDAwMDAwMDAKIkVuYWJsZVNuYXBBc3Npc3RGbHlvdXQiPWR3b3JkOjAwMDAwMDAwCiJTbmFwRmlsbCI9ZHdvcmQ6MDAwMDAwMDAKIkpvaW50UmVzaXplIj1kd29yZDowMDAwMDAwMAoKOyBlbmFibGUgZW5kdGFzayBtZW51IHRhc2tiYXIKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXFRhc2tiYXJEZXZlbG9wZXJTZXR0aW5nc10KIlRhc2tiYXJFbmRUYXNrIj1kd29yZDowMDAwMDAwMQoKOyBlbmFibGUgbG9uZyBwYXRocwpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxDb250cm9sXEZpbGVTeXN0ZW1dCiJMb25nUGF0aHNFbmFibGVkIj1kd29yZDowMDAwMDAwMQoKOyBhbHQgdGFiIG9wZW4gd2luZG93cyBvbmx5CltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3JlclxBZHZhbmNlZF0KIk11bHRpVGFza2luZ0FsdFRhYkZpbHRlciI9ZHdvcmQ6MDAwMDAwMDMKCjsgZGlzYWJsZSBzaGFyZSBhY3Jvc3MgZGV2aWNlcwpbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ0RQXQoiUm9tZVNka0NoYW5uZWxVc2VyQXV0aHpQb2xpY3kiPWR3b3JkOjAwMDAwMDAwCiJDZHBTZXNzaW9uVXNlckF1dGh6UG9saWN5Ij1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHJlY29tbWVuZGVkIHRyb3VibGVzaG9vdGVyIHByZWZlcmVuY2VzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NNaXRpZ2F0aW9uXQoiVXNlclByZWZlcmVuY2UiPWR3b3JkOjAwMDAwMDAxCgoKCgo7IC0tT1RIRVItLQoKCgoKOyBTVE9SRQo7IGRpc2FibGUgdXBkYXRlIGFwcHMgYXV0b21hdGljYWxseQpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFdpbmRvd3NTdG9yZVxXaW5kb3dzVXBkYXRlXQoiQXV0b0Rvd25sb2FkIj1kd29yZDowMDAwMDAwMgoKCgoKOyAtLUNBTidUIERPIE5BVElWRUxZLS0KCgoKCjsgTkVXIFNUQVJUIE1FTlUKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxDb250cm9sXEZlYXR1cmVNYW5hZ2VtZW50XE92ZXJyaWRlc1wxNFwyNzkyNTYyODI5XQoiRW5hYmxlZFN0YXRlIj1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxDb250cm9sXEZlYXR1cmVNYW5hZ2VtZW50XE92ZXJyaWRlc1wxNFwzMDM2MjQxNTQ4XQoiRW5hYmxlZFN0YXRlIj1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxDb250cm9sXEZlYXR1cmVNYW5hZ2VtZW50XE92ZXJyaWRlc1wxNFw3MzQ3MzE0MDRdCiJFbmFibGVkU3RhdGUiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXENvbnRyb2xcRmVhdHVyZU1hbmFnZW1lbnRcT3ZlcnJpZGVzXDE0XDc2MjI1NjUyNV0KIkVuYWJsZWRTdGF0ZSI9ZHdvcmQ6MDAwMDAwMDIKCjsgc2V0IHN0YXJ0IG1lbnUgYXBwcyB2aWV3IHRvIGxpc3QKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFN0YXJ0XQoiQWxsQXBwc1ZpZXdNb2RlIj1kd29yZDowMDAwMDAwMgoKCgoKOyBVV1AgQVBQUwo7IGRpc2FibGUgYmFja2dyb3VuZCBhcHBzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcQXBwUHJpdmFjeV0KIkxldEFwcHNSdW5JbkJhY2tncm91bmQiPWR3b3JkOjAwMDAwMDAyCgo7IGRpc2FibGUgYmFja2dyb3VuZCBhcHBzIGdsb2JhbApbSEtFWV9DVVJSRU5UX1VTRVJcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cU2VhcmNoXQoiQmFja2dyb3VuZEFwcEdsb2JhbFRvZ2dsZSI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxCYWNrZ3JvdW5kQWNjZXNzQXBwbGljYXRpb25zXQoiR2xvYmFsVXNlckRpc2FibGVkIj1kd29yZDowMDAwMDAwMQoKOyBkaXNhYmxlIHdpbmRvd3MgaW5wdXQgZXhwZXJpZW5jZSBwcmVsb2FkCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcaW5wdXRdCiJJc0lucHV0QXBwUHJlbG9hZEVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRHNoXQoiSXNQcmVsYXVuY2hFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIHdlYiBzZWFyY2ggaW4gc3RhcnQgbWVudSAKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXEV4cGxvcmVyXQoiRGlzYWJsZVNlYXJjaEJveFN1Z2dlc3Rpb25zIj1kd29yZDowMDAwMDAwMQoKOyBkaXNhYmxlIGNvcGlsb3QgJiBhaQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcUG9saWNpZXNcTWljcm9zb2Z0XFdpbmRvd3NcV2luZG93c0NvcGlsb3RdCiJUdXJuT2ZmV2luZG93c0NvcGlsb3QiPWR3b3JkOjAwMDAwMDAxCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXFdpbmRvd3NDb3BpbG90XQoiVHVybk9mZldpbmRvd3NDb3BpbG90Ij1kd29yZDowMDAwMDAwMQoKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXEFkdmFuY2VkXQoiU2hvd0NvcGlsb3RCdXR0b24iPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFBvbGljaWVzXE1pY3Jvc29mdFxXaW5kb3dzXFdpbmRvd3NBSV0KIkRpc2FibGVBSURhdGFBbmFseXNpcyI9ZHdvcmQ6MDAwMDAwMDEKIkFsbG93UmVjYWxsRW5hYmxlbWVudCI9ZHdvcmQ6MDAwMDAwMDAKIkRpc2FibGVDbGlja1RvRG8iPWR3b3JkOjAwMDAwMDAxCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXFNoZWxsXENvcGlsb3RcQmluZ0NoYXRdCiJJc1VzZXJFbGlnaWJsZSI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcUGFpbnRdCiJEaXNhYmxlR2VuZXJhdGl2ZUZpbGwiPWR3b3JkOjAwMDAwMDAxCiJEaXNhYmxlQ29jcmVhdG9yIj1kd29yZDowMDAwMDAwMQoiRGlzYWJsZUltYWdlQ3JlYXRvciI9ZHdvcmQ6MDAwMDAwMDEKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcUG9saWNpZXNcV2luZG93c05vdGVwYWRdCiJEaXNhYmxlQUlGZWF0dXJlcyI9ZHdvcmQ6MDAwMDAwMDEKCjsgZGlzYWJsZSB3aWRnZXRzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFBvbGljeU1hbmFnZXJcZGVmYXVsdFxOZXdzQW5kSW50ZXJlc3RzXEFsbG93TmV3c0FuZEludGVyZXN0c10KInZhbHVlIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIG1zLWdhbWViYXIgbm90aWZpY2F0aW9ucyB3aXRoIHhib3ggY29udHJvbGxlciBwbHVnZ2VkIGluCltIS0VZX0NMQVNTRVNfUk9PVFxtcy1nYW1lYmFyXQoiKERlZmF1bHQpIj0iVVJMOm1zLWdhbWViYXIiCiJVUkwgUHJvdG9jb2wiPSIiCiJOb09wZW5XaXRoIj0iIgoKW0hLRVlfQ0xBU1NFU19ST09UXG1zLWdhbWViYXJcc2hlbGxcb3Blblxjb21tYW5kXQoiKERlZmF1bHQpIj0iJVN5c3RlbVJvb3QlXFxTeXN0ZW0zMlxcc3lzdHJheS5leGUiCgpbSEtFWV9DTEFTU0VTX1JPT1RcbXMtZ2FtZWJhcnNlcnZpY2VzXQoiKERlZmF1bHQpIj0iVVJMOm1zLWdhbWViYXJzZXJ2aWNlcyIKIlVSTCBQcm90b2NvbCI9IiIKIk5vT3BlbldpdGgiPSIiCgpbSEtFWV9DTEFTU0VTX1JPT1RcbXMtZ2FtZWJhcnNlcnZpY2VzXHNoZWxsXG9wZW5cY29tbWFuZF0KIihEZWZhdWx0KSI9IiVTeXN0ZW1Sb290JVxcU3lzdGVtMzJcXHN5c3RyYXkuZXhlIgoKW0hLRVlfQ0xBU1NFU19ST09UXG1zLWdhbWluZ292ZXJsYXldCiIoRGVmYXVsdCkiPSJVUkw6bXMtZ2FtaW5nb3ZlcmxheSIKIlVSTCBQcm90b2NvbCI9IiIKIk5vT3BlbldpdGgiPSIiCgpbSEtFWV9DTEFTU0VTX1JPT1RcbXMtZ2FtaW5nb3ZlcmxheVxzaGVsbFxvcGVuXGNvbW1hbmRdCiIoRGVmYXVsdCkiPSIlU3lzdGVtUm9vdCVcXFN5c3RlbTMyXFxzeXN0cmF5LmV4ZSIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NSdW50aW1lXEFjdGl2YXRhYmxlQ2xhc3NJZFxXaW5kb3dzLkdhbWluZy5HYW1lQmFyLlByZXNlbmNlU2VydmVyLkludGVybmFsLlByZXNlbmNlV3JpdGVyXQoiQWN0aXZhdGlvblR5cGUiPWR3b3JkOjAwMDAwMDAwCgoKCgo7IERJU0FCTEUgQURWRVJUSVNJTkcgJiBQUk9NT1RJT05BTApbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cQ29udGVudERlbGl2ZXJ5TWFuYWdlcl0KIkNvbnRlbnREZWxpdmVyeUFsbG93ZWQiPWR3b3JkOjAwMDAwMDAwCiJGZWF0dXJlTWFuYWdlbWVudEVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJPZW1QcmVJbnN0YWxsZWRBcHBzRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKIlByZUluc3RhbGxlZEFwcHNFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiUHJlSW5zdGFsbGVkQXBwc0V2ZXJFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiUm90YXRpbmdMb2NrU2NyZWVuRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKIlJvdGF0aW5nTG9ja1NjcmVlbk92ZXJsYXlFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiU2lsZW50SW5zdGFsbGVkQXBwc0VuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJTbGlkZXNob3dFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiU29mdExhbmRpbmdFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiU3Vic2NyaWJlZENvbnRlbnQtMzEwMDkzRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKIlN1YnNjcmliZWRDb250ZW50LTMxNDU2M0VuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJTdWJzY3JpYmVkQ29udGVudC0zMzgzODhFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiU3Vic2NyaWJlZENvbnRlbnQtMzM4Mzg5RW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKIlN1YnNjcmliZWRDb250ZW50LTMzODM5M0VuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJTdWJzY3JpYmVkQ29udGVudC0zNTM2OTRFbmFibGVkIj1kd29yZDowMDAwMDAwMAoiU3Vic2NyaWJlZENvbnRlbnQtMzUzNjk2RW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKIlN1YnNjcmliZWRDb250ZW50LTM1MzY5OEVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJTdWJzY3JpYmVkQ29udGVudEVuYWJsZWQiPWR3b3JkOjAwMDAwMDAwCiJTeXN0ZW1QYW5lU3VnZ2VzdGlvbnNFbmFibGVkIj1kd29yZDowMDAwMDAwMAoKCgoKOyBPVEhFUgo7IHJlbW92ZSAzZCBvYmplY3RzClstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXE15Q29tcHV0ZXJcTmFtZVNwYWNlXHswREI3RTAzRi1GQzI5LTREQzYtOTAyMC1GRjQxQjU5RTUxM0F9XQoKWy1IS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcV09XNjQzMk5vZGVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRXhwbG9yZXJcTXlDb21wdXRlclxOYW1lU3BhY2VcezBEQjdFMDNGLUZDMjktNERDNi05MDIwLUZGNDFCNTlFNTEzQX1dCgo7IHJlbW92ZSBxdWljayBhY2Nlc3MKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxFeHBsb3Jlcl0KIkh1Yk1vZGUiPWR3b3JkOjAwMDAwMDAxCgo7IHJlbW92ZSBob21lIChicm9rZW4gb24gbmV3IHVwZGF0ZSkKOyBbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcQ2xhc3Nlc1xDTFNJRFx7Zjg3NDMxMGUtYjZiNy00N2RjLWJjODQtYjllNmIzOGY1OTAzfV0KOyBAPSJDTFNJRF9NU0dyYXBoSG9tZUZvbGRlciIKOyAiU3lzdGVtLklzUGlubmVkVG9OYW1lU3BhY2VUcmVlIj1kd29yZDowMDAwMDAwMAoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXERlc2t0b3BcTmFtZVNwYWNlXHtmODc0MzEwZS1iNmI3LTQ3ZGMtYmM4NC1iOWU2YjM4ZjU5MDN9XQo7ICJIaWRkZW5CeURlZmF1bHQiPWR3b3JkOjAwMDAwMDAxCgo7IFstSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXFdPVzY0MzJOb2RlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXEV4cGxvcmVyXERlc2t0b3BcTmFtZVNwYWNlXHtmODc0MzEwZS1iNmI3LTQ3ZGMtYmM4NC1iOWU2YjM4ZjU5MDN9XQoKOyByZW1vdmUgZ2FsbGVyeQpbSEtFWV9DVVJSRU5UX1VTRVJcU29mdHdhcmVcQ2xhc3Nlc1xDTFNJRFx7ZTg4ODY1ZWEtMGUxYy00ZTIwLTlhYTYtZWRjZDAyMTJjODdjfV0KIlN5c3RlbS5Jc1Bpbm5lZFRvTmFtZVNwYWNlVHJlZSI9ZHdvcmQ6MDAwMDAwMDAKCjsgcmVzdG9yZSB0aGUgY2xhc3NpYyBjb250ZXh0IG1lbnUKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXENsYXNzZXNcQ0xTSURcezg2Y2ExYWEwLTM0YWEtNGU4Yi1hNTA5LTUwYzkwNWJhZTJhMn1cSW5wcm9jU2VydmVyMzJdCkA9IiIKCjsgZGlzYWJsZSBtZW51IHNob3cgZGVsYXkKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnRyb2wgUGFuZWxcRGVza3RvcF0KIk1lbnVTaG93RGVsYXkiPSIwIgoKOyBkaXNhYmxlIGRyaXZlciBzZWFyY2hpbmcgJiB1cGRhdGVzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cRHJpdmVyU2VhcmNoaW5nXQoiU2VhcmNoT3JkZXJDb25maWciPWR3b3JkOjAwMDAwMDAwCgo7IG1vdXNlIGZpeCAobm8gYWNjZWwgd2l0aCBlcHAgb24pCltIS0VZX0NVUlJFTlRfVVNFUlxDb250cm9sIFBhbmVsXE1vdXNlXQoiTW91c2VTZW5zaXRpdml0eSI9IjEwIgoiU21vb3RoTW91c2VYQ3VydmUiPWhleDpcCgkwMCwwMCwwMCwwMCwwMCwwMCwwMCwwMCxcCglDMCxDQywwQywwMCwwMCwwMCwwMCwwMCxcCgk4MCw5OSwxOSwwMCwwMCwwMCwwMCwwMCxcCgk0MCw2NiwyNiwwMCwwMCwwMCwwMCwwMCxcCgkwMCwzMywzMywwMCwwMCwwMCwwMCwwMAoiU21vb3RoTW91c2VZQ3VydmUiPWhleDpcCgkwMCwwMCwwMCwwMCwwMCwwMCwwMCwwMCxcCgkwMCwwMCwzOCwwMCwwMCwwMCwwMCwwMCxcCgkwMCwwMCw3MCwwMCwwMCwwMCwwMCwwMCxcCgkwMCwwMCxBOCwwMCwwMCwwMCwwMCwwMCxcCgkwMCwwMCxFMCwwMCwwMCwwMCwwMCwwMAoKW0hLRVlfVVNFUlNcLkRFRkFVTFRcQ29udHJvbCBQYW5lbFxNb3VzZV0KIk1vdXNlU3BlZWQiPSIwIgoiTW91c2VUaHJlc2hvbGQxIj0iMCIKIk1vdXNlVGhyZXNob2xkMiI9IjAiCgo7IGRpc2FibGUgcGhvbmUgY29tcGFuaW9uIGluIHN0YXJ0IG1lbnUKW0hLRVlfQ1VSUkVOVF9VU0VSXFNvZnR3YXJlXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFN0YXJ0XQoiUmlnaHRDb21wYW5pb25Ub2dnbGVkT3BlbiI9ZHdvcmQ6MDAwMDAwMDAKCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxTdGFydFxDb21wYW5pb25zXE1pY3Jvc29mdC5Zb3VyUGhvbmVfOHdla3liM2Q4YmJ3ZV0KIklzRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKIklzQXZhaWxhYmxlIj1kd29yZDowMDAwMDAwMAoKOyBtb3JlIGluZm8gb24gYnNvZApbSEtFWV9MT0NBTF9NQUNISU5FXFN5c3RlbVxDdXJyZW50Q29udHJvbFNldFxDb250cm9sXENyYXNoQ29udHJvbF0KIkRpc3BsYXlQYXJhbWV0ZXJzIj1kd29yZDowMDAwMDAwMQoKOyBkaXNhYmxlIHdpbmRvd3MgcGxhdGZvcm0gYmluYXJ5IHRhYmxlCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXEN1cnJlbnRDb250cm9sU2V0XENvbnRyb2xcU2Vzc2lvbiBNYW5hZ2VyXQoiRGlzYWJsZVdwYnRFeGVjdXRpb24iPWR3b3JkOjAwMDAwMDAxCgo7IG5vIHdlYiBzZXJ2aWNlcyBpbiBleHBsb3JlcgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxXaW5kb3dzXEN1cnJlbnRWZXJzaW9uXFBvbGljaWVzXEV4cGxvcmVyXQoiTm9XZWJTZXJ2aWNlcyI9ZHdvcmQ6MDAwMDAwMDEKCjsgZGlzYWJsZSBjcm9zcyBkZXZpY2UgcmVzdW1lCltIS0VZX0NVUlJFTlRfVVNFUlxTb2Z0d2FyZVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxDcm9zc0RldmljZVJlc3VtZVxDb25maWd1cmF0aW9uXQoiSXNSZXN1bWVBbGxvd2VkIj1kd29yZDowMDAwMDAwMAoiSXNPbmVEcml2ZVJlc3VtZUFsbG93ZWQiPWR3b3JkOjAwMDAwMDAwCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNPRlRXQVJFXE1pY3Jvc29mdFxQb2xpY3lNYW5hZ2VyXGRlZmF1bHRcQ29ubmVjdGl2aXR5XERpc2FibGVDcm9zc0RldmljZVJlc3VtZV0KInZhbHVlIj1kd29yZDowMDAwMDAwMQoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxDb250cm9sXEZlYXR1cmVNYW5hZ2VtZW50XE92ZXJyaWRlc1w4XDEzODcwMjA5NDNdCiJFbmFibGVkU3RhdGUiPWR3b3JkOjAwMDAwMDAxCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXENvbnRyb2xcRmVhdHVyZU1hbmFnZW1lbnRcT3ZlcnJpZGVzXDhcMTY5NDY2MTI2MF0KIkVuYWJsZWRTdGF0ZSI9ZHdvcmQ6MDAwMDAwMDEKCjsgaGlkZSBob21lIGluIHNldHRpbmdzCltIS0VZX0xPQ0FMX01BQ0hJTkVcU09GVFdBUkVcTWljcm9zb2Z0XFdpbmRvd3NcQ3VycmVudFZlcnNpb25cUG9saWNpZXNcRXhwbG9yZXJdCiJTZXR0aW5nc1BhZ2VWaXNpYmlsaXR5Ij0iaGlkZTpob21lOyIKCjsgZGlzYWJsZSBvcGVuIHRlcm1pbmFsIGJ5IGRlZmF1bHQKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnNvbGVcJSVTdGFydHVwXQoiRGVsZWdhdGlvbkNvbnNvbGUiPSJ7QjIzRDEwQzAtRTUyRS00MTFFLTlENUItQzA5RkRGNzA5QzdEfSIKIkRlbGVnYXRpb25UZXJtaW5hbCI9IntCMjNEMTBDMC1FNTJFLTQxMUUtOUQ1Qi1DMDlGREY3MDlDN0R9IgoKOyBibGFjayBwb3dlcnNoZWxsIGNvbnNvbGUKW0hLRVlfQ1VSUkVOVF9VU0VSXENvbnNvbGVcJVN5c3RlbVJvb3QlX1N5c3RlbTMyX1dpbmRvd3NQb3dlclNoZWxsX3YxLjBfcG93ZXJzaGVsbC5leGVdCiJTY3JlZW5Db2xvcnMiPWR3b3JkOjAwMDAwMDBGCgo7IGZpeCBlbnRlciB5b3VyIHBpbiBoZWxsbyBmYWNlIHNpZ24gaW4gYnVnIGFsbG93IHBhc3N3b3JkIGluc3RlYWQKW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93cyBOVFxDdXJyZW50VmVyc2lvblxQYXNzd29yZExlc3NcRGV2aWNlXQoiRGV2aWNlUGFzc3dvcmRMZXNzQnVpbGRWZXJzaW9uIj1kd29yZDowMDAwMDAwMAoKOyBkaXNhYmxlIGZpbmlzaCBzZXR0aW5nIHVwIHlvdXIgZGV2aWNlCltIS0VZX0NVUlJFTlRfVVNFUlxTT0ZUV0FSRVxNaWNyb3NvZnRcV2luZG93c1xDdXJyZW50VmVyc2lvblxVc2VyUHJvZmlsZUVuZ2FnZW1lbnRdCiJTY29vYmVTeXN0ZW1TZXR0aW5nRW5hYmxlZCI9ZHdvcmQ6MDAwMDAwMDAKCjsgZGlzYWJsZSBiYWNrZ3JvdW5kIGJsdXIgZHVyaW5nIHNpZ24taW4KW0hLRVlfTE9DQUxfTUFDSElORVxTT0ZUV0FSRVxQb2xpY2llc1xNaWNyb3NvZnRcV2luZG93c1xTeXN0ZW1dCiJEaXNhYmxlQWNyeWxpY0JhY2tncm91bmRPbkxvZ29uIj1kd29yZDowMDAwMDAwMQo='
$sync.assets.servicesoff = '77u/IyBTQ1JJUFQgUlVOIEFTIEFETUlOCiAgICAgICAgSWYgKCEoW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzUHJpbmNpcGFsXVtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0lkZW50aXR5XTo6R2V0Q3VycmVudCgpKS5Jc0luUm9sZShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NCdWlsdEluUm9sZV0iQWRtaW5pc3RyYXRvciIpKQogICAgICAgIHtTdGFydC1Qcm9jZXNzIFBvd2VyU2hlbGwuZXhlIC1Bcmd1bWVudExpc3QgKCItTm9Qcm9maWxlIC1FeGVjdXRpb25Qb2xpY3kgQnlwYXNzIC1GaWxlIGAiezB9YCIiIC1mICRQU0NvbW1hbmRQYXRoKSAtVmVyYiBSdW5BcwogICAgICAgIEV4aXR9CiAgICAgICAgJEhvc3QuVUkuUmF3VUkuV2luZG93VGl0bGUgPSAkbXlJbnZvY2F0aW9uLk15Q29tbWFuZC5EZWZpbml0aW9uICsgIiAoQWRtaW5pc3RyYXRvcikiCiAgICAgICAgJEhvc3QuVUkuUmF3VUkuQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzRm9yZWdyb3VuZENvbG9yID0gIldoaXRlIgogICAgICAgIENsZWFyLUhvc3QKCiAgICAgICAgIyBGVU5DVElPTiBSVU4gQVMgVFJVU1RFRCBJTlNUQUxMRVIKICAgICAgICBmdW5jdGlvbiBSdW4tVHJ1c3RlZChbU3RyaW5nXSRjb21tYW5kKSB7CiAgICAgICAgdHJ5IHsKICAgIAlTdG9wLVNlcnZpY2UgLU5hbWUgVHJ1c3RlZEluc3RhbGxlciAtRm9yY2UgLUVycm9yQWN0aW9uIFN0b3AgLVdhcm5pbmdBY3Rpb24gU3RvcAogIAkJfQogIAkJY2F0Y2ggewogICAgCXRhc2traWxsIC9pbSB0cnVzdGVkaW5zdGFsbGVyLmV4ZSAvZiA+JG51bGwKICAJCX0KICAgICAgICAkc2VydmljZSA9IEdldC1DaW1JbnN0YW5jZSAtQ2xhc3NOYW1lIFdpbjMyX1NlcnZpY2UgLUZpbHRlciAiTmFtZT0nVHJ1c3RlZEluc3RhbGxlciciCiAgICAgICAgJERlZmF1bHRCaW5QYXRoID0gJHNlcnZpY2UuUGF0aE5hbWUKICAJCSR0cnVzdGVkSW5zdGFsbGVyUGF0aCA9ICIkZW52OlN5c3RlbVJvb3Rcc2VydmljaW5nXFRydXN0ZWRJbnN0YWxsZXIuZXhlIgogIAkJaWYgKCREZWZhdWx0QmluUGF0aCAtbmUgJHRydXN0ZWRJbnN0YWxsZXJQYXRoKSB7CiAgICAJJERlZmF1bHRCaW5QYXRoID0gJHRydXN0ZWRJbnN0YWxsZXJQYXRoCiAgCQl9CiAgICAgICAgJGJ5dGVzID0gW1N5c3RlbS5UZXh0LkVuY29kaW5nXTo6VW5pY29kZS5HZXRCeXRlcygkY29tbWFuZCkKICAgICAgICAkYmFzZTY0Q29tbWFuZCA9IFtDb252ZXJ0XTo6VG9CYXNlNjRTdHJpbmcoJGJ5dGVzKQogICAgICAgIHNjLmV4ZSBjb25maWcgVHJ1c3RlZEluc3RhbGxlciBiaW5QYXRoPSAiY21kLmV4ZSAvYyBwb3dlcnNoZWxsLmV4ZSAtZW5jb2RlZGNvbW1hbmQgJGJhc2U2NENvbW1hbmQiIHwgT3V0LU51bGwKICAgICAgICBzYy5leGUgc3RhcnQgVHJ1c3RlZEluc3RhbGxlciB8IE91dC1OdWxsCiAgICAgICAgc2MuZXhlIGNvbmZpZyBUcnVzdGVkSW5zdGFsbGVyIGJpbnBhdGg9ICJgIiREZWZhdWx0QmluUGF0aGAiIiB8IE91dC1OdWxsCiAgICAgICAgdHJ5IHsKICAgIAlTdG9wLVNlcnZpY2UgLU5hbWUgVHJ1c3RlZEluc3RhbGxlciAtRm9yY2UgLUVycm9yQWN0aW9uIFN0b3AgLVdhcm5pbmdBY3Rpb24gU3RvcAogIAkJfQogIAkJY2F0Y2ggewogICAgCXRhc2traWxsIC9pbSB0cnVzdGVkaW5zdGFsbGVyLmV4ZSAvZiA+JG51bGwKICAJCX0KICAgICAgICB9CgpXcml0ZS1Ib3N0ICJTZXJ2aWNlczogT2ZmLi4uYG4iCgojIGNyZWF0ZSByZWcgZmlsZQokU2VydmljZXNPZmYgPSBAJwpXaW5kb3dzIFJlZ2lzdHJ5IEVkaXRvciBWZXJzaW9uIDUuMDAKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQURQU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEFhclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBSlJvdXRlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBTEddCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQXBwSURTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQXBwaW5mb10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBcHBNZ210XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEFwcFJlYWRpbmVzc10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBcHBWQ2xpZW50XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEFwcFhTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQXB4U3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEFzc2lnbmVkQWNjZXNzTWFuYWdlclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBdWRpb0VuZHBvaW50QnVpbGRlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBdWRpb3Nydl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xhdXRvdGltZXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBeEluc3RTVl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xCY2FzdERWUlVzZXJTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEJERVNWQ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xCRkVdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQklUU10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xCbHVldG9vdGhVc2VyU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xCcm93c2VyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEJyb2tlckluZnJhc3RydWN0dXJlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEJUQUdTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEJ0aEF2Y3RwU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGJ0aHNlcnZdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcY2Ftc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXENhcHR1cmVTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGNiZGhzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ0RQU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXENEUFVzZXJTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ2VydFByb3BTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ2xpcFNWQ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xDbG91ZEJhY2t1cFJlc3RvcmVTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcY2xvdWRpZHN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xDT01TeXNBcHBdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ29uc2VudFV4VXNlclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xDb3JlTWVzc2FnaW5nUmVnaXN0cmFyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXENyZWRlbnRpYWxFbnJvbGxtZW50TWFuYWdlclVzZXJTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ3J5cHRTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ3NjU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEY29tTGF1bmNoXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGRjc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGRlZnJhZ3N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEZXZpY2VBc3NvY2lhdGlvbkJyb2tlclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEZXZpY2VBc3NvY2lhdGlvblNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRGV2aWNlSW5zdGFsbF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEZXZpY2VQaWNrZXJVc2VyU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXERldmljZXNGbG93VXNlclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEZXZRdWVyeUJyb2tlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEaGNwXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGRpYWdub3N0aWNzaHViLnN0YW5kYXJkY29sbGVjdG9yLnNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcZGlhZ3N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEaWFnVHJhY2tdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRGlhbG9nQmxvY2tpbmdTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXERpc3BCcm9rZXJEZXNrdG9wU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXERpc3BsYXlFbmhhbmNlbWVudFNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRG1FbnJvbGxtZW50U3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGRtd2FwcHVzaHNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRG5zY2FjaGVdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRG9TdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcZG90M3N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEUFNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRHNtU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXERzU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXER1c21TdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRWFwSG9zdF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xFRlNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcZW1iZWRkZWRtb2RlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEVudEFwcFN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xFdmVudExvZ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xFdmVudFN5c3RlbV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xGYXhdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcZmRQSG9zdF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xGRFJlc1B1Yl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xmaHN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xGb250Q2FjaGVdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRm9udENhY2hlMy4wLjAuMF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xGcmFtZVNlcnZlck1vbml0b3JdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRnJhbWVTZXJ2ZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcR2FtZUlucHV0U3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGdwc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEdyYXBoaWNzUGVyZlN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xoaWRzZXJ2XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGhwYXRjaG1vbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xIdkhvc3RdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcaWNzc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXElLRUVYVF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xJbnN0YWxsU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xJbnZlbnRvcnlTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcaXBobHBzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcSXB4bGF0Q2ZnU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEtleUlzb10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xLdG1SbV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xMYW5tYW5TZXJ2ZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTGFubWFuV29ya3N0YXRpb25dCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcbGZzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTGljZW5zZU1hbmFnZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcbGx0ZHN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xsbWhvc3RzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXExvY2FsS2RjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXExTTV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xMeHBTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTWFwc0Jyb2tlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xNY21TdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTWNwTWFuYWdlbWVudFNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCjsgW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcU2VydmljZXNcTURDb3JlU3ZjXQo7ICJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTWVzc2FnaW5nU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xtaWRpc3J2XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE1peGVkUmVhbGl0eU9wZW5YUlN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xtcHNzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTVNEVENdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTVNpU0NTSV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xtc2lzZXJ2ZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTXNLZXlib2FyZEZpbHRlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xOYXR1cmFsQXV0aGVudGljYXRpb25dCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTmNhU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE5jYlNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTmNkQXV0b1NldHVwXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE5ldGxvZ29uXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE5ldG1hbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xuZXRwcm9mbV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xOZXRTZXR1cFN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xOZXRUY3BQb3J0U2hhcmluZ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xOZ2NDdG5yU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE5nY1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xObGFTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTlBTTVN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xuc2ldCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcT25lU3luY1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xwMnBpbXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xwMnBzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUDlSZHJTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBjYVN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xQZWVyRGlzdFN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xQZW5TZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHBlcmNlcHRpb25zaW11bGF0aW9uXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBlcmZIb3N0XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBob25lU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBpbUluZGV4TWFpbnRlbmFuY2VTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNccGxhXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBsdWdQbGF5XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBOUlBBdXRvUmVnXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBOUlBzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUG9saWN5QWdlbnRdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUG93ZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUHJpbnREZXZpY2VDb25maWd1cmF0aW9uU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xQcmludE5vdGlmeV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xQcmludFNjYW5Ccm9rZXJTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFByaW50V29ya2Zsb3dVc2VyU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFByb2ZTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUHVzaFRvSW5zdGFsbF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xRV0FWRV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xSYXNBdXRvXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFJhc01hbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xyZWZzZGVkdXBzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUmVtb3RlQWNjZXNzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFJlbW90ZVJlZ2lzdHJ5XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFJldGFpbERlbW9dCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUm1TdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUnBjRXB0TWFwcGVyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFJwY0xvY2F0b3JdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUnBjU3NdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2FtU3NdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU0NhcmRTdnJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2NEZXZpY2VFbnVtXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNjaGVkdWxlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNDUG9saWN5U3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNEUlNWQ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzZWNsb2dvbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxTZXJ2aWNlc1xTZWN1cml0eUhlYWx0aFNlcnZpY2VdCjsgIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTRU1nclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTZW5zb3JEYXRhU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTZW5zb3JTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNlbnNyU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNFTlNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCjsgW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcU2VydmljZXNcU2Vuc2VdCjsgIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTZXNzaW9uRW52XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNncm1Ccm9rZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2hhcmVkQWNjZXNzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNoYXJlZFJlYWxpdHlTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2hlbGxIV0RldGVjdGlvbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzaHBhbXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzbXBob3N0XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNtc1JvdXRlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTTk1QVHJhcF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzcGVjdHJ1bV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTcG9vbGVyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHNwcHN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTU0RQU1JWXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHNzaC1hZ2VudF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTc3RwU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFN0YXRlUmVwb3NpdG9yeV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzdGlzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU3RpU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFN0b3JTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcc3ZzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcc3dwcnZdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU3lzTWFpbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTeXN0ZW1FdmVudHNCcm9rZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVGFibGV0SW5wdXRTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFRhcGlTcnZdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVGVybVNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVGV4dElucHV0TWFuYWdlbWVudFNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVGhlbWVzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFRpZXJpbmdFbmdpbmVTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFRpbWVCcm9rZXJTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVG9rZW5Ccm9rZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVHJrV2tzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFRyb3VibGVzaG9vdGluZ1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xUcnVzdGVkSW5zdGFsbGVyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHR6YXV0b3VwZGF0ZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xVZGtVc2VyU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVldkFnZW50U2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x1aHNzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVW1SZHBTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVuaXN0b3JlU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHVwbnBob3N0XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVzZXJEYXRhU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVzZXJNYW5hZ2VyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVzb1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xWYWNTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVmF1bHRTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcdmRzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHZtaWNndWVzdGludGVyZmFjZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x2bWljaGVhcnRiZWF0XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHZtaWNrdnBleGNoYW5nZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x2bWljcmR2XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHZtaWNzaHV0ZG93bl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x2bWljdGltZXN5bmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcdm1pY3Ztc2Vzc2lvbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x2bWljdnNzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFZTU10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXMzJUaW1lXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdhYVNNZWRpY1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXYWxsZXRTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdhcnBKSVRTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd2JlbmdpbmVdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2Jpb1NydmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2Ntc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdjbmNzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2RpU2VydmljZUhvc3RdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2RpU3lzdGVtSG9zdF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxTZXJ2aWNlc1xXZE5pc1N2Y10KOyAiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdlYkNsaWVudF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxTZXJ2aWNlc1x3ZWJ0aHJlYXRkZWZzdmNdCjsgIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxTZXJ2aWNlc1x3ZWJ0aHJlYXRkZWZ1c2Vyc3ZjXQo7ICJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2Vjc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdFUEhPU1RTVkNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd2VyY3Bsc3VwcG9ydF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXZXJTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV0ZEU0Nvbk1nclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x3aGVzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2lhUnBjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0Cgo7IFtIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXEN1cnJlbnRDb250cm9sU2V0XFNlcnZpY2VzXFdpbkRlZmVuZF0KOyAiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdpbkh0dHBBdXRvUHJveHlTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2lubWdtdF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXaW5STV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x3aXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXbGFuU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdsaWRzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd2xwYXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXTWFuU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdtaUFwU3J2XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdNUE5ldHdvcmtTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd29ya2ZvbGRlcnNzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV3BjTW9uU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdQREJ1c0VudW1dCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV3BuU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXcG5Vc2VyU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXU0FJRmFicmljU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0Cgo7IFtIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXEN1cnJlbnRDb250cm9sU2V0XFNlcnZpY2VzXHdzY3N2Y10KOyAiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdTZWFyY2hdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd3VhdXNlcnZdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd3VxaXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXd2FuU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFhibEF1dGhNYW5hZ2VyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFhibEdhbWVTYXZlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFhib3hHaXBTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcWGJveE5ldEFwaVN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xaVEhFTFBFUl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAonQApTZXQtQ29udGVudCAtUGF0aCAiJGVudjpTeXN0ZW1Sb290XFRlbXBcc2VydmljZXNvZmYucmVnIiAtVmFsdWUgJFNlcnZpY2VzT2ZmIC1Gb3JjZQoKIyBpbXBvcnQgcmVnIGZpbGUgYXMgdHJ1c3RlZApSdW4tVHJ1c3RlZCAiUmVnZWRpdC5leGUgL1MgYCIkZW52OlN5c3RlbVJvb3RcVGVtcFxzZXJ2aWNlc29mZi5yZWdgIiIKCiMgaW1wb3J0IHJlZyBmaWxlIGFzIGFkbWluClJlZ2VkaXQuZXhlIC9TICIkZW52OlN5c3RlbVJvb3RcVGVtcFxzZXJ2aWNlc29mZi5yZWciCgojIHJlbW92ZSBzYWZlIG1vZGUgYm9vdApjbWQgL2MgImJjZGVkaXQgL2RlbGV0ZXZhbHVlIHtjdXJyZW50fSBzYWZlYm9vdCA+bnVsIDI+JjEiCgpXcml0ZS1Ib3N0ICJSZXN0YXJ0aW5nYG4iIC1Gb3JlZ3JvdW5kQ29sb3IgUmVkCgojIHJlc3RhcnQKU3RhcnQtU2xlZXAgLVNlY29uZHMgNQpzaHV0ZG93biAtciAtdCAwMAo='
$sync.assets.serviceson = '77u/IyBTQ1JJUFQgUlVOIEFTIEFETUlOCiAgICAgICAgSWYgKCEoW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzUHJpbmNpcGFsXVtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0lkZW50aXR5XTo6R2V0Q3VycmVudCgpKS5Jc0luUm9sZShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NCdWlsdEluUm9sZV0iQWRtaW5pc3RyYXRvciIpKQogICAgICAgIHtTdGFydC1Qcm9jZXNzIFBvd2VyU2hlbGwuZXhlIC1Bcmd1bWVudExpc3QgKCItTm9Qcm9maWxlIC1FeGVjdXRpb25Qb2xpY3kgQnlwYXNzIC1GaWxlIGAiezB9YCIiIC1mICRQU0NvbW1hbmRQYXRoKSAtVmVyYiBSdW5BcwogICAgICAgIEV4aXR9CiAgICAgICAgJEhvc3QuVUkuUmF3VUkuV2luZG93VGl0bGUgPSAkbXlJbnZvY2F0aW9uLk15Q29tbWFuZC5EZWZpbml0aW9uICsgIiAoQWRtaW5pc3RyYXRvcikiCiAgICAgICAgJEhvc3QuVUkuUmF3VUkuQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzQmFja2dyb3VuZENvbG9yID0gIkJsYWNrIgogICAgICAgICRIb3N0LlByaXZhdGVEYXRhLlByb2dyZXNzRm9yZWdyb3VuZENvbG9yID0gIldoaXRlIgogICAgICAgIENsZWFyLUhvc3QKCiAgICAgICAgIyBGVU5DVElPTiBSVU4gQVMgVFJVU1RFRCBJTlNUQUxMRVIKICAgICAgICBmdW5jdGlvbiBSdW4tVHJ1c3RlZChbU3RyaW5nXSRjb21tYW5kKSB7CiAgICAgICAgdHJ5IHsKICAgIAlTdG9wLVNlcnZpY2UgLU5hbWUgVHJ1c3RlZEluc3RhbGxlciAtRm9yY2UgLUVycm9yQWN0aW9uIFN0b3AgLVdhcm5pbmdBY3Rpb24gU3RvcAogIAkJfQogIAkJY2F0Y2ggewogICAgCXRhc2traWxsIC9pbSB0cnVzdGVkaW5zdGFsbGVyLmV4ZSAvZiA+JG51bGwKICAJCX0KICAgICAgICAkc2VydmljZSA9IEdldC1DaW1JbnN0YW5jZSAtQ2xhc3NOYW1lIFdpbjMyX1NlcnZpY2UgLUZpbHRlciAiTmFtZT0nVHJ1c3RlZEluc3RhbGxlciciCiAgICAgICAgJERlZmF1bHRCaW5QYXRoID0gJHNlcnZpY2UuUGF0aE5hbWUKICAJCSR0cnVzdGVkSW5zdGFsbGVyUGF0aCA9ICIkZW52OlN5c3RlbVJvb3Rcc2VydmljaW5nXFRydXN0ZWRJbnN0YWxsZXIuZXhlIgogIAkJaWYgKCREZWZhdWx0QmluUGF0aCAtbmUgJHRydXN0ZWRJbnN0YWxsZXJQYXRoKSB7CiAgICAJJERlZmF1bHRCaW5QYXRoID0gJHRydXN0ZWRJbnN0YWxsZXJQYXRoCiAgCQl9CiAgICAgICAgJGJ5dGVzID0gW1N5c3RlbS5UZXh0LkVuY29kaW5nXTo6VW5pY29kZS5HZXRCeXRlcygkY29tbWFuZCkKICAgICAgICAkYmFzZTY0Q29tbWFuZCA9IFtDb252ZXJ0XTo6VG9CYXNlNjRTdHJpbmcoJGJ5dGVzKQogICAgICAgIHNjLmV4ZSBjb25maWcgVHJ1c3RlZEluc3RhbGxlciBiaW5QYXRoPSAiY21kLmV4ZSAvYyBwb3dlcnNoZWxsLmV4ZSAtZW5jb2RlZGNvbW1hbmQgJGJhc2U2NENvbW1hbmQiIHwgT3V0LU51bGwKICAgICAgICBzYy5leGUgc3RhcnQgVHJ1c3RlZEluc3RhbGxlciB8IE91dC1OdWxsCiAgICAgICAgc2MuZXhlIGNvbmZpZyBUcnVzdGVkSW5zdGFsbGVyIGJpbnBhdGg9ICJgIiREZWZhdWx0QmluUGF0aGAiIiB8IE91dC1OdWxsCiAgICAgICAgdHJ5IHsKICAgIAlTdG9wLVNlcnZpY2UgLU5hbWUgVHJ1c3RlZEluc3RhbGxlciAtRm9yY2UgLUVycm9yQWN0aW9uIFN0b3AgLVdhcm5pbmdBY3Rpb24gU3RvcAogIAkJfQogIAkJY2F0Y2ggewogICAgCXRhc2traWxsIC9pbSB0cnVzdGVkaW5zdGFsbGVyLmV4ZSAvZiA+JG51bGwKICAJCX0KICAgICAgICB9CgpXcml0ZS1Ib3N0ICJTZXJ2aWNlczogRGVmYXVsdC4uLmBuIgoKIyBjcmVhdGUgcmVnIGZpbGUKJFNlcnZpY2VzT24gPSBAJwpXaW5kb3dzIFJlZ2lzdHJ5IEVkaXRvciBWZXJzaW9uIDUuMDAKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQURQU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEFhclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBSlJvdXRlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBTEddCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQXBwSURTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQXBwaW5mb10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBcHBNZ210XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEFwcFJlYWRpbmVzc10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBcHBWQ2xpZW50XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEFwcFhTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQXB4U3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEFzc2lnbmVkQWNjZXNzTWFuYWdlclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBdWRpb0VuZHBvaW50QnVpbGRlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBdWRpb3Nydl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xhdXRvdGltZXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xBeEluc3RTVl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xCY2FzdERWUlVzZXJTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEJERVNWQ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xCRkVdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQklUU10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xCbHVldG9vdGhVc2VyU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xCcm93c2VyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEJyb2tlckluZnJhc3RydWN0dXJlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEJUQUdTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEJ0aEF2Y3RwU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGJ0aHNlcnZdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcY2Ftc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXENhcHR1cmVTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGNiZGhzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ0RQU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXENEUFVzZXJTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ2VydFByb3BTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ2xpcFNWQ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xDbG91ZEJhY2t1cFJlc3RvcmVTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcY2xvdWRpZHN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xDT01TeXNBcHBdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ29uc2VudFV4VXNlclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xDb3JlTWVzc2FnaW5nUmVnaXN0cmFyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXENyZWRlbnRpYWxFbnJvbGxtZW50TWFuYWdlclVzZXJTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ3J5cHRTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcQ3NjU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEY29tTGF1bmNoXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGRjc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGRlZnJhZ3N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEZXZpY2VBc3NvY2lhdGlvbkJyb2tlclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEZXZpY2VBc3NvY2lhdGlvblNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRGV2aWNlSW5zdGFsbF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEZXZpY2VQaWNrZXJVc2VyU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXERldmljZXNGbG93VXNlclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEZXZRdWVyeUJyb2tlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEaGNwXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGRpYWdub3N0aWNzaHViLnN0YW5kYXJkY29sbGVjdG9yLnNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcZGlhZ3N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEaWFnVHJhY2tdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRGlhbG9nQmxvY2tpbmdTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXERpc3BCcm9rZXJEZXNrdG9wU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXERpc3BsYXlFbmhhbmNlbWVudFNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRG1FbnJvbGxtZW50U3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGRtd2FwcHVzaHNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRG5zY2FjaGVdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRG9TdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcZG90M3N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xEUFNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRHNtU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXERzU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXER1c21TdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRWFwSG9zdF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xFRlNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcZW1iZWRkZWRtb2RlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEVudEFwcFN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xFdmVudExvZ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xFdmVudFN5c3RlbV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xGYXhdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcZmRQSG9zdF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xGRFJlc1B1Yl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xmaHN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xGb250Q2FjaGVdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRm9udENhY2hlMy4wLjAuMF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xGcmFtZVNlcnZlck1vbml0b3JdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcRnJhbWVTZXJ2ZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcR2FtZUlucHV0U3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGdwc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEdyYXBoaWNzUGVyZlN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xoaWRzZXJ2XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXGhwYXRjaG1vbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xIdkhvc3RdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcaWNzc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXElLRUVYVF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xJbnN0YWxsU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xJbnZlbnRvcnlTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcaXBobHBzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcSXB4bGF0Q2ZnU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXEtleUlzb10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xLdG1SbV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xMYW5tYW5TZXJ2ZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTGFubWFuV29ya3N0YXRpb25dCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcbGZzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTGljZW5zZU1hbmFnZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcbGx0ZHN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xsbWhvc3RzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXExvY2FsS2RjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXExTTV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xMeHBTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTWFwc0Jyb2tlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xNY21TdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTWNwTWFuYWdlbWVudFNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCjsgW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcU2VydmljZXNcTURDb3JlU3ZjXQo7ICJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTWVzc2FnaW5nU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xtaWRpc3J2XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE1peGVkUmVhbGl0eU9wZW5YUlN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xtcHNzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTVNEVENdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTVNpU0NTSV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xtc2lzZXJ2ZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTXNLZXlib2FyZEZpbHRlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xOYXR1cmFsQXV0aGVudGljYXRpb25dCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTmNhU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE5jYlNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTmNkQXV0b1NldHVwXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE5ldGxvZ29uXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE5ldG1hbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xuZXRwcm9mbV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xOZXRTZXR1cFN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xOZXRUY3BQb3J0U2hhcmluZ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xOZ2NDdG5yU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXE5nY1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xObGFTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcTlBTTVN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xuc2ldCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcT25lU3luY1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xwMnBpbXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xwMnBzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUDlSZHJTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBjYVN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xQZWVyRGlzdFN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xQZW5TZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHBlcmNlcHRpb25zaW11bGF0aW9uXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBlcmZIb3N0XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBob25lU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBpbUluZGV4TWFpbnRlbmFuY2VTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNccGxhXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBsdWdQbGF5XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBOUlBBdXRvUmVnXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFBOUlBzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUG9saWN5QWdlbnRdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUG93ZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUHJpbnREZXZpY2VDb25maWd1cmF0aW9uU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xQcmludE5vdGlmeV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xQcmludFNjYW5Ccm9rZXJTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFByaW50V29ya2Zsb3dVc2VyU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFByb2ZTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUHVzaFRvSW5zdGFsbF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xRV0FWRV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xSYXNBdXRvXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFJhc01hbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xyZWZzZGVkdXBzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUmVtb3RlQWNjZXNzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFJlbW90ZVJlZ2lzdHJ5XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDA0CgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFJldGFpbERlbW9dCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUm1TdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUnBjRXB0TWFwcGVyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFJwY0xvY2F0b3JdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcUnBjU3NdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2FtU3NdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU0NhcmRTdnJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2NEZXZpY2VFbnVtXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNjaGVkdWxlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNDUG9saWN5U3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNEUlNWQ10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzZWNsb2dvbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxTZXJ2aWNlc1xTZWN1cml0eUhlYWx0aFNlcnZpY2VdCjsgIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTRU1nclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTZW5zb3JEYXRhU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTZW5zb3JTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNlbnNyU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNFTlNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCjsgW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ3VycmVudENvbnRyb2xTZXRcU2VydmljZXNcU2Vuc2VdCjsgIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTZXNzaW9uRW52XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNncm1Ccm9rZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2hhcmVkQWNjZXNzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNoYXJlZFJlYWxpdHlTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU2hlbGxIV0RldGVjdGlvbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzaHBhbXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzbXBob3N0XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFNtc1JvdXRlcl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTTk1QVHJhcF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzcGVjdHJ1bV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTcG9vbGVyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHNwcHN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTU0RQU1JWXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHNzaC1hZ2VudF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTc3RwU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFN0YXRlUmVwb3NpdG9yeV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xzdGlzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU3RpU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFN0b3JTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcc3ZzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcc3dwcnZdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcU3lzTWFpbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xTeXN0ZW1FdmVudHNCcm9rZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVGFibGV0SW5wdXRTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFRhcGlTcnZdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVGVybVNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVGV4dElucHV0TWFuYWdlbWVudFNlcnZpY2VdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVGhlbWVzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFRpZXJpbmdFbmdpbmVTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFRpbWVCcm9rZXJTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVG9rZW5Ccm9rZXJdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVHJrV2tzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFRyb3VibGVzaG9vdGluZ1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xUcnVzdGVkSW5zdGFsbGVyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHR6YXV0b3VwZGF0ZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xVZGtVc2VyU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVldkFnZW50U2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwNAoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x1aHNzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDQKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVW1SZHBTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVuaXN0b3JlU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHVwbnBob3N0XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVzZXJEYXRhU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVzZXJNYW5hZ2VyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFVzb1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xWYWNTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcVmF1bHRTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcdmRzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHZtaWNndWVzdGludGVyZmFjZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x2bWljaGVhcnRiZWF0XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHZtaWNrdnBleGNoYW5nZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x2bWljcmR2XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHZtaWNzaHV0ZG93bl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x2bWljdGltZXN5bmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcdm1pY3Ztc2Vzc2lvbl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x2bWljdnNzXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFZTU10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXMzJUaW1lXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdhYVNNZWRpY1N2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXYWxsZXRTZXJ2aWNlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdhcnBKSVRTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd2JlbmdpbmVdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2Jpb1NydmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2Ntc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdjbmNzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2RpU2VydmljZUhvc3RdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2RpU3lzdGVtSG9zdF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxTZXJ2aWNlc1xXZE5pc1N2Y10KOyAiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdlYkNsaWVudF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxTZXJ2aWNlc1x3ZWJ0aHJlYXRkZWZzdmNdCjsgIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKOyBbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDdXJyZW50Q29udHJvbFNldFxTZXJ2aWNlc1x3ZWJ0aHJlYXRkZWZ1c2Vyc3ZjXQo7ICJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2Vjc3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdFUEhPU1RTVkNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd2VyY3Bsc3VwcG9ydF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXZXJTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV0ZEU0Nvbk1nclN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x3aGVzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2lhUnBjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgo7IFtIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXEN1cnJlbnRDb250cm9sU2V0XFNlcnZpY2VzXFdpbkRlZmVuZF0KOyAiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdpbkh0dHBBdXRvUHJveHlTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV2lubWdtdF0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXaW5STV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1x3aXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXbGFuU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdsaWRzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd2xwYXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXTWFuU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXHdtaUFwU3J2XQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdNUE5ldHdvcmtTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd29ya2ZvbGRlcnNzdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV3BjTW9uU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdQREJ1c0VudW1dCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcV3BuU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXcG5Vc2VyU2VydmljZV0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMgoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXU0FJRmFicmljU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgo7IFtIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXEN1cnJlbnRDb250cm9sU2V0XFNlcnZpY2VzXHdzY3N2Y10KOyAiU3RhcnQiPWR3b3JkOjAwMDAwMDAyCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFdTZWFyY2hdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDIKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd3VhdXNlcnZdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcd3VxaXN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xXd2FuU3ZjXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFhibEF1dGhNYW5hZ2VyXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFhibEdhbWVTYXZlXQoiU3RhcnQiPWR3b3JkOjAwMDAwMDAzCgpbSEtFWV9MT0NBTF9NQUNISU5FXFNZU1RFTVxDb250cm9sU2V0MDAxXFNlcnZpY2VzXFhib3hHaXBTdmNdCiJTdGFydCI9ZHdvcmQ6MDAwMDAwMDMKCltIS0VZX0xPQ0FMX01BQ0hJTkVcU1lTVEVNXENvbnRyb2xTZXQwMDFcU2VydmljZXNcWGJveE5ldEFwaVN2Y10KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwoKW0hLRVlfTE9DQUxfTUFDSElORVxTWVNURU1cQ29udHJvbFNldDAwMVxTZXJ2aWNlc1xaVEhFTFBFUl0KIlN0YXJ0Ij1kd29yZDowMDAwMDAwMwonQApTZXQtQ29udGVudCAtUGF0aCAiJGVudjpTeXN0ZW1Sb290XFRlbXBcc2VydmljZXNvbi5yZWciIC1WYWx1ZSAkU2VydmljZXNPbiAtRm9yY2UKCiMgaW1wb3J0IHJlZyBmaWxlIGFzIHRydXN0ZWQKUnVuLVRydXN0ZWQgIlJlZ2VkaXQuZXhlIC9TIGAiJGVudjpTeXN0ZW1Sb290XFRlbXBcc2VydmljZXNvbi5yZWdgIiIKCiMgaW1wb3J0IHJlZyBmaWxlIGFzIGFkbWluClJlZ2VkaXQuZXhlIC9TICIkZW52OlN5c3RlbVJvb3RcVGVtcFxzZXJ2aWNlc29uLnJlZyIKCiMgcmVtb3ZlIHNhZmUgbW9kZSBib290CmNtZCAvYyAiYmNkZWRpdCAvZGVsZXRldmFsdWUge2N1cnJlbnR9IHNhZmVib290ID5udWwgMj4mMSIKCldyaXRlLUhvc3QgIlJlc3RhcnRpbmdgbiIgLUZvcmVncm91bmRDb2xvciBSZWQKCiMgcmVzdGFydApTdGFydC1TbGVlcCAtU2Vjb25kcyA1CnNodXRkb3duIC1yIC10IDAwCg=='
$sync.assets.smtht = 'ICAgICAgICAjIFNDUklQVCBSVU4gQVMgQURNSU4KICAgICAgICBJZiAoIShbU2VjdXJpdHkuUHJpbmNpcGFsLldpbmRvd3NQcmluY2lwYWxdW1NlY3VyaXR5LlByaW5jaXBhbC5XaW5kb3dzSWRlbnRpdHldOjpHZXRDdXJyZW50KCkpLklzSW5Sb2xlKFtTZWN1cml0eS5QcmluY2lwYWwuV2luZG93c0J1aWx0SW5Sb2xlXSJBZG1pbmlzdHJhdG9yIikpCiAgICAgICAge1N0YXJ0LVByb2Nlc3MgUG93ZXJTaGVsbC5leGUgLUFyZ3VtZW50TGlzdCAoIi1Ob1Byb2ZpbGUgLUV4ZWN1dGlvblBvbGljeSBCeXBhc3MgLUZpbGUgYCJ7MH1gIiIgLWYgJFBTQ29tbWFuZFBhdGgpIC1WZXJiIFJ1bkFzCiAgICAgICAgRXhpdH0KICAgICAgICAkSG9zdC5VSS5SYXdVSS5XaW5kb3dUaXRsZSA9ICRteUludm9jYXRpb24uTXlDb21tYW5kLkRlZmluaXRpb24gKyAiIChBZG1pbmlzdHJhdG9yKSIKICAgICAgICAkSG9zdC5VSS5SYXdVSS5CYWNrZ3JvdW5kQ29sb3IgPSAiQmxhY2siCiAgICAgICAgJEhvc3QuUHJpdmF0ZURhdGEuUHJvZ3Jlc3NCYWNrZ3JvdW5kQ29sb3IgPSAiQmxhY2siCiAgICAgICAgJEhvc3QuUHJpdmF0ZURhdGEuUHJvZ3Jlc3NGb3JlZ3JvdW5kQ29sb3IgPSAiV2hpdGUiCiAgICAgICAgQ2xlYXItSG9zdAoKCQlXcml0ZS1Ib3N0ICJURU1QT1JBUklMWSBESVNBQkxFIENQVSBUSFJFQURTIEZPUiBURVNUSU5HIFBFUiBBUFAvR0FNRWBuIgogICAgICAgIFdyaXRlLUhvc3QgIlNNVC9IVDoiCiAgICAgICAgV3JpdGUtSG9zdCAiMS4gT2ZmOiBBbHJlYWR5IFJ1bm5pbmciCiAgICAgICAgV3JpdGUtSG9zdCAiMi4gT2ZmOiBTdGFydHVwYG4iCiAgICAgICAgd2hpbGUgKCR0cnVlKSB7CiAgICAgICAgJGNob2ljZSA9IFJlYWQtSG9zdCAiICIKICAgICAgICBpZiAoJGNob2ljZSAtbWF0Y2ggJ15bMS0yXSQnKSB7CiAgICAgICAgc3dpdGNoICgkY2hvaWNlKSB7CiAgICAgICAgMSB7CgpDbGVhci1Ib3N0CgojIGdldCBudW1iZXIgb2YgbG9naWNhbCBwcm9jZXNzb3JzCiROT0xQID0gKEdldC1XbWlPYmplY3QgV2luMzJfQ29tcHV0ZXJTeXN0ZW0pLk51bWJlck9mTG9naWNhbFByb2Nlc3NvcnMKCiMgY29udmVydCBpbnB1dCB0byBpbnRlZ2VyCiROT0xQID0gW2ludF0kTk9MUAoKIyBjb252ZXJ0IGlucHV0IHRvIGJpbmFyeSB2YWx1ZSB3aXRoIHNtdC9odCBvZmYKJGJpbmFyeSA9ICIiCmZvciAoJGkgPSAwOyAkaSAtbHQgJE5PTFA7ICRpKyspIHsKaWYgKCRpICUgMiAtZXEgMCkgewokYmluYXJ5ICs9ICIwIgp9IGVsc2UgewokYmluYXJ5ICs9ICIxIgp9Cn0KCiMgZW5zdXJlIGJpbmFyeSBsZW5ndGggaXMgbXVsdGlwbGUgb2YgNCBwYWRkaW5nIHdpdGggbGVhZGluZyB6ZXJvcyBpZiBuZWVkZWQKJGJpbmFyeSA9ICRiaW5hcnkuUGFkTGVmdChbbWF0aF06OkNlaWxpbmcoJGJpbmFyeS5MZW5ndGggLyA0KSAqIDQsICIwIikKCiMgY29udmVydCBiaW5hcnkgdG8gaGV4YWRlY2ltYWwKJGhleGFkZWNpbWFsID0gIiIKZm9yICgkaSA9IDA7ICRpIC1sdCAkYmluYXJ5Lkxlbmd0aDsgJGkgKz0gNCkgewokYmluY2h1bmsgPSAkYmluYXJ5LlN1YnN0cmluZygkaSwgNCkKJGhleGFkZWNpbWFsICs9IFtDb252ZXJ0XTo6VG9TdHJpbmcoW0NvbnZlcnRdOjpUb0ludDMyKCRiaW5jaHVuaywgMiksIDE2KQp9CgojIGNvbnZlcnQgaGV4YWRlY2ltYWwgdG8gYW4gaW50ZWdlcgokaGV4YWRlY2ltYWwgPSBbQ29udmVydF06OlRvSW50MzIoJGhleGFkZWNpbWFsLCAxNikKCiMgY29weSBnYW1lIGV4ZSBpZAooR2V0LVByb2Nlc3MgfCBXaGVyZS1PYmplY3QgeyRfLldvcmtpbmdTZXQ2NCAtZ3QgNTAwTUJ9IHwgU2VsZWN0LU9iamVjdCBOYW1lLCBJZCkgfCBGb3JtYXQtVGFibGUgLUF1dG9TaXplCiRleGVpZCA9IFJlYWQtSG9zdCAtUHJvbXB0ICJFTlRFUiBHQU1FIEVYRSBJRCIKCkNsZWFyLUhvc3QKCiMgc2V0IGdhbWUgZXhlIHNtdC9odCBvZmYKJHNtdGh0b2ZmID0gR2V0LVByb2Nlc3MgLUlkICRleGVpZAokc210aHRvZmYuUHJvY2Vzc29yQWZmaW5pdHkgPSAkaGV4YWRlY2ltYWwKCiMgY2hlY2sgbmV3IHZhbHVlCiRyZWxvYWRleGVpZCA9IEdldC1Qcm9jZXNzIC1JZCAkZXhlaWQKCiMgc2hvdyBuZXcgdmFsdWUKJHNob3d2YWx1ZSA9IFtDb252ZXJ0XTo6VG9TdHJpbmcoW2ludF0kcmVsb2FkZXhlaWQuUHJvY2Vzc29yQWZmaW5pdHksIDIpLlBhZExlZnQoJE5PTFAsICcwJykKV3JpdGUtSG9zdCAiSUQgLSAkZXhlaWQgPSAkc2hvd3ZhbHVlYG4iCgpQYXVzZQoKZXhpdAoKICAgICAgICAgIH0KICAgICAgICAyIHsKCkNsZWFyLUhvc3QKCiMgc3RvcCBnYW1lIGxhdW5jaGVycyBydW5uaW5nCiRzdG9wID0gIkJhdHRsZS5uZXQiLCAiQnNnTGF1bmNoZXIiLCAiRUFEZXNrdG9wIiwgIkVwaWNHYW1lc0xhdW5jaGVyIiwgIkdhbGF4eUNsaWVudCIsICJSb2Jsb3hQbGF5ZXJCZXRhIiwgIlJpb3RDbGllbnRTZXJ2aWNlcyIsICJMYXVuY2hlciIsICJzdGVhbSIsICJ1cGMiCiRzdG9wIHwgRm9yRWFjaC1PYmplY3QgeyBTdG9wLVByb2Nlc3MgLU5hbWUgJF8gLUZvcmNlIC1FcnJvckFjdGlvbiBTaWxlbnRseUNvbnRpbnVlIH0KCiMgZ2V0IG51bWJlciBvZiBsb2dpY2FsIHByb2Nlc3NvcnMKJE5PTFAgPSAoR2V0LVdtaU9iamVjdCBXaW4zMl9Db21wdXRlclN5c3RlbSkuTnVtYmVyT2ZMb2dpY2FsUHJvY2Vzc29ycwoKIyBjb252ZXJ0IGlucHV0IHRvIGludGVnZXIKJE5PTFAgPSBbaW50XSROT0xQCgojIGNvbnZlcnQgaW5wdXQgdG8gYmluYXJ5IHZhbHVlIHdpdGggc210L2h0IG9mZgokYmluYXJ5ID0gIiIKZm9yICgkaSA9IDA7ICRpIC1sdCAkTk9MUDsgJGkrKykgewppZiAoJGkgJSAyIC1lcSAwKSB7CiRiaW5hcnkgKz0gIjAiCn0gZWxzZSB7CiRiaW5hcnkgKz0gIjEiCn0KfQoKIyBlbnN1cmUgYmluYXJ5IGxlbmd0aCBpcyBtdWx0aXBsZSBvZiA0IHBhZGRpbmcgd2l0aCBsZWFkaW5nIHplcm9zIGlmIG5lZWRlZAokYmluYXJ5ID0gJGJpbmFyeS5QYWRMZWZ0KFttYXRoXTo6Q2VpbGluZygkYmluYXJ5Lkxlbmd0aCAvIDQpICogNCwgIjAiKQoKIyBjb252ZXJ0IGJpbmFyeSB0byBoZXhhZGVjaW1hbAokaGV4YWRlY2ltYWwgPSAiIgpmb3IgKCRpID0gMDsgJGkgLWx0ICRiaW5hcnkuTGVuZ3RoOyAkaSArPSA0KSB7CiRiaW5jaHVuayA9ICRiaW5hcnkuU3Vic3RyaW5nKCRpLCA0KQokaGV4YWRlY2ltYWwgKz0gW0NvbnZlcnRdOjpUb1N0cmluZyhbQ29udmVydF06OlRvSW50MzIoJGJpbmNodW5rLCAyKSwgMTYpCn0KCiMgc2VsZWN0IGdhbWUgbGF1bmNoZXIgbG5rIG9yIGV4ZQpXcml0ZS1Ib3N0ICJTRUxFQ1QgTEFVTkNIRVIvR0FNRS9TSE9SVENVVC9FWEU6IgpBZGQtVHlwZSAtQXNzZW1ibHlOYW1lIFN5c3RlbS5XaW5kb3dzLkZvcm1zCiREaWFsb2cgPSBOZXctT2JqZWN0IFN5c3RlbS5XaW5kb3dzLkZvcm1zLk9wZW5GaWxlRGlhbG9nCiREaWFsb2cuRmlsdGVyID0gIkFsbCBGaWxlcyAoKi4qKXwqLioiCiREaWFsb2cuU2hvd0RpYWxvZygpIHwgT3V0LU51bGwKJGdhbWVsYXVuY2hlciA9ICREaWFsb2cuRmlsZU5hbWUKCkNsZWFyLUhvc3QKCiMgc3RhcnQgZ2FtZSBsYXVuY2hlciBsbmsgb3IgZXhlIHdpdGggc210L2h0IG9mZgpjbWQgL2MgInN0YXJ0IGAiYCIgL2FmZmluaXR5ICRoZXhhZGVjaW1hbCBgIiRnYW1lbGF1bmNoZXJgIiIKV3JpdGUtSG9zdCAiR0VUVElORyBWQUxVRS4uLiIKClN0YXJ0LVNsZWVwIC1TZWNvbmRzIDEwCgojIGNvbnZlcnQgZGlyZWN0b3J5IHRvIGZpbGUgbmFtZSB3aXRob3V0IGV4ZQokZ2FtZWxhdW5jaGVyID0gW1N5c3RlbS5JTy5QYXRoXTo6R2V0RmlsZU5hbWVXaXRob3V0RXh0ZW5zaW9uKCRnYW1lbGF1bmNoZXIpCgojIGNoZWNrIHZhbHVlCiRyZWxvYWRnYW1lbGF1bmNoZXIgPSAoR2V0LVByb2Nlc3MgLU5hbWUgIiRnYW1lbGF1bmNoZXIiKS5Qcm9jZXNzb3JBZmZpbml0eQoKIyBjb252ZXJ0IHZhbHVlCiRzaG93dmFsdWUgPSBbQ29udmVydF06OlRvU3RyaW5nKFtpbnRdJHJlbG9hZGdhbWVsYXVuY2hlciwgMikKCkNsZWFyLUhvc3QKCiMgc2hvdyBuZXcgdmFsdWUKJE5PTFBsZW5ndGggPSAkTk9MUAokc2hvd3ZhbHVlID0gJHNob3d2YWx1ZS5QYWRMZWZ0KCROT0xQbGVuZ3RoLCAiMCIpCldyaXRlLUhvc3QgIkVYRSAtICRnYW1lbGF1bmNoZXIgPSAkc2hvd3ZhbHVlYG4iCgpQYXVzZQoKZXhpdAoKICAgICAgICAgIH0KICAgICAgICB9IH0gZWxzZSB7IFdyaXRlLUhvc3QgIkludmFsaWQgaW5wdXQuIFBsZWFzZSBzZWxlY3QgYSB2YWxpZCBvcHRpb24gKDEtMikuIiB9IH0='

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

    <!-- WindowChrome: custom title bar area, keep native caption buttons -->
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
                                <StackPanel x:Name="NavContent" Orientation="Horizontal" VerticalAlignment="Center" Margin="10,0,0,0">
                                    <TextBlock x:Name="Icon"
                                               FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                               FontSize="15"
                                               Text="{TemplateBinding Tag}"
                                               Foreground="#888888"
                                               VerticalAlignment="Center"
                                               TextAlignment="Center"
                                               Width="22"/>
                                    <TextBlock x:Name="NavText" Text="{TemplateBinding Content}"
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

        <!-- ── Caption button (minimize / maximize) ─────────────────────────── -->
        <Style x:Key="CaptionBtn" TargetType="Button">
            <Setter Property="Width" Value="46"/>
            <Setter Property="Height" Value="40"/>
            <Setter Property="Foreground" Value="#C6C6CA"/>
            <Setter Property="WindowChrome.IsHitTestVisibleInChrome" Value="True"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="cb" Background="Transparent">
                            <TextBlock Text="{TemplateBinding Content}" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                       FontSize="10" Foreground="{TemplateBinding Foreground}"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="cb" Property="Background" Value="#20FFFFFF"/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <!-- ── Caption close button (red hover) ─────────────────────────────── -->
        <Style x:Key="CaptionCloseBtn" TargetType="Button" BasedOn="{StaticResource CaptionBtn}">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="cb" Background="Transparent">
                            <TextBlock x:Name="ct" Text="{TemplateBinding Content}" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                       FontSize="10" Foreground="{TemplateBinding Foreground}"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="cb" Property="Background" Value="#E0142A"/>
                                <Setter TargetName="ct" Property="Foreground" Value="White"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <!-- ── Hamburger button ─────────────────────────────────────────────── -->
        <Style x:Key="HamburgerBtn" TargetType="Button">
            <Setter Property="Width" Value="40"/>
            <Setter Property="Height" Value="34"/>
            <Setter Property="HorizontalAlignment" Value="Left"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="hb" Background="Transparent" CornerRadius="6">
                            <TextBlock Text="&#xE700;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets"
                                       FontSize="15" Foreground="#C6C6CA" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="hb" Property="Background" Value="#12FFFFFF"/></Trigger>
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
            <Grid>
                <!-- Brand: logo + accent dot + AKARI TOOL wordmark -->
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="16,0,0,0">
                    <Image Name="TitleLogo" Height="24" VerticalAlignment="Center" Margin="0,0,8,0"
                           RenderOptions.BitmapScalingMode="HighQuality"/>
                    <Ellipse Width="5" Height="5" VerticalAlignment="Center" Margin="0,0,10,0" Fill="#E0142A"/>
                    <TextBlock FontSize="11" FontWeight="Bold" VerticalAlignment="Center"
                               FontFamily="Segoe UI Variable Display, Segoe UI">
                        <Run Text="AKARI " Foreground="White"/><Run Text="TOOL" Foreground="#E03535"/>
                    </TextBlock>
                </StackPanel>
            </Grid>
        </Border>

        <!-- ── Body: sidebar + content ─────────────────────────────────────── -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition x:Name="SidebarCol" Width="210"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Sidebar -->
            <Grid Grid.Column="0" Background="{StaticResource SidebarBg}">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <StackPanel Grid.Row="0" Margin="0,8,0,0">
                    <!-- Hamburger + search -->
                    <Button Name="NavHamburger" Style="{StaticResource HamburgerBtn}" Margin="10,0,0,6"/>
                    <Border Name="SidebarSearch" Background="#18FFFFFF" BorderBrush="#1FFFFFFF" BorderThickness="1" CornerRadius="5" Height="32" Margin="10,0,10,10">
                        <Grid Margin="10,0,8,0">
                            <TextBox Name="SearchBox" Background="Transparent" BorderThickness="0" Foreground="White" CaretBrush="White"
                                     VerticalContentAlignment="Center" FontSize="12.5" VerticalAlignment="Center" Padding="0"/>
                            <TextBlock Name="SearchPlaceholder" Text="Search all settings..." FontSize="12.5" Foreground="#8A8A90"
                                       VerticalAlignment="Center" IsHitTestVisible="False"/>
                            <TextBlock Text="&#xE721;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="13"
                                       Foreground="#8A8A90" HorizontalAlignment="Right" VerticalAlignment="Center"/>
                        </Grid>
                    </Border>
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
                <StackPanel Name="SidebarFooter" Grid.Row="1" Margin="16,12">
                    <TextBlock Text="Akari Tool  v1.0" FontSize="11" Foreground="#444444"/>
                    <TextBlock Text="by isleap · Fr33thy scripts" FontSize="11" Foreground="#444444"/>
                </StackPanel>
            </Grid>

            <!-- ══ CONTENT AREA (rounded panel — curved divide from the sidebar) ══ -->
            <Border Grid.Column="1" Background="#18FFFFFF" CornerRadius="14,0,0,0" ClipToBounds="True">
            <Grid Background="Transparent">

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

            </Grid>
            </Border><!-- end content area -->
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

# ── Build a searchable index of every card across all tabs (for global search) ─
function Get-ElementText([System.Windows.DependencyObject]$el) {
    $sb = New-Object System.Text.StringBuilder
    $stack = New-Object System.Collections.Stack
    $stack.Push($el)
    while ($stack.Count -gt 0) {
        $n = $stack.Pop()
        if ($n -is [System.Windows.Controls.TextBlock]) { [void]$sb.Append($n.Text); [void]$sb.Append(" ") }
        foreach ($c in [System.Windows.LogicalTreeHelper]::GetChildren($n)) {
            if ($c -is [System.Windows.DependencyObject]) { $stack.Push($c) }
        }
    }
    $sb.ToString()
}
$sync.CardIndex = New-Object System.Collections.ArrayList
foreach ($p in $panels) {
    $pl = $sync[$p]
    if ($pl -and $pl.Content -and $pl.Content.Children) {
        foreach ($child in $pl.Content.Children) {
            if ($child -is [System.Windows.Controls.Border]) {
                [void]$sync.CardIndex.Add([pscustomobject]@{ Panel = $p; Card = $child; Text = (Get-ElementText $child).ToLowerInvariant() })
            }
        }
    }
}

# ── Hamburger: toggle compact / expanded sidebar ─────────────────────────────
$sync.SidebarExpanded = $true
$navNames = @("NavCheck","NavRefresh","NavSetup","NavInstallers","NavGraphics","NavWindows","NavHardware","NavAdvanced")
if ($sync.NavHamburger) {
    $sync.NavHamburger.Add_Click({
        $sync.SidebarExpanded = -not $sync.SidebarExpanded
        $expanded = $sync.SidebarExpanded
        $col = $sync.window.FindName("SidebarCol")
        if ($col) { $col.Width = if ($expanded) { New-Object System.Windows.GridLength 210 } else { New-Object System.Windows.GridLength 56 } }
        $vis = if ($expanded) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        if ($sync.SidebarSearch) { $sync.SidebarSearch.Visibility = $vis }
        if ($sync.SidebarFooter) { $sync.SidebarFooter.Visibility = $vis }
        foreach ($n in $navNames) {
            $rb = $sync[$n]
            if ($rb -and $rb.Template) {
                $t = $rb.Template.FindName("NavText", $rb)
                $c = $rb.Template.FindName("NavContent", $rb)
                if ($t) { $t.Visibility = $vis }
                if ($c) {
                    if ($expanded) {
                        $c.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
                        $c.Margin = New-Object System.Windows.Thickness(10,0,0,0)
                    } else {
                        $c.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center
                        $c.Margin = New-Object System.Windows.Thickness(0)
                    }
                }
            }
        }
    }.GetNewClosure())
}

# ── Search: GLOBAL filter across every tab; jumps to the first tab with hits ──
if ($sync.SearchBox -and $sync.SearchPlaceholder) {
    $sync.SearchBox.Add_TextChanged({
        $q = $sync.SearchBox.Text
        $sync.SearchPlaceholder.Visibility =
            if ([string]::IsNullOrEmpty($q)) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        $ql = $q.ToLowerInvariant()
        $matchPanels = New-Object System.Collections.Generic.HashSet[string]
        foreach ($item in $sync.CardIndex) {
            $match = ($ql -eq "") -or $item.Text.Contains($ql)
            $item.Card.Visibility = if ($match) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
            if ($match -and $ql -ne "") { [void]$matchPanels.Add($item.Panel) }
        }
        if ($ql -ne "") {
            $cur = $panels | Where-Object { $sync[$_].Visibility -eq [System.Windows.Visibility]::Visible } | Select-Object -First 1
            if (-not $matchPanels.Contains($cur)) {
                $target = $panels | Where-Object { $matchPanels.Contains($_) } | Select-Object -First 1
                if ($target) {
                    $navKey = ($navMap.GetEnumerator() | Where-Object { $_.Value -eq $target } | Select-Object -First 1).Key
                    if ($navKey -and $sync[$navKey]) { $sync[$navKey].IsChecked = $true }
                }
            }
        }
    }.GetNewClosure())
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


