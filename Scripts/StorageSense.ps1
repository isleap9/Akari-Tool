# Storage Sense — Disable
# Mirrors Chris Titus WinUtil WPFTweaksStorageSense

$ErrorActionPreference = 'SilentlyContinue'

$path = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }

# Disable Storage Sense entirely
Set-ItemProperty -Path $path -Name "01" -Value 0 -Type DWord -Force

# Disable all sub-options too
Set-ItemProperty -Path $path -Name "04" -Value 0 -Type DWord -Force  # Delete temp files
Set-ItemProperty -Path $path -Name "08" -Value 0 -Type DWord -Force  # Recycle Bin cleanup
Set-ItemProperty -Path $path -Name "32" -Value 0 -Type DWord -Force  # Downloads folder cleanup
Set-ItemProperty -Path $path -Name "256" -Value 0 -Type DWord -Force # OneDrive dehydration

Write-Host "[STORAGE SENSE] Storage Sense disabled." -ForegroundColor Green
