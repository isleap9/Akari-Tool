# Visual Effects - Restore to Default - Undo
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/display/

$regKeys = @(
    @{Path="HKCU:\Control Panel\Desktop";                                              Name="DragFullWindows";        Value="1";   Type="String"},
    @{Path="HKCU:\Control Panel\Desktop";                                              Name="MenuShowDelay";          Value="400"; Type="String"},
    @{Path="HKCU:\Control Panel\Desktop\WindowMetrics";                                Name="MinAnimate";             Value="1";   Type="String"},
    @{Path="HKCU:\Control Panel\Keyboard";                                             Name="KeyboardDelay";          Value=1;     Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="ListviewAlphaSelect";    Value=1;     Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="ListviewShadow";         Value=1;     Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="TaskbarAnimations";      Value=1;     Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";   Name="VisualFXSetting";        Value=2;     Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\DWM";                                     Name="EnableAeroPeek";         Value=1;     Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="TaskbarMn";              Value=1;     Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="ShowTaskViewButton";     Value=1;     Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Search";                   Name="SearchboxTaskbarMode";   Value=2;     Type="DWord"}
)

foreach ($key in $regKeys) {
    If (!(Test-Path $key.Path)) { New-Item -Path $key.Path -Force | Out-Null }
    Set-ItemProperty -Path $key.Path -Name $key.Name -Value $key.Value -Type $key.Type -Force
}

Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "UserPreferencesMask" -Type Binary -Value ([byte[]](144,18,3,128,16,0,0,0))

Write-Host "Visual effects restored to default."
