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
