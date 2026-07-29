# Microsoft Edge - Debloat
# https://winutil.christitus.com/dev/tweaks/z--advanced-tweaks---caution/edgedebloat/

$regKeys = @(
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\EdgeUpdate";                      Name="CreateDesktopShortcutDefault";           Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="PersonalizationReportingEnabled";        Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallBlocklist";  Name="1";                                      Value="ofefcgjbeghpigppfmkologfjadafddi";  Type="String"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="ShowRecommendationsEnabled";             Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="HideFirstRunExperience";                 Value=1;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="UserFeedbackAllowed";                    Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="ConfigureDoNotTrack";                    Value=1;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="AlternateErrorPagesEnabled";             Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="EdgeCollectionsEnabled";                 Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="EdgeShoppingAssistantEnabled";           Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="MicrosoftEdgeInsiderPromotionEnabled";   Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="ShowMicrosoftRewards";                   Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="WebWidgetAllowed";                       Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="DiagnosticData";                         Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="EdgeAssetDeliveryServiceEnabled";        Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="WalletDonationEnabled";                  Value=0;                                    Type="DWord"},
    @{Path="HKLM:\SOFTWARE\Policies\Microsoft\Edge";                            Name="DefaultBrowserSettingsCampaignEnabled";  Value=0;                                    Type="DWord"}
)

foreach ($key in $regKeys) {
    If (!(Test-Path $key.Path)) { New-Item -Path $key.Path -Force | Out-Null }
    Set-ItemProperty -Path $key.Path -Name $key.Name -Value $key.Value -Type $key.Type -Force
}

Write-Host "Microsoft Edge debloated."