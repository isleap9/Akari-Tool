# Disable Windows AI - Extracted from CTT WinUtil
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/windowsai/

# Registry tweaks
$path1 = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"
If (!(Test-Path $path1)) { New-Item -Path $path1 -Force | Out-Null }
Set-ItemProperty -Path $path1 -Name "SettingsPageVisibility" -Value "hide:aicomponents" -Type String -Force

$path2 = "HKLM:\SOFTWARE\Policies\WindowsNotepad"
If (!(Test-Path $path2)) { New-Item -Path $path2 -Force | Out-Null }
Set-ItemProperty -Path $path2 -Name "DisableAIFeatures" -Value 1 -Type DWord -Force

# Remove Copilot and CoreAI packages
$Appx = (Get-AppxPackage MicrosoftWindows.Client.CoreAI).PackageFullName
$Sid = (Get-LocalUser $Env:UserName).Sid.Value

If ($Appx) {
    New-Item "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\EndOfLife\$Sid\$Appx" -Force | Out-Null
    Remove-AppxPackage $Appx -ErrorAction SilentlyContinue
}

Get-AppxPackage -AllUsers *Copilot* | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
Get-AppxPackage -AllUsers Microsoft.MicrosoftOfficeHub | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue

# Disable AI service and Recall feature
Set-Service -Name WSAIFabricSvc -StartupType Disabled -ErrorAction SilentlyContinue
Disable-WindowsOptionalFeature -FeatureName Recall -Online -ErrorAction SilentlyContinue

Write-Host "Windows AI disabled."