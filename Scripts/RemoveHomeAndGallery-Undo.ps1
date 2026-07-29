# File Explorer Home and Gallery - Restore
# Undo script for RemoveHomeAndGallery.ps1

# Restore Home to sidebar
$path1 = "HKCU:\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}"
If (Test-Path $path1) {
    Remove-ItemProperty -Path $path1 -Name "System.IsPinnedToNameSpaceTree" -Force -ErrorAction SilentlyContinue
}

# Restore Gallery to sidebar
$path2 = "HKCU:\Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}"
If (Test-Path $path2) {
    Remove-ItemProperty -Path $path2 -Name "System.IsPinnedToNameSpaceTree" -Force -ErrorAction SilentlyContinue
}

# Restore Explorer default open location to Home
$path3 = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"
Remove-ItemProperty -Path $path3 -Name "LaunchTo" -Force -ErrorAction SilentlyContinue

Write-Host "Home and Gallery restored to default."