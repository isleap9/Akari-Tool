# File Explorer Home and Gallery - Disable
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/removehomeandgallery/
# Removes Home and Gallery from the File Explorer sidebar and sets This PC as the default view.

$regKeys = @(
    # Hides the Home section from Explorer sidebar
    @{Path="HKCU:\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}"; Name="System.IsPinnedToNameSpaceTree"; Value=0; Type="DWord"},
    # Hides the Gallery section from Explorer sidebar
    @{Path="HKCU:\Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}"; Name="System.IsPinnedToNameSpaceTree"; Value=0; Type="DWord"},
    # Sets Explorer to open at This PC (1) instead of Home (2)
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";    Name="LaunchTo";                      Value=1; Type="DWord"}
)

foreach ($key in $regKeys) {
    If (!(Test-Path $key.Path)) { New-Item -Path $key.Path -Force | Out-Null }
    Set-ItemProperty -Path $key.Path -Name $key.Name -Value $key.Value -Type $key.Type -Force
}

Write-Host "Home and Gallery removed. Explorer now opens at This PC."