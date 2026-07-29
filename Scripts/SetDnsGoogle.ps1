# Set DNS to Google (8.8.8.8 / 8.8.4.4)
Write-Host "Setting DNS servers to Google..."
$adapters = Get-NetAdapter | Where-Object { $_.Status -eq "Up" }
foreach ($adapter in $adapters) {
    Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses ("8.8.8.8","8.8.4.4")
    Write-Host "  Updated: $($adapter.Name)"
}
Write-Host "DNS set to Google (8.8.8.8 / 8.8.4.4)."
