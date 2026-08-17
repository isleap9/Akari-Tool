# Disable WPBT - Undo
# WinUtil OriginalValue is <RemoveEntry>, so restore by deleting the value.
$path = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager"
If (Test-Path $path) {
    Remove-ItemProperty -Path $path -Name "DisableWpbtExecution" -Force -ErrorAction SilentlyContinue
}
Write-Host "WPBT execution restored to Windows default. Reboot required."
