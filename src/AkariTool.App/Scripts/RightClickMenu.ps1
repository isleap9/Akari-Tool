# Right-Click Menu - Restore Classic Layout
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/rightclickmenu/
# Restores the old Windows 10 right-click context menu in File Explorer.
# Explorer is restarted for the change to take effect immediately.

New-Item -Path "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}" `
         -Name "InprocServer32" -Force -Value "" | Out-Null

Write-Host "Restarting Explorer..."
Stop-Process -Name "explorer" -Force

Write-Host "Classic right-click menu restored."