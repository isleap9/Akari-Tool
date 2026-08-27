# Feature Research

**Domain:** Windows desktop system-utility tools — post-uninstall leftover cleaner, junk/disk-space
reclamation suite (junk cleaner + large-file finder + duplicate finder), and per-adapter NIC
registry tuning UI
**Researched:** 2026-08-27
**Confidence:** MEDIUM (cross-checked web sources on established tools — Revo Uninstaller,
BCUninstaller, CCleaner/BleachBit, WizTree/TreeSize, dupeGuru, Windows NDIS advanced properties/
TCP Optimizer — no official vendor docs behind auth walls; no Context7/library-doc source applies
since this is desktop-tool UX research, not an API)

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist in any tool in this category. Missing these = product feels
incomplete or, worse, unsafe compared to the free tools already on the market.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Scan → review list (with sizes/counts) → explicit selection → confirm → act | Every comparable tool (Revo, BCUninstaller, BleachBit, WizTree, dupeGuru) uses this shape; it's the baseline safety contract users have been trained on since CCleaner-era tools | MEDIUM | Already Akari's stated Constraint — this is not new, just needs consistent implementation across all three System Tools features |
| Nothing pre-checked / no "select all" default | dupeGuru marks the kept reference file by default and requires explicit marking of items to delete; BleachBit requires explicit category checks; auto-select-all is the #1 way naive cleaners cause data loss | LOW–MEDIUM | Directly required by Akari's Constraint ("no 'select all and go' default") — enforce at the ViewModel/selection-model level, not just UI copy |
| Per-item detail on hover/expand (path, size, last-modified, why it was flagged) | Users of Revo/BCUninstaller/WizTree expect to see *why* something is flagged before deleting it, not just a name | LOW | Maps to existing `TweakDialogs`/technical-details pattern already used elsewhere in the app |
| Dry-run/preview semantics baked into the scan itself | BleachBit's Preview step is explicitly the trust mechanism that turns "blind delete" into "reviewed delete" — the scan phase in Akari's design already *is* this, just needs framing as non-destructive | LOW | Confirms Akari's existing constraint ("scans are read-only discovery") matches user expectations set by the category |
| Post-action summary (what was deleted/freed, item count, space reclaimed) | Every comparable tool reports outcome; users need confirmation the action did what was promised | LOW | Reuse existing status-banner/technical-details pattern (4d) |
| Recycle Bin / soft-delete option for file deletions where feasible | dupeGuru defaults deletions to Recycle Bin, not permanent delete, specifically as a safety net beyond the review step | LOW–MEDIUM | For junk-temp-file and duplicate-file deletion (not registry/scheduled-task removal, which has no Recycle Bin equivalent) — use `Shell.ApplicationServices` / `IFileOperation` with `FOF_ALLOWUNDO`, or Infrastructure's `IFileSystemService` if it already supports it |
| Sortable, filterable results (by size, path, category, confidence) | WizTree/TreeSize's core value is fast sortable size views; BleachBit's category tree; large-result-set usability is table stakes once counts run into hundreds/thousands of items | MEDIUM | Especially needed for junk-file scan (can return thousands of rows) and duplicate finder |
| Per-adapter selection before applying NIC tweaks | Windows itself exposes tuning per-adapter (Device Manager > Advanced tab is always scoped to one NIC); a global "apply to all adapters" model (the current `network-apply.bat` approach) is already what Akari is explicitly moving away from | LOW–MEDIUM | Directly maps to Active requirement — enumerate adapters via `Get-NetAdapter`-equivalent, let user pick target(s) |
| Current-value display before change (not just target/recommended value) | Power tab's existing "current + recommended" column pattern (4h) already sets this precedent in-app | LOW | Reuse the Power tab's current-value column convention for NIC rows — internal consistency, not just external table stakes |

### Differentiators (Competitive Advantage)

Features that set Akari's System Tools apart from the free tools it's competing with
conceptually. These should lean into Akari's existing safety-first/reversible-by-design brand
established elsewhere in the app (SettingBackupService, restore-point integration, per-row
Apply/Restore-Default commands).

| Feature | Value Proposition | Complexity | Notes |
|---------|--------------------|------------|-------|
| Real per-value revert for NIC tweaks (not a blanket undo script) | This is the explicit gap Akari is closing vs. its own current `network-revert.bat` (5 lines, blanket) and vs. most NIC tuning guides/tools (TCP Optimizer et al. apply presets, revert is coarse or manual). Per-value revert to *previously read* value (not a hardcoded "Windows default") is stronger than any comparable free tool | MEDIUM–HIGH | Read-before-write pattern: snapshot current `Get-NetAdapterAdvancedProperty` value per (adapter, keyword) before writing, store it (SettingBackupService-style), expose a per-row "Revert" action. This is the single highest-value differentiator identified in this research |
| Attribution confidence on leftover scan results ("high confidence — matches uninstalled app's publisher/install path" vs "possible match") | Revo's bold/red highlighting shows *some* attribution signal but doesn't expose confidence tiers; Akari can be more transparent about *why* something is flagged, reducing accidental deletion of unrelated data | MEDIUM | Match on: install path prefix, publisher name in registry key, app name substring, orphaned uninstall-string target no longer on disk. Surface the matched signal(s) in the review row, not just a checkbox |
| Automatic restore-point offer before first destructive batch action in a session | Akari already has `SystemBackupService`/first-launch restore-point consent (4g) — extending that trigger to "before first System Tools deletion batch this session" is a natural, low-cost differentiator vs. competitors that only restore-point-gate registry edits (if at all) | LOW–MEDIUM | Reuse existing `ISystemRestoreService`/`ISystemBackupService`; gate on a per-session flag, not per-action (avoid nagging) |
| Unified leftover scan triggered contextually after Software tab uninstall (opt-in prompt), in addition to manual on-demand scan | No competitor reviewed does this well — Revo/BCUninstaller are separate manual runs; tying leftover-scan into the existing `UninstallCommand` flow as a one-click follow-up (not automatic, not silent) is a genuine UX improvement Akari can own given it already has both halves (uninstall + would-be scanner) in one app | MEDIUM | See Feature Dependencies below — this is additive to the Active requirement's "manual scan," not a replacement |
| NIC values sourced from the driver's actual advertised property range, not a static Akari-authored list | Windows' `Get-NetAdapterAdvancedProperty` returns each adapter's *actual* valid values/ranges (driver-defined, varies by NIC vendor/model) — building the UI to read this live instead of hardcoding a fixed registry-value table (like the current 184-line `.bat`) avoids applying values a given NIC doesn't support, which is a known failure mode of static "one-size-fits-all" NIC tweak guides | MEDIUM–HIGH | This is an architecture-level differentiator, not just UX — prevents the exact class of bug the current `.bat` script risks (writing values a driver silently ignores or misapplies) |
| Duplicate-finder "always keep this one" pinning + newest/oldest/path-preference default suggestion, never auto-selected | dupeGuru's "reference file can't be deleted" pattern, extended with an explicit suggested-keep heuristic (newest modified, or preferred folder) shown as a *suggestion* the user must still confirm | LOW–MEDIUM | Suggestion only — never pre-checks the deletion checkbox; consistent with Constraint |
| Scheduled-task and service leftovers surfaced distinctly from file/registry leftovers, each with its own explicit confirm | Competitors bucket everything into one leftover list; separating "this will stop deleting a scheduled task" from "this deletes a folder" gives users better risk calibration before confirming — services/tasks can affect system behavior in ways a folder deletion can't | LOW | Matches Akari's existing `ScheduledTaskSetting`/`IScheduledTaskService` infra already built for the declarative stack — reuse, don't reinvent |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good, are common in the category, or might be requested later, but conflict
with Akari's stated Core Value ("no silent all-or-nothing scripts... no changes the user can't
see or undo") and Constraints. Document these now so they're deliberately rejected, not
accidentally reintroduced under time pressure.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| "Registry cleaning" (scanning the *entire* registry for generic "invalid"/"orphaned" entries unrelated to a specific uninstalled app) | Category-standard feature in CCleaner-class tools; users may ask "why not just clean my whole registry" | Broadly discredited by Microsoft MVPs/Windows experts as providing no measurable benefit on NT-based Windows while carrying real risk of removing a key still in use, causing app breakage or boot issues; this is exactly the "no orphaned leftovers... no changes the user can't see" line but inverted — it's changes the user *can't verify are safe* | Scope leftover scanning strictly to keys/values/paths *provably tied to a specific, named, just-uninstalled application* (path/publisher/name match), never a general registry sweep |
| One-click "Clean Everything" / "Optimize Now" button that runs scan+select-all+delete in one action | This is literally what the current `ToolsPage`'s "Clear Temp Files"/"Disk Cleanup" buttons do today, and CCleaner's default flow encourages it | Directly violates the Active requirement replacing exactly this pattern ("replaces the current one-shot... buttons") and the Constraint against "select all and go" defaults | Scan always stops at the review screen; the closest to "one click" allowed is a "select all *safe* category checkboxes" pre-filter the user still must confirm via a visible list, never a silent full run |
| Auto-apply NIC tweak presets ("Gaming Mode" button that silently writes all ~18 values without per-value review) | This is what `network-apply.bat` does today, and what most "network booster" tools in the wild do (single "optimize" button) | Precisely the pattern the Active requirement is replacing; also unsafe because driver-valid value ranges differ per NIC — a blind preset can write a value a given adapter doesn't support | Presets may exist as a *suggested selection* that pre-highlights recommended rows (like the existing SettingItem "Apply Recommended" pattern elsewhere in-app), but each value still shows current→target and requires the existing per-row/per-page confirm flow, never bypassing review |
| Deep/aggressive duplicate matching by filename+size heuristic only (no content hash) as the default mode | Faster, so tempting as the default for a "quick scan" | Filename/size-only matching produces false positives (e.g., two unrelated files that happen to share size and a generic name) — dupeGuru's own docs distinguish this from content/MD5 matching precisely because of false-positive risk; defaulting to it risks deleting unrelated user data | Default to content-hash (MD5/SHA) exact matching; expose fuzzy filename matching as an explicit opt-in advanced mode with a visible warning, not the default |
| Permanent delete (bypassing Recycle Bin) as the default for junk/duplicate file cleanup | Frees more space "for real," some competitors offer this as the fast path | Removes the last safety net after the review step; a single bad selection becomes unrecoverable, directly conflicting with "no changes the user can't... undo" | Default all file deletions in Cleaner/duplicate-finder to Recycle Bin (soft delete); permanent delete only as an explicit secondary confirmation for advanced users, clearly labeled as unrecoverable |
| Auto-running the leftover scan silently in the background immediately after every Software-tab uninstall, without prompting | Feels "smart"/proactive, reduces clicks | Silent background scanning + surfacing results unprompted can feel invasive, and conflicts with "no silent... scripts" — the *scan* itself is safe (read-only) but launching it without the user's opt-in per action is still a behavior the user didn't ask for at that moment | Prompt-based follow-up: after `UninstallCommand` completes, offer (not auto-run) "Scan for leftovers from [App Name]?" — one click to proceed, one click to dismiss, per the Feature Dependencies note below |
| Deleting scheduled tasks/services outright as part of the same delete action as files/registry, with no distinct confirmation | Simpler single "Delete Selected" button covering all leftover types uniformly | Removing a scheduled task or service has different blast radius than deleting a leftover folder (can affect system behavior beyond disk space); conflating them in one undifferentiated action reduces the user's ability to make an informed choice on each mode of removal | Group leftovers by removal *mechanism* (files/folders, registry keys, scheduled tasks, services) in the review UI with per-group counts, even if a single "Delete Selected" ultimately executes all selections in one pass |
| Hardcoded per-vendor NIC registry value tables baked into Akari's catalog (Winhance-1:1 style static rows) | Matches Akari's existing catalog pattern (`SettingDefinition`/`RegistrySetting` static rows) used everywhere else in the app, so it's the "obvious" architectural default | NIC advanced properties are driver-defined — the same `RegistryKeyword` (e.g. `*InterruptModeration`) can have different valid value ranges or simply not exist on a given NIC/driver; a static catalog row list risks either failing silently or writing unsupported values, unlike the rest of Akari's OS-version-gated catalog rows which target stable, documented Windows registry contracts | Read available advanced properties (name + valid values) live per selected adapter at scan time (equivalent to `Get-NetAdapterAdvancedProperty`), build the row list dynamically from what the driver actually reports, rather than from a fixed catalog |

## Feature Dependencies

```
Software ▸ External/Windows Apps UninstallCommand (existing)
    └──enables (optional follow-up, not required)──> Deep-clean leftover scanner
                                                           ├──requires──> Read-only registry/file/task scan engine
                                                           ├──requires──> Attribution/confidence matching (path/publisher/name)
                                                           └──requires──> Review UI (scan → review → explicit select → confirm → delete)

Deep-clean leftover scanner ──shares──> Review UI pattern (scan/select/confirm/act)
System Cleaner (junk files) ──shares──> Review UI pattern
Duplicate-file finder ──shares──> Review UI pattern
Large-file finder ──enhances──> System Cleaner (can feed candidates into the same review/delete action, or stand alone as pure discovery)

Duplicate-file finder
    └──requires──> Content-hash matching engine (default mode)
    └──optionally extends with──> Fuzzy filename matching (opt-in advanced mode)

NIC tweak UI
    └──requires──> Per-adapter enumeration (list installed NICs)
    └──requires──> Live per-adapter advanted-property read (driver-reported valid values)
    └──requires──> Per-value snapshot-before-write (enables real per-value revert)
    └──replaces──> network-apply.bat / network-revert.bat (existing, all-or-nothing)

Restore-point offer (existing SystemBackupService/ISystemRestoreService)
    └──enhances──> Deep-clean leftover scanner (pre-delete safety net)
    └──enhances──> System Cleaner (pre-delete safety net)
    └──enhances──> NIC tweak UI (pre-apply safety net, secondary to per-value revert)

Registry "cleaning" (generic sweep) ──conflicts──> Core Value (reversibility/legibility) — rejected, see Anti-Features
Auto-apply presets / one-click "Optimize" ──conflicts──> Constraint (no select-all-and-go default) — rejected, see Anti-Features
```

### Dependency Notes

- **Deep-clean leftover scanner does *not* require automatic triggering after
  `UninstallCommand`.** PROJECT.md's Active requirements describe it as scanning "after
  uninstalling via the existing Software ▸ External/Windows Apps bulk-uninstall," which reads as
  a temporal/contextual relationship, not a hard technical dependency — the scanner must be able
  to run **standalone** too (a user uninstalled something last week, or via a different method,
  and wants to clean up leftovers later). Recommend: build the scan engine as callable both
  on-demand (manual entry point in the new hub) and as an opt-in follow-up prompt fired after
  `UninstallCommand` completes for that specific app. Do not make the follow-up automatic/silent
  (see Anti-Features).
- **System Cleaner's three sub-tools (junk cleaner, large-file finder, duplicate finder) share
  one review UI pattern but are logically independent scans.** They don't need to run together —
  WizTree-style large-file discovery and dupeGuru-style duplicate detection are different
  algorithms with different result shapes (a size-sorted flat list vs. grouped duplicate sets).
  Building one shared "review list with checkboxes + confirm + delete" component that all three
  (plus the leftover scanner and junk cleaner) can host is the efficient path — this is a UI
  component dependency, not a data dependency between the scans themselves.
  Large-file finder **enhances** rather than **requires** System Cleaner: it can feed found large
  files into the same delete-review flow, or ship as pure read-only discovery (like WizTree) with
  no delete action at all if scoped down for v1.
- **NIC tweak UI's per-value revert requires reading current values before any write** — this is
  the core technical unlock for the differentiator and cannot be retrofitted after the fact
  without re-scanning; the "read, snapshot, write, allow revert-to-snapshot" sequence must be
  designed in from the first NIC-tuning phase, not added later.
- **Registry "cleaning" (generic sweep) conflicts with Core Value** — flagged explicitly as an
  anti-feature dependency-breaker: even if a future request asks to "expand" the leftover
  scanner into a general registry cleaner, that expansion should be rejected on Core Value
  grounds, not treated as a natural extension of the uninstaller leftover feature.

## MVP Definition

### Launch With (v1)

Minimum viable product to satisfy PROJECT.md's Active requirements and Core Value.

- [ ] Deep-clean leftover scanner (manual, on-demand): registry key + folder + scheduled-task
      scan for a user-selected previously-uninstalled app name, path-based attribution, review UI
      with per-item selection (nothing pre-checked), explicit confirm, delete — why essential:
      directly required by Active requirements
- [ ] System Cleaner — junk/temp file scan-then-review (replaces the current one-shot buttons):
      categorized results (temp files, cache, icon cache — the categories `ToolsPage` already
      covers), sizes shown, nothing pre-checked, explicit confirm, delete to Recycle Bin — why
      essential: directly required, explicitly replaces existing unsafe pattern
- [ ] Large-file finder: read-only scan + sortable size list (WizTree-style), no delete required
      for v1 if time-constrained — but if delete is included, it must route through the same
      review/confirm/Recycle-Bin pattern — why essential: explicitly named in Active requirements
- [ ] Duplicate-file finder: content-hash (exact) matching only for v1, grouped review UI, one
      reference file per group protected from selection, delete to Recycle Bin — why essential:
      explicitly named in Active requirements; content-hash-only keeps v1 scope safe (defer fuzzy
      matching)
- [ ] Granular NIC tweak UI: per-adapter selection, per-value toggles for the existing ~18
      `network-apply.bat` values (interrupt moderation, RSS, offloads, buffer sizes, etc.),
      current-value display, snapshot-before-write, per-value revert — why essential: directly
      required, and per-value revert is the single most differentiating piece of this milestone
- [ ] System Tools hub page (card-based, mirrors `AdvancedHubPage`) routing to the above — why
      essential: explicit Active requirement, and the container all the above tools need

### Add After Validation (v1.x)

Features to add once the core scan-review-act pattern is proven across all three tools.

- [ ] Fuzzy filename duplicate matching (opt-in, clearly labeled riskier than content-hash) —
      trigger: users report content-hash mode missing renamed-but-identical-content duplicates,
      or explicitly request catching same-content-different-format near-duplicates
- [ ] Post-`UninstallCommand` opt-in prompt ("Scan for leftovers from X?") wiring the leftover
      scanner to the Software tab's uninstall flow — trigger: v1 manual-entry-point scanner is
      validated as safe and useful standalone first
- [ ] Attribution confidence tiers surfaced in the leftover-scan review UI (high/possible match) —
      trigger: once basic path/name/publisher matching is shipped and validated, layer confidence
      scoring on top rather than shipping it as part of the initial matching logic
- [ ] NIC tweak presets as a "suggested selection" pre-highlight (not pre-check) over the
      per-value rows — trigger: after per-value manual flow is validated, add convenience on top,
      never replacing it

### Future Consideration (v2+)

- [ ] Scheduled restore-point auto-offer keyed to "first destructive batch action this session"
      across all three tools — defer: existing first-launch restore-point offer (4g) already
      covers general risk; session-scoped re-offering adds complexity that should wait until
      real usage data shows it's needed
- [ ] Cross-tool "space reclaimed this session" running total on the hub page — defer: polish
      feature, not core to the safety/reversibility value proposition
- [ ] Export/import of leftover-scan or duplicate-scan results (for later review or team sharing)
      — defer: no comparable tool in this research treats this as expected, low priority

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|----------------------|----------|
| Deep-clean leftover scanner (manual scan + review UI) | HIGH | MEDIUM | P1 |
| System Cleaner junk/temp file scan-then-review | HIGH | MEDIUM | P1 |
| NIC tweak UI — per-adapter, per-value, with revert | HIGH | HIGH | P1 |
| Duplicate-file finder (content-hash mode) | MEDIUM | MEDIUM | P1 |
| Large-file finder (read-only discovery) | MEDIUM | LOW | P1 |
| System Tools hub page shell | HIGH | LOW | P1 |
| Recycle Bin (soft-delete) for file deletions | HIGH | LOW–MEDIUM | P1 |
| Attribution confidence tiers on leftover scan | MEDIUM | MEDIUM | P2 |
| Post-uninstall opt-in follow-up prompt | MEDIUM | LOW | P2 |
| Fuzzy filename duplicate matching (opt-in) | LOW–MEDIUM | MEDIUM | P2 |
| NIC "suggested selection" presets (pre-highlight only) | LOW | LOW | P3 |
| Session-scoped restore-point re-offer | LOW | MEDIUM | P3 |

**Priority key:**
- P1: Must have for launch (this milestone)
- P2: Should have, add when possible (v1.x)
- P3: Nice to have, future consideration (v2+)

## Competitor Feature Analysis

| Feature | Revo Uninstaller / BCUninstaller | CCleaner / BleachBit / WizTree / dupeGuru | Akari's Approach |
|---------|-----------------------------------|---------------------------------------------|-------------------|
| Leftover attribution signal | Revo: bold/red highlighting by heuristic match, no explicit confidence shown; BCUninstaller: technician-style raw detail, no simplification | N/A | Explicit confidence tiers on matched signal (path/publisher/name), shown per row — see Differentiators |
| Delete confirmation gate | Revo requires explicit "Delete" click distinct from wizard Next/Finish | BleachBit's Preview→Clean two-step is the same shape | Same shape, reused across all four tools via one shared review-list component |
| Registry scope | Both scope to leftover keys tied to the just-removed app, not a general sweep | CCleaner does offer a broader "Registry" cleaner tab (the most-criticized part of CCleaner) | Deliberately scope-limited to app-specific leftovers only; explicit anti-feature reject on general registry sweep |
| File deletion safety net | N/A (registry/folder focus, not general junk) | dupeGuru defaults to Recycle Bin; BleachBit's default is permanent delete (opt-in shred exists too) | Default to Recycle Bin for all file-category deletions across System Cleaner + duplicate finder |
| NIC/network tuning granularity | N/A (not in this category) | N/A — TCP Optimizer and community guides apply presets/whole-adapter changes, revert is coarse or manual | Per-adapter, per-value, live-driver-sourced valid ranges, snapshot-based per-value revert — no comparable tool researched does full per-value revert |
| Duplicate match default mode | N/A | dupeGuru offers content/filename/folder modes, user picks; no single hard default enforced beyond "content is most reliable" | Default to content-hash exact match; fuzzy filename explicitly opt-in with a risk label |

## Sources

- [Is Revo Uninstaller Safe? Leftovers and Registry Cleanup](https://blog.gridinsoft.com/revo-uninstaller-safe-leftovers/) — MEDIUM confidence (cross-checked against Revo's own online manual pages returned in the same search)
- [Revo Uninstaller Pro - Uninstaller (official manual)](https://www.revouninstaller.com/online-manual/uninstaller/) — MEDIUM confidence
- [Bulk Crap Uninstaller — official site](https://www.bcuninstaller.com/) — MEDIUM confidence
- [BCUninstaller GitHub README](https://github.com/benpope82/Bulk-Crap-Uninstaller/blob/master/README.md) — MEDIUM confidence
- [BleachBit Junk File Cleaner Review — Daves Computer Tips](https://davescomputertips.com/bleachbit-junk-file-cleaner-review/) — MEDIUM confidence
- [What's the Difference Between BleachBit and CCleaner? — BleachBit official guide](https://bleachbit.net/guide/whats-the-difference-between-bleachbit-and-ccleaner/) — MEDIUM confidence
- [WizTree — The Fastest Disk Space Analyzer (official)](https://diskanalyzer.com/) — MEDIUM confidence
- [WizTree vs TreeSize vs WinDirStat comparison](https://zenovix.app/blog/wiztree-vs-treesize-vs-windirstat/) — MEDIUM confidence
- [dupeGuru — official site](https://dupeguru.com/) — MEDIUM confidence
- [dupeGuru Results documentation](https://dupeguru.voltaicideas.net/help/en/results.html) — MEDIUM confidence
- [Get-NetAdapterAdvancedProperty — Microsoft Learn](https://learn.microsoft.com/en-us/powershell/module/netadapter/get-netadapteradvancedproperty) — MEDIUM confidence (Microsoft Learn is authoritative for the API surface; cross-checked against NDIS keyword docs)
- [Network Adapter Performance Tuning in Windows Server — Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/networking/technologies/network-subsystem/net-sub-performance-tuning-nics) — MEDIUM confidence
- [Windows hides powerful network tuning settings in Device Manager — MakeUseOf](https://www.makeuseof.com/windows-hides-network-tuning-settings-in-device-manager-no-one-touches/) — MEDIUM confidence
- [Are "registry cleaners" safe to use? — Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/2443008/are-registry-cleaners-safe-to-use) — MEDIUM confidence
- [Are Registry Cleaners good or bad? — TheWindowsClub](https://www.thewindowsclub.com/do-registry-cleaners-defragmenters-really-help-or-are-they-snake-oil) — MEDIUM confidence
- Safe cleanup tool design principles (dry-run, restore point, whitelist, undo log) — synthesized from multiple community/engineering sources returned in search (ITECS "Safe Windows Cleanup" guide, GitHub `Windows-Cleaner-and-Optimizer` project docs) — MEDIUM confidence
- `C:\Users\isleap\Documents\GitHub\Akari-Tool\.planning\PROJECT.md` — internal, ground truth for scope/constraints
- `src/AkariTool.App/Scripts/Network/network-apply.bat`, `network-revert.bat` — internal, verified present in codebase during this research

---
*Feature research for: Windows desktop system-utility rework (leftover cleaner, System Cleaner, NIC tuning)*
*Researched: 2026-08-27*
