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