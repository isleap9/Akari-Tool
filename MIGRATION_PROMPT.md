# Akari Tool — WinUI 3 MVVM Rebuild (Agent Brief)

You are continuing a rebuild of **Akari Tool** onto a native WinUI 3 **MVVM** shell. Read this fully before writing code. Also read `CLAUDE.md` — it holds the landmines, the Defender rule, and current state.

> **The old WPF→WinUI 3 migration is DONE.** That brief is obsolete. This document is the MVVM rebuild brief. Do not follow the old factory-migration plan.

---

## 0. THE SITUATION (three builds — know which you touch)

1. **WPF original (net8)** — dead history. Ignore.
2. **WinUI 3 factory build (net8, WinAppSDK 1.8)** — completed, VM-verified, functionally complete (433 tweaks, backup, Defender, search, installer). This is the **SHIPPING FALLBACK.** **Never modify, break, or delete it.** It stays the release until build #3 passes the same gates.
3. **WinUI 3 MVVM rebuild (net10, WinAppSDK 2.3.1)** — the ACTIVE work, separate project `Akari-Tool-MVVM/` on the in-house `WinUI-3-framework` (CommunityToolkit.Mvvm + DI). **All work in this brief happens here.**

Every step: **build #2 must remain untouched** (verify `git status` if unsure). Build #3 is a separate project/branch.

---

## 1. NON-NEGOTIABLE CONSTRAINTS

1. **Preserve every `TweakDefinition` Id byte-for-byte.** Ids are what `TweakRegistry` and Backup/Restore match on. A changed Id silently orphans that tweak from every existing user's saved config. Never rename, never "clean up," never reorder in a way that changes generator-produced Ids.
2. **Backup file format stays compatible** with build #2's exports (both directions where possible). Flag any format drift loudly.
3. **⛔ Defender: DO NOT TOUCH.** `DefenderService.cs`, `DefenderPhase2Scheduler.cs`, the `SetAsync`/`RunPhase2Native` call sites, the embedded payload — all ported byte-identical and must stay that way. Never edit/refactor/relocate/arm without isleap's explicit ask in the current message. If a task would incidentally touch it, STOP and ask. (Full rules in `CLAUDE.md`.)
4. **Elevation stays intact.** `requireAdministrator` manifest; `ElevationService` byte-identical; per-thread impersonation must stay self-contained on its thread.
5. **Keep the logic layer behavior-identical.** The port already brought `Services/*`, catalogs, `TweakRegistry`, drift/baseline, presets across. Any further change to logic must preserve exact registry paths, apply/read logic, and per-tweak delegates. Flag any forced change loudly (as was done for `ToolService`, `ServiceController`, and the Power write-path split).
6. **Build with VS MSBuild** (`msbuild /t:Rebuild /p:Configuration=Debug /p:Platform=x64`), not `dotnet build`. A phase isn't done until it builds with 0 errors. Report the LITERAL build output.
7. **No invented APIs.** WinAppSDK 2.3.1 / net10 only. Unsure an API exists → say so and leave a `// TODO` rather than guessing.
8. **Stop and report at each gate.** Don't chain phases. Update `MIGRATION_LOG.md`. isleap signs off (usually on a VM) before the next step.

---

## 2. WHAT'S ALREADY DONE

- **Shell:** native MVVM shell — Mica, custom title bar, native `NavigationView` (tags MATCH the routing contract), Frame nav via `INavigationService`, `requireAdministrator`, docked log console + status bar. Builds/runs/elevates. Home is real; other tabs → placeholder.
- **Logic ported (98 .cs, 0 errors):** all `Services/*`, `TweakDefinition`, `TweakRegistry`, drift/baseline, `SystemStateReader`, `ServicesPreset`, `PlaybookTweaks`, removal generators, and Gaming/Privacy/Software catalogs. Defender + elevation **byte-identical**.
- **Catalogs extracted:** Customize, Power, Notifications, Sound, Update — `TweakDefinition`s lifted out of tab partials into data-only catalog classes (225 defs), **all Ids byte-identical**.
- **Count reconciled:** 444 total (433 on isleap's desktop; 11 battery/GPU rows gate off by hardware — gating preserved). 444 is correct, not a bug.

**Forced logic changes (done, behavior-preserved, don't undo):**
- `ToolService` → headless events (`LineLogged`/`ProgressStarted`/`ProgressStopped`), same public surface for 23 callers.
- `System.ServiceProcess.ServiceController` package added.
- Power write-path: UI repaint relocated behind `PowerTweaks.PowerSchemeChanged` event; `/SETACTIVE`-last and "never reactivate from a read path" invariants verified intact.

---

## 3. 🔴 HEADLESS EVENTS THAT NEED SUBSCRIBERS (critical for the rendering layer)

When you build the tweak-rendering layer, wire real subscribers for each of these. A missing subscriber compiles and runs clean, then shows as **stale UI on interaction** — the exact bug class that has bitten this project twice (nav drift, search crash). Track them explicitly:

- `ToolService` `LineLogged` / `ProgressStarted` / `ProgressStopped` → log console + progress bar.
- `TweakRegistry.SectionsNeedRefresh` → section pill refresh after import / bulk apply.
- `PowerTweaks.PowerSchemeChanged` → repaint plan cards + persist indicator. **Handler MUST stay read-only** (repaint from `ResolveSchemeTarget()` + `_schemeInactive`); it must NEVER call back into `SetPowerCfg`. The event carries no power-state authority.

---

## 4. THE PLAN

### NEXT: Gaming rendering-layer spike (measurement gate — do this before rolling out any other tab)

Build the native MVVM tweak-rendering layer for **ONE** tab — **Gaming** — end to end, because it exercises the full rendering surface: toggles, a dropdown (with custom/Windows-default state), sections, quick-actions, and bulk bars.

- Design the rendering layer: a settings-list control + item ViewModel(s) + `DataTemplate`s per row type + a template selector. This replaces the old `TweakHelpers` factory. Aim for one render path + shared style tokens so all tabs stay visually consistent (the factory's leverage, done the MVVM way).
- Wire Gaming: catalog → ViewModel(s) → DataTemplates → registration into `TweakRegistry` (preserve the `Mark`/`ClaimRange` bracketing so the count and backup stay correct).
- Wire the relevant headless-event subscribers for this tab (ToolService log/progress at minimum; SectionsNeedRefresh for its sections).
- **Report the real cost:** how long Gaming took, what the rendering layer looks like, and which row types were fiddly. This number × ~15 is the remaining rollout cost — it's the point of the spike.
- **Do NOT roll out other tabs yet.** Stop and report; isleap measures and signs off first.

### THEN: tab rollout (after the spike is signed off)

Replace placeholders with real pages, a few tabs per wave, simplest → most complex, reusing the rendering layer. Preserve Ids and the `TweakRegistry` bracketing every time. Special-case tabs get their own waves: **Software** (destructive removal flow — Edge/OneDrive/bloat generators stay byte-identical, only selection/trigger UI changes), **Backup & Restore** (the only tab that proves `ClaimRange` bracketing — verify via a real VM round-trip), **Home / About / Tools / Advanced Tools** (bespoke layouts, not tweak-row tabs).

### THEN: startup orchestration + wiring

Splash / staged progress, the `--competitive` shortcut path, the startup update check (`Loaded → UpdateService.CheckAsync → navigate to AppUpdate`), and the Defender `--defender-phase2` startup handoff. **Defender phase-2 and its call site: show isleap the diff before applying; do not arm anything Defender-related without explicit go.**

### THEN: installer + release

Inno Setup / `build-installer.ps1` for the net10/2.3.1 self-contained unpackaged output. Only after build #3 passes the same VM gates build #2 did does it become the release — until then build #2 ships.

---

## 5. VERIFICATION DISCIPLINE

- **compile-clean ≠ launch-clean ≠ exercise-clean.** The automated gates (builds, launches, nav-contract assertion) are necessary but blind to interaction bugs. isleap runs functional tests on a VM.
- **Maintain a VM verification checklist** in `MIGRATION_LOG.md`, risk-ranked. Hard gates before any release: the **Defender reboot round-trip** (disable → reboot → phase-2 completes → re-enable → reboot → fully restored) and the **Backup round-trip** (apply mixed set → export → snapshot → revert → import → diff registry; empty diff = `ClaimRange` correct). Plus an **interaction smoke** set: type in search, open quick-actions flyout, change a dropdown, trigger a confirmation dialog, open import review.
- **Async dialogs:** WinUI `ContentDialog` is async-only (no WPF `Dispatcher.PushFrame` blocking). Every confirm path must actually `await` and return correctly — verify by a real click-through, not just compilation.
- **Mica renders on real hardware, often NOT in a VM.** Don't conclude Mica is broken from a VM.
- **Do NOT run/click tweaks on isleap's main machine.** Verify via a de-elevated (`asInvoker`) copy + UI Automation, or leave it as a VM checklist item. isleap does destructive testing on a VM.

---

## 6. MIGRATION LOG

Maintain the migration log. **`MIGRATION_LOG3.md` is the ACTIVE log — log all new phases there.** `MIGRATION_LOG.md` (Phases 1-8) and `MIGRATION_LOG2.md` (Phases 9-29) are ARCHIVAL — do not append to them. After every phase: files added/changed, any forced logic change (flag loudly), anything unverifiable, the literal build output, and updates to the VM checklist. Keep the headless-event subscriber list current until all are wired.

---

## 7. THIS RUN

Confirm which step you're on (`CLAUDE.md` → "MVVM rebuild status" has the latest). If the last sign-off was the Power invariants, the next step is the **Gaming rendering-layer spike** (Section 4). Do that one tab only, then STOP and report: literal build output, `MIGRATION_LOG.md`, the per-tab cost, and the rendering-layer design. Do not roll out other tabs. Do not touch build #2. Do not touch Defender.