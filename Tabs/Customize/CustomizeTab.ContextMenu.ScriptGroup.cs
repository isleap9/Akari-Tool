using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs
{
    public partial class CustomizeTab
    {
        // ── Script group helper ──
        // NOTE: BuildScriptGroup has no callers anywhere in the codebase. It is
        // parked here unchanged rather than deleted — removing it is a separate
        // decision from the pass-A split.

        private void BuildScriptGroup(StackPanel panel, string groupTitle,
            (string Title, string Desc, string Script, string Undo)[] items)
        {
            panel.Children.Add(new TextBlock
            {
                Text       = groupTitle,
                FontSize   = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary,
                Margin     = new Thickness(0, 12, 0, 6)
            });

            var card = new Border
            {
                Background      = TweakHelpers.CardBg,
                BorderBrush     = TweakHelpers.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius    = TweakHelpers.CardRadius
            };
            var stack = new StackPanel { Margin = new Thickness(16, 8, 16, 8) };

            for (int i = 0; i < items.Length; i++)
            {
                var (title, desc, script, undo) = items[i];
                if (i > 0) stack.Children.Add(new Separator { Background = TweakHelpers.Hairline, Height = 1, Margin = new Thickness(-16, 0, -16, 0) });

                var row = new Grid { Margin = new Thickness(0, 10, 0, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();
                Grid.SetColumn(info, 0);
                info.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.TextPrimary });
                info.Children.Add(new TextBlock { Text = desc, FontSize = 12, Foreground = TweakHelpers.TextSecondary, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });

                var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                Grid.SetColumn(btns, 1);

                var capturedTitle  = title;
                var capturedScript = script;
                var capturedUndo   = undo;

                var runBtn = new Button { Content = "Run", Style = (Style)FindResource("RunBtn") };
                runBtn.Click += async (_, _) =>
                    await Service!.RunWithTracking(new ScriptAction(capturedScript), capturedTitle, AppliedTweaks);
                btns.Children.Add(runBtn);

                if (!string.IsNullOrEmpty(undo))
                {
                    var undoBtn = new Button { Content = "Undo", Style = (Style)FindResource("UndoBtn") };
                    undoBtn.Click += async (_, _) => await Service!.RunAction(new ScriptAction(capturedUndo));
                    btns.Children.Add(undoBtn);
                }

                row.Children.Add(info);
                row.Children.Add(btns);
                stack.Children.Add(row);
            }

            card.Child = stack;
            panel.Children.Add(card);
        }
    }
}
