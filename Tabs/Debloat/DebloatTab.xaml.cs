// DebloatTab — the script-based debloat panel, restored 1:1 from the previous
// Software tab's Bloatware panel and moved into its own folder.
//
// Hosted inside SoftwareTab as the third sub-panel ("Debloat" in the sidebar);
// runs the embedded AkariOS/AME Playbook PowerShell scripts via ToolService.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class DebloatTab : BaseTab
    {
        public DebloatTab() => InitializeComponent();

        public override void Initialize(ToolService service)
        {
            base.Initialize(service);
            Build();
        }

        private void Build()
        {
            RootPanel.Children.Add(PageHeader("Debloat",
                "One-click PowerShell scripts: disable telemetry components, remove bloatware, and clean up Edge/OneDrive."));

            BuildGroup(RootPanel, "Privacy & Telemetry", new[]
            {
                ("Telemetry — Disable",             "Disables Windows data collection and telemetry",                  "Telemetry.ps1",             "Telemetry-Undo.ps1"),
                ("Activity History — Disable",      "Erases recent docs, clipboard, and run history",                  "ActivityHistory.ps1",       "ActivityHistory-Undo.ps1"),
                ("Location Tracking — Disable",     "Disables Windows location services",                              "LocationTracking.ps1",      "LocationTracking-Undo.ps1"),
                ("PS7 Telemetry — Disable",         "Opts out of PowerShell 7 telemetry",                             "PS7Telemetry.ps1",          "PS7Telemetry-Undo.ps1"),
                ("Windows AI — Disable",            "Removes Copilot, Recall, and all AI features",                    "WindowsAI.ps1",             "WindowsAI-Undo.ps1"),
                ("Consumer Features — Disable",     "Disables suggested apps, tips, and Windows promotions",           "ConsumerFeatures.ps1",      "ConsumerFeatures-Undo.ps1"),
                ("Background Apps — Disable",       "Stops Microsoft Store apps running in the background",            "DisableBGApps.ps1",         "DisableBGApps-Undo.ps1"),
                ("Store Search — Disable",          "Hides Microsoft Store results from Start Menu search",            "StoreSearch.ps1",           "StoreSearch-Undo.ps1"),
                ("Delivery Optimization — Disable", "Stops Windows using your bandwidth to share updates",             "DeliveryOptimization.ps1",  "DeliveryOptimization-Undo.ps1"),
                ("Device Companion Apps — Block",   "Stops Windows fetching vendor apps/ads when you plug in a device", "DeviceMetadata.ps1",       "DeviceMetadata-Undo.ps1"),
                ("WPBT — Disable",                  "Blocks OEM firmware from executing vendor binaries at boot",       "WPBT.ps1",                  "WPBT-Undo.ps1"),

            });

            BuildGroup(RootPanel, "Apps & Components", new[]
            {
                ("Unwanted Apps — Remove",       "Removes bloatware UWP apps (AkariOS AME Playbook list — 60+ packages)",   "Debloat.ps1",               "Debloat-Undo.ps1"),
                ("OneDrive — Remove",            "Completely removes OneDrive from the system",                     "RemoveOneDrive.ps1",        "RemoveOneDrive-Undo.ps1"),
                ("Microsoft Edge — Debloat",     "Disables telemetry, popups, and annoyances in Edge",             "EdgeDebloat.ps1",           "EdgeDebloat-Undo.ps1"),
                ("Microsoft Edge — Remove",      "Fully uninstalls Microsoft Edge from the system",                 "RemoveEdge.ps1",            ""),
                ("Widgets — Remove",             "Removes the Widgets button from the taskbar",                     "Widgets.ps1",               "Widgets-Undo.ps1"),
            });

            BuildGroup(RootPanel, "Cleanup", new[]
            {
                ("Create Restore Point",         "Creates a Windows system restore point before making changes",    "RestorePoint.ps1",          ""),
                ("Disk Cleanup — Run",           "Runs cleanup on C: and removes old Windows updates",             "DiskCleanup.ps1",           ""),
                ("Temporary Files — Remove",     "Clears temp folders and prefetch files",                          "TempFiles.ps1",             ""),
                ("O&O ShutUp10++ — Run",         "Downloads and launches the O&O ShutUp10 privacy tool",           "OOSU.ps1",                  ""),
            });
        }

        // ── Group builder (moved verbatim from the previous Software tab) ─────

        private void BuildGroup(StackPanel panel, string groupTitle,
            (string Title, string Desc, string Script, string Undo)[] items)
        {
            panel.Children.Add(new TextBlock
            {
                Text = groupTitle.ToUpperInvariant(),
                FontFamily = (FontFamily)(Application.Current.Resources["MonoFont"] ?? new FontFamily("Consolas")),
                FontSize = 10.5,
                FontWeight = FontWeights.Medium,
                Foreground = TweakHelpers.TextMuted,
                Margin = new Thickness(4, 16, 0, 6)
            });

            var card = new Border
            {
                Background = TweakHelpers.CardBackground(),
                BorderBrush = TweakHelpers.Token("AkariOverlayMedium"),
                BorderThickness = new Thickness(1),
                CornerRadius = TweakHelpers.CardRadius,
                Margin = new Thickness(0, 0, 0, 16),
                Effect = TweakHelpers.CardShadow()
            };
            var stack = new StackPanel { Margin = new Thickness(18, 10, 18, 10) };

            for (int i = 0; i < items.Length; i++)
            {
                var (title, desc, script, undo) = items[i];

                if (i > 0)
                    stack.Children.Add(new Separator { Background = TweakHelpers.Hairline, Height = 1, Margin = new Thickness(-16, 0, -16, 0) });

                var row = new Grid { Margin = new Thickness(0, 10, 0, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();
                Grid.SetColumn(info, 0);
                info.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
                info.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                Grid.SetColumn(buttons, 1);

                var capturedScript = script;
                var capturedUndo = undo;
                var capturedTitle = title;

                var runBtn = new Button { Content = "Run", Style = (Style)FindResource("RunBtn") };
                runBtn.Click += async (_, _) =>
                    await Service!.RunWithTracking(new ScriptAction(capturedScript), capturedTitle, AppliedTweaks);
                buttons.Children.Add(runBtn);

                if (!string.IsNullOrEmpty(undo))
                {
                    var undoBtn = new Button { Content = "Undo", Style = (Style)FindResource("UndoBtn") };
                    undoBtn.Click += async (_, _) => await Service!.RunAction(new ScriptAction(capturedUndo));
                    buttons.Children.Add(undoBtn);
                }

                row.Children.Add(info);
                row.Children.Add(buttons);
                stack.Children.Add(row);
            }

            card.Child = stack;
            panel.Children.Add(card);
        }
    }
}
