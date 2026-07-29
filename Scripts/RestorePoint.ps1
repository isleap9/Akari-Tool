# Extracted from CTT WinUtil - Create Restore Point
# https://winutil.christitus.com/dev/tweaks/essential-tweaks/restorepoint/

# Enable System Restore on C: if not already enabled
Enable-ComputerRestore -Drive "C:\"

# Set restore point creation frequency to 0 (allow multiple per day)
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore" `
    -Name "SystemRestorePointCreationFrequency" -Value 0 -Type DWord -Force

# Create the restore point
Checkpoint-Computer -Description "IsleapTool Restore Point" `
    -RestorePointType "MODIFY_SETTINGS"

Write-Host "Restore point created successfully."