# Reset Services to Default Startup Type - Undo
$services = @(
    @{Name="DiagTrack"; StartType="Automatic"},
    @{Name="dmwappushservice"; StartType="Manual"},
    @{Name="RetailDemo"; StartType="Disabled"},
    @{Name="RemoteRegistry"; StartType="Manual"},
    @{Name="XblAuthManager"; StartType="Manual"},
    @{Name="XblGameSave"; StartType="Manual"},
    @{Name="XboxGipSvc"; StartType="Manual"},
    @{Name="XboxNetApiSvc"; StartType="Manual"},
    @{Name="edgeupdate"; StartType="Automatic"},
    @{Name="edgeupdatem"; StartType="Manual"},
    @{Name="MapsBroker"; StartType="Automatic"},
    @{Name="PcaSvc"; StartType="Automatic"},
    @{Name="StorSvc"; StartType="Manual"},
    @{Name="UsoSvc"; StartType="Automatic"},
    @{Name="WpnService"; StartType="Automatic"},
    @{Name="camsvc"; StartType="Manual"}
)

foreach ($svc in $services) {
    $service = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
    if ($service) {
        Set-Service -Name $svc.Name -StartupType $svc.StartType -ErrorAction SilentlyContinue
        Write-Host "Reset $($svc.Name) to $($svc.StartType)"
    }
}
Write-Host "Services reset to default startup types."
