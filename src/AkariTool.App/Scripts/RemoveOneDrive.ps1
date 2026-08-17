# Microsoft OneDrive - Remove
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/removeonedrive/

# Deny permission to delete OneDrive user files during uninstall
icacls $Env:OneDrive /deny "Administrators:(D,DC)"

Write-Host "Uninstalling OneDrive..."
Start-Process 'C:\Windows\System32\OneDriveSetup.exe' -ArgumentList '/uninstall' -Wait

# Stop processes that lock OneDrive files
Write-Host "Removing leftover OneDrive files..."
Stop-Process -Name FileCoAuth, Explorer -ErrorAction SilentlyContinue

# Remove leftover folders
Remove-Item "$Env:LocalAppData\Microsoft\OneDrive" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "C:\ProgramData\Microsoft OneDrive" -Recurse -Force -ErrorAction SilentlyContinue

# Restore permission after uninstall
icacls $Env:OneDrive /grant "Administrators:(D,DC)"

# Disable the OneDrive sync service
Set-Service -Name OneSyncSvc -StartupType Disabled -ErrorAction SilentlyContinue

Write-Host "OneDrive removed."