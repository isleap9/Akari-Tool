# Enable End Task with Right Click on Taskbar
$path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings"
If (!(Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "TaskbarEndTask" -Value 1 -Type DWord -Force
Write-Host "End Task on right-click enabled."