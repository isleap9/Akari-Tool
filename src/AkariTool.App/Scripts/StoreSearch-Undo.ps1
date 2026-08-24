# Enable Store Search - Undo
$regKeys = @(
    @{Path="HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search"; Name="BingSearchEnabled"; Value=1; Type="DWord"}
)
foreach ($key in $regKeys) {
    If (!(Test-Path $key.Path)) { New-Item -Path $key.Path -Force | Out-Null }
    Set-ItemProperty -Path $key.Path -Name $key.Name -Value $key.Value -Type $key.Type -Force
}
Write-Host "Microsoft Store search enabled."
