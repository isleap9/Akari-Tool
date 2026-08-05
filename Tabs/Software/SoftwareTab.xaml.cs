// SoftwareTab — 1:1 functional port of Winhance's Software section, card-grid UI.
//
//   • Windows Apps  (panel "Bloatware"): 56 removable apps + 10 legacy
//     capabilities + 7 optional features as selectable cards with live
//     installed status, Winhance-style badges (Installed / Warning /
//     Permanent), select-all controls, and Install / Remove / Refresh.
//     Removals run the generated BloatRemoval.ps1 pipeline (+ dedicated
//     Edge/OneDrive scripts) and persist via a SYSTEM startup task.
//   • External Apps (panel "AppInstaller"): 193 winget apps in category
//     sections (Browsers, Compression, …), same card UI.
//   • Debloat       (panel "Debloat"): hosts Tabs/Debloat/DebloatTab —
//     the script-based one-click debloat groups.
//
// Cards mirror Winhance's card view: checkbox + avatar + name/description
// + badge row, click anywhere to select, responsive column count.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class SoftwareTab : BaseTab
    {
        private const double CardMinWidth = 340;   // responsive column threshold

        // ── Sub-panels (routed from the sidebar via ShowPanel) ────────────────
        private StackPanel _panelWindowsApps = null!;
        private StackPanel _panelExternalApps = null!;
        private StackPanel _panelDebloat = null!;
        private DebloatTab _debloatTab = null!;

        // ── Catalogs ──────────────────────────────────────────────────────────
        private readonly List<AppDefinition> _windowsApps = [];
        private readonly List<AppDefinition> _externalApps = [];

        // ── Per-card / per-section registries ─────────────────────────────────
        private sealed class AppCard
        {
            public required AppDefinition App { get; init; }
            public required Border Card { get; init; }
            public required CheckBox Check { get; init; }
            public required Border InstalledBadge { get; init; }
            public bool Visible { get; set; } = true;
        }

        private sealed class CardSection
        {
            public required TextBlock Header { get; init; }
            public required UniformGrid Grid { get; init; }
            public required List<AppCard> Cards { get; init; }
        }

        private readonly List<AppCard> _windowsCards = [];
        private readonly List<CardSection> _windowsSections = [];
        private readonly List<AppCard> _externalCards = [];
        private readonly List<CardSection> _externalSections = [];

        private bool _busy;

        // Windows Apps controls
        private Button _waInstallBtn = null!, _waRemoveBtn = null!, _waRefreshBtn = null!;
        private TextBlock _waSelectedCount = null!, _waStatus = null!;

        // External Apps controls
        private Button _eaInstallBtn = null!, _eaUninstallBtn = null!, _eaRefreshBtn = null!;
        private TextBlock _eaSelectedCount = null!, _eaStatus = null!;

        public SoftwareTab() => InitializeComponent();

        public override string NavTag   => "Software";
        public override string NavLabel => "Software";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        /// <summary>
        /// Sidebar routing. Panel keys: "Bloatware" → Windows Apps,
        /// "AppInstaller" → External Apps, "Debloat" → Debloat scripts.
        /// </summary>
        public void ShowPanel(string name)
        {
            _panelWindowsApps.Visibility = name == "Bloatware" ? Visibility.Visible : Visibility.Collapsed;
            _panelExternalApps.Visibility = name == "AppInstaller" ? Visibility.Visible : Visibility.Collapsed;
            _panelDebloat.Visibility = name == "Debloat" ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Windows apps currently ticked in the Windows Apps grid — consumed by
        /// the Advanced Tools Autounattend XML generator.
        /// </summary>
        public List<AppDefinition> GetSelectedWindowsApps() =>
            _windowsApps.Where(a => a.IsSelected).ToList();

        private void Build()
        {
            _windowsApps.AddRange(WindowsAppCatalog.GetWindowsApps().Items);
            _windowsApps.AddRange(CapabilityCatalog.GetWindowsCapabilities().Items);
            _windowsApps.AddRange(OptionalFeatureCatalog.GetWindowsOptionalFeatures().Items);
            _externalApps.AddRange(ExternalAppCatalog.GetExternalApps().Items);

            _panelWindowsApps = new StackPanel();
            _panelExternalApps = new StackPanel { Visibility = Visibility.Collapsed };
            _panelDebloat = new StackPanel { Visibility = Visibility.Collapsed };

            BuildWindowsAppsPanel(_panelWindowsApps);
            BuildExternalAppsPanel(_panelExternalApps);

            _debloatTab = new DebloatTab();
            _debloatTab.Initialize(Service!);
            _panelDebloat.Children.Add(_debloatTab);

            RootPanel.Children.Add(_panelWindowsApps);
            RootPanel.Children.Add(_panelExternalApps);
            RootPanel.Children.Add(_panelDebloat);

            _ = RefreshStatusAsync();
        }

    }
}
