# Akari Tool

## What This Is

Akari Tool is a Windows 11 optimization utility (gaming performance, privacy hardening, system
tweaks, software management, service presets) built on WinUI 3 + MVVM, with AkariOS (an
AME Playbook-based custom Windows environment) as a first-class, actively-ported tab. This
milestone reworks Advanced Tools ▸ System Tools — currently the last major page in the app still
built the old imperative code-behind way — into a structured hub with real, purpose-built tools:
a deep-clean app uninstaller, a scan-then-review System Cleaner, and a granular per-value NIC
tweak UI.

## Core Value

Every optimization the app performs must be safe, reversible, and legible to the user before and
after it happens — no silent all-or-nothing scripts, no orphaned leftovers, no changes the user
can't see or undo.

## Requirements

### Validated

- ✓ Declarative SettingDefinition stack (Gaming, Sound, Notifications, Privacy, Customize x5,
  Update, Power) — existing, Track A migration complete
- ✓ AkariOS tab (service presets, Playbook tweaks, BCD, Competitive Mode, GPU tooling,
  PostInstall) — existing, actively maintained
- ✓ Software management (Windows Apps + External Apps bulk-uninstall, Debloat) — existing
- ✓ Settings Backup/Restore + global search (SettingBackupService) — existing
- ✓ System restore point integration, compatibility gating (Windows version + hardware) — existing
- ✓ Home tab system info display (SystemInfoService.Gather) — existing; System Tools must not
  duplicate this

### Active

- [ ] System Tools becomes a hub (mirrors `AdvancedHubPage`'s card pattern), not a single
      scrolling page
- [ ] `ToolsPage.xaml`/`.xaml.cs` (current System Tools) is deleted outright and rewritten fresh —
      no code carried over, even for sections that stay conceptually the same (Repair & Health,
      Quick Shortcuts)
- [ ] Deep-clean app uninstaller: after uninstalling via the existing Software ▸ External/Windows
      Apps bulk-uninstall, scan for and offer to remove leftover registry keys, folders, and
      scheduled tasks the uninstaller left behind
- [ ] System Cleaner: scan-then-review junk/temp file cleanup (replaces the current one-shot
      "Clear Temp Files"/"Disk Cleanup" buttons), a large-file finder, and a duplicate-file finder
      — all read-only discovery with explicit user selection before any delete
- [ ] Granular NIC tweak UI: replaces `Scripts/Network/network-apply.bat` (184-line all-or-nothing
      batch writing ~18 registry values to every adapter) with per-value toggles (interrupt
      moderation, RSS, offloads, buffer sizes, etc.), per-adapter selection, and real per-value
      revert (not the current 5-line blanket revert script)
- [ ] Nav-tag mapping (`MainWindow.xaml.cs` `["Tools"] = typeof(ToolsPage)`) and the
      `AdvancedHubPage` "System Tools" card are repointed to the new hub

### Out of Scope

- Rebuilding App Uninstaller's bulk-uninstall itself — Software ▸ External Apps / Windows Apps
  already does multi-select uninstall; this milestone only adds leftover-cleanup on top
- System Information card — dropped entirely; Home tab already shows this via
  `SystemInfoService.Gather()`
- Per-app/game QoS traffic prioritization, latency/route diagnostics, TCP stack tuning UI —
  considered during scoping, deferred; this milestone is NIC-value granularity only

## Context

- Existing `ToolsPage.xaml.cs` (366 lines) is live and reachable today: System Info (read-only,
  now redundant with Home), Repair & Health (SFC/DISM/Restore Point), Network (flush DNS, Winsock
  reset, DNS provider switch — kept, folds into the reworked Network page), Maintenance (temp
  files/disk cleanup/icon cache — absorbed into System Cleaner), Quick Shortcuts (kept, rewritten
  fresh). Every current action is "click → run a fixed embedded `.ps1`/`.bat` → done," no
  scan-then-review UI anywhere in the page.
- An app uninstaller already exists at Software ▸ External Apps / Windows Apps
  (`ExternalAppsViewModel`/`WindowsAppsViewModel`, `UninstallCommand`, multi-select, confirm
  dialog) — this milestone's uninstaller work is additive (leftover scan/clean), not a duplicate.
- `Scripts/Network/network-apply.bat` and `network-revert.bat` already exist and are wired into
  AkariOS Gaming Tweaks' "Network Optimization" toggle (`AkariOSPage.GamingTweaks.cs`,
  `SetNetworkOptimization`) — the new granular NIC UI supersedes this all-or-nothing pair.
- The rest of the app (Optimize tabs, Customize tabs, Power) already runs on the declarative
  SettingDefinition/SettingPageViewModel stack; System Tools is the outlier still built as raw
  imperative WinUI code-behind (`StackPanel`/`Border` construction in C#, no XAML data templates).
  This milestone doesn't require migrating System Tools onto SettingDefinition (its actions aren't
  toggle/dropdown settings — they're scans, finds, and multi-step operations), but the new pages
  should follow current App-layer conventions (MVVM where it fits, `TweakDialogs` for
  confirmations, `ToolService`/`IAkariLogService` for logging, `IProcessExecutor`/
  `IWindowsRegistryService` from Infrastructure rather than raw P/Invoke in code-behind).

## Constraints

- **Tech stack**: WinUI 3 (WindowsAppSDK 2.3.1), .NET 10, CommunityToolkit.Mvvm 8.4.2, C# 12 —
  same as the rest of the app; no new frameworks
- **Build**: VS MSBuild only (`AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64`);
  `dotnet build` fails on WinUI 3 PRI/resource targets
- **Elevation**: registry/file operations that need admin rights must go through the existing
  `IProcessRestartManager`/elevation pattern, not raw process launches
- **Reversibility**: every destructive action (delete, registry write) needs an explicit
  user-visible review step before it runs and, where the existing app already has a revert
  pattern (registry writes via `WindowsRegistryService`), the new NIC UI must support real revert
  per value, not a blanket "undo everything" script
- **Safety**: file/registry scans for the Cleaner and Uninstaller are read-only discovery; nothing
  deletes without explicit per-item user selection (no "select all and go" default)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| System Tools becomes a hub, not a flat page | Matches `AdvancedHubPage`'s existing pattern (Image & Deployment / Backup / Verify); keeps each tool focused and independently testable | — Pending |
| Drop System Information card entirely | Home tab already shows this via `SystemInfoService.Gather()` — verified in codebase before deciding | — Pending |
| Delete `ToolsPage.xaml(.cs)` outright rather than refactor in place | User explicit: no old code carried over, even for sections that conceptually survive | — Pending |
| Uninstaller work is leftover-cleanup only, not a new bulk-uninstaller | Software ▸ External/Windows Apps already covers bulk uninstall — verified before scoping to avoid duplicating existing functionality | — Pending |
| NIC tweak UI is per-value/per-adapter with real revert | Current `network-apply.bat` is all-or-nothing with only a 5-line revert; granularity was the explicit ask | — Pending |

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
*Last updated: 2026-08-27 after initialization*
