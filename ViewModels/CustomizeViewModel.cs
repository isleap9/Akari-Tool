using System.Linq;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Customize page — the largest standard catalog-only tab. Reuses the Gaming
/// rendering layer; the tab is just the catalog order.
///
/// Section order matches net8's CustomizeTab.Build() exactly (6 top-level groups,
/// in this order: Taskbar → Explorer → Context Menu → Appearance → Start Menu →
/// Desktop), flattened into one scrolling page of section cards (the wave pattern),
/// with each group's sub-sections in net8's dispatch order.
///
/// Two net8 sub-sections are APPENDED into their parent card (net8 built them into
/// the same StackPanel), so they are concatenated here to reproduce one card each:
///   • Taskbar "Behavior" = TaskbarBehavior + TaskbarBehaviorExtras
///     (net8 BuildTaskbarBehavior calls BuildTaskbarBehaviorExtras(behaviorSection)).
///   • Explorer "View" = ExplorerView + ExplorerViewFolderOptions
///     (net8 BuildExplorerView calls BuildExplorerViewFolderOptions(viewSection)).
///
/// Taskbar ▸ "Button Grouping" is now a catalog section too (Phase 17 — net8's 3
/// hand-made ComboBoxes converted to 3 dropdown TweakDefinitions in
/// GamingTweaks-style catalog form; see CustomizeTweaks.Taskbar.Grouping).
///
/// (net8's nested Customize sub-navigation is not reproduced — this is the flat
/// single-page wave pattern.)
/// </summary>
public sealed partial class CustomizeViewModel : TweakPageViewModel
{
    public CustomizeViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Customize";
        Subtitle = "Taskbar, Explorer, context menu, appearance, Start menu, and desktop tweaks.";
    }

    public override string NavTag => "Customize";
    public override string NavLabel => "Customize";

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        // ── Taskbar ──────────────────────────────────────────────────────────
        yield return ("Layout", CustomizeTweaks.TaskbarLayout(log));
        yield return ("Behavior",
            CustomizeTweaks.TaskbarBehavior(log).Concat(CustomizeTweaks.TaskbarBehaviorExtras(log)).ToArray());
        // Phase 17: net8's 3rd/last Taskbar sub-section (net8 order Layout → Behavior
        // → Grouping). Was 3 hand-made ComboBoxes in net8; now 3 dropdown TweakDefinitions.
        yield return ("Button Grouping", CustomizeTweaks.TaskbarButtonGrouping(log));

        // ── Explorer ─────────────────────────────────────────────────────────
        yield return ("View",
            CustomizeTweaks.ExplorerView(log).Concat(CustomizeTweaks.ExplorerViewFolderOptions(log)).ToArray());
        yield return ("Behavior", CustomizeTweaks.ExplorerBehavior(log));
        yield return ("File Associations", CustomizeTweaks.ExplorerAssociations(log));
        yield return ("Sidebar", CustomizeTweaks.ExplorerSidebar(log));
        yield return ("This PC Folders", CustomizeTweaks.ExplorerThisPc(log));

        // ── Context Menu ─────────────────────────────────────────────────────
        yield return ("Entries", CustomizeTweaks.ContextMenuEntries(log));

        // ── Appearance ───────────────────────────────────────────────────────
        yield return ("Theme", CustomizeTweaks.AppearanceTheme(log));
        yield return ("Transparency & Effects", CustomizeTweaks.AppearanceEffects(log));
        yield return ("Color", CustomizeTweaks.AppearanceColor(log));
        yield return ("Window Style", CustomizeTweaks.AppearanceWindowStyle(log));

        // ── Start Menu ───────────────────────────────────────────────────────
        yield return ("Layout", CustomizeTweaks.StartMenuLayout(log));
        yield return ("Behavior", CustomizeTweaks.StartMenuBehavior(log));

        // ── Desktop ──────────────────────────────────────────────────────────
        yield return ("Desktop Icons", CustomizeTweaks.DesktopIcons(log));
        yield return ("Shortcuts", CustomizeTweaks.DesktopShortcuts(log));
        yield return ("Startup", CustomizeTweaks.DesktopStartup(log));
        yield return ("Devices", CustomizeTweaks.DesktopDevices(log));
        yield return ("Lock Screen", CustomizeTweaks.DesktopLockScreen(log));
        yield return ("Regional Settings", CustomizeTweaks.RegionalSettings(log)); // contains os-set-utc
    }
}
