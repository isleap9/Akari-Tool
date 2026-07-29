# Delivery Optimization - Disable
# https://winutil.christitus.com/dev/tweaks/essential-tweaks/deliveryoptimization/
# Stops Windows from using your bandwidth to upload updates to other PCs
# on the internet or local network (peer-to-peer Windows Update sharing).

$path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }

# DODownloadMode 0 = disabled (no peer-to-peer sharing)
Set-ItemProperty -Path $path -Name "DODownloadMode" -Value 0 -Type DWord -Force

Write-Host "Delivery Optimization disabled."