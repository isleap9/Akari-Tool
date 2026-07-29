# Microsoft OneDrive - Reinstall
# Undo script for RemoveOneDrive.ps1

Write-Host "Reinstalling OneDrive..."
winget install Microsoft.OneDrive --source winget --accept-source-agreements --accept-package-agreements

# Re-enable the OneDrive sync service
Set-Service -Name OneSyncSvc -StartupType Automatic -ErrorAction SilentlyContinue

Write-Host "OneDrive reinstalled."