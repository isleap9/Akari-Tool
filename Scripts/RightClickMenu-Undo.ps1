# Right-Click Menu - Restore Windows 11 Layout
# Undo script for RightClickMenu.ps1

Remove-Item -Path "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}" `
            -Recurse -Confirm:$false -Force -ErrorAction SilentlyContinue

Write-Host "Restarting Explorer..."
Stop-Process -Name "explorer" -Force

Write-Host "Windows 11 right-click menu restored."