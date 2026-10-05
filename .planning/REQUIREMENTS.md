# Requirements: Akari Tool — Winhance Parity

**Milestone:** v1.0 Winhance Parity
**Defined:** 2026-10-05
**Core Value:** Every new capability must be addable as one self-contained vertical slice — a Core
contract, an Infrastructure service, and a UI page — without touching code outside its own feature
folder.

## v1 Requirements

Requirements for this milestone. Each maps to exactly one roadmap phase.

### Verification Baseline

Established before any restructure, so the restructure can prove it regressed nothing.

- [ ] **SPIKE-01**: A recorded build baseline exists (assemblies produced, warning and error
      counts) so later phases can demonstrate the restructure introduced no regressions
- [ ] **SPIKE-02**: A spike confirms Winhance's `SettingsCard`, `DataGrid`, and `WrapPanel` render
      correctly under WindowsAppSDK 2.3.1 with `CommunityToolkit.WinUI`, or names the substitutes
      to use instead
- [ ] **SPIKE-03**: A generated report diffs every Akari setting ID against Winhance's, so
      config-format divergence is measured rather than assumed

### Architecture Restructure

- [ ] **ARCH-01**: Every feature domain exists at the same relative path in Core, Infrastructure,
      and UI
- [ ] **ARCH-02**: Optimize is one slice spanning its hub and 6 detail pages (Gaming, Privacy,
      Power, Notifications, Windows Update, Sound), each with Core models, Infrastructure services,
      and a UI page
- [ ] **ARCH-03**: Customize is one slice spanning its hub and 4 detail pages (Explorer including
      Desktop, Start Menu, Taskbar, Appearance), each with the full three-layer shape
- [ ] **ARCH-04**: Advanced Tools is a hub plus separate WIM/ISO and autounattend generator pages
- [ ] **ARCH-05**: Software & Apps is one page with Windows Apps and External Apps tabs
- [ ] **ARCH-06**: AkariOS is a sixth mirrored slice with Core contracts, Infrastructure services,
      and a UI page, keeping all 35 Infrastructure files and its catalog cohesive
- [ ] **ARCH-07**: Settings, Home, and Backup are UI-only slices with no Core or Infrastructure
      layer, proving the pattern Winhance's own Settings domain uses
- [ ] **ARCH-08**: A single composition root registers every service, and no `ServiceLocator` call
      sites remain
- [ ] **ARCH-09**: Every type's namespace matches its folder path across all three layers, so
      searching a domain's namespace finds its code
- [ ] **ARCH-10**: No code remains in Akari's legacy top-level App directories (`Views/`,
      `ViewModels/`, `Services/`, `Defender/`, `Nvidia/`, `Scripts/`, `DI/`, `Resource/`)
- [ ] **ARCH-11**: All services are registered explicitly, with no assembly scanning
- [ ] **ARCH-12**: The Common layer depends on no feature domain

### Defects and Dead Subsystems

- [ ] **BUG-01**: The Verify page lists the settings actually in effect and flags drift, instead of
      permanently showing "Nothing tracked yet"
- [ ] **BUG-02**: Setting-ID uniqueness is enforced at startup, so a duplicate ID fails loudly
      rather than silently corrupting backup-file compatibility
- [ ] **BUG-03**: The ambiguous `AkariPaths` type resolves to one definition, and no call site
      silently reads the wrong one
- [ ] **BUG-04**: `TweakRegistry`, `TweakTargets`, `TweakDefinition`, and `UpdateTweaks.cs` are
      deleted once confirmed unreferenced
- [ ] **BUG-05**: `DispatcherService` exists once — the byte-identical duplicate and the sole
      UI-framework import inside Infrastructure are both removed
- [ ] **BUG-06**: Akari's TrustedInstaller/SYSTEM impersonation handling detects and reports the
      same conditions Winhance's does

### Missing Machinery

- [ ] **CORE-01**: Every Infrastructure service has a corresponding Core interface, bringing Core's
      contract count to parity with Winhance's 90
- [ ] **CORE-02**: All settings pages share a common section-page ViewModel base instead of each
      page implementing hub behavior independently
- [ ] **CORE-03**: Each domain declares a marker interface the navigation layer can use for
      discovery
- [ ] **CORE-04**: Setting rows render through shared primitives — badge, technical details, status
      banner — rather than per-page duplication
- [ ] **CORE-05**: Hub pages build their cards from data rather than constructing them in
      code-behind
- [ ] **CORE-06**: Catalog aggregation happens at exactly one registration point, and the existing
      `CompatibleSettingsRegistry.GetKnownFeatureProviders()` anchor is preserved rather than
      rewritten

### Advanced Tools

- [ ] **ADV-01**: The WIM/ISO wizard runs from its own page with progress reporting and error
      handling, not from a monolithic code-behind panel
- [ ] **ADV-02**: The autounattend generator runs from its own page with Core contracts and a
      ViewModel
- [ ] **ADV-03**: Both tools' logic is reachable through DI-registered Infrastructure services
      rather than static calls from XAML code-behind

### Software & Apps

- [ ] **SOFT-01**: Software & Apps presents one page with Windows Apps and External Apps tabs
- [ ] **SOFT-02**: Users can switch the app list between card, table, and compact views
- [ ] **SOFT-03**: Users can sort the app list
- [ ] **SOFT-04**: Users see help content for the catalog rather than a bare list
- [ ] **SOFT-05**: WinGet installation runs through the shared CLI runner with progress parsing and
      exit-code handling
- [ ] **SOFT-06**: Chocolatey bootstrap and ghost-package recovery work
- [ ] **SOFT-07**: Debloat removal is reachable from the app removal flow
- [ ] **SOFT-08**: A user's app selection can be handed to Advanced Tools

### Customize

- [ ] **CUST-01**: Users can set a desktop wallpaper from the Customize slice
- [ ] **CUST-02**: Switching to the Windows theme sets light and dark app modes correctly, asks for
      confirmation, restarts Explorer, and can restore the OS default wallpaper
- [ ] **CUST-03**: Akari's 12 Desktop settings appear inside the Explorer section rather than as a
      separate page

### Localization

- [ ] **I18N-01**: Every user-facing string resolves through a localization service rather than an
      inline literal, including the currently null-passed `ILocalizationService` call sites
- [ ] **I18N-02**: Users can switch UI language live without restarting the app
- [ ] **I18N-03**: Setting labels and descriptions localize via keys derived from
      `SettingDefinition`, so keys grow with the catalog automatically
- [ ] **I18N-04**: A guardrail test fails when a requested key is missing from a locale file
- [ ] **I18N-05**: Language preference persists in the single JSON preference store

### Modes

- [ ] **MODE-01**: Users can enter Builder mode, stage setting changes, and review them as a set
      before applying
- [ ] **MODE-02**: Users can enter Config Review mode and inspect the staged configuration
- [ ] **MODE-03**: Numeric-range edits are recorded in Builder mode — Akari fixes Winhance's gap
      here rather than replicating it
- [ ] **MODE-04**: AC/DC power-plan edits are recorded in Builder mode
- [ ] **MODE-05**: Leaving a mode raises the corresponding event, and surfaces that listen for it
      actually respond

### Preferences

- [ ] **PREF-01**: Language, theme, display toggles, and section-collapse state all live in one
      JSON preference store keyed from `UserPreferenceKeys`
- [ ] **PREF-02**: Existing `UiPreferences` section-collapse state migrates into that store

### Scripts

- [ ] **SCR-01**: Users can open a folder containing copies of the app's embedded scripts, so the
      generate-to-folder capability Winhance has is not lost

### Tests

- [ ] **TEST-01**: An App-layer test project exists, so UI-adjacent logic is testable at all
- [ ] **TEST-02**: An integration test verifies the DI container resolves every registered service
- [ ] **TEST-03**: A test asserts Core references neither Infrastructure nor UI
- [ ] **TEST-04**: Tests cover Builder-mode recording of numeric-range and AC/DC edits

## v2 Requirements

Deferred to a future release. Tracked but not in the current roadmap.

### Localization

- **I18N-06**: All 29 of Winhance's locales ship — v1 delivers the mechanism plus the initial
  locale set
- **I18N-07**: RTL locales drive right-to-left flow direction

### Architecture

- **ARCH-13**: `vendor/WinUI.Framework` is fully removed and all ViewModel base types are brought
  into Common — v1 keeps the vendored framework and deletes only its `IoC` usage

### Modes

- **MODE-06**: Builder and Config Review modes persist staged changes across app restarts

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Authoring new setting content | The catalog is already ~95% at parity with Winhance (Optimize 301 vs ~288). This project re-shapes existing settings, it does not add new ones |
| Reconciling Akari's setting count with Winhance's | Akari is ahead by 13 in Optimize and behind by 9 in Customize; both are content differences, not structural gaps |
| Downgrading WindowsAppSDK to 1.8 | Akari is on 2.3.1, a full major version ahead, targeting TFM 26100 against Winhance's 19041. Retaining 2.3.1 is a constraint |
| `.winhance` config import or interop | Deliberate fork (D6) — setting IDs overlap closely enough that an import could half-apply silently |
| Copying any Winhance source code | Clean-room design parity — PolyForm Shield's noncompete and notice terms |
| Replicating Winhance's `ISettingsFeatureViewModel` → `Optimize.ViewModels` inverted dependency | Documented in the reference map as an anti-pattern; replicating it would bake a layering violation into the new structure |
| Removing Akari-specific capability | AkariOS, Home, Backup, Verify, Nvidia, Defender, and Tools are all kept as extensions |
| Converting Akari to Winhance's always-elevated launcher model | Already identical — both manifests are `requireAdministrator` |
| Replacing Akari's COM file pickers | Already at parity — `AkariFileService` uses the same approach for the same documented reason |
| Legal compliance remediation (MIT badge vs. missing LICENSE file, unshipped PolyForm notice) | Pre-existing debt that predates this project. Getting a legal opinion before the next release is recommended and tracked outside this roadmap |
| Fixing the pre-existing WinUI 3 PRI packaging failure | The solution compiles all six assemblies; PRI175/PRI252 is the known WinUI 3 resource-target fragility |
| Migrating Akari's 47 embedded `.ps1` payloads to generate-to-`%ProgramData%` | Embedded scripts are real Akari value; a Scripts-folder menu item preserves the capability without the migration |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| SPIKE-01 | Phase 1 | Pending |
| SPIKE-02 | Phase 1 | Pending |
| SPIKE-03 | Phase 1 | Pending |
| ARCH-01 | Phase 4 | Pending |
| ARCH-02 | Phase 4 | Pending |
| ARCH-03 | Phase 10 | Pending |
| ARCH-04 | Phase 7 | Pending |
| ARCH-05 | Phase 9 | Pending |
| ARCH-06 | Phase 4 | Pending |
| ARCH-07 | Phase 4 | Pending |
| ARCH-08 | Phase 3 | Pending |
| ARCH-09 | Phase 3 | Pending |
| ARCH-10 | Phase 4 | Pending |
| ARCH-11 | Phase 3 | Pending |
| ARCH-12 | Phase 4 | Pending |
| BUG-01 | Phase 2 | Pending |
| BUG-02 | Phase 2 | Pending |
| BUG-03 | Phase 2 | Pending |
| BUG-04 | Phase 2 | Pending |
| BUG-05 | Phase 2 | Pending |
| BUG-06 | Phase 2 | Pending |
| CORE-01 | Phase 5 | Pending |
| CORE-02 | Phase 5 | Pending |
| CORE-03 | Phase 5 | Pending |
| CORE-04 | Phase 5 | Pending |
| CORE-05 | Phase 5 | Pending |
| CORE-06 | Phase 5 | Pending |
| ADV-01 | Phase 7 | Pending |
| ADV-02 | Phase 7 | Pending |
| ADV-03 | Phase 7 | Pending |
| SOFT-01 | Phase 9 | Pending |
| SOFT-02 | Phase 9 | Pending |
| SOFT-03 | Phase 9 | Pending |
| SOFT-04 | Phase 9 | Pending |
| SOFT-05 | Phase 9 | Pending |
| SOFT-06 | Phase 9 | Pending |
| SOFT-07 | Phase 9 | Pending |
| SOFT-08 | Phase 9 | Pending |
| CUST-01 | Phase 10 | Pending |
| CUST-02 | Phase 10 | Pending |
| CUST-03 | Phase 10 | Pending |
| I18N-01 | Phase 6 | Pending |
| I18N-02 | Phase 6 | Pending |
| I18N-03 | Phase 6 | Pending |
| I18N-04 | Phase 6 | Pending |
| I18N-05 | Phase 6 | Pending |
| MODE-01 | Phase 8 | Pending |
| MODE-02 | Phase 8 | Pending |
| MODE-03 | Phase 8 | Pending |
| MODE-04 | Phase 8 | Pending |
| MODE-05 | Phase 8 | Pending |
| PREF-01 | Phase 6 | Pending |
| PREF-02 | Phase 6 | Pending |
| SCR-01 | Phase 7 | Pending |
| TEST-01 | Phase 1 | Pending |
| TEST-02 | Phase 3 | Pending |
| TEST-03 | Phase 3 | Pending |
| TEST-04 | Phase 8 | Pending |

**Coverage:**
- v1 requirements: 58 total
- Mapped to phases: 58
- Unmapped: 0 ✓

---
*Requirements defined: 2026-10-05*
*Last updated: 2026-10-05 after initial definition*