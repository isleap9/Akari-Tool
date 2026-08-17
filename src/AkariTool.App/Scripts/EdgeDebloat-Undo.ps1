# Enable Microsoft Edge Telemetry - Undo
$regKeys = @(
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge"; Name="MetricsReportingEnabled"; Value=1; Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge"; Name="SendSiteInfoToImproveServices"; Value=1; Type="DWord"}
)
foreach ($key in $regKeys) {
    If (!(Test-Path $key.Path)) { New-Item -Path $key.Path -Force | Out-Null }
    Set-ItemProperty -Path $key.Path -Name $key.Name -Value $key.Value -Type $key.Type -Force
}
Write-Host "Microsoft Edge telemetry re-enabled."
