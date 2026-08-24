# Enable End Task with Right Click on Taskbar - Undo
# This removes the setting, returning to default behavior
$path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings"
If (Test-Path $path) {
    Remove-ItemProperty -Path $path -Name "TaskbarEndTask" -Force -ErrorAction SilentlyContinue
}
Write-Host "End Task setting removed (default restored)."
