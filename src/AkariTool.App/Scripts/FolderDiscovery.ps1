# Disable File Explorer Automatic Folder Discovery
$path = "HKCU:\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell"
Set-ItemProperty -Path "$path\Bags\AllFolders\Shell" -Name "FolderType" -Value "NotSpecified" -Force -ErrorAction SilentlyContinue
Write-Host "Automatic Folder Discovery disabled."