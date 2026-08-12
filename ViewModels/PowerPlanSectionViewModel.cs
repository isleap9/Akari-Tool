using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Power;

namespace AkariTool.ViewModels;

/// <summary>
/// The bespoke Power ▸ Plan Selector + Persist Indicator section (Phase 21). NOT a
/// TweakDefinition / does NOT register with TweakRegistry (matches Gaming's bespoke
/// precedent). Faithful MVVM port of net8 PowerTab.PlanSelector.cs +
/// PowerTab.Persistence.cs — every read/write call, label, and behaviour
/// byte-identical, INCLUDING the no-confirmation "Revert to Balanced" delete (isleap:
/// port verbatim, no dialogs added).
///
/// Scheme reads reuse the (now internal) PowerTweaks statics:
///   • RefreshActiveCard  → SystemStateReader.ReadActivePowerPlan() + ListPowerPlans()
///   • RefreshPersistIndicator → ResolveSchemeTarget() + SchemeInactive
/// Plan-switch / revert are the ONLY writes and use powercfg directly (net8 used
/// Service.RunProcess), exactly as net8:
///   • ActivatePlan        → powercfg /setactive {guid}
///   • ActivateUltimatePlan→ powercfg /duplicatescheme + /setactive
///   • RevertToBalanced    → powercfg /setactive {Balanced} + /delete {Akari GUID}
/// This VM never calls SetPowerCfg / EnsureAkariScheme; the plan-switch write path is
/// net8's own (separate from the catalog's per-setting writes).
/// </summary>
public sealed partial class PowerPlanSectionViewModel : ObservableObject
{
    private readonly ToolService _tool;
    private readonly Action _requestCatalogRefresh;   // net8 RevertToBalanced re-ran _refreshActions

    // Descriptions verbatim from net8 BuildPlanSelector.
    public PowerPlanSectionViewModel(ToolService tool, Action requestCatalogRefresh)
    {
        _tool = tool;
        _requestCatalogRefresh = requestCatalogRefresh;

        _balanced = new PlanCardViewModel(PlanKind.Balanced, "Balanced",
            "Windows default balanced power plan", ActivateCardAsync);
        _highPerf = new PlanCardViewModel(PlanKind.HighPerformance, "High Performance",
            "Maximizes performance — disables CPU scaling", ActivateCardAsync);
        _ultimate = new PlanCardViewModel(PlanKind.Ultimate, "Ultimate Performance",
            "Unlocks and activates the hidden Ultimate Performance plan — best for gaming", ActivateCardAsync);
        _custom = new PlanCardViewModel(PlanKind.Custom, "Custom plan", "Custom power plan", ActivateCardAsync)
        { IsVisible = false };

        Cards = new ObservableCollection<PlanCardViewModel> { _balanced, _highPerf, _ultimate, _custom };
        // Detection is deferred to Refresh() (UI thread) — the ctor runs on the
        // warm-up background thread and status-brush lookups are UI-thread-affine.
    }

    private readonly PlanCardViewModel _balanced, _highPerf, _ultimate, _custom;
    private string? _customPlanGuid;

    public ObservableCollection<PlanCardViewModel> Cards { get; }

    // ── Persist indicator ─────────────────────────────────────────────────────

    [ObservableProperty] public partial string PersistText { get; set; } = "Detecting…";
    [ObservableProperty] public partial Brush? PersistBrush { get; set; }
    [ObservableProperty] public partial bool RevertVisible { get; set; }

    /// <summary>Run both read-only repaints (plan cards + persist indicator). UI thread.</summary>
    public void Refresh()
    {
        RefreshActiveCard();
        RefreshPersistIndicator();
    }

    // ── RefreshActiveCard — READ-ONLY (net8 verbatim) ─────────────────────────
    private void RefreshActiveCard()
    {
        var (name, guid) = SystemStateReader.ReadActivePowerPlan();
        if (guid is null) { SetActiveCard(null); return; }

        string effName = name ?? "";
        string g = guid.ToLower();
        if (!effName.Contains("Ultimate", StringComparison.OrdinalIgnoreCase)
            && g != PowerTweaks.BalancedGuid && g != PowerTweaks.HighPerfGuid && g != PowerTweaks.UltimatePerfGuid)
        {
            foreach (var (planGuid, planName) in PowerTweaks.ListPowerPlans())
                if (planGuid.Equals(guid, StringComparison.OrdinalIgnoreCase))
                { if (planName.Length > 0) effName = planName; break; }
        }

        PlanCardViewModel? active =
            (g == PowerTweaks.BalancedGuid) ? _balanced :
            (g == PowerTweaks.HighPerfGuid) ? _highPerf :
            (g == PowerTweaks.UltimatePerfGuid
             || effName.Contains("Ultimate", StringComparison.OrdinalIgnoreCase)) ? _ultimate :
            null;

        if (active is not null)
        {
            _customPlanGuid = null;
            _custom.IsVisible = false;
            SetActiveCard(active);
        }
        else
        {
            // Custom / OEM plan active (Akari Performance, AkariOS, vendor plan) — show it.
            _customPlanGuid = guid;
            _custom.Label = string.IsNullOrWhiteSpace(effName) ? "Custom plan" : effName;
            _custom.IsVisible = true;
            SetActiveCard(_custom);
        }
    }

    // Highlights <card> (null = none active), clearing the others.
    private void SetActiveCard(PlanCardViewModel? card)
    {
        foreach (var c in Cards) c.IsActive = ReferenceEquals(c, card);
    }

    // ── RefreshPersistIndicator — READ-ONLY (net8 verbatim) ───────────────────
    private void RefreshPersistIndicator()
    {
        bool active = PowerTweaks.ResolveSchemeTarget() != null;
        bool drifted = active && PowerTweaks.SchemeInactive;

        (string Text, string BrushKey) v = !active
            ? ("Power plan: not persisted yet — first change creates the Akari Performance plan", "TextFillColorSecondaryBrush")
            : drifted
                ? ($"Power plan: {PowerTweaks.AkariPlanName} exists but is not active — the next change reactivates it", "SystemFillColorCautionBrush")
                : ($"Power plan: {PowerTweaks.AkariPlanName} (persistent)", "SystemFillColorSuccessBrush");

        PersistText = v.Text;
        PersistBrush = ResolveBrush(v.BrushKey);
        RevertVisible = active;
    }

    private static Brush? ResolveBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var b) ? b as Brush : null;

    // ── Plan-switch commands — the ONLY writes (net8 verbatim) ────────────────

    private async Task ActivateCardAsync(PlanCardViewModel card)
    {
        SetActiveCard(card);   // optimistic highlight (net8 SetActiveCard before await)
        switch (card.Kind)
        {
            case PlanKind.Balanced:       await ActivatePlan(PowerTweaks.BalancedGuid, "Balanced"); break;
            case PlanKind.HighPerformance:await ActivatePlan(PowerTweaks.HighPerfGuid, "High Performance"); break;
            case PlanKind.Ultimate:       await ActivateUltimatePlan(); break;
            case PlanKind.Custom:
                if (_customPlanGuid is not null)
                    await ActivatePlan(_customPlanGuid, string.IsNullOrWhiteSpace(_custom.Label) ? "Custom plan" : _custom.Label);
                break;
        }
    }

    private async Task ActivatePlan(string guid, string name)
    {
        _tool.Log($"Activating {name} plan...");
        int exit = await _tool.RunProcess("powercfg", $"/setactive {guid}", timeoutMilliseconds: 10_000);
        if (exit == 0) { _tool.Log($"{name} plan activated."); App.DispatcherQueue?.TryEnqueue(Refresh); }
        else _tool.Log($"Failed to activate {name} (exit {exit}).");
    }

    private async Task ActivateUltimatePlan()
    {
        _tool.Log("Activating Ultimate Performance plan...");

        // Reuse an existing Ultimate plan if present — never /duplicatescheme when it
        // exists, or repeated clicks pile up copies (net8).
        string? existing = await Task.Run(FindUltimatePlanGuid);
        if (existing is not null) { await SetActiveUltimate(existing); return; }

        _tool.Log("No Ultimate Performance plan found — creating it...");
        await _tool.RunProcess("powercfg", $"/duplicatescheme {PowerTweaks.UltimatePerfGuid}", timeoutMilliseconds: 10_000);

        string? created = await Task.Run(FindUltimatePlanGuid);
        if (created is not null) { await SetActiveUltimate(created); return; }

        _tool.Log("Could not create or activate Ultimate Performance plan.");
    }

    private async Task SetActiveUltimate(string guid)
    {
        int act = await _tool.RunProcess("powercfg", $"/setactive {guid}", timeoutMilliseconds: 10_000);
        if (act == 0) { _tool.Log($"Ultimate Performance plan activated (GUID: {guid})."); App.DispatcherQueue?.TryEnqueue(Refresh); }
        else _tool.Log($"Failed to activate Ultimate Performance (exit {act}).");
    }

    // Returns the GUID of an already-present Ultimate Performance plan, or null (net8).
    private static string? FindUltimatePlanGuid()
    {
        foreach (var (guid, name) in PowerTweaks.ListPowerPlans())
            if (guid.Equals(PowerTweaks.UltimatePerfGuid, StringComparison.OrdinalIgnoreCase)
                || name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
                return guid;
        return null;
    }

    // ── Revert to Balanced — a destructive WRITE, NO confirmation (net8 verbatim) ──
    [RelayCommand]
    private async Task RevertToBalancedAsync()
    {
        _tool.Log("Power: reverting to the Balanced plan...");

        int exit = await _tool.RunProcess("powercfg", $"/setactive {PowerTweaks.BalancedGuid}", timeoutMilliseconds: 10_000);
        if (exit != 0)
        {
            _tool.Log($"Power: failed to activate Balanced (exit {exit}) — Akari scheme left in place.");
            return;
        }

        var guid = PowerTweaks.ReadStoredSchemeGuid();
        if (guid != null)
            await _tool.RunProcess("powercfg", $"/delete {guid}", timeoutMilliseconds: 10_000);
        TweakHelpers.ClearState(PowerTweaks.SchemeGuidValue);

        PowerTweaks.ResetSchemeCacheAfterRevert();   // net8: _schemeTarget=null; _schemeResolved=true

        _tool.Log("Power: Balanced plan active; Akari Performance scheme removed.");
        App.DispatcherQueue?.TryEnqueue(() =>
        {
            Refresh();
            _requestCatalogRefresh();   // net8 re-ran _refreshActions (re-read catalog rows)
        });
    }
}

public enum PlanKind { Balanced, HighPerformance, Ultimate, Custom }

/// <summary>One plan card (data for the plan-card DataTemplate).</summary>
public sealed partial class PlanCardViewModel : ObservableObject
{
    public PlanCardViewModel(PlanKind kind, string label, string description, Func<PlanCardViewModel, Task> activate)
    {
        Kind = kind;
        Label = label;
        Description = description;
        ActivateCommand = new AsyncRelayCommand(() => activate(this));
    }

    public PlanKind Kind { get; }

    /// <summary>Click-to-activate for this card (dispatches to the section's activator).</summary>
    public IAsyncRelayCommand ActivateCommand { get; }

    [ObservableProperty] public partial string Label { get; set; }
    [ObservableProperty] public partial string Description { get; set; }
    [ObservableProperty] public partial bool IsActive { get; set; }
    [ObservableProperty] public partial bool IsVisible { get; set; } = true;
}
