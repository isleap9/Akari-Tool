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
