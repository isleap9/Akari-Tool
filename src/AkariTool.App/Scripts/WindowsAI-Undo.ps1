# Enable Windows AI - Undo
# Remove registry blocks on AI components
$paths = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
    "HKLM:\SOFTWARE\Policies\WindowsNotepad"
)

foreach ($path in $paths) {
    if (Test-Path $path) {
        Remove-ItemProperty -Path $path -Name "SettingsPageVisibility" -Force -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $path -Name "DisableAIFeatures" -Force -ErrorAction SilentlyContinue
    }
}

# Attempt to restore Copilot and CoreAI
$Appx = (Get-AppxPackage MicrosoftWindows.Client.CoreAI -AllUsers).PackageFullName
if ($Appx) {
    Add-AppxPackage -Package $Appx -ErrorAction SilentlyContinue
}

# Enable AI service
Set-Service -Name WSAIFabricSvc -StartupType Automatic -ErrorAction SilentlyContinue
Start-Service -Name WSAIFabricSvc -ErrorAction SilentlyContinue

Write-Host "Windows AI components re-enabled."
