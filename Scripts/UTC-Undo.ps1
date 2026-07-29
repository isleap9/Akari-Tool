# Set Time Back to Local (Undo UTC) - Undo
# This sets the system time back to Local Time mode for Windows
# Useful when you want to revert from UTC to Local Time

$regPath = "HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation"
If (!(Test-Path $regPath)) { New-Item -Path $regPath -Force | Out-Null }

# RealTimeIsUniversal = 0 means use Local Time
Set-ItemProperty -Path $regPath -Name "RealTimeIsUniversal" -Value 0 -Type DWord -Force

Write-Host "System time set back to Local Time."
Write-Host "You may need to restart your computer for changes to take effect."
