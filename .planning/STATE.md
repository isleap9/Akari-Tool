---
gsd_state_version: "1.0"
milestone: v1.0
milestone_name: System Tools Rework
status: planning
last_updated: "2026-10-05T00:00:00.000Z"
last_activity: 2026-10-05
progress:
  total_phases: 6
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-05)
Roadmap: .planning/ROADMAP.md (v1.0 System Tools Rework — Phases 1-6, created 2026-10-05)
Requirements: .planning/REQUIREMENTS.md (22 v1 requirements, all mapped)

**Core value:** Every optimization the app performs must be safe, reversible, and legible to the
user before and after it happens — no silent all-or-nothing scripts, no orphaned leftovers, no
changes the user can't see or undo.
**Current focus:** Phase 1 — System Tools Hub (ready to plan)

## Current Position

Phase: 1 of 6 (System Tools Hub)
Plan: — (not yet planned)
Status: Ready to plan
Last activity: 2026-10-05 — Roadmap created, all 22 v1 requirements mapped (Unmapped: 0)

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:** 0 plans completed, 0.0 hours total execution time.

**By Phase:**

| Phase | Plans | Avg/Plan |
|-------|-------|----------|
| 1. System Tools Hub | 0 | — |
| 2. Deep-Clean Uninstaller | 0 | — |
| 3. System Cleaner — Junk & Large Files | 0 | — |
| 4. Duplicate-File Finder | 0 | — |
| 5. Granular NIC Tweak UI | 0 | — |
| 6. Hub Routing Complete & Legacy ToolsPage Removal | 0 | — |

## Accumulated Context

### Decisions

Roadmap-level decisions (full log in PROJECT.md):

- **Roadmap:** Research proposed 7 phases; roadmap uses 6 — merged Large-File Finder into the
  junk/temp Cleaner phase (LGFILE-01 is thin, read-only, reuses the same enumeration
  scaffolding), and moved Repair & Health + Quick Shortcuts from the final cleanup phase into
  Phase 1 so the hub is a usable destination immediately.
- **Roadmap:** HUB-02 (all five hub routes live) lands in Phase 6, not Phase 1 — "routes to all
  five pages" is only true once every destination exists, which is the moment the legacy
  `ToolsPage` can be deleted.
- **Roadmap:** Phase 5 (NIC) depends only on Phase 1 for its hub destination; it may be planned
  in parallel with Phases 2-4.

### Pending Todos

None yet.

### Blockers/Concerns
- **Phase 2:** Scope the pre-existing registry ACL/`SecurityException` gap fix — small wrapper at
  new call sites or a broader `WindowsRegistryService` change (CONCERNS.md, research Gap).
- **Phase 4:** Duplicate-finder funnel correctness and throttled progress need a research pass
  before planning if multi-TB volumes are in scope.
- **Phase 5:** Validate NIC enumeration/apply/revert against at least two NIC vendors/drivers.
- **Phase 5:** Decide whether per-value NIC state routes through `SettingBackupService`
  backup/restore + global search, or stays out of scope. Must be decided, not left implicit.
- **Phase 5:** Old `Scripts/Network/network-apply.bat` is still wired into AkariOS Gaming Tweaks
  (`SetNetworkOptimization`) — retiring it in Phase 6-era cleanup affects that toggle.

## Deferred Items

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| Uninstaller | UNINST-05 post-uninstall opt-in scan prompt | v2 | 2026-08-27 | v1.0 |
| Uninstaller | UNINST-06 attribution confidence tiers | v2 | 2026-08-27 | v1.0 |
| Large-File Finder | LGFILE-02 delete flow via review/Recycle Bin | v2 | 2026-08-27 | v1.0 |
| Duplicate Finder | DUPE-04 fuzzy filename matching | v2 | 2026-08-27 | v1.0 |
| NIC Tweaks | NIC-06 suggested-selection presets | v2 | 2026-08-27 | v1.0 |

## Session Continuity

Last session: 2026-10-05
Stopped at: Roadmap created — 6 phases derived, 22/22 v1 requirements mapped, Traceability
populated
Resume file: None
