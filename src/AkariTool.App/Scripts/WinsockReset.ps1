# Reset Winsock and TCP/IP Stack
Write-Host "Resetting Winsock catalog..."
netsh winsock reset
Write-Host ""
Write-Host "Resetting TCP/IP stack..."
netsh int ip reset
Write-Host ""
Write-Host "Releasing and renewing IP address..."
ipconfig /release
ipconfig /renew
Write-Host ""
Write-Host "Network stack reset complete. A reboot is recommended."
