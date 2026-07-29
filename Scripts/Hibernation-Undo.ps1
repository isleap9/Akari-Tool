# Hibernation — Undo (Re-enable)
# Mirrors Chris Titus WinUtil undo for WPFTweaksHibernation

$ErrorActionPreference = 'SilentlyContinue'

# Re-enable via powercfg
powercfg.exe /hibernate on

# Registry — Session Manager Power key
$powerPath = "HKLM:\System\CurrentControlSet\Control\Session Manager\Power"
If (!(Test-Path $powerPath)) { New-Item -Path $powerPath -Force | Out-Null }
Set-ItemProperty -Path $powerPath -Name "HibernateEnabled" -Value 1 -Type DWord -Force

# Registry — FlyoutMenuSettings
$flyoutPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings"
If (!(Test-Path $flyoutPath)) { New-Item -Path $flyoutPath -Force | Out-Null }
Set-ItemProperty -Path $flyoutPath -Name "ShowHibernateOption" -Value 1 -Type DWord -Force

Write-Host "[HIBERNATION] Hibernation re-enabled." -ForegroundColor Green
