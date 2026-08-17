# Set DNS to Quad9 (9.9.9.9 / 149.112.112.112)
Write-Host "Setting DNS servers to Quad9..."
$adapters = Get-NetAdapter | Where-Object { $_.Status -eq "Up" }
foreach ($adapter in $adapters) {
    Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses ("9.9.9.9","149.112.112.112")
    Write-Host "  Updated: $($adapter.Name)"
}
Write-Host "DNS set to Quad9 (9.9.9.9 / 149.112.112.112)."
