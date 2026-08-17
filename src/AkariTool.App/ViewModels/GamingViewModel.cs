using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Gaming;
using AkariTool.ViewModels.Tweaks;
using AkariTool.Core.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Gaming &amp; Performance page.
///
/// This is the whole tab: the rendering layer supplies the rows, sections, badges,
/// bulk bars, Quick Actions and search, so the tab itself is only the catalog
/// order. Compare net8's GamingTab.xaml.cs, which had to loop AddTweakRow /
/// AttachBulkActions per section and keep its own _refreshActions list.
///
/// FULLY PORTED. Beyond the 9 catalog sections, the three formerly-bespoke Gaming
/// sections are now catalog sections too, in net8's Build() order:
///   • System Services (Phase 9 — 37 dropdowns + 1 toggle)
///   • Scheduled Tasks (Phase 11 — 18 task toggles)
///   • System Restore  (Phase 19 — 2 toggles; system-restore-protection carries a
///     confirm-on-disable Warning, the one deliberate behavioral add vs net8)
/// No Gaming section remains unported.
/// </summary>
public sealed partial class GamingViewModel : TweakPageViewModel
{
    public GamingViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Gaming & Performance";
        Subtitle = "Game Mode, GPU, CPU, and network tweaks for maximum frame rates.";
    }

    public override string NavTag => "Gaming";
    public override string NavLabel => "Gaming & Performance";

    /// <summary>
    /// Section order matches net8's GamingTab.Build() exactly. The catalogs take an
    /// Action&lt;string&gt; logger, which is the ToolService the shell already
    /// listens to — so every tweak's own log line reaches the docked console.
    /// </summary>
    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Game Mode", GamingTweaks.GameMode(log));
        yield return ("Processor", GamingTweaks.Processor(log));
        yield return ("Graphics", GamingTweaks.Graphics(log));
        yield return ("Storage", GamingTweaks.Storage(log));
        yield return ("Network", GamingTweaks.Network(log));
        yield return ("Xbox", GamingTweaks.Xbox(log));
        yield return ("Security", GamingTweaks.Security(log));
        // Bespoke-turned-catalog section: 37 per-service startup dropdowns + the
        // input-app-preload toggle (Phase 9). net8 rendered this right after
        // Security (before Scheduled Tasks / System Restore, still unported).
        yield return ("System Services", GamingTweaks.SystemServices(log));
        // Phase 11: 18 scheduled-task toggle rows (self-contained schtasks.exe
        // read/apply). net8 rendered this right after System Services.
        yield return ("Scheduled Tasks", GamingTweaks.ScheduledTasks(log));
        // Phase 19: 2 toggles (System Protection on/off for C: + Long File Paths).
        // net8 order: …Scheduled Tasks → System Restore → Accessibility. net8's
        // AddSection titled it "System"; wired as the clearer "System Restore".
        yield return ("System Restore", GamingTweaks.SystemRestore(log));
        yield return ("Accessibility", GamingTweaks.Accessibility(log));
        yield return ("Visual Effects", GamingTweaks.VisualEffects(log));
    }
}
