# Set non-essential services to Manual
$services = @("DiagTrack","dmwappushservice","RetailDemo","RemoteRegistry","XblAuthManager","XblGameSave","XboxGipSvc","XboxNetApiSvc","edgeupdate","edgeupdatem","MapsBroker","PcaSvc","StorSvc","UsoSvc","WpnService","camsvc")
foreach ($svc in $services) {
    $s = Get-Service -Name $svc -ErrorAction SilentlyContinue
    if ($s) {
        Set-Service -Name $svc -StartupType Manual -ErrorAction SilentlyContinue
        Write-Host "Set $svc to Manual"
    }
}
# Disable telemetry service entirely
Set-Service -Name "DiagTrack" -StartupType Disabled -ErrorAction SilentlyContinue
Write-Host "Services configured."