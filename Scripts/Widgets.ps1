# Remove Widgets
Stop-Process -Name Widgets -ErrorAction SilentlyContinue
Get-AppxPackage Microsoft.WidgetsPlatformRuntime -AllUsers | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
Get-AppxPackage MicrosoftWindows.Client.WebExperience -AllUsers | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
Write-Host "Widgets removed."