# Visual Effects - Set to Best Performance
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/display/

$regKeys = @(
    @{Path="HKCU:\Control Panel\Desktop";                                              Name="DragFullWindows";        Value="0";  Type="String"},
    @{Path="HKCU:\Control Panel\Desktop";                                              Name="MenuShowDelay";          Value="200";Type="String"},
    @{Path="HKCU:\Control Panel\Desktop\WindowMetrics";                                Name="MinAnimate";             Value="0";  Type="String"},
    @{Path="HKCU:\Control Panel\Keyboard";                                             Name="KeyboardDelay";          Value=0;    Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="ListviewAlphaSelect";    Value=0;    Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="ListviewShadow";         Value=0;    Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="TaskbarAnimations";      Value=0;    Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";   Name="VisualFXSetting";        Value=3;    Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\DWM";                                     Name="EnableAeroPeek";         Value=0;    Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="TaskbarMn";              Value=0;    Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";        Name="ShowTaskViewButton";     Value=0;    Type="DWord"},
    @{Path="HKCU:\Software\Microsoft\Windows\CurrentVersion\Search";                   Name="SearchboxTaskbarMode";   Value=0;    Type="DWord"}
)

foreach ($key in $regKeys) {
    If (!(Test-Path $key.Path)) { New-Item -Path $key.Path -Force | Out-Null }
    Set-ItemProperty -Path $key.Path -Name $key.Name -Value $key.Value -Type $key.Type -Force
}

Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "UserPreferencesMask" -Type Binary -Value ([byte[]](144,18,3,128,16,0,0,0))

Write-Host "Visual effects set to best performance."