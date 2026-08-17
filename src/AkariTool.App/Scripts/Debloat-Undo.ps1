# Debloat-Undo.ps1
# Appx package removal is permanent — packages cannot be restored without
# a Windows repair, reset, or reinstallation from the Microsoft Store.
# This script informs the user of that limitation.

Write-Host "[DEBLOAT] Appx package removal cannot be undone automatically." -ForegroundColor Yellow
Write-Host "[DEBLOAT] To restore removed apps, visit the Microsoft Store" -ForegroundColor Yellow
Write-Host "[DEBLOAT] and reinstall them individually." -ForegroundColor Yellow
Write-Host "[DEBLOAT] Alternatively, run: DISM /Online /Cleanup-Image /RestoreHealth" -ForegroundColor Yellow
Write-Host "[DEBLOAT] to restore provisioned packages from Windows Update." -ForegroundColor Yellow
