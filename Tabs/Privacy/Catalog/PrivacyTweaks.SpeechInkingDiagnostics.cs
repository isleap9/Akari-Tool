using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Privacy
{
    public static partial class PrivacyTweaks
    {
        // ══════════════════════════════════════════════════════════════════════
        // SPEECH
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] Speech(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "privacy-speech-recognition", Name = "Online Speech Recognition",
                    Description = "Use your voice for apps using Microsoft's online speech recognition technology",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", on ? 1 : 0, RegistryValueKind.DWord);
                        if (!on) { foreach (var h in new[] { Registry.CurrentUser, Registry.LocalMachine }) h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\InputPersonalization", true)?.DeleteValue("AllowInputPersonalization", false); }
                        Log($"Online Speech Recognition {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-narrator-online-services", Name = "Narrator Online Services",
                    Description = "Allow Narrator to use Microsoft cloud services for features like intelligent image descriptions",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Narrator\NoRoam", "OnlineServicesEnabled"); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        const string sub = @"Software\Microsoft\Narrator\NoRoam";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("OnlineServicesEnabled", false); }
                        else Registry.SetValue(@"HKEY_CURRENT_USER\" + sub, "OnlineServicesEnabled", 0, RegistryValueKind.DWord);
                        Log($"Narrator Online Services {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-narrator-scripting", Name = "Narrator Scripting Support",
                    Description = "Allow Narrator to execute scripts for automation and custom functionality",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Narrator\NoRoam", "ScriptingEnabled"); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        const string sub = @"Software\Microsoft\Narrator\NoRoam";
                        if (on) { using var k = Registry.CurrentUser.OpenSubKey(sub, true); k?.DeleteValue("ScriptingEnabled", false); }
                        else Registry.SetValue(@"HKEY_CURRENT_USER\" + sub, "ScriptingEnabled", 0, RegistryValueKind.DWord);
                        Log($"Narrator Scripting {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // INKING & TYPING
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] InkingTyping(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "privacy-inking-typing-dictionary", Name = "Custom Inking and Typing Dictionary",
                    Description = "Uses your typing history and handwriting patterns to create a custom dictionary",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization", "Value"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\InkingAndTypingPersonalization", "Value", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", on ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Custom Inking/Typing Dictionary {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // DIAGNOSTICS & FEEDBACK
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] Diagnostics(Action<string> Log)
        {
            return new[]
            {
                new TweakDefinition
                {
                    Id = "privacy-diagnostics", Name = "Send Diagnostic Data",
                    Description = "Send diagnostic data to Microsoft to help improve Windows and keep it secure",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack", "ShowedToastAtLevel"); return v.HasValue ? v == 3 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack", "ShowedToastAtLevel", on ? 3 : 1, RegistryValueKind.DWord);
                        int t = on ? 3 : 0;
                        foreach (var path in new[] {
                            @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
                            @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection" })
                        { Registry.SetValue(path, "AllowTelemetry", t, RegistryValueKind.DWord); Registry.SetValue(path, "MaxTelemetryAllowed", t, RegistryValueKind.DWord); }
                        if (!on) { foreach (var h in new[] { "HKEY_CURRENT_USER","HKEY_LOCAL_MACHINE" }) Registry.SetValue($@"{h}\SOFTWARE\Policies\Microsoft\Windows\AppCompat", "AITEnable", 0, RegistryValueKind.DWord); }
                        Log($"Diagnostic Data {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-improve-inking-typing", Name = "Improve inking and typing",
                    Description = "Send optional inking and typing diagnostic data to Microsoft",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Input\TIPC", "Enabled"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Input\TIPC", "Enabled", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\CPSS\Store\ImproveInkingAndTyping", "Value", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Improve Inking and Typing {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-tailored-experiences", Name = "Tailored Experiences",
                    Description = "Let Microsoft use your diagnostic data to show personalized tips, ads and recommendations",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled"); return v.HasValue ? v == 1 : true; },
                    Apply = on =>
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", on ? 1 : 0, RegistryValueKind.DWord);
                        if (!on) { foreach (var h in new[] { Registry.CurrentUser, Registry.LocalMachine }) h.OpenSubKey(@"Software\Policies\Microsoft\Windows\CloudContent", true)?.DeleteValue("DisableTailoredExperiencesWithDiagnosticData", false); }
                        Log($"Tailored Experiences {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id = "privacy-feedback-frequency", Name = "Allow Windows to ask you for feedback",
                    Description = "Let Windows ask you to provide feedback on experiences in Windows",
                    RecommendedState = false, DefaultState = true,
                    ReadState = () => { var v = ReadDword(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod"); return v.HasValue ? v != 0 : true; },
                    Apply = on =>
                    {
                        if (on)
                        {
                            foreach (var h in new[] { Registry.CurrentUser, Registry.LocalMachine }) h.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true)?.DeleteValue("DoNotShowFeedbackNotifications", false);
                            Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Siuf\Rules", true)?.DeleteValue("NumberOfSIUFInPeriod", false);
                        }
                        else
                        {
                            foreach (var h in new[] { "HKEY_CURRENT_USER","HKEY_LOCAL_MACHINE" }) Registry.SetValue($@"{h}\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1, RegistryValueKind.DWord);
                            Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, RegistryValueKind.DWord);
                        }
                        Log($"Feedback requests {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }

    }
}
