# Rebuild Icon Cache
Write-Host "Stopping Explorer..."
taskkill /f /im explorer.exe | Out-Null

Write-Host "Deleting icon cache files..."
$cachePaths = @(
    "$env:LOCALAPPDATA\IconCache.db",
    "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\iconcache*"
)
foreach ($p in $cachePaths) {
    Remove-Item -Path $p -Force -ErrorAction SilentlyContinue
}

Write-Host "Restarting Explorer..."
Start-Process explorer.exe
Write-Host "Icon cache rebuilt."
