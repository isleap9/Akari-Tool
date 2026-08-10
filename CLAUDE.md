# CLAUDE.md — Akari Tool

Context file for any Claude session working on this repo. Read this fully before making changes.
Keep it updated as the project changes — it is the single source of truth, not chat history.

---

## What this is

**Akari Tool** — a Windows 11 optimization utility. WPF / .NET 8 / WPF-UI 4.1.0 (lepo.co), C#, XAML, `win-x64`.
Covers gaming performance, privacy hardening, system tweaks, software management, and service presets.

Originally a companion to AkariOS (an AME Playbook-based custom Windows environment), now **repositioned as a fully standalone product targeting stock Windows 11 installs**. The AkariOS Playbook has been retired as a dependency.

- Assembly / root namespace: `AkariTool`
- Current version: `1.1.5`
- Target: `net8.0-windows`, `win-x64`
- Registry root: `HKLM\SOFTWARE\AkariTool` (distinct from any AkariOS key). Pinned theme choice lives at `HKCU\Software\AkariTool`.
- GitHub: `isleap9`. Icon CDN pulls from `isleap9/package-icons` via jsDelivr.

A sibling project, **AkariOS Configurator**, is a separate standalone app scaffolded from ported AkariOS tab components; it deliberately keeps `AkariTool` as its root namespace for clean upstream diffs. (Not this repo.)

---

## Architecture (how the app is built)

Single WPF project, sliced by feature under `Tabs/`. Logic and UI currently live together in tab code-behind — there is no separate Core/Infrastructure layer (see "Known tech debt").

Key pieces:

- **`TweakDefinition`** (`Tabs/Shared/TweakDefinition.cs`) — a `sealed class` describing one tweak (id, read state, apply action, etc.).
- **`TweakHelpers`** factory (`Tabs/Shared/TweakHelpers*.cs`, partial across `.Apply`, `.BulkActions`, `.Controls`, `.QuickActions`, `.TweakRow`) — builds tweak rows/toggles. **Factory-level changes propagate to every tab automatically.** Prefer changing the factory over editing tabs one by one.
- **`TweakRegistry`** (`Tabs/Shared/TweakRegistry.cs`, static) — auto-captures every rendered tweak for Backup & Restore and search. Exposes `Register`, `Mark`, `ClaimRange`, `Search`, `ExportToFile`, `PreviewImport`.
- **`BaseTab`** (`Tabs/Shared/BaseTab.cs`) — base class for all tabs. Override `NavTag`, `NavLabel`, `Initialize(ToolService)`. Provides `AddItem`, `AddSectionTitle`, `ApplySearch`.
- **Partial-class catalog pattern** — large tabs are split into partials (e.g. `AkariOSTab.Competitive.cs`, `OSTweakCatalog.Performance.cs`). Follow this when a tab file gets large.
- **Elevation** — `Services/ElevationService.cs`. In-process TrustedInstaller/SYSTEM impersonation via native token duplication. The process must already run elevated (Administrator). Impersonation is **per-thread** — the impersonated action must be fully self-contained on that thread.
- **Distribution** — Inno Setup installer; `build-installer.ps1` automates publish + compile. Update checks + changelog via GitHub Releases API.
- **Software tab** — winget is the primary install mechanism. `AppDefinition.InstallScript` handles bootstrap for Windows 11 IoT LTSC (no winget by default).
- **Icon pipeline** — `isleap9/package-icons` repo → jsDelivr CDN → local disk cache at `%ProgramData%\AkariTool\IconCache` with TTL revalidation.

Navigation:
- `MainWindow.xaml.cs` holds `_topTags` (HashSet) and `_groupByTag` (Dictionary). **`HandleNav` falls through silently if a tag is absent from `_topTags`** — always register a new nav tag in BOTH `_topTags` and `_groupByTag`.

Theme:
- First launch follows Windows `AppsUseLightTheme`. Only an explicit in-app toggle persists `Theme` under `HKCU\Software\AkariTool`. A pinned choice wins over Windows forever. `Apply` no longer persists.
- WPF-UI 4.1.0 `ApplicationAccentColorManager`: use the **4-color overload** of `Apply()` to set all four accent keys explicitly. The single-color overload derives `SystemAccentColorPrimary/Secondary/Tertiary` from Windows' own palette in dark mode and ignores the custom crimson accent (`#E0142A`).

---

## Landmines — get these wrong and you break the user's machine

**Services that must NEVER be fully disabled (set to Manual `3`, not Disabled `4`, where noted):**
- Boot-critical: `DcomLaunch`, `RpcSs`, `RpcEptMapper`, `SamSs`
- ISO mounting: `ShellHWDetection`, `luafv`
- Action Center / NVIDIA App dependency (Manual, not Disabled): `CDPSvc`, `CDPUserSvc`, `WpnService`, `WpnUserService`
- `DusmSvc` / `Ndu` stack — disabling causes Settings "Not connected" and silently breaks Microsoft Store, NVIDIA App, Spotify, Xbox.

**Registry / API gotchas:**
- `OpenSubKey(writable: true)` raises `SecurityException`, **not** `UnauthorizedAccessException`, on ACL-locked services — catch accordingly.
- `DPS` requires the SYSTEM-elevation write path.
- Preset probe-table accuracy is critical: wrong declared values make every correctly-configured machine report "Mixed." Always validate probe-table entries against the actual preset dictionaries.
- Case-sensitivity bug pattern: keys like `["serenum"]`/`["serial"]` vs `["Serenum"]`/`["Serial"]` produce nondeterministic writes. Keep casing consistent across presets.
- `_stockDefault` contamination risk: confirm stock tables are captured **before** any preset writes, not after.
- Watch for self-inflicted drift: a value written by two rows with opposing intent produces false drift reports (historically `FolderContentsInfoTip`, `ExtendedUIHoverTime`).

**powercfg / power:**
- `powercfg` must run under **PowerShell**, not Git Bash (MSYS mangles `/QUERY` args into paths).
- Battery hardware gating: use `GetSystemPowerStatus` (`BatteryFlag=128` = no battery), **not** powercfg probes — Windows registers the battery subgroup even without a battery.
- Power scheme drift: detect-and-surface via `_persistIndicator`, reactivate on next write (`SetPowerCfg` already ends with `/SETACTIVE`). **Never reactivate from a read path.**

**C# / init order:**
- Static fields initialize top-to-bottom. Preset dictionaries that derive from others must be declared **after** their dependencies. Prefer lazy-init (`??=`) for `HttpClient` and registry handles.
- Cross-thread UI: snapshot `GetSelectedApps()` / `GetSelectedTweaks()` on the UI thread **before** entering `Task.Run`.

---

## ⛔ Windows Defender — DO NOT TOUCH without explicit permission

**This is a hard rule. Never edit, refactor, "clean up," re-wire, extend, or delete any Defender-related code, and never propose changes that alter Defender behavior, unless isleap explicitly asks in the current message. If a task would incidentally touch this code, stop and ask first.**

Why this matters: Akari ships a **live, two-phase engine that fully disables and re-enables Windows Defender** — this is genuinely destructive and rare (most similar tools never attempt it). Getting it wrong can leave a user's machine unprotected, unbootable, or with a half-removed servicing package. It is not something to improve on a hunch.

**The LIVE implementation** is `Services/DefenderService.cs` (`public static class DefenderService`):
- `SetAsync(bool disable, ToolService log)` — Phase 1 (interactive, admin): checks Tamper Protection, drops embedded `NoDefender.cab` to `C:\Windows`, runs embedded `DisableDefender.ps1` which removes the Defender servicing package via **DISM**, then schedules a headless post-reboot Phase 2.
- `RunPhase2Native(Action<string> log)` — Phase 2 (headless, post-reboot): Defender service keys are **ELAM-protected at runtime** and cannot be written while Defender is loaded. After Phase 1's package removal + reboot, ELAM no longer locks them, so Phase 2 writes them natively **as SYSTEM** via `ElevationService.RunAsSystem` — no PowerRun, no MinSudo, no generated .bat.
- `IsTamperProtectionEnabled()` — gate; disable cannot proceed with Tamper Protection ON.
- Payload (cab + ps1) is embedded in the assembly, so this path does **not** depend on `PostInstallService` / the downloaded PostInstall folder.

**Live call sites (do not disturb):**
- `Tabs/Gaming/Catalog/GamingTweaks.Security.cs` → `DefenderService.SetAsync(...)` — the Gaming-tab security toggle.
- `App.xaml.cs` → `DefenderService.RunPhase2Native(Log)` — runs on **every startup** to continue Phase 2 after a reboot. Do not "optimize away" this startup call; it is load-bearing for the reboot handoff.

**Assets:** `Defender/NoDefender.cab`, `Defender/DisableDefender.ps1` (embedded resources).

**The OLD implementation** is `Tabs/OSTweaks/OSTweaksTab.Defender.cs` + `OSTweaksTab.Security.cs` (`os-disable-defender` tweak, PowerRun/MinSudo/.bat based). This lives in the dead `OSTweaksTab` and is unreachable. Do not revive it, and do not use it as a reference for the live path — it is superseded by `DefenderService`.

**Non-destructive Defender references that are fine to work with normally:** the Defender *notification* registry tweaks in `Tabs/Notifications/Notificationstab.xaml.cs` (Windows Defender Security Center notifications) only touch notification toggles and do not disable protection. The preset UI copy ("Defender is always protected / never touched") refers to the *service preset* path, which deliberately leaves Defender alone — that copy must stay accurate; do not change it to imply presets touch Defender.

---

## ⛔ Windows Defender — DO NOT TOUCH WITHOUT EXPLICIT PERMISSION

This is the single most sensitive area in the codebase. **Never modify, relocate, activate, "fix", refactor, or delete any Defender code without isleap explicitly asking for it in the current session.** If a task would touch it as a side effect, STOP and ask first.

What exists:
- `Defender/DisableDefender.ps1` and `Defender/NoDefender.cab` — the payload.
- `Tabs/OSTweaks/OSTweaksTab.Defender.cs` — the disable/re-enable logic (`SetDefenderToggle` → `SetDefenderAsync`).
- Wired as the `os-disable-defender` tweak in `Tabs/OSTweaks/OSTweaksTab.Security.cs`.

What it does when disabling: verifies Tamper Protection is OFF (aborts if ON), copies `NoDefender.cab` to `C:\Windows`, installs it via `Add-WindowsPackage` (DISM), disables ~16 Defender service registry keys and SmartScreen keys, disables Defender scheduled tasks, sets `-DisableRealtimeMonitoring`, and schedules a post-reboot cleanup. Re-enabling runs `Remove-WindowsPackage`. State is tracked via `TweakHelpers.SaveState/HasState/ClearState("DisableDefender")`.

**Critical current state:** this toggle lives inside `OSTweaksTab`, which is **dead / never instantiated** (see below). So the Defender feature is present in source but **not currently reachable by users.** That is intentional for now. Two traps follow directly from it:

1. The pending cleanup task is "relocate tweaks out of the dead `OSTweaksTab`, then delete it." When doing that, relocate ONLY `os-set-utc` (or whatever isleap names). **Do NOT relocate the Defender toggle into a live tab** — that would silently arm a full Defender-disable feature. Leave it in place and ask.
2. When `OSTweaksTab` is eventually deleted, the Defender machinery goes with it. **Confirm with isleap whether the Defender code should be preserved elsewhere or deleted** before removing the folder/files. Do not decide this unilaterally.

Why this matters more than usual: most Windows-tweak tools of this kind do not ship a full disable-and-re-enable-Defender path. Getting it wrong — arming it accidentally, half-disabling, or breaking the re-enable path — leaves a user's machine unprotected or in an unrecoverable AV state. Treat every line here as load-bearing and off-limits by default.

---

## Dead code map (do NOT "helpfully" re-wire these)

- **`Tabs/OSTweaks/OSTweaksTab`** — never instantiated. Not referenced outside its own folder. Safe to delete once its last live tweak is relocated — **BUT** it also contains the Defender disable/re-enable machinery (`OSTweaksTab.Defender.cs`, `OSTweaksTab.Security.cs`). See the Defender section above: do not relocate the Defender toggle and do not delete the folder without explicit permission.
- **`os-set-utc` tweak** — currently lives in `Tabs/OSTweaks/Catalog/OSTweakCatalog.Performance.cs`. Relocate it out before deleting the dead OSTweaks tab.
- **`_akariOsPreserve` set and `akariOsContext` parameter** in `AkariOSTab` — inert dead code left after the AkariOS-gated services card was removed. Do not reactivate.
- **MinSudo** — fully removed from all code paths and replaced by `ElevationService`. `MinSudoService.cs` no longer exists in this repo. Do not reintroduce a MinSudo dependency.

---

## In flight / on the horizon

- Migrate tab content fully to WPF-UI via the `TweakHelpers` factory, ahead of a full app redesign using WPF-UI theme tokens. (Shell migration — FluentWindow, TitleBar, NavigationView — is already complete and stable.)
- Relocate `os-set-utc`, then delete the dead `OSTweaksTab`.
- Code signing via SignPath.io (unsigned installers currently trigger SmartScreen).
- `powercfg` batching: ~39 uncached call sites each spawn a subprocess; batching is a worthwhile standalone optimization.
- Optional: `ResetToSystem` (delete `Theme` registry value + re-detect system theme) wired to right-click on the theme toggle — not yet implemented.
- Startup update check is implemented in `MainWindow` via `Loaded` → `UpdateService.CheckAsync`; dialog with `Owner=this`; "Update now" navigates to the AppUpdate tab; silent on error / when up to date.

---

## How to work with me (isleap's preferences)

- **Tight scoping.** Only change what was asked. Don't expand a small request into an inventory report or architecture rewrite. A direct statement of intent IS the spec.
- **Incremental, reversible changes.** Targeted additions following existing conventions. Push back is welcome, but default to the smallest change that works.
- **Output format:** complete drop-in file replacements or paste-ready Claude Code prompts. **Not** partial diffs or scattered inline snippets.
- **UI copy:** show a preview of the text change before writing code.
- **Numbered options** when there's a real choice to make — isleap picks one and moves forward.
- **Honest, conservative language** in release notes and docs. No marketing fluff. State scope caveats plainly.
- **Sign-off:** isleap confirms when something works on the real machine / VM before moving on. Don't assume success.
- New significant features → prefer a new sidebar tab over nested sub-pages.
- Architectural decisions should emerge from accumulated evidence, not a single trigger.

**Reliability note:** structural/architectural advice about this codebase tends to be sound, but specific claims about what a given file contains or its current state can drift. When it matters, verify against the actual source before acting — some facts in this file were already stale on last check (e.g. MinSudoService removal). Treat the code as ground truth over this document where they disagree.