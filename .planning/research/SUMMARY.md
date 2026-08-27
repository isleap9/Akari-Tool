# Project Research Summary

Project: Akari Tool -- System Tools rework (uninstaller leftover cleaner, System Cleaner, granular NIC tuning)
Domain: Windows desktop system-utility features (uninstaller cleanup, disk-space/duplicate-file scanning, per-adapter NIC registry tuning) inside an existing WinUI 3 / .NET 10 app
Researched: 2026-08-27
Confidence: HIGH

## Executive Summary

This milestone replaces ToolsPage one-shot, all-or-nothing buttons (temp-file cleanup, disk cleanup, the 184-line network-apply.bat / 5-line network-revert.bat pair) with three safety-first, scan-then-review tools: an uninstaller leftover scanner, a System Cleaner (junk/temp files, large-file finder, duplicate-file finder), and a granular per-adapter/per-value NIC tweak UI with real per-value revert. Every established competitor in this space (Revo Uninstaller, Bulk Crap Uninstaller, BleachBit, WizTree, dupeGuru) converges on the same interaction shape: scan (read-only), then review list with nothing pre-checked, then explicit selection, confirm, act. Akari own Core Value and Constraints already mandate exactly this shape, so the research strongly validates the milestone stated approach.

No new heavy frameworks are needed. All three tools build entirely on BCL APIs (Directory.EnumerateFiles, System.IO.Hashing for fast non-cryptographic content hashing) and Windows-native surfaces already reachable through the app existing Infrastructure seams (IWindowsRegistryService, IPowerShellRunner driving the NetAdapterAdvancedProperty cmdlets, the existing Schedule.Service COM pattern). Architecturally, all three tools decompose the same way the app declarative tweak stack already does: Core defines pure interfaces/models, Infrastructure does all OS-touching scan/apply work, App hosts bespoke (non-SettingDefinition) ViewModels modeled on VerifyViewModel grouped-result-list shape but using async Task.Run plus IProgress plus CancellationToken scanning (not Verify synchronous-on-UI-thread pattern, which is only safe for Verify millisecond-scale registry-only work).

The dominant risk across all three tools is false-positive destructive action masquerading as just cleanup: leftover scanning can flag shared redistributables/runtime components still in use by other installed apps; duplicate-file hashing can correctly identify byte-identical files that are semantically independent per-app install-tree assets; and NIC tweaks written from a fixed app-side catalog can silently no-op or destabilize adapters because NIC advanced properties are driver-defined, not OS-defined, and vary per vendor/model. Mitigation is consistent across all three: cross-reference against live (not stale) installed-program state, require multiple corroborating signals before flagging, default every scan result to unchecked, route deletions through Recycle Bin, enumerate the NIC driver actual declared parameter set at runtime rather than assuming a fixed catalog, and capture real pre-write state per NIC value (including an explicit did-not-exist sentinel) so revert is genuinely accurate, not a hardcoded default masquerading as a real revert. A pre-existing, documented codebase gap (SecurityException vs UnauthorizedAccessException on ACL-locked registry keys, from CONCERNS.md) sits directly in the path of all three new tools and should be fixed once, early, rather than three times.

## Key Findings

### Recommended Stack

Everything needed is either already a dependency of this repo, a .NET 10 BCL API, or a built-in Windows component reachable through the app existing IWindowsRegistryService / IPowerShellRunner / IProcessExecutor / IScheduledTaskService seams. The only new package needed across all three features is System.IO.Hashing (Microsoft-owned, fits the repo existing System.* versioning convention).

Core technologies:
- Microsoft.Win32.Registry via IWindowsRegistryService -- enumerate ...\Uninstall keys for leftover detection; already the app single registry seam with ACL-exception handling in place
- Directory.EnumerateFiles / System.IO.Enumeration (BCL) -- streaming, lazy NTFS directory walk for junk/large-file/duplicate scans; avoids Directory.GetFiles materialize-whole-tree-then-abort-on-first-access-denied failure mode
- System.IO.Hashing (NuGet, 10.0.x) -- XxHash3/XxHash64 non-cryptographic content hashing for the duplicate-file finder, materially faster than SHA256/MD5 for bulk comparison
- PowerShell NetAdapter module (Get-/Set-/Reset-NetAdapterAdvancedProperty) via existing IPowerShellRunner -- the officially supported, validated surface for per-adapter, per-value NIC tuning with real per-value revert (Reset-NetAdapterAdvancedProperty restores the driver factory default)
- Task Scheduler COM (Schedule.Service, late-bound dynamic) -- extend the existing ScheduledTaskService.cs pattern with recursive folder/task enumeration for leftover scheduled-task detection

Explicitly avoid: writing directly into HKLM\SYSTEM\...\Control\Class\{...}\NNNN (today network-apply.bat approach -- the NNNN ordinal is unstable across driver reinstalls/reboots); reusing RegistrySetting.ApplyPerNetworkInterface for NIC tuning (targets a different registry namespace, TCP/IP stack not NDIS driver properties); MSI-specific P/Invoke detection (disproportionate for MVP); blind filesystem substring search for leftovers (high false-positive risk); Directory.GetFiles with AllDirectories (aborts on first ACL error); cryptographic hashing as a first-pass duplicate filter (needlessly slow -- size-bucket first, then XxHash3).

### Expected Features

Must have (table stakes):
- Scan then review list (sizes/counts) then explicit selection then confirm then act, matching every comparable competitor tool
- Nothing pre-checked / no select-all default -- the number one way naive cleaners cause data loss
- Per-item detail on hover/expand (path, size, last-modified, why flagged)
- Post-action summary (what was deleted/freed)
- Recycle Bin / soft-delete for file deletions (not registry/task removal, which has no equivalent)
- Sortable/filterable results for large result sets
- Per-adapter selection before applying NIC tweaks; current-value display before change

Should have (differentiators):
- Real per-value NIC revert to previously-read value (not a hardcoded default) -- the single highest-value differentiator identified
- Attribution confidence tiers on leftover scan results (high/possible match, with matched signal shown)
- Automatic restore-point offer before first destructive batch action per session (reuse existing SystemBackupService)
- Contextual (opt-in, not automatic) leftover-scan follow-up prompt after Software-tab uninstall
- NIC values sourced live from the driver advertised property range, not a static catalog
- Duplicate-finder always-keep-this-one pinning plus suggested-keep heuristic (never auto-selected)
- Scheduled-task/service leftovers surfaced distinctly from file/registry leftovers, own confirm

Defer (v2+):
- Fuzzy filename duplicate matching (opt-in, riskier -- content-hash only for v1)
- Generic/whole-registry cleaning -- explicitly rejected as an anti-feature, conflicts with Core Value
- One-click Clean-Everything/Optimize-Now -- explicitly rejected, exactly what is being replaced
- Auto-apply NIC presets without per-value review -- explicitly rejected
- Permanent delete as default (bypassing Recycle Bin) -- explicitly rejected
- Export/import of scan results, cross-tool space-reclaimed running total -- low priority polish

### Architecture Approach

Build within the existing Core / Infrastructure / App three-layer split -- no fourth layer, no separate scanning-subsystem. Each tool gets a Core interface (ILeftoverScanner, IJunkFileScanner, IDuplicateFileScanner, ILargeFileScanner, INicTweakService) with Task<T> ScanAsync(IProgress<ScanProgress>, CancellationToken) and a separate ApplyAsync(selectedEntries, ct), implemented in Infrastructure (Features/SystemTools/Services/), consumed only via interface from bespoke App ViewModels (not SettingPageViewModel -- model instead on VerifyViewModel grouped-ObservableCollection row-VM shape, but with async scanning per Pattern 1, not Verify synchronous UI-thread scan).

Major components:
1. SystemToolsHubPage (mirrors AdvancedHubPage/HubView) -- 3-card entry point routing to each tool
2. Tool-specific ViewModel (App) -- owns scan lifecycle (Task.Run plus IProgress plus CancellationTokenSource), ObservableCollection of row VMs with per-row IsSelected (nothing pre-checked), Apply command routed through TweakDialogs.ConfirmContentAsync
3. Tool-specific Scanner (Infrastructure) -- all OS-touching work: registry/filesystem/adapter enumeration, hashing, deletes/writes; strict two-phase split (scan never mutates, apply is separate and only reachable after review)
4. Core models (LeftoverEntry, DuplicateFileGroup, NicAdapterInfo, NicTweakValue, ScanProgress) -- pure, immutable records

Build order: hub scaffold first, then shared scan/review scaffolding (via the smallest scanner, leftover-cleanup), then System Cleaner sub-scans ordered by complexity (junk, then large-file, then duplicate), then NIC tweak UI (independent, can parallel, but benefits from an existing grouped-row pattern), then delete ToolsPage last, only once the new hub covers all surviving functionality.

### Critical Pitfalls

1. Leftover scanner deletes registry keys/folders still owned by shared components (VC++ redistributables, .NET runtime, Common Files, MSIX framework packages) -- cross-check candidates against a live re-read of installed-programs state at scan time, require multiple corroborating signals, never a bare name-substring match.
2. NIC tweak UI assumes every adapter/driver exposes every catalog value and writes unconditionally -- NIC advanced properties are driver-defined, not OS-defined; enumerate each adapter actual declared parameter set (Get-NetAdapterAdvancedProperty) at runtime and only expose/validate against what the driver reports.
3. NIC per-value revert built on a hardcoded known-default table instead of captured original state -- capture and persist the live pre-write value (or an explicit did-not-exist sentinel) on first read, before ever offering the toggle; revert must delete when the sentinel says absent, never write a substitute default.
4. Registry ACL failures (SecurityException, not UnauthorizedAccessException) swallowed as generic errors -- a documented pre-existing gap (CONCERNS.md) that sits directly in the write path of all three new tools (protected Services keys, tightly-ACLed NIC driver Class keys); fix once, early, before the leftover-scan phase first delete ships.
5. Duplicate-file finder deletes byte-identical files that are semantically independent (shared install-tree assets across two different apps) -- always full byte-compare after hash pre-filter, exclude or specially warn on pairs inside two different Program Files vendor trees or game-library trees, default scope to user data locations not Program Files.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: System Tools Hub Scaffold
Rationale: Small, low-risk, mirrors the existing AdvancedHubPage/HubView pattern exactly; every subsequent tool phase needs somewhere to navigate from and can be demoed independently inside a working shell rather than as an orphaned page.
Delivers: SystemToolsHubPage with 3 placeholder cards, wired into MainWindow.xaml.cs PageMap.
Addresses: the hub-page table-stakes requirement from FEATURES.md.
Avoids: N/A (no destructive logic in this phase).

### Phase 2: Shared Scan/Review Scaffolding plus Uninstaller Leftover Scanner
Rationale: Establishes the ScanProgress Core model, the IProgress plus CancellationTokenSource async-scan pattern, and the reusable grouped-result-list-with-checkbox row-VM shape that System Cleaner and NIC tweaks will both reuse. Leftover-cleanup is the smallest scan surface (known registry hives plus bounded folder/task check, no hashing), so it is the cheapest place to pay down this shared-infrastructure risk.
Delivers: ILeftoverScanner/LeftoverScanner, leftover review UI, delete-with-confirm through TweakDialogs, attribution matching (path/publisher/name, multi-signal), live cross-reference against currently-installed programs, scheduled-task target-path cross-check.
Addresses: Deep-clean leftover scanner (P1 in FEATURES.md); fixes the SecurityException/ACL gap here first since leftover-scan is the first of the three tools to hit protected keys.
Avoids: Pitfall 1 (shared-component false positives), Pitfall 8 (swallowed ACL exceptions), Pitfall 9 (scheduled-task false positives).

### Phase 3: System Cleaner -- Junk/Temp File Cleanup
Rationale: Lower complexity than large-file/duplicate scans (simple enumeration, mirrors the existing Clear-Temp-Files scope, just adding the review step); natural next step once shared scaffolding exists.
Delivers: IJunkFileScanner, categorized junk results (temp files, cache, icon cache), lock-check before flagging, Recycle Bin delete.
Uses: Directory.EnumerateFiles/FileSystemEnumerable streaming enumeration from STACK.md.
Avoids: Pitfall 3 (locked/in-use file force-delete).

### Phase 4: System Cleaner -- Large-File Finder
Rationale: Read-only discovery, no hashing, lower risk than duplicate-file finder; can ship without a delete action if scoped down.
Delivers: ILargeFileScanner, sortable size-list UI with last-modified/type context, risky-category flagging (VHDX, DB files, save-game dirs), no bulk delete-all-shown.
Implements: EnumerationOptions.IgnoreInaccessible pattern from ARCHITECTURE.md.
Avoids: Pitfall 4 (size mistaken for importance).

### Phase 5: System Cleaner -- Duplicate-File Finder
Rationale: Highest complexity of the three System Cleaner sub-scans (size/hash funnel, real performance-tuning risk on large drives); sequenced last so it benefits from the scan/review pattern being fully proven.
Delivers: IDuplicateFileScanner, size-bucket then prefix-hash then full-hash then byte-compare pipeline (System.IO.Hashing), grouped review UI with one protected reference file per group, install-tree exclusion/warning, Recycle Bin delete.
Uses: System.IO.Hashing (XxHash3/XxHash64) from STACK.md.
Avoids: Pitfall 2 (semantic false positives on shared install-tree assets).

### Phase 6: Granular NIC Tweak UI
Rationale: No hard dependency on the other tools scan/review scaffolding (different Infrastructure service, different Core models), but benefits from landing after at least one scan-review tool exists to crib the grouped-row pattern from; the per-value revert-log model is genuinely new with no existing precedent in the app.
Delivers: INicTweakService, per-adapter selection UI, live driver-declared parameter enumeration (not a fixed catalog), per-value current-value display, snapshot-before-write with did-not-exist sentinel support, real per-value revert, adapter-restart handling with active-session guard, replaces network-apply.bat/network-revert.bat.
Uses: PowerShell NetAdapter module cmdlets from STACK.md.
Avoids: Pitfall 5 (unconditional writes to undeclared values), Pitfall 6 (revert on hardcoded defaults), Pitfall 7 (restart timing/wrong-adapter restart).

### Phase 7: Cleanup -- Remove Legacy ToolsPage
Rationale: Must happen last, only once the new hub and all three tools cover the surviving ToolsPage functionality (Repair and Health, Quick Shortcuts fold into the hub too), so users are never left without a working entry point mid-milestone.
Delivers: Deletion of ToolsPage.xaml(.cs), nav-tag repoint to the new hub.

### Phase Ordering Rationale

- Hub-first establishes navigation before any tool has a destination, matching the low-risk-first principle in ARCHITECTURE.md Build Order Implications.
- Leftover-cleanup is deliberately sequenced before the disk-scanning tools specifically because it is the cheapest tool to establish the shared async-scan/review scaffolding on, and because it is also the first tool to hit the pre-existing ACL/SecurityException gap -- fixing that gap early means all subsequent phases inherit the fix rather than rediscovering it.
- System Cleaner three sub-scans are ordered by complexity (junk, large-file, duplicate) per ARCHITECTURE.md, so risk is paid down incrementally rather than tackling the hardest scan (duplicate, with its hash-funnel performance requirements) first.
- NIC tweak UI is architecturally independent and could theoretically run in parallel, but is placed after the scan-review pattern is proven because its revert-log model has no existing precedent to crib from, unlike the other two tools which reuse Verify grouped-row shape.

### Research Flags

Phases likely needing deeper research during planning:
- Phase 5 (Duplicate-File Finder): performance-tuning risk on large drives (size/hash funnel correctness, throttled progress reporting) warrants a research pass before planning if the roadmap targets multi-TB volumes.
- Phase 6 (NIC Tweak UI): driver-declared parameter enumeration and adapter-restart/active-session-guard behavior varies per NIC vendor -- PITFALLS.md notes no per-vendor driver spec was independently verified (see Gaps below); plan-phase research should confirm behavior against at least two NIC vendor/driver combinations before finalizing the UI contract.

Phases with standard patterns (skip research-phase):
- Phase 1 (Hub Scaffold): directly copies the existing AdvancedHubPage/HubView pattern -- no new research needed.
- Phase 2 (Leftover Scanner) and Phase 3 (Junk/Temp Cleanup): well-documented BCL/registry patterns already proven elsewhere in this codebase (WindowsRegistryService, Directory.EnumerateFiles, ScheduledTaskService COM pattern).

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Grounded in current Microsoft Learn docs (NetAdapter cmdlets, MSFT_NetAdapter CIM classes) plus direct reads of this repo own source (network-apply.bat, WindowsRegistryService.cs, ScheduledTaskService.cs) -- not just training memory |
| Features | MEDIUM | Cross-checked against established tools (Revo, BCUninstaller, BleachBit, WizTree, dupeGuru) via web sources, no official vendor docs behind auth walls; internal PROJECT.md constraints treated as ground truth |
| Architecture | HIGH | Grounded in direct reads of this codebase own .planning/codebase/ARCHITECTURE.md/STRUCTURE.md, TaskProgressService.cs, VerifyViewModel.cs, AdvancedToolsPage.xaml.cs, plus standard documented .NET BCL async/threading semantics |
| Pitfalls | MEDIUM | Web-corroborated domain patterns (registry cleaner risk, duplicate-finder cautions, Disk Cleanup Downloads-folder incident) plus direct codebase reads (PROJECT.md, CONCERNS.md); no vendor-specific NIC driver spec was fetched per-vendor beyond one NVIDIA WinOF-2 doc |

Overall confidence: HIGH

### Gaps to Address

- NIC driver parameter variability across vendors: research verified the general pattern (driver-declared ndi params, Get-NetAdapterAdvancedProperty valid-range surface) against Microsoft Learn and one NVIDIA doc, but did not independently verify behavior across a broad set of NIC vendors/driver versions. During planning for Phase 6, validate the enumeration/apply/revert flow against at least two different physical NIC vendors before finalizing the UI contract.
- Existing ACL/SecurityException gap fix scope: CONCERNS.md documents this as a known gap but the research did not fully scope the fix size. During Phase 2 planning, confirm whether this is a small wrapper at new call sites or a broader WindowsRegistryService change, and size accordingly.
- NIC per-value state and SettingBackupService/drift integration: PITFALLS.md flags this as an explicit open decision (whether NIC per-value state should route through the existing Backup/Restore plus global-search infrastructure like every other tab, or intentionally stay out of scope). This should be explicitly decided and documented during Phase 6 planning, not left implicit.
- Large-file finder v1 delete scope: FEATURES.md notes delete may be deferred to read-only discovery for v1 if time-constrained; this should be explicitly decided during Phase 4 planning based on remaining milestone budget.

## Sources

### Primary (HIGH confidence)
- Set-NetAdapterAdvancedProperty -- Microsoft Learn
- Reset-NetAdapterAdvancedProperty -- Microsoft Learn
- MSFT_NetAdapterAdvancedPropertySettingData -- Microsoft Learn
- MSFT_NetAdapter class -- Microsoft Learn
- System.IO.Hashing NuGet package page
- This repo own source (direct reads): network-apply.bat/network-revert.bat, WindowsRegistryService.cs, RegistrySetting.cs, ScheduledTaskService.cs, AppModels.cs, .planning/codebase/ARCHITECTURE.md, .planning/codebase/STRUCTURE.md, .planning/codebase/CONCERNS.md, .planning/PROJECT.md, TaskProgressService.cs, VerifyViewModel.cs, AdvancedToolsPage.xaml.cs, TweakDialogs.cs, IFileSystemService.cs, IWindowsRegistryService.cs, IProcessExecutor.cs

### Secondary (MEDIUM confidence)
- Bulk Crap Uninstaller (Klocman/Bulk-Crap-Uninstaller GitHub) -- JunkScanner leftover-detection architecture precedent
- BleachBit official guide, WizTree official site, dupeGuru official site plus Results docs -- table-stakes UX patterns for scan/review/act tools
- Network Adapter Performance Tuning in Windows Server -- Microsoft Learn
- Configuring the Driver Registry Keys -- NVIDIA WinOF-2 Docs
- rmlint -- Cautions -- dupefinder correctness pitfalls
- Are registry cleaners safe to use -- Microsoft Q&A
- Checkboxes: Design Guidelines -- NN/g

### Tertiary (LOW confidence)
- Revo Uninstaller/BCUninstaller blog and community coverage (gridinsoft, davescomputertips) -- directionally consistent, not independently verified
- CCleaner community bug-report threads, Microsoft Community Hub uninstall-leftover thread -- user reports, not vendor-verified
- Disk Mop blog, How to Remove Hidden/Ghost Network Adapters (Windows OS Hub) -- vendor/community content, needs validation if relied on directly

---
Research completed: 2026-08-27
Ready for roadmap: yes
