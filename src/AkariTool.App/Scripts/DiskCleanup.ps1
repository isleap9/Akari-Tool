# Extracted from CTT WinUtil - Disk Cleanup
# https://winutil.christitus.com/dev/tweaks/essential-tweaks/diskcleanup/

Write-Host "Running Disk Cleanup on C:..."
Start-Process -FilePath "cleanmgr.exe" -ArgumentList "/d C: /VERYLOWDISK" -Wait

Write-Host "Cleaning up Windows Update components..."
Start-Process -FilePath "Dism.exe" -ArgumentList "/online /Cleanup-Image /StartComponentCleanup /ResetBase" -Wait -NoNewWindow

Write-Host "Disk cleanup complete."