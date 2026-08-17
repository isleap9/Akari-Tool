# Delivery Optimization - Re-enable
# Undo script for DeliveryOptimization.ps1

$path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization"

# Remove the policy entry to restore Windows default behavior
If (Test-Path $path) {
    Remove-ItemProperty -Path $path -Name "DODownloadMode" -Force -ErrorAction SilentlyContinue
}

Write-Host "Delivery Optimization restored to default."