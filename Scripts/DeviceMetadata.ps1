# Prevent Device Companion Apps (WinUtil: WPFTweaksPreventDeviceMetadataFromNetwork)
# Blocks Windows from pulling device metadata/companion apps from the network
# when new hardware is plugged in (e.g. vendor ads when connecting a monitor).
$path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Device Metadata"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "PreventDeviceMetadataFromNetwork" -Value 1 -Type DWord -Force
Write-Host "Device companion apps / metadata retrieval disabled."
