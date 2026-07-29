using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.OSTweaks
{
    public partial class OSTweaksTab : BaseTab
    {
        private readonly List<Action> _refreshActions = new();

        public OSTweaksTab() => InitializeComponent();

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("OS Tweaks",
                "General Windows performance and stability tweaks safe for any install."));
            AttachSearch(RootPanel);

            AddSection(RootPanel, "Network", OSTweakCatalog.Network(Log));
            AddSection(RootPanel, "Services", OSTweakCatalog.Services(Log));
            AddSection(RootPanel, "UI & Visual", OSTweakCatalog.Ui(Log));
            BuildSecuritySection(RootPanel);
            AddSection(RootPanel, "Performance", OSTweakCatalog.Performance(Log));
            BuildTimerResolution(RootPanel);

            foreach (var refresh in _refreshActions)
                try { refresh(); } catch { }
        }

        // ── Shared helpers ────────────────────────────────────

        private void AddSection(StackPanel panel, string title, TweakDefinition[] tweaks)
        {
            var section = TweakHelpers.BuildSection(panel, title);
            foreach (var def in tweaks)
                _refreshActions.Add(TweakHelpers.AddTweakRow(section, def));
        }

        private static int? ReadDword(RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using var k = RegistryKey.OpenBaseKey(hive, RegistryView.Default).OpenSubKey(subKey);
                return k?.GetValue(valueName) is int i ? i : (int?)null;
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
