# DISM Health Check and Repair
Write-Host "Checking Windows image health..."
DISM /Online /Cleanup-Image /CheckHealth
Write-Host ""
Write-Host "Running ScanHealth..."
DISM /Online /Cleanup-Image /ScanHealth
Write-Host ""
Write-Host "Running RestoreHealth — this may take several minutes..."
DISM /Online /Cleanup-Image /RestoreHealth
Write-Host "DISM repair complete."
