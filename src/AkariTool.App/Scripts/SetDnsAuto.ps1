# Reset DNS to Automatic (DHCP)
Write-Host "Resetting DNS to automatic (DHCP)..."
$adapters = Get-NetAdapter | Where-Object { $_.Status -eq "Up" }
foreach ($adapter in $adapters) {
    Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ResetServerAddresses
    Write-Host "  Reset: $($adapter.Name)"
}
ipconfig /flushdns
Write-Host "DNS reset to automatic."
