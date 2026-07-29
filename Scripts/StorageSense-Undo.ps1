# Storage Sense — Undo (Re-enable)

$ErrorActionPreference = 'SilentlyContinue'

$path = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }

Set-ItemProperty -Path $path -Name "01" -Value 1 -Type DWord -Force
Set-ItemProperty -Path $path -Name "04" -Value 1 -Type DWord -Force
Set-ItemProperty -Path $path -Name "08" -Value 1 -Type DWord -Force
Set-ItemProperty -Path $path -Name "32" -Value 0 -Type DWord -Force
Set-ItemProperty -Path $path -Name "256" -Value 1 -Type DWord -Force

Write-Host "[STORAGE SENSE] Storage Sense re-enabled." -ForegroundColor Green
