using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab : BaseTab
    {
        private StackPanel _panelTaskbar     = null!;
        private bool _suppressRestart = false;
        private StackPanel _panelExplorer    = null!;
        private StackPanel _panelContextMenu = null!;
        private StackPanel _panelAppearance  = null!;
        private StackPanel _panelStartMenu   = null!;
        private StackPanel _panelDesktop     = null!;

        public CustomizeTab() => InitializeComponent();

        public override string NavTag   => "Customize";
        public override string NavLabel => "Customize";

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        public IEnumerable<(string Label, StackPanel Panel)> SubPanels =>
        [
            ("Taskbar",      _panelTaskbar),
            ("Explorer",     _panelExplorer),
            ("Context Menu", _panelContextMenu),
            ("Appearance",   _panelAppearance),
            ("Start Menu",   _panelStartMenu),
            ("Desktop",      _panelDesktop),
        ];

        public void ShowPanel(string name)
        {
            _panelTaskbar.Visibility     = name == "Taskbar"     ? Visibility.Visible : Visibility.Collapsed;
            _panelExplorer.Visibility    = name == "Explorer"    ? Visibility.Visible : Visibility.Collapsed;
            _panelContextMenu.Visibility = name == "ContextMenu" ? Visibility.Visible : Visibility.Collapsed;
            _panelAppearance.Visibility  = name == "Appearance"  ? Visibility.Visible : Visibility.Collapsed;
            _panelStartMenu.Visibility   = name == "StartMenu"   ? Visibility.Visible : Visibility.Collapsed;
            _panelDesktop.Visibility     = name == "Desktop"     ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Build()
        {
            _panelTaskbar     = new StackPanel();
            _panelExplorer    = new StackPanel();
            _panelContextMenu = new StackPanel();
            _panelAppearance  = new StackPanel();
            _panelStartMenu   = new StackPanel();
            _panelDesktop     = new StackPanel();

            BuildTaskbar(_panelTaskbar);
            BuildExplorer(_panelExplorer);
            BuildContextMenu(_panelContextMenu);
            BuildAppearance(_panelAppearance);
            BuildStartMenu(_panelStartMenu);
            BuildDesktop(_panelDesktop);

            RootPanel.Children.Add(_panelTaskbar);
            RootPanel.Children.Add(_panelExplorer);
            RootPanel.Children.Add(_panelContextMenu);
            RootPanel.Children.Add(_panelAppearance);
            RootPanel.Children.Add(_panelStartMenu);
            RootPanel.Children.Add(_panelDesktop);

            ShowPanel("Taskbar");
        }
        private static int? ReadDwordCu(string subKey, string valueName)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(subKey);
                return k?.GetValue(valueName) is int i ? i : (int?)null;
            }
            catch { return null; }
        }

        // Instance (not static) so it can log via the shared Service on failure. A denied
        // write degrades gracefully with a logged message instead of escaping to the WPF
        // dispatcher as an unhandled UnauthorizedAccessException. Shared by every Customize
        // taskbar/explorer tweak, so the guard protects all of them.
        private bool SetHkcu(string subKey, string valueName, int value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(subKey,
                    RegistryKeyPermissionCheck.ReadWriteSubTree);
                if (key == null)
                {
                    Service?.Log($"[TASKBAR] Could not open HKCU\\{subKey} for writing.");
                    return false;
                }
                key.SetValue(valueName, value, RegistryValueKind.DWord);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                Service?.Log($"[TASKBAR] Access denied writing {valueName} to HKCU\\{subKey}. " +
                             "This value may be locked by policy; try running as administrator.");
                return false;
            }
            catch (Exception ex)
            {
                Service?.Log($"[TASKBAR] Failed to write {valueName}: {ex.Message}");
                return false;
            }
        }
        // Explorer restarting lives in the shared ExplorerRestart helper
        // (Tabs/Shared/ExplorerRestart.cs) so bulk operations can batch it.
    }
}
