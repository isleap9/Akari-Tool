# Disable PowerShell 7 Telemetry
[System.Environment]::SetEnvironmentVariable("POWERSHELL_TELEMETRY_OPTOUT", "1", "Machine")
Write-Host "PowerShell 7 Telemetry disabled."