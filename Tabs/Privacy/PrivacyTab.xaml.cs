using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Privacy
{
    public partial class PrivacyTab : BaseTab
    {
        private readonly List<Action> _refreshActions = new();

        public PrivacyTab() => InitializeComponent();

        public override string NavTag   => "Privacy";
        public override string NavLabel => "Privacy & Security";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Privacy & Security",
                "Telemetry, data collection, app permissions, and AI opt-outs.",
                withActions: true, RootPanel));

            AddSection(RootPanel, "Security", PrivacyTweaks.Security(Log));
            AddSection(RootPanel, "Content Delivery & Advertising", PrivacyTweaks.ContentDelivery(Log));
            AddSection(RootPanel, "Lock Screen", PrivacyTweaks.LockScreen(Log));
            AddSection(RootPanel, "General", PrivacyTweaks.General(Log));
            AddSection(RootPanel, "Speech", PrivacyTweaks.Speech(Log));
            AddSection(RootPanel, "Inking & Typing Personalization", PrivacyTweaks.InkingTyping(Log));
            AddSection(RootPanel, "Diagnostics & Feedback", PrivacyTweaks.Diagnostics(Log));
            AddSection(RootPanel, "Search Permissions", PrivacyTweaks.SearchPermissions(Log));
            AddSection(RootPanel, "Activity History", PrivacyTweaks.ActivityHistory(Log));
            AddSection(RootPanel, "App Permissions", PrivacyTweaks.AppPermissions(Log));
            AddSection(RootPanel, "Windows AI", PrivacyTweaks.WindowsAI(Log));
            AddSection(RootPanel, "Microsoft Edge AI", PrivacyTweaks.EdgeAI(Log));
            AddSection(RootPanel, "Microsoft Office AI", PrivacyTweaks.OfficeAI(Log));

            foreach (var refresh in _refreshActions)
                try { refresh(); } catch { }
        }

        private void AddSection(StackPanel panel, string title, TweakDefinition[] tweaks)
        {
            var section = TweakHelpers.BuildSection(panel, title);
            foreach (var def in tweaks)
                _refreshActions.Add(TweakHelpers.AddTweakRow(section, def));
            TweakHelpers.AttachBulkActions(section);
        }

        private static bool? ReadConsentStore(string subKey)
        {
            try { using var k = Registry.LocalMachine.OpenSubKey(subKey); var v = k?.GetValue("Value") as string; return v == null ? (bool?)true : v != "Deny"; }
            catch { return true; }
        }

        private static void WriteConsentStore(string subKey, bool allow)
            => Registry.SetValue($@"HKEY_LOCAL_MACHINE\{subKey}", "Value", allow ? "Allow" : "Deny", RegistryValueKind.String);

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

        private void Log(string msg) => Service?.Log(msg);
    }
}
