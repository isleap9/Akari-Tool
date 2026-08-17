# Background Apps - Re-enable
# Undo script for DisableBGApps.ps1

$path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "GlobalUserDisabled" -Value 0 -Type DWord -Force

Write-Host "Background apps re-enabled."