# Roadmap: Akari Tool

**Milestone:** v1.0 Winhance Parity
**Granularity:** fine (10 phases)
**Mode:** mvp

## Overview

Akari's tuning data is already at content parity with Winhance — ~430 settings, external-app
categories matching one-for-one. What is not at parity is the structure: 11 Core domains against 3
Infrastructure domains and 3 App domains, plus eight legacy top-level App directories, a
`ServiceLocator` acting as a second DI channel, two dead subsystems shipping as working-looking
features, and localization and mode engines that exist as contracts with no producers. This
milestone finishes the vertical-slice migration onto Winhance's shape, kills the ambiguity that
makes it hard, and then builds the machinery that is genuinely missing.

The ordering is not arbitrary and three of its placements are counter-intuitive on purpose:

- **Restructure before features.** Every domain phase is cheaper on correct structure.
- **Localization sits at the top of the feature stage, not the bottom.** Winhance derives
  localization keys from `SettingDefinition`, so the key surface grows with the catalog. A setting
  authored after localization ships goes un-localized *invisibly*, because the key-reference test
  only fails for keys that are actually requested. Phase 6 therefore precedes every phase that adds
  a page or a setting.
- **Enablers are not "later cleanup."** `CORE-01..06` and `BUG-01..06` land in Phases 2 and 5
  because each domain phase depends on them, not because they are tidy-up.

## What counts as a passing build (applies to every phase)

A phase's build passes when, via VS MSBuild only —
`AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64` — every assembly in the solution is
emitted: Core, Infrastructure, WinGet.Interop, WinUI.Framework, App, and both test assemblies. Zero
compile errors, and no new warnings beyond the counts recorded in Phase 1.

The App project's `WINAPPSDKGENERATEPROJECTPRIFILE` errors **PRI175 / PRI252** are pre-existing and
explicitly Out of Scope (verified 2026-10-05: the solution compiles all six assemblies; the PRI
failure is because `vendor/WinUI.Framework`'s PRI is not generated at the expected path). They are
tolerated and must never be counted as a phase regression — and equally, they must never be used to
excuse a real compile error hiding behind them. Each phase's **Build Gate** line adds its own
phase-specific bar on top of this.

## Phases

- [ ] **Phase 1: Baseline, Spikes & Test Harness** - Record the build/warning baseline, settle the
      2.3.1 control-compatibility and setting-ID questions, and create the App test project
- [ ] **Phase 2: Dead Subsystems & Defect Repairs** - Make Verify actually verify, enforce setting-ID
      uniqueness, resolve the type ambiguities, delete the dead subsystems, adopt OTS detection
- [ ] **Phase 3: Namespace Alignment & Composition Root** - Every namespace matches its folder; every
      service resolves from one composition root; `ServiceLocator` is gone
- [ ] **Phase 4: Vertical Slices Across All Three Layers** - Six domains mirrored in Core,
      Infrastructure and UI; all eight legacy App directories retired
- [ ] **Phase 5: Common Machinery & Shared Rendering** - Core contracts at parity, one section-page
      base, domain markers, shared row primitives, data-built hubs, one aggregation point
- [ ] **Phase 6: Preferences & Localization** - One JSON preference store plus live-switchable
      localization with keys derived from `SettingDefinition`
- [ ] **Phase 7: Advanced Tools Split** - WIM/ISO and autounattend become their own pages behind
      DI-registered services; embedded scripts are exposed as a folder
- [ ] **Phase 8: Builder & Config Review Modes** - Stage changes, review as a set, and record the
      edit types Winhance silently drops
- [ ] **Phase 9: Software & Apps Machinery** - One page, two tabs, three view modes, sorting, help
      content, and the full install chain including Chocolatey recovery
- [ ] **Phase 10: Customize Slice Completion** - Wallpaper control, Windows-theme handler, and the 12
      Desktop settings folded into Explorer

## Phase Details

### Phase 1: Baseline, Spikes & Test Harness

**Goal**: Establish a recorded build baseline, answer the two compatibility questions that gate
every later UI phase, and create the test project that makes a restructure verifiable at all.
**Mode**: mvp
**Depends on**: Nothing (first phase)
**Requirements**: SPIKE-01, SPIKE-02, SPIKE-03, TEST-01
**Success Criteria** (what must be TRUE):
  1. A recorded baseline document states which assemblies the solution build emits and the exact
     warning and error counts, and explicitly names the PRI175/PRI252 packaging error as pre-existing
     and tolerated — so later phases have a number to compare against.
  2. A spike renders `SettingsCard`, `DataGrid` and `WrapPanel` under WindowsAppSDK 2.3.1 with
     `CommunityToolkit.WinUI`, and the written result is either "all three render" or a named list of
     substitutes to use instead. The finding exists before Phase 4 is planned.
  3. A generated report diffs every Akari setting ID against Winhance's and lists the divergences,
     so config-format divergence is measured rather than assumed — this report is the evidence base
     for the deliberate config fork (D6).
  4. An `AkariTool.App.Tests` project exists in the solution, builds, and runs at least one passing
     test against App-layer logic.
**Build Gate**: all solution assemblies emit; the new test project runs green; test count and
warning/error counts recorded as the Phase 1 reference.
**Plans**: 4 plans
- [ ] 01-01-PLAN.md — **Tracer (TEST-01 + D-03).** `tools/run-tests.ps1` proves the loop end to end (vswhere discovery → MSBuild → vstest → a measured count); `AkariTool.App.Tests` joins `AkariTool.sln` with D-01's `UseWinUI` buildability probed and any deviation recorded; `SettingBadgeCalculatorTests` characterises `Compute` as Phase 5's CORE-04 baseline. Wave 1. Carries the `checkpoint:decision` that fixes the runner's path and name.
- [ ] 01-02-PLAN.md — **SPIKE-01.** `-Mode Record | Baseline | Allowlist` turns the runner into the machine-checked gate: forced `/t:Rebuild`, error-list-matched PRI175/PRI252 allowlist, no-increase solution-wide tolerance. Records `tools/baseline.json` and `01-BASELINE.md`, then proves the gate red three ways before recording it green. Wave 2.
- [ ] 01-03-PLAN.md — **SPIKE-02.** A throwaway spike outside the solution renders `SettingsCard`, `DataGrid` and `WrapPanel` under WindowsAppSDK 2.3.1 with the registry-verified pinned package set; the human render check produces the verdict (any substitute proved on the same page), then the spike is deleted. Wave 2. Hard gate for Phases 4, 5 and 9.
- [ ] 01-04-PLAN.md — **SPIKE-03.** A reflection-based enumerator over the **15** static catalog factories plus a committed read-only Winhance snapshot feed a generator emitting a raw **and** normalised divergence report — the D6 config-format fork evidence base. Wave 3.
**Rationale**: Nothing here is referenced by production code, so the app is byte-for-byte unaffected
and keeps working; every artifact produced is a document or a test that only *observes*. This is the
only phase that can change the definition of "did it build" without consequence, so it goes first.
The SPIKE-02 finding is a hard gate: if `SettingsCard`/`DataGrid`/`WrapPanel` do not resolve under
2.3.1, the Phase 5 row primitives and the Phase 9 table view are planned against substitutes instead,
and finding that out after planning those phases wastes the planning.

### Phase 2: Dead Subsystems & Defect Repairs

**Goal**: Make the features that silently do nothing actually work, and remove the type ambiguities
that would otherwise be carried through the restructure.
**Mode**: mvp
**Depends on**: Phase 1
**Requirements**: BUG-01, BUG-02, BUG-03, BUG-04, BUG-05, BUG-06
**Success Criteria** (what must be TRUE):
  1. The Verify page lists the settings actually in effect and flags the ones that have drifted; after
     applying a setting, the page shows it and the permanent "Nothing tracked yet" empty state is gone.
  2. An app start with a duplicated setting ID fails loudly, naming the duplicate, and a test
     enumerates every real catalog's `Build()` methods and validates them — so the backup-file key
     invariant is enforced rather than only documented.
  3. `AkariPaths` resolves to exactly one definition: no CS0433 or CS0436 at any call site, and
     `SoftwareAppService` provably reads the Core copy at all four of its uses.
  4. `TweakRegistry`, `TweakTargets`, `TweakDefinition` and `UpdateTweaks.cs` no longer exist in the
     tree, exactly one `DispatcherService` type remains, and Infrastructure carries no
     `Microsoft.UI.Dispatching` import — with the solution still building.
  5. Running the app under OTS elevation (admin consented with different credentials) is detected and
     reported, naming the interactive user whose profile per-user settings will target.
**Build Gate**: baseline bar, plus the real-catalog validation test green and no new warnings from
the deletions.
**Plans**: TBD
**Rationale**: Each of these is 1–5 files and each is verified by the Phase 1 safety net, and each
removes an ambiguity that would otherwise survive ~300 file moves — the `AkariPaths` duplicate in
particular is a CS0433 landmine sitting inside the namespace the Phase 3 rename rewrites. Doing them
after the restructure converts a 2-line deletion into a hunt in a new directory. OTS detection lands
here, before more per-user (HKCU) write sites move: retrofitting it later means auditing every
affected write site, which is exactly the exposure Winhance's OTS path exists to prevent.
Leaves the app working because nothing navigates or renames — only behaviour defects are corrected.

### Phase 3: Namespace Alignment & Composition Root

**Goal**: Make every type findable by its folder, and every service resolvable from one composition
root instead of a global locator.
**Mode**: mvp
**Depends on**: Phase 2
**Requirements**: ARCH-08, ARCH-09, ARCH-11, TEST-02, TEST-03
**Success Criteria** (what must be TRUE):
  1. Searching a domain's expected namespace (for example `AkariTool.Core.Features.Optimize`) finds
     its code; no type is declared in `AkariTool.Tabs` or `AkariTool.Services`, and Core,
     Infrastructure and App each compile in a commit that touches only that assembly.
  2. The app starts and operates with zero `ServiceLocator` call sites remaining, and every page
     resolves its ViewModel from the container in its constructor.
  3. An integration test builds the real container and resolves every registered service, and
     `IDispatcherService` resolves to the single expected implementation.
  4. A test asserts the Core assembly references neither Infrastructure nor UI.
  5. Every registration is explicit — no assembly scanning — and each domain has its own registration
     method under a composition root that documents its registration-order dependency.
**Build Gate**: baseline bar, plus Core compiles standalone before Infrastructure is touched, plus
the container-resolution test green.
**Plans**: TBD
**Rationale**: Two orderings inside this phase are load-bearing. Namespace normalization runs
*before* the Phase 4 file moves, so any later failure has exactly one cause — a move or a rename,
never both. The composition root is created *before* `ServiceLocator` is removed, so the 48
eliminated call sites are replaced by a container the smoke test can verify, instead of by call-site
edits with nothing checking them. Leaves the app working because the locator's removal is
constructor injection into types that already resolved the same objects at runtime — same instances,
same lifetimes, one verified container.
**Deliberate non-replication**: domain Infrastructure services stay registered in Infrastructure.
Winhance registers them from the UI layer as a documented asymmetry; adopting that would make
Infrastructure unreachable from an Infrastructure-only container and is recorded in the composition
root comment as a conscious divergence.

### Phase 4: Vertical Slices Across All Three Layers

**Goal**: Put all six domains at mirrored relative paths across Core, Infrastructure and UI, and
retire every legacy top-level App directory.
**Mode**: mvp
**Depends on**: Phase 3
**Requirements**: ARCH-01, ARCH-02, ARCH-06, ARCH-07, ARCH-10, ARCH-12
**Success Criteria** (what must be TRUE):
  1. Each of the six domains is findable at the same relative path in Core, Infrastructure and App,
     and `Features/Common` imports nothing from any feature domain — the reference-direction test
     covers it alongside the Phase 3 Core-reference test.
  2. Every navigation destination in the app opens a page that exists and renders; walking the nav
     router's complete route set finds no entry pointing at a missing page.
  3. Optimize resolves as one slice — hub plus all six detail pages inside `Features/Optimize`, each
     with its models, services and page — and all 301 of its settings still load and render.
  4. AkariOS keeps its 35 Infrastructure services and its whole page in a single `Features/AkariOS`
     slice rather than being cut into three nav destinations, and Settings, Home and Backup exist as
     UI-only slices with no Core or Infrastructure folder.
  5. No file remains under the legacy top-level `Views/`, `ViewModels/`, `Services/`, `Defender/`,
     `Nvidia/`, `Scripts/`, `DI/` or `Resource/` directories, and
     `CompatibleSettingsRegistry.GetKnownFeatureProviders()` is unchanged in location, shape and role.
**Build Gate**: baseline bar, plus the real-catalog validation test still green (catalog moves are
pure data relocation), plus a route walk proving no nav entry dangles.
**Plans**: TBD
**Rationale**: This is the phase the whole Core Value depends on — a feature must be addable as one
self-contained slice without touching code outside its own folder, and today it cannot be. Each page
move is atomic (`.xaml` plus code-behind in one commit) and the nav router is updated in the *same*
commit as the page it points at, which is what keeps criterion 2 true throughout rather than only at
the end. Per-domain moves are separable commits; the shared files (Common, `MainWindow`, nav router,
DI extensions) are the single serialization point. AkariOS is a sixth mirrored slice rather than a
fold into Optimize/AdvancedTools because folding cuts one cohesive 2,631-line feature across three
user-facing destinations. Settings/Home/Backup are legal as UI-only slices because Winhance's own
Settings domain has no Core or Infrastructure layer — the precedent that makes this shape legitimate.
The shared setting-ViewModel bases land in `Features/Common/ViewModels/`, **not** in
`Features/Optimize/ViewModels/`, and `ISettingsFeatureViewModel` (when introduced) references only
Common types — Winhance's placement is its own documented anti-pattern and inheriting it would bake
a layering violation into the new structure.

### Phase 5: Common Machinery & Shared Rendering

**Goal**: Bring Core's contract count to parity, give every settings page one shared base, and render
setting rows through shared primitives instead of per-page duplication.
**Mode**: mvp
**Depends on**: Phase 4
**Requirements**: CORE-01, CORE-02, CORE-03, CORE-04, CORE-05, CORE-06
**Success Criteria** (what must be TRUE):
  1. Every Infrastructure service has a corresponding Core interface — a test enumerating
     Infrastructure's public service types fails when one lacks an interface, and the contract count
     reaches parity with Winhance's 90.
  2. Every settings page derives from the one shared section-page ViewModel base, and its domain is
     discoverable through a marker interface the navigation layer reads — so a new section page
     implements no hub behavior and no nav list is hard-coded.
  3. A setting row renders its badge, technical details and status banner through shared primitives:
     the same three elements appear identically on Optimize, Customize and SoftwareApps rows, and no
     per-page copy of that rendering exists.
  4. A hub page's cards are built from data — deleting the code-behind card construction from a hub
     changes no rendered card.
  5. Catalog aggregation happens at exactly one registration point, and
     `CompatibleSettingsRegistry.GetKnownFeatureProviders()` survives the phase as the same
     explicit, reflection-free, per-feature-degrading dictionary it is today.
**Build Gate**: baseline bar, plus the contract-coverage test and the real-catalog validation test
green, plus every Optimize/Customize/SoftwareApps page rendering with unchanged setting counts.
**Plans**: TBD
**UI hint**: yes
**Rationale**: This is the phase that makes a new slice genuinely one-slice. Without a section-page
base, a marker interface and shared row primitives, every future domain phase reinvents hub behavior
and row rendering, and the Core Value cannot hold. It lands before the feature phases so they build on
the primitives instead of growing around them. Criterion 5 restates a constraint as an observable:
the aggregation point is already the right shape, so "preserved rather than rewritten" is verifiable
by diff — which is exactly what makes the claim checkable rather than aspirational.
Leaves the app working because each primitive replaces a per-page rendering that already works, and
the real-catalog test plus the setting counts on every page prove nothing was dropped.

### Phase 6: Preferences & Localization

**Goal**: Converge on one JSON preference store and make every user-facing string — including
per-setting labels and descriptions — resolve through a live-switchable localization service.
**Mode**: mvp
**Depends on**: Phase 5
**Requirements**: PREF-01, PREF-02, I18N-01, I18N-02, I18N-03, I18N-04, I18N-05
**Success Criteria** (what must be TRUE):
  1. Changing the language in Settings re-renders the running app — navigation, page titles and
     setting labels — without an app restart.
  2. No user-facing string in a page or ViewModel is an inline literal; the call sites that currently
     pass `null` for `ILocalizationService` (including `SettingStatusBannerManager` and
     `TechnicalDetailsManager`) receive a real service.
  3. A guardrail test fails when a requested key is missing from a locale file, and a newly authored
     setting fails that test rather than rendering an untranslated label.
  4. Language, theme, display toggles and section-collapse state all persist in one JSON store keyed
     from `UserPreferenceKeys`, and section-collapse state written by the previous registry-backed
     store is read back after the upgrade.
  5. Because keys derive from `SettingDefinition`, a setting authored after this phase automatically
     requests a key — demonstrated by adding one throwaway setting and observing the key being
     requested.
**Build Gate**: baseline bar, plus the key-reference and JSON-integrity tests green, plus a
visual-diff-free smoke check that all pages render in the seeded locale.
**Plans**: TBD
**UI hint**: yes
**Rationale**: The single most counter-intuitive placement in this roadmap, and the most expensive to
get wrong. Winhance derives keys as `Setting_{LocalizationId ?? Id}_Name`, `_Description`,
`_Option_{n}`, `SettingGroup_{compacted}` — the key surface therefore *grows with the catalog*, so
every setting authored after this phase would otherwise ship un-localized, and invisibly, because the
key-reference test only fails for keys that are actually requested. Retrofitting locales across ~420
settings after several hundred more settings and a dozen more pages have landed is dramatically more
expensive than doing the plumbing first. Hence Phase 6 sits above Phases 7–10, all of which add pages
or settings. `PREF-01` lands in the same phase as `I18N-05` because the language preference persists
in exactly the store `PREF-01` creates — splitting them would mean two stores. The configuration
format stays forked (D6): this phase introduces no `.winhance` import and no shared config format.

### Phase 7: Advanced Tools Split

**Goal**: Run the WIM/ISO wizard and the autounattend generator from their own pages behind
DI-registered services, and give users the Scripts-folder capability without giving up Akari's
embedded payloads.
**Mode**: mvp
**Depends on**: Phase 6
**Requirements**: ADV-01, ADV-02, ADV-03, ARCH-04, SCR-01
**Success Criteria** (what must be TRUE):
  1. Advanced Tools is a hub with two cards; opening WIM/ISO or the autounattend generator navigates
     to its own page, and every wizard step reports progress and surfaces its errors on the page
     rather than in a monolithic code-behind panel.
  2. Both pages reach their logic through constructor-injected Infrastructure services — no static
     service call remains in their XAML code-behind.
  3. The WIM/ISO wizard still produces a working image end-to-end (ESD⇄WIM conversion and ISO
     creation), and the generator still produces a well-formed `autounattend.xml` written UTF-8
     without BOM — the behaviour that already exists is preserved through the split.
  4. A user can open a folder containing copies of the app's embedded scripts from a menu item, and
     generated files land at the documented path.
  5. `IAutounattendXmlGeneratorService` is declared in Core and implemented in Infrastructure, not
     placed as an interface-in-UI — Winhance's own flagged anti-pattern, which would otherwise be
     inherited by copying its shape.
**Build Gate**: baseline bar, plus a manual end-to-end wizard run producing a real image and a
real `autounattend.xml`, plus zero static service call sites in the two pages.
**Plans**: TBD
**UI hint**: yes
**Rationale**: The capability is present (851-line `WimUtilService`, 860-line `AutounattendService`)
and only the shape is wrong, so this is mostly a move — which is why it is cheap here and expensive
later, once this page has grown new behaviour on top of its 2,200 lines of code-behind. It sits
before Modes because the Builder-mode bar and the autounattend export path both attach to these pages,
and building them on the monolith would mean touching 2,200 lines of code-behind twice. Criterion 3
exists because "split into pages" is exactly the kind of change that silently drops capability: the
observable is the working image, not the new page count. Criterion 5 follows the constraint to
replicate the architecture but not Winhance's placement mistake.

### Phase 8: Builder & Config Review Modes

**Goal**: Let users stage setting changes and review them as a set before anything is applied —
including the two edit types Winhance itself documents as unrecorded.
**Mode**: mvp
**Depends on**: Phase 7
**Requirements**: MODE-01, MODE-02, MODE-03, MODE-04, MODE-05, TEST-04
**Success Criteria** (what must be TRUE):
  1. A user can enter Builder mode, change several settings across different pages, see them staged
     rather than applied, review the staged set, and then apply or cancel it.
  2. Leaving a mode with unsaved staged changes raises that mode's exit event, and the surfaces
     listening for it respond — the two orphan event contracts shipped in Core finally have a
     producer and a consumer.
  3. Config Review mode shows the staged configuration with an "N of M reviewed" count, and apply
     stays disabled until the set is fully reviewed.
  4. A numeric-range edit staged in Builder mode is recorded and reapplies the staged value on save.
  5. An AC/DC power-plan edit staged in Builder mode is recorded and reapplies the staged value on
     save, and tests cover both.
**Build Gate**: baseline bar, plus the Builder-mode recording test suite green for numeric-range and
AC/DC edits, plus every apply path still applying outside Builder mode.
**Plans**: TBD
**UI hint**: yes
**Rationale**: The mode contracts already exist in Core with no engine — this is an "engine missing"
phase, not new architecture, so it is cheaper than its absence suggests. It depends on Phase 7 for
the Builder-mode bar and the autounattend export path, and on the catalog shape being settled by then.
Criteria 4 and 5 are the phase's reason for existing: Winhance's own `BuilderEdit` documentation
records that `NumericRange` and AC/DC power edits are **not** recorded and silently fall back to the
system-seeded value. Akari fixes that (D10) rather than copying it, and pairs it with tests so the
fix cannot quietly regress into the Winhance behavior. Config format stays forked (D6) — no
`.winhance` import enters through the review/import path.

### Phase 9: Software & Apps Machinery

**Goal**: Consolidate Software & Apps into one page with two tabs, give it card/table/compact views
with sorting and help content, and complete the install chain through Chocolatey recovery.
**Mode**: mvp
**Depends on**: Phase 6
**Requirements**: SOFT-01, SOFT-02, SOFT-03, SOFT-04, SOFT-05, SOFT-06, SOFT-07, SOFT-08, ARCH-05
**Success Criteria** (what must be TRUE):
  1. Software & Apps is a single page with Windows Apps and External Apps tabs, and the separate
     Debloat page is gone with debloat removal reachable from the app removal flow.
  2. A user can switch the app list between card, table and compact views and sort it, and the chosen
     view mode and sort mode persist across restarts.
  3. Every catalog item offers help content, and the installed / can-be-reinstalled / not-installed /
     cannot-reinstall / warning legend is visible rather than implied.
  4. Installing an app runs through the shared CLI runner with parsed progress and handled exit codes;
     when WinGet is unavailable, Chocolatey bootstraps automatically, and a ghost package (Chocolatey
     reports installed, the files are missing) is recovered and the install retried.
  5. A user's current app selection can be handed to Advanced Tools and arrives there selected.
**Build Gate**: baseline bar, plus all 254+ catalog items still listed, plus a manual install run
through the WinGet path and the Chocolatey-bootstrap path.
**Plans**: TBD
**UI hint**: yes
**Rationale**: The catalog is already at parity (all 16 external-app categories match Winhance
exactly), so this phase builds machinery around existing data — it does not author settings, which is
why it is placed after localization: no new catalog content is added here, so the localization key
surface does not grow, but the new page and views still get localized strings. The view modes depend
on the Phase 1 SPIKE-02 finding, and the single-page consolidation is what makes the tab, sort and
view-mode state live in one ViewModel instead of three. Debloat is absorbed into the removal flow
rather than kept as a fourth page.

### Phase 10: Customize Slice Completion

**Goal**: Complete Customize as a four-section slice — wallpaper control, the Windows-theme special
handler, and the 12 Desktop settings folded into Explorer.
**Mode**: mvp
**Depends on**: Phase 6
**Requirements**: CUST-01, CUST-02, CUST-03, ARCH-03
**Success Criteria** (what must be TRUE):
  1. A user can set a desktop wallpaper from the Customize slice and it applies to the desktop.
  2. Switching to the Windows theme sets light and dark app modes correctly, asks for confirmation
     first, restarts Explorer, and can restore the OS default wallpaper.
  3. Akari's 12 Desktop settings appear inside the Explorer section; no separate Desktop nav entry
     or page exists.
  4. Explorer (including Desktop), Start Menu, Taskbar and Appearance each have Core models,
     Infrastructure services and a page inside `Features/Customize`, and all four render their
     settings.
**Build Gate**: baseline bar, plus all 121 Customize settings still resolvable and renderable (now
across four pages), plus no dangling nav entry for the removed Desktop page.
**Plans**: TBD
**UI hint**: yes
**Rationale**: Last because it is the smallest, least-coupled feature slice — it gains nothing from
waiting for the other domains, and putting it last keeps the highest-risk work (the restructure and
modes) as early as possible. The Desktop→Explorer merge is a deliberate shape change (D3): Winhance
has no Desktop section, and Akari's Desktop settings already claim Explorer-family membership, so
merging makes the nav match the reference without inventing or deleting settings. It depends on
Phase 6 so the theme and wallpaper strings are localized from the start — this phase adds settings
(special-handler rows), which is precisely the case where late localization would ship invisible
gaps. The Windows-theme handler additionally runs per-user writes, which is why OTS detection already
exists from Phase 2.

## Critical Path, Parallelism & Sequencing Risk

### Strictly serial (each step changes what the next step's compiler can see, or is verified by the
previous step's test)

```
1  Baseline, spikes, App test project
  → 2  Dead subsystems & defect repairs
  → 3  Namespace alignment → composition root → ServiceLocator removal
  → 4  Per-domain moves (Core → Infrastructure → App), namespace before move
  → 5  Common machinery (base, markers, row primitives)
  → 6  Preference store + localization plumbing
  → 7  Advanced Tools split
  → 8  Builder / Config Review modes
```

Within Phase 4 the order is: Core catalogs first (data only, so every move is verified by the
real-catalog test), then Infrastructure, then App pages — and namespace-before-move throughout, so a
failure has exactly one cause.

### Genuinely parallelizable

| Parallel work | Why it is safe | Constraint |
|---------------|----------------|------------|
| Phase 4's per-domain moves (Optimize, Customize, SoftwareApps, AdvancedTools, AkariOS, Settings) as separate commits | disjoint file sets per domain | each is a self-contained commit; the shared files (Common, `MainWindow`, nav router, DI extensions) are the single serialization point |
| Phase 9 ‖ Phase 10 | disjoint slices, both gated only on Phase 6 | neither touches the other's folders |
| Phase 7 work that only moves services ‖ Phase 9's contract extraction | the 646-line `SoftwareAppService` decomposition and the WIM/autounattend split touch different files | both write into `Features/*/Services/` — agree interface naming first |
| Phase 8 review-bar UI ‖ Phase 8 recording engine | the bar is presentation, the engine is recording | both meet at `SettingItemViewModel`'s apply short-circuit — one owner |
| Setting catalog additions | a new setting is pure data and survives file moves | **not exercised in this milestone** — catalog content expansion is Out of Scope |

### Items that get much harder if deferred to the end

| Item | Where it sits | Why deferral is expensive |
|------|---------------|---------------------------|
| Localization plumbing | Phase 6, above all feature phases | keys derive from `SettingDefinition`, so the key surface grows with the catalog; a late-authored setting ships un-localized *and invisibly*, because the key-reference test only fails for keys that are requested |
| Configuration-format fork (D6) | honored in Phases 6 and 8 | staying forked is cheap now and expensive once format-specific code accumulates around an implicit shape; no `.winhance` import or shared format may enter |
| `ServiceLocator` removal | Phase 3 | if it slips, every feature written in the meantime acquires the pattern, and the App test project stays blocked |
| OTS impersonation detection | Phase 2 | retrofitting after more per-user (HKCU) write sites land means auditing every affected site — the exposure it exists to prevent |
| Static path constants (`AkariPaths` shape) | Phase 2 | `static readonly` computed at type-init in two assemblies; fixing it after localization/WIM add consumers guarantees a third copy |
| Catalog uniqueness enforcement | Phase 2 | it is the safety net for every later phase; unenforced, a duplicate ID silently corrupts backup-file compatibility with no compile error and no test failure |
| `ElevationService` thread-affinity | vigilance in every phase | awaiting inside an impersonated action silently loses the elevated identity — no exception, no log. Any change that makes an impersonated path async must be audited at the call site. |

### Carried uncertainty — must not be treated as settled

| Item | Source | How this roadmap handles it |
|------|--------|----------------------------|
| WinAppSDK 2.3.1 + `CommunityToolkit.WinUI` / DataGrid compatibility | Winhance pins 1.8.x and warns the metapackage is mandatory; there is no documented "2.3.1 + toolkit 8.2.x" statement | Phase 1 SPIKE-02 is a **hard gate**; Phase 5's row primitives and Phase 9's table view plan against substitutes if the controls do not resolve |
| Builder-mode edit recording | Winhance's own docs: `NumericRange` and AC/DC edits are **not** recorded and fall back to system-seeded values | Akari **fixes** it (D10) as MODE-03/MODE-04 with TEST-04, rather than replicating a mode that appears to work and silently drops numeric edits |
| WinGet COM under self-contained | `WindowsPackageManagerElevatedFactory` hangs (microsoft/winget-cli#4377); Winhance is forced onto `WindowsPackageManagerStandardFactory` with `allowLowerTrustRegistration: true` | Phase 9 must confirm Akari's `WinGetComSession` made the same choice before building the install chain on it |
| Shared setting-ViewModel bases | Winhance's own anti-pattern: `Common` imports `Optimize.ViewModels`, so deleting Optimize would break Customize's type system | Phase 4 lands the shared bases in `Features/Common/ViewModels/`; Phase 5's `ISettingsFeatureViewModel` references only Common types |
| `IAutounattendXmlGeneratorService` placement | Winhance's own anti-pattern: interface in Core, implementation in UI | Phase 7 declares it in Core and implements it in Infrastructure |
| Overwrite-by-later-registration | Winhance's own anti-pattern that it nevertheless instructs replicators to keep | Phase 3 keeps the empty-default trick, with the explanatory comment **and** the container-resolution test, so the ordering cannot be "cleaned up" |
| Single-file catalogs | Winhance's advice is right and its own 4,151-line file is its own anti-pattern | Replicate the advice, not the file. Not a v1 requirement — recorded so a later phase does not copy the file |

## Progress

**Execution Order:** 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 (critical path); Phases 9 and 10 run in parallel
after 6 and may interleave with 7 and 8.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Baseline, Spikes & Test Harness | 1/4 | In Progress | - |
| 2. Dead Subsystems & Defect Repairs | 0/TBD | Not started | - |
| 3. Namespace Alignment & Composition Root | 0/TBD | Not started | - |
| 4. Vertical Slices Across All Three Layers | 0/TBD | Not started | - |
| 5. Common Machinery & Shared Rendering | 0/TBD | Not started | - |
| 6. Preferences & Localization | 0/TBD | Not started | - |
| 7. Advanced Tools Split | 0/TBD | Not started | - |
| 8. Builder & Config Review Modes | 0/TBD | Not started | - |
| 9. Software & Apps Machinery | 0/TBD | Not started | - |
| 10. Customize Slice Completion | 0/TBD | Not started | - |

---
*Roadmap created: 2026-10-05*