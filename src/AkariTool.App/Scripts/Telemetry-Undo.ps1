# Enable Windows Telemetry - Undo
$path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "AllowTelemetry" -Value 3 -Type DWord -Force
Set-ItemProperty -Path $path -Name "MaxTelemetryAllowed" -Value 3 -Type DWord -Force
Set-Service -Name "DiagTrack" -StartupType Automatic -ErrorAction SilentlyContinue
Start-Service -Name "DiagTrack" -ErrorAction SilentlyContinue
Write-Host "Telemetry enabled."
