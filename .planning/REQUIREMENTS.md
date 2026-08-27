# Requirements: Akari Tool — System Tools Rework

**Defined:** 2026-08-27
**Core Value:** Every optimization the app performs must be safe, reversible, and legible to the
user before and after it happens — no silent all-or-nothing scripts, no orphaned leftovers, no
changes the user can't see or undo.

## v1 Requirements

Requirements for this milestone. Each maps to roadmap phases.

### System Tools Hub

- [ ] **HUB-01**: User sees a "System Tools" hub with cards, matching `AdvancedHubPage`'s existing pattern
- [ ] **HUB-02**: Hub routes to Repair & Health, Cleaner, Uninstaller, Network, and Quick Shortcuts pages
- [ ] **HUB-03**: `ToolsPage.xaml`/`.xaml.cs` is deleted outright; `MainWindow.xaml.cs`'s nav-tag mapping and `AdvancedHubPage`'s "System Tools" card are repointed to the new hub

### Repair & Health

- [ ] **REPAIR-01**: User can run SFC scan, DISM repair, and create a restore point from a freshly-written Repair & Health page (same 3 actions as today, no code carried over)

### Quick Shortcuts

- [ ] **SHORT-01**: User can launch the same 10 quick shortcuts (Task Manager, Startup Apps, MSConfig, Device Manager, Event Viewer, Windows Update, Disk Management, Services, Resource Monitor, Registry Editor) from a freshly-written page

### Deep-Clean Uninstaller

- [ ] **UNINST-01**: User can trigger a manual, on-demand leftover scan for a chosen previously-uninstalled app
- [ ] **UNINST-02**: Scan finds leftover registry keys, folders, and scheduled tasks via path/publisher/name attribution
- [ ] **UNINST-03**: Results show grouped by removal mechanism (files/folders, registry keys, scheduled tasks), nothing pre-checked
- [ ] **UNINST-04**: User explicitly selects items and confirms before anything is deleted

### System Cleaner — Junk/Temp

- [ ] **CLEAN-01**: Scan finds temp files, Windows Update leftovers, icon cache, and similar junk categories (replaces the current one-shot "Clear Temp Files"/"Disk Cleanup" buttons)
- [ ] **CLEAN-02**: Results show sizes per category/item in a review list, nothing pre-checked
- [ ] **CLEAN-03**: Deletions default to Recycle Bin (soft-delete), not permanent
- [ ] **CLEAN-04**: Post-action summary shows items removed and space reclaimed

### Large-File Finder

- [ ] **LGFILE-01**: User can scan a chosen drive/folder for the largest files, sorted by size (read-only discovery, no delete in v1)

### Duplicate-File Finder

- [ ] **DUPE-01**: Scan finds byte-identical files via content-hash matching, grouped by duplicate set
- [ ] **DUPE-02**: Each group protects one reference file from selection by default; nothing else pre-checked
- [ ] **DUPE-03**: Deletions default to Recycle Bin

### Granular NIC Tweak UI

- [ ] **NIC-01**: User selects a target network adapter before applying any tweak (per-adapter, never global)
- [ ] **NIC-02**: Each of the ~18 existing `network-apply.bat` values is its own toggle, sourced from the adapter's actual driver-reported valid values — not a static hardcoded table
- [ ] **NIC-03**: Current value is shown before any change (matches the Power tab's current+recommended column convention)
- [ ] **NIC-04**: Each value is snapshotted before write, enabling real per-value revert
- [ ] **NIC-05**: `network-apply.bat`/`network-revert.bat` are retired once the new UI covers their functionality

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Deep-Clean Uninstaller

- **UNINST-05**: Post-uninstall opt-in leftover-scan prompt wired into Software tab's `UninstallCommand` ("Scan for leftovers from X?")
- **UNINST-06**: Attribution confidence tiers surfaced on leftover-scan review rows (high/possible match)

### Duplicate-File Finder

- **DUPE-04**: Fuzzy filename duplicate matching (opt-in, risk-labeled, alongside content-hash mode)

### Large-File Finder

- **LGFILE-02**: Delete capability through the same review/confirm/Recycle-Bin flow as the other cleaners

### Granular NIC Tweak UI

- **NIC-06**: "Suggested selection" presets that pre-highlight (never pre-check) recommended values

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Registry "cleaning" (generic sweep of unrelated "invalid" entries) | Discredited pattern with real breakage risk; conflicts with Core Value's reversibility/legibility bar |
| One-click "Clean Everything"/"Optimize Now" button | Violates the no-select-all-and-go default constraint |
| Auto-apply NIC presets bypassing per-value review | Same constraint violation; also unsafe since driver-valid value ranges differ per adapter |
| Permanent delete as the default for file deletions | Removes the last safety net beyond the review step |
| Silent automatic leftover-scan after every uninstall | No unprompted background scans — user must opt in each time |
| Rebuilding the App Uninstaller's bulk-uninstall itself | Already exists in Software ▸ External Apps / Windows Apps (`UninstallCommand`) |
| System Information card in the new hub | Redundant — Home tab already shows this via `SystemInfoService.Gather()` |
| Per-app/game QoS traffic prioritization | Considered during scoping, deferred — this milestone is NIC-value granularity only |
| Latency/route diagnostics (ping/traceroute UI) | Considered during scoping, deferred |
| TCP stack tuning UI (netsh int tcp exposure) | Considered during scoping, deferred |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| HUB-01 | TBD | Pending |
| HUB-02 | TBD | Pending |
| HUB-03 | TBD | Pending |
| REPAIR-01 | TBD | Pending |
| SHORT-01 | TBD | Pending |
| UNINST-01 | TBD | Pending |
| UNINST-02 | TBD | Pending |
| UNINST-03 | TBD | Pending |
| UNINST-04 | TBD | Pending |
| CLEAN-01 | TBD | Pending |
| CLEAN-02 | TBD | Pending |
| CLEAN-03 | TBD | Pending |
| CLEAN-04 | TBD | Pending |
| LGFILE-01 | TBD | Pending |
| DUPE-01 | TBD | Pending |
| DUPE-02 | TBD | Pending |
| DUPE-03 | TBD | Pending |
| NIC-01 | TBD | Pending |
| NIC-02 | TBD | Pending |
| NIC-03 | TBD | Pending |
| NIC-04 | TBD | Pending |
| NIC-05 | TBD | Pending |

**Coverage:**
- v1 requirements: 22 total
- Mapped to phases: 0
- Unmapped: 22 ⚠️ (roadmap creation fills this in next)

---
*Requirements defined: 2026-08-27*
*Last updated: 2026-08-27 after initial definition*
