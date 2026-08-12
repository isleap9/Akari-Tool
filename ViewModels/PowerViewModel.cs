using System.Collections.ObjectModel;
using AkariTool.Services;
using AkariTool.Tabs;
using AkariTool.Tabs.Power;
using AkariTool.ViewModels.Tweaks;

namespace AkariTool.ViewModels;

/// <summary>
/// Power page — standard catalog-only tab (the 15 powercfg tweak-row sections),
/// reusing the Gaming rendering layer.
///
/// Section order matches net8's PowerTab.Build() catalog order exactly (Display …
/// Start Menu Power Options). Battery + GPU Power are hardware-gated inside their
/// catalog methods (return empty on a battery-less / no-vendor-GPU machine), so
/// those sections drop out via TweakPageViewModel.Build's `items.Count == 0`
/// guard — the gating is preserved, not "fixed".
///
/// The bespoke **Plan Selector + Persist Indicator** (Phase 21 — plan cards +
/// persistent-scheme indicator + Revert to Balanced) render at the TOP, above the
/// catalog sections, matching net8's PowerTab.Build order. That section is NOT a
/// TweakDefinition and does NOT register with TweakRegistry (see
/// <see cref="PowerPlanSectionViewModel"/>); it is interleaved via
/// <see cref="DisplayItems"/> + a template selector.
/// </summary>
public sealed partial class PowerViewModel : TweakPageViewModel
{
    public PowerViewModel(TweakDialogs dialogs, ToolService tool) : base(dialogs, tool)
    {
        Title = "Power";
        Subtitle = "Power plan management and advanced power configuration.";

        // Bespoke Plan Selector VM. Its Revert path re-reads the catalog rows (net8
        // re-ran _refreshActions after revert), so it gets a read-only refresh callback.
        PlanSection = new PowerPlanSectionViewModel(tool, RefreshCatalogRows);

        // HEADLESS-EVENT SUBSCRIBER: PowerTweaks.PowerSchemeChanged. Static event +
        // singleton VM → no unsubscribe needed. Subscribing in the ctor (runs during
        // warm-up) is safe: the event only fires from a WRITE (a user applying a power
        // tweak), never during Build(), and if it somehow fired before Build the
        // handler just iterates an empty Sections collection.
        PowerTweaks.PowerSchemeChanged += OnPowerSchemeChanged;
    }

    public override string NavTag => "Power";
    public override string NavLabel => "Power";

    /// <summary>The bespoke Plan Selector + Persist Indicator section (top of page).</summary>
    public PowerPlanSectionViewModel PlanSection { get; }

    /// <summary>
    /// What the Power page's single ItemsControl binds to: the Plan Selector section
    /// first, then the catalog <see cref="TweakSectionViewModel"/>s. A template
    /// selector renders each by type. Kept separate from the base <c>Sections</c> so
    /// search / Quick Actions never see the bespoke card and the tweak-row
    /// registration / range tiling is untouched (Gaming/Phase-7 precedent).
    /// </summary>
    public ObservableCollection<object> DisplayItems { get; } = new();

    private bool _composed;

    /// <summary>
    /// Builds <see cref="DisplayItems"/> (plan section, then the catalog sections) and
    /// runs the read-only plan/persist detection. Call on the UI thread after
    /// <c>Build()</c> (the page ctor does). Idempotent.
    /// </summary>
    public void ComposeDisplay()
    {
        if (_composed) return;
        _composed = true;

        DisplayItems.Add(PlanSection);          // TOP — above the catalog sections
        foreach (var section in Sections)
            DisplayItems.Add(section);

        PlanSection.Refresh();                  // read-only detection on load
    }

    /// <summary>Read-only re-read of every catalog Power row (used by the Revert path).</summary>
    private void RefreshCatalogRows()
    {
        foreach (var section in Sections)
        {
            foreach (var row in section.Items)
                row.RefreshFromSystem();
            section.RefreshPendingPill();
        }
        RefreshQuickActionCounts();
    }

    protected override IEnumerable<(string Title, TweakDefinition[] Tweaks)> BuildCatalog()
    {
        Action<string> log = Tool.Log;

        yield return ("Display", PowerTweaks.Display(log));
        yield return ("Hard Disk", PowerTweaks.HardDisk(log));
        yield return ("Internet Explorer", PowerTweaks.InternetExplorer(log));
        yield return ("Desktop Background Settings", PowerTweaks.DesktopBackground(log));
        yield return ("Wireless Adapter Settings", PowerTweaks.WirelessAdapter(log));
        yield return ("Sleep", PowerTweaks.Sleep(log));
        yield return ("Battery", PowerTweaks.Battery(log));                       // hardware-gated
        yield return ("USB Settings", PowerTweaks.USB(log));
        yield return ("PCI Express", PowerTweaks.PciExpress(log));
        yield return ("GPU Power", PowerTweaks.GpuPower(log));                    // hardware-gated
        yield return ("Processor Power Management", PowerTweaks.ProcessorPower(log));
        yield return ("Processor Advanced Settings", PowerTweaks.ProcessorAdvanced(log));
        yield return ("Multimedia Settings", PowerTweaks.MultimediaSettings(log));
        yield return ("Power Buttons and Lid", PowerTweaks.PowerButtons(log));
        yield return ("Start Menu Power Options", PowerTweaks.StartMenuPower(log));
    }

    // ── PowerSchemeChanged subscriber — READ-ONLY (reviewed invariant) ────────
    /// <summary>
    /// Repaints the Power tab after a power write. **READ-ONLY** — this is the one
    /// wrong-direction bug CLAUDE.md flags by name, so the invariant is explicit:
    ///
    /// PowerTweaks raises <c>PowerSchemeChanged</c> ONLY from its WRITE path
    /// (<c>SetPowerCfg</c> / <c>EnsureAkariScheme</c>), AFTER the powercfg writes +
    /// <c>/SETACTIVE</c> + drift-clear have already run. This handler only RE-READS
    /// and repaints so sibling dropdowns reflect the now-active Akari Performance
    /// scheme. Every call it makes is read-only:
    ///   • <c>row.RefreshFromSystem()</c> → <c>ReadCurrentIndex</c> → <c>QueryPowerCfg</c>
    ///     = <c>powercfg /QUERY</c> (read) + <c>ResolveSchemeTarget</c> (registry read);
    ///     the row applies the read value through its SUPPRESSED setter, so re-reading
    ///     never re-enters the apply path.
    ///   • <c>section.RefreshPendingPill()</c> / <c>RefreshQuickActionCounts()</c> only
    ///     read row state.
    ///
    /// It NEVER calls <c>SetPowerCfg</c>, <c>EnsureAkariScheme</c>, or
    /// <c>powercfg /SETACTIVE</c> — it carries no power-state authority.
    ///
    /// Phase 21: it now ALSO calls <c>PlanSection.Refresh()</c> — the bespoke Plan
    /// Selector + Persist Indicator repaint, which is likewise READ-ONLY
    /// (RefreshActiveCard → ReadActivePowerPlan/ListPowerPlans reads;
    /// RefreshPersistIndicator → ResolveSchemeTarget() + SchemeInactive reads). Still
    /// nothing here writes / reactivates a scheme.
    ///
    /// Marshaled to the UI thread because it mutates bound collections/properties.
    /// </summary>
    private void OnPowerSchemeChanged()
    {
        App.DispatcherQueue?.TryEnqueue(() =>
        {
            foreach (var section in Sections)
            {
                foreach (var row in section.Items)
                    row.RefreshFromSystem();   // read-only re-read (QueryPowerCfg)
                section.RefreshPendingPill();
            }
            RefreshQuickActionCounts();
            PlanSection.Refresh();             // read-only plan-card + persist-indicator repaint
        });
    }
}
