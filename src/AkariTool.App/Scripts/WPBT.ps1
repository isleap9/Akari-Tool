# Disable Windows Platform Binary Table (WinUtil: WPFTweaksWPBT)
# WPBT lets the firmware/OEM execute a vendor binary at every boot before the
# user logs in. Disabling it blocks that execution path.
$path = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "DisableWpbtExecution" -Value 1 -Type DWord -Force
Write-Host "WPBT execution disabled. Reboot required."
