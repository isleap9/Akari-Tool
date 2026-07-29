using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Privacy
{
    public static partial class PrivacyTweaks
    {
        // ══════════════════════════════════════════════════════════════════════
        // MICROSOFT EDGE AI
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] EdgeAI(Action<string> Log)
        {
            const string EdgePol = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge";
            const string EdgeSub = @"SOFTWARE\Policies\Microsoft\Edge";

            // EnabledValue=[1,null], DisabledValue=[0]
            bool? ReadEdge(string v) { var val = ReadDword(RegistryHive.LocalMachine, EdgeSub, v); return val.HasValue ? val != 0 : true; }
            void  ApplyEdge(string v, bool on, int disabledVal = 0) { if (on) { using var k = Registry.LocalMachine.OpenSubKey(EdgeSub, true); k?.DeleteValue(v, false); } else Registry.SetValue(EdgePol, v, disabledVal, RegistryValueKind.DWord); }

            return new[]
            {
                new TweakDefinition { Id="privacy-edge-copilot-cdp-page-context", Name="Edge Copilot CDP Page Context", Description="Controls whether Copilot can use CDP to access page content in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("CopilotCDPPageContext"), Apply=on=>{ApplyEdge("CopilotCDPPageContext",on);Log($"Edge Copilot CDP Page Context {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-copilot-page-context", Name="Edge Copilot Page Context", Description="Controls whether Copilot can read page content in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("CopilotPageContext"), Apply=on=>{ApplyEdge("CopilotPageContext",on);Log($"Edge Copilot Page Context {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-copilot-sidebar", Name="Edge Copilot Sidebar", Description="Controls whether the Copilot sidebar is available in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("HubsSidebarEnabled"), Apply=on=>{ApplyEdge("HubsSidebarEnabled",on);Log($"Edge Copilot Sidebar {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-entra-copilot", Name="Edge Entra Copilot Page Context", Description="Controls whether Entra Copilot can access page context in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("EdgeEntraCopilotPageContext"), Apply=on=>{ApplyEdge("EdgeEntraCopilotPageContext",on);Log($"Edge Entra Copilot Page Context {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-m365-copilot-icon", Name="Edge M365 Copilot Chat Icon", Description="Controls whether the Microsoft 365 Copilot chat icon is shown in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("Microsoft365CopilotChatIconEnabled"), Apply=on=>{ApplyEdge("Microsoft365CopilotChatIconEnabled",on);Log($"Edge M365 Copilot Icon {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-ai-history-search", Name="Edge AI History Search", Description="Controls whether AI-powered history search is available in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("EdgeHistoryAISearchEnabled"), Apply=on=>{ApplyEdge("EdgeHistoryAISearchEnabled",on);Log($"Edge AI History Search {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-inline-compose", Name="Edge Inline AI Compose", Description="Controls whether AI-powered inline compose suggestions are available in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("ComposeInlineEnabled"), Apply=on=>{ApplyEdge("ComposeInlineEnabled",on);Log($"Edge Inline AI Compose {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-local-ai-model", Name="Edge Local AI Model Settings", Description="Controls whether local AI model settings are available in Microsoft Edge", RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.LocalMachine,EdgeSub,"GenAILocalFoundationalModelSettings"); return v.HasValue?v!=1:true; },
                    Apply=on=>{ApplyEdge("GenAILocalFoundationalModelSettings",on,disabledVal:1);Log($"Edge Local AI Model {(on?"enabled":"disabled")}.");}
                },
                new TweakDefinition { Id="privacy-edge-builtin-ai-apis", Name="Edge Built-in AI APIs", Description="Controls whether built-in AI APIs are available in Microsoft Edge for websites to use", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("BuiltInAIAPIsEnabled"), Apply=on=>{ApplyEdge("BuiltInAIAPIsEnabled",on);Log($"Edge Built-in AI APIs {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-ai-themes", Name="Edge AI Generated Themes", Description="Controls whether AI-generated themes are available in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("AIGenThemesEnabled"), Apply=on=>{ApplyEdge("AIGenThemesEnabled",on);Log($"Edge AI Themes {(on?"enabled":"disabled")}.");} },
                new TweakDefinition { Id="privacy-edge-devtools-ai", Name="Edge DevTools AI", Description="Controls whether AI features are available in Edge DevTools", RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.LocalMachine,EdgeSub,"DevToolsGenAiSettings"); return v.HasValue?v!=2:true; },
                    Apply=on=>{ApplyEdge("DevToolsGenAiSettings",on,disabledVal:2);Log($"Edge DevTools AI {(on?"enabled":"disabled")}.");}
                },
                new TweakDefinition { Id="privacy-edge-share-history-copilot", Name="Edge Share History with Copilot", Description="Controls whether browsing history is shared with Copilot search in Microsoft Edge", RecommendedState=false, DefaultState=true, ReadState=()=>ReadEdge("ShareBrowsingHistoryWithCopilotSearchAllowed"), Apply=on=>{ApplyEdge("ShareBrowsingHistoryWithCopilotSearchAllowed",on);Log($"Edge Share History with Copilot {(on?"enabled":"disabled")}.");} },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // MICROSOFT OFFICE AI
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] OfficeAI(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition { Id="privacy-office-ai-training", Name="Office AI Training",
                    Description="Controls whether Office collects AI training data from your usage",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Policies\Microsoft\office\16.0\common\ai\training","optionalconnectedexperiencesenabled"); return v.HasValue?v!=0:true; },
                    Apply=on=>{ const string sub=@"Software\Policies\Microsoft\office\16.0\common\ai\training"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);k?.DeleteValue("optionalconnectedexperiencesenabled",false);}else Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"optionalconnectedexperiencesenabled",0,RegistryValueKind.DWord); Log($"Office AI Training {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-office-connected-services", Name="Office Connected Services",
                    Description="Controls whether Office connected experiences and AI-powered services are available",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Policies\Microsoft\office\16.0\common\privacy","controllerconnectedservicesenabled"); return v.HasValue?v!=2:true; },
                    Apply=on=>{ const string sub=@"Software\Policies\Microsoft\office\16.0\common\privacy"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);k?.DeleteValue("controllerconnectedservicesenabled",false);k?.DeleteValue("usercontentdisabled",false);}else{Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"controllerconnectedservicesenabled",2,RegistryValueKind.DWord);Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"usercontentdisabled",2,RegistryValueKind.DWord);} Log($"Office Connected Services {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-word-copilot", Name="Word Copilot",
                    Description="Controls whether Copilot AI features are available in Microsoft Word",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Microsoft\Office\16.0\Word\Options","EnableCopilot"); return v.HasValue?v!=0:true; },
                    Apply=on=>{ const string sub=@"Software\Microsoft\Office\16.0\Word\Options"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);k?.DeleteValue("EnableCopilot",false);}else Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"EnableCopilot",0,RegistryValueKind.DWord); Log($"Word Copilot {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-excel-copilot", Name="Excel Copilot",
                    Description="Controls whether Copilot AI features are available in Microsoft Excel",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Microsoft\Office\16.0\Excel\Options","EnableCopilot"); return v.HasValue?v!=0:true; },
                    Apply=on=>{ const string sub=@"Software\Microsoft\Office\16.0\Excel\Options"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);k?.DeleteValue("EnableCopilot",false);}else Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"EnableCopilot",0,RegistryValueKind.DWord); Log($"Excel Copilot {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-onenote-copilot", Name="OneNote Copilot",
                    Description="Controls whether Copilot AI features, Copilot notebooks, and Copilot skittle are available in Microsoft OneNote",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Microsoft\Office\16.0\OneNote\Options\Other","EnableCopilot"); return v.HasValue?v!=0:true; },
                    Apply=on=>{ const string sub=@"Software\Microsoft\Office\16.0\OneNote\Options\Other"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);foreach(var n in new[]{"EnableCopilot","EnableCopilotNotebooks","EnableCopilotSkittle"})k?.DeleteValue(n,false);}else{foreach(var n in new[]{"EnableCopilot","EnableCopilotNotebooks","EnableCopilotSkittle"})Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,n,0,RegistryValueKind.DWord);} Log($"OneNote Copilot {(on?"enabled":"disabled")}."); }
                },
                new TweakDefinition { Id="privacy-office-content-safety-ai", Name="Office AI Content Safety",
                    Description="Controls whether AI content safety features are available in Office apps",
                    RecommendedState=false, DefaultState=true,
                    ReadState=()=>{ var v=ReadDword(RegistryHive.CurrentUser,@"Software\Policies\Microsoft\office\16.0\common\ai","contentsafetyserviceenabled"); return v.HasValue?v!=0:true; },
                    Apply=on=>{ const string sub=@"Software\Policies\Microsoft\office\16.0\common\ai"; if(on){using var k=Registry.CurrentUser.OpenSubKey(sub,true);k?.DeleteValue("contentsafetyserviceenabled",false);}else Registry.SetValue(@"HKEY_CURRENT_USER\"+sub,"contentsafetyserviceenabled",0,RegistryValueKind.DWord); Log($"Office AI Content Safety {(on?"enabled":"disabled")}."); }
                },
            };
        }
    }
}
