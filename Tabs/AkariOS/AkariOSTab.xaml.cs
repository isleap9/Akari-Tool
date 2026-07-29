using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.AkariOS
{
    public partial class AkariOSTab : BaseTab
    {
        public AkariOSTab() => InitializeComponent();

        public override string NavTag   => "AkariOS";
        public override string NavLabel => "AkariOS";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("AkariOS",
                "AkariOS presets and system configuration."));

            // One continuous page — every section below is a collapsible card,
            // which made the old sub-tab split redundant.
            BuildPostInstallBanner(RootPanel);
            BuildGamingTweaksCard(RootPanel);   // Services, Service Preset, Playbook, BCD,
                                                // Shader Cache Cleaner, Gaming Tweaks, NVIDIA, AMD
            BuildUtilitiesPanel(RootPanel);     // Account, Interface, System
            BuildUsefulToolsCard(RootPanel);    // Useful Tools — last
        }

        // ── Shared helpers ────────────────────────────────────────────────

        private static void RunShellProcess(string fileName, string arguments)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"RunShellProcess error: {ex.Message}"); }
        }

    }
}
