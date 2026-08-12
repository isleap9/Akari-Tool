# Akari Tool — MVVM Migration Log (continued)

Continuation of MIGRATION_LOG.md (Phases 1-8, archived there). New phases logged
here going forward.

---

## MVVM Phase 9 — Gaming ▸ System Services extracted (37 service dropdowns + 1 toggle) — **COMPLETE (VM sign-off pending)**

Date: 2026-08-05

The real Gaming System Services section (confirmed by isleap at the Phase-8
checkpoint) — 37 per-service startup-type dropdowns + the `gaming-input-app-preload`
toggle — extracted VERBATIM from build #2 into the Gaming catalog, and wired into
the Gaming page in net8's position (after Security). This is a normal catalog
section (tweak rows that register), NOT the mis-ported AkariOS preset card. Build #2
read-only; no Defender code touched.

### Housekeeping (items 1-2 of this run)

- **`MIGRATION_LOG2.md` created** as the active log (this file). Header only; no
  prior phase content copied. Phases 1-8 remain in `MIGRATION_LOG.md`.
- **Log-maintenance pointer updated.** CLAUDE.md's "MVVM rebuild status" section now
  opens with a line naming `MIGRATION_LOG2.md` as the active log (Phases 1-8 in
  `MIGRATION_LOG.md` for history). MIGRATION_PROMPT.md §6 updated to match (that is
  where the actual "Maintain the migration log" instruction lives; CLAUDE.md had no
  prior log line, so a pointer was added there).

### Build status (literal)

VS 18 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:21.23
```

### Files

- **New: `Tabs/Gaming/Catalog/GamingTweaks.SystemServices.cs`** — a `SystemServices(
  Action<string> Log)` method returning the 38-def `TweakDefinition[]`, matching the
  shape of the other extracted Gaming catalog files. Lifted VERBATIM from build #2's
  `GamingTab.SystemServices.cs`:
  - all 38 defs — same Ids, same order, same `recommendedStart`/`defaultStart` per
    service, same `disabledWarning` text, same `ReadCurrentIndex`/`ApplyIndex` (Start
    DWORD at `HKLM\SYSTEM\CurrentControlSet\Services\<key>`), same
    `gaming-input-app-preload` toggle body.
  - Only framework-mechanical changes: `ServiceDropdown` made `static` with `Log`
    threaded through as a parameter (net8 captured the instance `Log()` + instance
    `ReadDword`); it now calls the existing static `GamingTweaks.ReadDword`
    (identical signature); `BuildSystemServices(StackPanel)+AddSection` became the
    array-returning method. No def content reordered, retyped, or cleaned up.
- **Changed: `ViewModels/GamingViewModel.cs`** — added
  `("System Services", GamingTweaks.SystemServices(log))` to `BuildCatalog()`
  between Security and Accessibility (net8 rendered it after Security, before the
  still-unported Scheduled Tasks / System Restore). Stale class comment updated.

### Id diff vs build #2 — IDENTICAL

Ordered Id extraction (the 37 `ServiceDropdown(...)` first args + the 1 inline
`Id=`) diffed net8 vs MVVM: **IDENTICAL** — 38 Ids, same order, no additions,
removals, or renames.

### Verification (read-only, de-elevated + UIA; NOTHING applied)

- **Guard now tiles Gaming `[0..110)` and overall `[0..232)`** (72 + 38 = 110;
  Gaming is 10 sections now). Full table:
  ```
  [Gaming] 110 tweaks registered in 10 sections (registry total 110).
  [Sound] 5 tweaks registered in 1 sections (registry total 115).
  [Notifications] 16 tweaks registered in 5 sections (registry total 131).
  [Update] 12 tweaks registered in 3 sections (registry total 143).
  [Privacy] 89 tweaks registered in 13 sections (registry total 232).
  [WARMUP]   Gaming [0..110) 110 rows — Gaming & Performance
  [WARMUP]   Sound [110..115) 5 rows — Sound
  [WARMUP]   Notifications [115..131) 16 rows — Notifications
  [WARMUP]   Update [131..143) 12 rows — Windows Updates
  [WARMUP]   Privacy [143..232) 89 rows — Privacy & Security
  [WARMUP] OK: 5 range(s) contiguous and non-empty, tiling [0..232).
  ```
- **Section renders in position** — on-screen heading order:
  Game Mode -> Processor -> Graphics -> Storage -> Network -> Xbox -> Security ->
  **System Services** -> Accessibility -> Visual Effects (System Services is between
  Security and Accessibility; verified index-adjacent to both).
- **37 dropdowns + 1 toggle** — Gaming page ComboBox count went 7 -> 44 (+37); the
  "Input App Preload" toggle is present.
- **ReadState reflects real service Start values (read-only)** — sampled 6 rows
  against the live registry:
  | Service | registry Start | UI selection |
  |---|---|---|
  | SysMain | 4 | Disabled ✓ |
  | Spooler | 3 | Manual ✓ |
  | DiagTrack | 3 | Manual ✓ |
  | WSearch | 3 | Manual ✓ |
  | CDPSvc | 3 | Manual ✓ |
  | Fax | (absent) | Disabled ✓ (ReadCurrentIndex falls back to defaultStart 4) |

  The Fax row confirms the absent-service fallback: no `Start` value ⇒ the dropdown
  shows the per-service `defaultStart` (Disabled), exactly as net8's ReadCurrentIndex
  specifies. **No service startup type was changed.**

### Landmine note (for the eventual VM apply pass — NOT done here)

The section deliberately EXCLUDES the boot-critical never-disable services
(`DcomLaunch`, `RpcSs`, `RpcEptMapper`, `SamSs`, and also `WpnService`, `DusmSvc`/
`Ndu`, `DPS`). The one Action-Center/NVIDIA-App dependency present, **CDPSvc**, is
recommended **Manual (3)** with a Disabled-option warning (Night Light / Phone Link /
clipboard). All Read/Apply logic is byte-identical to net8; on this machine CDPSvc
reads Manual, matching the recommended value.

### VM checklist (Phase 9 — for isleap; DESTRUCTIVE, VM only)

- [ ] Change a couple of service dropdowns (e.g. Spooler -> Disabled, then back) —
      confirms `ApplyIndex` writes the Start DWORD and the "Restart to apply." log.
- [ ] Confirm the never-disable services are ABSENT from the list (no DcomLaunch/
      RpcSs/RpcEptMapper/SamSs/WpnService/DusmSvc/Ndu/DPS row).
- [ ] CDPSvc: selecting Disabled shows the Night-Light warning before applying.
- [ ] Section-level bulk "Recommended"/"Defaults" bars act on these rows correctly.
- [ ] Guard still tiles `[0..232)` after any change (service writes don't alter row
      registration).

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Scheduled Tasks / System Restore** — still separate, not-yet-shown bespoke
  sections for a future checkpoint. Not ported.

---

## MVVM Phase 10 — Scheduled Tasks (SHOW ONLY)

Date: 2026-08-05

Show-first recon checkpoint (same discipline as Phase 8). Located net8's Gaming ▸
Scheduled Tasks section. **Nothing extracted, ported, or edited** — build #3 code and
build #2 both untouched; this entry is the only write. Awaiting isleap's confirmation
before any code.

### Source

- **File:** build #2 `Tabs/Gaming/GamingTab.ScheduledTasks.cs` (117 lines).
- **Method:** `BuildScheduledTasks(StackPanel panel)`, called from `GamingTab.Build()`
  right after `BuildSystemServices` (line 37). Helpers in the same file:
  `ReadTaskEnabled(string taskPath)` and `SetTaskEnabled(string taskPath, string name,
  bool enable)`.

### Kind of UI — TweakDefinition rows (registers), NOT bespoke

This is **18 `TweakDefinition` toggle rows**, built by projecting a local
`(id, name, desc, taskPath)[]` table through `tasks.Select(t => new TweakDefinition
{...})` and handing the array to `AddSection(panel, "Scheduled Tasks", defs)`. So —
like System Services (Phase 9), and UNLIKE the Phase-7 preset card — these ARE
TweakDefinitions that register with `TweakRegistry` and belong **inside** the
Mark/ClaimRange bracket. Each is a toggle (`RecommendedState = false`,
`DefaultState = true` → recommended OFF/disabled, Windows default ON/enabled).

Implication for a future extraction: porting this correctly ADDS 18 registered rows —
Gaming would go `110 → 128`, overall `232 → 250`. (Not done in this phase.)

### Row count + every Id, in order (18)

| # | Id | Task path |
|---|---|---|
| 1 | `gaming-task-compatibility-appraiser` | `\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser` |
| 2 | `gaming-task-program-data-updater` | `\Microsoft\Windows\Application Experience\ProgramDataUpdater` |
| 3 | `gaming-task-ceip-consolidator` | `\Microsoft\Windows\Customer Experience Improvement Program\Consolidator` |
| 4 | `gaming-task-usb-ceip` | `\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip` |
| 5 | `gaming-task-disk-diagnostic` | `\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector` |
| 6 | `gaming-task-feedback-dmclient` | `\Microsoft\Windows\Feedback\Siuf\DmClient` |
| 7 | `gaming-task-feedback-dmclient-download` | `\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload` |
| 8 | `gaming-task-error-reporting-queue` | `\Microsoft\Windows\Windows Error Reporting\QueueReporting` |
| 9 | `gaming-task-sqm` | `\Microsoft\Windows\PI\Sqm-Tasks` |
| 10 | `gaming-task-mare-backup` | `\Microsoft\Windows\Application Experience\MareBackup` |
| 11 | `gaming-task-startup-app` | `\Microsoft\Windows\Application Experience\StartupAppTask` |
| 12 | `gaming-task-maps-update` | `\Microsoft\Windows\Maps\MapsUpdateTask` |
| 13 | `gaming-task-autochk-proxy` | `\Microsoft\Windows\Autochk\Proxy` |
| 14 | `gaming-task-power-efficiency` | `\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem` |
| 15 | `gaming-task-windows-ai-recall-config` | `\Microsoft\Windows\WindowsAI\RecallConfiguration` |
| 16 | `gaming-task-windows-ai-recall-pipeline` | `\Microsoft\Windows\WindowsAI\RecallPipeline` |
| 17 | `gaming-task-office-actions-server` | `\Microsoft\Office\Office Actions Server` |
| 18 | `gaming-task-family-safety` | `\Microsoft\Windows\Shell\FamilySafetyMonitor` |

### Data model + actions

Each row's identity is a **full scheduled-task path string** (`taskPath`, e.g.
`\Microsoft\Windows\...\Consolidator`) — not a GUID. The only action is **enable /
disable** (a toggle); there is no run-now / delete.
- **Read** (`ReadTaskEnabled`): shells `schtasks.exe /Query /TN "<path>" /FO CSV /NH`,
  returns `null` on non-zero exit (task absent), else `!output.Contains("Disabled")`
  (present + not disabled ⇒ toggle ON).
- **Apply** (`SetTaskEnabled`): `TweakHelpers.RunCommand("schtasks.exe",
  "/Change /TN \"<path>\" /Enable|/Disable")`, logs `"<name>: enabled|disabled."`.

### Three representative rows (full shape)

All 18 share the shape below; only `id`/`name`/`desc`/`taskPath` vary:
```
new TweakDefinition {
    Id = t.id, Name = t.name, Description = t.desc,
    RecommendedState = false,          // recommended: task OFF (disabled)
    DefaultState     = true,           // Windows default: task ON (enabled)
    ReadState = () => ReadTaskEnabled(t.taskPath),
    Apply     = on => SetTaskEnabled(t.taskPath, t.name, on)
}
```
- **#1 Microsoft Compatibility Appraiser** — id `gaming-task-compatibility-appraiser`,
  path `\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser`,
  desc "Collects program compatibility telemetry for Windows upgrades. Disable to
  reduce telemetry".
- **#3 CEIP Consolidator** — id `gaming-task-ceip-consolidator`, path
  `\Microsoft\Windows\Customer Experience Improvement Program\Consolidator`, desc
  "Consolidates and uploads usage data as part of the Customer Experience Improvement
  Program".
- **#15 Windows AI Recall Configuration** — id `gaming-task-windows-ai-recall-config`,
  path `\Microsoft\Windows\WindowsAI\RecallConfiguration`, desc "Windows AI Recall
  configuration task. Disable to prevent Recall from being configured in the
  background". (AI/privacy — NOT Defender; see below.)

### Landmine handling

- **No never-disable / gating logic.** Unlike the services section (which excludes
  boot-critical services) or Power (hardware-gated rows), this section has NO
  exclusions and NO hardware/OS-edition gating — every row renders unconditionally.
  Safety is by CURATION: the 18 tasks are all telemetry / diagnostics / feedback /
  CEIP / AI (Recall, Office Actions) tasks, all disable-recommended. Absent tasks are
  handled by `ReadTaskEnabled` returning `null` (row reads as unknown, per the shared
  toggle logic).
- **Defender: checked — NONE.** No task path targets `\Microsoft\Windows\Windows
  Defender\*` or any Defender/Sense component (grep for defend/windefend/sense = 0
  hits). Rows 15-16 are **Windows AI Recall** (`\Microsoft\Windows\WindowsAI\Recall*`)
  — a privacy/AI feature, not Defender. So no Defender rule is triggered; nothing to
  stop describing.

### Relationship to `PlaybookTweaks.ScheduledTasks.cs` — SEPARATE implementations

Checked both files (not assumed):
- **Gaming ▸ Scheduled Tasks has its OWN implementation.** `GamingTab.ScheduledTasks.cs`
  does not reference `PlaybookTweaks` at all; its `ReadTaskEnabled`/`SetTaskEnabled`
  helpers live in that same Gaming file.
- **`Tabs/Shared/PlaybookTweaks.ScheduledTasks.cs`** (already in build #3, 137 lines)
  is a DIFFERENT surface: a `private static readonly string[] ScheduledTasks` bulk
  list consumed by `ApplyScheduledTasksAsync` / `EnableScheduledTasksAsync` — the mass
  fire-all-tasks path behind the AkariOS Playbook "Apply All" / "Undo All" buttons. No
  per-row toggles, no `TweakDefinition`s, different Ids (none), different curated list.
- **Consequence for extraction:** the Gaming section would need NEW logic ported (the
  18-task table + the two `ReadTaskEnabled`/`SetTaskEnabled` helpers), NOT a thin
  wrapper over the shared Playbook code. The apply helper only depends on
  `TweakHelpers.RunCommand`, which is already present in build #3. The port would be
  small and self-contained (one catalog file, like Phase 9), and would go INSIDE the
  Gaming Mark/ClaimRange bracket as a normal `("Scheduled Tasks", ...)` catalog
  section after System Services.

### Status

SHOW ONLY. No `GamingTweaks.ScheduledTasks.cs` created, `BuildCatalog` untouched,
`PlaybookTweaks.ScheduledTasks.cs` untouched, build #2 untouched. System Restore is
still a separate, not-yet-shown section for a later checkpoint.

---

## MVVM Phase 11 — Gaming ▸ Scheduled Tasks extracted (18 task toggles) — **COMPLETE (VM sign-off pending)**

Date: 2026-08-05

The Gaming Scheduled Tasks section (confirmed by isleap at the Phase-10 checkpoint) —
18 `TweakDefinition` toggle rows, each enabling/disabling one scheduled task via
schtasks.exe — extracted VERBATIM from build #2 into the Gaming catalog and wired
into the Gaming page after System Services. Self-contained implementation; the
unrelated `Tabs/Shared/PlaybookTweaks.ScheduledTasks.cs` was NOT referenced or
touched. Build #2 read-only; no Defender code touched.

### Build status (literal)

VS 18 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:23.89
```

### Files

- **New: `Tabs/Gaming/Catalog/GamingTweaks.ScheduledTasks.cs`** — a `ScheduledTasks(
  Action<string> Log)` method returning the 18-def `TweakDefinition[]`, matching the
  shape of `GamingTweaks.SystemServices`. Lifted VERBATIM from build #2's
  `GamingTab.ScheduledTasks.cs`: same 18 Ids in the same order, same task paths, same
  Name/Description, same `RecommendedState = false` / `DefaultState = true` polarity
  (recommended OFF/disabled, Windows default ON/enabled — NOT normalized or
  inverted), same `ReadTaskEnabled` (schtasks `/Query …/FO CSV /NH`, `null` on
  non-zero exit, else `!output.Contains("Disabled")`) and `SetTaskEnabled`
  (`/Change …/Enable|/Disable` via `TweakHelpers.RunCommand`).
  - Only framework-mechanical changes (same pattern as ServiceDropdown in Phase 9):
    the two helpers are `static` with `Log` threaded through as a parameter (net8
    captured the instance `Log()`); `SetTaskEnabled` uses the already-present static
    `TweakHelpers.RunCommand`; `BuildScheduledTasks(StackPanel)+AddSection` became the
    array-returning method. No task, Id, path, or text reordered, retyped, or cleaned up.
- **Changed: `ViewModels/GamingViewModel.cs`** — added
  `("Scheduled Tasks", GamingTweaks.ScheduledTasks(log))` to `BuildCatalog()` right
  after System Services (net8 Build() order). Stale class comment updated.

### Id diff vs build #2 — IDENTICAL

`grep -oE '"gaming-task-[a-z-]+"'` net8 vs MVVM: **IDENTICAL** — 18 Ids, same order,
no additions/removals/renames. Task-path diff (`@"\Microsoft…"`): **IDENTICAL** too.

### Verification (read-only, de-elevated + UIA; NO task enabled/disabled)

- **Guard now tiles Gaming `[0..128)` and overall `[0..250)`** (110 + 18 = 128;
  Gaming is 11 sections now). Full table:
  ```
  [Gaming] 128 tweaks registered in 11 sections (registry total 128).
  [Sound] 5 tweaks registered in 1 sections (registry total 133).
  [Notifications] 16 tweaks registered in 5 sections (registry total 149).
  [Update] 12 tweaks registered in 3 sections (registry total 161).
  [Privacy] 89 tweaks registered in 13 sections (registry total 250).
  [WARMUP]   Gaming [0..128) 128 rows — Gaming & Performance
  [WARMUP]   Sound [128..133) 5 rows — Sound
  [WARMUP]   Notifications [133..149) 16 rows — Notifications
  [WARMUP]   Update [149..161) 12 rows — Windows Updates
  [WARMUP]   Privacy [161..250) 89 rows — Privacy & Security
  [WARMUP] OK: 5 range(s) contiguous and non-empty, tiling [0..250).
  ```
- **Section renders in position** — on-screen heading order tail:
  Xbox -> Security -> System Services -> **Scheduled Tasks** -> Accessibility ->
  Visual Effects (Scheduled Tasks is index-adjacent after System Services, before
  Accessibility). 18 task toggles rendered (Gaming toggle-control count rose to 84).
- **ReadState reflects real schtasks output** — sampled against the live machine:
  - **present + disabled → toggle Off** (`ReadTaskEnabled` = false): CEIP Consolidator,
    MAR Backup, Windows Error Reporting Queue — all Off. ✓
  - **absent → toggle Off** (`ReadTaskEnabled` = null, schtasks exits non-zero):
    Microsoft Compatibility Appraiser, Office Actions Server, Windows AI Recall
    Configuration — all Off. ✓
  - **present + enabled → toggle On** (`ReadTaskEnabled` = true): NONE of the 18 rows
    is currently enabled on isleap's optimized machine (all present rows are already
    Disabled; several are Absent). To avoid a destructive change, the enabled branch
    (`exit 0` + `!Contains("Disabled")` → true → On) was validated read-only against a
    known-enabled NON-row task (`\Microsoft\Windows\Time Synchronization\
    SynchronizeTime` → true). The enabled→On UI case itself is a VM-checklist item.
  - **No task was enabled or disabled.** (The Defender scheduled-task candidate was
    deliberately not queried.)

### Landmine notes

- Polarity preserved exactly: `RecommendedState = false` (recommend task OFF),
  `DefaultState = true` (Windows default ON). Not inverted.
- No never-disable / gating logic in this section (curated telemetry/diagnostics/AI
  tasks; absent tasks read null). Two rows are Windows AI Recall
  (`\Microsoft\Windows\WindowsAI\Recall*`) — privacy/AI, NOT Defender. No task path
  targets Windows Defender; no Defender code referenced.
- `PlaybookTweaks.ScheduledTasks.cs` (the unrelated bulk Playbook surface) was not
  referenced or modified.

### VM checklist (Phase 11 — for isleap; DESTRUCTIVE, VM only)

- [ ] On a stock machine, a present + ENABLED task reads as toggle ON (the case not
      sampleable on the dev machine, where all 18 are already Disabled/Absent).
- [ ] Toggle a task OFF then ON — confirms `SetTaskEnabled` runs schtasks
      `/Change …/Disable|/Enable` and logs "<name>: disabled|enabled."
- [ ] An absent task (e.g. Compatibility Appraiser here) stays OFF and its apply is a
      harmless no-op / logged error, not a crash.
- [ ] Section-level bulk "Recommended" (disable all) / "Defaults" (enable all) act on
      these rows correctly.
- [ ] Guard still tiles `[0..250)` after any change (task writes don't alter row
      registration).

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced; no Defender task queried.
- **`PlaybookTweaks.ScheduledTasks.cs`** — untouched.
- **System Restore** — still a separate, not-yet-shown bespoke section for a future
  checkpoint. Not ported.

---

## MVVM Phase 12 — System Restore (SHOW ONLY)

Date: 2026-08-05

Show-first recon of the last unexamined Gaming section. **Nothing extracted, ported,
or edited** — build #3 code and build #2 both untouched; this log entry is the only
write. Awaiting isleap's confirmation before any code (same discipline as Phase 8/10).

### Source

- **File:** build #2 `Tabs/Gaming/GamingTab.SystemRestore.cs` (66 lines).
- **Method:** `BuildSystemRestore(StackPanel panel)`, called from `GamingTab.Build()`
  after `BuildScheduledTasks` (line 38). The **section title passed to AddSection is
  "System"** (not "System Restore" — the method name differs from the visible label).

### Kind of UI — TweakDefinition rows (registers), NOT bespoke

This is **2 `TweakDefinition` toggle rows**, rendered via
`AddSection(panel, "System", new[]{ ... })`. So — like System Services (Phase 9) and
Scheduled Tasks (Phase 11), and UNLIKE the Phase-7 preset card — these ARE
TweakDefinitions that register with `TweakRegistry` and belong INSIDE the
Mark/ClaimRange bracket. **It is NOT a bespoke restore-point list/grid** — there is
no per-restore-point UI, no sequence numbers/GUIDs, no create/delete/restore-to
actions, and no per-volume protection grid. Just two plain toggles. (Explicitly
confirmed, since this is exactly the distinction Phase 7 got wrong.)

Implication for a future extraction: porting it adds 2 registered rows — Gaming would
go `128 → 130`, overall `250 → 252`.

### Both rows, in order (2)

| # | Id | Name | Reads | Applies |
|---|---|---|---|---|
| 1 | `system-restore-protection` | System Protection (Restore Points) | `RPSessionInterval` DWORD under `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore` (>0 ⇒ on; absent ⇒ true) | `Enable-ComputerRestore`/`Disable-ComputerRestore -Drive "C:\"` via `powershell.exe` (through `TweakHelpers.RunCommand`) |
| 2 | `fs-long-paths` | Enable Long File Paths | `LongPathsEnabled` DWORD under `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem` (==1 ⇒ on) | writes that DWORD 1/0; `RequiresRestart` |

Both Ids are **unique** in build #2 (grep: each appears only in this one file — no
duplicate-registration risk on extraction). Note row 2 (`fs-long-paths`) is a
filesystem tweak bundled into this "System" section — not literally System Restore,
but it lives here in net8 and would move with the section.

### Representative rows (full shape)

**#1 `system-restore-protection`** (toggle, `IsPreference=true`, Recommended ON,
Default ON):
```
ReadState = () => {
    var v = ReadDword(HKLM, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", "RPSessionInterval");
    return v.HasValue ? v > 0 : true;          // absent ⇒ treated as on
}
Apply = on => {
    string verb = on ? "Enable" : "Disable";
    TweakHelpers.RunCommand("powershell.exe",
        "-NoProfile -ExecutionPolicy Bypass -Command " + verb + @"-ComputerRestore -Drive ""C:\""");
    Log("System Restore " + (on ? "enabled" : "disabled") + " for C:\\.");
}   // wrapped in try/catch → Log("ERROR System Restore: " + ex.Message)
```

**#2 `fs-long-paths`** (toggle, Recommended ON, Default OFF, `RequiresRestart=true`):
```
ReadState = () => (ReadDword(HKLM, @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled") ?? 0) == 1;
Apply = on => {
    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem",
        "LongPathsEnabled", on ? 1 : 0, RegistryValueKind.DWord);
    Log($"Long file paths {(on ? "enabled" : "disabled")}.");
}
```

### Landmine handling — ⚠ a destructive row that net8 does NOT guard

- **`system-restore-protection` OFF runs `Disable-ComputerRestore -Drive "C:\"`,
  which turns off System Protection AND deletes all existing restore points for C:.**
  This is genuinely destructive. **net8 attaches NO `Warning`/`WarningState` to this
  row** — there is no confirmation dialog; toggling it off applies immediately (the
  generic apply path only prompts when a `Warning` is set, and this def sets none).
  If/when this is extracted, isleap should decide whether to add a confirmation
  Warning (a behavior change from net8, so flagged now rather than done). Not a
  Defender concern.
- `fs-long-paths` is non-destructive (a single reversible DWORD; needs a restart).
- No hardware/OS-edition gating; both rows render unconditionally.
- **Defender:** none — no Defender path, service, or code referenced.

### Relationship to `RestorePointHelper.cs` — SEPARATE (checked, not assumed)

`GamingTab.SystemRestore.cs` does **not** reference `RestorePointHelper` (grep: 0
hits). Different concern:
- This section **toggles System Protection on/off for C:** (`Enable/Disable-
  ComputerRestore` + the `RPSessionInterval` read).
- `Tabs/Shared/RestorePointHelper.cs` (already in build #3) **creates a restore point
  before applying tweaks** (`EnsureRestorePointAsync` / `IsSystemRestoreEnabled`) —
  used by the bulk Quick-Actions "create restore point" path, not by this toggle.

**Consequence for extraction:** it would need NO wrapper over `RestorePointHelper`,
and no new shared logic — the two toggles are standard TweakDefinitions whose only
dependency is `TweakHelpers.RunCommand` (already present) + `Registry`/`ReadDword`
(present). Extraction would be trivial (a small `GamingTweaks.SystemRestore.cs`
catalog file returning the 2 defs, wired after Scheduled Tasks), aside from the
open question of whether to add a confirmation Warning to the destructive
protection-off row.

### Status

SHOW ONLY. No `GamingTweaks.SystemRestore.cs` created, `BuildCatalog` untouched,
`RestorePointHelper.cs` untouched, build #2 untouched. This was the last unexamined
Gaming section — after isleap confirms, extraction would complete the Gaming tab.

### ⟳ Re-checked (2026-08-05) — STILL SHOW ONLY / MISSING (not present in build #3)

isleap reported System Restore "already working"; a read-only re-check shows it is
**NOT** present — the net8 System Restore section was never extracted. Evidence:

- **Not a section anywhere.** `GamingViewModel.BuildCatalog()` yields 11 sections —
  Game Mode, Processor, Graphics, Storage, Network, Xbox, Security, System Services,
  Scheduled Tasks, Accessibility, Visual Effects — with **no "System Restore" / "System"**
  yield. The name appears only in code comments ("BuildSystemRestore … not-yet-shown").
- **No catalog file, no Ids.** There is no `GamingTweaks.SystemRestore.cs`, and the
  two net8 Ids **`system-restore-protection` and `fs-long-paths` are entirely absent
  from build #3** (repo-wide grep = 0 hits).
- **`RestorePointHelper.cs` IS referenced — but by a DIFFERENT feature.** It is used
  only by `ViewModels/Tweaks/TweakPageViewModel.cs` (the tab-level **Quick Actions ▸
  "Create restore point"** menu item, present on every tweak page) and by its own
  file. That "create a restore point before applying" action works — which is almost
  certainly what looked like "System Restore working" — but it is NOT the net8
  "System Restore" section, which instead toggles **System Protection on/off for C:**
  (`Enable/Disable-ComputerRestore`, Id `system-restore-protection`) and **Long File
  Paths** (Id `fs-long-paths`). Neither toggle exists in build #3.
- **Guard confirms.** Latest `[WARMUP]`: `Gaming [0..128) 128 rows` in **11 sections**;
  overall tiles `[0..401)`. If the 2 System Restore toggles were registered, Gaming
  would read 130 rows / 12 sections and overall `[0..403)`. It does not — so there is
  no System Restore range, exactly as expected for an un-ported section.

**Verdict: SHOW ONLY — still missing.** Extraction is still the open item to close
out Gaming (2 toggles; see the ⚠ landmine above — net8's protection-off row deletes
existing restore points with no confirmation dialog). No code created/edited in this
re-check.

---

## MVVM Phase 13 — tab rollout: Customize (18 catalog files) — **COMPLETE (VM sign-off pending)**

Date: 2026-08-05

The largest standard catalog-only tab. Same wave pattern as Sound/Notifications/
Update/Privacy — one VM + one page copy — flattening net8's 6-group Customize into
a single scrolling page of section cards in net8's Build() order. Build #2
read-only; no Defender code touched.

### Build status (literal)

VS 18 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:26.50
```

### Files

- **New:** `ViewModels/CustomizeViewModel.cs`, `Views/CustomizePage.xaml`
  (byte-for-byte copy of the wave page, `x:Class` only) + `.xaml.cs`.
- **Changed (wiring, additive):** `App.xaml.cs` — `CustomizeViewModel` singleton +
  warm-up enumeration entry; `MainWindow.xaml.cs` — `Customize → CustomizePage` in
  the route map and `SyncSelectedItem`.

### Section order = net8 CustomizeTab.Build(), flattened

net8's 6 top-level groups render in order Taskbar → Explorer → Context Menu →
Appearance → Start Menu → Desktop, each dispatching sub-sections. Reproduced as 20
section cards. **Two net8 sub-sections are APPENDED into their parent card** (net8
built them into the same StackPanel), so they are `Concat`-ed here to reproduce one
card each:
- Taskbar **"Behavior"** = `TaskbarBehavior` + `TaskbarBehaviorExtras`
  (net8 `BuildTaskbarBehavior` calls `BuildTaskbarBehaviorExtras(behaviorSection)`).
- Explorer **"View"** = `ExplorerView` + `ExplorerViewFolderOptions`
  (net8 `BuildExplorerView` calls `BuildExplorerViewFolderOptions(viewSection)`).

The 20 sections, in order: **Taskbar** → Layout, Behavior · **Explorer** → View,
Behavior, File Associations, Sidebar, This PC Folders · **Context Menu** → Entries ·
**Appearance** → Theme, Transparency & Effects, Color, Window Style · **Start Menu**
→ Layout, Behavior · **Desktop** → Desktop Icons, Shortcuts, Startup, Devices, Lock
Screen, Regional Settings.

### NOT ported — Taskbar ▸ "Button Grouping" (bespoke, out of scope)

net8's `BuildTaskbarGrouping` is NOT a catalog file and NOT TweakDefinitions — it
builds three hand-made ComboBoxes (Combine Taskbar Buttons / multi-monitor mode /
secondary-taskbar grouping) that do NOT register with TweakRegistry. It is the
Customize equivalent of Gaming's bespoke sections and belongs to a later checkpoint,
not this catalog rollout. (net8's nested Customize sub-navigation is likewise not
reproduced — this is the flat single-page wave pattern, as asked.)

### Id integrity vs build #2 — byte-identical (definitive parity proof)

Full tweak-Id extraction (`customize-*` / `region.*` / `os-*`, covering literals AND
generator tuple Ids) from the MVVM Customize catalog vs net8's:
- **145 unique tweak Ids each; `diff` EMPTY — IDENTICAL sets.**
- No duplicate Ids introduced (the `Concat` merges combine disjoint files).
- Every one of the 22 catalog section methods is called exactly once. Since the
  catalog is byte-identical to net8 and rendered by the identical methods, **each
  section's contents/counts match net8 by construction.**

### os-set-utc — present, Id preserved (NOT re-ported)

Confirmed already in the Phase-2-extracted catalog at
`CustomizeTweaks.Desktop.Regional.cs` → `RegionalSettings`, **Id `os-set-utc`**
("Set Clock to UTC"), relocated from net8's dead OSTweaks tab with the Id preserved.
It registers as part of the "Regional Settings" section and its row renders ("Set
Clock to UTC" found in the UI tree). Not re-ported — reused as-is.

### Verification (read-only, de-elevated + UIA; NO tweak actuated)

- **Guard tiles Customize `[250..398)` and overall `[0..398)`** (no navigation):
  ```
  [Gaming] 128 tweaks registered in 11 sections (registry total 128).
  [Sound] 5 tweaks registered in 1 sections (registry total 133).
  [Notifications] 16 tweaks registered in 5 sections (registry total 149).
  [Update] 12 tweaks registered in 3 sections (registry total 161).
  [Privacy] 89 tweaks registered in 13 sections (registry total 250).
  [Customize] 148 tweaks registered in 20 sections (registry total 398).
  [WARMUP]   Customize [250..398) 148 rows — Customize
  [WARMUP] OK: 6 range(s) contiguous and non-empty, tiling [0..398).
  ```
  **Customize = 148 registered rows in 20 sections** (the authoritative count).
- **Per-section counts:** the tweak-Id set is byte-identical to net8 (above), so
  per-section counts equal net8's. Static per-section tallies are imprecise for this
  catalog (5 of the 20 sections are generator-built: Context Menu ×17-row table,
  Desktop Icons ×6-loop, This PC Folders ×6-loop, Sidebar = 2 explicit + 6 nav-pane
  loop + 1 Libraries = 9, Regional = 6 dropdowns + os-set-utc), and the long page
  (53 sections' collapse prefs carried over from net8) defeats UIA row-counting
  (off-screen realized rows report empty rects). The guard's 148/20 total + the
  identical Id-set to net8 are the authoritative evidence. Group totals in net8
  order: Taskbar (Layout + Behavior[+Extras]), Explorer (View[+FolderOptions],
  Behavior, File Associations, Sidebar, This PC Folders), Context Menu (Entries),
  Appearance (Theme, Effects, Color, Window Style), Start Menu (Layout, Behavior),
  Desktop (Icons, Shortcuts, Startup, Devices, Lock Screen, Regional) = 148 rows.
- **First-keystroke search — PASS:** typed `t→ta→tas→task→taskb` char-by-char, then
  `clock`, `zzznomatch`, cleared — no crash on any keystroke (the known
  first-keystroke crash class, worth checking on a tab this large).
- App alive throughout; nothing actuated. **Test-harness note:** to attempt a
  per-section UIA count, the 53 carried-over `SectionCollapsed_*` UI prefs were
  temporarily cleared and then **restored** (backed up first) — these are UI
  preferences, not tweaks; isleap's saved collapse state is intact.

### VM checklist (Phase 13 — for isleap)

- [ ] Walk all 20 sections; spot-check toggles/dropdowns read correct state per
      group; the "Regional Settings" dropdowns + **os-set-utc** ("Set Clock to UTC")
      apply + read back.
- [ ] Context Menu "Entries" (17 add/remove shell verbs) render and detect presence.
- [ ] Watch for the known self-inflicted-drift pattern (FolderContentsInfoTip /
      ExtendedUIHoverTime) if exercising Explorer/Desktop rows.
- [ ] Section collapse persists across restart (note: repeated titles Layout/
      Behavior share a collapse key by title — same as net8).
- [ ] Per-tab search + Quick Actions behave as the other waves.
- [ ] The bespoke "Button Grouping" ComboBoxes are ABSENT (deferred, expected).

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Taskbar "Button Grouping"** (bespoke) and net8's nested Customize sub-nav — not
  ported; later checkpoint. Gaming ▸ System Restore also still pending (Phase 12
  show-only).

---

## MVVM Phase 14 — crimson accent theme tokens — **COMPLETE (visual sign-off pending on real hardware)**

Date: 2026-08-05

Every tab built so far rendered in stock Fluent blue; the Akari crimson (#E0142A)
accent was deferred since the Gaming spike. Closed at the **shell / App-resource
level** — a single file changed (`App.xaml`), **no per-page edits**. Build #2
untouched; no Defender code touched.

### Build status (literal)

VS 18 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:25.04
```

Launch-clean (de-elevated copy, no navigation): alive 10s — the new
`ThemeDictionaries` parse and every `{ThemeResource}` resolves (a bad key or
malformed dictionary would throw at `InitializeComponent`). Visual/Mica confirmation
is real-hardware only (can't screenshot accent/Mica in a VM) — see the description
below for what to check.

### Approach + the failure mode addressed

Framework check first: `WinUI.Framework.IThemeService` has **no accent-color API**
(only `CurrentTheme` / `RootElement` / `Initialize` / `ApplyTheme`), so there was no
existing hook to extend — the correct lever is App-level theme resources. This is
WinUI 3, so the WPF-UI `ApplicationAccentColorManager` doesn't apply, but its failure
mode does transfer: seeding a single accent color and letting Windows derive the
secondary/tertiary shades can be silently overridden by the OS palette (notably in
dark mode). Fix: set the **seed AND all six shade variants AND the concrete accent
brushes the app uses, explicitly, in BOTH theme dictionaries** — zero reliance on
derivation. Values mirror build #2's real-hardware-proven crimson set.

### File changed — `App.xaml` (ONLY)

Added a `<ResourceDictionary.ThemeDictionaries>` (Default = dark, Light = light) to
`Application.Resources`, declared directly on the root dictionary so it wins over the
merged `XamlControlsResources`. The theme toggle already flips `RequestedTheme` on
the shell root, which re-resolves these per theme. MergedDictionaries
(XamlControlsResources + TweakTemplates.xaml) unchanged.

**Resource keys set (both Default and Light unless noted):**

| Key | Before (stock Fluent) | After (crimson) |
|---|---|---|
| `SystemAccentColor` | Windows accent (user's, ~blue) | `#FFE0142A` |
| `SystemAccentColorLight1/2/3` | derived from OS | `#FFFF2438` / `#FFFF4150` / `#FFFF6373` |
| `SystemAccentColorDark1/2/3` | derived from OS | `#FFCC1226` / `#FFB3121F` / `#FF990F1B` |
| `AccentFillColorDefaultBrush` | stock blue | `#FFE0142A` |
| `AccentFillColorSecondaryBrush` | stock blue | `#E6E0142A` |
| `AccentFillColorTertiaryBrush` | stock blue | `#CCE0142A` |
| `TextOnAccentFillColorPrimaryBrush` | white | `#FFFFFFFF` (pinned) |
| `AccentTextFillColorPrimaryBrush` | stock light/dark blue | **dark:** `#FFFF8A94` · **light:** `#FFB01020` |
| `AccentTextFillColorSecondaryBrush` | stock | **dark:** `#FFE2808A` · **light:** `#FFB0424D` |
| `AccentTextFillColorTertiaryBrush` | stock | **dark:** `#FFE2808A` · **light:** `#FFB0424D` |
| `AccentFillColorSelectedTextBackgroundBrush` | stock blue wash | **dark:** `#33E0142A` · **light:** `#26E0142A` |
| `ToggleSwitchFillOn` | stock blue | `#FFE0142A` |
| `ToggleSwitchFillOnPointerOver` | stock | `#FFFF2438` |
| `ToggleSwitchFillOnPressed` | stock | `#FFCC1226` |
| `ToggleSwitchStrokeOn` | stock | `#FFE0142A` |
| `TextControlBorderBrushFocused` | stock blue | `#FFE0142A` |
| `NavigationViewSelectionIndicatorForeground` | stock blue | `#FFE0142A` |

The accent-TEXT brushes are the only theme-variant values (lightened on dark,
darkened on light) for legibility on the page background — a full `#E0142A` reads too
dark on a dark surface and too light on a white one. Everything else is
theme-invariant crimson (as in build #2).

### Why this covers the whole app with no per-page edits

The build-3 UI references exactly three accent keys —
`AccentTextFillColorPrimaryBrush` (×56: the "TOOL" wordmark, Recommended pill text,
Quick-actions glyph, card/section accents), `AccentFillColorDefaultBrush` (×32: title
dot, badge borders, ⊞ default-icon squares), and
`AccentFillColorSelectedTextBackgroundBrush` (×6: Recommended/bulk pill backgrounds)
— all now crimson. Native controls (NavigationView selection bar, ToggleSwitch
on-fill, AutoSuggestBox/TextBox focus ring, AccentButton) derive from
`SystemAccentColor` + the fill/toggle/nav/focus brushes, all pinned above. So the
override is genuinely shell-level; **no page, template, or code file needed editing**
(scope check from the brief: passed — this did NOT require touching per-page
hardcoded colors).

### What isleap should confirm visually (real hardware, both themes)

- Rail: selected nav item's indicator bar is crimson (not blue).
- Title bar: the "TOOL" half of the wordmark and the accent dot are crimson.
- A tweak tab: Recommended badge pill (crimson text + border + faint crimson wash),
  the ★ recommended quick-set stays gold, ⊞ default squares are crimson; toggles
  switched ON show a crimson track.
- Search box / any TextBox: focus ring is crimson.
- Flip dark ⇄ light with the title-bar toggle: accent stays crimson in both, and the
  accent TEXT stays legible (lighter crimson on dark, deeper crimson on light).
- Mica backdrop unaffected (accent tokens don't touch it).

### Not touched

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **No per-page / template / code changes** — `App.xaml` is the only file changed for
  this phase (Part A of this run, System Restore, was SHOW-ONLY and logged as
  Phase 12).

### Addendum (isleap correction) — ⊞ Windows-logo mark stays Windows blue

The crimson accent override also recolored the ⊞ "Windows default" mark (the 2×2
square logo on the ⊞ quick-set button and the section "Defaults" bulk button),
because those squares used `AccentFillColorDefaultBrush`. The Windows logo must keep
the Windows-brand blue, not the Akari accent.

Fix (shared rendering layer, no per-page edits): added
`<SolidColorBrush x:Key="AkariWindowsLogoBrush" Color="#FF0078D4" />` to
`Views/Templates/TweakTemplates.xaml`, and repointed the **12** logo `Rectangle`
fills (3 marks × 4 squares: toggle-row ⊞, dropdown-row ⊞, section "Defaults" ⊞) from
`{ThemeResource AccentFillColorDefaultBrush}` to `{StaticResource AkariWindowsLogoBrush}`.
Theme-invariant (#0078D4 in both light and dark). The ★ recommended mark stays gold
(`SystemFillColorCautionBrush`); badge/pill borders stay crimson
(`AccentFillColorDefaultBrush`, unchanged). Rebuild: **0 errors, 0 warnings.**

---

## MVVM Phase 16 — Taskbar Button Grouping (SHOW ONLY)

Date: 2026-08-05

Show-first recon of the bespoke Customize ▸ Taskbar ▸ Button Grouping UI that Phase
13 deliberately left out of the catalog extraction. **Nothing extracted, ported, or
edited** — build #2 and build #3 both untouched; this log entry is the only write.
Awaiting isleap's confirmation before any code.

### Source

- **File:** build #2 `Tabs/Customize/CustomizeTab.Taskbar.Grouping.cs` (143 lines).
- **Method:** `BuildTaskbarGrouping(StackPanel panel)`. Builds ONE section card
  titled **"Button Grouping"** (`TweakHelpers.BuildSection(panel, "Button Grouping")`)
  containing three `ComboBox`es, each preceded by a title + description `TextBlock`.
- **Not TweakDefinitions:** raw `ComboBox` controls added directly to the section
  panel — no `AddTweakRow`, no `TweakDefinition`, **no Id**, so they never register
  with `TweakRegistry` (never in Backup/Restore or global search). Confirmed: grep
  for any Id in the file = none.

### The three ComboBoxes — what each controls (verified from source)

All three write `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced`
DWORDs where **the value written IS the ComboBox SelectedIndex (0/1/2)**, then call
`ExplorerRestart.Request()` (gated by `!_suppressRestart`).

| # | Label | Options (index → meaning) | Registry value (DWORD) | Read via |
|---|---|---|---|---|
| 1 | **Combine Taskbar Buttons** | 0 Always combine · 1 Combine when taskbar is full · 2 Never combine | `TaskbarGlomLevel` | `SystemStateReader.ReadCombineTaskbarButtons()` (reads `TaskbarGlomLevel`) |
| 2 | **Show Taskbar Apps On** (multi-monitor) | 0 All taskbars · 1 Main taskbar and taskbar where window is open · 2 Taskbar where window is open | `MMTaskbarMode` | `ReadDwordCu(…Advanced, "MMTaskbarMode")` |
| 3 | **Combine Buttons on Other Taskbars** (secondary monitors) | 0 Always combine · 1 Combine when taskbar is full · 2 Never combine | `MMTaskbarGlomLevel` | `ReadDwordCu(…Advanced, "MMTaskbarGlomLevel")` |

Notes: the value is `SelectedIndex` directly (clamped to 0..2 on read via
`Math.Min(v,2)`, defaulting to index 0 when the value is absent). Write is
`SetHkcu(…Advanced, <valueName>, SelectedIndex)`. Each also logs
`[TASKBAR] …: {SelectedItem}`. (The task-prompt's "Always combine, hide labels"
phrasing is the old Win10 wording; net8 uses the labels in the table above with
the `TaskbarGlomLevel` key — confirmed from source, not memory.)

### Why hand-made — and can they be plain TweakDefinition dropdowns?

**They CAN be expressed as three ordinary dropdown `TweakDefinition`s — no bespoke
ViewModel/DataTemplate is needed.** Each is an INDEPENDENT selector with:
no interdependency between the three, no live preview, no non-registry mechanism —
just read a DWORD (index == value), write it back, restart Explorer. That is exactly
the `TweakDefinition { InputKind = Dropdown, Options = [Value 0,1,2],
ReadCurrentIndex, ApplyIndex }` shape already used by e.g. the Sound ducking and
Update policy dropdowns.

Why they were hand-made in net8 (best read of the evidence, not a functional need):
- a minor **layout** preference — net8 stacks title / description / full-width-300
  left-aligned combo, whereas the standard tweak-row renders Name + Description with
  the dropdown on the right;
- **no Recommended/Default badges** (a plain ComboBox) — trivially matched by a
  dropdown `TweakDefinition` whose Options set neither `IsRecommended` nor
  `IsDefault`;
- likely **historical** — the two multi-monitor combos are a "Winhance port" added
  ad hoc, and the combine one predates/sits outside the dropdown-TweakDefinition
  convention.

**Recommendation:** extraction should follow the **System Services / Scheduled Tasks
CATALOG pattern** (3 dropdown `TweakDefinition`s in a `TaskbarButtonGrouping(Action
<string> Log)` catalog method), **NOT** the removed Phase-7 bespoke-preset-card
approach. It is a standard-dropdown conversion, not bespoke work.

### Placement (to preserve)

`BuildTaskbar` (net8 `CustomizeTab.Taskbar.cs`) dispatches in order:
`BuildTaskbarLayout → BuildTaskbarBehavior → BuildTaskbarGrouping`. So Button
Grouping is the **3rd and last Taskbar sub-section, after the already-ported "Layout"
and "Behavior" cards**. In the MVVM flat Customize page it slots as a "Button
Grouping" section immediately after Taskbar "Behavior" and before the Explorer group
(i.e. inserted into `CustomizeViewModel.BuildCatalog()` between the Taskbar
"Behavior" yield and the Explorer "View" yield).

### Extraction specifics (for isleap's decision — NOT done here)

- **No new logic needed.** The helpers are already in build #3:
  `ReadDwordCu` and `SetHkcu` are static in `Tabs/Customize/Catalog/CustomizeTweaks.cs`
  (hoisted in Phase 2); `SystemStateReader.ReadCombineTaskbarButtons()` is ported;
  `ExplorerRestart` is ported. A catalog method + 3 dropdown defs is all that's new.
- **3 new Ids must be MINTED.** net8 assigns none (these were never TweakDefinitions),
  so there is no build-#2 Id to preserve byte-for-byte — unlike System Services /
  Scheduled Tasks. Because build #2 never registered them in `TweakRegistry`, they
  were **never in any Backup/Restore export**, so adding them as tweaks is a net
  feature ADDITION (first-time Backup/search coverage), not a compatibility risk.
  isleap picks the Id strings (e.g. `customize-taskbar-combine-buttons`,
  `customize-taskbar-multimon-mode`, `customize-taskbar-multimon-combine`).
- **Count impact:** +3 registered rows → Customize 148 → 151, overall 398 → 401.

### Landmine handling

- **Cosmetic only.** Writes three `HKCU\…\Explorer\Advanced` DWORDs and restarts
  Explorer (coalesced via `ExplorerRestart`, suppressed during bulk). No boot,
  security, or service impact.
- The two multi-monitor settings (`MMTaskbarMode`, `MMTaskbarGlomLevel`) only have
  a visible effect with 2+ displays; harmless no-ops on a single monitor.
- **No warning/confirmation dialogs in net8** (grep: none) — plain apply-on-change.
- **No Defender references** anywhere in the file (grep: none).

### Status

SHOW ONLY. No build #3 files created/edited, `CustomizeViewModel.BuildCatalog()`
untouched, the Customize catalog untouched, build #2 untouched. Still-pending
not-yet-extracted items after this: **Gaming ▸ System Restore** (Phase 12 show-only)
and this **Taskbar Button Grouping**; both are convertible to standard
TweakDefinition rows (no bespoke rendering needed).

---

## MVVM Phase 17 — Taskbar Button Grouping extracted (3 dropdowns) — **COMPLETE (VM sign-off pending)**

Date: 2026-08-05

net8's Taskbar ▸ Button Grouping (3 hand-made ComboBoxes, never TweakDefinitions —
confirmed in the Phase-16 recon) converted to 3 standard dropdown TweakDefinitions,
following the System Services / Scheduled Tasks catalog pattern (NOT bespoke
ViewModel/DataTemplate). Wired into Customize as the last Taskbar sub-section. Build
#2 read-only; no Defender code touched.

### Build status (literal)

VS 18 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:26.32
```

### Files

- **New: `Tabs/Customize/Catalog/CustomizeTweaks.Taskbar.Grouping.cs`** — a
  `TaskbarButtonGrouping(Action<string> Log)` method returning 3 dropdown
  `TweakDefinition`s. Read/write logic is REUSED, not reimplemented:
  `SystemStateReader.ReadCombineTaskbarButtons()` (dropdown 1), the already-ported
  static `ReadDwordCu` / `SetHkcu`, and `ExplorerRestart`. Apply writes the DWORD
  then calls `ExplorerRestart.Request()` gated by `!_suppressRestart` — exactly as
  net8 did. Only the TweakDefinition dropdown wrapper is new.
- **Changed: `ViewModels/CustomizeViewModel.cs`** — added
  `("Button Grouping", CustomizeTweaks.TaskbarButtonGrouping(log))` to
  `BuildCatalog()` immediately after the Taskbar "Behavior" yield (net8 order
  Layout → Behavior → Grouping; last Taskbar sub-section, before the Explorer
  group). Stale "NOT PORTED" class comment updated.

### The 3 new Ids (minted; collision-checked against the full Customize set — none)

| Id | Name | Registry value (`HKCU\…\Explorer\Advanced`) |
|---|---|---|
| `customize-taskbar-button-grouping` | Combine Taskbar Buttons | `TaskbarGlomLevel` |
| `customize-taskbar-grouping-multimonitor` | Show Taskbar Apps On | `MMTaskbarMode` |
| `customize-taskbar-grouping-other-taskbars` | Combine Buttons on Other Taskbars | `MMTaskbarGlomLevel` |

net8 assigned NO Ids to these (they were never TweakDefinitions / never in
TweakRegistry), so there is nothing to preserve byte-for-byte and they were never in
any Backup export — adding them is first-time Backup/search coverage. Names follow
the existing `customize-taskbar-*` convention; each is unique (verified). Each has 3
options with values 0/1/2 (value == SelectedIndex, matching net8).

### ⟳ RESOLVED (isleap, post-Phase-17) — bulk must not overwrite a taste choice

The initial Phase-17 rows marked index 0 with `IsRecommended: true` + `IsDefault:
true`, so bulk "Apply recommended" reset them to the Windows default. During review
the mechanism was checked: **`IsPreference` is badge-only — it is referenced only in
`TweakDefinition.cs` (the "Preference" pill) and does NOT gate bulk.** The bulk
engine (`TweakTargets.TryGetRecommendedTarget` / `TryGetDefaultTarget`) keys purely
off `IsRecommended` / `IsDefault` on the options. So to stop bulk "Recommended" from
overwriting a deliberate taste choice, the fix is to DROP `IsRecommended`, not to add
`IsPreference`.

**Decision (isleap): "Protect from Recommended only"** — matching the existing
Customize cosmetic-dropdown convention (Taskbar Transparency / Button Size). All
three rows now carry `IsPreference = true` with the index-0 (Windows-default) option
marked `IsDefault: true` but **NOT** `IsRecommended`. Effect:
- bulk **"Apply recommended"** SKIPS these rows (no `IsRecommended` option) → a
  user's deliberate grouping choice is never silently overwritten;
- bulk **"Reset to Windows defaults"** still resets them to index 0;
- badges show **Preference + Windows Default** (+ **Custom** when the user picks a
  non-default option).

`IsDefault` (DefaultState) unchanged. Rebuilt clean (0 warnings / 0 errors); guard
re-verified: **Customize still 151 rows / 21 sections, tiling `[0..401)`** — a flag
change, count-neutral. (Supersedes the "RecommendedState = DefaultState" wording
under "Windows DEFAULT values" below.)

### Windows DEFAULT values + Recommended/Default reasoning (flagged judgment call)

**Actual clean-install default = index 0 for all three** — NOT guessed:
- net8's own reader defaults an ABSENT value to index 0
  (`SelectedIndex = HasValue ? Math.Min(v,2) : 0`) for each of the three combos —
  that is net8's encoding of the clean-install default.
- Meanings: `TaskbarGlomLevel` 0 = **Always combine**, `MMTaskbarMode` 0 =
  **All taskbars**, `MMTaskbarGlomLevel` 0 = **Always combine** — the Windows 11
  fresh-profile behaviour (all three values are absent by default).
- Confirmed on this dev machine: all three values are **absent** → each dropdown
  reads back index 0 (see verification).

**Decision — `RecommendedState = DefaultState` (index 0), as instructed.** These are
pure cosmetic preferences with no objectively-correct answer, so per the brief the
recommended option IS the Windows default (index 0) — no directional nudge. Concretely
the index-0 option carries `IsRecommended: true, IsDefault: true`; options 1/2 are
plain; `IsPreference` is NOT set (we are making a definite "leave at Windows default"
recommendation rather than a no-answer preference). No evidence net8 treated any as
recommended-different (net8's raw ComboBoxes carry no recommendation at all).
**Consequence, flagged:** the section's "Recommended" AND "Defaults" bulk bars both
target index 0 for these rows — a user who set e.g. "Never combine" would be reset to
"Always combine" by either bulk action. That is the direct result of
Recommended = Default = Windows-default and is the instructed behaviour.

### Verification (read-only, de-elevated + UIA; NOTHING applied)

- **Guard tiles Customize `[250..401)` and overall `[0..401)`** (148 → 151; Customize
  is 21 sections now):
  ```
  [Customize] 151 tweaks registered in 21 sections (registry total 401).
  [WARMUP]   Customize [250..401) 151 rows — Customize
  [WARMUP] OK: 6 range(s) contiguous and non-empty, tiling [0..401).
  ```
- **Section renders in position** — "Button Grouping" section heading present, with
  3 ComboBoxes; placed after Taskbar "Behavior" (guaranteed by BuildCatalog order).
- **ReadCurrentIndex reflects this machine's real registry (read-only):** all three
  values are ABSENT on this machine → each dropdown shows its index-0 option:
  | Row | registry | UI selection |
  |---|---|---|
  | Combine Taskbar Buttons | TaskbarGlomLevel (absent) | Always combine ✓ |
  | Show Taskbar Apps On | MMTaskbarMode (absent) | All taskbars ✓ |
  | Combine Buttons on Other Taskbars | MMTaskbarGlomLevel (absent) | Always combine ✓ |
- **No taskbar grouping was changed.** App alive throughout.

### Landmine handling

- Cosmetic only — three `HKCU\…\Explorer\Advanced` DWORDs + Explorer restart
  (coalesced via `ExplorerRestart`, suppressed during bulk). No boot/security impact.
- The two multi-monitor rows (`MMTaskbarMode`, `MMTaskbarGlomLevel`) only have a
  visible effect with 2+ displays; harmless no-ops on single-monitor.
- No warning/confirmation dialogs (net8 had none); no Defender references.

### VM checklist (Phase 17 — for isleap)

- [ ] Change "Combine Taskbar Buttons" to "Never combine" then back — confirms
      `ApplyIndex` writes `TaskbarGlomLevel` and Explorer restarts once.
- [ ] The two multi-monitor rows apply + read back (test on a multi-monitor rig for a
      visible effect; single-monitor still writes the values correctly).
- [ ] Section "Recommended"/"Defaults" bulk both drive these to index 0 (expected,
      per the decision above) — confirm that is acceptable.
- [ ] Guard still tiles `[0..401)` after any change (writes don't alter registration).

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Gaming ▸ System Restore** (Phase 12, still show-only/unconfirmed) — the only
  remaining open item before Gaming + Customize are fully closed out. Not touched.

---

## MVVM Phase 18 — System Restore extraction prep (REPORT ONLY — pre-decision)

Date: 2026-08-05

Pre-extraction recon of Gaming ▸ System Restore's `system-restore-protection` toggle,
so isleap can choose "port verbatim (no dialog)" vs "port + confirmation on the
disable path". **Nothing extracted or edited** — build #2 and build #3 untouched;
this log entry is the only write.

### 1. Exact net8 text + shape (`GamingTab.SystemRestore.cs`, verbatim)

- **Id:** `system-restore-protection`
- **Name:** `System Protection (Restore Points)`
- **Description:** `Allow Windows to automatically create restore points for the C: drive`
- `IsPreference = true`, `RecommendedState = true`, `DefaultState = true`.
- **Read:** `RPSessionInterval` DWORD at
  `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore` (`> 0` ⇒ on;
  absent ⇒ on/true).
- **Apply:** `verb = on ? "Enable" : "Disable"`; runs
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command <verb>-ComputerRestore -Drive "C:\"`
  via `TweakHelpers.RunCommand`, logs `System Restore enabled|disabled for C:\`.
  **The def has NO `Warning` / `WarningState` field.**

### 2. OFF path + destructiveness — confirmed from source AND Microsoft behavior

- **From source:** toggling OFF runs `Disable-ComputerRestore -Drive "C:\"` (the
  `verb` is "Disable" when `on == false`), with **no confirmation** — the def sets no
  `Warning`, so the ported per-row confirm path is a no-op for it.
- **Documented mechanism (not just restated):** `Disable-ComputerRestore` turns off
  System Protection monitoring for the drive; **turning System Protection off on a
  volume deletes all existing restore points on that volume.** This is standard,
  documented Windows behavior — the System Protection control-panel UI's own "Turn
  off system protection" option displays *"…will delete all restore points on this
  drive"* and requires a Yes/No confirmation before proceeding. So net8's one-click
  toggle-off performs the destructive action while **skipping the confirmation
  Windows itself normally shows**.
- **Risk-surface nuance (matters for the decision):** because `RecommendedState = true`
  AND `DefaultState = true`, BOTH bulk actions ("Apply recommended" and "Reset to
  Windows defaults") drive this toggle **ON** (enable protection) — neither bulk ever
  disables it. The destructive `Disable-ComputerRestore` is therefore reachable ONLY
  via a **deliberate manual toggle-off** (or a settings import carrying it OFF). A
  per-row OFF-only warning would fire on exactly that one path and nowhere else.

### 3. Confirmation-dialog plumbing — ALREADY WIRED, reuse is one field

No new plumbing needed. The ported rendering layer already implements per-row
confirmation:
- `TweakDefinition` has **`Warning`** (string) + **`WarningState`** (bool?):
  `GetToggleWarning(target)` returns the warning when `WarningState == null` (any
  change), `false` (only OFF), or `true` (only ON).
- `ViewModels/Tweaks/ToggleTweakViewModel.cs` (`OnUserToggledAsync`) already does
  `if (!await Dialogs.ConfirmWarningAsync(Name, Definition.GetToggleWarning(newState)))
  { revert; return; }` — a warned toggle shows an OK/Cancel `ContentDialog` before
  applying, and Cancel **reverts the switch without writing**. The quick-set (★/⊞)
  path awaits the same confirm.
- `Services/TweakDialogs.cs` `ConfirmWarningAsync` is the wired primitive (returns
  true when there's no warning or the user confirms; treats a missing XamlRoot as
  declined — fail-safe).
- **Existing examples in the ported catalogs:** `GamingTweaks.Processor.cs` (1) and —
  the ideal analog — **`NotificationsTweaks.cs` `explorer-action-center` ("Disable
  Action Center")**, which uses `Warning = "…Continue?"` + `WarningState = false`
  (warn only when switching OFF). (`GamingTweaks.Security.cs` also has one, on the
  inert Defender row — noted only; not to be touched or used as a reference.)

**Consequence:** adding a confirmation to the disable-protection path is a **UI-only,
one-field change** — set `Warning = "…"` + `WarningState = false` on the extracted
`system-restore-protection` def. Id, registry read, and the
`Enable/Disable-ComputerRestore` apply logic stay byte-identical either way. It does
NOT touch the bulk / Quick-Actions confirmation flow (that coarser bulk dialog is a
separate path and, per §2, never disables this toggle anyway).

### Decision for isleap (STOP here — not extracted)

- **A) Port verbatim** — no `Warning`; one-click OFF runs `Disable-ComputerRestore`
  with no dialog, exactly as net8 does today.
- **B) Port + confirmation on disable only** — add `Warning` + `WarningState = false`
  (fires solely on the manual toggle-off, the only destructive path); restores the
  Yes/No gate that Windows' own System Protection UI shows. Ids/registry/apply
  unchanged; UI-only.

Either way `fs-long-paths` (the other System Restore toggle) is ported verbatim —
it has no destructive landmine.

### Not touched

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — not referenced (the Security.cs warning example was noted, not read
  into or reused).
- **No extraction** — no `GamingTweaks.SystemRestore.cs`, no `BuildCatalog` change.

---

## MVVM Phase 19 — Gaming ▸ System Restore extracted (2 toggles; closes out Gaming) — **COMPLETE (VM sign-off pending)**

Date: 2026-08-05

The last unported Gaming section. Ported verbatim EXCEPT the one deliberate
behavioral addition isleap chose in Phase 18 (option B): a confirm-on-disable
Warning on `system-restore-protection`, reusing the existing `Warning`/`WarningState`
plumbing. This **completes Gaming & Performance**. Build #2 read-only; no Defender
code touched.

### Build status (literal)

VS 18 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:24.75
```

### Files

- **New: `Tabs/Gaming/Catalog/GamingTweaks.SystemRestore.cs`** — a
  `SystemRestore(Action<string> Log)` method returning 2 toggle `TweakDefinition`s.
  Reuses the existing static `GamingTweaks.ReadDword` + `TweakHelpers.RunCommand`.
- **Changed: `ViewModels/GamingViewModel.cs`** — added
  `("System Restore", GamingTweaks.SystemRestore(log))` to `BuildCatalog()` between
  the Scheduled Tasks and Accessibility yields; class comment updated to "FULLY
  PORTED".

### The 2 Ids + the Warning text

| Id | Name | Read / Apply |
|---|---|---|
| `system-restore-protection` | System Protection (Restore Points) | Read `RPSessionInterval` DWORD @ `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore` (>0 or absent ⇒ on); Apply shells `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command Enable\|Disable-ComputerRestore -Drive "C:\"` via `TweakHelpers.RunCommand` |
| `fs-long-paths` | Enable Long File Paths | Read/write `LongPathsEnabled` DWORD @ `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem` (==1 ⇒ on); `RequiresRestart` |

**Id diff vs build #2: IDENTICAL** (2 Ids, same order). Both unique in build #3.

**Exact Warning text added to `system-restore-protection`** (the only behavioral
change; Id / registry / Enable/Disable-ComputerRestore apply logic otherwise
byte-identical to net8):

> `Turning off System Protection will delete all existing restore points on this drive. Continue?`

with `WarningState = false`. House style matches the existing
`explorer-action-center` warning (ends with "Continue?", WarningState = false = warn
only on OFF). `fs-long-paths` is verbatim — no Warning.

### Warning wiring — confirmed by code review (NOT clicked)

The destructive path is NOT exercised here (read-only; that's isleap's VM step).
Verified from source that the plumbing fires correctly for `WarningState = false`:
- `TweakDefinition.GetToggleWarning(target)` returns the Warning only when
  `target == WarningState` (here `false` = OFF); a toggle-ON returns `null`.
- `ToggleTweakViewModel.OnUserToggledAsync(newState)` does
  `if (!await Dialogs.ConfirmWarningAsync(Name, Definition.GetToggleWarning(newState)))
  { SetSilently(!newState); return; }` before `ApplyToggle` — so a manual OFF shows
  the OK/Cancel `ContentDialog`, and **Cancel reverts the switch without running
  `Disable-ComputerRestore`**; a manual ON applies with no dialog.
- This is byte-for-byte the same path the ported `explorer-action-center` uses.

### Placement

net8 `GamingTab.Build()` order: … Xbox → Security → **System Services → Scheduled
Tasks → System Restore** → Accessibility → Visual Effects. So System Restore is the
last of the three formerly-bespoke sections, after Scheduled Tasks and before
Accessibility. Reproduced exactly. (net8's `AddSection` titled the card **"System"**;
wired as the clearer **"System Restore"** per isleap — a card label only, not an Id,
no Backup/Restore impact.)

### Verification (read-only, de-elevated + UIA; NOTHING toggled)

- **Guard tiles Gaming `[0..130)` and overall `[0..403)`** (128 → 130; Gaming is 12
  sections now):
  ```
  [Gaming] 130 tweaks registered in 12 sections (registry total 130).
  [WARMUP]   Gaming [0..130) 130 rows — Gaming & Performance
  … Customize [252..403) 151 rows …
  [WARMUP] OK: 6 range(s) contiguous and non-empty, tiling [0..403).
  ```
- **Section renders in position** — on-screen tail order: Scheduled Tasks →
  **System Restore** → Accessibility → Visual Effects (index-adjacent to both
  neighbours). 2 toggles present.
- **ReadState reflects this machine's real registry (read-only):**
  | Row | registry | UI toggle |
  |---|---|---|
  | System Protection (Restore Points) | RPSessionInterval = 1 (>0) | On ✓ |
  | Enable Long File Paths | LongPathsEnabled = 1 | On ✓ |
- **No row was toggled.** Confirming the warning dialog actually appears on a manual
  OFF is isleap's VM step (the read-only harness must not exercise the destructive
  `Disable-ComputerRestore` path).

### ✅ Gaming & Performance — FULLY PORTED (dead-code map re-checked)

net8 `GamingTab.Build()` renders exactly **12** sections; MVVM `BuildCatalog()` now
yields **12**. The only bespoke Gaming section methods in build #2
(`BuildSystemServices`, `BuildScheduledTasks`, `BuildSystemRestore`) are all ported
(Phases 9 / 11 / 19). **No Gaming section remains unported**, and CLAUDE.md's
dead-code map contains no Gaming entry. Gaming is closed out.

### VM checklist (Phase 19 — for isleap; DESTRUCTIVE, VM only)

- [ ] Toggle `System Protection (Restore Points)` **OFF** → the confirmation dialog
      ("…will delete all existing restore points…Continue?") MUST appear; **Cancel**
      leaves it ON with no registry/PowerShell change; **OK** runs
      `Disable-ComputerRestore -Drive "C:\"`.
- [ ] Toggle it back **ON** → NO dialog (WarningState = false); runs
      `Enable-ComputerRestore`.
- [ ] `Enable Long File Paths` toggles with no dialog; writes `LongPathsEnabled`;
      note it needs a restart.
- [ ] Guard still tiles `[0..403)` after either change (writes don't alter
      registration).

### Not touched

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **`fs-long-paths`** — verbatim; no behavior change.

---

## MVVM Phase 20 — tab rollout: Power (+ PowerSchemeChanged subscriber) — **COMPLETE (VM sign-off pending)**

Date: 2026-08-05

Power rolled out as a standard catalog-only tab (the 15 powercfg tweak-row sections),
plus a READ-ONLY subscriber for `PowerTweaks.PowerSchemeChanged`. Build #2 read-only;
no Defender code touched.

### Pre-flight confirmations (asked before wiring)

**1. Phase 14 (crimson theme tokens) — EXECUTED and still in place.** `App.xaml`
carries the `ResourceDictionary.ThemeDictionaries` crimson override from Phase 14
(`SystemAccentColor` `#FFE0142A` + all six shade variants + the accent fill/text/
toggle/nav/focus brushes, both Default and Light). Confirmed present; not touched
this task. (The ⊞ Windows-logo blue `AkariWindowsLogoBrush #FF0078D4` from the
Phase-14 addendum is also still in `TweakTemplates.xaml`.) Not re-done — it stays its
own item.

**2. `PowerTweaks.PowerSchemeChanged` — signature, raise sites, subscribers.**
- **Signature:** `public static event Action? PowerSchemeChanged;`
  (`Tabs/Power/Catalog/PowerTweaks.cs`).
- **Raised from (WRITE path only):** `EnsureAkariScheme()` line 124 (after
  `/duplicatescheme` + `/changename` + `/setactive` create the Akari scheme) and
  `SetPowerCfg()` line 213 (after `/SETACVALUEINDEX` + `/SETDCVALUEINDEX` +
  `/SETACTIVE` + `ClearSchemeDrift()`). **Never raised from a read path.**
- **Subscribers before this phase:** NONE (dangling no-op).
- Invariant re-verified: the new handler stays READ-ONLY and NEVER calls back into
  `SetPowerCfg` / `EnsureAkariScheme` / `powercfg /SETACTIVE` (see the subscriber
  below).

### Build status (literal)

VS 18 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:24.28
```

### Files

- **New:** `ViewModels/PowerViewModel.cs`, `Views/PowerPage.xaml` (byte-for-byte copy
  of the wave page, `x:Class` only) + `.xaml.cs`.
- **Changed (wiring, additive):** `App.xaml.cs` — `PowerViewModel` singleton +
  warm-up enumeration entry; `MainWindow.xaml.cs` — `Power → PowerPage` route +
  `SyncSelectedItem`.

### Section order = net8 PowerTab.Build() catalog order (15 sections)

Display → Hard Disk → Internet Explorer → Desktop Background Settings → Wireless
Adapter Settings → Sleep → Battery → USB Settings → PCI Express → GPU Power →
Processor Power Management → Processor Advanced Settings → Multimedia Settings →
Power Buttons and Lid → Start Menu Power Options.

**NOT ported (bespoke, deferred — like Gaming's former bespoke sections):** net8's
**Plan Selector** (plan cards) and **Persist Indicator** that render ABOVE the
catalog sections (`PowerTab.PlanSelector.cs` / `PowerTab.Persistence.cs`) are
hand-built UI, not catalog TweakDefinitions, and are out of scope for this
catalog-only rollout. Flagged; their repaint hooks into the same subscriber when
they land.

### Per-section counts (registered on this machine + full data-layer)

Battery + GPU Power are hardware-gated inside their catalog methods
(`Array.Empty<>()` when `GetSystemPowerStatus` reports no battery / no vendor-GPU
`powercfg` subgroup). On isleap's battery-less, no-vendor-GPU desktop both return 0
and their sections drop out — **13 sections / 36 rows** register here; the full data
layer is **47** (the CLAUDE.md 47-vs-36 figure — do NOT "fix").

| Section | Registered (this machine) | Data-layer |
|---|---|---|
| Display | 1 | 1 |
| Hard Disk | 1 | 1 |
| Internet Explorer | 1 | 1 |
| Desktop Background Settings | 1 | 1 |
| Wireless Adapter Settings | 1 | 1 |
| Sleep | 6 | 6 |
| Battery | **0 (gated)** | 7 |
| USB Settings | 3 | 3 |
| PCI Express | 1 | 1 |
| GPU Power | **0 (gated)** | 4 |
| Processor Power Management | 5 | 5 |
| Processor Advanced Settings | 7 | 7 |
| Multimedia Settings | 3 | 3 |
| Power Buttons and Lid | 3 | 3 |
| Start Menu Power Options | 3 | 3 |
| **Total** | **36** | **47** |

(36 + Battery 7 + GPU 4 = 47 — reconciles exactly with the guard and the Phase-2
gating note.)

### Verification (read-only, de-elevated + UIA; NO power scheme changed)

- **Guard tiles Power `[403..439)` and overall `[0..439)`** (7 pages now):
  ```
  [Power] 36 tweaks registered in 13 sections (registry total 439).
  [WARMUP]   Power [403..439) 36 rows — Power
  [WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
  ```
- **Section renders** — sampled headings present (Display, Sleep, USB Settings,
  Processor Advanced Settings, Start Menu Power Options); **Battery and GPU Power
  correctly ABSENT** (gated on this hardware); 30 ComboBoxes on the page.
- **No power scheme was changed; no `powercfg /SETACTIVE` was run** in verification.
- The `PowerSchemeChanged` subscriber **compiles and is wired** (subscribed in the
  PowerViewModel ctor). Its runtime correctness (repaints without reactivating) is
  isleap's VM check — the read-only path can't safely exercise a power write here.

### 🔎 PowerSchemeChanged subscriber — FULL CODE FOR REVIEW (read-only invariant)

Subscribed once in the singleton VM's ctor (static event → no unsubscribe):

```csharp
PowerTweaks.PowerSchemeChanged += OnPowerSchemeChanged;
```

Handler body:

```csharp
private void OnPowerSchemeChanged()
{
    App.DispatcherQueue?.TryEnqueue(() =>
    {
        foreach (var section in Sections)
        {
            foreach (var row in section.Items)
                row.RefreshFromSystem();   // read-only re-read (QueryPowerCfg)
            section.RefreshPendingPill();
        }
        RefreshQuickActionCounts();
    });
}
```

Read-only argument (why it CANNOT reactivate a scheme — the CLAUDE.md wrong-direction
bug):
- The event is raised by PowerTweaks **from the write path, AFTER** the powercfg
  write + `/SETACTIVE` + `ClearSchemeDrift()` already ran. The handler runs strictly
  afterwards and only re-reads.
- `row.RefreshFromSystem()` → `ReadCurrentIndex` → `QueryPowerCfg` = `powercfg /QUERY`
  (read) + `ResolveSchemeTarget` (registry read). The read value is pushed into the
  dropdown through its **suppressed** setter (`_suppress`), so re-reading never
  re-enters `ApplyOption`/`SetPowerCfg`.
- `section.RefreshPendingPill()` and `RefreshQuickActionCounts()` only read row state
  (they compute pending counts via `TweakTargets.CollectPending`, which calls
  `ReadState`/`ReadCurrentIndex` — reads).
- The handler calls **nothing** named `SetPowerCfg` / `EnsureAkariScheme` /
  `/SETACTIVE` / `/SETAC*VALUEINDEX`. It carries no power-state authority.
- Marshaled to the UI thread (`App.DispatcherQueue.TryEnqueue`) because it mutates
  bound collections/properties; safe even if ever raised off-thread.

**Note for review:** in this catalog-only tab there are no plan cards / persist
indicator yet, so the handler repaints the tweak ROWS (re-reads them so sibling
dropdowns reflect the now-active Akari scheme). When the deferred Plan Selector +
Persist Indicator land, their repaint (from `ResolveSchemeTarget()` + `_schemeInactive`,
likewise read-only) is added inside this same handler. isleap's VM step: apply one
Power dropdown, confirm the tab repaints and the active scheme is NOT re-toggled /
no extra `/SETACTIVE` churn.

### VM checklist (Phase 20 — for isleap)

- [ ] Apply one Power dropdown on a VM; confirm `PowerSchemeChanged` fires once, the
      rows repaint, and NO second scheme activation / drift churn occurs (the
      read-only invariant, in practice).
- [ ] On a laptop / vendor-GPU rig: Battery + GPU Power sections APPEAR and register
      (7 + 4 rows → Power 47/15 sections there); gating is hardware-driven.
- [ ] Each subgroup dropdown reads back correctly after apply; `/SETACTIVE`-last
      still makes Akari Performance active (net8 invariant, unchanged in the catalog).

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Power Plan Selector + Persist Indicator** (bespoke) — deferred; not part of this
  catalog-only rollout. **Software, Backup, and the bespoke Home/About/Tools tabs**
  — not touched.

### ⟳ Follow-up (2 items folded into Phase 20)

#### (1) PowerSchemeChanged subscriber — LITERAL method body (from disk)

`ViewModels/PowerViewModel.cs` — subscribed once in the ctor
(`PowerTweaks.PowerSchemeChanged += OnPowerSchemeChanged;`); handler verbatim:

```csharp
private void OnPowerSchemeChanged()
{
    App.DispatcherQueue?.TryEnqueue(() =>
    {
        foreach (var section in Sections)
        {
            foreach (var row in section.Items)
                row.RefreshFromSystem();   // read-only re-read (QueryPowerCfg)
            section.RefreshPendingPill();
        }
        RefreshQuickActionCounts();
    });
}
```

(No `SetPowerCfg` / `EnsureAkariScheme` / `/SETACTIVE` anywhere in the body — the
read-only invariant. Full XML-doc rationale sits above the method in the file.)

#### (2) SHOW-ONLY recon — Power ▸ Plan Selector + Persist Indicator (NOT built/extracted)

Same discipline as Gaming Phases 8/10/12/16 — nothing built or extracted; build #2
and build #3 untouched by this recon.

**Files / methods (build #2, read-only):**
- `Tabs/Power/PowerTab.PlanSelector.cs` — `BuildPlanSelector`, `BuildCustomPlanCard`,
  `SetActiveCard`, `RefreshActiveCard`, `ActivatePlan`, `ActivateUltimatePlan`,
  `SetActiveUltimate`, `FindUltimatePlanGuid`.
- `Tabs/Power/PowerTab.Persistence.cs` — `BuildPersistIndicator`,
  `RefreshPersistIndicator`, `RevertToBalanced`, plus the scheme machinery
  (`ResolveSchemeTarget`, `ClearSchemeDrift`, `EnsureAkariScheme`,
  `ReadStoredSchemeGuid`, `_schemeTarget` / `_schemeResolved` / `_schemeInactive`).
- Rendered by `PowerTab.Build()` at the TOP, before the catalog sections:
  `BuildPlanSelector(RootPanel)` then `BuildPersistIndicator(RootPanel)`.

**Bespoke or TweakDefinition-backed? — GENUINELY BESPOKE.** Both are hand-built UI
(plan cards = `Border`/`Grid`/`TextBlock`; persist indicator = `TextBlock` +
`Button`). Neither creates a `TweakDefinition`, neither calls `AddSection`/
`AddTweakRow`, so **neither registers with `TweakRegistry`** — exactly like Gaming's
former bespoke sections. (NB: the *scheme machinery* — `ResolveSchemeTarget` /
`EnsureAkariScheme` / `ClearSchemeDrift` / `_schemeInactive` — was already ported
into build #3's `PowerTweaks.cs` because the catalog's `SetPowerCfg` needs it; only
the VIEW code here is unported.)

**What each reads / writes:**
- **Plan Selector — an APPLY action, NOT read-only.** Reads
  `SystemStateReader.ReadActivePowerPlan()` (registry) + `ListPowerPlans()`
  (`powercfg /list`). Clicking a plan card **WRITES**: `ActivatePlan` →
  `Service.RunProcess("powercfg", "/setactive {guid}")`; `ActivateUltimatePlan` →
  `powercfg /duplicatescheme {UltimatePerfGuid}` then `/setactive`. **It does NOT
  call `EnsureAkariScheme` / `SetPowerCfg`** — it drives `powercfg /setactive` +
  `/duplicatescheme` directly (a *separate* write path that switches the ACTIVE plan;
  it does not touch the Akari scheme's per-setting values). After activating it
  repaints read-only (`RefreshActiveCard()` + `RefreshPersistIndicator()`) and does
  **NOT** raise `PowerSchemeChanged`.
- **Persist Indicator — the repaint is READ-ONLY (matches the contract); but its
  section also wires a destructive WRITE button.**
  - `RefreshPersistIndicator()` reads `ResolveSchemeTarget()` + `_schemeInactive`
    only, and updates the indicator text/colour + revert-button visibility. This is
    **exactly** the read-only "repaint from ResolveSchemeTarget() + _schemeInactive"
    contract `PowerSchemeChanged` documents — i.e. the method the Phase-20 subscriber
    would call here once this UI is extracted.
  - The same `BuildPersistIndicator` also adds a **"Revert to Balanced" button** →
    `RevertToBalanced()` which **WRITES + DELETES**: `powercfg /setactive {Balanced}`
    then `powercfg /delete {stored Akari GUID}` (removes the persistent Akari
    Performance plan and all its customisations) + `ClearState`. So the *repaint* is
    read-only, but the *button it renders* is a destructive apply.

**Confirmation / warning dialogs — NONE (both are one-click writes):**
- **Plan Selector:** clicking a plan card immediately runs `powercfg /setactive` —
  no dialog, no warning. Switching plans mid-session takes effect instantly.
- **"Revert to Balanced":** immediately activates Balanced and **deletes** the Akari
  Performance scheme with **no confirmation** — only a hover tooltip ("Reactivate
  the Windows Balanced plan and delete the Akari Performance scheme"). This is a
  System-Restore-class destructive-without-confirm landmine to flag at extraction
  time (isleap may want an option-B-style Warning, as chosen for
  `system-restore-protection` in Phase 19).

**Implication for a future extraction (not decided here):** these are bespoke like
Gaming's sections — they need a small bespoke section VM/DataTemplate (plan cards +
indicator + revert button), NOT catalog TweakDefinitions. The read-only repaint
(`RefreshPersistIndicator`/`RefreshActiveCard`) is what the existing
`OnPowerSchemeChanged` subscriber would additionally call; the plan-switch and
revert are apply actions (a read-only accessor for `ResolveSchemeTarget()` /
`_schemeInactive`, both currently `private` in `PowerTweaks.cs`, would need exposing).
No code written — awaiting isleap's go.

---

## MVVM Phase 21 — Power ▸ Plan Selector + Persist Indicator (bespoke section) — **COMPLETE (VM sign-off pending)**

Date: 2026-08-05

Ported net8's Power Plan Selector + Persist Indicator as a bespoke section (confirmed
genuinely bespoke in the Phase-20 recon — NOT TweakDefinition rows), rendered at the
TOP of the Power page above the catalog sections. Verbatim behaviour — no
confirmation dialogs added anywhere (isleap's decision), including the
no-confirmation "Revert to Balanced" delete. Build #2 read-only; no Defender code
touched.

### Build status (literal)

VS 18 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:22.96
```

### Files

- **New:** `ViewModels/PowerPlanSectionViewModel.cs` (+ nested `PlanCardViewModel` /
  `PlanKind`) — the bespoke section VM; `Views/Selectors/PowerSectionTemplateSelector.cs`.
- **Changed:** `Views/PowerPage.xaml` — added the `PowerPlanSelectorTemplate`
  DataTemplate + selector in `Page.Resources`, and the single ItemsControl now binds
  `DisplayItems` with `ItemTemplateSelector`; `Views/PowerPage.xaml.cs` — calls
  `ComposeDisplay()` after `Build()`; `ViewModels/PowerViewModel.cs` — added
  `PlanSection`, `DisplayItems`, `ComposeDisplay()` (plan section FIRST, then catalog
  sections), a read-only `RefreshCatalogRows` callback for the Revert path, and
  extended `OnPowerSchemeChanged` to also call `PlanSection.Refresh()`.
- **`Tabs/Power/Catalog/PowerTweaks.cs` — VISIBILITY CHANGES ONLY (+ 2 data consts,
  + 1 relocated reset):** `ResolveSchemeTarget()`, `ListPowerPlans()`,
  `ReadStoredSchemeGuid()`, `BalancedGuid`, `StateKeyPath`, `SchemeGuidValue`,
  `AkariPlanName` → `internal`; added `internal static bool SchemeInactive =>
  _schemeInactive;` (read-only accessor); added `internal const HighPerfGuid /
  UltimatePerfGuid` (byte-identical to net8's PowerTab constants); added
  `internal static void ResetSchemeCacheAfterRevert()` which does exactly net8
  RevertToBalanced's inline `_schemeTarget = null; _schemeResolved = true;`. **No
  existing logic altered** — `ResolveSchemeTarget`/`_schemeInactive`/`ListPowerPlans`
  bodies are unchanged.

### Byte-identical to net8 (Plan Selector + Persistence)

- **Plan cards:** Balanced / High Performance / Ultimate Performance (3 fixed) + a
  dynamic **Custom** card, with the exact net8 descriptions, the ACTIVE tag, and
  click-to-activate.
- **`RefreshActiveCard`** (read-only) — `ReadActivePowerPlan()` + `ListPowerPlans()`
  friendly-name resolution + the same Balanced/HighPerf/Ultimate/name-contains-Ultimate
  matching; a non-matching plan shows the Custom card with the real name.
- **`RefreshPersistIndicator`** (read-only) — `ResolveSchemeTarget()` +
  `SchemeInactive`; the three exact net8 strings (not-persisted / drifted /
  persistent) and Revert-button visibility.
- **Writes (the interactive controls — net8's own write path, NOT the catalog's):**
  `ActivatePlan` → `powercfg /setactive {guid}`; `ActivateUltimatePlan` →
  `/duplicatescheme` (only if none present) + `/setactive`; **`RevertToBalanced`** →
  `/setactive {Balanced}` + `/delete {stored Akari GUID}` + `ClearState` +
  `ResetSchemeCacheAfterRevert()`, with **no confirmation dialog** (only the hover
  tooltip), exactly as net8.

### Read-only invariant — CONFIRMED (the named CLAUDE.md landmine)

- **`OnPowerSchemeChanged`** now additionally calls `PlanSection.Refresh()` =
  `RefreshActiveCard()` + `RefreshPersistIndicator()`. Both are **read-only**
  (`ReadActivePowerPlan`/`ListPowerPlans` reads; `ResolveSchemeTarget()`/`SchemeInactive`
  reads). **Nothing added to the handler calls `SetPowerCfg`, `EnsureAkariScheme`, or
  `powercfg /SETACTIVE`** — the repaint carries no power-state authority.
- The plan-switch and Revert are user-initiated command handlers (card/button click),
  NOT part of `OnPowerSchemeChanged`. They drive `powercfg` directly via
  `ToolService.RunProcess` (net8's path) and **do not call `SetPowerCfg` /
  `EnsureAkariScheme`**, and do **not** raise `PowerSchemeChanged` (they repaint via
  `Refresh()` directly) — so there is no write→event→write loop.

### Registry / guard — UNCHANGED (bespoke section registers nothing)

The Plan Selector section does NOT register with `TweakRegistry`. Guard, launched
de-elevated with no navigation, is byte-for-byte the pre-Phase-21 tiling:

```
[Power] 36 tweaks registered in 13 sections (registry total 439).
[WARMUP]   Power [403..439) 36 rows — Power
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (read-only, de-elevated + UIA; NO plan switched / NO Revert clicked)

- **Renders at the TOP** — "POWER PLAN" header Y=454, above the first catalog section
  "Display" Y=659.
- **All cards present** — Balanced, High Performance, Ultimate Performance, and the
  Custom card. On this machine the active plan is the custom **"Akari Performance"**
  scheme (GUID `bca373e4-…`), so the Custom card shows it with the **ACTIVE** tag and
  the three fixed cards are not active — correct.
- **Persist indicator** reads **"Power plan: Akari Performance (persistent)"** (the
  stored Akari GUID resolved and is active → not drifted); **Revert to Balanced**
  button visible.
- App alive; **no plan was switched and Revert was not clicked** (destructive — that
  is isleap's VM step).

### VM checklist (Phase 21 — for isleap; DESTRUCTIVE plan writes, VM only)

- [ ] Click Balanced / High Performance → the card goes ACTIVE, `powercfg /setactive`
      runs, and the persist indicator updates read-only (no re-toggle churn).
- [ ] Click Ultimate Performance → reuses an existing Ultimate plan if present, else
      `/duplicatescheme` once, then activates (no pile-up of copies).
- [ ] **Revert to Balanced** → Balanced activates AND the Akari Performance scheme is
      deleted, with **no confirmation dialog** (net8 verbatim); catalog Power rows
      re-read afterward.
- [ ] After any of the above, the `[WARMUP]` guard still tiles `[0..439)` (plan
      writes don't alter row registration).

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Software, Backup, bespoke Home/About/Tools** — not touched.

---

## MVVM Phase 22 — Software tab (SHOW ONLY)

Recon only. No code written in build #3; build #2 read-only; Defender untouched.

### Build #2 file inventory — `Akari-Tool/Tabs/Software/`

| File | Lines | Role |
|---|---|---|
| `SoftwareTab.xaml` / `.xaml.cs` | 20 / 126 | `BaseTab` shell, 3 sub-panels routed by `ShowPanel(name)` |
| `SoftwareTab.WindowsApps.cs` | 117 | "Bloatware" panel + `RemoveSelectedWindowsAppsAsync` |
| `SoftwareTab.ExternalApps.cs` | 107 | "AppInstaller" panel + `UninstallSelectedExternalAsync` |
| `SoftwareTab.InstallQueue.cs` | 115 | `InstallSelectedAsync` (shared by both panels) |
| `SoftwareTab.Cards.cs` | 255 | card grid, badges, selection, `RefreshStatusAsync` |
| `SoftwareTab.UiHelpers.cs` | 224 | `MakeActionButton`, `SelectAllRow`, `AddSearchRow`, `CountBadge` |
| `SoftwareAppService.cs` | 585 | detection snapshot + install/uninstall/removal pipeline |
| `AppModels.cs` | 105 | `AppDefinition` / `AppGroup` / `ExternalAppMetadata` |
| `AppIconService.cs` | 189 | jsDelivr icon fetch + `%ProgramData%\AkariTool\IconCache` |
| `Catalog/*.cs` (20 files) | 3517 | `WindowsAppCatalog` (653), `CapabilityCatalog`, `OptionalFeatureCatalog`, `ExternalAppCatalog.*` (17 category files) |
| `Removal/BloatRemovalScriptGenerator.cs` | 454 | generates `BloatRemoval.ps1` |
| `Removal/EdgeRemovalScript.cs` | 685 | `GetScript()` → Edge removal PS1 |
| `Removal/OneDriveRemovalScript.cs` | 465 | `GetScript()` → OneDrive removal PS1 |

Plus `Tabs/Debloat/DebloatTab.xaml(.cs)` (10 + 138) — hosted *inside* SoftwareTab as
the third sub-panel ("Debloat" rail tag).

**TweakDefinition-backed sections: NONE.** `grep TweakRegistry|TweakDefinition
Tabs/Software Tabs/Debloat` returns **zero hits**. The whole tab is bespoke — its data
model is `AppDefinition` (Id/Name/AppxPackageName/WinGetPackageId/CapabilityName/
OptionalFeatureName/RemovalScript/…), a separate identity space from `TweakDefinition`.
No Ids to preserve for Backup/Restore; the tab contributes 0 to `TweakRegistry.Count`,
so the `[WARMUP]` total must stay unchanged after this wave.

### The three panels

1. **Windows Apps** (`Tag="Bloatware"`) — `WindowsAppCatalog` + `CapabilityCatalog` +
   `OptionalFeatureCatalog`, split into 3 flat alphabetical card sections (Windows Apps /
   Legacy Capabilities / Optional Features). Buttons: Remove Selected, Install Selected,
   Refresh.
2. **External Apps** (`Tag="AppInstaller"`) — `ExternalAppCatalog`, grouped by
   `GroupName`. Buttons: Install Selected, Uninstall Selected, Refresh.
3. **Debloat** (`Tag="Debloat"`) — `DebloatTab`, a separate script-runner UI (3 groups of
   `<name, description, run.ps1, undo.ps1>` tuples shelled through `ToolService`).
   Not card-based, no `AppDefinition`. **No confirmation dialog anywhere in DebloatTab.**

### Bespoke removal generators — trigger, effect, confirmation

**Trigger is NOT per-app-row.** Rows are select-only (checkbox / click-anywhere).
Removal is a single bulk **"Remove Selected"** button per panel.

**Dispatch** (`SoftwareAppService.RemoveWindowsAppsAsync`, line 220): selection is split
on `a.RemovalScript != null`.
- **Dedicated-script apps** — exactly two in the catalog: `windows-app-edge`
  (`RemovalScript = () => EdgeRemovalScript.GetScript()`, `HasInstabilityWarning = true`)
  and `windows-app-onedrive` (`OneDriveRemovalScript.GetScript()`). Each script text is
  written to a temp `.ps1` (`EdgeRemoval.ps1` / `OneDriveRemoval.ps1`) and run hidden via
  `RunScriptTextAsync`.
- **Everything else** — categorised into `packages` / `capabilities` / `optionalFeatures`
  / `specialApps` (`Categorize`, line 261), fed to
  `BloatRemovalScriptGenerator.GenerateScript(..., xboxFix, teamsKill)` and run once as
  `BloatRemoval-Run.ps1`. `xboxFix` fires on GamingApp/XboxGamingOverlay/XboxGameOverlay;
  `teamsKill` on `MSTeams`.

**What the scripts actually do** — all three are Winhance ports, far beyond an uninstall:
- **Edge** (685 lines): `Stop-Process` on Edge processes; sets `Visibility=1` on the
  Edge servicing package key and deletes its `Owners` subkey; `Remove-AppxPackage
  -AllUsers` for `Microsoft.MicrosoftEdge` + `.Stable`; deletes the Edge program
  directory; `reg query`/`reg delete` sweep of `StartMenuInternet`, MUICache, and Edge
  registry trees; export/re-import backup of `EdgeUpdate\ClientState`; unregisters the
  EdgeUpdate **service**; `Unregister-ScheduledTask` for Edge tasks; removes
  `%LOCALAPPDATA%` Edge folders.
- **OneDrive** (465 lines): kills `*OneDrive*` processes; runs the uninstall string from
  `HKLM` and **per-user `HKU\<SID>`** `Uninstall\OneDriveSetup.exe` keys; deletes
  `OneDriveSetup.exe` from System32/SysWOW64 via P/Invoke `MoveFileEx` with
  `MOVEFILE_DELAY_UNTIL_REBOOT` for locked files; `reg delete` of the Default-user `Run`
  entry; `Unregister-ScheduledTask` for OneDrive tasks.
- **BloatRemoval generator** (454 lines): self-elevating header, logging to
  `AkariPaths.LogsDirectory\BloatRemovalLog.txt` (rotates at 500 KB), a runspace-pool
  parallel helper (`MaxThreads=10`), then provisioned-package removal → AppX removal →
  capabilities → optional features → special apps (OneNote).

**Persistence (the part that outlives the click):** after every removal,
`SaveAndRegisterBloatRemovalAsync` (line 286) merges the new items into
`C:\ProgramData\AkariTool\Scripts\BloatRemoval.ps1` (union with arrays re-parsed out of
any existing script via `ExtractArrayFromScript`) and registers
`schtasks /Create /F /TN "AkariTool\BloatRemoval" /SC ONSTART /RU SYSTEM /RL HIGHEST`.
`RemoveFromSavedScriptAsync` (line 341) is the inverse — called after a reinstall; it
deletes the script and `schtasks /Delete /F` the task when nothing remains.

**Confirmation: YES, one bulk `ContentDialog` per action, none per row.**
- Windows Apps: `AkariDialogs.ConfirmYesNoAsync(msg, "Remove Windows Apps")` — message
  lists count, first 10 names + "(+N more)", a warning line naming every
  `HasInstabilityWarning` item, and the sentence "A startup task keeps these removed
  after Windows updates."
- External Apps: `ConfirmYesNoAsync(msg, "Uninstall External Apps")` — count + first 10
  names.
- Install paths and DebloatTab: **no confirmation at all.**

### Build #3 — already partially ported (do NOT assume unstarted)

`Akari-Tool-MVVM/Tabs/Software/` **exists** and the entire non-UI layer is present:
`AppModels.cs`, `SoftwareAppService.cs`, all 20 `Catalog/*.cs`, all 3 `Removal/*.cs`.
`diff -rq` against build #2: **all identical, byte-for-byte, zero differences.**
This was never logged as a phase — it rode along with the `AutounattendService` port,
which consumes `AppDefinition` (`AutounattendService.cs:69,98`,
`.ScriptSystem.cs:23 AppendAppRemovalSection`).

**Missing in build #3:** `AppIconService.cs`, all six `SoftwareTab.*` UI files, and
`Tabs/Debloat/` entirely (the `Debloat*.ps1` / `RemoveEdge.ps1` / `RemoveOneDrive.ps1`
payloads *are* in `Scripts/`). No `Views/SoftwarePage*`, no `ViewModels/Software*`.
The rail already carries all three tags (`MainWindow.xaml:73-88` — SOFTWARE header,
`Bloatware` / `AppInstaller` / `Debloat`); all three currently fall through to
`PlaceholderPage`.

So the remaining work is **UI-only** — the destructive engine is already in place and
must stay byte-identical.

### `AppDefinition.InstallScript` — does not exist

`grep -rn "InstallScript"` over **both** builds: **zero hits.** `AppModels.cs` has no
such property, and no `IoT` / `LTSC` / winget-bootstrap code exists anywhere in either
tree. `SoftwareAppService.RunWingetAsync` (line 487) just launches `FileName = "winget"`
and, if it is absent, logs `[ERROR] winget not available` and returns `-1`. The
CLAUDE.md line "`AppDefinition.InstallScript` bootstraps Win11 IoT LTSC" is **stale —
it describes something that is not in the code.**

### Phase 22 follow-up — CLAUDE.md corrected (docs only)

isleap approved the fix; the stale Architecture ▸ "Shared, both builds" bullet
("winget primary; `AppDefinition.InstallScript` bootstraps Win11 IoT LTSC") now reads
that winget is the only install path, that `RunWingetAsync` logs
`[ERROR] winget not available` and returns `-1` when winget is missing, and that no
bootstrap path and no `InstallScript` property exist. Doc change only — **zero code
changes in build #3, zero writes to build #2, no Defender code referenced.**

**Process note worth keeping.** This claim was in CLAUDE.md — the declared source of
truth — and had survived every prior phase unchallenged. It was caught only because the
recon brief asked to *confirm* whether `InstallScript` was ported, which meant grepping
for it rather than reading the doc and answering from it. The doc already warns that
"specific claims about what a given file contains can drift"; this is the first logged
instance of that actually happening. Standing takeaway, beyond this one line: **when a
doc claim names a specific symbol, file, or property, grep for it before relying on or
repeating it.** A named symbol is cheap to verify and expensive to assume — the failure
mode is not a wrong sentence in a file, it is a port built on a feature that was never
there. Treat CLAUDE.md as authoritative for *intent and constraints*, and the code as
authoritative for *what exists*.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Build #3** — no code files created or edited; only `MIGRATION_LOG2.md` and the one
  corrected CLAUDE.md bullet.

---

## MVVM Phase 23 — Software ▸ External Apps (AppInstaller) — **COMPLETE (VM sign-off pending)**

Stage 1 of the Software rollout, the lowest-risk panel. **Bloatware, Debloat and the
Edge/OneDrive removal scripts were not touched.** Build #2 read-only; no Defender code
referenced.

### Which net8 files this panel actually came from

Bloatware and Debloat share most of the same source set, so the split matters:

| net8 file | Relevance to AppInstaller |
|---|---|
| `SoftwareTab.ExternalApps.cs` | **THE panel** — `BuildExternalAppsPanel` + `UninstallSelectedExternalAsync` |
| `SoftwareTab.InstallQueue.cs` | **shared** — `InstallSelectedAsync(_externalApps, isWindowsApps: false)`, `RefreshStatusAsync`, `SetButtonsEnabled` |
| `SoftwareTab.Cards.cs` | **shared** — `BuildCardSection` / `BuildAppCard` / `LoadIconAsync` |
| `SoftwareTab.UiHelpers.cs` | **shared** — action buttons, count badge, pills, search row, select-all row, `SetSelection`, `ApplySearch`, `RefreshCounts` |
| `SoftwareTab.xaml(.cs)` | shell only — `ShowPanel` routing, catalog loading. In build #3 the rail routes to a page directly, so the 3-panel switch is gone |
| `SoftwareTab.WindowsApps.cs` | **Bloatware only — NOT ported** (Remove Selected + the BloatRemoval/Edge/OneDrive pipeline) |

### New in build #3

- `Tabs/Software/AppIconService.cs` — **ported this stage, verbatim** (`diff` clean vs
  build #2). Required here: net8's `BuildAppCard` fires `LoadIconAsync` for **every**
  card including external ones, and `AppIconService.CandidatePaths` has a dedicated
  `external-app-*` → `icons/external/{wingetId}.png` branch. Confirmed live — the UIA
  tree shows a `ControlType.Image` inside each card avatar.
- `ViewModels/Software/AppCardViewModel.cs` — one card. Wraps `AppDefinition`; selection
  is written **through to the definition**, so `SoftwareAppService` (which reads
  `a.IsSelected`) works unchanged.
- `ViewModels/Software/AppSectionViewModel.cs` — one category; refills `VisibleCards` on
  search rather than collapsing in place (same reason net8 rebuilt the UniformGrid's
  Children — a uniform grid still reserves cells for collapsed items).
- `ViewModels/Software/ExternalAppsViewModel.cs` — the panel. **Plain `ObservableObject`,
  NOT a `TweakPageViewModel`.**
- `Views/ExternalAppsPage.xaml(.cs)`.

Edited: `Services/TweakDialogs.cs` (+`ConfirmYesNoAsync`), `App.xaml.cs` (DI),
`MainWindow.xaml.cs` (route + rail sync).

### Registration — guard UNCHANGED

The panel registers nothing. `ExternalAppsViewModel` is a DI **singleton** (it owns the
built catalog + live selection) but is deliberately **absent from the
`TweakPageViewModel` enumeration** — adding it there would have put a non-registering VM
into the warm-up and broken the `ClaimRange` tiling assertion. Guard from the
de-elevated run, byte-for-byte the Phase 21 tiling:

```
[Power] 36 tweaks registered in 13 sections (registry total 439).
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP]   Gaming [0..130) 130 rows — Gaming & Performance
[WARMUP]   Sound [130..135) 5 rows — Sound
[WARMUP]   Notifications [135..151) 16 rows — Notifications
[WARMUP]   Update [151..163) 12 rows — Windows Updates
[WARMUP]   Privacy [163..252) 89 rows — Privacy & Security
[WARMUP]   Customize [252..403) 151 rows — Customize
[WARMUP]   Power [403..439) 36 rows — Power
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Confirmation dialog — ported verbatim

Uninstall, `ExternalAppsViewModel.UninstallSelectedAsync`, character-for-character from
net8 `SoftwareTab.ExternalApps.UninstallSelectedExternalAsync`:

```csharp
var msg = $"Uninstall {selected.Count} app(s)?\n\n" +
          string.Join(", ", selected.Take(10).Select(a => a.Name)) +
          (selected.Count > 10 ? $" (+{selected.Count - 10} more)" : "");
if (!await _dialogs.ConfirmYesNoAsync("Uninstall External Apps", DialogText(msg)))
    return;
```

Title `Uninstall External Apps`, buttons **Yes / No** (net8 `AkariDialogs.ConfirmYesNoAsync`
→ `PrimaryButtonText = "Yes"`, `CloseButtonText = "No"`, `DefaultButton = Primary`),
content a wrapping `TextBlock` with `MaxWidth = 440` — the same element
`AkariDialogs.ShowAsync` built. `TweakDialogs` had only OK/Cancel, hence the new
`ConfirmYesNoAsync`; it keeps the existing fail-safe (no XamlRoot ⇒ **declined**).

The early `return` sits **before** the loop, so `SoftwareAppService.UninstallExternalAppAsync`
cannot be reached on a decline. Verified by code review, not by clicking.

**⚠ Install has NO confirmation — that is net8's behaviour, preserved, not an omission.**
Phase 22 already recorded it ("Install paths and DebloatTab: no confirmation at all").
The only install-path dialog is net8's single-button **"Permanent Items"** notice, ported
verbatim, which fires when a selection contains `CanBeReinstalled == false` items and
filters them out. Adding an install confirmation would be a behaviour change and is
isleap's call.

### Deliberate deviations (all behaviour-preserving, all flagged)

1. **Selected-count is state-driven, not click-driven.** net8 called `RefreshCounts()` by
   hand from four mutation sites. Here the panel subscribes to
   `AppDefinition.PropertyChanged` / `IsSelected`. **This was a real defect caught in
   verification**: the first UIA pass toggled a card and the badge stayed at "0 selected"
   because nothing had raised the click. Now it tracks every path — click, checkbox,
   select-all, post-install deselect, automation.
2. **`UniformGrid` → `ItemsRepeater` + `UniformGridLayout`** at the same 340px
   `MinItemWidth` (net8 `CardMinWidth`), replacing net8's `SizeChanged` column
   recomputation with the declarative equivalent.
3. **Hover moved onto the VM** (`IsHovered`). net8 assigned `card.BorderBrush` directly
   from PointerEntered/Exited; a local value would permanently break the binding, so the
   brush is derived (selected > hovered > resting) — same three-way result.
4. **`RefreshStatusAsync` is panel-scoped.** net8's single method refreshed both panels
   because both lived in one tab. Bloatware will get its own when that stage lands; the
   underlying `GetInstallSnapshotAsync` / `ApplyInstallStatus` calls are unchanged.
5. **Badge pills use stock Fluent brushes** (`SystemFillColorSuccessBrush` /
   `SystemFillColorCautionBrush`) — the build #3 convention; the Akari token dictionary
   (`AkariSuccessBgColor` …) still isn't ported. One-dictionary swap later.
6. The **"Permanent" pill is absent** — it is `isWindowsApps && !CanBeReinstalled` in
   net8, i.e. Bloatware-only. Correct for this panel.

No install / uninstall / winget logic was reimplemented: the VM calls
`SoftwareAppService.InstallAppAsync`, `.UninstallExternalAppAsync`,
`.GetInstallSnapshotAsync`, `.ApplyInstallStatus` and nothing else. The
`isWindowsApps`-only `RemoveFromSavedScriptAsync` call is correctly absent — that
keep-removed bookkeeping belongs to Bloatware.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```

### Verification (read-only, de-elevated + UIA; NOTHING installed or uninstalled)

- **Renders with real catalog data.** Header "External Apps" + net8's subtitle verbatim,
  the three buttons, the "0 selected" badge, the search box, the select-all row, then
  "Browsers" with real cards (Ablaze Floorp, Arc Browser, Brave, DuckDuckGo, Google
  Chrome, Helium, LibreWolf, Maxthon …), two columns at this window width.
- **All 16 category groups present** — probed one representative app per group through the
  search box; every group's header AND its app appeared, 16/16
  (Browsers, Compression, Customization Utilities, Development Apps, Document Viewers,
  File & Disk Management, Gaming, Imaging, Messaging Email & Calendar, Multimedia,
  Online Storage & Backup, Optical Disc Tools, Other Utilities, Privacy & Security,
  Remote Access, Runtimes & Dependencies). Catalog totals: **193 external apps, 16 groups.**
- **Search filters and hides empty sections** — `7-zip` left exactly the Compression
  section with 7-Zip + NanaZip; every other header gone.
- **Selection works** — toggling a card moved the badge `0 selected → 1 selected → 0
  selected`.
- **Detection is live** — Google Chrome carries the **Installed** badge; icons resolved
  from the CDN and replaced the letter tiles.
- **Refresh is read-only and completed** — invoked once, status line appeared and cleared,
  no `[ERROR]` in the log, app alive afterwards.
- **Install Selected / Uninstall Selected were NOT clicked** — destructive, isleap's VM step.

### VM checklist (Phase 23 — for isleap; DESTRUCTIVE, VM only)

- [ ] Select 1 app → **Uninstall Selected** → dialog reads
      `Uninstall 1 app(s)?` + the name, Yes/No; **No** cancels with nothing run.
- [ ] Select 12 apps → dialog lists the first 10 then `(+2 more)`.
- [ ] **Yes** → each app uninstalls in order, status line counts `(i/N)`, winget output
      streams into the log dock, selection clears, badges re-read.
- [ ] **Install Selected** → runs with **no confirmation** (net8 parity — confirm this is
      still wanted before shipping).
- [ ] Website glyph on a card opens the URL and does **not** toggle that card.
- [ ] Select apps, then type a search that hides them → they stay selected and Uninstall
      Selected still acts on them (net8-identical; confirm this is the wanted behaviour).
- [ ] After all of the above the `[WARMUP]` guard still tiles `[0..439)`.

### Addendum (review follow-up) — the two selection-read paths, and the actual diff

**1. The dialog and the badge read the SAME path.** The `PropertyChanged` subscription
is only the *trigger* that tells the badge to recompute; it is not a second store of
selection state. Both call sites read the same list and the same property:

```csharp
// Badge — ExternalAppsViewModel.RefreshCounts()
private void RefreshCounts() =>
    SelectedCountText = $"{_externalApps.Count(a => a.IsSelected)} selected";

// Dialog + the work loop — ExternalAppsViewModel.UninstallSelectedAsync()
var selected = _externalApps.Where(a => a.IsSelected).ToList();
if (selected.Count == 0 || _busy) return;

var msg = $"Uninstall {selected.Count} app(s)?\n\n" +
          string.Join(", ", selected.Take(10).Select(a => a.Name)) +
          (selected.Count > 10 ? $" (+{selected.Count - 10} more)" : "");
```

`_externalApps.Count(a => a.IsSelected)` vs `_externalApps.Where(a => a.IsSelected)` —
same collection, same predicate, same single `AppDefinition` instance per app. There is
exactly one copy of the selection bit in the process: `AppDefinition.IsSelected`. The
card VM's `IsSelected` is a pass-through property (`get => App.IsSelected; set =>
App.IsSelected = value;`), it stores nothing, and `SoftwareAppService` reads the same
field. They agree by construction, not by convention.

They also cannot disagree *in time*: `AppDefinition`'s setter raises `PropertyChanged`
inline, the handler calls `RefreshCounts()` synchronously, and all of it runs on the UI
thread — so the badge is current before the setter returns. The dialog reads its own
snapshot at click time, which is the value it then acts on. There is no window in which
the badge shows one number and the dialog uninstalls a different set.

**Worth knowing (net8-identical, not a regression):** both reads are over the FULL
catalog, not the search-filtered view. Selecting apps, then typing a search that hides
them, leaves them selected — the badge still counts them and Uninstall Selected still
acts on them. net8's `RefreshCounts` / `UninstallSelectedExternalAsync` were unfiltered
in exactly the same way. Only `SetSelection` (the select-all row) is filtered
(`c.Visible`), also as in net8. Added to the VM checklist below rather than "fixed",
since changing it would be a behaviour change.

**2. The selection-count fix, verbatim.** Purely additive — no line was removed or
changed. The three net8-inherited `RefreshCounts()` call sites (`SetSelection`, the
`finally` in `RefreshStatusAsync`, `SetCardSelected`) all remain; the subscription just
closes the paths that never had one.

```diff
--- a/ViewModels/Software/ExternalAppsViewModel.cs
+++ b/ViewModels/Software/ExternalAppsViewModel.cs
@@ Build()
             _cards.AddRange(cards);
             Sections.Add(new AppSectionViewModel(group.Key, cards));
         }
 
+        // The "N selected" badge tracks the SELECTION STATE, not the click that
+        // caused it. net8 called RefreshCounts() by hand from each of the four
+        // mutation sites; driving it from the definition's own PropertyChanged
+        // covers every path — including ones with no click at all (post-install
+        // deselect, import refresh, automation) — and cannot drift out of sync.
+        foreach (var app in _externalApps)
+            app.PropertyChanged += (_, e) =>
+            {
+                if (e.PropertyName == nameof(AppDefinition.IsSelected)) RefreshCounts();
+            };
+
         RefreshCounts();
     }
```

Mechanism change in one line: the badge went from *"recomputed wherever someone
remembered to call it"* to *"recomputed whenever the value it displays changes."* The
dialog was never affected — it always read the state directly, which is why the UIA
toggle produced a stale badge but would still have uninstalled the right app.

No rebuild: no code changed for this addendum.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Bloatware, Debloat, Edge/OneDrive removal** — untouched; separate stages.
- **Backup, bespoke Home/About/Tools** — untouched.

---

## MVVM Phase 24 — Bloatware panel + persistence (SHOW ONLY)

Stage 2 recon of the Software tab. **Nothing created or edited in build #3; build #2
read-only; no Defender code referenced.** Everything below was read from source this
phase, not carried over from Phase 22's summary — where the two disagree, this entry
wins.

Scope: `WindowsAppCatalog` + `CapabilityCatalog` + `OptionalFeatureCatalog`, **excluding**
the two `RemovalScript`-backed definitions (Edge, OneDrive) — those are Stage 3.

**Catalog counts (re-counted from source, build #3 copy, `diff -rq` clean vs build #2):**
`Id = "windows-app-*"` **56** · `Id = "capability-*"` **10** · `Id = "feature-*"` **7** =
**73 definitions**, of which **2** carry `RemovalScript` (Edge, OneDrive → Stage 3),
leaving **71 in Stage 2's scope**. `CanBeReinstalled = false` on **15**;
`HasInstabilityWarning = true` on exactly **2** — `windows-app-edge` and
`windows-app-app-installer`.

### 1. Bloatware-specific vs shared (Stage 1 already ported the shared half)

| net8 file | Stage 2 status |
|---|---|
| `SoftwareTab.WindowsApps.cs` (117 ln) | **BLOATWARE-ONLY — the whole extraction target.** `BuildWindowsAppsPanel` + `RemoveSelectedWindowsAppsAsync`. Nothing else in the tab references it. |
| `SoftwareTab.Cards.cs` | **shared** — `BuildCardSection` / `BuildAppCard` / `LoadIconAsync`. Ported Stage 1 as `AppCardViewModel` + `AppSectionViewModel` + `AppCardTemplate`. **Reusable as-is** except the `isWindowsApps` flag (below). |
| `SoftwareTab.UiHelpers.cs` | **shared** — buttons, count badge, pills, search row, select-all row, `SetSelection`, `ApplySearch`, `RefreshCounts`. Ported Stage 1. **Reusable as-is.** |
| `SoftwareTab.InstallQueue.cs` | **shared** — `InstallSelectedAsync`, `RefreshStatusAsync`, `SetButtonsEnabled`. Ported Stage 1 **for `isWindowsApps: false` only**; the `true` branch is still unported (one extra call — see §5). |
| `SoftwareTab.xaml` / `.xaml.cs` | shell only. Loads all four catalogs, `ShowPanel` routes the 3 panels, `GetSelectedWindowsApps()`. In build #3 the rail routes straight to pages, so `ShowPanel` has no successor. |
| `SoftwareTab.ExternalApps.cs` | AppInstaller-only — done in Stage 1. |
| `Tabs/Debloat/DebloatTab.*` | Debloat-only — Stage 4, untouched. |

**The one thing Stage 1's card layer does NOT already cover:** `BuildAppCard(app,
isWindowsApps)` renders a third pill when `isWindowsApps && !app.CanBeReinstalled` —

```csharp
var perm = MakePill("\uE7C1  Permanent", "AkariDangerBgColor", "AkariDangerBorderColor", "AkariDangerFgColor");
ToolTipService.SetToolTip(perm, "Once removed, this item can't be reinstalled.");
```

`AppCardViewModel` deliberately omits it (Phase 23, flagged). Stage 2 needs it back for
**15** definitions. Sections differ too: Bloatware is three **fixed** sections filtered by
shape (`CapabilityName == null && OptionalFeatureName == null` / `CapabilityName != null` /
`OptionalFeatureName != null`), each `OrderBy(Name, OrdinalIgnoreCase)` — not
`GroupBy(GroupName)` like AppInstaller.

**Cross-tab dependency to plan for:** `MainWindow.xaml.cs:254` wires
`adv.SetSelectedAppsProvider(() => _software.GetSelectedWindowsApps())` — the Advanced
Tools **Autounattend XML generator** reads this panel's live selection. Build #3 already
has `AutounattendService.GenerateToFile(..., IReadOnlyList<AppDefinition> selectedWindowsApps, ...)`
ported, but it currently has **no caller** — whatever Stage 2 builds must expose the
equivalent provider or the Advanced Tools wave has nothing to bind to.

### 2. The generated-script path, verified from source

`SoftwareAppService.RemoveWindowsAppsAsync(apps, log, status)` — three steps:

1. **Split on `RemovalScript`** — `scriptApps = apps.Where(a => a.RemovalScript != null)`
   run their own script first (Stage 3); `regularApps = apps.Where(a => a.RemovalScript == null)`
   go to the generator.
2. **`Categorize(regularApps)`** → 4 lists. Order matters, and the branch is **else-if for
   the first three but a separate unconditional `if` for packages**:

```csharp
if (app.CapabilityName != null) capabilities.Add(app.CapabilityName);
else if (app.OptionalFeatureName != null) features.Add(app.OptionalFeatureName);
else if (app.RegistrySubKeyName != null && app.Id == "windows-app-onenote") specialApps.Add("OneNote");
if (app.AppxPackageName != null) packages.AddRange(app.AppxPackageName);
```

   So one definition can land in **both** a capability/feature/special list **and**
   `packages`. `specialApps` is hard-wired to the single literal `"OneNote"` gated on
   `Id == "windows-app-onenote"` — there is no general special-app mechanism. All four
   lists are `.Distinct()`.

3. **Special cases**, computed from `packages` (not from the definitions):

```csharp
bool xboxFix   = packages.Any(p => p is "Microsoft.GamingApp" or "Microsoft.XboxGamingOverlay" or "Microsoft.XboxGameOverlay");
bool teamsKill = packages.Any(p => p.Equals("MSTeams", StringComparison.OrdinalIgnoreCase));
```

   Note `xboxFix` uses an **ordinal, case-SENSITIVE** pattern match while `teamsKill` is
   case-insensitive. (`BloatRemovalScriptGenerator` line 62 has a second, case-INsensitive
   xbox check in its convenience overload — that overload is not the one this path calls.)

4. `BloatRemovalScriptGenerator.GenerateScript(packages, capabilities, features, specialApps, xboxFix, teamsKill)`
   assembles: `AppendHeader` (self-elevating preamble, `ScriptVersion = "2.3"`) →
   `AppendLoggingSetup` (`C:\ProgramData\AkariTool\Logs\BloatRemovalLog.txt`, rotates at
   500 KB) → `AppendRunspaceHelper` (`Invoke-RunspacePool`) → `AppendArrays` (emits
   `$packages` / `$capabilities` / `$optionalFeatures` / `$specialApps` as
   `@(\n    'item'\n)`) → `GetMainRemovalLogic(xboxFix, teamsKill)`.

   Main logic order: `teamsProcessKill` block (if enabled) → discover packages →
   **`Remove-AppxProvisionedPackage` first** (comment: "critical for Win10 —
   `Remove-AppxPackage -AllUsers` fails with 0x80070002 otherwise", 10 threads) →
   `Remove-AppxPackage` (10 threads) → capabilities (single DISM enumeration, then
   `Remove-WindowsCapability`, 5 threads) → `Disable-WindowsOptionalFeature -NoRestart`
   (batch) → special apps (registry uninstall; `'OneNote' { @('OneNote','ONENOTE','ONENOTEM') }`)
   → `xboxRegistryFix` block (if enabled) → `Write-Log "Bloat removal process completed"`.

   `xboxRegistryFix` redirects `HKCR\ms-gamebar` + `ms-gamebarservices` to `systray.exe`
   and writes GameDVR keys, with a **SYSTEM-context branch** that resolves the logged-in
   user's SID from `ProfileList` and writes `HKU\<sid>\…` instead of HKCU — because the
   same script also runs from the SYSTEM startup task.

5. Run: `RunScriptTextAsync(script, "BloatRemoval-Run.ps1", log)` — writes to
   `%TEMP%\AkariTool-{guid:N}-BloatRemoval-Run.ps1`, runs
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<temp>"` hidden, streams
   stdout/stderr to the log, deletes the temp file in `finally`. **The run copy is
   throwaway and is NOT the persisted script.**

**Confirmation dialog — verbatim, and it is NOT the External Apps variant:**

```csharp
var warnings = selected.Where(a => a.HasInstabilityWarning).Select(a => a.Name).ToList();
var msg = $"Remove {selected.Count} item(s) from Windows?\n\n" +
          string.Join(", ", selected.Take(10).Select(a => a.Name)) +
          (selected.Count > 10 ? $" (+{selected.Count - 10} more)" : "") +
          (warnings.Count > 0 ? $"\n\n⚠ {string.Join(", ", warnings)}: removal can affect Windows components that depend on it." : "") +
          "\n\nA startup task keeps these removed after Windows updates.";
if (!await AkariDialogs.ConfirmYesNoAsync(msg, "Remove Windows Apps"))
    return;
```

Title **`Remove Windows Apps`**, Yes/No. Matches Phase 22's description exactly (count,
first 10 + `(+N more)`, ⚠ line, persistence line). It differs from Stage 1's ported
`Uninstall External Apps` dialog in the lead sentence, the title, **and two clauses the
External variant has no equivalent of** — the ⚠ instability line and the startup-task
line. It must be ported as its own string, not adapted.

The ⚠ clause is reachable in Stage 2 for **exactly one** definition — **App Installer**
(`windows-app-app-installer`), i.e. removing winget itself. The other warned definition
is Edge (Stage 3).

### 3. Persistence, in full

**`SaveAndRegisterBloatRemovalAsync(apps, log)` — `private static`, no-arg-return, runs
UNCONDITIONALLY as step 3 of every `RemoveWindowsAppsAsync` call** (outside the
`regularApps.Count > 0` guard):

1. `Directory.CreateDirectory(AkariPaths.ScriptsDirectory)`;
   `scriptPath = C:\ProgramData\AkariTool\Scripts\BloatRemoval.ps1`.
2. If the file exists, the four arrays are **re-parsed back out of the previous script**
   via `ExtractArrayFromScript(existing, "packages" | "capabilities" | "optionalFeatures" | "specialApps")`
   — a `Regex` `\$name\s*=\s*@\(\s*(.*?)\s*\)` (Singleline, IgnoreCase), split on `\n`,
   each line trimmed of `,` `'` `"`. **The generated script IS the database** — there is no
   separate state file.
3. New items come from `Categorize(apps.Where(a => a.RemovalScript == null))` — so
   Edge/OneDrive are **excluded from persistence** even when they were part of the run.
4. Merge is `Union(..., StringComparer.OrdinalIgnoreCase)` per list.
5. **Early out:** if all four merged lists are empty, it `return`s — no file written, no
   task registered.
6. Recompute `xboxFix` / `teamsKill` from the **merged** packages (so the flags reflect
   the cumulative set, not just this run), regenerate, and
   `File.WriteAllTextAsync(scriptPath, merged, Encoding.UTF8)`; logs `[SAVED] {scriptPath}`.
7. Register the task, exact command line from source:

```csharp
var taskCmd =
    "schtasks /Create /F /TN \"AkariTool\\BloatRemoval\" /SC ONSTART /RU SYSTEM /RL HIGHEST " +
    $"/TR \"'{AkariPaths.PowerShellExePath}' -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File '{scriptPath}'\"";
await RunHiddenAsync("cmd.exe", $"/c {taskCmd}", timeoutMs: 30_000);
log("[TASK] BloatRemoval startup task registered");
```

   Fully expanded, what actually reaches `cmd.exe`:

```
cmd.exe /c schtasks /Create /F /TN "AkariTool\BloatRemoval" /SC ONSTART /RU SYSTEM /RL HIGHEST /TR "'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File 'C:\ProgramData\AkariTool\Scripts\BloatRemoval.ps1'"
```

   Confirms Phase 22's `/SC ONSTART /RU SYSTEM /RL HIGHEST`, and adds `/F` (overwrite
   without prompting) and the single-quoted inner paths. `RunHiddenAsync` kills the
   process tree on the 30 s timeout. The whole method body is inside one `try/catch` that
   logs `[ERROR] Failed to persist BloatRemoval script: {ex.Message}` — **a persistence
   failure is swallowed and the removal still reports as done.**

**`RemoveFromSavedScriptAsync(apps, log)` — `public static`, the inverse:**

1. `return`s immediately if `BloatRemoval.ps1` doesn't exist.
2. Re-parses the same four arrays out of the script.
3. `Categorize(apps)` — note **no `RemovalScript == null` filter here**, unlike the save
   path.
4. `RemoveAll(... Contains(..., StringComparer.OrdinalIgnoreCase))` per list.
5. **If all four end up empty:** `File.Delete(scriptPath)` **and**
   `cmd.exe /c schtasks /Delete /F /TN "AkariTool\BloatRemoval"` (30 s), logging
   `[TASK] BloatRemoval script and task removed (nothing left to keep removed)`.
6. Otherwise regenerate with recomputed `xboxFix`/`teamsKill`, rewrite the file, log
   `[SAVED] BloatRemoval.ps1 updated (reinstalled items excluded)`. Same swallowing
   `try/catch`.

**Already ported in build #3? YES — the whole logic layer, and it is callable.** Verified
this phase: `diff` of `Tabs/Software/SoftwareAppService.cs` → **identical**; `diff -rq` of
`Tabs/Software/Removal/` and `Tabs/Software/Catalog/` → **identical**. This is **not** just
the generator without the persistence wrapper — `AkariPaths` (same
`C:\ProgramData\AkariTool\Scripts`, same `powershell.exe` path),
`SaveAndRegisterBloatRemovalAsync`, `RemoveFromSavedScriptAsync`, `RunScriptTextAsync` and
`RunHiddenAsync` are all present at the same line numbers.

Accessibility, exactly:
- `RemoveWindowsAppsAsync` — **public**, callable from a Stage 2 VM.
- `SaveAndRegisterBloatRemovalAsync` — **private**; reachable *only* as step 3 of
  `RemoveWindowsAppsAsync`. A VM cannot (and must not) call it directly, and cannot skip
  it — calling the remove entry point always registers the task.
- `RemoveFromSavedScriptAsync` — **public**, and currently has **no caller in build #3**
  (the only hit is a comment in `ExternalAppsViewModel.cs:148` noting its correct
  absence). Stage 2's install path is what re-introduces the call.

So Stage 2 is, again, **UI-only**.

### 4. The persistence warning line — exactly where it lives

Direct answer to the question as asked: **"Windows Apps" and "Bloatware" are the same
panel** — rail tag `Bloatware`, page header "Windows Apps". There is **one** dialog, not
two, so there is no discrepancy to reconcile.

`grep` over all of build #2 for `startup task` / `stay removed` / `keeps these removed`
finds the string in user-facing text in exactly **two** places, both in this panel:

- **`SoftwareTab.WindowsApps.cs:92`** — the dialog:
  `"\n\nA startup task keeps these removed after Windows updates."`
- **`SoftwareTab.WindowsApps.cs:34`** — the panel subtitle:
  `"Remove pre-installed Windows apps, legacy capabilities, and optional features — or reinstall them. Removed apps stay removed across Windows updates."`

Everywhere else it appears only in C# comments / XML doc / the `[TASK]` log line.

The whole Software tab contains exactly **three** dialog calls, all confirmed by grep:
`Remove Windows Apps` (Yes/No), `Uninstall External Apps` (Yes/No, ported Stage 1),
`Permanent Items` (info, ported Stage 1). **`DebloatTab` has zero dialogs** — its
"OneDrive — Remove" and "Microsoft Edge — Remove" buttons run with no confirmation at all
(Stage 4 concern, noted here because it is the same warning gap).

Both strings must be ported byte-for-byte. The dialog line is the only place a user is
told the action installs a **SYSTEM-privileged ONSTART scheduled task** that will keep
re-running — and even it doesn't say "scheduled task", "SYSTEM", or that it persists until
the app is reinstalled through this same panel. Worth a copy review with isleap during
Stage 2 rather than after.

### 5. Install / Refresh for this panel

Both are the **shared** `SoftwareTab.InstallQueue.cs` methods called with
`isWindowsApps: true`:

- **Install Selected** → `InstallSelectedAsync(_windowsApps, isWindowsApps: true)`.
  **No Yes/No confirmation** — same as AppInstaller. The only dialog is the info-only
  `AkariDialogs.InfoAsync(..., "Permanent Items")` when the selection contains
  `CanBeReinstalled == false` items, which are then filtered out; if that empties the
  selection the method returns. **The one difference from Stage 1's ported version:**

```csharp
// Reinstalled Windows apps must leave the keep-removed script
if (isWindowsApps)
    await SoftwareAppService.RemoveFromSavedScriptAsync(selected, m => Service!.Log(m));
```

  runs after the install loop, inside the same `try`. Skipping it would leave the startup
  task re-removing what the user just reinstalled — on the next boot.
  Install itself routes per definition: `CapabilityName` → `Add-WindowsCapability`,
  `OptionalFeatureName` → DISM, otherwise winget (package-id fallbacks, then msstore id).

- **Refresh** → `RefreshStatusAsync()`, read-only: `GetInstallSnapshotAsync()` then
  `ApplyInstallStatus` + `RefreshBadges`. No confirmation. In net8 one method refreshed
  **both** panels; Stage 1 scoped its copy to External Apps, so Stage 2 needs its own
  (Phase 23 deviation #4).

- **Remove Selected** → the only guarded action; see §2.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Build #3** — recon only; no files created or edited except this log entry.
- **Defender** — no Defender code referenced.
- **Edge / OneDrive removal scripts** — deliberately out of scope (Stage 3).
- **Debloat** — out of scope (Stage 4).

---

## MVVM Phase 25 — Software ▸ Windows Apps (Bloatware) — **COMPLETE (VM sign-off pending)**

Stage 2 of the Software rollout. Build #2 read-only; no Defender code referenced.
Stage 3 (Edge/OneDrive) and Stage 4 (Debloat) untouched.

UI-only, as Phase 24 established: no removal, categorization or persistence logic was
written. Every action calls the already-ported `SoftwareAppService` verbatim.

### New / edited in build #3

- `ViewModels/Software/WindowsAppsViewModel.cs` — the panel. Plain `ObservableObject`,
  **not** a `TweakPageViewModel`.
- `Views/WindowsAppsPage.xaml(.cs)`.
- `ViewModels/Software/AppCardViewModel.cs` — ctor gained `bool isWindowsApps = false`
  and the `IsPermanent` property (net8 gate: `isWindowsApps && !App.CanBeReinstalled`).
  Default keeps the Stage 1 call site unchanged.
- `App.xaml.cs` — `AddSingleton<WindowsAppsViewModel>()`, again deliberately NOT under
  the `TweakPageViewModel` enumeration.
- `MainWindow.xaml.cs` — `["Bloatware"] = typeof(WindowsAppsPage)` + rail sync.

`AppSectionViewModel` was reused unchanged.

### ⚠ CORRECTION to Phase 24's count: the Permanent pill covers 20, not 15

Phase 24 reported 15 `CanBeReinstalled = false` definitions. That number came from a grep
of `WindowsAppCatalog.cs` **only**. Re-extracted per-object across all three catalogs this
phase:

| Catalog | `CanBeReinstalled = false` |
|---|---|
| `WindowsAppCatalog.cs` | 15 |
| `CapabilityCatalog.cs` | **5** |
| `OptionalFeatureCatalog.cs` | 0 |
| **total in scope** | **20** |

The five missed are all in Legacy Capabilities: `capability-internet-explorer`,
`capability-quick-assist`, `capability-wordpad`, `capability-notepad`,
`capability-paint-legacy`. All 20 verified rendering the pill (below). Same lesson as the
Phase 22 follow-up: a count from one file is not a count of the set.

### Sections — three fixed, by SHAPE

Filters are net8's exactly, applied to the merged catalog and each ordered
`OrderBy(Name, OrdinalIgnoreCase)`:

| Section | Filter | Items |
|---|---|---|
| Windows Apps | `CapabilityName == null && OptionalFeatureName == null` | 56 − 2 = **54** |
| Legacy Capabilities | `CapabilityName != null` | **10** |
| Optional Features | `OptionalFeatureName != null` | **7** |
| | | **71** |

Shape filters verified clean against source: `WindowsAppCatalog` has zero
`CapabilityName`/`OptionalFeatureName` occurrences; `CapabilityCatalog` sets
`CapabilityName` on all 10; `OptionalFeatureCatalog` sets `OptionalFeatureName` on all 7.
No definition can fall into two sections.

### ⚠ ONE DELIBERATE DEVIATION FROM net8 — the Stage-3 scope filter

```csharp
_windowsApps.RemoveAll(a => a.RemovalScript != null);
```

net8 renders Edge and OneDrive in this panel and lets `RemoveWindowsAppsAsync` dispatch
them to their dedicated scripts. They are held back to Stage 3, so this one line excludes
them — which also makes the `scriptApps` branch of `RemoveWindowsAppsAsync` **unreachable
from this panel**, i.e. Stage 2 cannot fire either removal script. **Revert this single
line in Stage 3.** It is commented as such in the source. This is why the panel shows 71,
not net8's 73.

### Remove — dialog ported byte-for-byte

Verified by `diff` of the extracted expression against
`Akari-Tool/Tabs/Software/SoftwareTab.WindowsApps.cs:87-92` → **BYTE-IDENTICAL**:

```csharp
var warnings = selected.Where(a => a.HasInstabilityWarning).Select(a => a.Name).ToList();
var msg = $"Remove {selected.Count} item(s) from Windows?\n\n" +
          string.Join(", ", selected.Take(10).Select(a => a.Name)) +
          (selected.Count > 10 ? $" (+{selected.Count - 10} more)" : "") +
          (warnings.Count > 0 ? $"\n\n⚠ {string.Join(", ", warnings)}: removal can affect Windows components that depend on it." : "") +
          "\n\nA startup task keeps these removed after Windows updates.";
if (!await _dialogs.ConfirmYesNoAsync("Remove Windows Apps", DialogText(msg)))
    return;
```

Title `Remove Windows Apps`, Yes/No, content a wrapping `TextBlock` `MaxWidth = 440`.
Its own string — Stage 1's `Uninstall External Apps` copy was not reused or adapted.
Per isleap, net8's wording is ported as-is and NOT strengthened.

Subtitle also `diff`-verified **BYTE-IDENTICAL**:

> Remove pre-installed Windows apps, legacy capabilities, and optional features — or reinstall them. Removed apps stay removed across Windows updates.

The ⚠ clause is reachable in Stage 2 for exactly one definition — **App Installer**
(`windows-app-app-installer`); the other warned definition is Edge, excluded above.

Removal calls `SoftwareAppService.RemoveWindowsAppsAsync(selected, log, status)` and
nothing else. Its step 3 (`SaveAndRegisterBloatRemovalAsync`, private, outside the
`count > 0` guard) always runs — not called separately, not bypassed. The `status` sink is
marshalled with `App.DispatcherQueue.TryEnqueue`, as net8 did.

### Install — the reinstall-unpersist wiring

No Yes/No confirmation (net8 parity); only the info-only `Permanent Items` notice. After
the install loop, inside the same `try`:

```csharp
await SoftwareAppService.RemoveFromSavedScriptAsync(selected, m => _tool.Log(m));
```

`WindowsAppsViewModel.cs:254` — the **first and only** call site of this method in build
#3 (Phase 24 recorded it had none). Without it the SYSTEM ONSTART task would silently
re-remove a just-reinstalled app on the next boot.

**net8 asymmetry PRESERVED VERBATIM, again deliberately not "fixed":** the save path
filters `apps.Where(a => a.RemovalScript == null)` before `Categorize`; this inverse path
does not. net8 also passes the full pre-loop `selected` list, including apps whose install
failed — kept. Likewise untouched: `Categorize`'s else-if/if asymmetry, `specialApps`
hard-wired to `Id == "windows-app-onenote"`, and the case-SENSITIVE `xboxFix` vs
case-INsensitive `teamsKill` mismatch.

### Advanced Tools provider hook

```csharp
public List<AppDefinition> GetSelectedWindowsApps() =>
    _windowsApps.Where(a => a.IsSelected).ToList();
```

`WindowsAppsViewModel.cs:164`. net8's equivalent was
`adv.SetSelectedAppsProvider(() => _software.GetSelectedWindowsApps())`
(`MainWindow.xaml.cs:254`). `List<AppDefinition>` satisfies
`AutounattendService.GenerateToFile`'s `IReadOnlyList<AppDefinition>` parameter directly.
Returns a snapshot; call on the UI thread before any `Task.Run` (CLAUDE.md cross-thread
rule). **No Advanced Tools UI was built and nothing calls this in-app yet** — the hook
exists so that wave has something to bind to.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```

Zero warnings, zero errors, first attempt.

### Registration — guard UNCHANGED

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP]   Gaming [0..130) 130 rows — Gaming & Performance
[WARMUP]   Sound [130..135) 5 rows — Sound
[WARMUP]   Notifications [135..151) 16 rows — Notifications
[WARMUP]   Update [151..163) 12 rows — Windows Updates
[WARMUP]   Privacy [163..252) 89 rows — Privacy & Security
[WARMUP]   Customize [252..403) 151 rows — Customize
[WARMUP]   Power [403..439) 36 rows — Power
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

### Verification (read-only, de-elevated + UIA; NOTHING removed or reinstalled)

- **Renders** — title "Windows Apps", the verbatim subtitle, Remove Selected (accent) /
  Install Selected / Refresh in net8's order, "0 selected", search, select-all row, then
  the "Windows Apps" section with real cards (3D Viewer, AI Manager, AI Workload Packages,
  Alarms & Clock, App Installer …).
- **All three sections** — probed one representative each: Windows Apps/Cortana,
  Legacy Capabilities/Internet Explorer, Optional Features/Windows Sandbox — header AND
  app present, 3/3.
- **Permanent pill: 20 / 20 OK** — every `CanBeReinstalled = false` definition across both
  catalogs renders it.
- **Negative control** — Calculator renders with **no** Permanent pill.
- **Stage-3 exclusions absent** — searching `Microsoft Edge` → not present;
  `OneDrive` → not present.
- No `[ERROR]` in the log; app alive throughout.
- **Remove Selected / Install Selected were NOT clicked** — destructive, isleap's VM step.

### VM checklist (Phase 25 — for isleap; DESTRUCTIVE, VM only)

- [ ] Select 1 harmless app → **Remove Selected** → dialog reads
      `Remove 1 item(s) from Windows?` + name + the startup-task line; **No** cancels with
      nothing run.
- [ ] Select **App Installer** → the ⚠ line appears naming it.
- [ ] Select 12 → first 10 listed then `(+2 more)`.
- [ ] **Yes** → BloatRemoval-Run.ps1 output streams to the log dock, then
      `[SAVED] C:\ProgramData\AkariTool\Scripts\BloatRemoval.ps1` and
      `[TASK] BloatRemoval startup task registered`.
- [ ] `schtasks /Query /TN "AkariTool\BloatRemoval"` shows an ONSTART SYSTEM task.
- [ ] **Reinstall the same app** → after the install loop the saved script no longer lists
      it; if it was the only entry, script deleted and
      `[TASK] BloatRemoval script and task removed (nothing left to keep removed)`.
- [ ] **Reboot** → the removed-and-reinstalled app is STILL installed (this is the whole
      point of the unpersist wiring).
- [ ] After all of the above the `[WARMUP]` guard still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Edge / OneDrive** — excluded (Stage 3); revert the one-line scope filter there.
- **Debloat** — untouched (Stage 4).
- **Advanced Tools UI** — not built; only the provider hook exists.
- **Backup, bespoke Home/About/Tools** — untouched.

---

## MVVM Phase 26 — Edge/OneDrive dedicated removal (Stage 3) — **SHOW ONLY (awaiting isleap go-ahead before revert)**

Highest-risk stage. Recon of points 1–4 only; **the exclusion line is NOT reverted and
nothing was rebuilt** — isleap reviews first. Build #2 read-only; no Defender code
referenced; Stage 4 (Debloat) untouched.

### 1. The exclusion line, and what re-enabling it actually does

`ViewModels/Software/WindowsAppsViewModel.cs:113`, inside `Build()`:

```csharp
_windowsApps.RemoveAll(a => a.RemovalScript != null);
```

Removes exactly the 2 `RemovalScript`-backed definitions (Edge, OneDrive) from the
panel's working list before sectioning. Reverting = deleting this one line (and its 6-line
comment block, 107–113).

**This is a true one-line revert for both rendering and removal routing — no other Stage-2
code assumed these items were absent.** Traced every touch point:

- **Sectioning** (`Build()`): both have `CapabilityName == null && OptionalFeatureName ==
  null`, so both land in the first section, **"Windows Apps"**, ordered by name. No shape
  special-case needed.
- **Card building** (`AddSection` → `AppCardViewModel`): the card VM never references
  `RemovalScript`. Icons resolve via `AppIconService` from `AppxPackageName`
  (`Microsoft.MicrosoftEdge.Stable` / `Microsoft.OneDriveSync`) — the existing
  `windows-app-*` path.
- **Selection / count**: the `PropertyChanged` subscription loops over `_windowsApps`, so
  after revert Edge/OneDrive are included automatically.
- **Removal routing**: `RemoveSelectedAsync` does **no categorization** — it passes the
  raw `selected` list to `SoftwareAppService.RemoveWindowsAppsAsync`, whose existing
  split does the work:

  ```csharp
  var scriptApps  = apps.Where(a => a.RemovalScript != null).ToList();   // Edge, OneDrive
  var regularApps = apps.Where(a => a.RemovalScript == null).ToList();
  ```

  `scriptApps` each run `app.RemovalScript!()` written to a temp `.ps1` named by
  `app.Id.Contains("edge") ? "EdgeRemoval.ps1" : app.Id.Contains("onedrive") ?
  "OneDriveRemoval.ps1" : …`. Both Ids match (`windows-app-edge`, `windows-app-onedrive`).
  `SoftwareAppService.cs` is byte-identical to build #2 (Phase 22/24) — this branch is
  correct and untouched.

  ⚠ Minor naming footgun to be *aware* of (not a bug today): the router keys on
  `Id.Contains("edge")`. `windows-app-edge-game-assist` also contains "edge" — but it has
  **no** `RemovalScript`, so it never enters `scriptApps`. Only if a future
  RemovalScript-backed "edge-*" item were added would the substring match mis-name its
  temp file. Out of scope; noted.

**Verdict: genuine one-line revert.** Nothing in Stage 2 implicitly depended on their
absence beyond the filter itself.

### 2. HasInstabilityWarning — Edge YES, OneDrive NO (confirmed from source)

From `WindowsAppCatalog.cs`, full definitions extracted:

| | `HasInstabilityWarning` | `GroupName` | lands in section |
|---|---|---|---|
| `windows-app-edge` | **`true`** (explicit) | Browsers | Windows Apps |
| `windows-app-onedrive` | **absent ⇒ false** | System | Windows Apps |

(`GroupName` is irrelevant on this panel — it sections by shape, not group.)

The ⚠ clause is already correct for this, no change needed. It reads
`selected.Where(a => a.HasInstabilityWarning)`, so:
- Edge selected → ⚠ names **"Microsoft Edge"**.
- OneDrive-only selection → **no ⚠ clause** (correct — OneDrive doesn't carry the flag).
- Both selected → ⚠ names Edge only.

### 3. Persistence — and a finding that contradicts the prompt's assumption

**3a. BloatRemoval.ps1 correctly excludes Edge/OneDrive.**
`SaveAndRegisterBloatRemovalAsync` categorizes only `apps.Where(a => a.RemovalScript ==
null)`, so the dedicated-script items never enter the persisted arrays. If a removal run
contains **only** Edge/OneDrive, all four merged lists are empty and the method hits its
early `return` — **no `BloatRemoval.ps1` written, no `AkariTool\BloatRemoval` ONSTART task
registered.** Confirmed. Removing Edge does not arm a BloatRemoval task to re-remove it.

**3b. ⚠ BUT Edge's OWN script self-registers a separate scheduled task.** The prompt asked
me to confirm "Edge's dedicated script presumably has no startup-task re-run mechanism of
its own." **It does have a scheduled task — though not a re-removal one.**
`EdgeRemovalScript.cs:328–335`:

```powershell
$repairTaskName = "OpenWebSearchRepair"
$repairAction  = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "… OpenWebSearchRepair.ps1 …"
$repairTrigger = New-ScheduledTaskTrigger -AtLogon
$repairPrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $repairTaskName -TaskPath "\AkariTool\" … -Force
```

What it is and is NOT:
- **Task:** `\AkariTool\OpenWebSearchRepair`, SYSTEM, RunLevel Highest, **AtLogon** (not
  ONSTART). Separate name and trigger from `\AkariTool\BloatRemoval`.
- **Purpose:** it does **not** re-remove Edge or re-run the uninstaller. It re-asserts the
  **OpenWebSearch protocol redirect** (AveYo's `ie_to_edge_stub`, cited at
  `EdgeRemovalScript.cs:37`) — if Edge later overwrites the `microsoft-edge:` /
  `MSEdgeHTM` shell-open-command handlers, the logon task points them back at the stub
  (`EdgeRemovalScript.cs:308–335`). Files live in `C:\ProgramData\AkariTool\OpenWebSearch\`.
- **Consequence:** this task **survives an Edge reinstall** and keeps redirecting the
  Edge protocol to the search stub. It is a genuine second persistence path, independent
  of BloatRemoval, that Stage 3 turns on the moment Edge removal becomes reachable.

**3c. OneDrive's script registers NO task.** It only *un*-registers OneDrive's own tasks
(`OneDriveRemovalScript.cs:425 Unregister-ScheduledTask`), deletes the Default-user Run
key (`:449 reg delete …\Run /v OneDriveSetup`), and force-deletes locked binaries via
`MoveFileEx(..., MOVEFILE_DELAY_UNTIL_REBOOT)` (`:83–87`) — a **reboot-pending delete**,
which is a boot-time side effect but not a recurring task. No self-persistence.

**Net for §3:** removing Edge/OneDrive does not register a BloatRemoval re-removal task.
Edge *does* leave behind a SYSTEM AtLogon `OpenWebSearchRepair` task by design (protocol
redirect maintenance), and OneDrive leaves a reboot-pending file delete. isleap should
weigh 3b specifically — it's the one durable, self-reinstating artifact and it is not
mentioned anywhere in the panel's UI copy.

### 4. Reinstallability — both `CanBeReinstalled = true`

Confirmed from source: **both** Edge and OneDrive have `CanBeReinstalled = true`.
Consequences on this panel:

- **No "Permanent" pill** for either (gate is `isWindowsApps && !CanBeReinstalled`).
- **Install action is live for both.** `InstallAppAsync` routes via winget:
  Edge `WinGetPackageId = ["Microsoft.Edge"]` (+ `MsStoreId "XPFFTQ037JWMHS"` fallback),
  OneDrive `WinGetPackageId = ["Microsoft.OneDrive"]`. So winget genuinely can put the
  binaries back — the flag is meaningful, not cosmetic.
- **⚠ but reinstall is only PARTIAL for Edge.** A winget reinstall restores the Edge
  binary; it does **not** undo the `OpenWebSearchRepair` logon task or the
  `ie_to_edge_stub` IFEO/protocol redirect from §3b. And the panel's install path
  (`RemoveFromSavedScriptAsync`) only edits `BloatRemoval.ps1`, which never contained
  Edge. So "reinstall Edge" via this panel leaves the protocol still redirected to the
  search stub until that task/redirect is removed by other means. Worth surfacing to
  isleap; net8 has the same behaviour (no undo path for OpenWebSearch), so this is a
  *description*, not a regression, and I did not change anything.

### Cross-cutting copy inaccuracy (net8-inherited, flagging not fixing)

The Remove dialog **unconditionally** appends "A startup task keeps these removed after
Windows updates." For an **Edge/OneDrive-only** selection, §3a shows **no BloatRemoval
task is registered**, so that sentence is inaccurate for that selection (Edge separately
gets the *different* OpenWebSearchRepair logon task; OneDrive gets none). net8's wording is
identically unconditional, and isleap chose to port net8 copy verbatim — so this stays as
recon, not a change. Raising it because Stage 3 is the first time a selection can consist
solely of items excluded from the BloatRemoval persistence the line describes.

### If isleap confirms 1–4 → remaining Stage-3 steps (NOT done yet)

1. Delete the exclusion line (`WindowsAppsViewModel.cs:107–113`).
2. Rebuild (VS MSBuild) + de-elevated build; report literal output.
3. Read-only UIA verify: Edge + OneDrive render in "Windows Apps"; **neither** shows the
   Permanent pill (both reinstallable); Edge shows the **Warning** pill, OneDrive does
   not; selecting Edge makes the ⚠ dialog clause name "Microsoft Edge".
4. `[WARMUP]` still tiles `[0..439)` (panel registers nothing).
5. **Never** click Remove Selected with Edge/OneDrive — isleap's disposable-VM step only,
   NOT the de-elevated copy.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Build #3** — recon only; **no files created or edited except this log entry.** The
  exclusion line is still in place.
- **Defender** — no Defender code referenced.
- **Debloat** — untouched (Stage 4).

---

## MVVM Phase 26 (continued) — revert + verify — **COMPLETE (VM sign-off pending)**

isleap reviewed findings 1–4 and approved. Stage 3 exclusion reverted. Build #2
read-only; no Defender code touched; Stage 4 (Debloat) untouched.

### Change

One line removed from `WindowsAppsViewModel.Build()` — the Stage-2 scope filter
`_windowsApps.RemoveAll(a => a.RemovalScript != null)` (was line 113), replaced by a
comment pointing at the standalone note below. **No other code changed** — Phase 26
points 1/2/4 confirmed no other Stage-2 code assumed Edge/OneDrive were absent. Per
isleap, **no new UI/warning copy was added** for the OpenWebSearchRepair task (3b): net8's
dialog wording stays verbatim, same decision as the earlier BloatRemoval-warning call.

### ⚠⚠ STANDALONE NOTE (Phase 26 ▸ 3b) — Edge leaves a SECOND, self-reinstating persistence path

**This is deliberately its own heading so it is findable later. It is a known net8
behaviour gap, logged for a possible future product decision, NOT fixed in this
migration.**

Removing **Microsoft Edge** through the Windows Apps panel runs
`EdgeRemovalScript.GetScript()`, which — separately from the BloatRemoval mechanism —
registers its own scheduled task:

- **Task:** `\AkariTool\OpenWebSearchRepair` (`EdgeRemovalScript.cs:328–335`).
- **Principal / trigger:** SYSTEM, `RunLevel Highest`, **AtLogon** (not ONSTART).
- **Purpose:** re-asserts the OpenWebSearch `ie_to_edge_stub` protocol redirect (AveYo,
  cited `EdgeRemovalScript.cs:37`) — if Edge later rewrites the `microsoft-edge:` /
  `MSEdgeHTM` shell-open handlers, this logon task points them back at the search stub
  under `C:\ProgramData\AkariTool\OpenWebSearch\`. It does **not** re-remove Edge or re-run
  the uninstaller.

Why it matters, made explicit:

1. It is **distinct from** `\AkariTool\BloatRemoval` (different name, different trigger,
   different purpose). §3a already confirmed Edge/OneDrive are excluded from
   `BloatRemoval.ps1` by the `RemovalScript == null` filter, so removing Edge registers
   **no** BloatRemoval task — but it does register **this** one.
2. It **survives an Edge reinstall through this panel.** The panel's Install path restores
   the Edge binary via winget but does nothing to `OpenWebSearchRepair` or the
   `ie_to_edge_stub` IFEO/protocol redirect. So "reinstall Edge" here is **partial**: the
   browser returns, but its protocol stays redirected to the stub until that task +
   redirect are removed by other means (which no Akari UI currently offers).
3. **OneDrive has no equivalent** — its script registers no task; it only *un*-registers
   OneDrive's own tasks, deletes the Default-user `Run\OneDriveSetup` key
   (`OneDriveRemovalScript.cs:449`), and force-deletes locked binaries via
   `MoveFileEx(MOVEFILE_DELAY_UNTIL_REBOOT)` (`:83–87`, a reboot-pending delete, not a
   recurring task).

The Remove dialog's unconditional line "A startup task keeps these removed after Windows
updates." is therefore imprecise for an Edge/OneDrive-only selection (no BloatRemoval task
is registered; Edge separately gets the different OpenWebSearchRepair task; OneDrive gets
none). Wording left verbatim by decision.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```

Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### Registration — guard UNCHANGED

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP]   Gaming [0..130) 130 rows — Gaming & Performance
[WARMUP]   Sound [130..135) 5 rows — Sound
[WARMUP]   Notifications [135..151) 16 rows — Notifications
[WARMUP]   Update [151..163) 12 rows — Windows Updates
[WARMUP]   Privacy [163..252) 89 rows — Privacy & Security
[WARMUP]   Customize [252..403) 151 rows — Customize
[WARMUP]   Power [403..439) 36 rows — Power
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

Edge/OneDrive register nothing with TweakRegistry (they're `AppDefinition`s, not tweaks),
so 439 is unchanged — as expected.

### Panel count — back to 73

Catalog totals (grep-confirmed): 56 windows-apps + 10 capabilities + 7 features = **73**,
with the exclusion filter now removing 0. Phase 25 measured **71** with the filter active;
removing it restores exactly the **2** `RemovalScript`-backed items
(`grep -c RemovalScript` = 2) → **73**. Both restored items individually confirmed
rendering in the panel via targeted search (below).

(Live UIA scroll-enumeration realized only 19 distinct cards — that is `ItemsRepeater`
virtualization materializing a viewport subset, not a panel total; it is not evidence of
the count either way. The 73 is catalog arithmetic + the two targeted-render confirmations.)

### Verification (read-only, de-elevated + UIA; NOTHING removed or reinstalled)

- **Edge card renders** — `present=True`, **Warning pill present**, **Permanent pill
  absent** (`CanBeReinstalled = true`). ✓
- **OneDrive card renders** — `present=True`, **no Warning pill**, **no Permanent pill**
  (`CanBeReinstalled = true`, `HasInstabilityWarning` absent). ✓
- **Dialog preview, Edge selected alone** (read the ContentDialog text, then pressed **No**
  — Remove never confirmed):
  ```
  Remove 1 item(s) from Windows?

  Microsoft Edge

  ⚠ Microsoft Edge: removal can affect Windows components that depend on it.

  A startup task keeps these removed after Windows updates.
  ```
  ⚠ clause present and names **Microsoft Edge**. ✓
- **Dialog preview, OneDrive selected alone** (read text, pressed **No**):
  ```
  Remove 1 item(s) from Windows?

  OneDrive

  A startup task keeps these removed after Windows updates.
  ```
  **No ⚠ clause** — correct (OneDrive doesn't carry the flag). ✓
- Both dialog previews were dismissed with **No**; no `[ERROR]` in the log; app alive
  throughout.
- **Remove Selected was NEVER confirmed for Edge or OneDrive.** The destructive round-trip
  (Edge: process kills, servicing-key mutation, AppX AllUsers removal, EdgeUpdate service
  + task unregistration; OneDrive: reboot-pending forced file deletion) is exclusively
  isleap's step, on a **disposable VM** — not this de-elevated copy.

### VM checklist (Phase 26 — for isleap; DESTRUCTIVE, disposable VM ONLY)

- [ ] On a throwaway VM, select **OneDrive** → Remove → Yes → OneDriveRemoval.ps1 output
      streams; confirm OneDrive gone after the pending reboot completes the locked-file
      deletes.
- [ ] Select **Microsoft Edge** → Remove → Yes → EdgeRemoval.ps1 output streams; confirm
      Edge gone, and confirm the `\AkariTool\OpenWebSearchRepair` task now exists
      (`schtasks /Query /TN "AkariTool\OpenWebSearchRepair"`).
- [ ] Reinstall Edge via **Install Selected** → binary returns, but confirm the protocol
      is still redirected to the stub (this is the §3b partial-reinstall behaviour — expected,
      not a bug).
- [ ] Confirm removing Edge/OneDrive did **not** create `\AkariTool\BloatRemoval` or write
      `C:\ProgramData\AkariTool\Scripts\BloatRemoval.ps1` (they're excluded from that path).
- [ ] `[WARMUP]` still tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **Debloat** — untouched (Stage 4).
- **OpenWebSearchRepair copy gap (3b)** — logged above, deliberately not fixed.
- **Advanced Tools UI / Backup / bespoke Home-About-Tools** — untouched.

---

## MVVM Phase 27 — Debloat (SHOW ONLY)

Stage 4 recon, the last Software-tab stage. **Nothing created or edited in build #3;
build #2 read-only; no Defender code referenced.** All script bodies below were read from
source this phase.

### 1. Files, and build #3 status

**Build #2 UI — two files, `Tabs/Debloat/`:**
- `DebloatTab.xaml` (18 lines) — `BaseTab` shell, a single `RootPanel` StackPanel.
- `DebloatTab.xaml.cs` (138 lines) — `Build()` declares 3 groups of tuples; `BuildGroup()`
  renders each as a card of Run/Undo rows.

**Payload scripts** live in `Scripts/*.ps1`, embedded in the assembly
(`<EmbeddedResource Include="Scripts\*.ps1" />`). 20 entries → 20 run scripts + 16 undo
scripts (4 have no undo).

**Build #3 status — re-confirmed, Phase 22's finding holds:** no `Tabs/Debloat/` directory,
no `DebloatTab`, no `DebloatViewModel`. The three build-#3 "Debloat" hits are unrelated:
`MainWindow.xaml:84` (the rail tag, currently → `PlaceholderPage`),
`AutounattendService.Tweaks.cs:30` (an `EdgeDebloat.ps1` unattend option — see §4), and
two `PostInstallService.cs` URL strings. **BUT all 36 payload scripts already exist
byte-identical in build #3's `Scripts/` folder** (line-count matched every one, both
builds) and are already embedded — so like Stages 1–3, the payload layer is present and
Stage 4 would be UI-only.

### 2. Full inventory + per-script risk

Confirmed shape (Phase 22 accurate): **`(Title, Description, RunScript, UndoScript)` tuples,
not `AppDefinition` cards.** Each row = a label + Run button (+ Undo button when the 4th
tuple field is non-empty). Run → `Service.RunWithTracking(new ScriptAction(run), title,
AppliedTweaks)`; Undo → `Service.RunAction(new ScriptAction(undo))`. `ScriptAction` →
`RunScript` extracts the embedded `.ps1` to `%TEMP%` and runs
`powershell -ExecutionPolicy Bypass -File` (no `-NoProfile`), then deletes the temp file.

**Group A — "Privacy & Telemetry" (11 entries). All registry/policy writes, reversible,
low–moderate. Same class as ordinary Privacy/Customize tweaks.**

| Entry | Run does | Undo |
|---|---|---|
| Telemetry — Disable | HKLM Policies `AllowTelemetry`/`MaxTelemetryAllowed`=0 **+ disables & stops the `DiagTrack` service** | ✓ reverses |
| Activity History — Disable | 3 HKLM Policies\System DWORDs =0 | ✓ |
| Location Tracking — Disable | 4 HKLM writes (ConsentStore Deny, Sensor override, `lfsvc` config, Maps) | ✓ |
| PS7 Telemetry — Disable | sets **Machine** env var `POWERSHELL_TELEMETRY_OPTOUT=1` | ✓ |
| Windows AI — Disable | **see Group B — this one is destructive**, listed here by net8's grouping | partial |
| Consumer Features — Disable | HKLM CloudContent `DisableWindowsConsumerFeatures`=1 | ✓ |
| Background Apps — Disable | HKCU `BackgroundAccessApplications GlobalUserDisabled`=1 | ✓ |
| Store Search — Disable | **`icacls store.db /deny Everyone:F`** (ACL mutation, not registry) | ⚠ **mismatched** — undo sets HKCU `BingSearchEnabled`=1 and does **NOT** re-grant the ACL |
| Delivery Optimization — Disable | HKLM Policies `DODownloadMode`=0 | ✓ |
| Device Companion Apps — Block | HKLM Policies `PreventDeviceMetadataFromNetwork`=1 | ✓ |
| WPBT — Disable | HKLM Session Manager `DisableWpbtExecution`=1 | ✓ |

(`WindowsAI` is placed in this group by net8 but belongs in B by risk.)

**Group B — "Apps & Components" (5 entries). This is where the destructive, Edge/OneDrive-class
scripts live.**

| Entry | Run does | Reversibility |
|---|---|---|
| **Unwanted Apps — Remove** (`Debloat.ps1`, 77 ln) | `Remove-AppxPackage -AllUsers` + `Remove-AppxProvisionedPackage` for **19 named packages** (BingNews/Weather, Clipchamp, Todos, PowerAutomate, Solitaire, SoundRecorder, StickyNotes, DevHome, Paint, OutlookForWindows, Alarms, GetHelp, ZuneMusic, QuickAssist, MSTeams …) **+ 8 wildcard families** (`*Xbox*`, `*GamingApp*`, `*Edge*` stub, Copilot, AIX, WebExperience, Cortana `549981C3F5F10`, YourPhone, CrossDevice) **+ legacy Teams uninstall & folder delete** | ⚠ **Undo is a no-op** — `Debloat-Undo.ps1` only prints "cannot be undone, use the Store / DISM RestoreHealth" |
| **OneDrive — Remove** (`RemoveOneDrive.ps1`, 23 ln) | `icacls` deny, `OneDriveSetup.exe /uninstall`, **`Stop-Process Explorer`**, `Remove-Item` LocalAppData + ProgramData OneDrive folders, disable `OneSyncSvc` | Undo = `winget install Microsoft.OneDrive` (✓ real) |
| Microsoft Edge — Debloat (`EdgeDebloat.ps1`, 28 ln) | 17 HKLM Edge **policy** DWORDs (disable shopping/rewards/telemetry/first-run…). No removal. | ✓ registry, reversible |
| **Microsoft Edge — Remove** (`RemoveEdge.ps1`, 27 ln) | finds Edge `setup.exe`, clears EdgeUpdate uninstall-block registry flags, runs `setup.exe --uninstall --system-level --force-uninstall` | ⚠ **no undo** (empty tuple field) |
| **Widgets — Remove** (`Widgets.ps1`) | `Stop-Process Widgets` + `Remove-AppxPackage -AllUsers` (WidgetsPlatformRuntime, WebExperience) | partial — undo `Add-AppxPackage` only works if a per-user copy survives |

**Group C — "Cleanup" (4 entries). No undo on any.**

| Entry | Run does | Risk |
|---|---|---|
| Create Restore Point (`RestorePoint.ps1`) | `Enable-ComputerRestore C:` + freq=0 + `Checkpoint-Computer` | **protective** (creates, doesn't delete). Minor: description string still says "IsleapTool Restore Point". |
| Disk Cleanup — Run (`DiskCleanup.ps1`) | `cleanmgr /VERYLOWDISK` + **`Dism … /StartComponentCleanup /ResetBase`** | moderate — `/ResetBase` makes already-installed updates un-uninstallable (standard, but irreversible) |
| Temporary Files — Remove (`TempFiles.ps1`) | `Remove-Item -Recurse -Force` on `%TEMP%`, `%SystemRoot%\Temp`, `Prefetch` | low (cache), but real file deletion |
| O&O ShutUp10++ — Run (`OOSU.ps1`) | **downloads `OOSU10.exe` from oo-software.com and launches it** | network fetch + execute of an external binary (reputable vendor, but the classic download-and-run pattern) |

**Destructive, Edge/OneDrive-class:** `Debloat.ps1`, `RemoveOneDrive.ps1`, `RemoveEdge.ps1`,
`Widgets.ps1`, `WindowsAI.ps1` (AppX AllUsers removal + `WSAIFabricSvc` disable +
`Disable-WindowsOptionalFeature Recall`). `TempFiles.ps1`/`DiskCleanup.ps1` are file-level
destructive but low-stakes. `OOSU.ps1` is the lone network-download-and-execute.

### 3. Confirmation status — CONFIRMED: ZERO dialogs, zero guards

`grep -n "ContentDialog|ConfirmYesNo|ConfirmWarning|ConfirmOkCancel|ConfirmContent|AkariDialogs|InfoAsync"`
over `DebloatTab.xaml.cs` → **empty.** Every Run and Undo fires immediately on click. No
count, no name list, no ⚠ line, nothing — not even for `Debloat.ps1` (irreversibly removes
27 package families with a no-op undo), `RemoveEdge.ps1`, or `RemoveOneDrive.ps1`.

**This is the least-guarded destructive surface in the app.** For contrast, the same Edge
and OneDrive removals reached through the Bloatware panel (Stages 2–3) go through the
`Remove Windows Apps` Yes/No dialog with the ⚠ instability clause; reached through Debloat
they have **no gate at all**. The three highest-risk unguarded entries:
`Debloat.ps1` (bulk irreversible), `RemoveEdge.ps1` (no undo), `RemoveOneDrive.ps1`
(Stop-Process Explorer + folder delete). This is exactly the A/B decision isleap made for
System Restore and Revert-to-Balanced, now for the riskiest surface yet.

### 4. Redundancy with already-ported paths — extensive

| Debloat entry | Overlaps | Nature |
|---|---|---|
| `RemoveOneDrive.ps1` | **OneDriveRemovalScript** (Bloatware Stage 3, 465 ln) | same capability, **different & weaker** impl (23-ln CTT vs Winhance); Bloatware path also persists via startup task, Debloat does not |
| `RemoveEdge.ps1` | **EdgeRemovalScript** (Bloatware Stage 3, 685 ln) | same capability, different impl; Debloat's lacks the OpenWebSearchRepair protocol-redirect + service/task handling |
| `Debloat.ps1` (27 families) | **WindowsAppCatalog** (Bloatware) | many packages appear in both (Paint, Alarms, Cortana, Xbox, ZuneMusic, QuickAssist, Widgets/WebExperience…). Bloatware = per-app + persistent keep-removed task; Debloat = bulk one-shot, **no persistence**, no-op undo |
| `WindowsAI.ps1` | Bloatware AI packages (`client-copilot`, `client-aix`, `ai-manager`, `ai-workloads`, `copilot-plus-pc`) **and** Privacy-tab AI content | triple overlap on AI disable/removal |
| `Widgets.ps1` | `Debloat.ps1`'s own `*WebExperience*` family (**self-overlap**) + Bloatware | Widgets removed by two Debloat entries at once |
| `EdgeDebloat.ps1` | **`AutounattendService.Tweaks.cs:30` `edge-debloat`** (already in build #3) | the *same* script is already an Autounattend option |
| `RestorePoint.ps1` | `RestorePointHelper.cs` + the Quick-Actions "Create restore point" already on ported tweak pages | same capability already wired |
| `Telemetry` / `Activity History` / `Location` / `Delivery Optimization` / `Device Metadata` | Privacy tab (`AllowTelemetry`), Gaming ▸ System Services (`DiagTrack`), `PlaybookTweaks.Registry.cs` (`EnableActivityFeed`, `DODownloadMode`, `PreventDeviceMetadataFromNetwork`) | policy already expressed as catalog tweaks elsewhere |
| Consumer Features / WPBT / Background Apps | no match found | **net-new** capability, no overlap |

Takeaway for isleap: **most of Debloat duplicates capability already ported into the
catalog tabs (Privacy, Gaming services, Update, Bloatware) and the Stage-3 removal scripts**
— but as *unguarded, non-persistent, weaker-undo* one-shot scripts. The genuinely
Debloat-unique items are a short list: Consumer Features, WPBT, Background Apps, PS7
Telemetry, Store Search, Disk Cleanup, Temp Files, O&O ShutUp10, and the bulk
`Debloat.ps1` convenience itself. isleap may want Stage 4 to **exclude** the items already
covered better elsewhere (both OneDrive/Edge removals, and possibly `Debloat.ps1` vs the
Bloatware catalog) rather than ship two code paths to the same destructive action with
different guarantees.

### 5. Data model

**Hardcoded C# tuples** in `DebloatTab.Build()` — three `(string Title, string Desc, string
Script, string Undo)[]` arrays passed to `BuildGroup()`. **Not** `TweakDefinition`, **not**
`AppDefinition`, **no** TweakRegistry, **no** Backup/Restore participation, **no** Id.
The identity of an entry is its **run-script filename string** (e.g. `"Telemetry.ps1"`) —
the same "identified only by a name string" shape that Scheduled Tasks turned out to have.
`AppliedTweaks` (the tracked-titles list) records the Title string when Run is clicked; that
is the only state. Porting is therefore a straight data+template job (like the tuple-driven
tabs), with **no Id-preservation concern** — there are no Ids.

### Recommendation surface for isleap's decision (not acted on)

1. **Confirmation A/B:** given §3, a dialog on at least the Group-B removals + `Debloat.ps1`
   is the obvious ask; net8 ships none. Verbatim-no-dialog vs add-guard is isleap's call.
2. **Redundancy (§4):** decide whether Stage 4 drops the OneDrive/Edge-removal and bulk
   package rows that Bloatware/Stage-3 already cover better, to avoid two divergent paths.
3. **Two real net8 bugs surfaced, for the record (not fixed):** `StoreSearch` undo does not
   reverse its `icacls` deny; `Debloat.ps1`'s Undo is a no-op while the UI shows an Undo
   button implying reversibility.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Build #3** — recon only; no files created or edited except this log entry.
- **Defender** — no Defender code referenced.
- No extraction, no rail wiring, no `DebloatViewModel` — awaiting isleap's confirmation /
  redundancy decision.

---

## MVVM Phase 28 — Software ▸ Debloat (Stage 4) — **COMPLETE (VM sign-off pending)**

Final Software-tab stage. **This closes the Software tab: Stages 1–4 (AppInstaller,
Bloatware, Edge/OneDrive, Debloat) are all ported.** Build #2 read-only; no Defender code
touched. UI-only — the 36 embedded `.ps1` payloads were already present byte-identical in
build #3 (Phase 27) and were **not modified**.

### isleap decisions applied

- **All 20 entries ported verbatim**, no confirmation dialogs anywhere — matching net8
  exactly, including the entries that duplicate Bloatware/Stage-3 removals (OneDrive, Edge,
  `Debloat.ps1`, `WindowsAI.ps1`, `EdgeDebloat.ps1`, `RestorePoint`). This second,
  unguarded path to some already-guarded actions is **accepted divergence**, per isleap.
- **Bug handling: port verbatim, unfixed** (isleap chose this when asked). `StoreSearch`'s
  undo still doesn't reverse its `icacls` deny, and `Debloat.ps1`'s undo is still a no-op
  message. Both `.ps1` files untouched — consistent with "do not modify any .ps1 content."
  Remain logged (Phase 27) as known net8 bugs.

### New in build #3

- `ViewModels/Software/DebloatRowViewModel.cs` — one row; `RunCommand` →
  `ToolService.RunWithTracking(new ScriptAction(run), Title, appliedTweaks)`, `UndoCommand`
  → `RunAction(new ScriptAction(undo))`. Verbatim net8 call shape.
- `ViewModels/Software/DebloatViewModel.cs` — the panel + `DebloatGroupViewModel`. Plain
  `object`/`ObservableObject`-free container (no observable state — the tree is built once),
  DI **singleton**, **not** a `TweakPageViewModel`, registers nothing.
- `Views/DebloatPage.xaml(.cs)`.
- Edited: `App.xaml.cs` (`AddSingleton<DebloatViewModel>()`, again outside the
  `TweakPageViewModel` enumeration), `MainWindow.xaml.cs` (`["Debloat"] = typeof(DebloatPage)`
  + rail sync).

### Data model (as Phase 27 found)

`(Title, Desc, RunScript, UndoScript)` tuples, three groups, built verbatim in
`DebloatViewModel.Build()`. Identity = run-script filename string; **no Id, no
TweakDefinition/AppDefinition, no TweakRegistry, no Backup participation.** The only state
is `_appliedTweaks` (net8's `AppliedTweaks` tracked-titles list), panel-local. Group
titles, group order, row order, and every Title/Description/script-name string are copied
byte-for-byte from net8 `DebloatTab.Build()`.

### Deviations from net8 (both cosmetic, flagged)

1. **Inter-row hairline separators dropped.** net8's `BuildGroup` drew a 1px hairline
   between rows; the MVVM card uses row margins for separation instead. Same as the
   "shadows/hairlines return in the cosmetic pass" deviations already logged for other
   panels. No behavioural effect.
2. Group header uses `Consolas` directly (net8 read `Application.Current.Resources["MonoFont"]`
   with a `Consolas` fallback; build #3 has no `MonoFont` resource, so the fallback value is
   used directly — identical result).

### Confirmation dialogs — NONE, by design

No `ContentDialog` / `TweakDialogs` / confirm call anywhere in the VM or page. Every Run and
Undo invokes immediately, exactly as net8. This is the app's least-guarded destructive
surface and it is intentional (Phase 27/28). Not re-litigated here.

### Build (VS MSBuild, literal)

```
  WinUI.Framework -> C:\Users\isleap\Documents\GitHub\WinUI-3-framework\src\WinUI.Framework\bin\x64\Debug\net10.0-windows10.0.26100.0\WinUI.Framework.dll
  AkariTool -> C:\Users\isleap\Documents\GitHub\Akari-Tool-MVVM\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\AkariTool.dll
```

Zero warnings, zero errors, first attempt. De-elevated (`asInvoker`) copy built the same.

### Registration — guard UNCHANGED

```
[WARMUP] Tweak registry warmed: 7 tweak page(s), 439 tweaks, 7 claimed range(s).
[WARMUP]   Gaming [0..130) 130 rows — Gaming & Performance
[WARMUP]   Sound [130..135) 5 rows — Sound
[WARMUP]   Notifications [135..151) 16 rows — Notifications
[WARMUP]   Update [151..163) 12 rows — Windows Updates
[WARMUP]   Privacy [163..252) 89 rows — Privacy & Security
[WARMUP]   Customize [252..403) 151 rows — Customize
[WARMUP]   Power [403..439) 36 rows — Power
[WARMUP] OK: 7 range(s) contiguous and non-empty, tiling [0..439).
```

Debloat registers nothing (tuples, not tweaks) → 439 unchanged, as expected.

### Verification (read-only, de-elevated + UIA; NOTHING run or undone)

- **All 3 group headers present** — `PRIVACY & TELEMETRY`, `APPS & COMPONENTS`, `CLEANUP`.
- **All 20 entries present — 20/20** (scrolled top→bottom collecting every Text; matched
  each verbatim title including the em dashes).
- **Buttons: 20 Run + 15 Undo.** 15 is exactly correct — the 5 no-undo entries are Microsoft
  Edge — Remove, Create Restore Point, Disk Cleanup — Run, Temporary Files — Remove, O&O
  ShutUp10++ — Run (empty 4th tuple field → no Undo button, per net8).
- No `[ERROR]` in the log; app alive throughout.
- **NOTHING was clicked.** Every Run/Undo in this panel is real, unguarded, and destructive
  by design, so no button was invoked on the de-elevated copy — that is exclusively
  isleap's disposable-VM step.

### VM checklist (Phase 28 — for isleap; DESTRUCTIVE + UNGUARDED, disposable VM ONLY)

Every click below executes immediately with no confirmation. On a throwaway VM only:

- [ ] A low-risk reversible pair round-trips: `Telemetry — Disable` Run then Undo →
      DiagTrack disabled/stopped, then re-enabled; log shows `[APPLIED] Telemetry — Disable`
      + `[RUN]`/`[DONE]`.
- [ ] `Store Search — Disable` Run then Undo → confirm (known net8 bug) that Undo does NOT
      re-grant the `store.db` ACL.
- [ ] `Unwanted Apps — Remove` (`Debloat.ps1`) Run → bulk AppX removal streams; then Undo →
      confirm (known net8 bug) it only prints the "cannot be undone" message.
- [ ] `OneDrive — Remove` / `Microsoft Edge — Remove` → confirm they run their CTT-style
      scripts (distinct from, and weaker than, the Stage 2/3 Bloatware removals — the
      accepted divergence).
- [ ] `O&O ShutUp10++ — Run` → downloads + launches the external OOSU10.exe.
- [ ] `[WARMUP]` still tiles `[0..439)` after any of the above.

### Software tab — CLOSED (pending VM sign-off)

| Stage | Panel | Phase | Status |
|---|---|---|---|
| 1 | External Apps (AppInstaller) | 23 | VM-verified (real install/uninstall round-trip) |
| 2 | Windows Apps (Bloatware) | 25 | COMPLETE, VM sign-off pending |
| 3 | Edge/OneDrive dedicated removal | 26 | COMPLETE, VM sign-off pending |
| 4 | Debloat | 28 | COMPLETE, VM sign-off pending |

All three Software rail tags (`AppInstaller`, `Bloatware`, `Debloat`) now route to real
pages; none falls through to `PlaceholderPage`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Defender** — no Defender code referenced.
- **The 36 `.ps1` payloads** — unmodified (the two known undo bugs left verbatim by decision).
- **Backup, bespoke Home/About/Tools, Advanced Tools UI** — still the remaining non-Software
  waves.

---

## MVVM Phase 29 — Backup & Restore (SHOW ONLY)

Recon of the last structural-risk wave — the only tab that exercises the
`Mark`/`ClaimRange` bracketing end to end. **Nothing created or edited in build #3;
`TweakRegistry.cs` not touched; build #2 read-only.** All bodies read from source this
phase.

**⚠ Contains a DEFENDER landmine (point 7). Flagged and left for isleap's explicit
go-ahead — this recon does NOT design or propose any handling for it.**

### 1. Files, and build #3 status

**Build #2 — `Tabs/Backup/`:**
- `BackupTab.xaml` (10 ln) — `BaseTab` shell, single `RootPanel`.
- `BackupTab.xaml.cs` (264 ln) — the tab: two cards (Export / Import) + helpers.
- `ImportReviewDialog.cs` (174 ln) — the preview/diff `ContentDialog`.

**Build #3 status:**
- **No Backup UI** — no `Tabs/Backup/`, no `Views/Backup*`, no `ViewModels/Backup*`, no
  `BackupTab`/`ImportReviewDialog`. (Same "don't assume unstarted" caution as Software —
  but here it genuinely is UI-unstarted.)
- **Rail tag EXISTS** — `MainWindow.xaml:151` `Content="Backup & Restore" Tag="Backup"`,
  currently falling through to `PlaceholderPage` (not in the `PageMap`).
- **The engine is present and format-identical** — `TweakRegistry.cs` is in build #3 with
  `ExportToFile` / `PreviewImport` / `ImportFromFile` / `ParseSettingsFile` (see §4).
- **Missing dependency:** `FilePickers` (build #2 `Tabs/Shared/FilePickers.cs`,
  `SaveFileAsync`/`OpenFileAsync`) does **not** exist in build #3 — the Backup port needs
  it (or an equivalent) before the file-picker calls resolve.

### 2. Export — thin wrapper over `TweakRegistry.ExportToFile`

Exact chain (`BackupTab.ExportSettingsAsync`):
1. `FilePickers.SaveFileAsync("Akari Tool settings", ".json", "AkariTool-Settings-{yyyy-MM-dd}.json")`
   — cancel returns null, no-op.
2. `var result = TweakRegistry.ExportToFile(picked);` — **direct call, no extra logic.**
3. Logs `[Backup] Exported {Exported} tweak(s) → {path}` (+ `{Skipped} skipped` when any),
   shows a status line.

**Always-everything, no selective export.** `ExportToFile` walks `_entries`, first-Id-wins
dedupe, and serializes every registered toggle/dropdown whose state reads non-null; there
is no per-tab / per-section UI to choose a subset. The Export card shows a live summary:
`"{TweakRegistry.Count} tweaks are currently tracked and will be included in the export."`
(refreshed on `Loaded`). Returns `ExportResult(int Exported, int Skipped)`; unreadable
state → skipped, never fails the file.

### 3. Import — preview/diff FIRST, then selective apply

Exact chain (`BackupTab.ImportSettingsAsync`):
1. `FilePickers.OpenFileAsync(".json")` — cancel → no-op.
2. `var preview = TweakRegistry.PreviewImport(picked);` then
   `differing = preview.Entries.Where(e => e.Differs)`.
3. **If `differing.Count == 0`:** no dialog — status `"Everything already matches — nothing
   to apply."` (+ `"(N entries not recognized by this version.)"` when `preview.Unknown > 0`).
4. **Else:** `new ImportReviewDialog(differing, preview.Unknown).ShowAsync()`. Cancel or
   zero selected → status `"Import cancelled — nothing was changed."`
5. On Apply → `TweakRegistry.ImportFromFile(picked, review.SelectedIds)` — applies **only
   the ticked Ids**.
6. Logs `[Backup] Import complete — {Applied} applied, {AlreadySet} already set, {Unknown}
   unknown, {Failed} failed (of {Total})`; status echoes it (+ "Some changes may need a
   restart." when `Applied > 0`).

**So net8 does NOT apply immediately** — it always previews, and applies a user-curated
subset. `ImportReviewDialog` (a `ContentDialog`, `MinWidth 560`, scroll `MaxHeight 380`):
- Title `Review Import`; header `"Review changes before applying"`.
- Sub: `"{N} tweak(s) in this file differ from your current settings. Uncheck anything you
  want to keep as-is."` (+ `"{U} entr{y is/ies are} not recognized by this version and will
  be skipped."` when unknown > 0).
- One row per differing entry: **checkbox (default CHECKED)** + `Name` + `CurrentDisplay
  → ImportedDisplay` (e.g. `Off → On`, or dropdown labels).
- Buttons: **Primary `Apply Selected`**, Close `Cancel`. `PrimaryButtonClick` harvests the
  ticked Ids into `SelectedIds`.

### 4. File format — JSON, CONFIRMED compatible between builds

`ExportToFile` writes (indented) JSON:

```json
{
  "format": "akari-tool-settings",
  "version": 1,
  "exportedAt": "<ISO-8601 UTC 'o'>",
  "machine": "<Environment.MachineName>",
  "tweaks": {
    "<tweak-id>": { "type": "toggle",   "name": "...", "value": true },
    "<tweak-id>": { "type": "dropdown", "name": "...", "value": "<raw>", "label": "<label>" }
  }
}
```

Import (`ParseSettingsFile`) validates `root["format"] == "akari-tool-settings"` and that
`root["tweaks"]` is an object (else `InvalidDataException`); `version`/`exportedAt`/`machine`
are metadata only, not re-checked. Matching is by **Id**; dropdowns resolve by raw `value`
first, then `label` (`ResolveOptionIndex`, reorder-robust); unknown Ids counted + skipped.

**Compatibility verified from source, not assumed:**
- `FormatName = "akari-tool-settings"`, `FormatVersion = 1` — **identical constants in both
  builds.**
- `ExportToFile` method body — **`diff` byte-identical** build #2 vs build #3.
- `ParseSettingsFile` — **`diff` byte-identical.**
- The only import-path difference is the documented MVVM seam: net8's
  `TweakHelpers.RefreshAllSectionPills()` (a rendering call) → build #3's
  `SectionsNeedRefresh?.Invoke()` event. **That is a UI-refresh hook, not serialization** —
  it does not alter a single byte of the file.

⇒ Build #3's already-ported registry **produces and consumes the exact same format** as
build #2. A backup exported by either build imports into the other. Constraint #2 holds.

### 5. Confirmation/warning on import

**The `ImportReviewDialog` IS the guard** — a preview-with-opt-out, not a blunt "are you
sure." Its worst-case copy is the sub line in §3 ("…differ from your current settings.
Uncheck anything you want to keep as-is."). There is **no additional destructive/overwrite
warning** beyond that review, and **no dialog at all when nothing differs.** Default is
every differing row checked, so a straight Apply overwrites all differing settings — the
protection is that the user sees each `current → imported` change first and can untick.
**Per-tweak `Warning` text is NOT surfaced by the review dialog** (see §7).

### 6. Bespoke, non-TweakRegistry elements

Essentially none. **No backup history, no saved-file list, no file management** — just two
static cards (Export button + summary; Import button) each with a mono status line. The
only bespoke UI is `ImportReviewDialog`, and it holds **no independent data model**: it is
driven entirely by `TweakRegistry.PreviewEntry` records
`(string Id, string Name, string CurrentDisplay, string ImportedDisplay, bool Differs)`,
plus a transient `List<(string Id, CheckBox Box)>` and an output `HashSet<string>
SelectedIds`. All state is derived from the registry; nothing persists between sessions.

### 7. ⚠ DEFENDER LANDMINE — flag + stop (needs isleap's explicit go-ahead)

The Defender disable is a **registered `TweakDefinition`**: `Id = "gaming-disable-defender"`
(`GamingTweaks.Security.cs`), a Toggle with `ReadState = () => TweakHelpers.HasState("DisableDefender")`
and `Apply = on => DefenderService.SetAsync(on, …)`. Because it is registered, it is **part
of the Backup set on both sides**:
- **Export** reads its state and writes it into the file like any other toggle.
- **Import** — when the file's value differs from the machine — reaches it through the
  ordinary apply path (`ImportFromFile` → `TweakHelpers.ApplyToggle` → `def.Apply`), i.e. a
  restored or shared backup can **silently invoke the live two-phase Defender
  disable/enable engine**. The Backup flow's only gate is the generic `ImportReviewDialog`;
  the row's own `Warning` text is not shown there.

Per the standing Defender rule and this task's point 7, I am **stopping description here**
and not analyzing or proposing any handling. **This must have isleap's explicit go-ahead
before any Backup extraction**, because porting Backup verbatim carries
Defender-state-in-backup behavior (export includes it; import can actuate it). No Defender
code was read beyond confirming the registration/apply wiring needed to answer the
question, and none was or will be changed.

### Summary for the extraction decision (not acted on)

Backup is a **thin two-card UI over already-ported, format-identical registry
infrastructure** — the lightest-weight tab remaining, except for two real gates isleap must
clear first: **(a)** the Defender-in-backup behavior above (point 7), and **(b)** a
`FilePickers` equivalent must exist in build #3 (§1). The `Mark`/`ClaimRange` round-trip the
CLAUDE.md calls out as this tab's unique proof is exercised by `ExportToFile` /
`ImportFromFile` against the live `_entries`, which the warm-up already tiles `[0..439)`.

### Not touched / not done

- **Build #2 (net8)** — read-only; zero writes.
- **Build #3** — recon only; no files created or edited except this log entry.
  `TweakRegistry.cs` not touched.
- **Defender** — registration/apply wiring confirmed only to answer point 7; flagged, not
  analyzed further, not modified. Needs isleap's explicit go-ahead.
