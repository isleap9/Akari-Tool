using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Update;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Windows Updates page. Wave-2 rollout tab — a single-catalog, standard
/// tweak-row tab with no bespoke sections, reusing the Gaming rendering layer.
///
/// Section order matches net8's UpdateTab.Build() exactly: Update Policy →
/// Delivery &amp; Store → Update Behavior. Two of the rows are dropdowns
/// ("Windows Update Policy", "Delivery Optimization").
///
/// Cosmetic note: the net8 UpdateTab centred its content at MaxWidth 860 (one of
/// three intentionally-narrow tabs). This page uses the shared 1100-wide shell
/// (copy of the wave-1 page); the narrower centring is a deferred cosmetic, not a
/// functional difference. Flagged in MIGRATION_LOG.
/// </summary>
public sealed partial class UpdateViewModel : TweakPageViewModel
{
    public UpdateViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Windows Updates";
        Subtitle = "Update policy, delivery optimisation, and update behaviour controls.";
    }

    public override string NavTag => "Update";
    public override string NavLabel => "Windows Updates";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Update Policy", UpdateTweaks.UpdatePolicy(log));
        yield return ("Delivery & Store", UpdateTweaks.DeliveryAndStore(log));
        yield return ("Update Behavior", UpdateTweaks.UpdateBehavior(log));
    }
}
