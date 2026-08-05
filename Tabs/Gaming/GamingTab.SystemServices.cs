using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public partial class GamingTab
    {
        // ── Service dropdown helper ───────────────────────────────────────────

        private TweakDefinition ServiceDropdown(
            string id, string name, string description,
            string serviceKey,
            int recommendedStart, int defaultStart,
            string? disabledWarning = null, string? manualWarning = null)
        {
            var options = new List<TweakDropdownOption>();

            // Recommended is Disabled (4) for some, Manual (3) for others
            if (recommendedStart == 4 && defaultStart == 4)
            {
                options.Add(new TweakDropdownOption("Disabled", 4, IsRecommended: true, IsDefault: true));
                options.Add(new TweakDropdownOption("Manual",   3));
                options.Add(new TweakDropdownOption("Automatic", 2));
            }
            else if (recommendedStart == 4)
            {
                options.Add(new TweakDropdownOption("Disabled",  4, IsRecommended: true));
                options.Add(new TweakDropdownOption("Manual",    3));
                options.Add(new TweakDropdownOption("Automatic", 2, IsDefault: defaultStart == 2));
                if (defaultStart == 3) options[1] = new TweakDropdownOption("Manual", 3, IsDefault: true);
            }
            else // recommended == 3
            {
                options.Add(new TweakDropdownOption("Disabled",  4));
                options.Add(new TweakDropdownOption("Manual",    3, IsRecommended: true, IsDefault: defaultStart == 3));
                options.Add(new TweakDropdownOption("Automatic", 2, IsDefault: defaultStart == 2));
            }

            // Attach warnings to the matching start values (Winhance-style
            // option-specific warnings — confirmed by the user before apply)
            for (int i = 0; i < options.Count; i++)
            {
                string? w = (int)options[i].Value switch
                {
                    4 => disabledWarning,
                    3 => manualWarning,
                    _ => null
                };
                if (w != null) options[i] = options[i] with { Warning = w };
            }

            var opts = options.ToArray();

            return new TweakDefinition
            {
                Id          = id,
                Name        = name,
                Description = description,
                InputKind   = TweakInputKind.Dropdown,
                Options     = opts,
                ReadCurrentIndex = () =>
                {
                    var v = ReadDword(RegistryHive.LocalMachine,
                        $@"SYSTEM\CurrentControlSet\Services\{serviceKey}", "Start");
                    if (!v.HasValue) return opts.ToList().FindIndex(o => (int)o.Value == defaultStart);
                    return opts.ToList().FindIndex(o => (int)o.Value == (int)v.Value) is int i && i >= 0 ? i : 0;
                },
                ApplyIndex = idx =>
                {
                    if (idx < 0 || idx >= opts.Length) return;
                    int val = (int)opts[idx].Value;
                    Registry.SetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{serviceKey}",
                        "Start", val, RegistryValueKind.DWord);
                    Log($"{name}: set to {opts[idx].Label}. Restart to apply.");
                }
            };
        }
        // ══════════════════════════════════════════════════════════════════════
        // SYSTEM SERVICES
        // ══════════════════════════════════════════════════════════════════════

        private void BuildSystemServices(StackPanel panel)
        {
            // Service dropdowns: ServiceDropdown(id, name, desc, serviceKey, recommendedStart, defaultStart)
            // Recommended 4 = Disabled recommended, Recommended 3 = Manual recommended
            // Default 2 = Automatic, Default 3 = Manual, Default 4 = Disabled
            var defs = new[]
            {
                ServiceDropdown("gaming-sysmain-service",
                    "SysMain Service (Superfetch)",
                    "Preload frequently used applications into RAM for faster launch times. Automatic is recommended for HDD or mixed-storage systems; Manual or Disabled for SSD-only systems",
                    "SysMain", recommendedStart: 4, defaultStart: 2,
                    disabledWarning: "Disabling SysMain on systems with a traditional hard drive (HDD) can noticeably reduce responsiveness and slow app launches. Recommended only for SSD-only systems."),
                ServiceDropdown("gaming-windows-search-service",
                    "Windows Search Indexing Service",
                    "Indexes files and folders for faster search results. Disabling reduces background CPU and disk activity but breaks Outlook search and makes Start Menu and File Explorer search slow or unreliable",
                    "WSearch", recommendedStart: 3, defaultStart: 2,
                    disabledWarning: "Disabling Windows Search stops file content indexing. Outlook search, Start Menu search, and File Explorer search will become slow or return no results until re-enabled."),
                ServiceDropdown("gaming-print-spooler-service",
                    "Print Spooler Service",
                    "Manages print jobs sent to printers. If you don't use a printer, set to Manual or Disabled to free up system resources",
                    "Spooler", recommendedStart: 3, defaultStart: 2),
                ServiceDropdown("gaming-telemetry-service",
                    "Connected User Experiences and Telemetry",
                    "Sends usage data and diagnostics to Microsoft. Setting to Manual or Disabled reduces background network and CPU usage",
                    "DiagTrack", recommendedStart: 3, defaultStart: 2),
                ServiceDropdown("gaming-error-reporting-service",
                    "Windows Error Reporting Service",
                    "Collects and sends crash data to Microsoft. Disabling prevents crash reporting and reduces network traffic",
                    "WerSvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-geolocation-service",
                    "Geolocation Service",
                    "Tracks your physical location for apps and services. Disabling improves privacy and prevents location tracking",
                    "lfsvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-retail-demo-service",
                    "Retail Demo Service",
                    "Controls device activity when in retail demo mode. Safe to disable for personal computers",
                    "RetailDemo", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-insider-service",
                    "Windows Insider Service",
                    "Manages Windows Insider Program features and preview builds. Safe to disable if you're not in the Insider Program",
                    "wisvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-phone-service",
                    "Phone Service",
                    "Manages telephony state on the device. Safe to disable if you don't use phone connectivity features",
                    "PhoneSvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-wallet-service",
                    "Wallet Service",
                    "Provides wallet functionality for payment and NFC scenarios. Safe to disable if you don't use Microsoft Wallet",
                    "WalletService", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-maps-broker-service",
                    "Downloaded Maps Manager",
                    "Provides access to downloaded maps for applications. Set to Manual to allow map access when needed",
                    "MapsBroker", recommendedStart: 3, defaultStart: 2),
                ServiceDropdown("gaming-fax-service",
                    "Fax Service",
                    "Enables sending and receiving faxes. Safe to disable for most users as fax functionality is rarely used",
                    "Fax", recommendedStart: 4, defaultStart: 4),
                ServiceDropdown("gaming-wmp-network-service",
                    "Windows Media Player Network Sharing",
                    "Shares Windows Media Player libraries to other networked players and media devices",
                    "WMPNetworkSvc", recommendedStart: 4, defaultStart: 3),
                ServiceDropdown("gaming-mixed-reality-service",
                    "Windows Mixed Reality OpenXR Service",
                    "Runs OpenXR applications on Windows Mixed Reality devices. Safe to disable if you don't use VR or AR headsets",
                    "MixedRealityOpenXRSvc", recommendedStart: 3, defaultStart: 4),
                ServiceDropdown("gaming-mobile-hotspot-service",
                    "Windows Mobile Hotspot Service",
                    "Provides ability to share internet connection with other devices",
                    "icssvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-sms-router-service",
                    "SMS Router Service",
                    "Routes SMS messages according to rules. Safe to disable if you don't use SMS features on your PC",
                    "SmsRouter", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-parental-controls-service",
                    "Parental Controls Service",
                    "Enables parental controls and family safety features. Safe to disable if you don't use parental controls",
                    "WpcMonSvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-payments-nfc-service",
                    "Payments and NFC/SE Manager",
                    "Manages payments and Near Field Communication secure elements. Safe to disable if you don't use NFC payments",
                    "SEMgrSvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-biometric-service",
                    "Windows Biometric Service",
                    "Enables fingerprint and facial recognition login via Windows Hello. Safe to disable on desktop systems without biometric hardware",
                    "WbioSrvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-remote-access-manager",
                    "Remote Access Connection Manager",
                    "Manages VPN and dial-up connections. Set to Manual to reduce background activity while keeping VPN available",
                    "RasMan", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-remote-access-auto",
                    "Remote Access Auto Connection Manager",
                    "Automatically connects to remote networks when programs reference remote resources",
                    "RasAuto", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-remote-desktop-services",
                    "Remote Desktop Services",
                    "Allows users to connect interactively to a remote computer",
                    "TermService", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-remote-desktop-configuration",
                    "Remote Desktop Configuration",
                    "Manages Remote Desktop Services and Remote Desktop related configurations",
                    "SessionEnv", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-compatibility-assistant-service",
                    "Program Compatibility Assistant Service",
                    "Monitors programs for compatibility issues and suggests fixes. Disabling prevents compatibility prompts",
                    "PcaSvc", recommendedStart: 3, defaultStart: 2),
                ServiceDropdown("gaming-ai-fabric-service",
                    "Windows AI Fabric Service",
                    "Windows AI Fabric Service (WSAIFabricSvc) manages AI workloads. Disable if you don't use Windows AI features",
                    "WSAIFabricSvc", recommendedStart: 4, defaultStart: 2),
                ServiceDropdown("gaming-sensor-monitoring-service",
                    "Sensor Monitoring Service",
                    "Monitors various sensors like ambient light and orientation. Safe to disable on desktop systems without sensor hardware",
                    "SensrSvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-sensor-data-service",
                    "Sensor Data Service",
                    "Delivers data from a variety of sensors to applications. Safe to disable on desktop systems without sensor hardware",
                    "SensorDataService", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-telephony-service",
                    "Telephony Service",
                    "Manages telephony (TAPI) for Phone Link audio relay, modems, fax, and VoIP softphones. Leave at Manual unless you use no telephony software",
                    "TapiSrv", recommendedStart: 3, defaultStart: 3,
                    disabledWarning: "Disabling Telephony breaks Phone Link audio relay, fax software, dial-up modems, and VoIP softphones (e.g. 3CX, Cisco Jabber)."),
                ServiceDropdown("gaming-connected-devices-platform-service",
                    "Connected Devices Platform Service",
                    "Enables cross-device experiences like phone linking and nearby sharing. Note: can break Windows Night Light. Use Automatic if you use Night Light.",
                    "CDPSvc", recommendedStart: 3, defaultStart: 2,
                    disabledWarning: "Disabling the Connected Devices Platform can break Windows Night Light and cross-device features (Phone Link, Nearby Sharing, clipboard sync). Manual keeps these working — it effectively auto-starts with your session."),
                ServiceDropdown("gaming-smart-card-services",
                    "Smart Card Services",
                    "Enables smart card reader functionality. Safe to disable if you don't use physical smart cards.",
                    "SCardSvr", recommendedStart: 4, defaultStart: 3),
                ServiceDropdown("gaming-spot-verifier-service",
                    "Spot Verifier Service",
                    "Verifies potential file system corruptions. Set to Manual to allow verification when needed.",
                    "svsvc", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-remote-desktop-port-redirector",
                    "Remote Desktop Services UserMode Port Redirector",
                    "Allows local device redirection for Remote Desktop connections. Safe to disable if you don't use Remote Desktop.",
                    "UmRdpService", recommendedStart: 3, defaultStart: 3),
                ServiceDropdown("gaming-touch-keyboard-service",
                    "Touch Keyboard and Handwriting Panel Service",
                    "Manages Windows touch keyboard, pen/stylus, and handwriting panel. Safe to disable on desktop systems without touch input.",
                    "TabletInputService", recommendedStart: 4, defaultStart: 3),
                new TweakDefinition
                {
                    Id               = "gaming-input-app-preload",
                    Name             = "Input App Preload",
                    Description      = "Preload the Windows Input Experience (touch keyboard, emoji panel) at sign-in. Disable alongside the Touch Keyboard service to stop it running in the background",
                    IsPreference     = true,
                    // Winhance models IsInputAppPreloadEnabled only inside the
                    // gaming-touch-keyboard-service combo: Disabled(recommended) → 0,
                    // Manual(default)/Automatic → 1. Registry leg itself carries null
                    // Recommended/Default → states derived from the combo flags.
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () =>
                    {
                        try
                        {
                            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\input");
                            return k?.GetValue("IsInputAppPreloadEnabled") is int v ? v != 0 : true;
                        }
                        catch { return null; }
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\input",
                            "IsInputAppPreloadEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Input App Preload {(on ? "enabled" : "disabled")}.");
                    }
                },
                ServiceDropdown("gaming-xbox-auth-manager",
                    "Xbox Live Auth Manager",
                    "Provides authentication for Xbox Live. Safe to disable if you don't use Xbox Game Pass or Microsoft Store games.",
                    "XblAuthManager", recommendedStart: 4, defaultStart: 3,
                    disabledWarning: "Disabling will prevent Xbox Game Pass and Microsoft Store games from signing in or launching."),
                ServiceDropdown("gaming-xbox-game-save",
                    "Xbox Live Game Save",
                    "Syncs game saves to Xbox Live cloud. Only needed for Xbox Game Pass and Microsoft Store games with cloud saves.",
                    "XblGameSave", recommendedStart: 4, defaultStart: 3),
                ServiceDropdown("gaming-xbox-networking",
                    "Xbox Live Networking Service",
                    "Supports Xbox Live multiplayer networking. Not needed for Steam or Epic games.",
                    "XboxNetApiSvc", recommendedStart: 4, defaultStart: 3),
                ServiceDropdown("gaming-midi-service",
                    "Windows MIDI Service",
                    "Routes MIDI data for connected musical instruments and audio interfaces. Safe to disable if you don't use MIDI hardware; set to Manual to allow it to start on demand",
                    "midisrv", recommendedStart: 3, defaultStart: 3),
            };

            AddSection(panel, "System Services", defs);
        }
    }
}
