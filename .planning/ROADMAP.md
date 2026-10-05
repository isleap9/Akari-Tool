# Roadmap: Akari Tool

## Overview

This milestone turns Advanced Tools ▸ System Tools — the last major page in Akari Tool still
built as imperative WinUI code-behind — into a structured hub of purpose-built, scan-then-review
tools. The journey runs: establish the hub shell and port the two actions that survive unchanged
in meaning (Repair & Health, Quick Shortcuts); pay down the shared async scan/review scaffolding
on the cheapest scan surface (uninstaller leftovers); build the three System Cleaner scans in
increasing order of complexity (junk/temp + large files, then content-hash duplicates); replace
the all-or-nothing `network-apply.bat` with a granular per-adapter/per-value NIC UI that has real
revert; and only then delete the legacy `ToolsPage.xaml(.cs)` and repoint the nav in one atomic
swap. The user's System Tools entry point works before, during, and after the milestone — never
is there a moment where they open it and find nothing.

## Milestones

- 📋 **v1.0 System Tools Rework** — Phases 1-6 (roadmap created, not started)

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Phase numbering starts at 1 — this is the project's first milestone, so there is no prior
milestone to continue numbering from.

- [ ] **Phase 1: System Tools Hub** - Card-based hub (matching `AdvancedHubPage`) with Repair & Health and Quick Shortcuts rewritten fresh as working destinations
- [ ] **Phase 2: Deep-Clean Uninstaller** - On-demand leftover scan for a chosen app, grouped by removal mechanism, nothing pre-checked, delete only on explicit confirm
- [ ] **Phase 3: System Cleaner — Junk & Large Files** - Categorized junk/temp scan-then-review to the Recycle Bin plus a read-only large-file finder
- [ ] **Phase 4: Duplicate-File Finder** - Content-hash duplicate groups with one protected reference file per group and Recycle Bin deletion
- [ ] **Phase 5: Granular NIC Tweak UI** - Per-adapter, per-value toggles sourced from the driver's real value set, with snapshot-backed per-value revert; retires the batch scripts
- [ ] **Phase 6: Hub Routing Complete & Legacy ToolsPage Removal** - All five hub routes live, nav repointed, `ToolsPage.xaml(.cs)` deleted outright

## Phase Details

### Phase 1: System Tools Hub

**Goal**: When the user opens System Tools they get a card-based hub instead of one long
scrolling imperative page, and the two actions that survive conceptually — Repair & Health and
Quick Shortcuts — already work as real destinations on it.

**Depends on**: Nothing (first phase)

**Requirements**: HUB-01, REPAIR-01, SHORT-01

**Success Criteria** (what must be TRUE):
1. Opening Advanced Tools ▸ System Tools shows a hub of cards laid out like the existing
   Advanced Tools hub, not one long scrolling page with everything stacked on it.
2. From the hub the user opens Repair & Health and runs an SFC scan, a DISM repair, and creates
   a restore point — each shows progress and a result before returning to the hub.
3. From the hub the user opens Quick Shortcuts and launches any of the 10 shortcuts (Task
   Manager, Startup Apps, MSConfig, Device Manager, Event Viewer, Windows Update, Disk
   Management, Services, Resource Monitor, Registry Editor) and the target tool actually opens.
4. At no point during the milestone is the user left without a working System Tools entry point:
   temp cleanup, DNS/Winsock actions, repair and shortcuts all stay reachable until each one is
   ported to the new hub.

**Rationale**: Research proposed a hub-scaffold-only Phase 1 and deferred Repair & Health /
Quick Shortcuts to its final cleanup phase. Deviating: those two are user-facing page rewrites,
not cleanup work — folding them into the deletion phase would have made that phase a grab-bag and
would have left the hub a dead shell for the entire milestone. Landing them here means the hub is
a genuinely usable destination from the first phase, and Phase 6 becomes a single-purpose
deletion. Neither page is new scope: both reproduce actions the legacy `ToolsPage.xaml.cs` already
performs, written fresh with no code carried over.

**Notes**: The hub's Cleaner, Uninstaller and Network cards appear as each tool ships (Phases 2,
3, 5). The complete five-route state is verified in Phase 6 (HUB-02). The legacy page is retained
in the project — and stays reachable for un-ported actions — until Phase 6.

**Plans**: TBD

**UI hint**: yes

### Phase 2: Deep-Clean Uninstaller

**Goal**: The user can ask what a previously-uninstalled app left behind, see the answer grouped
by removal mechanism with nothing selected, and delete only the rows they personally ticked.

**Depends on**: Phase 1

**Requirements**: UNINST-01, UNINST-02, UNINST-03, UNINST-04

**Success Criteria** (what must be TRUE):
1. The user picks a previously-uninstalled app and triggers a leftover scan on demand, and sees
   live progress while it runs without the UI freezing or a restart being required.
2. The scan surfaces that app's leftover registry keys, leftover folders and leftover scheduled
   tasks, and each result shows the signal it was attributed by (path / publisher / name).
3. Results are grouped into Files & Folders, Registry Keys and Scheduled Tasks sections, and
   every row arrives unchecked.
4. Nothing is removed until the user ticks specific rows and confirms a dialog — dismissing that
   confirm removes nothing, and a row left unchecked is never touched.

**Rationale**: Unchanged from the research ordering. This is the cheapest scan surface (bounded
registry hives plus a folder/task check, no hashing), which makes it the right place to establish
the shared async-scan (`Task.Run` + `IProgress` + `CancellationTokenSource`) and grouped-row-VM
scaffolding that Phases 3-5 reuse. It is also the first tool to hit the pre-existing
ACL/`SecurityException` handling gap in the registry seam, so fixing that here means later phases
inherit the fix. This phase is additive to the existing Software-tab bulk uninstaller, which is
left untouched.

**Notes**: ACL/`SecurityException` gap in the registry seam is fixed in this phase. Attribution
requires multiple corroborating signals and cross-references live installed-program state, so
shared components (VC++ redistributables, .NET runtime, Common Files, MSIX framework packages)
are not flagged as removable leftovers.

**Plans**: TBD

**UI hint**: yes

### Phase 3: System Cleaner — Junk & Large Files

**Goal**: The user scans junk/temp categories and a chosen drive for large files, reviews
exactly what was found and how big it is, then clears only what they selected — into the Recycle
Bin — and sees how much space came back.

**Depends on**: Phase 2

**Requirements**: CLEAN-01, CLEAN-02, CLEAN-03, CLEAN-04, LGFILE-01

**Success Criteria** (what must be TRUE):
1. A junk scan finds temp files, Windows Update leftovers, icon cache and similar categories,
   listed as distinct categories with per-category and per-item sizes — replacing the old one-shot
   "Clear Temp Files" / "Disk Cleanup" buttons, which no longer exist on this path.
2. The junk review list shows every candidate with its size and path, and nothing is pre-checked.
3. Selected junk items land in the Recycle Bin and are restorable from it.
4. After a delete the user sees a summary of how many items were removed and how much space was
   reclaimed.
5. The user picks a drive or folder, scans it, and gets its largest files sorted by size with
   path, type and last-modified context — read-only discovery with no delete action in v1.

**Rationale**: Deviating from the research's 7-phase structure by merging the Large-File Finder
into the junk/temp phase. LGFILE-01 is a single read-only requirement with no delete path in v1 —
too thin to justify its own phase at `standard` granularity — and it consumes exactly the same
streaming directory-enumeration scaffolding as the junk scan. Risk still rises within the phase:
junk/temp (simple enumeration) first, large-file (size ordering + risk flagging for VHDX / DB /
save-game locations) second.

**Notes**: Locked or in-use files are detected before being flagged rather than force-deleted.
Enumeration streams via `Directory.EnumerateFiles` / `FileSystemEnumerable` with inaccessible
entries ignored — never `Directory.GetFiles(..., AllDirectories)`, which aborts the whole scan on
the first ACL error.

**Plans**: TBD

**UI hint**: yes

### Phase 4: Duplicate-File Finder

**Goal**: The user finds byte-identical files grouped into duplicate sets, sees one protected
reference file per set, and clears the remainder through the Recycle Bin.

**Depends on**: Phase 3

**Requirements**: DUPE-01, DUPE-02, DUPE-03

**Success Criteria** (what must be TRUE):
1. A duplicate scan groups files into duplicate sets by content, and two files that merely share a
   size or name are never placed in the same set.
2. Each set shows one reference file that cannot be selected, and no other row in any set is
   pre-checked.
3. Deleted duplicates land in the Recycle Bin, and the protected reference file of each set is
   never deleted.

**Rationale**: Deviating by merging research Phase 5 (Duplicate-File Finder) — no, this *is* that
phase, kept separate and sequenced last of the three Cleaner scans. It is the hardest scan
(size-bucket → prefix-hash → full-hash → byte-compare funnel), the only one with real
performance-tuning risk on multi-TB volumes, and the one where a correctness bug means silent data
loss — so it lands after the junk and large-file scans have proven the scan/review pattern.

**Notes**: Research flag — this phase warrants a deeper research pass at planning time on funnel
correctness and throttled progress reporting if the roadmap targets multi-TB volumes. Pairs inside
two different `Program Files` vendor trees or game-library trees are excluded or specially warned
about, since byte-identical files there can be semantically independent per-app install assets.
Uses `System.IO.Hashing` (XxHash3/XxHash64) — the only new package this milestone needs.

**Plans**: TBD

**UI hint**: yes

### Phase 5: Granular NIC Tweak UI

**Goal**: The user picks one adapter, sees exactly which values that adapter's driver reports
alongside their current values, changes individual values, and can put any single value back
exactly as it was.

**Depends on**: Phase 1

**Requirements**: NIC-01, NIC-02, NIC-03, NIC-04, NIC-05

**Success Criteria** (what must be TRUE):
1. The user selects a target network adapter before any tweak becomes available; nothing in this
   UI ever writes to every adapter at once.
2. The tweak list shows only values the selected adapter's driver actually reports, each as its
   own toggle — interrupt moderation, RSS, offloads, buffer sizes and the rest — read from the
   adapter at runtime rather than from a hardcoded app-side table.
3. Every row shows the adapter's current value before any change is made, in the same
   current-versus-recommended shape the Power tab already uses.
4. After a change, the user can revert that one value and it returns to precisely the value it
   held before — including going back to "did not exist" and removing it again — not to a generic
   default.
5. The legacy `Scripts/Network/network-apply.bat` and `network-revert.bat` are gone, and the
   AkariOS Gaming Tweaks "Network Optimization" toggle no longer routes through them.

**Rationale**: Kept at the research's position — architecturally independent of the scan/review
tools (different Infrastructure service, different Core models) but placed after the
scan-then-review pattern exists to crib from, because the per-value snapshot/revert log is the one
genuinely new model in this milestone with no existing precedent in the app. It depends on Phase 1
for its hub destination only; it does not depend on Phases 2-4 and can be planned in parallel.

**Notes**: Research flag — confirm the enumeration/apply/revert flow against at least two different
physical NIC vendors/drivers before finalizing the UI contract; research verified the general
driver-declared-parameter pattern but not per-vendor behavior. Also open at planning time: whether
per-value NIC state should route through the existing `SettingBackupService` backup/restore plus
global-search infrastructure (every other tab does) or intentionally stay out of scope — that
decision gets made and documented in this phase, not left implicit. Writes go through
`IPowerShellRunner`'s `NetAdapter` cmdlets rather than the unstable
`HKLM\...\Control\Class\{...}\NNNN` ordinal the batch script used.

**Plans**: TBD

**UI hint**: yes

### Phase 6: Hub Routing Complete & Legacy ToolsPage Removal

**Goal**: System Tools has exactly one entry point — the new hub — with all five tools reachable
from it, and the legacy imperative `ToolsPage.xaml(.cs)` deleted outright with the nav repointed.

**Depends on**: Phases 1, 2, 3, 4, 5

**Requirements**: HUB-02, HUB-03

**Success Criteria** (what must be TRUE):
1. Every card on the System Tools hub opens a working page — Repair & Health, Cleaner,
   Uninstaller, Network and Quick Shortcuts — with no dead or placeholder card left anywhere.
2. Opening System Tools from anywhere in the app (the Advanced Tools card and the nav-tag route)
   lands on the new hub, and the legacy `ToolsPage.xaml` / `ToolsPage.xaml.cs` no longer exist in
   the project.
3. Every action the old System Tools page offered is still reachable from the new hub — the user
   can flush DNS, reset Winsock, switch DNS provider, run repair, and open the quick shortcuts.

**Rationale**: Same position as the research's final cleanup phase, and last by hard constraint
from PROJECT.md — `ToolsPage.xaml(.cs)` is deleted only once the new hub covers all surviving
functionality, so the user is never left without a working System Tools entry point mid-milestone.
HUB-02 lands here rather than in Phase 1 because "the hub routes to all five pages" is only true
once all five pages exist; Phase 1 delivers the hub shell and two live routes, each subsequent
phase adds one more card, and this phase is where the complete five-route state is verified.

**Notes**: HUB-03 is one atomic change — delete the files and repoint both
`MainWindow.xaml.cs`'s `["Tools"] = typeof(ToolsPage)` and `AdvancedHubPage`'s "System Tools"
card in the same change, so there is no window in which either route points at a missing page.
No System Information card is reintroduced — Home already shows this via `SystemInfoService.Gather()`.

**Plans**: TBD

**UI hint**: yes

## Phase Ordering Rationale

- **Hub first** establishes navigation before any tool has a destination, and matches the existing
  `AdvancedHubPage` / `HubView` pattern exactly — low risk, no new research needed.
- **Leftover cleanup before the disk scans** because it is the cheapest tool on which to establish
  the shared async-scan/review scaffolding, and because it is the first tool to hit the
  pre-existing registry ACL/`SecurityException` gap — fixing it here means every later phase
  inherits the fix instead of rediscovering it.
- **Cleaner scans by rising complexity** — junk/temp with large files, then content-hash
  duplicates — so risk is paid down incrementally rather than starting with the scan most likely
  to cause silent data loss if its funnel is wrong.
- **NIC tweak UI last of the features** because its snapshot/revert model has no precedent to
  crib from; it is architecturally independent and its Phase 1 dependency is only for the hub
  destination, so it may be planned in parallel with Phases 2-4.
- **Legacy `ToolsPage` deletion absolutely last**, as a standalone capstone — mandated by
  PROJECT.md and reinforced in the research.

### Deviations from the Research's Proposed 7-Phase Structure

| Research phase | Roadmap phase | Change | Reason |
|----------------|---------------|--------|--------|
| 1 — Hub Scaffold | 1 — System Tools Hub | Absorbed Repair & Health + Quick Shortcuts (REPAIR-01, SHORT-01) from research Phase 7 | They are user-facing page rewrites, not cleanup work; landing them here makes the hub a usable destination from the start and keeps the deletion phase single-purpose |
| 3 — Junk/Temp | 3 — System Cleaner: Junk & Large Files | Absorbed Large-File Finder (LGFILE-01) from research Phase 4 | A single read-only requirement with no v1 delete path is too thin for its own phase at `standard` granularity, and it reuses the same enumeration scaffolding as the junk scan |
| 4 — Large-File Finder | — | Folded into Phase 3 | As above |
| 2, 5, 6 | 2, 4, 5 | Numbered down one | Mechanical consequence of the two merges above |
| 7 — Remove Legacy ToolsPage | 6 — Hub Routing Complete & Legacy ToolsPage Removal | Kept last and standalone; additionally now owns HUB-02 (all five routes verified live) | HUB-02 is only satisfied once every destination page exists, which is exactly this moment |

## Requirement Coverage

All 22 v1 requirements map to exactly one phase.

| Category | Requirements | Phases |
|----------|--------------|--------|
| System Tools Hub | HUB-01, HUB-02, HUB-03 | 1, 6, 6 |
| Repair & Health | REPAIR-01 | 1 |
| Quick Shortcuts | SHORT-01 | 1 |
| Deep-Clean Uninstaller | UNINST-01, UNINST-02, UNINST-03, UNINST-04 | 2 |
| System Cleaner — Junk/Temp | CLEAN-01, CLEAN-02, CLEAN-03, CLEAN-04 | 3 |
| Large-File Finder | LGFILE-01 | 3 |
| Duplicate-File Finder | DUPE-01, DUPE-02, DUPE-03 | 4 |
| Granular NIC Tweak UI | NIC-01, NIC-02, NIC-03, NIC-04, NIC-05 | 5 |

**Coverage:**
- v1 requirements: 22 total
- Mapped to phases: 22
- Unmapped: 0 ✓

### Deliberately Not Phased (v2 — deferred, tracked, not in this roadmap)

UNINST-05 (post-uninstall opt-in leftover-scan prompt), UNINST-06 (attribution confidence tiers),
LGFILE-02 (large-file delete flow), DUPE-04 (fuzzy filename duplicate matching), NIC-06
("suggested selection" presets). These are recorded in REQUIREMENTS.md under v2 Requirements and
intentionally have no phase in v1.0.

### Explicitly Out of Scope (no phase, per REQUIREMENTS.md)

Registry "cleaning" sweeps, one-click "Clean Everything"/"Optimize Now", auto-applied NIC presets,
permanent delete as the file-delete default, silent automatic leftover scans, rebuilding the App
Uninstaller's bulk uninstall, the System Information card, per-app/game QoS prioritization,
latency/route diagnostics, and TCP stack tuning UI.

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. System Tools Hub | 0/TBD | Not started | - |
| 2. Deep-Clean Uninstaller | 0/TBD | Not started | - |
| 3. System Cleaner — Junk & Large Files | 0/TBD | Not started | - |
| 4. Duplicate-File Finder | 0/TBD | Not started | - |
| 5. Granular NIC Tweak UI | 0/TBD | Not started | - |
| 6. Hub Routing Complete & Legacy ToolsPage Removal | 0/TBD | Not started | - |

---
*Roadmap created: 2026-10-05 for milestone v1.0 System Tools Rework*
*Derived from .planning/research/SUMMARY.md (7-phase proposal, compressed to 6 — see Deviations above)*
