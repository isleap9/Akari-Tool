using Microsoft.Win32;
using AkariTool.Services;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs.Privacy
{
    public static partial class PrivacyTweaks
    {
        // ══════════════════════════════════════════════════════════════════════
        // WINDOWS AI
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] WindowsAI(Action<string> Log)
        {
            const string WinAI  = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
            const string WinAIS = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI";

            // Disable pattern: EnabledValue=[0,null], DisabledValue=[1]  → ON=absent, OFF=set to 1
            bool? ReadDisable(string v) { var val = ReadDword(RegistryHive.LocalMachine, WinAIS, v); return val.HasValue ? val != 1 : true; }
            void  ApplyDisable(string v, bool on) { if (on) { using var k = Registry.LocalMachine.OpenSubKey(WinAIS, true); k?.DeleteValue(v, false); } else Registry.SetValue(WinAI, v, 1, RegistryValueKind.DWord); }

            // Allow pattern: EnabledValue=[1,null], DisabledValue=[0]  → ON=absent, OFF=set to 0
            bool? ReadAllow(string v)  { var val = ReadDword(RegistryHive.LocalMachine, WinAIS, v); return val.HasValue ? val != 0 : true; }
            void  ApplyAllow(string v, bool on)  { if (on) { using var k = Registry.LocalMachine.OpenSubKey(WinAIS, true); k?.DeleteValue(v, false); } else Registry.SetValue(WinAI, v, 0, RegistryValueKind.DWord); }

            return new[]
            {
                new TweakDefinition { Id="privacy-turn-off-copilot", Name="Windows Copilot",
                    Description="Controls whether Windows Copilot is available system-wide",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{
                        // Apply writes to ...\Windows\WindowsCopilot (both hives) — read the same key,
                        // not the section-wide WindowsAI helper. Disabled when either hive has value 1.
                        var lm = ReadDword(RegistryHive.LocalMachine, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
                        var cu = ReadDword(RegistryHive.CurrentUser,  @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
                        return lm != 1 && cu != 1;
                    },
                    Apply=on=>{ if(on){foreach(var h in new[]{Registry.CurrentUser,Registry.LocalMachine})h.OpenSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot",true)?.DeleteValue("TurnOffWindowsCopilot",false);}else{foreach(var h in new[]{"HKEY_CURRENT_USER","HKEY_LOCAL_MACHINE"})Registry.SetValue($@"{h}\Software\Policies\Microsoft\Windows\WindowsCopilot","TurnOffWindowsCopilot",1,RegistryValueKind.DWord);}Log($"Windows Copilot {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-ai-data-analysis", Name="AI Data Analysis",
                    Description="Controls whether Windows AI can analyze user data for personalization and recommendations",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadDisable("DisableAIDataAnalysis"),
                    Apply=on=>{ ApplyDisable("DisableAIDataAnalysis",on); Log($"AI Data Analysis {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-block-recall-enablement", Name="Recall Enablement",
                    Description="Controls whether Windows Recall can be enabled via policy",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadAllow("AllowRecallEnablement"),
                    Apply=on=>{ ApplyAllow("AllowRecallEnablement",on); Log($"Recall Enablement {(on?"allowed":"blocked")}."); }
                },
                new TweakDefinition { Id="privacy-disable-recall-snapshots", Name="Recall Saving Snapshots",
                    Description="Allows Windows Recall to save screenshots of your activity for later recall",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadDisable("TurnOffSavingSnapshots"),
                    Apply=on=>{ ApplyDisable("TurnOffSavingSnapshots",on); Log($"Recall Snapshots {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-click-to-do", Name="Click to Do",
                    Description="Controls whether the Click to Do AI feature is available in Windows",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadDisable("DisableClickToDo"),
                    Apply=on=>{ ApplyDisable("DisableClickToDo",on); Log($"Click to Do {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-settings-agent", Name="AI Settings Agent",
                    Description="Controls whether the AI-powered Settings Agent is available in Windows",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadDisable("DisableSettingsAgent"),
                    Apply=on=>{ ApplyDisable("DisableSettingsAgent",on); Log($"AI Settings Agent {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-agent-connectors", Name="AI Agent Connectors",
                    Description="Controls whether AI agents can use connectors to access external services",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadDisable("DisableAgentConnectors"),
                    Apply=on=>{ ApplyDisable("DisableAgentConnectors",on); Log($"AI Agent Connectors {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-agent-workspaces", Name="AI Agent Workspaces",
                    Description="Controls whether AI Agent Workspaces are available in Windows",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadDisable("DisableAgentWorkspaces"),
                    Apply=on=>{ ApplyDisable("DisableAgentWorkspaces",on); Log($"AI Agent Workspaces {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-remote-agent-connectors", Name="Remote AI Agent Connectors",
                    Description="Controls whether AI agents can use remote connectors to access remote services",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadDisable("DisableRemoteAgentConnectors"),
                    Apply=on=>{ ApplyDisable("DisableRemoteAgentConnectors",on); Log($"Remote AI Agent Connectors {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-copilot-hardware-key", Name="Copilot Hardware Key",
                    Description="Controls whether the dedicated Copilot key on keyboards opens Copilot",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadString(RegistryHive.LocalMachine,@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CopilotKey","SetCopilotHardwareKey"); return v==null; },
                    Apply=on=>{ const string sub=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\CopilotKey"; if(on){using var k=Registry.LocalMachine.OpenSubKey(sub,true);k?.DeleteValue("SetCopilotHardwareKey",false);}else Registry.SetValue(@"HKEY_LOCAL_MACHINE\"+sub,"SetCopilotHardwareKey","",RegistryValueKind.String); Log($"Copilot Hardware Key {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-copilot-runtime", Name="Copilot Runtime",
                    Description="Controls whether the Copilot runtime is allowed to run via policy",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadAllow("AllowCopilotRuntime"),
                    Apply=on=>{ ApplyAllow("AllowCopilotRuntime",on); Log($"Copilot Runtime {(on?"allowed":"blocked")}."); }
                },
                new TweakDefinition { Id="privacy-copilot-unavailable", Name="Copilot Availability",
                    Description="Controls whether Copilot is available in the Windows Shell",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Microsoft\Windows\Shell\Copilot","IsCopilotAvailable"); return v.HasValue?v!=0:true; },
                    Apply=on=>{ const string sub=@"Software\Microsoft\Windows\Shell\Copilot"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);k?.DeleteValue("IsCopilotAvailable",false);}else Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"IsCopilotAvailable",0,RegistryValueKind.DWord); Log($"Copilot Availability {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-bing-chat", Name="Bing Chat Eligibility",
                    Description="Controls whether the user is eligible for Bing Chat and Copilot in Search",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Microsoft\Windows\Shell\Copilot\BingChat","IsUserEligible"); return v.HasValue?v!=0:true; },
                    Apply=on=>{ const string sub=@"Software\Microsoft\Windows\Shell\Copilot\BingChat"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);k?.DeleteValue("IsUserEligible",false);}else Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"IsUserEligible",0,RegistryValueKind.DWord); Log($"Bing Chat Eligibility {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-deny-generative-ai-access", Name="Generative AI Access",
                    Description="Controls whether apps can access the generative AI capability on your device",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\generativeAI"),
                    Apply=on=>{ WriteConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\generativeAI",on); Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy","LetAppsAccessGenerativeAI",on?0:2,RegistryValueKind.DWord); Log($"Generative AI Access {(on?"allowed":"denied")}."); }
                },
                new TweakDefinition { Id="privacy-deny-system-ai-models", Name="System AI Models Access",
                    Description="Controls whether apps can access system AI models on your device",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>ReadConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels"),
                    Apply=on=>{ WriteConsentStore(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels",on); Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy","LetAppsAccessSystemAIModels",on?0:2,RegistryValueKind.DWord); Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\systemAIModels","RecordUsageData",on?1:0,RegistryValueKind.DWord); Log($"System AI Models Access {(on?"allowed":"denied")}."); }
                },
                new TweakDefinition { Id="privacy-deny-copilot-microphone", Name="Copilot Microphone Access",
                    Description="Controls whether Copilot and Office Hub apps have microphone permission",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadString(RegistryHive.CurrentUser,@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\Microsoft.Copilot_8wekyb3d8bbwe","Value"); return v==null||v!="Deny"; },
                    Apply=on=>{ foreach(var app in new[]{"Microsoft.Copilot_8wekyb3d8bbwe","Microsoft.MicrosoftOfficeHub_8wekyb3d8bbwe"}) Registry.SetValue($@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\{app}","Value",on?"Allow":"Deny",RegistryValueKind.String); Log($"Copilot Microphone Access {(on?"allowed":"denied")}."); }
                },
                new TweakDefinition { Id="privacy-disable-input-insights", Name="Input Insights",
                    Description="Controls whether Windows Input Insights can track typing patterns and provide suggestions",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Microsoft\input\Settings","InsightsEnabled"); return v.HasValue?v==1:true; },
                    Apply=on=>{ Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\input\Settings","InsightsEnabled",on?1:0,RegistryValueKind.DWord); Log($"Input Insights {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-copilot-nudges", Name="Copilot Nudges",
                    Description="Controls whether Copilot promotional nudges and background task notifications are shown",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced","ShowCopilotNudges"); return v.HasValue?v!=0:true; },
                    Apply=on=>{ const string sub=@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);k?.DeleteValue("ShowCopilotNudges",false);}else Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"ShowCopilotNudges",0,RegistryValueKind.DWord); Log($"Copilot Nudges {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-consumer-ai-content", Name="AI Consumer Content",
                    Description="Controls whether AI-driven consumer account content recommendations are shown",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.LocalMachine,@"SOFTWARE\Policies\Microsoft\Windows\CloudContent","DisableConsumerAccountStateContent"); return v.HasValue?v!=1:true; },
                    Apply=on=>{ const string sub=@"SOFTWARE\Policies\Microsoft\Windows\CloudContent"; if(on){using var k=Registry.LocalMachine.OpenSubKey(sub,true);k?.DeleteValue("DisableConsumerAccountStateContent",false);}else Registry.SetValue(@"HKEY_LOCAL_MACHINE\"+sub,"DisableConsumerAccountStateContent",1,RegistryValueKind.DWord); Log($"AI Consumer Content {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-paint-ai-image-creator", Name="Paint AI Image Creator",
                    Description="Controls whether the AI Image Creator feature is available in Microsoft Paint",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.LocalMachine,@"SOFTWARE\Policies\Microsoft\Paint","DisableImageCreator"); return v.HasValue?v!=1:true; },
                    Apply=on=>{ Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint","DisableImageCreator",on?0:1,RegistryValueKind.DWord); Log($"Paint AI Image Creator {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-paint-ai-cocreator", Name="Paint AI Cocreator",
                    Description="Controls whether the AI Cocreator feature is available in Microsoft Paint",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.LocalMachine,@"SOFTWARE\Policies\Microsoft\Paint","DisableCocreator"); return v.HasValue?v!=1:true; },
                    Apply=on=>{ Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint","DisableCocreator",on?0:1,RegistryValueKind.DWord); Log($"Paint AI Cocreator {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-paint-generative-fill", Name="Paint Generative Fill",
                    Description="Controls whether the AI Generative Fill feature is available in Microsoft Paint",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.LocalMachine,@"SOFTWARE\Policies\Microsoft\Paint","DisableGenerativeFill"); return v.HasValue?v!=1:true; },
                    Apply=on=>{ Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint","DisableGenerativeFill",on?0:1,RegistryValueKind.DWord); Log($"Paint Generative Fill {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-paint-generative-erase", Name="Paint Generative Erase",
                    Description="Controls whether the AI Generative Erase feature is available in Microsoft Paint",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.LocalMachine,@"SOFTWARE\Policies\Microsoft\Paint","DisableGenerativeErase"); return v.HasValue?v!=1:true; },
                    Apply=on=>{ Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint","DisableGenerativeErase",on?0:1,RegistryValueKind.DWord); Log($"Paint Generative Erase {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-disable-paint-remove-background", Name="Paint Remove Background",
                    Description="Controls whether the AI Remove Background feature is available in Microsoft Paint",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.LocalMachine,@"SOFTWARE\Policies\Microsoft\Paint","DisableRemoveBackground"); return v.HasValue?v!=1:true; },
                    Apply=on=>{ Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Paint","DisableRemoveBackground",on?0:1,RegistryValueKind.DWord); Log($"Paint Remove Background {(on?"enabled":"disabled")}."); }
                },
            };
        }

    }
}
