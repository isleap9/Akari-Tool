# Akari Tool

## What This Is

Akari Tool is a Windows 11 optimization utility built on WinUI 3 + MVVM. Its tuning catalog is
already at content parity with the Winhance reference implementation — ~430 settings across
Optimize (301), Customize (121), and SoftwareApps (254+ items), with the 16 external-app
categories matching Winhance's counts exactly. What is *not* at parity is the structure: Akari
started a vertical-slice migration and never finished it, so it has 11 domains in Core but only 3
in Infrastructure and 3 in App, plus legacy top-level `Views/`, `ViewModels/`, `Services/`,
`Defender/`, `Nvidia/`, and `Scripts/` directories. This project finishes that migration onto
Winhance's clean per-domain slices and then builds the machinery that is genuinely missing.

## Core Value

Every new capability must be addable as one self-contained vertical slice — a Core contract, an
Infrastructure service, and a UI page — without touching code outside its own feature folder. If a
change requires edits across layer boundaries, the architecture has failed, and that is a defect to
fix rather than a cost to accept.

## Requirements

### Validated

Inferred from the codebase and verified by a successful compile on 2026-10-05.

- ✓ Declarative `SettingDefinition` stack adopted from Winhance — `SettingDefinition`,
  `RegistrySetting`, `BadgePillState`, `SettingDefinitionToggleState`, `FeatureBadgeSummary`, the
  enums, and the one-catalog-file-per-tab convention. Track A migration complete.
- ✓ Optimize: 301 settings (Gaming 129, Privacy 89, Power 48, Notifications 16, Update 12, Sound 7)
  — **ahead of Winhance by 13**
- ✓ Customize: 121 settings (Explorer 67, Taskbar 32, Desktop 12, Start Menu 12, Appearance 10)
- ✓ Software & Apps: 254+ item definitions in 16 `ExternalAppCatalog.<Category>` partials —
  per-category counts identical to Winhance's
- ✓ WIM/ISO wizard (`WimUtilService`, 851 lines: 31 oscdimg refs, ESD⇄WIM, disk-space checks,
  `Mount-DiskImage` with unmount-on-all-paths, driver injection) and autounattend generator
  (`AutounattendService`, 860 lines) — capability present, structurally monolithic
- ✓ WinGet COM interop via `vendor/WinGet.Interop` with 4 Core contracts and 4 Infrastructure
  services — **already present, not a gap**
- ✓ `CompatibleSettingsRegistry.GetKnownFeatureProviders()` — the single catalog aggregation point,
  already the right shape (explicit dictionary, no reflection, per-feature try/catch degradation)
- ✓ AkariOS: service presets, Playbook tweaks, BCD, Competitive Mode, GPU tooling, PostInstall —
  35 Infrastructure files, 2,631 lines of page code
- ✓ Akari-only surfaces: Home, Backup/Restore, Verify, Nvidia, Defender, Tools
- ✓ Always-elevated `requireAdministrator` manifest, identical to Winhance
- ✓ COM `IFileOpenDialog`/`IFileSaveDialog` via `AkariFileService` (335 lines) — same approach and
  same documented rationale as Winhance (WinRT pickers throw `COMException 0x80004005` when
  elevated). **Already at parity.**
- ✓ SYSTEM/TrustedInstaller impersonation plus a `build-deelevated.ps1` `asInvoker` test build —
  Akari has this capability and Winhance does **not**
- ✓ `IProcessRestartManager` — a Winhance restart-coalescer, 1:1 ported. **Not** an elevation
  mechanism.
- ✓ Solution compiles: all six projects emit DLLs (Core, Infrastructure, WinGet.Interop,
  WinUI.Framework, App, both test assemblies)

### Active

- [ ] Restructure onto vertical slices: six domains (`Optimize`, `Customize`, `AdvancedTools`,
      `SoftwareApps`, `Settings`, `AkariOS`) mirrored across Core, Infrastructure, and UI at
      identical paths
- [ ] Retire the legacy top-level `App/` directories (`Views/`, `ViewModels/`, `Services/`,
      `Defender/`, `Nvidia/`, `Scripts/`, `DI/`, `Resource/`) into their proper slices
- [ ] Replace `ServiceLocator` (48 call sites) as a second DI channel with a real composition root
- [ ] Resolve the namespace/folder disagreement: `AkariTool.Tabs` is declared in three assemblies
      and 8 catalog-only Core domains have folder paths no namespace matches
- [ ] Repair the two orphan subsystems — `TweakRegistry.Register` (0 call sites, so Verify is
      permanently broken) and the Builder/Review mode contracts that have no engine
- [ ] Enforce `SettingCatalogValidator` in production; the setting-ID uniqueness invariant that
      backup-file compatibility depends on is currently unenforced
- [ ] Resolve `AkariPaths` type shadowing — CS0436 at 10 sites in `SoftwareAppService.cs`; the
      compiler silently resolves to the App-layer definition over the Core one
- [ ] Build the missing machinery: Core contracts (32 vs Winhance's 90), section-page ViewModel
      base, per-domain marker interfaces, row-rendering primitives
- [ ] AdvancedTools: split the ~2,200-line code-behind page into Winhance's shape — hub page plus
      `WimUtilPage` and `AutounattendGeneratorPage`, with Core contracts and ViewModels
- [ ] SoftwareApps: consolidate 4 pages into 1 page + 2 tabs; expand to Winhance's 20 Core
      contracts and 28 Infrastructure services; add view/sort modes and help-content views
- [ ] Customize: add `IWallpaperService` + wallpaper applier + `WindowsTheme` special handler;
      merge Akari's standalone `Desktop` section into Winhance's Explorer `Desktop` group
- [ ] Localization: locale JSON, live language switch, per-setting keys derived from
      `SettingDefinition` — sequenced at the **top** of the feature stage, not the bottom
- [ ] Builder / Config Review mode engine — including fixing Winhance's own recording gap where
      `NumericRange` and AC/DC power edits are silently dropped
- [ ] Adopt Winhance's OTS (TrustedInstaller / SYSTEM) impersonation detection
- [ ] Test coverage capable of validating a restructure — there is no App test project and no
      integration test project today

### Out of Scope

- **Catalog content expansion** — the data is already ~95% at parity. This project re-shapes
  existing settings; it does not author new ones.
- **Downgrading WindowsAppSDK** — Akari is on 2.3.1, a full major version ahead of Winhance's
  1.8.260416003, targeting TFM 26100 against Winhance's 19041. Retain 2.3.1.
- **`.winhance` config import or interop** — deliberate fork (decision D6)
- **Copying any Winhance source code** — clean-room design parity (see Constraints)
- **Replicating Winhance's anti-patterns** — notably
  `Common/Interfaces/ISettingsFeatureViewModel.cs` importing `Optimize.ViewModels`, which inverts
  the intended dependency direction
- **Removing Akari-specific capability** — AkariOS, Home, Backup, Verify, Nvidia, Defender, and
  Tools all survive as extensions
- **Fixing the WinUI 3 PRI packaging failure** — the solution compiles; `dotnet build` remains
  unusable for this project per its existing constraint

## Context

- **Reference implementation**: Winhance at `C:\Users\isleap\Documents\GitHub\Winhance`, active at
  `v26.06.12` / Release 27. **Read-only** — never modified. Mapped in
  `.planning/reference/winhance/`.
- **Parity analysis**: `.planning/research/PARITY.md` (1,045 lines). Its headline finding is that
  the *data* is at parity while the *structure and machinery* are not, which validates the
  "restructure first" ordering. Effort profile: 19 small, 37 medium, 8 large — almost none of it
  catalog content.
- **Namespace/folder disagreement is the restructure's main hazard.** `AkariTool.Tabs` is declared
  in three assemblies; the 8 catalog-only Core domains have folder paths that no namespace matches,
  so a search for `AkariTool.Core.Features.Gaming` returns nothing. Any bulk move must establish
  namespaces deliberately rather than inferring them from folders.
- **`ILocalizationService` is a partially-wired stub, not absent.** `SettingStatusBannerManager` and
  `TechnicalDetailsManager` both accept it and are being passed `null` (build warning CS8604). There
  is a contract and call sites but no implementation, which is a different problem from "no
  localization" and changes the work estimate.
- **Two dead subsystems ship as working-looking features.** `Verify` shows "Nothing tracked yet"
  forever and the Builder/Review modes have no engine — both because contracts landed without their
  producers. Silent failure, not a crash, so it will not surface on its own.
- **Build status (2026-10-05)**: `AkariTool.sln` compiles all six projects. The App project's
  packaging step fails with `WINAPPSDKGENERATEPROJECTPRIFILE` errors PRI175 / PRI252 because
  `vendor/WinUI.Framework`'s PRI is not generated at the expected path. This is the known WinUI 3
  PRI fragility, not a code defect.
- **`vendor/WinUI.Framework`** is not in the solution and is still used for non-DI types including
  ViewModel base classes. Kept for now; full removal is a later, separately budgeted phase.
- Akari-specific scripts: 47 hand-authored embedded `.ps1` payloads.

## Constraints

- **Licensing — clean-room design parity (hard constraint).** Winhance is PolyForm Shield 1.0.0,
  which carries a **noncompete** clause, a **required notice**
  (`Required Notice: Copyright (c) 2025 Marco du Plessis (https://github.com/Jeyloh/Winhance)`), and
  patent-defense and cure provisions. Akari is a Windows optimization utility in the same category
  and already contains ~1:1 copies of Winhance's model layer. Parity therefore means matching
  Winhance's **architecture, conventions, feature set, and UX patterns while writing Akari's own
  implementation**. **No new file may be copied from Winhance.** Already-adopted 1:1 model types
  stay as they are and are out of scope for this project.
- **Licensing — outstanding compliance debt (not this project's scope, but must not be made
  worse).** Akari's README shows an MIT badge while no `LICENSE` file exists in the repository, and
  the PolyForm required notice is not shipped. Both predate this project. Getting a real legal
  opinion before the next release is recommended and is tracked outside this roadmap.
- **Tech stack**: WinUI 3 (WindowsAppSDK 2.3.1), .NET 10 (SDK 10.0.401 / global.json pin
  10.0.102), CommunityToolkit.Mvvm 8.4.2, C# 12. No new frameworks.
- **Build**: VS MSBuild only (`AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64`).
  `dotnet build` fails on WinUI 3 PRI/resource targets.
- **Unverified compatibility (gates the UX-parity work)**: Akari is on WindowsAppSDK 2.3.1 while
  Winhance is on 1.8.x, and Akari has never used `CommunityToolkit.WinUI`. Whether Winhance's
  `SettingsCard`, `DataGrid`, and `WrapPanel` resolve under 2.3.1 needs a spike before the
  UI-parity phases are planned.
- **Elevation**: the app is always-elevated by manifest. Operations needing SYSTEM or
  TrustedInstaller context use the existing impersonation path, not raw process launches.
- **Safety**: the pre-existing Core Value holds — every optimization must be safe, reversible, and
  legible to the user before and after. The restructure must not regress this.
- **Scope**: every Winhance capability is in scope. There is no anti-parity list; edge cases are
  decided at phase-planning time.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Clean-room design parity — match Winhance's design, never its code | PolyForm Shield's noncompete and notice terms; a 1:1 port strategy maximizes exposure | — Pending |
| Restructure first, then features | Stated driver is that the half-finished slice makes every feature harder than it should be. Every feature phase is cheaper on correct structure | — Pending |
| AkariOS becomes a 6th mirrored domain (D1) | Keeps 35 Infrastructure files and 2,631 lines of page code cohesive in one slice. Folding it into Optimize/AdvancedTools would cut one feature across three nav destinations | — Pending |
| Deliberate config-format fork; reject `.winhance` imports (D6) | Akari's setting IDs overlap Winhance's closely enough that an import could half-apply silently. Requires auditing ~430 IDs to do safely | — Pending |
| Verify → `Features/Common`; Tools → `Features/AdvancedTools`; Backup and Home as UI-only slices; Home stays the landing page (D5, D11) | Winhance proves a UI-only slice is legal — its own `Settings` domain has no Core or Infrastructure layer. Home as landing is Akari's one intentional navigation divergence | — Pending |
| Localization sequenced at the **top** of the feature stage | Winhance derives localization keys from `SettingDefinition`, so the key surface grows with the catalog. Every setting added after localization ships would go un-localized — invisibly, because the key-reference test only fails for keys that are requested | — Pending |
| Merge Akari's standalone `Desktop` section into Winhance's Explorer `Desktop` group (D3) | 12 settings; it is Winhance's shape and Akari's settings already claim Explorer-family membership | — Pending |
| Consolidate SoftwareApps to 1 page + 2 tabs, absorbing Debloat into the removal flow (D4) | Winhance's shape; 4 page moves | — Pending |
| Converge on one JSON preference store keyed from `UserPreferenceKeys`, migrating `UiPreferences` section-collapse state into it (D7) | N12 and N1 both need a single preference store, and localization depends on it | — Pending |
| Keep embedded `.ps1` payloads **and** add a Scripts-folder menu item exposing a copy (D8) | 47 hand-authored scripts are real Akari value that Winhance's generate-to-`%ProgramData%` approach would lose | — Pending |
| Keep `vendor/WinUI.Framework` for non-DI types; delete only `IoC` usage (D9) | Affects every ViewModel base class; full removal needs separate budget | — Pending |
| Fix Winhance's Builder-mode recording gap rather than replicating it (D10) | Winhance's own `BuilderEdit.cs` documents that `NumericRange` and AC/DC power edits are not recorded. Do not ship a mode that silently drops numeric edits | — Pending |
| Do not replicate Winhance's `ISettingsFeatureViewModel` → `Optimize.ViewModels` inverted dependency | Documented in the reference map as an anti-pattern; replicating it would bake a layering violation into Akari's new structure | — Pending |
| `CompatibleSettingsRegistry.GetKnownFeatureProviders()` is the one file the restructure must not move or rewrite | Already the correct shape and the single structural anchor that is right today | — Pending |
| Treat the mode system and localization as "engine missing," not "contract missing" | Both shipped their contracts without producers. Planning them as new work overstates the cost | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-10-05 after initialization*