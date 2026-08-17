# Hibernation — Disable
# Mirrors Chris Titus WinUtil WPFTweaksHibernation

$ErrorActionPreference = 'SilentlyContinue'

# Disable via powercfg (most reliable method)
powercfg.exe /hibernate off

# Registry — Session Manager Power key
$powerPath = "HKLM:\System\CurrentControlSet\Control\Session Manager\Power"
If (!(Test-Path $powerPath)) { New-Item -Path $powerPath -Force | Out-Null }
Set-ItemProperty -Path $powerPath -Name "HibernateEnabled" -Value 0 -Type DWord -Force

# Registry — FlyoutMenuSettings (may not exist — create it first)
$flyoutPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings"
If (!(Test-Path $flyoutPath)) { New-Item -Path $flyoutPath -Force | Out-Null }
Set-ItemProperty -Path $flyoutPath -Name "ShowHibernateOption" -Value 0 -Type DWord -Force

Write-Host "[HIBERNATION] Hibernation disabled." -ForegroundColor Green
