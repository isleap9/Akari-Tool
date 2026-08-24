# Set Time to UTC (Dual Boot)
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/utc/

$path = "HKLM:\SYSTEM\CurrentControlSet\Control\TimeZoneInformation"
Set-ItemProperty -Path $path -Name "RealTimeIsUniversal" -Value 1 -Type QWord -Force

Write-Host "Time set to UTC. Windows will now use UTC hardware clock, same as Linux."