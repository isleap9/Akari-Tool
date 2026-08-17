# Set DNS to Cloudflare (1.1.1.1 / 1.0.0.1)
Write-Host "Setting DNS servers to Cloudflare..."
$adapters = Get-NetAdapter | Where-Object { $_.Status -eq "Up" }
foreach ($adapter in $adapters) {
    Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses ("1.1.1.1","1.0.0.1")
    Write-Host "  Updated: $($adapter.Name)"
}
Write-Host "DNS set to Cloudflare (1.1.1.1 / 1.0.0.1)."
