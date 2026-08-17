# Microsoft Edge - Remove
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/removeedge/

# Find Edge setup.exe
$EdgeBase = "C:\Program Files (x86)\Microsoft\Edge\Application"
$Setup = Get-ChildItem "$EdgeBase\*\Installer\setup.exe" -ErrorAction SilentlyContinue | Select-Object -Last 1

if (-not $Setup) {
    Write-Host "[ERROR] Edge setup.exe not found. Edge may already be removed."
    exit
}

# Unblock the uninstaller via registry
$RegPath = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\ClientState\{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}"
If (Test-Path $RegPath) {
    Set-ItemProperty -Path $RegPath -Name "experiment_control_labels" -Value "" -Force -ErrorAction SilentlyContinue
}

# Remove experiment flags that block uninstall
$RegPath2 = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate"
If (Test-Path $RegPath2) {
    Remove-ItemProperty -Path $RegPath2 -Name "IsEdgeStableUninstallBlocked" -Force -ErrorAction SilentlyContinue
}

Write-Host "Running Edge uninstaller..."
Start-Process -FilePath $Setup.FullName -ArgumentList "--uninstall --system-level --verbose-logging --force-uninstall" -Wait -NoNewWindow

Write-Host "Microsoft Edge removed. A reboot may be required."