# Pitfalls Research

**Domain:** Windows desktop system-utility rework — app-uninstaller leftover scanner, system cleaner (junk/large/duplicate files), per-adapter NIC registry tweak UI (Akari Tool, System Tools rework)
**Researched:** 2026-08-27
**Confidence:** MEDIUM (web-corroborated domain patterns + direct codebase read of PROJECT.md/CONCERNS.md; no vendor-specific NIC driver spec was fetched per-vendor, see Gaps)

## Critical Pitfalls

### Pitfall 1: Leftover scanner deletes registry keys/folders still owned by shared components

**What goes wrong:**
The scanner flags a registry key or folder as "orphaned" because the app it was originally installed by is gone, but the key/folder is actually still in active use by a *different* installed program — a shared redistributable (VC++ Redistributable, .NET runtime, DirectX, WebView2), a shared "Common Files" folder, an MSIX shared framework package, or a WinGet-installed shared dependency. Deleting it silently breaks the still-installed program the next time it runs.

**Why it happens:**
Leftover detection is inherently a heuristic: cross-referencing the current `Uninstall` registry list against folder/key names that *look* orphaned. There is no authoritative Windows API that says "nothing references this key/folder anymore" — reference-counted shared components (redistributables, runtime packages) are exactly the case where name-based matching gives a false positive, because the name on disk belongs to the uninstalled app but the *files* are still depended on by a sibling app.

**How to avoid:**
- Maintain an explicit deny-list/allow-pattern for known shared-component publishers and paths (Microsoft, redistributable GUIDs, `Common Files\`, WinGet shared package roots, MSIX `Program Files\WindowsApps` framework packages) and never surface these as candidates, full stop — not even in the review list.
- Cross-check candidate folders/keys against the *live* installed-programs registry (same source `Software ▸ External Apps`/`Windows Apps` already reads) at scan time, not against a stale snapshot taken before the user's most recent uninstall/install actions.
- Treat "leftover" as "correlated with the app name, timestamp near uninstall, and not matched by any current install" — require multiple corroborating signals, not name substring match alone.

**Warning signs:**
Scanner flags anything under `Common Files`, a GUID-only folder shared across installers, or any path that also appears as a subpath of a currently-installed program's install directory.

**Phase to address:**
Uninstaller leftover-scan phase — this is the core detection-accuracy risk for that phase; must be solved before any delete action ships, not patched after a bad report.

---

### Pitfall 2: Duplicate-file finder deletes files that are byte-identical but semantically independent

**What goes wrong:**
Two files hash identically and are byte-for-byte the same, but they are not "duplicates" in the sense the user means — e.g. two different games each ship the same default engine placeholder texture, default startup asset, or shared middleware DLL as part of their own self-contained install. Deleting the "duplicate" removes a file the other game's installer expects to find in *its own* directory tree, breaking that title even though nothing was technically lost data-wise.

**Why it happens:**
Content-hash duplicate detection (correctly, per research) treats identical bytes as identical files — that part of the algorithm is sound and false hash-collision risk is negligible at SHA-256. The actual failure mode is a category error one layer up: the tool conflates "same content" with "safe to delete one copy," ignoring that many Windows apps expect self-contained install trees where a shared/default asset is *supposed* to be duplicated per-app, not deduplicated system-wide (no symlink/hardlink convention on Windows the way package managers use on Linux/macOS).

**How to avoid:**
- Never dedupe by deleting — for files inside two different applications' own install directories (detected via the same installed-programs source used by the uninstaller scanner), exclude the pair from delete candidates or show them but disable one-click delete on install-tree matches.
- Default duplicate-finder scope to user data locations (Documents, Downloads, Desktop, user-picked folders) rather than `Program Files`/`AppData\Local\<vendor>` trees; require an explicit opt-in ("also scan program directories") with a stronger warning before surfacing install-tree duplicates at all.
- Always do a full byte-for-byte compare after the hash pre-filter before ever presenting a pair as "duplicate" (matches researched best practice — hash is a candidate filter, not the final verdict).
- Never delete in place — move confirmed-selected duplicates to Recycle Bin (not `File.Delete`), so a wrong delete is one Windows-native undo away.

**Warning signs:**
Duplicate groups where both paths live under different `Program Files\<Vendor>\<App>\` trees, or under Steam/Epic/other game-library `steamapps/common/<Title>/` trees for two *different* titles.

**Phase to address:**
System Cleaner phase (duplicate-file finder sub-feature).

---

### Pitfall 3: Junk/temp cleanup force-deletes locked or in-use files

**What goes wrong:**
A file living in a "temp" location is actually open/locked by a running process (an in-progress download, a browser cache/session SQLite file mid-write, an application's active temp workspace). A naive recursive delete either fails silently (user thinks space was reclaimed, it wasn't) or, worse, forces a handle closure / corrupts a database file that was mid-transaction.

**Why it happens:**
"It's in a temp folder" is a location-based heuristic, not a usage-based one. Windows' own Disk Cleanup has repeatedly shipped this exact category of mistake — most notably silently pre-checking and deleting the whole `Downloads` folder by default (added ~Windows 10 1809), catching users unaware because a folder that *looks* disposable by name/location is treated as always-safe.

**How to avoid:**
- Never treat "temp-named folder" as sufficient — attempt a non-destructive file-lock check (open with `FileShare.None` probe) before listing a file as a delete candidate; skip and flag (not silently drop) anything currently locked.
- Never include user content folders (`Downloads`, `Documents`, `Desktop`) as default-scanned junk targets — the existing app's "Maintenance" actions this milestone absorbs (temp files/disk cleanup/icon cache) already scope to known-safe OS temp paths; keep that scope, don't widen it to "find junk anywhere" without explicit user-added folders.
- Default every scan result item to **unchecked**; nothing is pre-selected for delete (per Constraints in PROJECT.md — no "select all and go" default).

**Warning signs:**
Any scan result surfacing a file under a user profile content folder (not `%TEMP%`, not `%LOCALAPPDATA%\...\Temp`, not `%WINDIR%\Temp`), or a file whose last-write timestamp is within the current session.

**Phase to address:**
System Cleaner phase (junk/temp cleanup sub-feature).

---

### Pitfall 4: Large-file finder invites deletion of "large" mistaken for "unimportant"

**What goes wrong:**
A large-file finder's entire value proposition is "here's what's taking up space" — but size has no correlation with importance. Users delete VM disk images, game save data, media libraries, or archive files they forgot were large, because the UI presented "big" as an implicit signal of "safe to remove."

**Why it happens:**
The feature is discovery-only by design (per PROJECT.md Constraints — read-only scan, explicit selection before delete), which is correct, but discovery-only isn't the same as *informative*. A bare file list sorted by size with no context (last-accessed date, file-type/association, containing-app inference) still leads users to delete things they'll regret, because they made the call on size alone.

**How to avoid:**
- Surface last-modified/last-accessed date and inferred file type/category alongside size in the review list — "47 GB, last opened 3 years ago, .vhdx" vs "47 GB, last opened yesterday, .vhdx" changes the decision.
- Flag (don't auto-exclude, but visibly warn on) files under known-risky categories: virtual disk images (`.vhd`/`.vhdx`), database files, save-game directories, anything currently open by a running process.
- No "delete all shown" bulk action for this tool — per-item selection only, consistent with the Constraint against pre-checked/select-all defaults.

**Warning signs:**
Large-file results list offering a single "select all large files" or "clean up X GB" one-click action.

**Phase to address:**
System Cleaner phase (large-file finder sub-feature).

---

### Pitfall 5: NIC tweak UI assumes a value name exists for every adapter/driver and writes it unconditionally

**What goes wrong:**
The existing `network-apply.bat` this milestone replaces writes ~18 fixed registry values to *every* adapter unconditionally — the exact anti-pattern the granular rework exists to fix. But a naive "granular" rewrite can repeat the same mistake per-value instead of per-batch: presenting a fixed catalog of tweak names (RSS, interrupt moderation, offloads, buffer sizes) as if every NIC driver exposes all of them. In practice these live under each adapter's own driver-instance registry subkey (`HKLM\SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002bE10318}\<0000, 0001, ...>`), and which value names/ranges a given driver actually reads is vendor- and driver-version-specific (vendor docs, e.g. NVIDIA WinOF-2, confirm this explicitly — some parameters must be manually *created* to take effect, others don't exist for that driver at all). Writing a value the driver doesn't recognize is either a silent no-op (the UI shows "applied," nothing changed — false confidence) or, on a driver that *does* parse a similarly-named value with different semantics/valid range, can put the adapter into an undefined state.

**Why it happens:**
Treating NIC tweaks as a fixed, app-defined catalog (the same mental model that works fine for OS registry tweaks in the rest of Akari Tool's SettingDefinition stack) doesn't hold for NIC driver parameters — those are defined by the third-party driver, not by Windows, and vary per vendor/model/driver version in a way OS settings don't.

**How to avoid:**
- Enumerate each adapter's *actual* advertised parameter set at runtime (Device Manager "Advanced" tab equivalent — `IDeviceIoControl`/registry enumeration of the adapter's `ndi\params` subkey, which lists the driver-declared parameter names, types, and valid ranges) rather than assuming Akari's fixed 18-value catalog applies everywhere.
- Only expose a toggle/value in the per-adapter UI if it is present in that adapter's declared parameter set; grey out or hide (don't fake-apply) anything the driver doesn't declare.
- Validate any numeric/enum value against the driver-declared valid range before writing, not just against an app-side hardcoded range.

**Warning signs:**
A tweak shows as "applied" in the UI immediately after write with no adapter restart and no read-back verification against the live driver state.

**Phase to address:**
NIC-tweak phase — this is the core detection/scoping risk for that phase.

---

### Pitfall 6: NIC per-value revert is not built on captured original state

**What goes wrong:**
"Real per-value revert" (the explicit ask in PROJECT.md, replacing the current 5-line blanket `network-revert.bat`) is only real if the revert restores the value that was actually present *before* Akari wrote it — including the case where the value didn't exist at all before Akari created it. A revert implementation built the easy way — hardcoding "the known Windows/driver default" per value name — reintroduces the exact blanket-script problem in per-value clothing: it can restore the *wrong* default for that specific driver/vendor, or leave a value present that didn't originally exist (subtly changing driver behavior even after a "successful" revert).

**Why it happens:**
Hardcoded defaults are much less work than capturing and persisting live pre-write state per adapter per value, and the difference is invisible in testing unless someone specifically compares registry state before-apply vs after-revert on a machine where the value was originally absent.

**How to avoid:**
- On first read of any per-adapter value (before ever offering a toggle to the user), capture and persist the live original value *or an explicit "did not exist" sentinel* — same pattern already used elsewhere in the app for reversibility, not a new concept.
- Revert must delete the value if the sentinel says "did not exist," not write a default in its place.
- Store captured state keyed by adapter instance (not just adapter name — NICs can be replaced/renamed) so revert targets the same physical adapter that was tweaked, not whatever now occupies that slot.

**Warning signs:**
A revert path that has no persisted "original value" store and instead branches on a static default table.

**Phase to address:**
NIC-tweak phase — directly maps to the "real per-value revert" requirement in PROJECT.md Constraints.

---

### Pitfall 7: NIC value writes don't account for adapter-restart timing, and restarting the wrong adapter drops connectivity mid-operation

**What goes wrong:**
Many NIC driver parameters are only re-read at driver bind/init time — a registry write with no adapter restart can appear to silently "not apply," leading users (or the app's own read-back verification) to conclude the write failed when it actually just needs `Disable`/`Enable` or `Restart-NetAdapter` to take effect. Separately, if the tool restarts the adapter currently carrying the user's active session (including a remote/RDP session used to administer the machine), connectivity drops mid-operation — potentially stranding an in-progress apply/revert half-committed.

**Why it happens:**
NIC tweaks are unlike other OS registry settings in the app's existing catalog stack: they require a device-level reinit to take effect, which nothing else in the SettingDefinition stack currently models (no other tab restarts a hardware device after a write).

**How to avoid:**
- After a per-value write, explicitly cycle the adapter (or clearly tell the user a restart/reboot is required) rather than silently reading back state that hasn't refreshed yet.
- Detect whether the target adapter is the one carrying the current process's/RDP session's active route before restarting it, and require explicit extra confirmation (or block it) if so.
- Apply and restart per-adapter, not as one batch across all adapters — so a failure on one adapter doesn't touch others, and the user is never surprised by a connectivity drop they didn't select.

**Warning signs:**
A NIC tweak "Apply" that returns success immediately after the registry write with no adapter cycle step, or a bulk "apply to all adapters" action.

**Phase to address:**
NIC-tweak phase.

---

### Pitfall 8: Registry ACL failures are swallowed as generic exceptions instead of surfaced as "needs elevation"

**What goes wrong:**
`WindowsRegistryService.SetValue`/`CreateSubKey` can throw `SecurityException` (not `UnauthorizedAccessException`) on ACL-locked keys — this is a **documented existing gap** in this codebase (see `.planning/codebase/CONCERNS.md`, "Registry Write ACL Handling"), not a hypothetical. All three new tools in this milestone write to registry locations that can plausibly be ACL-locked: protected `Services\*` keys the leftover scanner might touch if an uninstalled app left a service registration, and NIC driver instance keys under `Control\Class\{...}` which are more tightly ACL'd than typical `HKCU` tweak targets. If this exception path is inherited unfixed, a per-item "apply" in the new tools fails with a generic error instead of telling the user "this needs elevation" or "this specific adapter/key is policy-protected" — undermining the "legible to the user" bar this milestone's Core Value explicitly sets.

**Why it happens:**
The existing catch-all `catch (Exception)` + broad logging pattern predates this milestone and wasn't written with NIC/leftover-scan ACL surface area in mind.

**How to avoid:**
- Before building the leftover scanner or NIC UI on top of `WindowsRegistryService`, fix (or at minimum wrap at the new call sites) the `SecurityException` vs `UnauthorizedAccessException` distinction so both new tools can show "needs elevation" / "protected by policy" as a specific, actionable status per item rather than a generic failure.
- Route every write in both new tools through the existing `IProcessRestartManager`/elevation pattern (per PROJECT.md Constraints) rather than assuming the current process already has sufficient rights — some leftover keys and some NIC driver keys will require elevation the rest of the app's SettingDefinition writes don't need.

**Warning signs:**
A delete/apply action in the new tools that reports generic "Error" or silently no-ops instead of a specific elevation/permission message.

**Phase to address:**
All three sub-phases inherit this — fix (or wrap) it once, early, ideally before the leftover-scan phase's first delete action ships, since that's the first of the three to hit protected keys (orphaned service registrations).

---

### Pitfall 9: Scheduled-task leftover detection false-positives on shared/OEM task naming conventions

**What goes wrong:**
Scheduled tasks left behind by an uninstalled app are matched by name/path heuristics (task folder named after the vendor/app). A task belonging to a still-installed shared component that happens to share a naming convention or vendor folder with the uninstalled app (OEM driver-update tasks, shared telemetry/update tasks under a common vendor `Task Scheduler` folder) gets flagged and disabled/deleted, breaking the still-installed component's update or health-check mechanism.

**Why it happens:**
Same root cause as Pitfall 1 (name-based correlation without a second corroborating signal), applied to the Task Scheduler namespace instead of the registry/filesystem namespace.

**How to avoid:**
- Cross-check the task's actual target executable path (Action → "Start a program") against the same currently-installed-programs source used for registry/folder leftover detection — if the path still resolves to a file that exists and belongs to a currently-installed program, exclude it regardless of task name/folder match.
- Never auto-delete a scheduled task; disabling first (reversible) with delete as a separate, explicitly-confirmed second step is safer given task recreation is often non-trivial for the user to redo manually.

**Warning signs:**
A flagged task whose target executable path still exists on disk and isn't inside the uninstalled app's own install directory.

**Phase to address:**
Uninstaller leftover-scan phase (scheduled-task detection sub-feature).

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Name-substring matching for leftover detection (no cross-reference against live installed-programs list) | Fast to ship, simple code | False positives break other installed software; erodes trust in the whole tool after one bad report | Never for delete; acceptable only as a first-pass *candidate* filter before cross-referencing |
| Hardcoded "known default" table for NIC revert instead of captured original state | Less state to persist, simpler code | Reintroduces the exact blanket-revert problem the milestone exists to fix, invisibly | Never — this is the explicit ask in PROJECT.md Constraints |
| Fixed app-side catalog of NIC tweak names applied to every adapter | Reuses the existing SettingDefinition catalog pattern the rest of the app already has | Writes values some drivers don't declare (silent no-op or undefined driver state) | Never for NIC; the existing catalog pattern is correct for OS registry settings but does not transfer to third-party driver parameters |
| Catching registry write failures as generic `Exception` | Fast, matches existing app pattern elsewhere | Users can't tell "needs elevation" from "actually broken"; masked ACL failures already flagged in CONCERNS.md | Only acceptable for genuinely unexpected errors, never for the known `SecurityException` ACL case |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|-------------------|
| Existing `Software ▸ External Apps`/`Windows Apps` uninstaller | Leftover scanner re-scans installed-programs state from a stale snapshot instead of live registry read | Re-read the live `Uninstall` registry key set at leftover-scan time; the uninstall action and the scan are two separate steps that can be arbitrarily far apart |
| `IWindowsRegistryService`/`WindowsRegistryService` | Assuming all writes succeed with the same exception surface as HKCU tweak writes elsewhere in the app | Explicitly handle `SecurityException` for the new ACL surface area (Services keys, NIC driver Class keys) — see Pitfall 8 |
| `IProcessRestartManager` elevation pattern | Launching raw elevated processes for NIC/registry operations instead of routing through the existing pattern | Every write in both new tools goes through the existing elevation seam, per PROJECT.md Constraints |
| Existing System Restore point integration (`ISystemRestoreService`/`SystemBackupService`) | Building a new ad hoc "create a restore point" call for the Cleaner/Uninstaller phases instead of reusing the existing 4g stack | Reuse the existing restore-point creation flow before any batch of deletes runs, consistent with how the rest of the app already offers this |
| `DriftScanner`/`TweakRegistry` | Not registering NIC per-value state with the existing drift/backup infrastructure, leaving NIC tweaks invisible to Backup/Restore + global search | Decide explicitly whether NIC per-value state should route through `SettingBackupService` like every other tab, or intentionally stay out of scope (document the decision either way) |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Full-disk recursive duplicate/large-file scan with no scope limit | UI hangs or takes minutes on first run; users cancel and distrust the tool | Default scope to user-selected folders + known-safe OS temp locations, not full-drive; make full-drive scan an explicit opt-in with a progress/cancel affordance | Breaks noticeably above a few hundred thousand files or on spinning-disk hardware |
| Hashing every file before size-filtering in duplicate finder | Wasted I/O hashing large files that have no size-match candidate at all | Group by exact file size first (cheap), only hash within same-size groups | Breaks on large media libraries where most files are unique-sized |
| Synchronous registry enumeration across all adapters on page load | NIC tweak page feels frozen on machines with many virtual/physical adapters (VPN, Hyper-V, Bluetooth PAN, etc.) | Enumerate adapters and their declared parameter sets off the UI thread, same async pattern as the rest of the app's `SettingPageViewModel.Build()` | Breaks on machines with 10+ adapters (common with VPN/virtualization software installed) |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Leftover scanner runs unelevated but silently fails to detect/report protected-key leftovers as "couldn't check" | User believes a full clean scan happened when protected areas were actually skipped | Explicitly report skipped/inaccessible items in the review list as "needs elevation to check," not omit them silently |
| Deleting a scheduled task or registry key that turns out to be a security-relevant boot/service entry, based on name-match alone | Could disable a security-relevant startup mechanism (AV/EDR helper task, driver health-check) mistaken for app leftovers | Apply the same "never touch" posture the app already has for Defender and for the documented never-fully-disable service list — extend an equivalent deny-list to leftover scan/delete candidates |
| NIC tweak writes performed without validating driver-declared valid ranges | A value outside the driver's accepted range could put the adapter into an unstable/insecure state (e.g., disabling checksum offload validation unexpectedly) | Validate against driver-declared range before write (see Pitfall 5); never trust an app-side hardcoded range as authoritative |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Pre-checked "select all" on any scan result list | Users delete more than they intended, often the exact failure mode that made Windows' own Disk Cleanup notorious (Downloads folder default-checked) | Every scan result defaults unchecked; explicit per-item selection, consistent with PROJECT.md Constraints |
| One confirmation dialog per delete, regardless of severity | Habituation ("dialog blindness") — users click through without reading after the first few uses | Scale friction to severity: routine temp-file review needs one clear list+confirm step; NIC changes to the active adapter or leftover deletes touching shared-component-adjacent paths warrant a stronger, distinct confirmation |
| No explanation of *why* an item was flagged | Users can't make an informed keep/delete decision, so they either delete everything or trust nothing | Show the detection reason per item (e.g., "folder name matches uninstalled app X, no currently-installed program references this path") in the review step |
| NIC tweak toggle shows "on" immediately after write, before verifying the driver actually picked it up | Users think a tweak applied when it silently no-op'd (Pitfall 5/7); erodes trust in every other toggle in the app | Read back live driver state (post-restart-cycle where required) before showing the toggle as applied, same "read-verify" discipline the SettingDefinition stack already uses elsewhere |

## "Looks Done But Isn't" Checklist

- [ ] **Leftover scanner delete action:** Often missing a live cross-reference against currently-installed programs at delete time (not just at scan time) — verify a re-check happens if any time has passed between scan and delete, in case the user installed something in between.
- [ ] **Duplicate-file finder:** Often missing the full byte-compare step after the hash match — verify it's not deleting on hash-match alone.
- [ ] **Duplicate-file finder:** Often missing install-tree exclusion — verify pairs inside two different `Program Files\<Vendor>\` or game-library trees are excluded or specially warned, not treated like ordinary user-file duplicates.
- [ ] **NIC tweak revert:** Often built on a hardcoded default table instead of captured original state — verify revert restores "value did not exist" correctly (deletes, doesn't write a default) for values the app itself created.
- [ ] **NIC tweak apply:** Often missing driver-declared-range validation — verify the UI reads the adapter's actual `ndi\params` (or equivalent) before exposing a control, not a fixed app-side catalog.
- [ ] **All three tools:** Often missing elevation-specific error messaging — verify `SecurityException` (ACL-locked keys) is surfaced distinctly from a generic failure, not swallowed by a catch-all handler (existing gap, see CONCERNS.md).
- [ ] **All three tools' delete/apply actions:** Often missing a restore-point checkpoint before a destructive batch runs — verify the existing `ISystemRestoreService` flow is invoked, not skipped because it's "new tools, different flow."

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|-----------------|
| Leftover scanner deleted a shared-component key/folder | MEDIUM–HIGH | If a restore point was created before the batch ran (Constraint requirement), restore from it; otherwise the affected app typically needs a repair/reinstall — no registry-level undo exists once deleted |
| Duplicate-file finder deleted a file still needed by another app's install tree | LOW–MEDIUM | If deletion routed through Recycle Bin (not `File.Delete`), restore from Recycle Bin; otherwise reinstall/repair the affected app to regenerate its own copy of the shared asset |
| NIC tweak wrote an invalid value and broke adapter connectivity | LOW if revert-state was captured (Pitfall 6) — one-click revert; HIGH if not, requiring manual registry edit or driver reinstall while offline | Persisted per-value original-state capture (or sentinel-for-absent) makes this a one-click fix; without it, guide the user to `netcfg`/driver reinstall as a last resort |
| Scheduled task disabled that turned out to still be needed | LOW | Re-enable via the same UI (disable-first, delete-as-separate-step per Pitfall 9 keeps this cheap) |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|-------------------|----------------|
| Shared-component false positives (registry/folder) | Uninstaller leftover-scan phase | Test against a machine with multiple apps sharing a redistributable/runtime; confirm none are flagged |
| Scheduled-task false positives | Uninstaller leftover-scan phase | Test against OEM/shared-vendor scheduled tasks; confirm target-path cross-reference excludes still-valid executables |
| Duplicate-file semantic false positives (install-tree assets) | System Cleaner phase (duplicate-file finder) | Test with two different installed apps that ship a bit-identical shared asset; confirm exclusion or explicit warning, not silent delete |
| Hash-only delete without byte verification | System Cleaner phase (duplicate-file finder) | Code review: confirm byte-compare step exists between hash-match and delete-eligible state |
| Locked/in-use file force-delete | System Cleaner phase (junk/temp cleanup) | Test cleanup while a target file is open/locked by another process; confirm it's skipped and flagged, not force-deleted |
| Large-file finder context-free deletion | System Cleaner phase (large-file finder) | UAT: confirm last-modified/type context is shown per item, no bulk "delete all shown" action exists |
| NIC value written for unsupported driver | NIC-tweak phase | Test against at least two different NIC vendors/drivers; confirm the exposed control set differs per adapter and no value is written that the driver doesn't declare |
| NIC revert not based on captured state | NIC-tweak phase | Test: apply a tweak to a value that didn't exist before, revert, confirm the value is deleted (not defaulted) afterward |
| NIC restart timing / wrong-adapter restart | NIC-tweak phase | Test apply-then-verify against a live driver read-back after restart cycle; test restart-adapter guard against the adapter carrying the current session |
| Swallowed `SecurityException` on ACL-locked keys | All three phases (fix once, early) | Test against a known ACL-protected key path (e.g. a protected `Services\*` key); confirm a specific "needs elevation" message, not a generic error |
| Pre-checked/select-all destructive defaults | All three phases (UX layer) | UAT: every scan result list opens with nothing selected |
| No restore-point checkpoint before destructive batch | All three phases | Code review: confirm the existing restore-point flow is called before any batch delete/apply, not only referenced in the leftover-scanner |

## Sources

- [Cleaning your Windows registry with CCleaner probably isn't helping — and might be hurting (MakeUseOf)](https://www.makeuseof.com/cleaning-windows-registry-with-ccleaner-isnt-helping-might-be-hurting/) — MEDIUM
- [CCleaner community/bug-report threads on custom key deletion and stubborn keys](https://community.ccleaner.com/t/custom-registry-keys-are-removed-after-a-shutdown-reboot-cycle/69049) — LOW (user reports, not vendor-verified)
- [Are Registry Cleaners Worth It? (PCWorld)](https://www.pcworld.com/article/512635/reg_cleaners_worthwhile.html) — MEDIUM
- [The truth about Windows registry cleaners (HowToGeek)](https://www.howtogeek.com/the-truth-about-windows-registry-cleaners-and-why-people-still-use-them/) — MEDIUM
- [Are Registry Cleaners good or bad? (TheWindowsClub)](https://www.thewindowsclub.com/do-registry-cleaners-defragmenters-really-help-or-are-they-snake-oil) — MEDIUM
- [The Uninstaller Left 50 Registry Keys Behind (Microsoft Community Hub)](https://techcommunity.microsoft.com/discussions/windows11/the-uninstaller-left-50-registry-keys-behind/4534102) — LOW
- [What Are Uninstall Leftovers? (Disk Mop Blog)](https://diskmop.com/blog/uninstall-leftovers-cleaner) — LOW (vendor content, directionally consistent with other sources)
- [rmlint — Cautions (why it's hard to write a dupefinder)](https://rmlint.readthedocs.io/en/master/cautions.html) — MEDIUM (maintainer-authored, technical)
- [Duplicate Detective — File Checksums](https://www.duplicatedetective.com/content/static/help/html/filechecksums.html) — MEDIUM
- [Configuring the Driver Registry Keys — NVIDIA WinOF-2 Docs](https://docs.nvidia.com/networking/display/winof2v280/configuring+the+driver+registry+keys) — MEDIUM (vendor documentation)
- [How to Remove Hidden/Ghost Network Adapters in Windows (Windows OS Hub)](https://woshub.com/remove-hidden-ghost-network-adapter-windows/) — LOW
- [Disk Cleanup Downloads-folder default-checked pattern, various user reports](https://www.easeus.com/storage-media-recovery/undo-disk-cleanup-in-windows-10-8-7.html) — MEDIUM (recurring pattern corroborated across multiple independent sources)
- [Checkboxes: Design Guidelines (NN/g)](https://www.nngroup.com/articles/checkboxes-design-guidelines/) — MEDIUM
- [A UX guide to destructive actions (Medium/Bootcamp)](https://medium.com/design-bootcamp/a-ux-guide-to-destructive-actions-their-use-cases-and-best-practices-f1d8a9478d03) — LOW
- Internal: `.planning/PROJECT.md` (Constraints, Context) — HIGH (primary source, this codebase)
- Internal: `.planning/codebase/CONCERNS.md` ("Registry Write ACL Handling," "ElevationService.RunAsSystem Impersonation Scope," fragile-areas sections) — HIGH (primary source, this codebase)

---
*Pitfalls research for: Akari Tool System Tools rework — uninstaller leftover scanner, System Cleaner, NIC tweak UI*
*Researched: 2026-08-27*
