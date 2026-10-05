# Phase 1: Baseline, Spikes & Test Harness - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-05
**Phase:** 1-Baseline, Spikes & Test Harness
**Areas discussed:** What "App-layer test" means & how tests run, SPIKE-02 spike shape & whether the packages stay, SPIKE-03 report mechanics, SPIKE-01 baseline: recorded doc vs. enforced gate

---

## What "App-layer test" means & how tests run

**Q1 — How should `AkariTool.App.Tests` be wired to reach App-layer code?**

| Option | Description | Selected |
|--------|-------------|----------|
| Reference App directly, test non-UI logic | Test project sets `UseWinUI=true` and ProjectReferences `AkariTool.App`, consuming the DLL that already builds successfully. Only tests logic that needs no live DispatcherQueue/no XAML visual tree. Lowest risk that satisfies TEST-01 literally. | ✓ |
| Reference Core + Infrastructure only | Test project references only Core + Infrastructure — no WinUI at all, so no PRI/XAML fragility. But it cannot touch a single line of App code, which is what TEST-01 actually asks for. | |
| Pull App logic downward into Core/Infrastructure first | Move App-layer pure logic (SettingBackupService, StartupOrchestrator, SettingPageWarmUp) down into Core/Infrastructure first, then test it there. | |
| Compile-link App sources into the test project | Test project `<Compile Include>`s App's .cs files without the XAML. | |

**User's choice:** Reference App directly, test non-UI logic

**Notes:** Chosen because App already emits a DLL successfully, so consuming it is lower risk than
re-compiling XAML. Constrains the phase to App logic that needs no `DispatcherQueue` and no visual
tree — which is what ruled out `SettingBackupService` as the first target.

---

**Q2 — What should the App.Tests project's first test target be?**

| Option | Description | Selected |
|--------|-------------|----------|
| SettingBadgeCalculator.Compute | Pure, static, no I/O, already App-layer. Proves the harness works AND becomes the characterization test that guards Phase 5's CORE-04 badge-primitive extraction. | ✓ |
| UnattendTweakCatalog | Untested App-layer data that Phase 7 will split out of the monolith. Data assertions, not behaviour. | |
| SettingBackupService logic | 658 lines of export/preview/import/search — highest value, but takes `IEnumerable<SettingPageViewModel>`, which needs a hand-built ServiceCollection first. | |
| Deliberately trivial first test | One trivial passing test purely to prove the project builds and runs. | |

**User's choice:** SettingBadgeCalculator.Compute

**Notes:** The user was shown that this choice does double duty — it satisfies TEST-01 *and* becomes
the safety net for Phase 5's shared badge primitive (CORE-04). That second role was the deciding factor.

---

**Q3 — How should tests be built and run, given `dotnet build` is declared unusable?**

| Option | Description | Selected |
|--------|-------------|----------|
| Committed wrapper script: MSBuild builds, VSTest runs | Commit `tools/run-tests.ps1`: MSBuild builds the sln, then `vstest.console.exe` runs the produced test DLLs. One command for every later phase. | ✓ |
| MSBuild to build, then `dotnet test --no-build` | Build with MSBuild as the constraint mandates, run with `dotnet test --no-build`. | |
| Plain VS MSBuild + VSTest console, no wrapper | Run the raw commands by hand each time. | |
| Just use `dotnet test` and fix whatever breaks | Simplest on paper; risks contradicting the standing constraint. | |

**User's choice:** Committed wrapper script: MSBuild builds, VSTest runs

**Notes:** Flagged that `TESTING.md` currently documents `dotnet test` as the run command, which
conflicts with the VS-MSBuild-only constraint. The wrapper resolves the conflict in favour of the
constraint. Also pre-stated that the same script would become the SPIKE-01 baseline gate's runner,
which the user then confirmed in the SPIKE-01 area.

---

**Q4 — How wide is `AkariTool.App.Tests`'s remit in Phase 1?**

| Option | Description | Selected |
|--------|-------------|----------|
| Minimal: prove it works, leave the rest to later phases | Only `SettingBadgeCalculatorTests`. Phase 2 adds the real-catalog validity test, Phase 3 adds the container-resolution test. Keeps the baseline test count stable. | ✓ |
| Add the real-catalog validity test now too | Also land the test enumerating all 11 catalog `Build()` methods. Moves BUG-02's test ahead of its own phase. | |
| Also add a Core-references-neither-layer test now | Also lands TEST-03 in Phase 1. Assigned to Phase 3 in the traceability table. | |

**User's choice:** Minimal: prove it works, leave the rest to later phases

**Notes:** Both alternatives were declined on traceability grounds — the requirements belong to Phases
2 and 3, and pulling them forward would destabilise the very baseline Phase 1 is recording.

---

**Area check:** User chose "Next area" — no further questions on test wiring.

---

## SPIKE-02 spike shape & whether the packages stay

**Q1 — Where does the SPIKE-02 spike physically live?**

| Option | Description | Selected |
|--------|-------------|----------|
| Throwaway spike project in the repo, deleted after the verdict | Own csproj, own XAML page rendering all three controls, same WinAppSDK 2.3.1 TFM. Built once, verdict written up, project deleted. `AkariTool.App`'s package list stays byte-identical. | ✓ |
| Scratch XAML page inside AkariTool.App | A `SpikeControlsPage.xaml` plus nav entry in the real app. Tests the real project exactly, but a failing package set breaks the app build until reverted. | |
| Keep the spike project permanently as the control-compat harness | Add the packages to App.csproj and keep a canary page. Phase 4/5 inherit a reference immediately, but App's package list changes in Phase 1 and a later package bump re-opens the question. | |

**User's choice:** Throwaway spike project in the repo, deleted after the verdict

**Notes:** The deciding factor was containment — a failing toolkit package set must not be able to
break the app build that every later phase's Build Gate depends on.

---

**Q2 — What bar must the spike clear for the result to read as "all three render"?**

| Option | Description | Selected |
|--------|-------------|----------|
| Compile + launch + visually render | Each control compiles against 2.3.1, the spike app launches, all three visible in the window. Catches the runtime XAML resource and theming failures where a version-skewed toolkit actually breaks. | ✓ |
| Compile + launch, no visual confirmation | Compile and launch without a UAC prompt or window confirmation. 'Renders' would be inferred. | |
| Compile-only, no run | Resolve at compile time only, per SPIKE-02's literal wording. | |

**User's choice:** Compile + launch + visually render

**Notes:** Explicitly rejected the compile-only reading of the requirement's wording, on the grounds
that a compile pass does not establish that the controls "render correctly".

---

**Q3 — If a control fails, how concrete must the named substitutes be?**

| Option | Description | Selected |
|--------|-------------|----------|
| Concrete substitute + a render-checkable XAML snippet | For each failing control, name a specific substitute AND prove it renders in the same spike page. Phases 5 and 9 plan against something observed, not a guess. | ✓ |
| Name the substitute, don't prove it renders | Name the control and version without proving it renders. Leaves Phase 5/9 planning against an unverified assumption. | |
| Diagnose the root incompatibility too | Also determine whether the cause is the WinAppSDK version, the CsWinRT 2.0.4-vs-2.2.0 gap, or the WCT 8.x reorg. Risks turning a bounded spike into an open-ended project. | |

**User's choice:** Concrete substitute + a render-checkable XAML snippet

**Notes:** Root-cause diagnosis explicitly ruled out of scope.

---

**Area check:** User chose "Next area" — declined a second round covering package versions,
`Microsoft.Windows.SDK.BuildTools`, verdict-document authoring, and clean-machine reproducibility.

---

## SPIKE-03 report mechanics

**Q1 — Where does the Winhance side of the ID diff come from?**

| Option | Description | Selected |
|--------|-------------|----------|
| Committed snapshot + regeneration script | A committed snapshot of Winhance setting IDs, generated once by a script reading the Winhance checkout, then committed. The test reads the snapshot, so it runs anywhere with no external path dependency. | ✓ |
| Live-diff against the Winhance checkout | The generator reads `C:\Users\isleap\Documents\GitHub\Winhance` at report time. Always current, but unusable without that checkout and silently changes when Winhance updates. | |
| Pure text parsing over both repos | Regex both repos' `.cs` sources. Fastest, no build needed, but fragile to formatting and misses computed or templated IDs. | |

**User's choice:** Committed snapshot + regeneration script

**Notes:** Framed against the phase's rationale — nothing here should be referenced by production code,
so the report must not take a hard dependency on a path outside the repo. Live-diffing was rejected on
that basis, not on freshness grounds.

---

**Q2 — How does the tool get Akari's own setting IDs?**

| Option | Description | Selected |
|--------|-------------|----------|
| Reflect over the real catalogs at runtime | Call the 11 real catalog `Build()` methods via reflection and read each `SettingDefinition.Id`. The Akari side can never drift, and it doubles as the enumeration seam Phase 2's BUG-02 test will reuse. | ✓ |
| Call `Build()` directly, no reflection | Hand-write a list of the 11 catalog classes. Fails loudly on a rename, but the list is a second place to update. | |
| Text-parse Akari's catalogs the same way | Symmetric with the Winhance side, but can silently miss an ID that is built rather than written as a literal. | |

**User's choice:** Reflect over the real catalogs at runtime

**Notes:** The reuse benefit — becoming the enumeration seam for Phase 2's BUG-02 uniqueness test —
was the deciding factor over the simpler hand-written list.

---

**Q3 — What should the report do with a divergence it finds?**

| Option | Description | Selected |
|--------|-------------|----------|
| Measure and report only — no gate | Report the divergence and assert nothing about it. The known counts are deliberate and catalog expansion is Out of Scope, so a gate would fail on intent rather than defect. | ✓ |
| Fail the test on any unexplained divergence | Strongest guard on the fork, but immediately red on the 22 known content differences. | |
| Informational test with a committed allowlist | Also assert Akari-side ID uniqueness, reusing `SettingCatalogValidator`'s existing rule. | |

**User's choice:** Measure and report only — no gate

**Notes:** Notably, the third option was declined even though it would have been safe, because
`SettingCatalogValidator`'s uniqueness rule is Phase 2's BUG-02 deliverable — the same traceability
argument that decided Q4 of the first area.

---

**Q4 — What form does the SPIKE-03 report take?**

| Option | Description | Selected |
|--------|-------------|----------|
| Committed generator writes a committed markdown report | Plus machine-readable ID sets. Re-runnable, human-readable for the D6 decision, git-diffable so the evidence evolves with the catalog. | ✓ |
| Test-only, writes no artifact | Zero committed files beyond the snapshot, but the D6 evidence base lives only in a test nobody reads. | |
| Generated report plus an xUnit test asserting it | Catches a stale report, at the cost of a test that fails whenever the catalog legitimately changes. | |

**User's choice:** Committed generator writes a committed markdown report

**Notes:** The D6 fork decision is the report's whole purpose, so it had to be human-readable as
evidence — which ruled out the test-only variant.

---

**Area check:** User chose "Next area" — declined a second round covering generator location, snapshot
version tagging, divergence categorisation, and exact-vs-prefix ID comparison.

---

## SPIKE-01 baseline: recorded doc vs. enforced gate

**Q1 — Should the baseline be a recorded number or an enforced gate?**

| Option | Description | Selected |
|--------|-------------|----------|
| Machine-checked gate: `run-tests.ps1` gains a baseline-compare mode | Force a Rebuild, capture the MSBuild warning/error summary and VSTest test count, compare against a machine-readable baseline, exit non-zero on any increase. Every later Build Gate becomes one command. | ✓ |
| Recorded document only | A markdown document; a human or verifier compares by eye. Zero parsing fragility, but every phase re-derives the judgement. | |
| Document plus a test-count assertion only | Mechanises the cheap, reliable part; leaves warning counting to review. | |

**User's choice:** Machine-checked gate: `run-tests.ps1` gains a baseline-compare mode

**Notes:** ROADMAP defines passing as "no new warnings beyond the counts recorded in Phase 1" — a
machine check makes that definition mechanical for all nine later phases.

---

**Q2 — When the baseline gate runs a build, does it force a full rebuild?**

| Option | Description | Selected |
|--------|-------------|----------|
| Rebuild always — counts are only honest from a clean build | `/t:Rebuild` every run. MSBuild only re-emits warnings for files it recompiles, so an incremental build under-reports and the gate would pass on stale numbers. | ✓ |
| Incremental build, warn on under-report | Fast inner loop, full rebuild only when the recorded counts are not reproduced. |
| Clean `bin/`+`obj/` each time, always | Most honest, slowest — and re-triggers the slow PRI step every time. | |

**User's choice:** Rebuild always

**Notes:** Raised proactively as a landmine rather than asked cold: a false green is worse than no
gate, which is what an incremental warning count produces.

---

**Q3 — Where do the baseline document, spike verdict and ID report live, and where does the generator code go?**

| Option | Description | Selected |
|--------|-------------|----------|
| Generator code in `tools/`, artifacts in `.planning/phases/01-…/` | Durable, unmistakably code for the scripts; phase evidence stays with the rest of the phase's artifacts. | ✓ |
| Everything in `.planning/phases/01-…/` | Whole phase self-contained and archives as one unit — but GSD archives phase directories on milestone close, taking the runner with it while nine later phases still depend on it. | |
| Code in `tools/`, documents in a new `docs/development/` folder | Most discoverable, but adds a second home for planning artifacts. | |

**User's choice:** Generator code in `tools/`, artifacts in `.planning/phases/01-…/`

**Notes:** Checked `docs/` before offering it — it is the GitHub Pages website (HTML + a README), not
a documentation folder, so it was ruled out on the merits rather than on principle.

---

**Q4 — How strict should the baseline comparison be?**

| Option | Description | Selected |
|--------|-------------|----------|
| No increase, solution-wide — matches ROADMAP wording | Gate fails if warnings or errors exceed the recorded figure; reductions pass silently. Matches ROADMAP's wording and Phase 2's expected deletions. | ✓ |
| Exact match, re-baseline explicitly | Any change fails unless a phase updates the baseline file. Turns a warning reduction into extra work. | |
| No increase, tracked per project | Better attribution when it fails, larger baseline file to maintain. | |

**User's choice:** No increase, solution-wide

**Notes:** Phase 2's deletions of dead code are expected to *reduce* the warning count, which is what
ruled out exact-match-and-rebaseline.

---

**Area check:** User chose "I'm ready for context" — declined a further round covering baseline file
fields, the PRI175/PRI252 allowlist mechanism, CI wiring, and MSBuild warning-summary parsing. The
PRI allowlist was nonetheless recorded as D-16, carried from ROADMAP rather than chosen here.

---

## the agent's Discretion

The user did not constrain the following; each is recorded in CONTEXT.md under "the agent's Discretion":

- The internal shape of `tools/` — file names, and whether the runner, the baseline gate and the
  ID-diff generator are one script or several.
- The machine-readable baseline's format and which fields it holds beyond warnings/errors/test count.
- Whether `AkariTool.App.Tests` needs `InternalsVisibleTo` (`SettingBadgeCalculator` is already public).
- Whether the throwaway spike project joins `AkariTool.sln` temporarily, and how the baseline run
  interacts with `vendor/WinUI.Framework` being absent from the solution.
- How the ID report categorises divergence kinds.

## Deferred Ideas

- Real-catalog validity test → **Phase 2 / BUG-02**.
- Container-resolution integration test → **Phase 3 / TEST-02**.
- Core-references-neither-layer assembly test → **Phase 3 / TEST-03**.
- Root-cause diagnosis of a SPIKE-02 incompatibility → out of scope; Phases 5 and 9 plan around
  substitutes.
- Adding `vendor/WinUI.Framework` to `AkariTool.sln` (CONCERNS #7) → Phase 3/4 restructure.
- Deleting the dead `Microsoft.WindowsAppSDK` / `CommunityToolkit.Mvvm` refs from Core (CONCERNS #14)
  → unrelated cleanup; would shift the recorded warning baseline if done in this phase.
- CI wiring for the baseline gate → future work; no CI exists in the repo.