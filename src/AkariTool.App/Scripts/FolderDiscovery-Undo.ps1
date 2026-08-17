# Enable Folder Discovery (Explorer Auto-changing folder view layouts)
$regKeys = @(
    @{Path="HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced"; Name="FolderContentsMode"; Value=0; Type="DWord"}
)
foreach ($key in $regKeys) {
    If (!(Test-Path $key.Path)) { New-Item -Path $key.Path -Force | Out-Null }
    Remove-ItemProperty -Path $key.Path -Name $key.Name -Force -ErrorAction SilentlyContinue
}
Write-Host "Folder Discovery enabled (default restored)."
