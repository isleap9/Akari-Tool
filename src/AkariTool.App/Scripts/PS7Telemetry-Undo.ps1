# Enable PowerShell 7 Telemetry - Undo
Remove-Item -Path "$PSHOME\pwsh.exe.blocktel" -Force -ErrorAction SilentlyContinue
if ($PROFILE) {
    $profileContent = Get-Content $PROFILE -ErrorAction SilentlyContinue
    if ($profileContent -like "*pwsh.exe.blocktel*") {
        $newContent = $profileContent -replace "(?s).*pwsh\.exe\.blocktel.*", ""
        Set-Content -Path $PROFILE -Value $newContent -Force
    }
}
Write-Host "PowerShell 7 telemetry re-enabled (opt-in)."
