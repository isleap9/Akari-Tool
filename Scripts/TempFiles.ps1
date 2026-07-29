# Remove Temporary Files
$paths = @("$env:TEMP\*", "$env:SystemRoot\Temp\*", "$env:SystemRoot\Prefetch\*")
foreach ($p in $paths) {
    Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Temporary files removed."