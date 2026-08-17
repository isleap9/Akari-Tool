# System File Checker Scan
Write-Host "Running SFC scan — this may take a few minutes..."
$result = sfc /scannow
$result | ForEach-Object { Write-Host $_ }
Write-Host "SFC scan complete."
