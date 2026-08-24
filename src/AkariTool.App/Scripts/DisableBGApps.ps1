# Background Apps - Disable
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/disablebgapps/
# Disables all Microsoft Store apps from running in the background globally.

$path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "GlobalUserDisabled" -Value 1 -Type DWord -Force

Write-Host "Background apps disabled."