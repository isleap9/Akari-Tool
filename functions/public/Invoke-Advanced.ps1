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
