# Prevent Device Companion Apps - Undo
# WinUtil OriginalValue is <RemoveEntry>, so restore by deleting the policy value.
$path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Device Metadata"
If (Test-Path $path) {
    Remove-ItemProperty -Path $path -Name "PreventDeviceMetadataFromNetwork" -Force -ErrorAction SilentlyContinue
}
Write-Host "Device companion apps / metadata retrieval restored to Windows default."
