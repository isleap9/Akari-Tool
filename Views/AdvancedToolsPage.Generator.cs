using System.IO;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AkariTool.Services;
using AkariTool.Tabs;

namespace AkariTool.Views;

/// <summary>
/// Advanced Tools ▸ Autounattend generator — faithful port of net8
/// AdvancedToolsTab.Generator. Read-only apps-to-remove summary (from the selection
/// provider) + two tweak checklists (System/User, backed by UnattendTweakCatalog) →
/// Generate → AutounattendService.GenerateToFile.
/// </summary>
public sealed partial class AdvancedToolsPage
{
    private void BuildGenerator()
    {
        // Back row
        var backRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        backRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        backRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var backBtn = MakeButton("");
        var backContent = new StackPanel { Orientation = Orientation.Horizontal };
        backContent.Children.Add(new TextBlock
        {
            Text = G("E72B"),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0),
        });
        backContent.Children.Add(new TextBlock { Text = "Back", VerticalAlignment = VerticalAlignment.Center });
        backBtn.Content = backContent;
        backBtn.Click += (_, _) => ShowLanding();

        var title = new TextBlock
        {
            Text = "Create Autounattend XML",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
        };
        Grid.SetColumn(title, 1);

        backRow.Children.Add(backBtn);
        backRow.Children.Add(title);
        GeneratorPanel.Children.Add(backRow);

        GeneratorPanel.Children.Add(MakeHint(
            "Builds an autounattend.xml that installs Windows unattended and applies your Akari Tool " +
            "selections: the Windows apps ticked in Software › Windows Apps are removed (with self-healing " +
            "scheduled tasks), and the tweaks selected below are baked in. Includes Windows 11 hardware " +
            "bypasses, local account setup, and .NET 3.5 from the media."));

        // Windows apps summary card
        var appsCard = MakeGenCard();
        var appsInner = (StackPanel)appsCard.Child;
        appsInner.Children.Add(new TextBlock
        {
            Text = "Windows Apps to Remove",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 6),
        });
        _genAppsSummary = MakeHint("—");
        appsInner.Children.Add(_genAppsSummary);
        GeneratorPanel.Children.Add(appsCard);

        // Tweak checklists
        AddTweakGroup("System Tweaks (applied during setup)",
            UnattendTweakCatalog.All.Where(t => !t.UserScoped));
        AddTweakGroup("User Tweaks (applied once at first logon, then reboots)",
            UnattendTweakCatalog.All.Where(t => t.UserScoped));

        // Generate row
        var generateBtn = MakePrimaryButton("Generate autounattend.xml");
        generateBtn.Click += async (_, _) =>
        {
            var pickedXml = await _files.SaveFileAsync("autounattend.xml", new[] { ".xml" });
            if (pickedXml is null) return;

            await RunBusyAsync("Autounattend XML generation", async _ =>
            {
                var apps = GetSelectedApps();
                var tweaks = GetSelectedTweaks();
                await Task.Run(() => _xmlGen.GenerateToFile(pickedXml.Path, apps, tweaks));
                _tool.Log($"[XML] Done — {apps.Count} app(s), {tweaks.Count} tweak(s) → {pickedXml.Path}");
            });
        };

        var hint = new TextBlock
        {
            Text = "Tip: use the WIM Utility to bake this file into a bootable ISO, or copy it to the root of a Windows USB drive.",
            FontSize = 12,
            Foreground = Res("TextFillColorSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        GeneratorPanel.Children.Add(HRow(generateBtn, hint));
    }

    private void AddTweakGroup(string heading, IEnumerable<UnattendTweakOption> options)
    {
        var card = MakeGenCard();
        var inner = (StackPanel)card.Child;

        inner.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Res("TextFillColorPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 10),
        });

        foreach (var opt in options)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            var check = new CheckBox
            {
                Content = opt.Name,
                IsChecked = opt.DefaultOn,
                Foreground = Res("TextFillColorPrimaryBrush"),
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
            };
            row.Children.Add(check);
            row.Children.Add(new TextBlock
            {
                Text = opt.Description,
                FontSize = 12,
                Foreground = Res("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(22, 1, 0, 0),
            });
            inner.Children.Add(row);
            _tweakChecks.Add((opt, check));
        }
        GeneratorPanel.Children.Add(card);
    }

    private void RefreshGeneratorSummary()
    {
        var apps = GetSelectedApps();
        _genAppsSummary.Text = apps.Count == 0
            ? "No Windows apps are currently ticked in Software › Windows Apps. " +
              "Tick the apps you want removed during installation, then come back here."
            : $"{apps.Count} app(s) ticked in Software › Windows Apps will be removed during installation: " +
              string.Join(", ", apps.Take(12).Select(a => a.Name)) + (apps.Count > 12 ? ", …" : "");
    }

    private List<AppDefinition> GetSelectedApps() => ViewModel.GetSelectedApps();

    private List<UnattendTweakOption> GetSelectedTweaks() =>
        _tweakChecks.Where(tc => tc.Box.IsChecked == true).Select(tc => tc.Option).ToList();
}
