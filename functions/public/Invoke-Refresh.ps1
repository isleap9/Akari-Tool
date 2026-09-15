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
