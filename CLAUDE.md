# CLAUDE.md — Akari Tool

Context file for any Claude session working on this repo. Read this fully before making changes.
Keep it updated as the project changes — it is the single source of truth, not chat history.

---

## What this is

**Akari Tool** — a Windows 11 optimization utility (gaming performance, privacy hardening, system tweaks, software management, service presets). Grew out of AkariOS (an AME Playbook-based custom Windows environment). **AkariOS is a first-class, actively-ported tab and the product's primary differentiator** — service presets, Playbook tweaks, BCD, Competitive Mode, GPU tooling and PostInstall are supported features, not a legacy dependency. The app also runs fully on stock Windows 11; AkariOS-specific pieces degrade gracefully rather than being required.

- Assembly / root namespace: `AkariTool` (kept identical across all variants for clean diffs)
- Registry root: `HKLM\SOFTWARE\AkariTool`. (Theme choice is NOT in the registry in build #3 — the HKCU-persisted `Theme` value is build #1/#2's WPF system. Build #3 persists theme via the framework's `IThemeService`→`SettingsService` under the `"AppTheme"` key: `ApplicationData.LocalSettings` when packaged, else `%LOCALAPPDATA%` JSON. Akari forces `Dark` when the persisted value is `Default` — see `MainWindow.xaml.cs`.)
- GitHub: `isleap9`. Icon CDN: `isleap9/package-icons` via jsDelivr → local cache `%ProgramData%\AkariTool\IconCache`.
- Crimson accent: `#E0142A`.

---

## ⚠️ THREE codebase lineages — know which one you're in

1. **WPF + WPF-UI 4.1.0 (net8)** — the ORIGINAL. Dead history. Do not work here.
2. **WinUI 3 factory build (net8, WinAppSDK 1.8)** — the completed WPF→WinUI 3 migration. Factory-based (`BaseTab`/`TweakHelpers`). **VM-verified and functionally complete** (433 tweaks, backup, Defender, search, installer all working). This is the **SHIPPING FALLBACK.** Has two known cosmetic issues (a footer/content-card corner and content-card material vs Mica) — non-blocking. **Do not delete or break this build; it is the proven release until the MVVM build passes the same gates.**
3. **WinUI 3 MVVM rebuild (net10, WinAppSDK 2.3.1)** — the ACTIVE work, separate project (`Akari-Tool-MVVM/`), built on the in-house `WinUI-3-framework` (CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection). Native shell (Mica, native NavigationView, `requireAdministrator`). **This is where new work happens.** See "MVVM rebuild status" below.

When a task says "the app," ask which build if it isn't obvious. Most new work is on **#3**; the fallback is **#2**.

---

## MVVM rebuild status (build #3 — the active effort)

**Migration log:** `MIGRATION_LOG3.md` is the ACTIVE phase log — record all new phases there. `MIGRATION_LOG.md` (Phases 1–8) and `MIGRATION_LOG2.md` (Phases 9–29) are ARCHIVAL only; do not append to them. (`MIGRATION_PROMPT.md` is historical — it describes the now-completed Gaming rendering-layer spike that kicked this off; CLAUDE.md is the current source of truth.)

**Goal:** rebuild Akari Tool on the native WinUI 3 MVVM shell, porting logic across and rebuilding the tweak-rendering layer natively (MVVM ViewModels + DataTemplates) instead of the imperative `TweakHelpers`/`BaseTab` factory. Build #2 (net8/WinAppSDK 1.8) stays the shipping fallback until #3 passes the same VM gates.

**Rendering layer + startup — DONE and STABLE:**
- **Native MVVM rendering layer** (settings-list of section cards, item ViewModels, per-row-type DataTemplates + selectors, badge pills, bulk bars, per-page search, Quick Actions) replaces the old factory. One render path; a tab is just its catalog order. Built in the Gaming spike, reused by every tab since.
- **Startup warm-up + registration-completeness guard** — every tweak-page VM is `Build()`-ed once at startup on a single background thread (via the `TweakPageViewModel` DI enumeration), so a never-visited tab is still present in Backup export + global search. `Build()` is idempotent + lock-guarded. After warm-up the guard logs the total `TweakRegistry.Count` and each tab's claimed range, asserting the ranges are non-empty and tile `[0..Count)` contiguously.
- **The `[WARMUP] OK: … tiling [0..N)` line is now the standing verification convention** — every tab rollout confirms the guard still tiles contiguously (and that a bespoke, non-registering section leaves the total unchanged). Read-only checks run against a de-elevated (`asInvoker`) copy + UI Automation.

**Catalog tabs FULLY rolled out and registered (7 tweak pages, 439 registered rows on isleap's desktop):**
- **Gaming & Performance** — 130 rows / 12 sections, incl. the 3 formerly-bespoke sections now catalog: System Services (37 dropdowns + 1 toggle), Scheduled Tasks (18 toggles), System Restore (2 toggles; `system-restore-protection` carries a confirm-on-disable Warning — the one deliberate behavioral add vs net8).
- **Sound** — 5 · **Notifications** — 16 · **Update** — 12 · **Privacy & Security** — 89.
- **Customize** — 151 rows / 21 sections, incl. Taskbar ▸ Button Grouping (net8's 3 hand-made ComboBoxes converted to 3 dropdown TweakDefinitions).
- **Power** — 36 rows / 13 sections here (47 in the full data layer; Battery 7 + GPU 4 hardware-gate off on this battery-less/no-vendor-GPU desktop — gating preserved, do not "fix"). Plus the **bespoke Plan Selector + Persist Indicator** (plan cards + persistent-scheme indicator + Revert-to-Balanced), which does NOT register with TweakRegistry.
- Latest guard: `7 tweak page(s), 439 tweaks` — Gaming `[0..130)`, Sound `[130..135)`, Notifications `[135..151)`, Update `[151..163)`, Privacy `[163..252)`, Customize `[252..403)`, Power `[403..439)`, `OK … tiling [0..439)`.

**Software tab — FULLY rolled out (Stages 1–4, Phases 23–28). Bespoke, non-catalog: registers NOTHING with TweakRegistry, so the `[WARMUP]` total stays 439.** All three rail tags now route to real pages (none falls through to `PlaceholderPage`).
- **Stage 1 — External Apps** (`AppInstaller`, Phase 23, VM-verified with a real winget install/uninstall round-trip): 193 apps in 16 category groups; card grid; Install/Uninstall/Refresh over the already-ported `SoftwareAppService`; Uninstall confirm dialog ported verbatim.
- **Stage 2 — Windows Apps** (`Bloatware`, Phase 25): 3 fixed shape-filtered sections (Windows Apps / Legacy Capabilities / Optional Features), Permanent pill on the 20 `CanBeReinstalled=false` defs, `Remove Windows Apps` dialog ported byte-for-byte. Remove → `RemoveWindowsAppsAsync` (which unconditionally persists the merged `BloatRemoval.ps1` + registers the SYSTEM ONSTART task); Install success wires the load-bearing `RemoveFromSavedScriptAsync` un-persist. Exposes `GetSelectedWindowsApps()` — the **Advanced Tools selection-provider hook**, now wired live in `MainWindow` (see bespoke pages below).
- **Stage 3 — Edge/OneDrive dedicated removal** (Phase 26): re-enabled by deleting Stage 2's one-line scope filter; both render as normal Bloatware cards routed to their 685/465-line dedicated scripts. **Landmine logged (LOG2 "Phase 26 ▸ 3b"):** Edge removal separately registers a SYSTEM AtLogon `\AkariTool\OpenWebSearchRepair` task that survives an Edge reinstall — known net8 behavior, left verbatim by isleap's decision.
- **Stage 4 — Debloat** (`Debloat`, Phase 28): 20 `(Title, Desc, Run.ps1, Undo.ps1)` tuples in 3 groups; identity = script filename, no Ids, no registration. Run/Undo shell the already-embedded scripts via `ToolService`. **No confirmation dialogs anywhere — the app's least-guarded destructive surface, ported verbatim per isleap** (incl. the two known net8 undo bugs, left unfixed by decision).

**Crimson theme tokens — DONE (Phase 14).** `#E0142A` accent set at the shell level in `App.xaml` `ResourceDictionary.ThemeDictionaries` (`SystemAccentColor` + all six shade variants + accent fill/text/toggle/nav/focus brushes, both light + dark) — no per-page edits. The ⊞ Windows-default logo mark stays Windows blue (`#0078D4`, `AkariWindowsLogoBrush`).

**Headless events — ALL WIRED** (were the "stale UI on interaction" bug class): `ToolService` `LineLogged`/`ProgressStarted`/`ProgressStopped` → shell log console + status-bar progress; `TweakRegistry.SectionsNeedRefresh` → section pills after import (subscribed per page in `Build()`); `PowerTweaks.PowerSchemeChanged` → `PowerViewModel.OnPowerSchemeChanged` repaints rows + Plan Selector/Persist Indicator. **That handler stays READ-ONLY (repaint from `ResolveSchemeTarget()` + `SchemeInactive`); it NEVER calls `SetPowerCfg`/`EnsureAkariScheme`/`powercfg /SETACTIVE`** — the named CLAUDE.md landmine, verified by code review.

**Forced logic changes during the port (all flagged, behavior-preserved):** `ToolService` rewritten headless (event surface, same 23-caller API); `System.ServiceProcess.ServiceController` package added; Power write-path repaint relocated behind the `PowerSchemeChanged` event (powercfg writes, Akari-scheme persistence, drift-clear, `/SETACTIVE`-last, "never reactivate from a read path" all UNCHANGED).

**Bespoke non-catalog pages — FULLY rolled out (register NOTHING, so `[WARMUP]` stays 439):**
- **Backup & Restore** (`Backup`, LOG3 Phase 1) — two-card Export/Import over the already-ported, **format-identical** `TweakRegistry` (`ExportToFile`/`PreviewImport`/`ImportFromFile`; `format:"akari-tool-settings"`, `version:1`, export + parse byte-identical to build #2 — the only tab that proves the `Mark`/`ClaimRange` round-trip). Import shows a `PreviewImport`-driven review dialog; **when the diff includes `gaming-disable-defender`, that row's own `TweakDefinition.Warning` is surfaced as a prominent caution banner (isleap Option 2 — presentation only; no Defender code is called, referenced, or imported).** File dialogs go through `IFileService` (see the elevation lesson below).
- **Advanced Tools** (`Advanced`, LOG3 Phase 3) — WIM ISO wizard (4 steps over the ported `WimUtilService`) + Autounattend generator (over `AutounattendService` + `UnattendTweakCatalog`). The generator's selected-apps provider is wired in `MainWindow` to `WindowsAppsViewModel.GetSelectedWindowsApps()` — the Phase-25 hook, now live.
- **Home / About / Tools** (LOG3 Phase 9) — **Home:** system banner + global search wired to `TweakRegistry.Search` + quick-nav card grid routing via `MainWindow.SelectRailTag`. **About:** version pill / credits / links (the ".NET 8" → ".NET 10" runtime label + URL corrected for the platform migration). **Tools:** read-only System Information (WMI/registry) + Repair/Network/Maintenance `RunScript` buttons + shell shortcuts. No file pickers; no Defender.

**Rail-tag routing audit (fresh — `MainWindow.xaml` tags vs `MainWindow.xaml.cs` `PageMap`; 15 of 24 route to real pages):**
- **Routed to real pages (15):** Home, Gaming, Sound, Notifications, Update, Privacy, Customize, Power, AppInstaller, Bloatware, Debloat, Backup, Advanced, Tools, About.
- **Still `PlaceholderPage` (9) — the genuine remaining work:**
  - **AkariOS · AppUpdate · Verify** — three bespoke tabs with their own net8 implementations (`Tabs/AkariOS`, `Tabs/AppUpdate`, `Tabs/Verify`), not yet ported. (AppUpdate is the app's self-update/changelog UI — distinct from the ported Windows-Update *tweak* tab; `UpdateService.CheckAsync` is ported but not yet called at startup.)
  - **Appearance · ContextMenu · Desktop · Explorer · StartMenu · Taskbar** — six sub-nav `MenuItems` NESTED under the Customize rail item. Customize itself is a complete page (151 rows / 21 sections, incl. those areas); these six deep-links are orphaned (→ PlaceholderPage) — a sub-navigation wiring gap, NOT missing content.

**STANDING PRACTICE — show-source-first, then confirm, then extract (NOT optional):** for any section whose exact net8 source has NOT been directly confirmed in the migration log, do a show-only recon FIRST — locate the file/method, confirm whether it's TweakDefinition-backed (registers) or genuinely bespoke, and report the Ids or the data model — before writing any extraction code. This is the discipline that caught Phase 7's wrong-section port (the AkariOS preset card mistaken for Gaming System Services) before it shipped, and has gated every bespoke section since. Same for anything power/scheme-adjacent: re-confirm the read-only invariant.

**STANDING PRACTICE — file pickers + elevation (learned Phases 4–7):** the app ships `requireAdministrator`, and WinRT `Windows.Storage.Pickers` (FileOpenPicker/FileSavePicker/FolderPicker) throw `COMException 0x80004005` under elevation — the out-of-process picker broker refuses a High-integrity caller, and `InitializeWithWindow` does NOT fix it. `IFileService` is therefore an **app-local Win32-dialog implementation, `Services/AkariFileService.cs`** (classic `IFileOpenDialog`/`IFileSaveDialog` via `CoCreateInstance`, in-process, works at any integrity level), registered AFTER `AddWinUIFrameworkCore()` so DI's last-registration-wins picks it. **ANY future file-picker use MUST go through the existing `IFileService`; never call WinRT pickers directly, and do not modify `WinUI-3-framework`'s default `FileService`.** Also: **always physically delete `bin\` + `obj\` (or build the de-elevated test copy via `build-deelevated.ps1`, which isolates `obj\DeElevated\` via `/p:DeElevatedTest=true`) before an elevation-sensitive test** — a shared `obj\` silently re-embedded an `asInvoker` manifest, making a `requireAdministrator` build launch un-elevated and masking whether the elevated path actually works (Phase 6). Note the automation constraint: a Medium-IL, non-interactive session cannot drive a High-IL (elevated) window (UIPI), so elevated picker/UI verification is isleap's interactive step.

**Hard constraint across the whole rebuild — preserve every `TweakDefinition` Id byte-for-byte.** Ids are what `TweakRegistry` and Backup/Restore match on; a changed Id silently orphans that tweak from every existing user's saved config. Backup file format must stay compatible with build #2's exports.

---

## Architecture

**Build #2 (factory):** single project sliced by feature under `Tabs/`. `TweakDefinition` records → `TweakHelpers` factory (partial across `.Apply`/`.BulkActions`/`.Controls`/`.QuickActions`/`.TweakRow`) builds rows; factory-level changes propagate to all tabs. `TweakRegistry` (static) auto-captures rendered tweaks for Backup/Restore + search (`Register`/`Mark`/`ClaimRange`/`Search`/`ExportToFile`/`PreviewImport`). `BaseTab` base class (`NavTag`/`NavLabel`/`Initialize`/`AddItem`/`AddSectionTitle`/`ApplySearch`). Nav: `_topTags` + `_groupByTag` — `HandleNav` falls through silently if a tag is missing from `_topTags`; register new tags in BOTH.

**Build #3 (MVVM):** `WinUI-3-framework` provides `ViewModelBase`, DI (`ServiceCollectionExtensions`/`ServiceLocator`), `INavigationService`, `IDialogService`, `IThemeService`, `ILogService`, etc. App project has `Views/` + `ViewModels/`. Catalogs are data-only static classes (`public static partial class …Tweaks`, one `TweakDefinition[] Section(Action<string> Log)` per section). The tweak-rendering layer (settings-list + item ViewModels + DataTemplates + selector) is the piece still to be built — it replaces the factory.

**Shared, both builds:**
- **Elevation** — `Services/ElevationService.cs`: in-process TrustedInstaller/SYSTEM impersonation via native token duplication. Process must already run elevated. Impersonation is **per-thread** — the impersonated action must be fully self-contained on that thread.
- **Distribution** — Inno Setup installer; `build-installer.ps1` automates publish + compile; GitHub Releases API for update checks/changelog. Build #2 ships self-contained (WinAppSDK runtime bundled). Build must use **VS MSBuild**, not `dotnet build`, for the WinUI PRI/packaging tasks.
- **Software tab** — winget is the only install path. `SoftwareAppService.RunWingetAsync` launches `winget` directly; if it is absent the call logs `[ERROR] winget not available` and returns `-1`. **There is no bootstrap path** (no IoT/LTSC handling, no `AppDefinition.InstallScript` — that property does not exist). Verified by grep across both builds, Phase 22.

---

## Landmines — get these wrong and you break the user's machine

**Services that must NEVER be fully disabled (set Manual `3`, not Disabled `4`, where noted):**
- Boot-critical: `DcomLaunch`, `RpcSs`, `RpcEptMapper`, `SamSs`
- ISO mounting: `ShellHWDetection`, `luafv`
- Action Center / NVIDIA App dependency (Manual, not Disabled): `CDPSvc`, `CDPUserSvc`, `WpnService`, `WpnUserService`
- `DusmSvc`/`Ndu` stack — disabling causes Settings "Not connected" and silently breaks Store, NVIDIA App, Spotify, Xbox.

**Registry / API:**
- `OpenSubKey(writable: true)` raises `SecurityException`, NOT `UnauthorizedAccessException`, on ACL-locked services.
- `DPS` requires the SYSTEM-elevation write path.
- Preset probe-table accuracy is critical — wrong declared values make every correct machine report "Mixed." Validate against actual preset dictionaries.
- Case-sensitivity: `["serenum"]` vs `["Serenum"]` etc. produce nondeterministic writes. Keep casing consistent.
- `_stockDefault` contamination: capture stock tables BEFORE any preset writes.
- Self-inflicted drift: a value written by two rows with opposing intent produces false drift (historically `FolderContentsInfoTip`, `ExtendedUIHoverTime`).

**powercfg / power:**
- `powercfg` must run under PowerShell, not Git Bash (MSYS mangles `/QUERY`).
- Battery gating: use `GetSystemPowerStatus` (`BatteryFlag=128` = no battery), NOT powercfg probes.
- Power scheme drift: detect/surface via persist indicator; reactivate on next WRITE (`SetPowerCfg` ends with `/SETACTIVE`). **NEVER reactivate from a read path.** (In build #3 this is guarded by the `PowerSchemeChanged` event being raise-only + read-only subscriber.)

**C# init order:** static fields initialize top-to-bottom; derived preset dictionaries declared AFTER dependencies; prefer lazy-init (`??=`) for `HttpClient`/registry handles. Cross-thread UI: snapshot `GetSelectedApps()`/`GetSelectedTweaks()` on the UI thread BEFORE `Task.Run`.

**Verification discipline (learned the hard way):** compile-clean ≠ launch-clean ≠ exercise-clean. Two of this project's worst bugs (nav-contract drift; a search-box first-keystroke crash) were invisible to build+launch and only appeared on interaction. When rendering is wired, every interactive path (search typing, quick-actions flyout, dropdown apply, confirmation dialog, import review) must be exercised on a VM, not just rendered. Keep an "interaction smoke" checklist.

---

## ⛔ Windows Defender — DO NOT TOUCH without explicit permission

**Hard rule. Never edit, refactor, "clean up," re-wire, relocate, activate, extend, or delete any Defender-related code, and never propose changes that alter Defender behavior, unless isleap explicitly asks in the current message. If a task would incidentally touch it, STOP and ask first.**

Why: Akari ships a **live two-phase engine that fully disables and re-enables Windows Defender** — genuinely destructive and rare. Getting it wrong leaves a machine unprotected, unbootable, or with a half-removed servicing package.

**LIVE implementation — `Services/DefenderService.cs` (`public static class`):**
- `SetAsync(bool disable, ToolService log)` — Phase 1 (interactive, admin): checks Tamper Protection, drops embedded `NoDefender.cab` to `C:\Windows`, runs embedded `DisableDefender.ps1` (removes Defender servicing package via DISM), schedules headless post-reboot Phase 2.
- `RunPhase2Native(Action<string> log)` — Phase 2 (headless, post-reboot): Defender keys are ELAM-protected at runtime; after Phase 1's package removal + reboot they're writable, so Phase 2 writes them as SYSTEM via `ElevationService.RunAsSystem`.
- `IsTamperProtectionEnabled()` — gate; disable cannot proceed with Tamper Protection ON.
- Payload (`Defender/NoDefender.cab`, `Defender/DisableDefender.ps1`) embedded in the assembly.

**Live call sites (do not disturb):**
- `GamingTweaks.Security.cs` → `DefenderService.SetAsync(...)` — Gaming-tab security toggle.
- `App.xaml.cs` (startup) → `DefenderService.RunPhase2Native(Log)` — runs every startup to continue Phase 2 after reboot. **Load-bearing for the reboot handoff — do not "optimize away."**

In build #3, all of the above ported **byte-identical** and the embedded payload resolves under the same resource names. Keep it that way.

**OLD/dead Defender implementation:** `Tabs/OSTweaks/OSTweaksTab.Defender.cs` + `.Security.cs` (`os-disable-defender`, PowerRun/MinSudo/.bat based) — lives in dead `OSTweaksTab`, unreachable, superseded. Do not revive, do not reference, do not relocate into a live tab (that would silently arm a Defender-disable feature), and do not delete the folder without explicit permission (deletion decision is isleap's alone). MinSudo is fully removed — do not reintroduce.

**Fine to work with normally:** the Defender *notification* toggles in Notifications (only touch notification registry, don't disable protection). The service-preset UI copy ("Defender is always protected / never touched") must stay accurate — presets deliberately leave Defender alone.

---

## Dead code map (do NOT "helpfully" re-wire)

- `Tabs/OSTweaks/OSTweaksTab` — never instantiated; contains the OLD Defender machinery. See Defender section. In build #2 it was excluded from compile; in build #3 it was not ported.
- `os-set-utc` tweak — relocated (in build #2) into Customize ▸ Regional Settings, Id preserved. In build #3 it lives in the extracted Customize catalog.
- `_akariOsPreserve` / `akariOsContext` in `AkariOSTab` — inert dead code, do not reactivate.
- MinSudo — fully removed; `ElevationService` replaces it. Do not reintroduce.

---

## How to work with me (isleap's preferences)

- **Tight scoping.** Only change what was asked. A direct statement of intent IS the spec. Don't expand into inventory reports or architecture rewrites.
- **Incremental, reversible changes.** Smallest change that works, following existing conventions. Push back is welcome but default to minimal.
- **Output format:** complete drop-in file replacements or paste-ready Claude Code prompts. NOT partial diffs or scattered snippets.
- **UI copy:** show a preview of the text before writing code.
- **Numbered options** when there's a real choice — isleap picks one and moves on.
- **Honest, conservative language** in notes/docs. No marketing fluff. State scope caveats plainly.
- **Sign-off:** isleap confirms it works on the real machine / VM before moving on. Don't assume success. Report the LITERAL build output, not a summary of intentions.
- **VM for functional testing, real hardware for Mica/visuals** (Mica often doesn't render in a VM — don't conclude it's broken from a VM).
- New significant features → prefer a new sidebar tab over nested sub-pages.
- Architectural decisions emerge from accumulated evidence, not a single trigger.
- Build with **VS MSBuild** (`msbuild /t:Rebuild /p:Configuration=Debug /p:Platform=x64`), not `dotnet build`.

**Reliability note:** structural advice about this codebase tends to be sound, but specific claims about what a given file contains can drift. Verify against the actual source when it matters — the code is ground truth over this document where they disagree.