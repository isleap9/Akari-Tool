using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Gaming
{
    public partial class GamingTab : BaseTab
    {
        private readonly List<Action> _refreshActions = new();

        public GamingTab() => InitializeComponent();

        public override string NavTag   => "Gaming";
        public override string NavLabel => "Gaming & Performance";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Gaming & Performance",
                "Game Mode, GPU, CPU, and network tweaks for maximum frame rates.",
                withActions: true, RootPanel));

            AddSection(RootPanel, "Game Mode", GamingTweaks.GameMode(Log));
            AddSection(RootPanel, "Processor", GamingTweaks.Processor(Log));
            AddSection(RootPanel, "Graphics", GamingTweaks.Graphics(Log));
            AddSection(RootPanel, "Storage", GamingTweaks.Storage(Log));
            AddSection(RootPanel, "Network", GamingTweaks.Network(Log));
            AddSection(RootPanel, "Xbox", GamingTweaks.Xbox(Log));
            AddSection(RootPanel, "Security", GamingTweaks.Security(Log));
            BuildSystemServices(RootPanel);
            BuildScheduledTasks(RootPanel);
            BuildSystemRestore(RootPanel);
            AddSection(RootPanel, "Accessibility", GamingTweaks.Accessibility(Log));
            AddSection(RootPanel, "Visual Effects", GamingTweaks.VisualEffects(Log));
            foreach (var refresh in _refreshActions)
                try { refresh(); } catch { }
        }

        // ── Section helper ────────────────────────────────────────────────────

        private void AddSection(StackPanel panel, string title, TweakDefinition[] tweaks)
        {
            var section = TweakHelpers.BuildSection(panel, title);
            foreach (var def in tweaks)
                _refreshActions.Add(TweakHelpers.AddTweakRow(section, def));
            TweakHelpers.AttachBulkActions(section);
        }

        // ── Registry read helpers ─────────────────────────────────────────────

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey);
                return k?.GetValue(valueName) is int i ? i : null;
            }
            catch { return null; }
        }

        private static string? ReadString(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey);
                return k?.GetValue(valueName) as string;
            }
            catch { return null; }
        }

        private void Log(string msg) => Service?.Log(msg);
    }
}