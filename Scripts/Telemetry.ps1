# Disable Windows Telemetry
$path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "AllowTelemetry" -Value 0 -Type DWord -Force
Set-ItemProperty -Path $path -Name "MaxTelemetryAllowed" -Value 0 -Type DWord -Force
Set-Service -Name "DiagTrack" -StartupType Disabled -ErrorAction SilentlyContinue
Stop-Service -Name "DiagTrack" -Force -ErrorAction SilentlyContinue
Write-Host "Telemetry disabled."