# Restore Widgets - Undo
# Attempt to reinstall Widgets packages
$packages = @("Microsoft.WidgetsPlatformRuntime", "MicrosoftWindows.Client.WebExperience")

foreach ($pkg in $packages) {
    Write-Host "Attempting to restore $pkg..."
    Get-AppxPackage -AllUsers -Name $pkg | Add-AppxPackage -ErrorAction SilentlyContinue
}

# Enable Widgets service
Set-Service -Name "WidgetService" -StartupType Automatic -ErrorAction SilentlyContinue
Start-Service -Name "WidgetService" -ErrorAction SilentlyContinue

Write-Host "Widgets restoration attempted."
