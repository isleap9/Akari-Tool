using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using AkariTool.Services;

namespace AkariTool.Tabs.Power
{
    public partial class PowerTab
    {

        // ── Plan selector ─────────────────────────────────────────────────────

        private void BuildPlanSelector(StackPanel panel)
        {
            // ── Section header — mono "POWER PLAN" label (matches BuildSection style) ──
            var header = new TextBlock
            {
                Text       = "POWER PLAN",
                FontFamily = MonoFont,
                FontSize   = 10,
                FontWeight = FontWeights.Medium,
                Foreground = TweakHelpers.TextMuted, // rgba(255,255,255,0.42)
                Margin     = new Thickness(4, 0, 0, 6)
            };
            header.Margin = new Thickness(4, 0, 0, 10);
            panel.Children.Add(header);

            // ── 3-column grid of translucent plan cards ──
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // 4th column for the dynamic custom-plan card — collapsed to zero until needed.
            _customPlanColumn = new ColumnDefinition { Width = new GridLength(0) };
            grid.ColumnDefinitions.Add(_customPlanColumn);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var plans = new (string Label, string Desc, Func<string?, string?, bool> Matcher, Func<Task> Action)[]
            {
                ("Balanced", "Windows default balanced power plan",
                 (name, guid) => guid?.ToLower() == BalancedGuid,
                 () => ActivatePlan(BalancedGuid, "Balanced")),
                ("High Performance", "Maximizes performance — disables CPU scaling",
                 (name, guid) => guid?.ToLower() == HighPerfGuid,
                 () => ActivatePlan(HighPerfGuid, "High Performance")),
                ("Ultimate Performance", "Unlocks and activates the hidden Ultimate Performance plan — best for gaming",
                 (name, guid) => guid?.ToLower() == UltimatePerfGuid
                                 || (name?.Contains("Ultimate", StringComparison.OrdinalIgnoreCase) ?? false),
                 () => ActivateUltimatePlan()),
            };

            for (int i = 0; i < plans.Length; i++)
            {
                var (label, desc, matcher, action) = plans[i];
                double left  = i == 0 ? 0 : 4.5;
                double right = i == plans.Length - 1 ? 0 : 4.5;

                var stack = new StackPanel();

                // top row: plan name (left) + ACTIVE tag (right)
                var topRow = new Grid();
                topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameText = new TextBlock
                {
                    Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Foreground = TweakHelpers.TextPrimary, TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(nameText, 0);

                var activeTag = new TextBlock
                {
                    Text = "ACTIVE", FontFamily = MonoFont, FontSize = 9,
                    FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.SuccessFg,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    Visibility = Visibility.Collapsed
                };
                Grid.SetColumn(activeTag, 1);

                topRow.Children.Add(nameText);
                topRow.Children.Add(activeTag);

                var descText = new TextBlock
                {
                    Text = desc, FontSize = 11.5, LineHeight = 16,
                    Foreground = TweakHelpers.TextSecondary, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0)
                };

                stack.Children.Add(topRow);
                stack.Children.Add(descText);

                var card = new Border
                {
                    Background      = CardBg,
                    BorderBrush     = CardBorderIdle,
                    BorderThickness = new Thickness(1),
                    CornerRadius    = TweakHelpers.CardRadius,
                    Padding         = new Thickness(15, 13, 15, 13),
                    Margin          = new Thickness(left, 0, right, 0),
                    Child           = stack
                };
                ToolTipService.SetToolTip(card, desc);

                int idx = i;
                // WinUI pointer events replace WPF mouse events; Tapped replaces
                // MouseLeftButtonUp (and also covers touch/pen).
                card.PointerEntered += (_, _) => card.BorderBrush = CardBorderActive;
                card.PointerExited  += (_, _) => card.BorderBrush =
                    activeTag.Visibility == Visibility.Visible ? CardBorderActive : CardBorderIdle;
                // Highlight the clicked card immediately, then run the (async) activation.
                card.Tapped += async (_, _) => { SetActiveCard(idx); await action(); };

                Grid.SetColumn(card, i);
                grid.Children.Add(card);

                _planCards.Add(card);
                _planActiveTags.Add(activeTag);
                _planMatchers.Add(matcher);
            }

            BuildCustomPlanCard(grid);

            panel.Children.Add(grid);
            RefreshActiveCard();
        }

        // Dynamic 4th card: rendered only when the active plan is custom / OEM
        // (Akari Performance, AkariOS playbook plan, or a vendor plan). Same visual
        // style as the fixed cards; starts collapsed with its column at zero width.
        private void BuildCustomPlanCard(Grid grid)
        {
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _customPlanName = new TextBlock
            {
                Text = "Custom plan", FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = TweakHelpers.TextPrimary, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_customPlanName, 0);

            _customPlanActiveTag = new TextBlock
            {
                Text = "ACTIVE", FontFamily = MonoFont, FontSize = 9,
                FontWeight = FontWeights.SemiBold, Foreground = TweakHelpers.SuccessFg,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(_customPlanActiveTag, 1);

            topRow.Children.Add(_customPlanName);
            topRow.Children.Add(_customPlanActiveTag);

            _customPlanDesc = new TextBlock
            {
                Text = "Custom power plan", FontSize = 11.5, LineHeight = 16,
                Foreground = TweakHelpers.TextSecondary, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            };

            var stack = new StackPanel();
            stack.Children.Add(topRow);
            stack.Children.Add(_customPlanDesc);

            _customPlanCard = new Border
            {
                Background      = CardBg,
                BorderBrush     = CardBorderIdle,
                BorderThickness = new Thickness(1),
                CornerRadius    = TweakHelpers.CardRadius,
                Padding         = new Thickness(15, 13, 15, 13),
                Margin          = new Thickness(4.5, 0, 0, 0),
                Child           = stack,
                Visibility      = Visibility.Collapsed
            };

            _customPlanCard.PointerEntered += (_, _) => _customPlanCard.BorderBrush = CardBorderActive;
            _customPlanCard.PointerExited  += (_, _) => _customPlanCard.BorderBrush =
                _customPlanActiveTag.Visibility == Visibility.Visible ? CardBorderActive : CardBorderIdle;
            _customPlanCard.Tapped += async (_, _) =>
            {
                if (_customPlanGuid is null) return;
                SetActiveCard(-1);
                await ActivatePlan(_customPlanGuid, _customPlanName?.Text ?? "Custom plan");
            };

            Grid.SetColumn(_customPlanCard, 3);
            grid.Children.Add(_customPlanCard);
        }

        // Applies the active border + "ACTIVE" tag to card <index>, clearing all others.
        // index < 0 → no card active (unknown / OEM plan).
        private void SetActiveCard(int index)
        {
            for (int i = 0; i < _planCards.Count; i++)
            {
                bool isActive = i == index;
                _planActiveTags[i].Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                _planCards[i].BorderBrush     = isActive ? CardBorderActive : CardBorderIdle;
            }

            // A built-in card is going active — make sure the custom card never also shows ACTIVE.
            if (index >= 0)
            {
                if (_customPlanActiveTag is not null) _customPlanActiveTag.Visibility = Visibility.Collapsed;
                if (_customPlanCard is not null)      _customPlanCard.BorderBrush     = CardBorderIdle;
            }
        }

        // Re-reads the real active plan and highlights the matching card (none if unknown).
        private void RefreshActiveCard()
        {
            var (name, guid) = SystemStateReader.ReadActivePowerPlan();
            if (guid is null) { SetActiveCard(-1); return; }

            // The registry FriendlyName can be missing/MUI-encoded for a duplicated
            // Ultimate plan; resolve it from `powercfg /list` so the name-aware
            // Ultimate match still works for its non-canonical GUID.
            string effName = name ?? "";
            string g = guid.ToLower();
            if (!effName.Contains("Ultimate", StringComparison.OrdinalIgnoreCase)
                && g != BalancedGuid && g != HighPerfGuid && g != UltimatePerfGuid)
            {
                foreach (var (planGuid, planName) in ListPowerPlans())
                    if (planGuid.Equals(guid, StringComparison.OrdinalIgnoreCase))
                    { if (planName.Length > 0) effName = planName; break; }
            }

            int active = -1;
            for (int i = 0; i < _planMatchers.Count; i++)
                if (_planMatchers[i](effName, guid)) { active = i; break; }

            if (active >= 0)
            {
                // A built-in plan is active — hide the custom card.
                _customPlanGuid = null;
                if (_customPlanCard  is not null) _customPlanCard.Visibility = Visibility.Collapsed;
                if (_customPlanColumn is not null) _customPlanColumn.Width  = new GridLength(0);
                SetActiveCard(active);
            }
            else
            {
                // Custom / OEM plan active (Akari Performance, AkariOS, vendor plan) — show it.
                _customPlanGuid = guid;
                if (_customPlanName is not null)
                    _customPlanName.Text = string.IsNullOrWhiteSpace(effName) ? "Custom plan" : effName;
                if (_customPlanCard is not null)
                {
                    _customPlanCard.Visibility  = Visibility.Visible;
                    _customPlanCard.BorderBrush = CardBorderActive;
                }
                if (_customPlanColumn is not null) _customPlanColumn.Width = new GridLength(1, GridUnitType.Star);
                if (_customPlanActiveTag is not null) _customPlanActiveTag.Visibility = Visibility.Visible;
                SetActiveCard(-1);   // clears the three built-in cards
            }
        }

        private async Task ActivatePlan(string guid, string name)
        {
            Service?.Log($"Activating {name} plan...");
            int exit = await Service!.RunProcess("powercfg", $"/setactive {guid}", timeoutMilliseconds: 10_000);
            if (exit == 0) { Service.Log($"{name} plan activated."); RootPanel.DispatcherQueue.TryEnqueue(() => { RefreshActiveCard(); RefreshPersistIndicator(); }); }
            else Service.Log($"Failed to activate {name} (exit {exit}).");
        }

        private async Task ActivateUltimatePlan()
        {
            Service?.Log("Activating Ultimate Performance plan...");

            // Reuse an existing Ultimate plan if one is already present — never
            // /duplicatescheme when it exists, or repeated clicks pile up copies.
            string? existing = await Task.Run(FindUltimatePlanGuid);
            if (existing is not null)
            {
                await SetActiveUltimate(existing);
                return;
            }

            // None present — create it exactly once, then find + activate the new GUID.
            Service?.Log("No Ultimate Performance plan found — creating it...");
            await Service!.RunProcess("powercfg", $"/duplicatescheme {UltimatePerfGuid}", timeoutMilliseconds: 10_000);

            string? created = await Task.Run(FindUltimatePlanGuid);
            if (created is not null) { await SetActiveUltimate(created); return; }

            Service?.Log("Could not create or activate Ultimate Performance plan.");
        }

        private async Task SetActiveUltimate(string guid)
        {
            int act = await Service!.RunProcess("powercfg", $"/setactive {guid}", timeoutMilliseconds: 10_000);
            if (act == 0) { Service.Log($"Ultimate Performance plan activated (GUID: {guid})."); RootPanel.DispatcherQueue.TryEnqueue(() => { RefreshActiveCard(); RefreshPersistIndicator(); }); }
            else Service.Log($"Failed to activate Ultimate Performance (exit {act}).");
        }

        // Returns the GUID of an already-present Ultimate Performance plan, or null.
        // Matches by canonical base GUID or by friendly name (covers duplicated plans).
        private static string? FindUltimatePlanGuid()
        {
            foreach (var (guid, name) in ListPowerPlans())
                if (guid.Equals(UltimatePerfGuid, StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
                    return guid;
            return null;
        }

        // Parses `powercfg /list` into (GUID, FriendlyName) pairs.
        private static List<(string Guid, string Name)> ListPowerPlans()
        {
            var result = new List<(string, string)>();
            foreach (var line in RunPowerCfg("/list").Split('\n'))
            {
                int gi = line.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
                if (gi < 0) continue;
                var rest = line.Substring(gi + 5).Trim();
                var guid = rest.Split(' ')[0].Trim();
                if (guid.Length != 36) continue;

                string name = "";
                int p1 = rest.IndexOf('(');
                int p2 = rest.LastIndexOf(')');
                if (p1 >= 0 && p2 > p1) name = rest.Substring(p1 + 1, p2 - p1 - 1).Trim();

                result.Add((guid, name));
            }
            return result;
        }

    }
}
