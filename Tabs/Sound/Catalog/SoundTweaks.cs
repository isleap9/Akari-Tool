using Microsoft.Win32;
using AkariTool.Core.Tweaks;

namespace AkariTool.Tabs.Sound
{
    // MVVM PORT: extracted verbatim from the net8 SoundTab.xaml.cs. The TweakDefinition
    // data (and its ReadDword/ReadString read helpers) moved here unchanged; only the
    // rendering scaffolding (BaseTab, PageHeader, AddSection wrapper, InitializeComponent,
    // _refreshActions) was left behind. Section title "System Sounds" is the method name.
    public static partial class SoundTweaks
    {
        // ── Registry helpers ──────────────────────────────────────────────────

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) is int i ? i : (int?)null; }
            catch { return null; }
        }

        private static string? ReadString(RegistryHive hive, string subKey, string valueName)
        {
            try { using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey); return k?.GetValue(valueName) as string; }
            catch { return null; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // SYSTEM SOUNDS (Winhance port)
        // ══════════════════════════════════════════════════════════════════════

        public static TweakDefinition[] SystemSounds(Action<string> Log)
        {
            const string BootKey  = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation";
            const string BootSub  = @"Software\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation";
            const string EdKey    = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\EditionOverrides";
            const string EdSub    = @"Software\Microsoft\Windows\CurrentVersion\EditionOverrides";
            const string DuckKey  = @"HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio";
            const string DuckSub  = @"Software\Microsoft\Multimedia\Audio";
            const string NarrKey  = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam";
            const string NarrSub  = @"Software\Microsoft\Narrator\NoRoam";
            const string VoiceKey = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings";
            const string VoiceSub = @"Software\Microsoft\Windows\CurrentVersion\SpeechOneCore\Settings";
            const string AccKey   = @"HKEY_CURRENT_USER\Control Panel\Accessibility";
            const string AccSub   = @"Control Panel\Accessibility";

            return new[]
            {
                new TweakDefinition
                {
                    Id               = "sound-startup",
                    Name             = "Startup Sound During Boot",
                    Description      = "Play the Windows startup sound when the system boots",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    // Disabled when DisableStartupSound=1 (either key); absent/0 = enabled
                    ReadState = () =>
                    {
                        var a = ReadDword(RegistryHive.LocalMachine, BootSub, "DisableStartupSound");
                        var b = ReadDword(RegistryHive.LocalMachine, EdSub,   "UserSetting_DisableStartupSound");
                        return (a ?? 0) != 1 && (b ?? 0) != 1;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(BootKey, "DisableStartupSound",             on ? 0 : 1, RegistryValueKind.DWord);
                        Registry.SetValue(EdKey,   "UserSetting_DisableStartupSound", on ? 0 : 1, RegistryValueKind.DWord);
                        Log($"Startup sound {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id           = "sound-ducking",
                    Name         = "Sound Ducking Preference",
                    Description  = "How Windows adjusts other sounds when it detects communication activity",
                    IsPreference = true,
                    InputKind    = TweakInputKind.Dropdown,
                    Options = new[]
                    {
                        new TweakDropdownOption("Mute all other sounds",                     0),
                        new TweakDropdownOption("Reduce the volume of other sounds by 80%", 1, IsDefault: true),
                        new TweakDropdownOption("Reduce the volume of other sounds by 50%", 2),
                        new TweakDropdownOption("Do nothing",                               3, IsRecommended: true),
                    },
                    // Absent = Windows default (index 1)
                    ReadCurrentIndex = () =>
                    {
                        var v = ReadDword(RegistryHive.CurrentUser, DuckSub, "UserDuckingPreference") ?? 1;
                        return v is >= 0 and <= 3 ? v : 1;
                    },
                    ApplyIndex = idx =>
                    {
                        Registry.SetValue(DuckKey, "UserDuckingPreference", idx, RegistryValueKind.DWord);
                        Log($"Sound ducking preference set to option {idx}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "sound-narrator-ducking",
                    Name             = "Narrator Audio Ducking",
                    Description      = "Lower the volume of other apps while Narrator is speaking",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => (ReadDword(RegistryHive.CurrentUser, NarrSub, "DuckAudio") ?? 1) != 0,
                    Apply = on =>
                    {
                        Registry.SetValue(NarrKey, "DuckAudio", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Narrator audio ducking {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "sound-voice-activation",
                    Name             = "Voice Activation for Apps",
                    Description      = "Allow apps to listen for voice keywords and activate via speech",
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () => (ReadDword(RegistryHive.LocalMachine, VoiceSub, "AgentActivationEnabled") ?? 1) != 0,
                    Apply = on =>
                    {
                        Registry.SetValue(VoiceKey, "AgentActivationEnabled",  on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(VoiceKey, "AgentActivationLastUsed", on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Voice activation for apps {(on ? "enabled" : "disabled")}.");
                    }
                },
                new TweakDefinition
                {
                    Id               = "sound-accessibility",
                    Name             = "Accessibility Activation & Warning Sounds",
                    Description      = "Play sounds when accessibility features like Sticky Keys are activated",
                    IsPreference     = true,
                    RecommendedState = false,
                    DefaultState     = true,
                    ReadState = () =>
                    {
                        // REG_SZ "1"/"0" on some builds, DWORD on others — handle both
                        var s = ReadString(RegistryHive.CurrentUser, AccSub, "Sound on Activation");
                        if (s != null) return s != "0";
                        return (ReadDword(RegistryHive.CurrentUser, AccSub, "Sound on Activation") ?? 1) != 0;
                    },
                    Apply = on =>
                    {
                        Registry.SetValue(AccKey, "Sound on Activation", on ? 1 : 0, RegistryValueKind.DWord);
                        Registry.SetValue(AccKey, "Warning Sounds",      on ? 1 : 0, RegistryValueKind.DWord);
                        Log($"Accessibility sounds {(on ? "enabled" : "disabled")}.");
                    }
                },
            };
        }
    }
}
