# Disable Microsoft Store search results in Start Menu
icacls "$Env:LocalAppData\Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalState\store.db" /deny Everyone:F
Write-Host "Microsoft Store search results disabled."