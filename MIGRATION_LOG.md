# Akari Tool — WPF-UI → WinUI 3 Migration Log

Branch: `winui3-migration`  ·  Target: unpackaged WinUI 3, Windows App SDK 1.8,
`net8.0-windows10.0.19041.0`, `win-x64`, `WindowsPackageType=None`.

---

## Phase 0 — Scaffold & harness — **COMPLETE ✅**

Date: 2026-08-01

### Build status (literal)

Clean rebuild via VS 2026 MSBuild (`…\MSBuild\Current\Bin\amd64\MSBuild.exe`),
`-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:06.28
```

Output: `bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\AkariTool.exe` (216 KB).

### Launch status (literal)

Launched the produced `AkariTool.exe` from a **non-elevated** shell:
- Process stayed **alive 5s** → WinUI 1.8 runtime bootstrap + `MainWindow`
  created successfully (framework-dependent, `SelfContained=false`, using the
  installed `Microsoft.WindowsAppRuntime.1.8_8000.921.1539.0` runtime).
- `Stop-Process` returned **"Access is denied"** from the non-elevated session →
  the process is running **elevated as Administrator**. That is positive proof
  the `requireAdministrator` manifest auto-elevated the unpackaged WinUI app.
- Note: because it elevated, the launched blank window could not be killed from
  this session (left running until closed/reboot).

### Environment (was the Phase-0 blocker last run — now resolved)

- **.NET 8 SDK** `8.0.423` installed (alongside 10.0.302); pinned via `global.json`.
- **VS Community 2026 (18.8.2)** with **NativeDesktop (C++)** + **ManagedDesktop**;
  **VC Tools 14.51**, **Windows 11 SDK 10.0.26100.0**.
- The previously-missing AppxPackage/PRI MSBuild tasks now exist at
  `…\VS\18\Community\MSBuild\Microsoft\VisualStudio\v18.0\AppxPackage\`.
- **Windows App Runtime 1.8** present (framework package `8000.921.1539.0`).
- Build must use **VS MSBuild**, not `dotnet build` (the .NET 10 CLI SDK still
  lacks those tasks). Documented as the project's build command.

### Files created

- `global.json` — pins .NET 8 SDK `8.0.423` (`rollForward: latestFeature`).
- `App.xaml` / `App.xaml.cs` — **replaced** WPF versions with WinUI 3 lifecycle
  (`OnLaunched` → `new MainWindow().Activate()`), process-wide crash handlers
  (`App.UnhandledException` + AppDomain + TaskScheduler → CrashReport, same
  `%APPDATA%\AkariTool\` sink as before).
- `MainWindow.xaml` / `MainWindow.xaml.cs` — **replaced** WPF `ui:FluentWindow`
  with a blank WinUI `Window`.
- `Tabs/Shared/TweakHelpers.State.cs` — **new** logic-only partial (see below).
- `AkariTool.csproj` — **replaced** with the WinUI 3 unpackaged project + the
  Phase-0 compile closure.

### Files copied UNCHANGED and compiling (logic layer)

All of `Services/**` **except** `ThemeService.cs` (28 of 29 files) compile
byte-for-byte, plus these logic-only `Tabs/Shared` / `Tabs/Software` files pulled
in as transitive dependencies of the Services layer:
`TweakHelpers.State.cs`, `PostInstallService.cs`, `ExplorerRestart.cs`,
`Software/AppModels.cs`, `Software/Removal/*.cs`, `Software/SoftwareAppService.cs`.

### Files where framework code HAD to change (flagged per brief §4)

1. **`Services/ToolService.cs`** — the only Service that referenced `System.Windows`.
   **Logic untouched** (process runner, script extraction, package install, URL
   open, shortcut creation are byte-identical). Framework-only edits:
   - usings `System.Windows.*` → `Microsoft.UI.Xaml[.Controls/.Media]` +
     `Microsoft.UI.Dispatching` + `Windows.UI`.
   - `control.Dispatcher.Invoke(…)` → `control.DispatcherQueue.TryEnqueue(…)`
     (WinUI). **Behavioural note:** `TryEnqueue` is fire-and-forget where WPF's
     `Invoke` was synchronous — fine for log/progress updates, but recorded here.
   - `TextBox.AppendText` + `ScrollToEnd()` → `Text +=`; **`ScrollToEnd` has no
     WinUI equivalent** on `TextBox` → `// TODO(migration):` left to wire the
     template `ScrollViewer` when the log panel is built (Phase 1).
   - `BrushConverter` (does not exist in WinUI) → hand-written `#RRGGBB`/`#AARRGGBB`
     hex → `SolidColorBrush` parser in `BrushFrom`.
   - Constructor signature `ToolService(TextBox, ProgressBar, TextBlock)` **kept**
     (now the WinUI types), so the Phase-1 shell wires it identically.

2. **`Tabs/Shared/TweakHelpers.State.cs`** — **new** file. The registry state
   methods `SaveState/HasState/ClearState` + `StateKey` were extracted verbatim
   from the (WPF-coupled) `TweakHelpers.cs` so the byte-for-byte
   `Services/DefenderService.cs` can call them without dragging in the UI factory.
   No behaviour change (same `HKCU\Software\AkariTool` key). **Phase-1 action:**
   remove the duplicate definitions from `TweakHelpers.cs` when it is migrated.

### Deferred / dropped (with rationale)

- **`Services/ThemeService.cs`** — excluded from Phase-0 compile. It applies
  **WPF-UI** themes (`Wpf.Ui.Appearance`) and is a pure theme-layer file; **no
  other `Services/` file references it** (only App/MainWindow/tabs did, all of
  which are deferred). Deviation from the literal "all Services compile" gate,
  called out here; it is rebuilt against WinUI in Phase 1 with the theme tokens.
- **Defender `--defender-phase2` headless startup path** (was in the old
  `App.xaml.cs` → `DefenderService.RunPhase2Native` + `DefenderPhase2Scheduler.
  ClearRunOnce`). **NOT ported.** Per CLAUDE.md, Defender code is off-limits
  without explicit sign-off. `DefenderService.cs` itself is carried over
  **byte-for-byte**; only this App call-site is deferred. `// TODO(migration):`
  marker left in `App.xaml.cs`. **Needs isleap's decision (see below).**
- **WPF startup orchestration** — splash window with staged progress, persisted-
  theme apply, WPF-UI ComboBox popup class-handler hack, `--competitive`
  shortcut path — intentionally not ported in Phase 0 (Phase 1 shell work).
- **Startup crash `MessageBox`es** — dropped for now (WinUI `ContentDialog` needs
  a `XamlRoot` that doesn't exist before the window); logging + crash-report file
  preserved. `// TODO(migration):` in `App.xaml.cs`.
- **`NavDummyPage.cs`** (WPF-UI nav shim) and **`AssemblyInfo.cs`** (WPF
  `ThemeInfo`) — excluded; not needed under WinUI.

### csproj compile closure (Phase 0)

Default SDK globs, then: remove `Tabs/**/*.cs`, all non-shell `*.xaml`
(`Tabs/**`, `Themes/**`, `SplashWindow.xaml`, `AkariFluentTheme.xaml`),
`SplashWindow.xaml.cs`, `NavDummyPage.cs`, `AssemblyInfo.cs`,
`Services/ThemeService.cs`; then re-`Include` the six logic-only files listed
above. Embedded payloads (`Scripts/*.ps1`, `Scripts/Network/*.bat`,
`Defender/NoDefender.cab`, `Defender/DisableDefender.ps1`, `Nvidia/Settings.nip`)
kept unchanged. Fonts/logos/nav icons added as **ms-appx `Content`**.

### Things I could not verify here

- **Silent UAC auto-elevation UX** — elevation itself is proven (see Launch
  status), but whether the UAC prompt appears/behaves as intended is an
  interactive check for isleap on the real machine.
- Runtime rendering of the ms-appx Content fonts/icons — not exercised by the
  blank Phase-0 window; validated when the shell renders them in Phase 1.

---

## Phase 1 — Shell + theme tokens + factory + pilot tab — **COMPLETE ✅**

Date: 2026-08-01

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    8 Warning(s)
    0 Error(s)
Time Elapsed 00:00:07.69
```

The 8 warnings are all benign `CS0649` ("field never assigned") on
`TweakHelpers.SectionCollapse` (Chevron/Body/Title/UserCollapsed) — those fields
are assigned by `BuildSection`, which is in the deferred `Controls` partial and
ports with the first tweak-tab batch. Output: `AkariTool.exe` (216 KB).

### Launch status (literal)

Launched from a non-elevated shell; **alive 6s** then `Stop-Process` denied
(running elevated). So the WinUI runtime + `ThemeService.Apply` + native
`NavigationView` + the `AboutTab` factory path all initialised with **no XAML /
theme-resource load crash** (a bad `{ThemeResource}` key or factory brush would
throw during `InitializeComponent` / `Build` and exit the process immediately).

### What was migrated

- **Theme tokens** → `App.xaml` `ResourceDictionary.ThemeDictionaries`
  (`Default` = dark, `Light` = light). Every Akari token key from
  `Themes/Akari.Dark.xaml` / `Akari.Light.xaml` ported **with identical key
  names**, plus native-WinUI accent overrides (`SystemAccentColor`,
  `AccentFillColorDefaultBrush`, `ToggleSwitchFillOn`,
  `NavigationViewSelectionIndicatorForeground`, `TextControlBorderBrushFocused`)
  so the crimson `#E0142A` accent themes native controls. `AkariFluentTheme.xaml`
  agnostic tokens (radii, `DisplayFont`/`BodyFont`/`MonoFont`) live as direct
  app resources. **Dropped:** the two `DropShadowEffect` keys (no WinUI Effect).
- **`ThemeService`** rebuilt against WinUI: drives theming via the root's
  `RequestedTheme` (re-resolves `{ThemeResource}`), reads palette values straight
  from the App.xaml `ThemeDictionaries`, keeps the managed-brush live-update
  engine (`ManagedBrush` + `CardElevationBorder`), `Logo` via `ms-appx:///`.
  Removed WPF-UI (`ApplicationThemeManager`/`ApplicationAccentColorManager`),
  dictionary hot-swap, and `CardShadowEffect`.
- **`BaseTab`** ported, **all public signatures preserved** (`AddItem` ×2,
  `PageHeader` ×2, `AddSectionTitle`, `AttachSearch`, `ApplySearch`, `NavTag`,
  `NavLabel`, `Initialize`, `BrushFrom`, `DisplayFont`/`MonoFont`). Native buttons
  replace `RunBtn`/`UndoBtn` (Run = `AccentButtonStyle`); `AutoSuggestBox`
  replaces the WPF-UI search box (focus-grow animation dropped); `Separator`
  handling reworked to `Tag="separator"` Borders (WinUI has no `Separator`).
- **`TweakHelpers` factory spine:** `.cs` (token accessors, `Token`, `BrushFrom`,
  `CardBackground`; `CardShadow()` dropped, state methods now in `.State.cs`),
  `.Apply.cs` (unchanged logic), new `.Sections.cs` (`SectionCollapse` +
  `SectionCollapseStates`, extracted from the deferred `.Controls.cs`).
- **`MainWindow`** shell rebuilt native: custom title bar via
  `ExtendsContentIntoTitleBar` + `SetTitleBar`; native `NavigationView`
  (SOFTWARE / OPTIMIZE / ADVANCED headers + items, About in `FooterMenuItems`);
  status bar + log panel (`TxtLog`/`LogProgress`/`TxtProgressStatus` → `ToolService`);
  theme toggle in the title bar; a `ContentDialog` (info) wired to a log-panel
  button. Content uses the Visibility-toggled stack pattern (AboutTab + a
  placeholder for not-yet-migrated tabs).
- **`AboutTab`** migrated end-to-end (logo Ellipse/ImageBrush, version pill,
  Environment/Credits cards, `Hyperlink`/`Run` inlines, link buttons). Logo
  `DropShadowEffect` glow dropped (no WinUI Effect). "UI" line updated to
  "WinUI 3 (Windows App SDK)".
- **`ToolService.ScrollToEnd`** TODO wired: the log `TextBox` scrolls via its
  template `ScrollViewer` (found through `VisualTreeHelper`).

### VisualStates / compact-expanded

The native `NavigationView` provides adaptive compact/expanded pane behaviour
built-in (its own VisualStateManager), so no hand-authored compact/expanded
VisualStates were needed — the WPF attached-property `Nav.IsCompact` DataTriggers
are replaced by the control's native adaptive states.

### Deferred to the tab batches (flagged, compiles-clean without them)

- **`TweakHelpers.Controls.cs` / `.TweakRow.cs` / `.BulkActions.cs` /
  `.QuickActions.cs`** and **`AkariDialogs.cs`** — still excluded. They depend on
  a **synchronous** dialog pattern (`Dispatcher.PushFrame` nested pump) with **no
  WinUI equivalent** (`ContentDialog` is async-only); porting them requires a
  sync→async rework across many call sites, best done against the first real
  consuming tab. Public methods in these partials (`BuildToggle`, `BuildSection`,
  `AddToggleRow`, `BuildTweakGrid`, `AddTweakRow`, `BuildQuickActionsButton`, bulk
  actions) are therefore not present yet. `BaseTab.PageHeader(withActions)`'s one
  call to `BuildQuickActionsButton` is a disabled placeholder button with a
  `// TODO(migration)` (no Phase-1 tab uses that path).
- **Defender `--defender-phase2` headless path** — still deferred pending sign-off
  (see report). `DefenderService.cs` remains byte-for-byte untouched.
- **Full startup orchestration** (splash window + staged progress, persisted-theme
  ordering, `--competitive` shortcut path) — not ported; Phase 1 App startup is
  minimal (create window; theme applied in MainWindow ctor).

### Things I could not verify here

- Actual on-screen rendering (rail styling, crimson accent, About cards, theme
  toggle flip, ContentDialog) — the app runs elevated and can't be screenshotted
  from this non-elevated session. Needs isleap's eyes on the real window.
  **UPDATE: isleap signed off on the Phase 1 window (accent, theme toggle, rail
  nav, About tab, cards, log panel all confirmed working on-screen).**

---

## Tab Batch 1 — Factory (async dialogs) + Notifications + Sound — **COMPLETE ✅**

Date: 2026-08-01

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:08.34
```

(The Phase-1 `CS0649` warnings cleared naturally — `BuildSection` now assigns
the `SectionCollapse` fields.)

### Tabs migrated

- **NotificationsTab** (5 sections, 16 tweaks incl. the warned "Disable Action
  Center") — code-behind logic byte-identical, only the framework usings swapped;
  XAML re-rooted on a `ScrollViewer` + `RootPanel`.
- **SoundTab** (1 section, 5 tweaks incl. a Dropdown) — same treatment.
- Both wired into the shell's new tag→tab dictionary with the WPF shell's
  `TweakRegistry.Mark()/ClaimRange()` bracketing reproduced around `Initialize`.

### Factory partials ported (WinUI + async dialogs)

- **`AkariDialogs`** — sync `Dispatcher.PushFrame` pump replaced with async
  `ContentDialog` (`ConfirmOkCancelAsync` / `ConfirmYesNoAsync` / `InfoAsync` /
  `ConfirmContentAsync`), serialized by a semaphore (WinUI allows one dialog at
  a time), `XamlRoot` supplied by MainWindow. No-XamlRoot fallback = treat
  confirmation as declined.
- **`TweakHelpers.Controls`** — `BuildToggle` (WinUI `ToggleSwitch` with a
  suppress flag restoring WPF-UI's "Click fires only on user interaction"
  semantics — WinUI's `Toggled` fires on programmatic changes too),
  `BuildTweakCell`, `BuildSection` (header `Tapped`, collapse persistence
  unchanged), `AddToggleRow`, `BuildTweakGrid`, `ApplyRoundedClip` (Composition
  `CreateRoundedRectangleGeometry` + `CreateGeometricClip`; first attempt used a
  non-existent `CreateRoundedRectangleClip` — caught at compile), `ShadowWrapCard`
  (API preserved; shadow itself deferred to cosmetic pass). Row separators are
  `Border Tag="separator"` (WinUI has no `Separator`).
- **`TweakHelpers.TweakRow`** — `AddTweakRow` with async `ConfirmWarningAsync`
  before every apply (row toggle, dropdown change, quick-set ★/⊞). Pill row is a
  horizontal `StackPanel` (no in-box WrapPanel; ≤3 short pills per row). The ⊞
  Windows-logo `Geometry.Parse` path became a 2×2 `Rectangle` grid (no C#
  geometry parser in WinUI). **Toggles/dropdowns/buttons now carry
  `AutomationProperties.Name`** — an accessibility improvement that also enables
  UIA testing.
- **`TweakHelpers.BulkActions`** — logic identical; `RunBulk` → `RunBulkAsync`
  with async confirms; pill tooltips via `ToolTipService`.
- **`TweakHelpers.QuickActions`** — WPF `ContextMenu` → WinUI `Flyout` with
  custom two-line rows (`MenuFlyoutItem` is single-line only); bulk confirm
  dialog (warning callout + restore-point `CheckBox`) rebuilt on
  `ConfirmContentAsync`. `BaseTab.PageHeader(withActions)`'s placeholder replaced
  with the real `BuildQuickActionsButton`.
- Logic-only deps added to the compile set unchanged: `TweakRegistry`,
  `RestorePointHelper`, the `SystemStateReader.*` family.

### Live dialog verification (actual click-through, per isleap's requirement)

Method: copied the build output to the scratchpad, swapped the **copy's**
manifest to `asInvoker` with `mt.exe` (repo artifact untouched — still
`requireAdministrator`), launched it non-elevated, and drove it via UI
Automation (the elevated real build is unreachable from a non-elevated
automation client).

**Result — warning-dialog click-through on "Disable Action Center" (PASS):**
- Machine pre-state: `DisableNotificationCenter=1` (Action Center already
  disabled by isleap), so the flow was inverted to end net-zero.
- Toggle ON → no dialog (correct: `WarningState=false` warns only on OFF).
- Toggle OFF → **warning ContentDialog appeared** → **Cancel** clicked → toggle
  visually reverted to ON, **no registry write**. ✅ cancel path
- Toggle OFF → **dialog appeared again** → **OK** clicked → apply proceeded. ✅
  confirm path
- Final machine state = pre-test state (`DisableNotificationCenter=1`).

**Test-harness artifact (not a defect):** the non-elevated copy could not write
`HKCU\Software\Policies\...\Explorer` (restricted ACL) — crash log captured
`SecurityException` on `OpenSubKey(writable)` and `UnauthorizedAccessException`
on `SetValue`, exactly the CLAUDE.md "ACL-locked keys raise SecurityException"
gotcha. The real `requireAdministrator` build writes this key fine. Incidentally
this also live-verified `App.UnhandledException`: both exceptions were caught,
written to `%APPDATA%\AkariTool\AkariTool_crash_2026-08-01.log`, and the app
kept running.

**Supplementary apply-pipeline round-trip on "Clock Change Notifications"
(user-writable `HKCU\Control Panel\Desktop\DstNotification`) — PASS:**
pre `0` → toggle ON → value deleted → toggle OFF → value `0` re-written.
Proves `ApplyToggle` + `DriftBaseline.Record` run end-to-end from a real UI
click when ACLs permit. Machine state restored exactly.

### Deferred / notes

- Focus-grow search animation, pill wrapping, quick-set glow effects, card
  shadows — cosmetic pass (already logged).
- `AkariDialogs` API is now async-only; remaining WPF tabs that call the old
  sync names get their call sites converted in their own batches.
- Two harness crash entries dated 2026-08-01 in the AkariTool crash log are from
  the deliberately de-elevated test run (see above); harmless to delete.

---

## Tab Batch 2 — Update + Debloat — **COMPLETE ✅**

Date: 2026-08-01

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.86
```

### Tabs migrated

- **UpdateTab** (3 sections, 11 tweaks incl. the 4-option Update Policy and
  Delivery Optimization dropdowns) — pure catalog tab; logic byte-identical,
  only the usings + XAML root changed.
- **DebloatTab** — script-row tab (Run/Undo per row → embedded `.ps1` via
  `ToolService.RunWithTracking`). Logic and script list byte-identical.
  Framework-only edits: usings; `Separator` → tagged `Border`; `RunBtn`/`UndoBtn`
  WPF styles → native `AccentButtonStyle`/default (same treatment as
  `BaseTab.AddItem`); `Effect = TweakHelpers.CardShadow()` dropped (cosmetic
  pass); `AutomationProperties.Name` added to Run buttons.
- Both wired into the shell dictionary + `Mark()/ClaimRange()` bracket.

### ⚠ Scope note — Debloat is NOT the selection→removal-generator flow

The batch instructions assumed Debloat hosts the bulk "selection → removal
script" path. In the WPF source, **DebloatTab has no selection UI and no
confirmation dialogs at all** — each Run button immediately executes an embedded
PowerShell script (1:1 from the old Software tab's Bloatware panel). The
checkbox-selection flow that feeds `BloatRemovalScriptGenerator` /
`EdgeRemovalScript` / `OneDriveRemovalScript` lives in the **Software tab**
(later batch). Per tight-scoping, NO confirmation dialog was invented for
Debloat — its no-confirm behaviour is byte-identical to WPF. The removal script
generators remain in the compile set **unchanged** (since Phase 0, as
`AutounattendService` deps). The "selection→removal-script byte-identical"
verification therefore belongs to the Software batch and stays tracked above.

### Live bulk-confirmation verification (actual click-through)

Same method as Batch 1 (build copied to scratchpad, copy's manifest swapped to
`asInvoker` via mt.exe — repo exe untouched — launched non-elevated, driven via
UIA). Exercised on the **Update tab**. No-op proof = SHA256 over `reg.exe`
exports of all 8 registry keys the Update tweaks touch, before launch vs after
exit.

**Path A — tab-level bulk (`RunTabBulkAsync` → `ShowBulkConfirmDialogAsync`):**
Quick actions button → flyout → "Restore Windows defaults" row →
**bulk ContentDialog appeared** (identity confirmed by its
"Create a restore point first (recommended)" checkbox) → **Cancel** clicked.

**Path B — section-level bulk (`RunBulkAsync`):** section bulk-bar "Defaults"
pill → the section had 0 pending on this machine, so the
**`InfoAsync` "Nothing to change" dialog appeared** → OK (a no-op path by
construction).

**Result: PASS** — dialogs shown, nothing executed:
- App log: empty — no `[QUICK]`/applied lines.
- Registry: **all 8 key exports byte-identical (SHA256)** → Cancel was a true
  no-op before any work ran.

Dialog-primitive coverage after two batches: `ConfirmOkCancelAsync` (Batch 1
single-toggle warning), `ConfirmContentAsync`/bulk dialog (Batch 2 A),
`InfoAsync` (Batch 2 B) — every AkariDialogs primitive live-verified at least
once. The section-level OK/Cancel *confirm* variant fires the already-verified
`ConfirmOkCancelAsync`; a confirm-then-proceed bulk run remains for a VM (see
not-yet-verified).

---

## Tab Batch 3 — Privacy + Gaming — **COMPLETE ✅**

Date: 2026-08-01

**Verification bar changed this batch** (isleap, standing instruction): no more
live click-throughs on the dev machine. Verification = compiles clean + launches
without crashing. All functional testing moves to isleap's VM (see checklist).

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.31
```

### Launch status (literal)

Launched the produced `AkariTool.exe` (read-only: constructs all 7 migrated tabs
and runs their `ReadState` registry **reads**; nothing clicked, nothing applied).
- **Alive 8s**, no early exit → all tabs constructed successfully.
- Crash log `%APPDATA%\AkariTool\AkariTool_crash_2026-08-01.log` **unchanged**
  (2278 bytes before and after) → **zero exceptions during tab construction**,
  including every Privacy/Gaming `ReadState` probe.

### Tabs migrated

- **PrivacyTab** — 6 catalog groups (ContentLockGeneral, EdgeOfficeAI,
  SearchActivityApps, Security, SpeechInkingDiagnostics, WindowsAI).
- **GamingTab** — 9 catalog groups (GameMode, Processor, Graphics, Storage,
  Network, Xbox, Security, Accessibility, VisualEffects) plus the
  SystemServices / ScheduledTasks / SystemRestore partials.

### Migration changes (all mechanical)

Survey first: **all 17 catalog files used ZERO WPF types** — their
`using System.Windows / .Controls / .Media` lines were vestigial. The 5 tab
files used only `StackPanel`. So:
- 16 non-Defender catalog files: the 3 vestigial usings **deleted** (no other change).
- 5 tab files (`PrivacyTab.xaml.cs`, `GamingTab.xaml.cs` + 3 Gaming partials):
  usings swapped to `Microsoft.UI.Xaml` / `Microsoft.UI.Xaml.Controls`.
- 2 XAML files re-rooted on `ScrollViewer` + `RootPanel` (`using:` namespace).
- Both tabs wired into the shell dictionary with the `Mark()/ClaimRange()` bracket.
- **No tweak logic, registry path, or catalog entry was altered in any file.**

### ⛔ Defender file — flagged per CLAUDE.md

`Tabs/Gaming/Catalog/GamingTweaks.Security.cs` carries the **live** Defender
disable toggle (`gaming-disable-defender` → `DefenderService.SetAsync`). It
**could not be excluded**: `GamingTab.Build()` calls `GamingTweaks.Security(Log)`
(line 36), and `using System.Windows;` does not resolve in a WinUI project, so
the file had to change to compile at all.

The change was held to the provable minimum — **git diff: 0 additions,
3 deletions**, exactly the three unused `using System.Windows*` lines. No comment
was added, no line reordered. `DefenderService.cs` itself remains byte-for-byte
untouched (unchanged since Phase 0), as does every line of the Defender
`TweakDefinition` (Id, Warning text, `WarningState`, `ReadState`, `Apply`).

**Consequence to be aware of:** the Gaming tab is live in the WPF build too, so
this migration *preserves* existing reachability rather than arming anything new
— the toggle is as reachable in the WinUI build as it is today. But combined
with the still-deferred `--defender-phase2` startup call site, a disable started
in the WinUI build will **not** complete its post-reboot phase 2.

**RESOLVED in Batch 4 — the row is now INERT BY DESIGN.** See the guard note in
the Tab Batch 4 entry below.

---

## Tab Batch 4 — Customize + Power (+ Defender guard) — **COMPLETE ✅**

Date: 2026-08-01

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:09.26
```

### Launch status (literal)

Launched the produced exe (read-only; nothing clicked). **Alive 10s**, all 9
migrated tabs constructed. Crash log **unchanged** (2278 bytes before and after)
→ zero exceptions during construction. Additionally asserted, because Power
constructs powercfg probes: **active power scheme identical before and after**
(`bca373e4-…` / "Akari Performance") → construction confirmed read-only.

### ⛔ Defender toggle — GUARDED (isleap's decision, Batch 4)

Decision: keep the Batch-3 using-line fix, but make `gaming-disable-defender`
non-functional until phase 2 is wired; keep the row visible.

**How it is guarded: APPLY-PATH NO-OP** (not merely a greyed control), in
`Tabs/Gaming/Catalog/GamingTweaks.Security.cs`:
- `Apply = on => { Log("…temporarily inert…"); }` — the body no longer calls
  `DefenderService.SetAsync`. Verified repo-wide: `SetAsync` now appears **only
  inside a comment**, and there are **zero live `DefenderService.SetAsync` /
  `RunPhase2Native` invocation sites** anywhere in the codebase.
- The original one-line body is preserved verbatim in the comment block directly
  above, for exact restoration when re-arming.
- `Services/DefenderService.cs` remains byte-for-byte untouched.

**Why the apply path and not a disabled/greyed toggle** — this row is reachable
*without a click*, so a UI-only disable would not have been a real guard:
- the tab-level **"Restore Windows defaults"** bulk includes it (the row sets
  `DefaultState = false`, and `TryGetDefaultTarget` only requires
  `DefaultState.HasValue`; `RecommendedState` is null so "Apply all recommended"
  correctly skips it), and
- **`TweakRegistry` settings-import** calls `Apply` directly by tweak Id.
A no-op `Apply` blocks click, bulk, and import paths at once.

Also, one UI-copy change (flagged): the row's `Description` is prefixed
`"[Temporarily unavailable in this build] "` so a VM tester doesn't file a false
bug when the toggle does nothing. Trivially revertible.

**Re-arm plan:** restore the commented body together with the `--defender-phase2`
call site in the startup-orchestration sub-phase, and test both as one unit on a
VM (P0 checklist item updated accordingly).

### Tabs migrated

- **CustomizeTab** — 22 partials (Taskbar ×5, Explorer ×7, Desktop ×4,
  ContextMenu ×4, StartMenu, Appearance) + shell.
- **PowerTab** — 9 partials (PlanSelector, Persistence, Probes, Processor,
  Peripherals, Battery, Gpu, MultimediaButtons) + shell.

### Migration changes

Survey first: most partials used only `StackPanel`; 7 files needed real work.
- **Mechanical** (all 33 files): `System.Windows*` → `Microsoft.UI.Xaml*` usings;
  `Microsoft.UI.Text` added where `FontWeights` is used (it moved namespaces).
- **`Separator` → tagged `Border`** (`CustomizeTab.ContextMenu.ScriptGroup.cs`).
- **WPF `Style` resource lookups dropped** — `FindResource("RunBtn"/"UndoBtn")`
  and `Application.Current.Resources["GridBtn"]` → native `AccentButtonStyle` /
  default `Button` chrome with Akari token brushes (same treatment as
  `BaseTab.AddItem`). Affected: ScriptGroup.cs, PowerTab.Persistence.cs.
- **`Dispatcher.Invoke` → `DispatcherQueue.TryEnqueue`** (4 sites: Persistence ×2,
  PlanSelector ×2).
- **`.ToolTip =` → `ToolTipService.SetToolTip`** (Persistence, PlanSelector).
- **Mouse → pointer events** on the plan cards: `MouseEnter`/`MouseLeave` →
  `PointerEntered`/`PointerExited`, `MouseLeftButtonUp` → `Tapped` (also covers
  touch/pen). 6 sites in PlanSelector.
- **Dropped, no WinUI equivalent:** `Cursors.Hand` (3 sites — WinUI uses
  `ProtectedCursor`, not settable this way) and
  `Typography.KerningProperty` (1 site, PlanSelector header). Both cosmetic.

**Power logic layer untouched:** every `powercfg` argument string, GUID, probe
table, scheme-drift rule and `SetPowerCfg`/`EnsureAkariScheme` body is
byte-identical. Only framework calls around them changed. The CLAUDE.md invariant
"never reactivate from a read path" was re-verified: `EnsureAkariScheme()` (the
`/duplicatescheme` + `/setactive` writer) is called **only** from `SetPowerCfg`,
never from `RefreshPersistIndicator`/`RefreshActiveCard`.

---

## Tab Batch 5 — Home + Tools + AppUpdate — **COMPLETE ✅**

Date: 2026-08-01

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.18
```

### Launch status (literal)

**The launch check caught a real runtime crash on the first attempt** — see
"Stale style-resource lookups" below. After the fix: **alive 12s**, all 12
migrated tabs constructed, crash log **unchanged** (4065 bytes before and after)
→ no exceptions, including Home's background WMI gather and AppUpdate's
changelog fetch marshalling back to the UI thread.

### ⚠ Stale style-resource lookups — a latent-crash class worth knowing about

First launch died with
`COMException … Cannot find a resource with the given key: GridBtn`
from `ToolsTab.MakeButton`. Cause: `(Style)Application.Current.Resources["X"]`
lookups for WPF styles (`RunBtn`, `UndoBtn`, `GridBtn`, `AppCheckBox`,
`AppButton`) that no longer exist — the old `AkariFluentTheme.xaml` styles were
not ported (WinUI has no `ControlTemplate.Triggers`). These **compile fine** and
only throw when the code path runs, so a build check cannot catch them.

Repo-wide sweep result: **28 sites**. All fixed or accounted for:
- **Compiled files: now zero.** Fixed this batch in `ToolsTab` (3) and earlier in
  `BaseTab`, `DebloatTab`, `CustomizeTab.ContextMenu.ScriptGroup`,
  `PowerTab.Persistence`, `AppUpdateTab`.
- **Remaining 24 sites are in files not yet in the compile set** — `AkariOS` (11),
  `OSTweaks` (8, the dead tab — will not be migrated), `Software` (2),
  `AdvanceTools` (2). **Action for the upcoming batches: sweep for these keys
  before declaring a batch done.** No latent bug exists in batches 1–4 (those
  files are not compiled).

### Tabs migrated

- **HomeTab** — system-info banner, global search box + results, and the 14
  quick-access nav cards.
- **ToolsTab** — system info + copy, repair, network/DNS, maintenance, shortcuts.
- **AppUpdateTab** — status card, check/update buttons, live changelog.

Shell: all three wired in; **Home added to the rail (landing tab, matching WPF)**
and **App Updates added to the footer**. A new `SelectNavItem(tag)` helper drives
rail selection from Home's cards/search results.

### Home — live-data / custom-UI items (flagged as requested)

1. **System-info banner (live WMI)** — `SystemInfoService.Gather()` still runs on
   a background thread; the marshal-back changed from `Dispatcher.Invoke`
   (synchronous) to `DispatcherQueue.TryEnqueue` (**fire-and-forget**). Behaviour
   is equivalent here (labels start at "Detecting…" and fill in), but it is a
   real semantic change — noted for VM verification.
2. **Nav-icon artwork** — the WPF build resolved `NavIco_*` keys from
   `Themes/NavIcons.xaml`. That dictionary was not ported; the map now holds file
   names and images are built from `ms-appx:///Resource/NavIcons/<name>.png`
   (the PNGs already ship as Content since Phase 0). Same artwork, no dictionary.
   Includes the light-theme AkariOS variant (`akarios_light`).
3. **Global search box** — WPF-UI `TextBox` → WinUI `AutoSuggestBox`.
   `CaretBrush`/`SelectionBrush` have no per-instance equivalent; the crimson
   caret/selection now come from the `TextControl*` theme overrides in App.xaml.
4. **Search sources wiring** — `SetupGlobalSearch` is fed from the migrated tabs;
   each tab's root panel is resolved via `FindName("RootPanel")` so **no tab XAML
   or BaseTab signature changed**. Sources currently cover the 12 migrated tabs
   and grow automatically as later batches land.
   **This is HomeTab's own search box — the rail-pinned global "Find a setting"
   box remains a separate deferred restore item (unchanged).**
5. **Result rows / cards** — `MouseLeftButtonUp`→`Tapped`,
   `MouseEnter`/`MouseLeave`→`PointerEntered`/`PointerExited`; `Cursors.Hand`
   dropped (no WinUI equivalent); result-row `Grid` given a transparent
   background so the whole row stays hit-testable.
6. `TextBlock.ToolTip` → `ToolTipService.SetToolTip` (GPU names are long);
   `RenderOptions.SetBitmapScalingMode` dropped (WinUI handles DPI itself);
   `Separator` → tagged `Border`.

### AppUpdate — startup trigger DEFERRED, not half-wired (confirmed)

Verified by grep: **`MainWindow.xaml.cs` and `App.xaml.cs` contain no reference
to `UpdateService` at all** — the `Loaded → UpdateService.CheckAsync → navigate
to AppUpdate` flow is entirely absent, not partially present. `UpdateService` is
referenced only from `AppUpdateTab` (user-initiated "Check for Updates") and
`AboutTab` (reads `CurrentVersionDisplay`, a version string — not a check).
The startup trigger stays with the deferred startup-orchestration cluster
(splash, `--competitive`, Defender phase 2, Defender toggle re-arm).

Other AppUpdate changes (logic byte-identical — GitHub Releases API, download,
installer launch args all unchanged):
- **`Geometry.Parse` does not exist in WinUI** → the four state icons are parsed
  from the **same path-data strings** via `XamlReader.Load` of a `<Path Data='…'/>`.
- **Spinner storyboard** rebuilt for WinUI: `DoubleAnimation` object-initializer
  (no `(from,to,duration)` ctor), `SetTargetProperty` takes a **string** (no
  `PropertyPath`), and `EnableDependentAnimation = true` is required.
- `new RotateTransform(0)` → `new RotateTransform { Angle = 0 }`;
  `Point` → `Windows.Foundation.Point`.
- **`Application.Current.Shutdown()` → `Application.Current.Exit()`** in the
  post-download installer-launch path (framework-only; the installer arguments
  `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1` are unchanged).
- `AppButton` style → native `AccentButtonStyle`.

### Tools — notable changes

- **`Clipboard.SetText`** (WPF static) → WinRT `DataPackage` +
  `Windows.ApplicationModel.DataTransfer.Clipboard.SetContent` (same behaviour).
- **`UniformGrid` has no WinUI equivalent** → hand-built 2-column `Grid` with the
  same left-to-right/top-to-bottom fill for the 10 shortcut buttons.
- **`WrapPanel` has no WinUI equivalent** → the 4 short DNS buttons fit one row,
  so a horizontal `StackPanel` is behaviourally equivalent.
- `Effect`/`CardShadow()` dropped; `Separator` → `Border`; `Cursors.Hand` dropped;
  `RunBtn`/`GridBtn` styles → native button chrome.

---

## Tab Batch 6 — AkariOS + Advanced Tools (+ OSTweaks disposition) — **COMPLETE ✅**

Date: 2026-08-01

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.25
```

### Launch status (literal)

**Alive 14s**, all **14** migrated tabs constructed, crash log **unchanged**
(4065 bytes before and after) → no exceptions during construction.

### ✅ Stale-style sweep (hard gate) — ZERO live sites

Swept **115 compiled files** for `FindResource(...)` and
`Application.Current.Resources[...]`. Every surviving lookup resolves:
- **`AccentButtonStyle`** — a real WinUI built-in (ships in `XamlControlsResources`).
- **`MonoFont`** — defined in the migrated `App.xaml`.

**Zero** references remain to the unported trigger-based styles
(`RunBtn`, `UndoBtn`, `GridBtn`, `AppCheckBox`, `AppButton`, `AkariCard`,
`SectionHeader`, `Badge*`). Fixed this batch: 10 sites across
`AkariOSTab.Account/Competitive/GpuTools/Services/ShaderCache` and
`AdvancedToolsTab.Wizard`.
One was an **indirect** lookup a naive grep would miss:
`MakeCompetitiveButton(label, "GridBtn"|"RunBtn", …)` passed the style **key as a
string parameter** and resolved it inside. It now maps the key to native chrome
(`RunBtn` → `AccentButtonStyle`, else default), so those string literals are no
longer resource lookups. Call sites unchanged.

### Tabs migrated

- **AkariOSTab** — 7 partials (Account, Competitive, GamingTweaksCard, GpuTools,
  PostInstall, Services, ShaderCache) + shell.
- **AdvancedToolsTab** — 5 partials (Generator, Landing, Steps12, Steps34,
  Wizard) + shell.
- Also pulled into the compile set (logic-only, unchanged): `ServicesPreset*`
  (×3), `PlaybookTweaks*` (×7), `BcdBackup`, `DriftScanner`.

### Substantive migration work

- **File/folder pickers (6 sites)** — `Microsoft.Win32.OpenFileDialog` /
  `SaveFileDialog` / `OpenFolderDialog` do not exist in WinUI. New
  `Tabs/Shared/FilePickers.cs` wraps `FileOpenPicker` / `FileSavePicker` /
  `FolderPicker`. Two real consequences: the pickers are **async** (each call
  site became `await`, incl. `BrowseForGame` → `BrowseForGameAsync`), and in an
  **unpackaged** app a picker throws unless initialised with the window HWND —
  so `MainWindow.WindowHandle` was added and every picker calls
  `InitializeWithWindow`. **VM-critical: these are unverifiable without clicking.**
- **WPF-UI `MessageBox` (3 sites)** → `AkariDialogs.ConfirmContentAsync`
  (Competitive disclaimer, network-reboot confirm, shader-clean confirm). All
  three were already `await`-based, so no control-flow restructuring; wording and
  button labels unchanged. The old "don't use AkariDialogs, it pumps a nested
  dispatcher frame" comments are now obsolete — WinUI dialogs are natively async.
- **`Window.GetWindow(this)` / `Window.Hide/Show` / `WindowState`** (Competitive
  Mode hides the app during a session) → `MainWindow.Instance` + `AppWindow.Hide()`
  / `AppWindow.Show()` / `OverlappedPresenter.Restore()`.
- **`Border.IsEnabled`** (Advanced Tools step gating) — WinUI puts `IsEnabled` on
  `Control`, not `FrameworkElement`. Interactivity now uses `IsHitTestVisible`;
  the existing `Opacity` already supplied the disabled look.
- **`DispatcherTimer.Tick`** — WinUI signature is `EventHandler<object>` (WPF used
  `EventHandler`), so the Competitive elapsed-timer handler changed to
  `(object? sender, object e)`.
- **`WrapPanel`** (shader-cache targets) — no WinUI equivalent; rows carry long
  "<target> — <size>" labels, so they now stack **vertically** (one per line)
  rather than wrapping. Layout change, flagged for VM review.
- **Dropped, no WinUI equivalent:** `Cursors.*` (several), `Border.ClipToBounds`,
  `BitmapCache` (paired with the already-dropped shadow), `TextBox.CaretBrush` /
  `SelectionBrush` (crimson caret now comes from the App.xaml theme overrides),
  `Effect`/`CardShadow()`.
- `FontStyles.Italic` → `Windows.UI.Text.FontStyle.Italic`;
  `.ToolTip =` → `ToolTipService.SetToolTip` (3 sites); `Wpf.Ui.Controls.ProgressRing`
  → native `ProgressRing`; `Separator` → tagged `Border`; mouse → pointer/Tapped.

### ⛔ OSTweaksTab disposition — EXCLUDED, not deleted (and why)

**Decision: excluded from the compile set + left on disk for the cleanup pass.**
I did **not** delete the folder, deliberately:

- CLAUDE.md states the OSTweaks folder also contains the **superseded Defender
  implementation** (`OSTweaksTab.Defender.cs`, `OSTweaksTab.Security.cs`) and that
  deleting it requires explicit sign-off on whether that machinery should be
  preserved elsewhere first — "do not decide this unilaterally". Deleting the
  folder mid-migration would take that code with it, so exclusion is the
  reversible choice you offered.
- `MinSudoService.cs` **does not exist** in this repo (already removed before the
  migration, matching CLAUDE.md) — nothing to delete.
- The exclusion is **already effective**: the csproj's blanket
  `<Compile Remove="Tabs\**\*.cs" />` plus per-batch re-includes means
  `Tabs\OSTweaks\**` has never been compiled in any batch. It is now documented
  explicitly in the csproj so it cannot be re-added by accident.
- Consequence: the 8 stale-style sites inside OSTweaks are **unreachable and
  harmless** — they are not compiled.

**DECIDED (isleap, 2026-08-01): PRESERVE the old Defender copy. Do NOT delete it
in the cleanup pass.** `Tabs/OSTweaks/**` stays on disk, excluded and uncompiled,
exactly as it is now — including `OSTweaksTab.Defender.cs` / `.Security.cs`.
Rationale: CLAUDE.md requires explicit sign-off; the live Defender path is still
deferred (phase 2 unwired, gaming toggle inert); deleting is irreversible while
keeping it costs nothing. **Revisit deletion ONLY after the startup-orchestration
sub-phase lands and the full Defender flow has been VM-tested end-to-end.**

### ✅ `os-set-utc` relocated (the last live tweak in the dead tab)

Moved **verbatim** from `Tabs/OSTweaks/Catalog/OSTweakCatalog.Performance.cs`
into `Tabs/Customize/CustomizeTab.Desktop.Regional.cs`, appended to the
**Regional Settings** section (it governs how the hardware clock is interpreted,
alongside the other International/clock rows).

Unchanged: `Id = "os-set-utc"` (so exported configs and the registry keep
matching), Name, Description, `IsPreference`, the read
(`HKLM\SYSTEM\CurrentControlSet\Control\TimeZoneInformation\RealTimeIsUniversal`,
absent ⇒ false) and the apply (set `1` / delete the value), including the
"Restart to apply." log wording.
Only change: the catalog's injected `Log(...)` delegate became `Service?.Log(...)`
(the tab's logger), and a local `ReadDwordLm` helper replaces the catalog's
`ReadDword`. It renders through `TweakHelpers.AddTweakRow`, so it is registered
with `TweakRegistry` exactly as before.

---

## Tab Batch 7 — Software (destructive removal flow) — **COMPLETE ✅**

Date: 2026-08-01

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.51
```

### Launch status (literal)

**Alive 15s**, all **15** migrated tabs constructed (incl. the Software card
grids), crash log **unchanged** (4065 bytes) → no exceptions.

### ✅ Stale-style sweep (hard gate) — ZERO live sites

**The gate caught a real miss in this batch**: `SoftwareTab.Cards.cs:74` still
carried `Resources["AppCheckBox"]` (an earlier bulk replace missed it on
indentation). It compiles fine and would have thrown a `COMException` the moment
a card rendered. Fixed → native `CheckBox` chrome (its checked fill already comes
from the crimson accent overrides in App.xaml).

Sweep now covers **both** patterns:
- **(a) direct** — `FindResource("X")` / `Application.Current.Resources["X"]`
- **(b) indirect** — the style key passed as a **string/ternary**, e.g.
  `Resources[primary ? "RunBtn" : "GridBtn"]` and
  `MakeCompetitiveButton(label, "GridBtn", …)`. Pattern (b) is what a naive grep
  for `FindResource` misses.

Result over the 148 compiled files: every surviving lookup resolves
(`AccentButtonStyle` — WinUI built-in; `MonoFont` — App.xaml). The only remaining
matches are the documented Competitive.cs **string parameters** feeding the
native-chrome mapper, which are no longer resource lookups.

⚠ **Known, not yet compiled:** `Tabs/Backup/BackupTab.xaml.cs:231` and
`Tabs/Verify/VerifyTab.xaml.cs:337` both use the **indirect ternary** form
`Resources[primary ? "RunBtn" : "GridBtn"]`. Fix both in the Backup & Restore /
Verify batch.

### Selection → removal-script wiring: **BYTE-IDENTICAL** (verified)

`git diff --numstat HEAD` over the logic layer returns **zero changed rows**:

```
Tabs/Software/Removal/BloatRemovalScriptGenerator.cs   (unchanged)
Tabs/Software/Removal/EdgeRemovalScript.cs             (unchanged)
Tabs/Software/Removal/OneDriveRemovalScript.cs         (unchanged)
Tabs/Software/SoftwareAppService.cs                    (unchanged)
Tabs/Software/AppModels.cs                             (unchanged)
Tabs/Software/Catalog/**  (all 20 files)               (unchanged)
```

Stronger still: `Removal/**`, `SoftwareAppService.cs` and `AppModels.cs` have been
in the compile set **since Phase 0** (pulled in as `AutounattendService`
dependencies), so they have been compiling untouched for the whole migration.

**The chain, unchanged end-to-end:**
1. UI ticks a card → `AppDefinition.IsSelected` (model, unchanged).
2. `RemoveSelectedWindowsAppsAsync()` collects
   `_windowsApps.Where(a => a.IsSelected)` — identical LINQ.
3. → `SoftwareAppService.RemoveWindowsAppsAsync(selected, log, status)` —
   **unchanged call, unchanged signature**.
4. → `BloatRemovalScriptGenerator.GenerateScript(...)` /
   `ExtractArrayFromScript(...)` for the merge-into-saved-script path —
   **unchanged**.
5. Edge/OneDrive come from the catalog as
   `RemovalScript = () => EdgeRemovalScript.GetScript()` /
   `OneDriveRemovalScript.GetScript()` in `WindowsAppCatalog.cs` — **unchanged**.

Only the 8 UI files changed (`SoftwareTab.*`, `AppIconService`, and the XAML):
`19 insertions / 288 deletions` in the XAML alone (the retemplates), and small
edits elsewhere. **No selection semantics, no generator input, no script text.**

### Guarding dialogs (what stands between a click and destruction)

| Flow | Guard | Change |
|---|---|---|
| **Remove Selected** (Windows apps — the destructive one) | `AkariDialogs.ConfirmYesNoAsync` — "Remove N item(s) from Windows?", lists the first 10, appends "⚠ …removal can affect Windows components that depend on it" for any `HasInstabilityWarning` item, and notes "A startup task keeps these removed after Windows updates." | sync → **await**; message, title and Yes/No unchanged; still returns **before** any removal work when declined |
| **Uninstall Selected** (external apps) | `AkariDialogs.ConfirmYesNoAsync` — "Uninstall N app(s)?" + first 10 names | sync → **await**; otherwise unchanged |
| **Install Selected** (permanent-item notice) | `AkariDialogs.InfoAsync` — "These items are permanent … and will be skipped" | sync → **await**; the await preserves ordering (notice dismissed before the filtered install runs) |

All three now route through the WinUI `ContentDialog` path that was
click-through-verified in Batches 1–2. **No confirmation was added or removed** —
the WPF guards are reproduced exactly.

### Other migration changes (UI only)

- **`SoftwareTab.xaml`: 288 lines deleted.** It was almost entirely WPF
  `ControlTemplate`/`Trigger` retemplates (6 styles: `DlCheckBox`,
  `DlListViewItem`, `DlSearchBox`, `DlFilterBtn`, `DlActionBtn`, `DlCancelBtn`)
  that existed to suppress the system highlight and paint Akari chrome. WinUI has
  no trigger system and the brief says rebuild native rather than port. Only
  `DlActionBtn`/`DlCancelBtn` were referenced from C# → now
  `AccentButtonStyle` / default chrome. Body was always just `RootPanel`.
- **`UniformGrid` (new: `Tabs/Shared/UniformGrid.cs`)** — WinUI has no
  `UniformGrid`, and the Software card grid is **responsive** (recomputes
  `Columns` from `ActualWidth` on `SizeChanged`, and the search filter rebuilds
  `Children` because collapsed children still reserve cells). Rather than
  restructure that onto `ItemsRepeater`/`UniformGridLayout` (Children → ItemsSource,
  which would have rippled through the card + search code), I implemented a small
  WinUI `Panel` exposing the **same API** (`Columns`, `Rows`, `Children`,
  `SizeChanged`, `ActualWidth`) so **every call site is unchanged**.
  ⚠ Custom layout code — needs eyes on the VM (see checklist).
- **`AppIconService.LoadFrozen`** — WPF's `BeginInit`/`EndInit`/`CacheOption`/
  `Freeze` don't exist on WinUI `BitmapImage`. Properties are set directly;
  `DecodePixelWidth = 80` kept. `CacheOption.OnLoad` has no analogue (WinUI does
  not hold the file handle) and `Freeze()` was for cross-thread WPF use — WinUI
  image objects must be created and used on the UI thread instead.
  **Behavioural note:** icon objects are no longer freezable/cross-thread.
- Search box: WPF-UI `TextBox` → `AutoSuggestBox` (caret/selection colours now
  from the App.xaml `TextControl*` overrides).
- `Cursors.Hand` dropped; `.ToolTip =` → `ToolTipService.SetToolTip`;
  `Effect`/`CardShadow()` dropped; `Run(string)` ctor → `Run { Text = … }`.

### Shell wiring

`SoftwareTab` serves **two** rail tags (`Bloatware` → Windows Apps,
`AppInstaller` → External Apps) through its existing `ShowPanel(name)`, exactly
as the WPF shell did; both tags map to the same instance and
`Nav_SelectionChanged` calls `ShowPanel`. The Advanced Tools autounattend
generator is re-wired to the Software tab's ticked apps via
`SetSelectedAppsProvider(() => _software.GetSelectedWindowsApps())` —
identical to the WPF shell.

---

## Shell cluster — global rail search + drift banner — **COMPLETE ✅**

Date: 2026-08-01. Two shell-level deferred items done as one batch (no tab touched).

### Build status (literal)

Clean rebuild from scratch:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:13.47
```

Launch: **alive**, nav assertion **green**, crash log **unchanged** (4065). Stale-
style sweep: **zero live sites** (only the documented Competitive.cs mapper params).

### Global "Find a setting" search (rail pane header)

The deferred rail-pinned global search (distinct from Home's own box) is restored.

- WPF used a `ui:TextBox` + a `Popup` hosting a `ListBox` with a two-line
  `DataTemplate`. WinUI replaces all of that with the NavigationView's native
  **`AutoSuggestBox`** (`NavigationView.AutoSuggestBox`), whose built-in
  suggestion dropdown replaces the Popup/ListBox. Same two-line item
  (tweak name + tab label) via `ItemTemplate`.
- Logic is unchanged: `TweakRegistry.Search(q)` (min 2 chars), and on a chosen hit
  `SelectNavItem(hit.TabTag)` then `tab.ApplySearch(hit.Name)` at **Low** dispatcher
  priority so the tab is visible before its own search box runs — mirroring WPF's
  `DispatcherPriority.Loaded`. `TweakRegistry` and `BaseTab.ApplySearch` untouched.
- **Improvement over a literal port:** `hit.TabTag` may be a **sub-panel** tag
  (e.g. `Taskbar`); `NavigateToHit` resolves the owning tab through `_subInfo`
  before calling `ApplySearch`, so a hit inside a Customize sub-panel navigates
  and filters correctly.
- Events: `TextChanged` (guarded to `UserInput`) populates suggestions;
  `SuggestionChosen` navigates to the picked hit; `QuerySubmitted` (Enter)
  navigates to the chosen or first hit.

### Drift banner (shell, above content)

The banner the Verify tab already expected (its `RefreshDriftBanner` call site was
a no-op stub since Batch 9) is now real.

- **`Tabs/Shared/DriftBanner.cs`** ported to WinUI and added to the compile set
  (it had never been compiled). Framework-only changes: `Run(string)` ctor →
  `Run { Text = … }`; `Brushes.Transparent` → `SolidColorBrush(Colors.Transparent)`;
  `Cursors.Hand` dropped; `.ToolTip =` → `ToolTipService.SetToolTip`. Wording,
  layout (glyph ring · message · Review · ✕), and `BuildDetail` are unchanged.
- **Shell host:** a `ContentControl x:Name="DriftBannerHost"` was added as a new
  **row 0** above `ContentHost` (which moved to row 1; the log panel moved to
  row 2). Collapsed by default.
- `MainWindow.RefreshDriftBanner` is now the real WPF implementation
  (`HasDrift ? Show : Hide`); `RunStartupDriftScan()` runs once on `Loaded`
  (`DriftScanner.Scan()`, guarded, never surfaces a scan failure); Review →
  `SelectNavItem("Verify")`; dismiss hides until next launch. The Batch-9 no-op
  stub is gone.

### ⚠ Not exercisable by launch here

Both need state to show: global search needs ≥2 chars typed; the drift banner
needs actual drift (`DriftScanner.Scan()` finding reverted tweaks). Neither
appears on a clean launch, so both are **VM checklist** items (added below).

---

## Startup orchestration sub-phase — items 1–4 **COMPLETE ✅** (5–6 HELD→APPLIED)

Date: 2026-08-01

### Build status (literal)

Clean rebuild from scratch:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.81
```

Launch: splash visible at t+6s, main window up by t+20s, **nav assertion green**,
crash log **unchanged** (4065 bytes).

### 1. Splash window + staged startup progress ✅

`SplashWindow.xaml(.cs)` ported and re-added to the compile set (it had been
excluded since Phase 0). Notable WinUI deltas:

- **Window chrome has no XAML equivalent.** WPF's `WindowStyle=None`,
  `ResizeMode=NoResize`, `Topmost`, `WindowStartupLocation=CenterScreen` are now
  applied in code via `AppWindow` + `OverlappedPresenter`
  (`SetBorderAndTitleBar(false,false)`, `IsResizable=false`, `IsAlwaysOnTop=true`,
  `Resize`, and a manual centre on `DisplayArea.WorkArea`).
- **`Window.Opacity` does not exist in WinUI**, so `FadeOutAndCloseAsync` fades
  the **root element** instead and closes when the storyboard completes.
- **`element.BeginAnimation(...)` does not exist.** The pip pulse is now a
  `Storyboard` retargeted at the active pip, and is explicitly `Stop()`ped before
  each `Report()` so its hold value cannot fight the newly-assigned opacities.
- Every `DoubleAnimation` needs `EnableDependentAnimation="True"` (Opacity and
  TranslateTransform.Y are dependent animations in WinUI).
- `RenderOptions.BitmapScalingMode` dropped; `{DynamicResource}` → `{ThemeResource}`.
- The 7-stage sequence, labels, `MinStepMs = 320` hold and the 300 ms 100% hold are
  reproduced exactly.
- **`Dispatcher.Yield(DispatcherPriority.Background)`** (used to flush a paint
  between stages) has no WinUI equivalent → `PaintAsync()` re-enqueues at
  `DispatcherQueuePriority.Low` and awaits that.

### 2. Persisted-theme apply ordering ✅

WPF applied the theme at the very top of `OnStartup`, before the splash existed,
so the splash painted with correct tokens. Reproduced: `App.ShowSplashAndLaunchAsync`
calls `ThemeService.LoadPersisted()` → constructs the splash → `AttachRoot(splash root)`
→ `Apply(theme)` → `splash.Activate()`.

`ThemeService.AttachRoot` was widened to accept a **nullable** root, ignore null,
and **re-assert `RequestedTheme` on the newly attached root** — necessary because
the theme root now changes once (splash → MainWindow) during startup.

### 3. `--competitive` shortcut path ✅

`ParseCompetitiveArgument()` ported verbatim (returns null for a missing or
non-existent path so a stale shortcut falls back to a normal launch). On that
path the main window is **not** activated; the splash still fades out, then
`CheckOrphanedCompetitiveSessionAsync()` and
`StartCompetitiveFromCommandLineAsync(exe)` run.

**WPF's `ShutdownMode` juggling is gone and not needed:** WPF had to flip
`OnExplicitShutdown` → `OnMainWindowClose` so closing the splash didn't kill the
app. In WinUI the app stays alive while any Window exists and a never-Activated
window still counts, so the hidden-window competitive path keeps the process
running with no equivalent setting.

`CheckOrphanedCompetitiveSessionAsync` was ported onto the WinUI shell. **One
genuine simplification:** WPF had to `Show()` the hidden window purely because a
WPF dialog requires a *shown* Owner, then `Hide()` it again. A WinUI
`ContentDialog` needs only a `XamlRoot`, which exists as soon as content is
loaded — so **the window is no longer revealed just to host the prompt**. The
Restore/Ignore semantics (including clearing the record either way) are unchanged.

### 4. Startup update check ✅ (previously deferred — now wired)

`MainWindow.Loaded` → `RunStartupUpdateCheckAsync()` → `UpdateService.CheckAsync()`.
Silent on error and when up to date (`catch { return; }`, and returns unless
`Status == UpdateAvailable`), exactly as WPF. On "Update now" it navigates to the
App Updates tab via `SelectNavItem("AppUpdate")`. The WPF-UI MessageBox became
`AkariDialogs.ConfirmContentAsync` with the same wording and
"Update now" / "Later" buttons.

### 5 + 6. Defender — **APPLIED (isleap approved, 2026-08-01)** ⛔→✅

Both landed together, A-before-B interlock intact. Clean build 0/0; normal launch
confirmed the phase-2 path is **not** triggered without the flag
(`defender-phase2.log` absent).

#### Registration-path confirmation (the failure mode isleap flagged)

The reboot handoff is what makes phase 2 actually fire; the concern is that its
registration drifted during migration. **It did not.** Verified:

- **`Services/DefenderPhase2Scheduler.cs` — CONTENT-IDENTICAL to WPF** (git sees
  zero changes; line-ending-normalised hash matches). This is the whole handoff:
  `ScheduleRunOnce()` writes
  `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`, value
  **`AkariDefenderPhase2`** = `"<Environment.ProcessPath>" --defender-phase2`.
  `ClearRunOnce()` deletes that value; `IsScheduled()` checks it. Unchanged.
- **`Services/DefenderService.cs` — CONTENT-IDENTICAL to WPF.** The registration
  is invoked **inside the untouched engine**:
  `DefenderService.SetAsync` phase 1 calls `DefenderPhase2Scheduler.ScheduleRunOnce()`
  at **line 92 — the exact same line as WPF** (`HEAD:Services/DefenderService.cs:92`).
- **`Services/ElevationService.cs` — CONTENT-IDENTICAL** (phase 2 writes the ELAM-
  protected keys as SYSTEM through this; unchanged).
- **Same entry points from both call sites:**
  - re-armed toggle → `DefenderService.SetAsync(on, ToolService.Current!)` — WinUI
    line 88 vs WPF line 86, executable line **identical** (only vestigial `using`
    removals + comments differ in that file).
  - `--defender-phase2` branch → `DefenderService.RunPhase2Native(Log)` +
    `DefenderPhase2Scheduler.ClearRunOnce()` — same calls as WPF `App.xaml.cs`.

**Full chain, unchanged end-to-end:** toggle ON → `SetAsync(disable:true)` →
[Tamper check → DISM package removal → **`ScheduleRunOnce()` writes the HKLM
RunOnce trigger**] → user reboots → Windows RunOnce launches
`AkariTool.exe --defender-phase2` → `App.OnLaunched` phase-2 branch →
`RunPhase2Native` (writes Defender keys as SYSTEM) → `ClearRunOnce()` → exit.
Every step except `Shutdown()→Exit()` is byte-identical to WPF.

#### What changed (framework-only)

- `App.xaml.cs`: added the `--defender-phase2` branch + `RunDefenderPhase2Headless`
  (verbatim WPF port; `Shutdown()`→`this.Exit()`; WPF's `ShutdownMode` line dropped
  — no window is created on this path so it is unnecessary).
- `GamingTweaks.Security.cs`: `Apply` body restored verbatim (from the guard
  comment); the `"[Temporarily unavailable in this build]"` description prefix
  **removed** per instruction. `RecommendedState`/`DefaultState`/`Warning`/
  `WarningState`/`ReadState` untouched.

#### ⚠ Defender is UNVERIFIABLE by compile / launch / assertion

Nothing in this migration's automated bar (compile-clean, launches-clean,
nav-assertion-green, stale-style sweep) exercises the Defender engine — it only
runs on a real disable + reboot. **Only the VM round-trip below proves it.**
Treat Defender as NOT done until that round-trip passes on a VM.

---

## Tab Batch 9 — Verify (FINAL TAB) + content-width fix — **COMPLETE ✅**

Date: 2026-08-01. **All tabs are now migrated.**

### Build status (literal)

Clean rebuild from scratch (`obj/x64`, `bin/x64` deleted first):

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.43
```

Launch: **alive**, 16 tabs, crash log **unchanged** (4065 bytes),
**nav-contract assertion GREEN with an empty allow-list** — `_notYetMigrated` is
now `new()`, so *every* rail tag is enforced with no exemptions.

### 🐛 Content-width / centering fix (all tabs, one shell-level change)

**Root cause — and it was NOT in the shell; it was my own migration artifact.**
The WPF tab XAML was simply `<StackPanel x:Name="RootPanel"/>` with **no width
cap**, so content filled the pane. When each tab's XAML was rewritten during the
migration I added `MaxWidth="920"` (`1000` on Software). A `StackPanel` whose
`HorizontalAlignment` is the default **Stretch** but which is clamped by
`MaxWidth` is laid out **LEFT** inside the extra space — producing exactly the
reported large right-hand gutter, on every tab.

**Fix — one place, `MainWindow.BuildContent().Init()`:**

```csharp
if (tab.FindName("RootPanel") is FrameworkElement rootPanel)
{
    rootPanel.MaxWidth = double.PositiveInfinity;   // element: tab's RootPanel StackPanel
    rootPanel.HorizontalAlignment = HorizontalAlignment.Stretch;   // property: MaxWidth + HorizontalAlignment
}
```

**Exact element/property changed:** each tab's `RootPanel` `StackPanel` —
`MaxWidth` (920/1000 → `PositiveInfinity`) and `HorizontalAlignment` (→ `Stretch`).
No individual tab file was edited.

**Ordering matters and is deliberate:** this runs **before** `tab.Initialize()`,
because **About (860)**, **AppUpdate (720)** and **Update (860)** set their own
`RootPanel.MaxWidth` + `HorizontalAlignment.Center` inside `Build()` — that is
original WPF behaviour, and running first means those intentional per-tab widths
still win.

**Verified empirically at three window widths** (temporary instrumentation,
reverted; de-elevated copy used so the window could be resized):

```
[Loaded]      ContentHost=1683  RootPanel=1619  align=Stretch max=∞
[SizeChanged] ContentHost=1663  RootPanel=1599  align=Stretch max=∞
[SizeChanged] ContentHost= 763  RootPanel= 699  align=Stretch max=∞
[SizeChanged] ContentHost= 523  RootPanel= 459  align=Stretch max=∞
```

`ContentHost − RootPanel = 64` at **every** width — exactly the ScrollViewer's
`Padding="32,28,32,28"` (32 left + 32 right). Content now fills the pane with
balanced margins, wide and narrow.

### Tab migrated — Verify (the last one)

- `IsVisibleChanged` → `Loaded` (no WinUI equivalent).
  ⚠ **Behavioural delta:** the drift scan no longer re-runs on every re-visit —
  it runs on load; use the tab's Re-scan button to refresh. VM item added.
- **`VerifyTab.xaml.cs:337`** — the indirect ternary
  `Resources[primary ? "RunBtn" : "GridBtn"]` → native `AccentButtonStyle` /
  default chrome. **This was the last known stale-style site.**
- `AkariDialogs.ConfirmOkCancel` → `ConfirmOkCancelAsync` + `await` (the
  "Re-apply all" guard); same prompt, still returns before any re-apply on cancel.
- WPF `Run(string)` ctor → `Run { Text = … }`; `Brushes.Transparent` →
  `SolidColorBrush(Colors.Transparent)`; `Cursors.Hand` and `.ToolTip =` dropped /
  moved to `ToolTipService`; `Effect`/`CardShadow()` dropped.
- **`Application.Current.MainWindow`** does not exist in WinUI → routed to
  `MainWindow.Instance`. Verify calls `RefreshDriftBanner(result)`; the **drift
  banner UI is not ported** (deferred cluster), so the shell now exposes a
  documented **no-op `RefreshDriftBanner`** that keeps the wiring intact — landing
  the banner later is a one-place change. Flagged in the deferred list.

### ✅ Stale-style sweep — ZERO live sites (151 files, direct + indirect)

Only `AccentButtonStyle` (WinUI built-in) and `MonoFont` (App.xaml) remain, plus
the documented Competitive.cs mapper **string parameters**. `VerifyTab:337` is
fixed — **no unported style key is referenced anywhere in compiled code.**

---

## 📊 "433 tweaks tracked" — how it is derived, and the reconciliation

`TweakRegistry.Count` = the number of rows registered through
`TweakHelpers.AddTweakRow()`. Each tab is bracketed in the shell with
`Mark()` → `Initialize()` → `ClaimRange(tag, label, start)`, so every tab owns a
**contiguous** index range. **433 is the sum of those per-tab ranges.**

Measured at runtime (temporary instrumentation on `ClaimRange`, since reverted —
`TweakRegistry.cs` is byte-identical to the WPF baseline again):

| Tab | Claimed range | Count |
|---|---|---|
| Software | 0 → 0 | **0** |
| Notifications | 0 → 16 | **16** |
| Sound | 16 → 21 | **5** |
| Update | 21 → 33 | **12** |
| Privacy | 33 → 122 | **89** |
| Gaming | 122 → 252 | **130** |
| Customize | 252 → 397 | **145** |
| Power | 397 → 433 | **36** |
| AkariOS | 433 → 433 | **0** |
| Advanced Tools | 433 → 433 | **0** |
| Tools | 433 → 433 | **0** |
| Backup & Restore | 433 → 433 | **0** |
| **Verify** | 433 → 433 | **0** |
| | **SUM** | **433 ✅** |

**It reconciles exactly**, and the ranges are **contiguous with no gaps or
overlaps** (each `start` equals the previous `end`) — which is itself strong
structural evidence the bracketing is correct.

**Note:** these runtime counts are **higher than a static `new TweakDefinition`
grep** for some tabs (e.g. Gaming 130 vs 70, Customize 145 vs 85) because helper
methods generate definitions in loops (`AddRegionalDropdown`, catalog builders).
The runtime claim count is the authoritative number.

**Zero-count tabs are correct, not a bug:** Software (card grids, not
TweakRegistry rows), AkariOS / Advanced Tools / Tools (action tabs), Backup and
Verify (they operate *on* the registry rather than contributing to it).

**Will 433 change now that Verify is migrated?** **No.** Verify claims **0** rows
— it declares no `TweakDefinition`s and makes no `AddTweakRow` calls. The number
was already 433 before Verify was added and is unchanged after. It will only
change if tweak rows are added/removed from the seven contributing tabs.

---

## Tab Batch 8 — Backup & Restore — **COMPLETE ✅**

Date: 2026-08-01. Verify NOT bundled — still its own batch.

### Build status (literal)

Clean rebuild via VS 2026 MSBuild, `-t:Rebuild -p:Configuration=Debug -p:Platform=x64`:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.88
```

Launch: **alive 14s**, 15 tabs, crash log **unchanged** (4065 bytes),
**nav-contract assertion green**.

### ✅ Nav contract tightened

`Backup` was removed from `_notYetMigrated`, so the startup assertion now
*enforces* it (a broken Backup route would fail the gate). Only `Verify` remains
allow-listed.

### ✅ Stale-style sweep — ZERO live sites (direct AND indirect)

150 compiled files. Fixed this batch: **`BackupTab.xaml.cs:231`**, the indirect
ternary `Resources[primary ? "RunBtn" : "GridBtn"]` → native `AccentButtonStyle`
for primary, default chrome + Akari tokens for secondary. Remaining direct
lookups resolve only to `AccentButtonStyle` (WinUI built-in) and `MonoFont`
(App.xaml). The only pattern-(c) matches are the documented Competitive.cs
**string parameters** feeding the native-chrome mapper — not lookups.
`VerifyTab.xaml.cs:337` deliberately untouched (Verify batch).

⚠ **Hardening note (found by the sweep, not a live bug):**
`TweakHelpers.cs:105` does
`Application.Current?.Resources[key] is CornerRadius c ? c : new CornerRadius(fallback)`.
In WinUI a **missing** resource key *throws* rather than returning null, so that
fallback is illusory — it would throw, not fall back. Both keys it is called with
(`AkariCardRadius`, `AkariControlRadius`) are defined in App.xaml, so it is safe
today. Flagged rather than changed mid-batch; a `TryLookup` would make the
fallback real.

### Byte-identical proof — logic layer untouched

`git diff --stat HEAD` over `TweakRegistry.cs`, `RestorePointHelper.cs`,
`TweakDefinition.cs` → **zero changed rows** (git sees no changes at all).

**Precision note, because I checked and it matters:** a raw `sha256` of the
working file vs the git blob *does* differ — but only because the working tree
uses **CRLF** while the git blob stores **LF**. With line endings normalized the
content hashes match exactly:

```
CONTENT-IDENTICAL  Tabs/Shared/TweakRegistry.cs
CONTENT-IDENTICAL  Tabs/Shared/RestorePointHelper.cs
CONTENT-IDENTICAL  Tabs/Shared/TweakDefinition.cs
CONTENT-IDENTICAL  Tabs/Software/Removal/BloatRemovalScriptGenerator.cs
```

That is a source-file artifact with **no effect on runtime output**. Claiming
"byte-identical" without this caveat would have been sloppy.

**Since when compiling:** `TweakRegistry.cs` and `RestorePointHelper.cs` have been
in the compile set since **Tab Batch 1** — i.e. the entire export/import
serialization has been compiling unchanged for seven batches. Only the tab UI
changed this batch.

### 📦 BACKUP FILE FORMAT COMPATIBILITY — verified, not assumed

**Answer: YES, compatible in both directions.** Evidence:

| Check | Result |
|---|---|
| `FormatName` | `"akari-tool-settings"` — identical in both builds |
| `FormatVersion` | `1` — identical in both builds |
| Writer | `JsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true })` — same code, same file, unchanged |
| Reader | `ParseSettingsFile` — validates `format`, reads `tweaks`; unchanged |
| Runtime | both builds target **net8.0** → same `System.Text.Json` writer, same formatting |
| Schema | root `format`/`version`/`exportedAt`/`machine`/`tweaks`; entries keyed by **tweak Id** with `type`/`name`/`value` (+`label` for dropdowns) — unchanged |

- **WPF-produced backup → imported by this WinUI build: fully supported.** Every
  Id the WPF build could export is registered in the WinUI build.
- **WinUI-produced backup → imported by an older WPF build: supported, with one
  benign asymmetry.** The WinUI export contains **one extra key**, `os-set-utc`,
  because that tweak moved out of the dead OSTweaks tab into Customize ▸ Desktop ▸
  Regional Settings and is now reachable (it was never exported by WPF, since
  `OSTweaksTab` was never instantiated). An older WPF build will classify it as
  **unknown** and skip it — the format has explicit `unknown` handling and reports
  the count. **No data loss, no failure.**
- **No Ids were lost.** `Verify` (still unmigrated) declares **zero**
  `TweakDefinition`s and makes zero `AddTweakRow` calls, so its absence does not
  shrink the export set. Confirmed per-tab: every tab's definition count matches
  WPF exactly, Customize +1 (`os-set-utc`).

### Tab migrated

- **BackupTab** — export card, import card, status blocks.
- **ImportReviewDialog** — was a WPF-UI **`FluentWindow`** shown modally with
  `ShowDialog()` / `DialogResult`. WinUI has **no modal secondary window**, so it
  is now a **`ContentDialog`** with `PrimaryButtonText = "Apply Selected"` /
  `CloseButtonText = "Cancel"`; the ticked Ids are harvested in
  `PrimaryButtonClick`, and `ShowAsync()` returns true only on Primary. The
  per-row checkbox list, the "current → imported" change display, the unknown-count
  copy and the default-checked behaviour are all preserved. Its own title bar and
  window sizing were dropped (ContentDialog supplies chrome); the row list is
  capped at `MaxHeight = 380` since there is no window height to fill.
- **File dialogs** → `FilePickers` (async WinUI pickers, HWND-associated):
  `SaveFileDialog` → `SaveFileAsync("Akari Tool settings", ".json", …)`,
  `OpenFileDialog` → `OpenFileAsync(".json")`. `ExportSettings`/`ImportSettings`
  became `…Async` and are awaited from the button handlers.
- **`IsVisibleChanged` → `Loaded`.** WPF refreshed the "N tweaks tracked" summary
  when the tab became visible; WinUI has no such event and the shell toggles
  `Visibility` silently, so the summary now refreshes on load.
  ⚠ **Behavioural delta:** the count no longer re-reads on every re-visit. It is
  computed once per load, so if tweaks are registered after the Backup tab first
  loads the number could be stale — VM item added.
- `Effect`/`CardShadow()` dropped; `Cursors.Hand` dropped.

---

## Fix — Nav-contract drift (2 rendering bugs) — **COMPLETE ✅**

Date: 2026-08-01. Batch plan paused; Backup/Verify untouched.

### Build status (literal)

Clean rebuild from scratch (`obj/x64`, `bin/x64` deleted first):

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:11.99
```

Launch: **alive 14s**, crash log **unchanged** (4065 bytes), **no nav-contract
violation** logged.

### Symptoms → ONE root cause

Reported: (1) Customize showed far fewer options than WPF; (2) Software's Windows
Apps / External Apps card grids were empty. Both compiled and launched clean.

**Root cause: nav-contract drift.** When the rail was rebuilt in Phase 1, its nav
tags were authored from scratch instead of reproducing the WPF shell's routing
contract (`_topTags` + `_subInfo` + `CallShowPanel`). WPF had **9 sub-panel
routes** feeding two multi-panel tabs; the WinUI rail had neither the right tag
names nor the sub-items. Nav wiring **cannot throw** when it is wrong — a bad tag
silently renders the placeholder — so build/launch gates could never catch it.

**Bug 1 — Software unreachable.** Rail used `Tag="WindowsApps"` / `"ExternalApps"`,
but the tab registers under `Bloatware` / `AppInstaller` (what `ShowPanel`
expects). Those items matched no tab → placeholder shown, `SoftwareTab` stayed
`Collapsed`. Proven by instrumenting `UniformGrid`: **zero Measure/Arrange calls**
even when forced to land on Software (collapsed elements are never measured),
while `[CARDS]` logging showed all 19 sections building correctly (56 Windows
Apps, 10 Capabilities, 7 Optional Features + 16 external sections). The grid and
cards were always fine — nothing ever displayed them.

**Bug 2 — Customize 5 of 6 sub-panels hidden.** `Build()` creates six sub-panels
then calls `ShowPanel("Taskbar")`; the rest are `Collapsed`. WPF exposed each as a
rail child item; the WinUI rail had a single "Customize" item. **58 of 85 tweak
rows (68%) were unreachable** — Explorer (5 sections), Context Menu (1),
Appearance (4), Start Menu (2), Desktop (6). Only Taskbar (3) rendered.

### Bug-class sweeps (all migrated tabs, not just the two reported)

| Sweep | Result |
|---|---|
| **A — unwired partials** (`Build*` defined vs called) | ✅ clean: all 85 `Build*` methods across 12 tabs are called (AkariOS 21, Customize 20, Power 18…). The "partial not wired" hypothesis was wrong. |
| **B — sub-panel reachability** | ❌ **8 of 9 unreachable** (all 6 Customize + Bloatware + AppInstaller; only `Debloat` had a rail item). |
| **C — async-populated lists refresh** | ✅ clean. `RefreshStatusAsync` awaits and resumes on the UI thread (no dispatcher needed); the 13 `TryEnqueue` sites are already inventoried. No async-refresh bug. Software's cards are built **synchronously**, not async-populated. |
| **D — rail tag ↔ tab key audit** (new) | ❌ dead rail items `WindowsApps`, `ExternalApps`; unreachable tabs `Bloatware`, `AppInstaller`. (`Backup`/`Verify` correctly pending.) |

**Key lesson: "all partials wired" (Sweep A, clean) is NOT "all content
reachable."** Only Sweep B/D found the defect.

### Fixes applied

1. **`_subInfo` ported VERBATIM** from the WPF shell into `MainWindow`, plus
   `_defaultSub` (`Customize → Taskbar`). Confirmed identical to WPF's map:
   all 9 entries, same keys, same (parent, panel) pairs.
2. **Rail tags corrected:** `WindowsApps` → `Bloatware`, `ExternalApps` →
   `AppInstaller` (display text unchanged).
3. **Six Customize sub-items added** to the rail via native
   `NavigationViewItem.MenuItems` (Taskbar, Explorer, Context Menu, Appearance,
   Start Menu, Desktop) — matching WPF's nesting.
4. **`Nav_SelectionChanged` rewritten** to the WPF algorithm: a sub-panel tag
   resolves to its parent tab + `ShowPanel(panel)`; a parent selected directly
   falls back to `_defaultSub`. The ad-hoc `Bloatware || AppInstaller`
   special-case is gone.
5. **Duplicate `DebloatTab` removed.** `SoftwareTab` already creates one in
   `_panelDebloat` (the WPF design); the shell's standalone `Init("Debloat", …)`
   was a second instance. Now exactly **one** `new DebloatTab()` in the codebase,
   and the `Debloat` rail tag routes to Software's sub-panel as in WPF.

### ✅ NEW PERMANENT GATE — startup nav-contract assertion (Debug)

`MainWindow.AssertNavContract()`, `[Conditional("DEBUG")]`, runs on
`RootGrid.Loaded`. Checks:
1. every rail tag resolves to a tab, or to a sub-panel of a registered tab
   (allow-listed exceptions only: `_notYetMigrated` = `Backup`, `Verify`);
2. every registered tab is reachable from the rail;
3. every sub-panel a parent can show has a rail item;
4. rail tags are unique.

**Fails loudly on four channels** — `Debug.WriteLine`, a dedicated
`%APPDATA%\AkariTool\nav-contract-violation.log`, the in-app log, a
`Debugger.Break()`, and a modal `ContentDialog` listing every problem.
Deliberately **not thrown**: `App.UnhandledException` marks exceptions handled,
which would swallow it silently.

**The gate was proven to fire, not just to exist.** Re-introducing the original
bug (`Tag="WindowsApps"`) produced, at launch:

```
NAV CONTRACT VIOLATION — rail/tab routing is broken:
 • Rail tag 'WindowsApps' resolves to NOTHING — it will render the placeholder. …
 • Sub-panel 'Software.Bloatware' has NO rail item — its content is unreachable …
```

It caught **both** facets of the real bug. Tag then restored.
`_notYetMigrated` must shrink as Backup/Verify land.

### ✅ `UniformGrid` finally exercised (was functionally unproven)

With routing fixed, instrumentation confirmed it measures **and** arranges, and
the responsive column recompute works:

```
[UG] MEASURE avail=(1000x∞) count=56 rows=7 cols=8
[UG] ARRANGE final=(1000x924) count=56 rows=7 cols=8 cell=(125x132)
[UG] MEASURE avail=(1000x∞) count=56 rows=28 cols=2      ← SizeChanged recomputed Columns
```

All instrumentation was reverted afterwards; the tree contains none of it.

### Per-tab inventory — reachable content vs WPF

`new TweakDefinition` count per tab (WPF `HEAD` vs WinUI working tree):

| Tab | WPF | WinUI | Δ | Reachable |
|---|---|---|---|---|
| Notifications | 16 | 16 | 0 | ✅ |
| Sound | 5 | 5 | 0 | ✅ |
| Update | 12 | 12 | 0 | ✅ |
| Privacy | 79 | 79 | 0 | ✅ |
| Gaming | 70 | 70 | 0 | ✅ |
| **Customize** | 84 | **85** | **+1** | ✅ (all 6 sub-panels) |
| Power | 44 | 44 | 0 | ✅ |
| AkariOS / Tools / Software / Home / About / AppUpdate / AdvancedTools | 0 | 0 | 0 | ✅ (action tabs, no TweakDefinitions) |
| OSTweaks (dead, excluded) | 33 | 33 | 0 | ❌ by design — was already unreachable in WPF (tab never instantiated) |

**No tab has less reachable content than WPF.** Customize's **+1** is exactly the
relocated `os-set-utc`. Software's 3 sub-panels and Customize's 6 are all
reachable; `Debloat` resolves to Software's sub-panel as in WPF.

---

## Fix — Global search first-keystroke hard-crash — **COMPLETE ✅**

**Symptom (VM, 2026-08-01):** typing into the rail "Find a setting" box hard-exited
the app on the first keystroke. Reported as "launches clean" because an empty box
never runs the search path — the **construction-clean ≠ exercise-clean** gap
again (same class as the nav-contract drift above).

**Observation first (per isleap's process).** Reproduced *in-process* on a Debug
build (never on the dev machine) by driving the exact keystroke path — assign
`ItemsSource`, then open the suggestion flyout so the `ItemTemplate` realizes.
Captured literal:
- Process hard-exit code **`-1073741189` = `0xC000027B` (STOWED_EXCEPTION** — a
  XAML render-pipeline crash).
- `System.Runtime.InteropServices.COMException (0x8000FFFF): Catastrophic failure`.
- Key: the exception is **asynchronous** — `ItemsSource` assignment returns
  cleanly; the throw lands a tick later during `DataTemplate` realization in the
  flyout `ListView`. `App.UnhandledException` logs it with `e.Handled = true` **but
  the process still hard-exits** — a stowed render exception is unrecoverable. That
  is why it read as a silent hard exit past the handler.

**Root cause (bisected, not assumed).** The `AutoSuggestBox` set **both**
`DisplayMemberPath="Name"` **and** an `ItemTemplate` (MainWindow.xaml) — a
forbidden WinUI combination that throws at template realization. Bisection proof:
removing **only** `DisplayMemberPath` stops the crash; the two-line `ItemTemplate`
and the `record struct` `{Binding}` render fine. isleap's three suspects
(`_subInfo` routing, null-before-ready, `ApplySearch`) were each disproven
empirically — the crash precedes any navigation.

**Fix (one line).** Deleted `DisplayMemberPath="Name"` from the `AutoSuggestBox`;
kept `TextMemberPath="Name"` (fills the box on selection) and the `ItemTemplate`
(owns the two-line rendering).

**Verification.** De-elevated copy (manifest `requireAdministrator`→`asInvoker` via
`mt.exe`) driven by UIA: forced the window wide so the NavigationView pane expands
and realizes the box, typed `defe` char-by-char (real `TextChanged=UserInput`).
Result: app **ALIVE**, `20` suggestion items, two-line template correct
(`Disable Windows Defender` / `Gaming & Performance`). Pre-fix the identical drive
hard-exited with `0xC000027B`.

**Checklist:** added a **INTERACTION SMOKE** section (do-first, non-destructive)
to the VM checklist — type in global search, open a quick-actions flyout, change a
dropdown, trigger a confirm dialog, open the import review dialog — the minimal set
that must be *exercised*, since compile/launch/nav-assertion structurally can't.

---

## Cosmetic pass — WPF DropShadowEffect glows/shadows + branding — **COMPLETE ✅**

Restores the visual accents dropped because WinUI has no `UIElement.Effect`. Scope was
held to **WPF parity only** — every value below is the WPF original (referenced from
`main`), no new styling. The full WPF effect inventory is small and bounded: the regular
tweak-row cards never had shadows, so this is the complete set.

**Mechanism.** New `Helpers/AkariShadow.cs` — a Composition `DropShadow` rendered into a
dedicated empty *host* element that overlays the target and sits BEHIND it in z-order
(`SetElementChildVisual` renders above its host's own content, so the host must be a
sibling, never the target's ancestor). The sprite is positioned under the target via
`TransformToVisual`, masked to a `Shape`'s alpha when the target is one (round glow for
the dots). Composition, not `ThemeShadow`, because the glows are **coloured** — neutral
`ThemeShadow` cannot produce crimson/green. (Named `AkariShadow` to avoid colliding with
the framework type `Microsoft.UI.Composition.CompositionShadow`.)

**Per item (all values = WPF originals):**

| Item | Element | WPF spec restored | Where |
|---|---|---|---|
| Title dot glow | 5px ellipse | crimson `#E0142A`, blur 8, opacity 0.9 | MainWindow.xaml `TitleDot` + host |
| Status dot glow | 6px ellipse | green `#3DDC84`, blur 7, opacity 0.7 | MainWindow.xaml `StatusDot` + host |
| About logo glow | 58px ellipse | **black** `#000000`, blur 22, opacity 0.5 | AboutTab HeaderCard |
| Log-panel card shadow | log Border | black, blur 12, depth 2, **0.45 dark / 0.18 light** | MainWindow `LogShadowHost` |
| Card/panel shadows | every `ShadowWrapCard` card (gaming preset + 7 section cards + 3 Home cards) | black, blur 12, depth 2, 0.45/0.18 | `TweakHelpers.ShadowWrapCard` (factory — one change, all sites) |

- **Title-bar branding (item 2):** restored the "**AKARI** TOOL" mono wordmark (AKARI in
  `AkariTextPrimary`, TOOL in `AkariAccent`) + the crimson glow dot, replacing the plain
  DisplayFont "Akari Tool" label. Font `MonoFont` 10.5 Bold (WPF value).
- **Status bar (item 3):** re-added the full-width footer row (was dropped in the shell
  migration) with a green Ready dot + "Ready" and a right-aligned build stamp. Stamp now
  reads **"WinUI 3 · .NET 8 · v1.1.6"** (was WPF "WPF-UI 4.1 · .NET 8 · v3.0"); the
  version segment is `UpdateService.CurrentVersionDisplay`, not hardcoded.
- **About tagline clip (item 4):** root cause was the header `row` being a **horizontal
  StackPanel**, which gives its text child infinite width so `TextWrapping` never engaged
  ("cleaner frame d…"). Converted `row` to a 2-col Grid (Auto logo / * text) — the star
  column bounds the width and the tagline now wraps. Confirmed on the de-elevated build.
- **Theme-awareness:** the black card/log shadows swap opacity by theme (WPF used a
  DynamicResource that did the same); the coloured glows are theme-invariant (identical in
  both App.xaml dictionaries). Retune is wired in `MainWindow.UpdateThemeVisuals` (log) and
  per-card via `ThemeService.ThemeChanged` in `ShadowWrapCard` (unsubscribed on `Unloaded`).

**Verification.** VS MSBuild clean (0 errors / 0 warnings); de-elevated + UIA launch is
clean (nav-contract assertion passed — no dialog); screenshots confirm the wordmark, glow
dot, status bar + stamp, crimson selection pill, and the wrapped About tagline.

**Accepted parity exceptions (isleap 2026-08-03 — intentional, NOT deferred; do not revisit):**
1. **Active-nav pill GLOW — accepted crimson pill, NO halo.** The pill *colour* is crimson
   (`NavigationViewSelectionIndicatorForeground = #E0142A`, both themes) — colour parity is
   met. The WPF crimson halo (blur 9) is deliberately not restored: adding it to WinUI's
   **built-in** selection indicator would require a full `NavigationViewItem` template
   override reaching into the contract-assertion-hardened nav, and the structural risk isn't
   justified for a 9px halo on a 3px bar. **Decision: leave as colour-only.**
2. **Card shadow corners rectangular, not radius-matched — accepted as-is.** A `Border`
   exposes no `GetAlphaMask`, so the Composition shadow is a soft blurred rectangle rather
   than following `CornerRadius`. Marginal at blur 12 / opacity ≤0.45; the rounded-rect-mask
   complexity isn't worth it. **Decision: leave rectangular.**

---

## Shell layout — inset "floating card" content column (Winhance-style) — **COMPLETE ✅**

Content column only. The nav rail / `NavigationView` / left column (the contract-asserted
region) were **not touched**; no factory/BaseTab/TweakRegistry/backup/search changes.

**What changed (two elements):**
- **`MainWindow.xaml`, inside `NavigationView.Content`:** the content grid went from 3 rows
  (drift / content / log) to 2 (card / log). Drift banner + `ContentHost` are now wrapped
  in a new **`ContentCard`** `Border`:
  - `CornerRadius="{ThemeResource AkariCardRadius}"` (8 — the existing token, = Winhance's
    content-card radius),
  - `Background="{ThemeResource AkariContentSurface}"` (#262626 dark / #F0F0F0 light — the
    existing content-surface token; distinct from the rail `AkariSidebarBackground` #202020
    and the `AkariFlatBackdrop` that shows in the margins),
  - `Margin="8,8,12,8"` — left 8 = the gap from the rail, top 8, right 12, bottom 8; the
    window backdrop shows on all four sides so it reads as lifted.
- **`MainWindow.xaml.cs` ctor:** `TweakHelpers.ApplyRoundedClip(ContentCard)` — a
  Composition geometric clip on the card's whole visual subtree so the ScrollViewer's
  scrolling content is clipped to the radius (a plain `Border.CornerRadius` rounds only the
  outline; the ScrollViewer composites on its own layer and would overdraw the corners).
  Re-applies on resize (the helper hooks `SizeChanged`).

**Log panel + status bar: OUTSIDE the card** (base-shell chrome), consistent with how
Winhance keeps chrome off the floating content card. The log panel sits below the card with
the card's bottom margin showing the backdrop as a gap; the status bar is unchanged
(already a full-width row below the NavigationView).

**Backdrop: flat surface suffices — Mica NOT needed.** The look comes from
`AkariContentSurface` over `AkariFlatBackdrop` in the margins; no runtime Mica/DesktopAcrylic
backdrop is required (and adding one would touch window backdrop config, out of this scope).
NB: the lift is intentionally gentle because the tokens are close in value — raising contrast
would be a **token** change (separate), not a layout one.

**Verified (de-elevated + UIA, screenshots):** rounded corners on all four sides with
backdrop margin around the card; the Composition clip is active (crop-zoom shows the card's
top corners rounding the drift banner, and the single clip geometry covers the whole subtree,
so scrolling content clips identically); nav rail visually unchanged and the **nav-contract
assertion is GREEN** (shell launched, Home→Gaming navigation worked, no assertion dialog);
margin/radius hold at wide (1280) and narrow (900) widths. Factory cards were deliberately
**not** restyled — that is the next separate pass.

### Follow-up — continuous right-side backdrop gutter (Winhance frame)

The card was inset only in the content column, so the backdrop gap was broken by the
full-width title bar (top) and log/status footer (bottom). Reshaped so the whole right side
is one uninterrupted vertical backdrop strip, title bar → footer (Winhance's inset-frame
look), **without touching the rail/NavigationView/left column**:

- **`RootGrid` gained a second column:** `[* , 12]`. Column 1 is an empty 12px strip that
  shows `AkariFlatBackdrop` unbroken down the full height. The body already had no
  `Grid.Column`, so `NavigationView` (row 1) and the status bar (row 2) now sit in column 0
  and stop at the gutter automatically; the log panel (inside the NavigationView content)
  follows the same right edge. 12 = the card's former right margin, so every right edge lines up.
- **Title bar spans both columns** (`Grid.ColumnSpan="2"`, still full width): its internal
  layout, `SetTitleBar` drag region, and the system min/max/close buttons are byte-identical
  to before. Its right edge is transparent, so the gutter's backdrop shows through at the top
  and reads continuous with the rows below.
- **`ContentCard` right margin `12 → 0`** — the gutter column now supplies the right inset,
  so the card's right edge aligns with the title bar's and footer's.

**Do the title-bar / footer insets align with the card's right edge?** Yes — card, log panel,
and status bar all stop at `window − 12`, and the title bar's transparent right edge lets the
same 12px backdrop show, so it's one aligned strip. The **only** element still reaching the
window corner is the *system caption-button cluster* (min/max/close) — those are framework-
drawn at the top-right and can't be inset 12px without switching to fully custom caption
buttons (rejected: it would risk the drag/close behaviour). Because the title bar is
transparent, the gutter backdrop still shows behind them, so the strip reads continuous.

**Protected items — explicitly tested (de-elevated + UIA):**
- **Drag:** simulated a title-bar mouse-drag → the window moved by exactly the applied delta
  (dx 120 / dy 48). Draggable. ✅
- **Caption buttons:** found via UIA at the top-right — Minimize (x≈1194), Maximize (x≈1240),
  Close (x≈1286), all correctly positioned; clicking **Minimize** minimised the window
  (rect → −32000,−32000). Functional. ✅
- **Rail:** unchanged; nav-contract assertion GREEN (launch clean, no dialog).
- Gutter continuous and aligned at wide (1280) and narrow (900). ✅

### Follow-up — drift banner was rounding instead of the card

Because the drift banner + `ContentHost` were wrapped together in `ContentCard`, the opaque
amber banner sat at the very top and so became the element the composition clip rounded —
the card read square while the banner got the rounded top corners. **Fix (one property on
`DriftBannerHost`):** `Margin="0,8,0,0"` — a top inset equal to the card radius (8) pushes the
banner just below the corner curve, so the card's own rounded surface (`AkariContentSurface`)
owns the top corners and the banner renders as a flat, square-cornered full-width strip below
the rounding. Nothing else touched (no rail/registry/factory; the banner content in
`DriftBanner.Build` is unchanged and already had square corners).

**Verified both states (de-elevated + UIA, corner crop-zooms):**
- **Banner visible:** the card's dark rounded top corners sit above a flat amber strip — the
  banner no longer rounds. ✅
- **Banner dismissed** (clicked ✕): the card's own rounded top corners show clean. ✅
- **Bottom corners:** scrolled the Gaming tab — the card's rounded bottom corners are clean
  and the scrolling content is still clipped (no bleed). ✅
- Rail unchanged; assertion GREEN.

---

## Shell — Mica system backdrop (the real Winhance mechanism) — **COMPLETE ✅**

Correction from isleap's read of Winhance's shell source: Winhance has **no** rounded inner
content card — its content Frame is plain/square. The rounded, floating look is entirely a
**Mica `SystemBackdrop`** on the window: Windows 11 DWM rounds the outer window corners and
draws the translucent Mica material in every transparent area of the content.

**What changed (backdrop only — no rail/registry/factory, no MVVM):**
- **`MainWindow.TrySetBackdrop()`** (called in the ctor after the ToolService exists so the
  outcome is logged). Mirrors Winhance's `TrySetMicaBackdrop`:
  - `MicaController.IsSupported()` → `this.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base }`,
  - else `DesktopAcrylicController.IsSupported()` → `new DesktopAcrylicBackdrop()`,
  - else keep the flat fill.
  Uses the modern `Window.SystemBackdrop` property, which wires the backdrop controller +
  activation/theme handling internally (valid unpackaged and elevated on Windows App SDK 1.x).
  The `IsSupported` gate + the logged result are what prove it initialised rather than
  silently no-opping.
- **Transparent root only when a backdrop applied:** `RootGrid` keeps `AkariFlatBackdrop` in
  XAML as the no-backdrop fallback; `TrySetBackdrop` sets `RootGrid.Background = Transparent`
  **only** if a backdrop was set, so the Mica shows through the title bar + the margin gutters
  while a machine with no backdrop support keeps the flat look (never a see-through/black
  window). The opaque rail / content / log / status surfaces stay opaque and sit on top —
  exactly Winhance's "opaque panels, Mica frame."

**Verified (de-elevated + UIA):**
- **Mica initialised, not a no-op:** the LOG panel shows `[Backdrop] Mica (Base)` →
  `MicaController.IsSupported()` returned true and the Mica backdrop was applied.
- **Outer corners:** the window renders with rounded corners (DWM), confirmed by a corner
  crop-zoom.
- **Mica material:** the title bar and the right gutter render the translucent, theme-tinted
  Mica material (distinct from the opaque content surface and from the sharp desktop outside
  the window). Mica Base in dark theme is intentionally mostly-dark with a subtle desktop tint.
- Rail unchanged; nav-contract assertion GREEN.

**Elevated configuration — NOT directly tested here, flagged for the VM:** the automated
session is non-elevated, and UIA cannot drive an elevated (`requireAdministrator`) window
across the integrity boundary, so I verified against a de-elevated (`asInvoker`) copy.
`MicaController.IsSupported()` does not depend on elevation (it gates on OS build + Windows
App SDK), and the elevated-Mica compositor bug from early WinAppSDK 1.0 was fixed in 1.2 — on
**1.8** elevated Mica works. The code path is identical. **VM check:** launch the normal
(elevated) build and confirm the LOG shows `[Backdrop] Mica (Base)` and the frame is
translucent. See the checklist item.

**Decision on the inner `ContentCard` (isleap): KEEP it rounded** — the app keeps its own
rounded floating content panel on top of the Mica frame (a deliberate departure from
Winhance's square Frame), and all four of its corners must render the radius.

### Follow-up — all four ContentCard corners now render the radius

Symptom: only the **top-left** corner of the content panel showed the curve; the other three
were square. Root cause: the clip came from `TweakHelpers.ApplyRoundedClip`, which sizes the
rounded-rect geometry from a one-shot `ActualWidth/Height` read. When the card's size changed
after that read, the geometry stayed **larger** than the card, so three corners fell in the
clip's straight region and only the origin-anchored (0,0) top-left kept its curve — the exact
"top-left rounds, other three square" signature.

**Fix (ContentCard only — not the shared factory helper):** replaced that call with a private
`MainWindow.ClipCardToRadius(ContentCard)` that drives the clip geometry's `Size` from the
card visual's own `Size` via a Composition `ExpressionAnimation` (`"host.Size"`), so it always
matches the card exactly at any width and rounds all four corners. `TweakHelpers.ApplyRoundedClip`
is untouched (still used by `ShadowWrapCard`); Mica, the rail, registry, factory and colours
are untouched.

**Verified (de-elevated + UIA, precise client-area capture + brightened corner crops):** with
the tab ScrollViewer's UIA rect giving exact card bounds, all four corners render the identical
curve — **banner visible** (card's rounded top corners above a flat amber strip) **and banner
dismissed** (clean rounded top). Rail unchanged; assertion GREEN.

### Follow-up — log console docked INSIDE the card + footer Mica + LOG toggle

The corners isleap actually meant were the **darkest** ones — the near-black log surface
(`#0E0E0E`), which was a **separate** panel below the card with square corners. Fix: the log
console now lives INSIDE `ContentCard` as its bottom row (`ContentCard` inner grid rows =
drift / content* / log), so the card's rounded corners **wrap around** the log and the dark
log surface fills flush to the card's rounded bottom corners — those (the darkest) corners now
render the radius via the same `ClipCardToRadius` clip. The log's own drop shadow +
`LogShadowHost` were removed (it no longer floats separately; the card carries the elevation).

Two requested tweaks landed with it:
- **Footer is now `Background="Transparent"`** (was the opaque `AkariSidebarBackground`) so the
  window's Mica shows through the status bar too.
- **New footer LOG show/hide button** (`LogToggleBtn` → `LogToggle_Click`): toggles the log
  console's `Visibility`; the chevron flips down/up, and the tab content reclaims the space
  when it's hidden.

**Verified (de-elevated + UIA):** log console renders docked at the card bottom with **rounded
dark bottom corners** (brightened bottom-strip crop); the footer shows the Mica backdrop; the
LOG toggle hides/shows the console (UIA confirmed `TxtLog` gone after toggle, content reflowed).
Rail unchanged; assertion GREEN.

### Follow-up — footer bar's square bottom corners = OS-wide rounded-corners disabled

isleap's status/footer bar showed **square** bottom corners; mine rendered rounded. Root cause:
the footer bar is transparent (Mica), so its bottom corners = the **window's** bottom corners,
and on installs where Windows' rounded-corners are disabled OS-wide (common on debloated
machines) the window draws square. **Fix:** opt this window into rounded corners explicitly —
`DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE=33, DWMWCP_ROUND=2)` in the
`MainWindow` ctor — so the corners follow the shell radius regardless of the global setting.
No colour/Mica/layout change; footer stays transparent.

**Proven, not assumed:** flipping the same attribute to `DWMWCP_DONOTROUND` reproduced the exact
**square** footer corner isleap sees; `DWMWCP_ROUND` renders it **curved** — so the attribute is
the control, and isleap's machine is in the globally-square state that `ROUND` overrides.

---

## Installer — unpackaged WinUI 3 runtime deployment — **COMPLETE ✅**

Reworks `build-installer.ps1` + `installer/AkariTool.iss` for the WinUI 3 output. The
app is unchanged (build/packaging only).

**Runtime strategy: FULLY SELF-CONTAINED (chosen).** This is an unpackaged app
(`WindowsPackageType=None`) targeting stock Windows 11 with no Store/MSIX dependency, so
the target may have neither the .NET Desktop Runtime nor the Windows App Runtime. The
publish therefore bundles **both**:
- `SelfContained=true` → the .NET 8 runtime ships in-app (coreclr / hostfxr / clrjit …),
- `WindowsAppSDKSelfContained=true` → the Windows App SDK runtime ships in-app
  (Microsoft.ui.xaml.dll, Microsoft.WindowsAppRuntime*.dll, CoreMessagingXP.dll …).

The installer just xcopy-deploys the whole publish folder — **no Windows App Runtime
bootstrapper, no prerequisite install on the target.** This matches the standalone /
no-Store model and is the most robust for a broad Windows 11 install base. (Framework-
dependent was rejected: it would require every target to have the matching Windows App
Runtime + .NET Desktop Runtime pre-installed — exactly the friction a standalone tool
should not impose.)

**csproj untouched.** The two self-contained switches are passed at **publish time only**
(MSBuild `/p:` flags in the script), so the csproj default stays framework-dependent and
the dev inner loop / VS F5 stays fast. Confirmed csproj state: `WindowsPackageType=None`,
`WindowsAppSDKSelfContained=false`, `SelfContained=false`, `RuntimeIdentifier=win-x64`.

**Build engine: VS MSBuild, not `dotnet`.** The script now locates VS MSBuild via
`vswhere` and runs `/t:Publish` (the standalone .NET SDK lacks the PRI/resource targets).

**Publish command (what the script runs):**
```
<vswhere-resolved MSBuild.exe> AkariTool.csproj /t:Publish /p:Configuration=Release ^
  /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=true ^
  /p:WindowsAppSDKSelfContained=true /p:PublishReadyToRun=true ^
  /p:PublishSingleFile=false /p:AppxPackage=false
```
Output: `bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\` (~260 MB, ~508 files).

**⚠ Publish gap found + fixed (build-config, not app code): the app PRI is dropped.**
The Publish target copies the *framework* PRIs (Microsoft.UI.pri, …) but **not the app's
own `AkariTool.pri`**. Empirically proven blocking: the published exe launched to
**0 windows** (process alive, blank, no exception) — WinUI could not resolve its XAML
resources. Copying `AkariTool.pri` from the build output into `publish\` made the shell
appear (windows=1, main shell present). The script now copies it in after publish and
**hard-fails** if it is absent; the `.iss` also `#error`s at compile time if the payload
lacks `AkariTool.exe` or `AkariTool.pri`, so a broken payload can never ship. (App code
was NOT touched — this is purely a publish-payload assembly fix.)

**Installer behaviour preserved (unchanged in the `.iss`):** `PrivilegesRequired=admin`
(matches the app's `requireAdministrator` manifest — verified intact in the published
exe), `DefaultDirName={autopf}\Akari Tool`, Start-menu + optional desktop shortcuts,
in-place upgrade via the fixed `AppId`, silent self-update relaunch (`/RELAUNCH=1`),
`CloseApplications=yes`, `MinVersion=10.0.19041`, x64-only. The only `.iss` change is the
`PublishDir` path (stale WPF `bin\Release\net8.0-windows\...` → WinUI
`bin\x64\Release\net8.0-windows10.0.19041.0\...`) plus the two payload guards. The
`--competitive` desktop shortcut is created by the app at runtime, not the installer, so
it needs nothing here.

**Code signing (SignPath):** deliberately NOT wired — left as a separate later step;
nothing in the script or `.iss` blocks it.

**Verification:** VS MSBuild publish clean; published exe smoke-tested on a de-elevated
copy (main shell renders once the PRI is present); `build-installer.ps1` ran end-to-end
(exit 0) and produced **`installer-output\AkariTool-Setup-v1.1.6.exe` (66.7 MB**, lzma2/max
of the 261 MB / 509-file payload). NOT installed here — isleap runs the installed setup on
the VM (see the INTERACTION SMOKE / installer checklist item).

---

## Color-token consolidation pass (inline literals → theme tokens) — **COMPLETE ✅**

Zero-visual-change tokenization of shell + factory inline color literals. WPF-UI API refs:
none (one comment only). Shell XAML was already fully tokenized; factory `.cs` had no brand
literals. Only a small set of real leftovers.

**Tokenized (all invariant → provably identical in both themes):**
- `MainWindow.xaml.cs` title-dot glow crimson `Color.FromArgb(…E0142A)` → `ThemeService.Color("AkariAccentColor")` (`#E0142A` both themes).
- `AkariFluentTheme.xaml` BadgeRecommended `#1AE0142A` / `#6BE0142A` → new `AkariAccentSoftColor` / `AkariAccentBorderColor`.
- `AkariFluentTheme.xaml` RunBtn hover `#FF2438` → existing `AkariAccentHotColor`; pressed `#B3121F` → new `AkariAccentPressedColor`.

**New tokens (defined in BOTH dark + light dictionaries with IDENTICAL values — these
accents are intentionally theme-invariant, per the existing "Recommended pill colours are
fixed crimson in both themes" design):**
- `AkariAccentSoftColor` = `#1AE0142A` (10% crimson)
- `AkariAccentBorderColor` = `#6BE0142A` (42% crimson)
- `AkariAccentPressedColor` = `#FFB3121F` (dark crimson, pressed)

**Resolved with dedicated invariant tokens (isleap chose option (a)):**
- **Status-dot glow `#3DDC84`** → new **`AkariSuccessGlowColor`** = `#3DDC84` both themes
  (fixed-brand glow, deliberately distinct from the theme-VARIANT `AkariSuccessColor`
  `#3DDC84`/`#1E9E5A`). `MainWindow.xaml.cs` now uses `ThemeService.Color("AkariSuccessGlowColor")`.
- **`#F2F2F4` ×2** (RunBtn Foreground + checkbox ✓) → new **`AkariAccentFgColor`** = `#F2F2F4`
  both, plus companion brush **`AkariAccentFg`**. This fills a real gap: an "on-accent
  foreground" for anything sitting on the crimson accent (white in both themes). Mapping to
  `AkariText` (dark `#1A1A1E` in light) would have made the text dark-on-crimson — verified
  the fix keeps "Run" white on crimson in BOTH themes.

**Full new-token list (all defined IDENTICALLY in dark + light — invariant):**
| Token | Value (both themes) | Used by |
|---|---|---|
| `AkariAccentSoftColor` | `#1AE0142A` | Recommended pill background |
| `AkariAccentBorderColor` | `#6BE0142A` | Recommended pill border |
| `AkariAccentPressedColor` | `#FFB3121F` | RunBtn pressed |
| `AkariAccentFgColor` (+ `AkariAccentFg` brush) | `#FFF2F2F4` | text/✓ on crimson (RunBtn, checkbox) |
| `AkariSuccessGlowColor` | `#FF3DDC84` | status-dot green glow |

**Result: zero inline brand-color literals in shell + factory.** Only system colors remain
(`Colors.Transparent` for the Mica pass / resolver fallbacks / a `Transparent()` helper; and
the deferred `ShadowWrapCard` `Colors.Black` → future `AkariShadowColor`). Verified pixel-
identical in dark AND light (badges crimson, RunBtn/✓ white-on-crimson, green glow), theme
toggle recolors correctly, nav-contract assertion green.

**Light-theme note (per instruction — noted, NOT changed):** the fixed crimson tints
(`AkariAccentSoft/Border/PressedColor`) are identical in both dictionaries to preserve the
current look. A *future* light-theme polish might want slightly different (e.g. more opaque
or darker-outline) crimson-pill tints for contrast on light backgrounds — deferred, zero
change now.

**Also left as-is (per audit + isleap):** `Colors.Transparent`/`Colors.Black` system colors,
incl. the `ShadowWrapCard` `Colors.Black` (future `AkariShadowColor` candidate, in the
in-flight corner/shadow area).

---

## Dispatcher inventory — `Dispatcher.Invoke` → `DispatcherQueue.TryEnqueue`

Running list of **every** UI-thread marshal in the migrated code. WPF's
`Dispatcher.Invoke` was **synchronous** (blocked until the delegate ran);
`Dispatcher.BeginInvoke` and WinUI's `DispatcherQueue.TryEnqueue` are both
**fire-and-forget**. So every `Invoke` site below became asynchronous — harmless
in all current uses (each just writes UI state with nothing sequenced after it),
but listed here in one place in case ordering ever matters.

`TryEnqueue` also returns `bool` (false if the queue is shutting down); no call
site checks it, matching the old fire-and-forget behaviour of `BeginInvoke`.

| # | Site | Was | Now | Notes |
|---|------|-----|-----|-------|
| 1 | `Services/ToolService.cs:47` | `Invoke` (sync) | TryEnqueue | log append + scroll-to-end |
| 2 | `Services/ToolService.cs:77` | `Invoke` (sync) | TryEnqueue | StartProgress |
| 3 | `Services/ToolService.cs:90` | `Invoke` (sync) | TryEnqueue | StopProgress |
| 4 | `Tabs/Home/HomeTab.xaml.cs:169` | `Invoke` (sync) | TryEnqueue | **live data** — WMI system-info banner fills in |
| 5 | `Tabs/Power/PowerTab.Persistence.cs:114` | `Invoke` (sync) | TryEnqueue | after creating the persistent Akari plan |
| 6 | `Tabs/Power/PowerTab.Persistence.cs:195` | `Invoke` (sync) | TryEnqueue | after Revert to Balanced |
| 7 | `Tabs/Power/PowerTab.PlanSelector.cs:272` | `Invoke` (sync) | TryEnqueue | after plan activation |
| 8 | `Tabs/Power/PowerTab.PlanSelector.cs:302` | `Invoke` (sync) | TryEnqueue | after Ultimate-plan activation |
| 9 | `Tabs/AdvanceTools/AdvancedToolsTab.xaml.cs:146` | `Invoke` (sync) | TryEnqueue | wizard progress/status update |
| 10 | `Tabs/AkariOS/AkariOSTab.Competitive.cs:229` | `BeginInvoke` | TryEnqueue | game-detection results → combo (already async) |
| 11 | `Tabs/AkariOS/AkariOSTab.Competitive.cs:814` | `BeginInvoke` | TryEnqueue | session state → UI (already async) |
| 12 | `Tabs/AkariOS/AkariOSTab.Competitive.cs:845` | `await BeginInvoke` | TryEnqueue | hide window during session — **the `await` was dropped** (TryEnqueue is not awaitable); nothing sequenced after it |
| 13 | `Tabs/Shared/TweakHelpers.QuickActions.cs:80` | `BeginInvoke` | TryEnqueue | hides Quick-actions button when a page has no tweaks |

Verified: **zero** `Dispatcher.Invoke` / `Dispatcher.BeginInvoke` remain in
compiled code (the only textual match is a comment in HomeTab).

---

## VM VERIFICATION CHECKLIST (isleap — run on a disposable VM)

**Standing rule from 2026-08-01:** no further live click-throughs, apply paths,
or removal runs are executed on the dev machine. Per-batch verification here is
limited to **(a) compiles clean with VS MSBuild** and **(b) the app launches
without crashing** (launch is read-only: it constructs tabs and runs `ReadState`
registry *reads* only). Everything below is for isleap's VM pass.

Take a VM snapshot before each destructive item so you can roll back.

### 🔥 INTERACTION SMOKE — do this FIRST (non-destructive, ~2 min)

**Why this exists:** twice now a bug that compile + launch + nav-assertion +
stale-style sweep all reported "clean" only appeared the instant a control was
actually *exercised* — nav-contract drift (a whole tab unreachable), then the
global-search hard-crash (`DisplayMemberPath` + `ItemTemplate` conflict throwing
an async stowed COM exception on the first keystroke, `0xC000027B`, past
`App.UnhandledException`). **Construction-clean ≠ exercise-clean.** These paths
run real event handlers, template realization, and dialog lifecycles that none of
the structural gates touch. Run this smoke set on every build before anything
else — it is the minimum that must be *interacted with*, not just looked at. None
of it is destructive (Cancel every confirm; don't Apply).

- [ ] **Global search — TYPE in it (the crash class).** Expand the pane, type
      ≥2 chars (e.g. `defe`) into "Find a setting". Confirm: the app does **not**
      crash, the **two-line** suggestion dropdown renders (tweak name over tab
      label), clicking a hit lands on the right tab with the query applied, and
      **Enter with no selection** navigates to the first hit. *(Regression guard
      for the `DisplayMemberPath`/`ItemTemplate` hard-exit — an empty box never
      runs this path, so it must be typed into.)*
- [ ] **Open a quick-actions flyout.** Any tab's Quick actions button → the
      flyout opens, renders its items, and dismisses cleanly (no crash on open or
      on light-dismiss).
- [ ] **Change one dropdown.** e.g. Sound ▸ "Sound Ducking Preference" or
      Update ▸ "Delivery Optimization" — the ComboBox opens, selection changes,
      and the value reads back (no need to keep it; revert after).
- [ ] **Trigger one confirmation dialog.** Any warned toggle (e.g. Notifications ▸
      "Disable Action Center") → the ContentDialog appears, is readable, and
      **Cancel** closes it with no state change and no crash.
- [ ] **Open the Import review dialog.** Backup & Restore ▸ Import from File →
      pick any exported `.json` → the review dialog opens and lists entries →
      **Cancel** (do not Apply). Proves the async ContentDialog + review-list path
      is alive.

If any smoke item crashes or misbehaves, stop and treat it as a release blocker —
the structural gates cannot see it.

### ⛔ P0 — Installer / runtime deployment (fresh VM, no dev tools)

- [ ] **Install on a CLEAN Windows 11 VM that has NEITHER the .NET Desktop Runtime NOR
      the Windows App Runtime installed** — this is the whole point of the self-contained
      strategy. Run `AkariTool-Setup-v1.1.6.exe`, accept the admin prompt, install to the
      default `Program Files\Akari Tool`.
      - [ ] The app **launches and renders** (shell + tabs) — proves the bundled .NET +
            Windows App SDK runtimes and the **`AkariTool.pri`** are all present. (The PRI
            was dropped by the raw publish and is copied back by `build-installer.ps1`; a
            missing PRI shows as *process alive but no window*.)
      - [ ] Start-menu shortcut and (if ticked) desktop shortcut work.
      - [ ] Elevation: the app is running **as admin** (it must, for tweaks to apply).
      - [ ] **In-place upgrade:** install again over the top → same install dir, no second
            entry in Apps & Features (fixed `AppId`).
      - [ ] **Competitive Mode desktop shortcut** (created by the app at runtime, not the
            installer) still launches with `--competitive` and the session watcher runs.
      - [ ] **Uninstall** removes the app cleanly.
      - [ ] Do NOT run `ISCC` directly — always via `build-installer.ps1` (the `.iss` now
            `#error`s if the payload is missing `AkariTool.exe`/`AkariTool.pri`).

### ⛔ P0 — Destructive / irreversible (snapshot first, test in isolation)

- [ ] **🚨 HARD RELEASE GATE — Gaming ▸ Security ▸ "Disable Windows Defender"
      full reboot round-trip.** The toggle is now **ARMED** (re-armed with the
      `--defender-phase2` handoff, both approved 2026-08-01). This is the single
      most destructive path in the app and is **UNVERIFIABLE by compile / launch /
      nav-assertion / stale-style sweep** — none of them touch the Defender
      engine. **ONLY this VM round-trip proves it.** Defender is NOT "done" — and
      the build must NOT ship — until this passes on a VM. Snapshot the VM first;
      run in isolation.

      **Disable half:**
      1. Turn **Tamper Protection OFF** (Windows Security → Virus & threat
         protection → Manage settings). Confirm the app aborts with a clear
         message if it is still ON.
      2. Toggle "Disable Windows Defender" **ON** → confirm the warning dialog,
         accept. Watch the log for DISM package removal.
      3. **Before rebooting**, confirm the handoff is registered:
         `reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" /v AkariDefenderPhase2`
         → value = `"…\AkariTool.exe" --defender-phase2`. **If this key is
         missing, phase 2 will never fire — STOP, the handoff drifted.**
      4. **Reboot.**
      5. After login, confirm **phase 2 completed**: `%APPDATA%\AkariTool\
         defender-phase2.log` shows "Headless phase-2 started" → "Native phase-2
         complete"; the RunOnce value is **gone** (self-cleaned); the app did
         **not** show a window during phase 2.
      6. Confirm Defender is **actually disabled** (Get-MpComputerStatus /
         services / Security app), and that the machine still boots and is usable.

      **Re-enable half (must also pass):**
      7. Toggle **OFF** → confirm re-enable requested; reboot if prompted.
      8. Confirm **Defender is FULLY restored** — real-time protection on, service
         running, Security app healthy, `HasState("DisableDefender")` cleared.

      **Any failure — especially the RunOnce key missing at step 3 or phase-2 log
      absent at step 5 — is a release blocker.** Registration path (verified
      identical to WPF at the code level): `DefenderService.SetAsync` →
      `DefenderPhase2Scheduler.ScheduleRunOnce()` (HKLM RunOnce, value
      `AkariDefenderPhase2`).
- [ ] **Gaming ▸ Security ▸ VBS / Memory Integrity (HVCI)** — both require a
      restart; confirm the machine still boots after toggling.
- [ ] **Gaming ▸ System Services** — service preset changes. Cross-check against
      the CLAUDE.md never-disable list (`DcomLaunch`, `RpcSs`, `RpcEptMapper`,
      `SamSs`, `ShellHWDetection`, `luafv`, `CDPSvc`/`CDPUserSvc`/`WpnService`/
      `WpnUserService` = Manual not Disabled, `DusmSvc`/`Ndu`). Confirm Store /
      NVIDIA App / Settings still work afterwards.
- [ ] **Debloat ▸ every Run button** — each immediately executes an embedded
      `.ps1` with **no confirmation** (byte-identical to WPF — deliberate).
      Especially: "Unwanted Apps — Remove", "OneDrive — Remove",
      "Microsoft Edge — Remove" (no Undo), "Disk Cleanup", "Temporary Files".
      Verify each Undo where present.
- [ ] **Update ▸ Windows Update Policy = "Paused for a long time" / "Disabled"** —
      confirm the policy applies and can be restored to Normal.
- [ ] **⛔ Software ▸ Windows Apps ▸ "Remove Selected" — the destructive removal
      flow (Batch 7, never run live).** Snapshot the VM first. Steps:
      1. **Dry-run the script first.** Tick a couple of low-risk apps, click
         Remove Selected, confirm the dialog lists exactly what you ticked (and
         shows the ⚠ instability note for any flagged item), then **Cancel** —
         verify **nothing** is removed and no keep-removed script is written.
      2. **Byte-compare the generated script against WPF.** The saved
         keep-removed script lives under `%ProgramData%\AkariTool` (see
         `AkariPaths`). Generate it from the WPF build and the WinUI build with
         the **same selection**, and diff the two files — they should be
         identical (the generators are unchanged; this proves the *selection*
         feeding them is too).
      3. Only then confirm for real: apps are removed, the startup keep-removed
         task is registered, and the removal survives a reboot.
      4. **Edge removal** and **OneDrive removal** specifically (they come from
         `WindowsAppCatalog` as `EdgeRemovalScript.GetScript()` /
         `OneDriveRemovalScript.GetScript()`) — irreversible, test each alone.
      5. **Reinstall path:** re-tick a removed app → Install Selected → verify it
         reinstalls **and** is dropped from the keep-removed script
         (`RemoveFromSavedScriptAsync`), so it doesn't get re-removed at startup.
      6. **Permanent items:** tick something with `CanBeReinstalled = false`
         alongside a normal app → the "Permanent Items" notice appears, and only
         the reinstallable one proceeds.
- [ ] **Software ▸ External Apps ▸ "Uninstall Selected"** — confirm dialog lists
      the right apps; Cancel is a no-op; uninstall works per app.
- [ ] **Software card grid layout (custom `UniformGrid`)** — WinUI has no
      UniformGrid so the panel was hand-written; verify cards lay out in even
      columns, **reflow when the window is resized** (columns recompute from
      width), stay aligned after a search filter (collapsed cards still reserve
      cells), and that nothing overlaps or clips at 125%/150% DPI and at the
      narrowest window width.
- [ ] **Software app icons** — icons still load and are crisp (the WPF
      `Freeze()`/`CacheOption.OnLoad` decode path was replaced); check the icon
      cache under `%ProgramData%\AkariTool\IconCache` still populates and that no
      file stays locked (try deleting the cache while the app runs).
- [ ] **Power ▸ power-scheme apply + persistence + drift** *(scheme drift has
      caused issues before — test this hard)*:
      - [ ] Plan cards: click Balanced / High Performance / **Ultimate
            Performance** (the last one unhides the hidden plan first) — each
            activates, the ACTIVE tag moves, and `powercfg /getactivescheme`
            agrees.
      - [ ] **First tweak change creates the persistent plan**: from a stock
            machine, change any Power setting → an "Akari Performance" scheme is
            duplicated, renamed, set active, and its GUID stored under
            `HKCU\Software\AkariTool`. The indicator switches to
            "Power plan: Akari Performance (persistent)".
      - [ ] **Drift detect + reactivate-on-next-write**: with the Akari plan
            existing, switch Windows to another plan *outside the app* (Control
            Panel) → reopen the tab → indicator must read "exists but is not
            active — the next change reactivates it" (amber), and the app must
            **NOT** reactivate merely from opening/reading. Then change any Power
            setting → it reactivates the Akari plan (`SetPowerCfg` ends with
            `/SETACTIVE`).
      - [ ] **Revert to Balanced** button: Balanced becomes active, the Akari
            scheme is deleted, the stored GUID is cleared, the indicator resets.
      - [ ] **Custom/OEM plan card**: with a vendor plan active, the dynamic 4th
            card appears with the right name and is clickable.
      - [ ] **Battery-gated rows**: on a desktop (no battery) the battery
            subgroup rows hide/disable via `GetSystemPowerStatus`
            (`BatteryFlag=128`) — confirm on both a laptop and a desktop VM.
      - [ ] Probe accuracy: no row should report "Mixed"/Custom on a
            correctly-configured machine (wrong probe-table values are a known
            failure mode).
      - [ ] Plan cards are now driven by `Tapped` (was `MouseLeftButtonUp`) —
            confirm a single click activates and there is no double-activation.
- [ ] **Customize ▸ Context Menu ▸ script rows** — Run/Undo execute embedded
      scripts (shell verb / tools registration). Verify each Undo restores.
- [ ] **Customize ▸ Explorer / Taskbar rows that restart Explorer** — confirm
      Explorer restarts exactly once per bulk apply and the desktop returns.

### ⛔ P0 — Backup & Restore ROUND-TRIP (proves ClaimRange bracketing)

**Why this is data-shaped and needs a diff, not eyeballing:** if `Mark()`/
`ClaimRange()` bracket the wrong range, the export silently captures the *wrong
tweak set* — the file looks fine, the import "succeeds", and the user ends up on
a machine state that does not match what they saved. **The registry diff is the
proof; the visible restore is only confirmation.**

Run verbatim on a VM. Take a VM snapshot first.

**STEP −1 (DO THIS FIRST) — reconcile the tracked-N total.**
The Backup tab's "**N tweaks are currently tracked**" line is the cheapest proxy
for bracketing correctness: it is `TweakRegistry.Count`, i.e. the **sum of every
tab's claimed range**. On the dev machine it reads **433**, and the per-tab
breakdown sums to exactly 433 (table above).

- [ ] Open Backup & Restore, note the number. Expect **433** (it will differ if
      your VM's hardware/OS gates some rows — e.g. battery rows hide on a
      desktop — so treat a *different* number as "explain it", not "fail").
- [ ] **Reconcile it against the per-tab counts.** Use global search (or the
      export file, Step 4) to confirm each contributing tab is represented:
      Notifications 16 · Sound 5 · Update 12 · Privacy 89 · Gaming 130 ·
      Customize 145 · Power 36 → **433**. Software, AkariOS, Advanced Tools,
      Tools, Backup and Verify contribute **0** by design.
- [ ] **If the total does not reconcile, the bracketing is wrong on a specific
      tab — identify which.** A tab whose Ids are entirely absent from the export
      has a broken `Mark()/ClaimRange()` bracket; a tab whose count is short is
      registering rows outside its bracket. Name the tab and the missing rows.
- [ ] Sanity: the number must **not** change when you merely navigate between
      tabs (registration happens once, at startup).

Only once the total reconciles is the round-trip below meaningful — a wrong total
means the export is already capturing the wrong set.

**Step 0 — pick a deliberately MIXED set (spanning tabs, both input kinds):**
- Notifications ▸ *Show notification bell icon* (toggle, HKCU)
- Sound ▸ *Sound Ducking Preference* (**dropdown**, HKCU)
- Update ▸ *Delivery Optimization* (**dropdown**, HKLM policy)
- Privacy ▸ any two toggles
- Gaming ▸ *Memory Integrity (HVCI)* (toggle, HKLM)
- Customize ▸ Desktop ▸ Regional Settings ▸ **Set Clock to UTC** (`os-set-utc` —
  the relocated tweak; include it deliberately so the relocation is proven)
- Customize ▸ Taskbar ▸ any toggle (different sub-panel, same tab)

Choosing rows from ≥6 tabs and from **two different Customize sub-panels** is
what actually exercises the per-tab `ClaimRange` bracket.

**Step 1 — apply them**, noting each tweak's name and the value you set.

**Step 2 — snapshot the affected registry** (adjust keys to your selection):
```powershell
$keys = @(
  "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
  "HKCU\Software\Microsoft\Multimedia\Audio",
  "HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
  "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard",
  "HKLM\SYSTEM\CurrentControlSet\Control\TimeZoneInformation",
  "HKCU\Control Panel\International"
)
New-Item -ItemType Directory -Force C:\akari-rt\before | Out-Null
$i=0; foreach ($k in $keys) { reg export $k "C:\akari-rt\before\$i.reg" /y | Out-Null; $i++ }
```

**Step 3 — export the backup** (Backup & Restore ▸ Export to File) to
`C:\akari-rt\backup.json`.

**Step 4 — inspect the file BEFORE restoring** (this is where bracketing bugs
show up first):
- [ ] `"format": "akari-tool-settings"`, `"version": 1`.
- [ ] **Every tweak you changed in Step 1 is present**, with the value you set —
      including `os-set-utc`.
- [ ] The **total count** is plausible for the whole app (the tab's summary says
      "N tweaks are currently tracked"); the file's `tweaks` object should have
      about that many keys. A number far below N means a bracket is truncating.
- [ ] **No duplicate Ids**, and no Id appears under a tab it doesn't belong to.
- [ ] Spot-check that Ids from **every** migrated tab appear — if an entire tab's
      Ids are missing, that tab's `ClaimRange` bracket is wrong.

**Step 5 — revert everything.** Cleanest: **roll the VM back to the snapshot**
taken before Step 1 (guarantees a true baseline). Otherwise flip every tweak back
by hand.

**Step 6 — import the backup** (Backup & Restore ▸ Import from File → review
dialog → Apply Selected, everything ticked).

**Step 7 — re-snapshot and DIFF — this is the actual test:**
```powershell
New-Item -ItemType Directory -Force C:\akari-rt\after | Out-Null
$i=0; foreach ($k in $keys) { reg export $k "C:\akari-rt\after\$i.reg" /y | Out-Null; $i++ }
foreach ($f in Get-ChildItem C:\akari-rt\before\*.reg) {
  $d = Compare-Object (Get-Content $f) (Get-Content "C:\akari-rt\after\$($f.Name)")
  if ($d) { Write-Host "DIFF in $($f.Name)" -Foreground Red; $d | Format-Table -Auto }
  else    { Write-Host "match $($f.Name)" -Foreground Green }
}
```

**Pass = an empty diff on every key.** That means the exported set was complete
and correct, and the import restored exactly the captured state.

**Any delta must be explained by name.** For each differing value, identify the
tweak Id that owns it, then:
- value present in Step 2 snapshot but **missing from backup.json** → that tweak
  was **not captured** → its tab's `Mark()/ClaimRange()` bracket is wrong (likely
  registered outside its bracket, or the bracket starts too late);
- value **in backup.json** but not restored → import/apply failure for that Id
  (check the in-app log for `[Backup] Import complete — … failed`);
- value changed that you **never touched** → an over-broad bracket claimed a row
  belonging to another tab.

**Step 8 — cross-build compatibility (do this too):**
- [ ] Export from the **WPF** build → import into the **WinUI** build → diff as
      above. Expect an empty diff.
- [ ] Export from the **WinUI** build → import into the **WPF** build → expect
      success with **exactly one** unknown entry reported (`os-set-utc`, which the
      WPF build never registered). More than one unknown ⇒ investigate.

- [ ] **Backup summary staleness** — the "N tweaks are currently tracked" line now
      refreshes on `Loaded` instead of WPF's `IsVisibleChanged`. Navigate away and
      back and confirm the number is still correct.

### 🔶 P1 — Still-open verifications carried forward

- [ ] **Confirm-then-PROCEED bulk run** — only the *Cancel* path is verified so
      far. On the VM: section bulk bar "★ Recommended" and Quick actions ▸
      "Apply all recommended" → confirm → verify the tweaks actually apply, the
      pending pill recounts, badges refresh, and the log reports
      `[QUICK] <tab>: applied N tweaks`.
- [ ] **Software tab selection → removal-script flow** — migrated in Batch 7; the
      generators are provably byte-identical (see that entry), but the **live
      removal has never been run**. Full P0 steps are in the destructive section
      below.
- [ ] **`TweakRegistry.Mark()/ClaimRange()` bracketing** — Backup is migrated
      (Batch 8); run the **round-trip procedure below**. This is the P1 that has
      been open since Batch 1.
- [ ] **Global search** — migrated and the first-keystroke hard-crash is **fixed**
      (removed the `DisplayMemberPath="Name"` that conflicted with the
      `ItemTemplate`; verified on a de-elevated copy via UIA — typing `defe`
      renders the two-line dropdown, no exit). VM pass: confirm a tweak-name search
      jumps to the right tab and highlights it (see INTERACTION SMOKE for the
      typed-input regression guard).

### ✅ P2 — Per-tab functional checks (migrated so far)

- [ ] **Shell** — rail navigation across all groups; theme toggle dark⇄light
      (cards/text/accent all recolor); title-bar drag/snap/maximize; log panel
      auto-scrolls during a long-running script; window resize at 125%/150% DPI.
- [ ] **Mica backdrop — ELEVATED confirm (was only verifiable de-elevated in dev).**
      Launch the normal (admin) build. Confirm the LOG panel shows `[Backdrop] Mica (Base)`
      (NOT "Desktop Acrylic" or "none") and the title bar + margin gutters render the
      translucent Mica material with rounded outer window corners. If it reads Acrylic/none,
      elevated Mica silently failed — a release note, not a crash.
- [ ] **About** — logo swaps with theme; version pill correct; Repository and
      .NET 8 Runtime links open; **tagline currently clips** (known, logged).
- [ ] **Notifications** (5 sections / 16 tweaks) — toggles read correct state;
      "Disable Action Center" shows its warning dialog; per-tab search filters;
      section collapse persists across restart.
- [ ] **Sound** (5 tweaks) — the "Sound Ducking Preference" dropdown applies and
      reads back; accessibility-sounds row (REG_SZ vs DWORD dual-format read).
- [ ] **Update** (11 tweaks) — both dropdowns (Update Policy, Delivery
      Optimization) apply + read back; "Custom" pill lights when the machine
      value matches no option.
- [ ] **Debloat** — see P0; also confirm group cards/rows render and the log
      streams script output live.
- [ ] **Privacy** (6 catalog groups) — toggles read + apply; warned rows prompt.
- [ ] **Gaming** — see P0; also Scheduled Tasks and System Restore sub-sections
      render and act correctly; quick-set ★/⊞ buttons apply.
- [ ] **Customize** (22 partials: Taskbar / Explorer / Desktop / ContextMenu /
      StartMenu / Appearance) — toggles read + apply per group; the ContextMenu
      script card renders with Run/Undo; watch for the known self-inflicted-drift
      pattern (`FolderContentsInfoTip`, `ExtendedUIHoverTime` written by two rows
      with opposing intent).
      **Post nav-fix:** walk **all six** rail sub-items and confirm each renders
      its sections (Taskbar 3, Explorer 5, Context Menu 1, Appearance 4,
      Start Menu 2, Desktop 6) — these were unreachable until the routing fix.
- [ ] **⚠ `os-set-utc` ("Set Clock to UTC") — NEVER YET VISIBLE.** It was
      relocated into **Customize ▸ Desktop ▸ Regional Settings**, which was one of
      the five hidden sub-panels, so the relocation has never been seen or
      exercised. Verify: the row **renders** in Desktop ▸ Regional Settings; its
      state **reads back** correctly (ON when
      `HKLM\SYSTEM\CurrentControlSet\Control\TimeZoneInformation\RealTimeIsUniversal`
      = 1, OFF when absent); toggling ON **writes** the value and OFF **deletes**
      it; the log says "UTC clock … Restart to apply."; and its Id is still
      `os-set-utc` in a settings export (so old configs still match).
- [ ] **Nav routing (post-fix regression check)** — every rail item lands on real
      content; the "migrated in a later phase" placeholder should now be
      **unreachable** (all tabs migrated, allow-list empty). Specifically: Windows
      Apps + External Apps show the Software card grids; Debloat shows Software's
      Debloat sub-panel (**not** a second copy of the tab); selecting the
      "Customize" parent lands on Taskbar.
- [ ] **Content width (post-fix)** — every tab's content fills the pane with
      balanced ~32px margins, at both a maximised window and a narrow one. The
      three intentionally centred tabs (**About** 860, **App Updates** 720,
      **Windows Updates** 860) should still be centred at their own width — that
      is WPF behaviour, not a regression.
- [ ] **Verify tab** — drift scan lists reverted tweaks with
      "recorded → current · set <date> on build <n>"; "changed across an OS update"
      is highlighted; **Re-apply all** prompts and only then re-applies; "Stop
      tracking" removes a row.
      ⚠ **Behavioural delta:** the scan now runs on **Loaded**, not on every
      re-visit (WPF's `IsVisibleChanged` has no WinUI equivalent) — confirm the
      **Re-scan** button refreshes it, and that stale results after changing
      tweaks elsewhere are acceptable.
      ⚠ The **drift banner** in the shell is still not ported —
      `RefreshDriftBanner` is a documented no-op, so expect **no banner** above
      the content even when drift exists. Tracked in the deferred list.
- [ ] **Power** — see the P0 power-scheme block; also every subgroup section
      (Display, Hard disk, Sleep, USB, PCIe, GPU, Processor, Multimedia, Power
      buttons) reads back correctly after apply.
- [ ] **Home** — landing tab. System-info banner fills in from "Detecting…"
      (edition, version, CPU, GPU, memory) within ~1s; the 14 quick-access cards
      show the right **PNG nav icons** (now loaded via `ms-appx`, and the AkariOS
      card must swap to its light variant in light theme); clicking a card
      navigates to that tab; hovering a card lifts it; the **search box** filters
      across every migrated tab, results carry the correct tab badge, and
      clicking a result navigates. Long GPU names show a tooltip.
- [ ] **Tools** — "Copy to Clipboard" actually puts system info on the clipboard
      (rewritten on the WinRT clipboard API); the 10 shortcut buttons launch the
      right MMC/settings targets (rebuilt grid — check nothing is misaligned or
      missing); DNS buttons (Cloudflare/Google/Quad9/Auto) apply; repair and
      maintenance actions run.
- [ ] **App Updates** — "Check for Updates" queries GitHub and the state chip
      icon/colour changes; **the spinner actually rotates** while checking (the
      storyboard was rebuilt for WinUI and needs `EnableDependentAnimation`);
      the changelog card populates from live release notes.
      ⚠ **"Update Now" downloads and runs the real installer and exits the app** —
      test only on a VM you're happy to upgrade in place.
      Confirm **no update check fires at startup** (that trigger is deliberately
      not wired yet).
- [ ] **Cosmetic regressions from dropped WPF constructs** (expected, confirm
      acceptable): no hand-cursor on Power plan cards / revert button / Home
      cards / search rows; slightly different header letter-spacing on the Power
      "POWER PLAN" label (kerning property dropped); Run/Undo/grid buttons now
      use native WinUI chrome; DNS buttons no longer wrap to a second row.
- [ ] **AkariOS** — Service preset Apply Gaming / Daily / Restore Stock (⚠ mass
      service changes — snapshot first, then confirm Store/NVIDIA App/Settings
      still work); Playbook tweaks apply + undo; BCD tweaks (⚠ **boot config** —
      confirm the machine still boots); GPU tools; PostInstall; **Shader cache**
      scan + clean (targets now stack vertically — confirm the layout reads OK);
      **Competitive Mode** — the disclaimer dialog appears on first use, Browse…
      opens the **new WinUI file picker**, Create Shortcut works, a session
      starts/hides the window/ends and **restores the window** (rewritten on
      AppWindow), and the elapsed timer ticks.
- [ ] **⚠ Dropped `await` — Competitive Mode window-hide
      (`AkariOSTab.Competitive.cs:845`)** — the WPF code was
      `await Dispatcher.BeginInvoke(() => w.Hide())`; WinUI's `TryEnqueue` is not
      awaitable, so the `await` was **dropped**. Static reading says nothing is
      sequenced after it, but dropped awaits only bite when something downstream
      assumed completion — so verify on the VM, don't take the read on faith:
      start a Competitive session and confirm the window hides at the right
      moment (not early/late/never), the session still starts correctly, ending
      the session **restores** the window, and no race appears when starting and
      immediately ending a session, or when the game exits while the window is
      still hiding. Repeat with "Close after launch" both on and off.
- [ ] **Advanced Tools** — the ISO wizard end-to-end: **Select ISO / Select
      Folder / driver folder / output location / XML file all use the new WinUI
      pickers** (⚠ highest-risk untested area this batch — unpackaged pickers need
      the HWND association; if one throws or opens behind the window, that's the
      cause); step cards enable/disable correctly (now driven by
      `IsHitTestVisible`, not `IsEnabled`); Generate autounattend.xml writes a
      valid file; Cancel aborts a running step.
- [ ] **Stale style-key sweep** — ✅ now a per-batch hard gate, **zero live sites**
      across all 115 compiled files as of Batch 6. Remaining unported-style
      references live only in the **excluded** `Tabs/OSTweaks/**` (8 sites, not
      compiled). Re-run the sweep when the Software batch lands (2 known sites).

### 🚀 Startup orchestration (new — items 1–4)

- [ ] **Splash** — appears centred, borderless, above other windows; logo matches
      the active theme; the status dot pulses; the bottom block fades/slides up;
      the 7 pips light in order with the % counter reaching 100%; the splash fades
      out (~250 ms) and the main window appears. No flash of an unthemed splash.
- [ ] **Theme ordering** — set the app to Light, restart: the **splash itself**
      must render in Light (proves the theme is applied before it paints).
- [ ] **Cold-start feel** — no blank taskbar icon before the splash paints; total
      startup not noticeably slower than the WPF build.
- [ ] **`--competitive` shortcut** — create a Competitive shortcut from AkariOS,
      launch it: the main window must **stay hidden**, the process must keep
      running (Task Manager), the session must start, and ending it must restore
      settings. ⚠ WPF needed `ShutdownMode` juggling here; WinUI relies on a
      never-Activated window keeping the app alive — **verify the app does not
      exit immediately** on this path.
- [ ] **Orphaned-session recovery** — kill the app mid-session (Task Manager),
      relaunch: the "not closed properly" prompt appears; **Restore** ends the
      session and restores settings; **Ignore** discards the record and the prompt
      does **not** reappear next launch. Test on BOTH the normal launch path and
      the `--competitive` path (on the latter the window stays hidden — confirm
      the dialog is still visible and usable, since it no longer force-shows the
      window as WPF did).
- [ ] **Startup update check** — with an older version installed, launch and
      confirm the "Update available" prompt appears once; "Update now" navigates
      to **App Updates**; "Later" dismisses. With the latest version, or with the
      network off, **nothing** must appear (silent on up-to-date and on error).

### 🔎 Global search + drift banner (shell cluster)

- [ ] **Global "Find a setting"** (rail pane header, above the nav items) — type
      ≥2 chars: a dropdown of matching tweaks (name + tab) appears; picking one
      navigates to that tab AND the tab's own search box is pre-filled with the
      term and filtered to it. Test a hit that lives in a **Customize sub-panel**
      (e.g. an Explorer or Desktop tweak) — it must land on the right sub-panel,
      not just the Customize parent. Enter with no selection jumps to the first hit.
      `< 2` chars shows nothing.
- [ ] **Drift banner** — cause drift (apply a tweak, then change it back in
      Windows Settings, or use a VM where an OS update reverted something), then
      relaunch: a warning banner appears **above the content**, "N tweaks no
      longer match what Akari set" with the reverted/changed breakdown; **Review**
      navigates to Verify; **✕** dismisses it until next launch. Also confirm the
      Verify tab's **Re-scan** updates/clears the banner live
      (`RefreshDriftBanner`). With no drift, **no banner** appears.

### 🔁 Regression checks after any batch

- [ ] Elevation: app still auto-elevates via UAC on launch.
- [ ] No unhandled-exception dialogs; check
      `%APPDATA%\AkariTool\AkariTool_crash_<date>.log` is empty after a session.
- [ ] Explorer restart coalescing: a bulk apply containing `RequiresRestart`
      rows restarts Explorer exactly **once**, at the end.

---

## Deferred visual/functional restore (NOT forgotten — restore before release)

Functional/visual gaps vs the old WPF-UI build, logged per isleap so they
survive to the end of the migration. **Do not fix mid-batch** unless noted.

1. **Global "Find a setting" search** — the old build pinned a global search box
   in the rail pane header (under the hamburger) that searched tweaks across ALL
   tabs via `TweakRegistry.Search` and navigated to the hit. The per-tab
   `AutoSuggestBox` in BaseTab is NOT a replacement. Restore in the rail's
   `NavigationView.PaneHeader`/`PaneCustomContent` once tabs are migrated.
2. **Title-bar branding** — ✅ DONE (cosmetic pass). "AKARI TOOL" mono wordmark +
   crimson glow dot restored.
3. **Status bar "Ready" state** — ✅ DONE (cosmetic pass). Green dot + "Ready" +
   build stamp "WinUI 3 · .NET 8 · v1.1.6".
4. **About tab tagline clipped** ("cleaner frame d…") — ✅ DONE (cosmetic pass).
   Root cause was exactly the hypothesis: horizontal StackPanel → unbounded text
   width. Converted to a 2-col Grid so TextWrapping engages.
5. **Drift banner** — the WPF shell rendered a banner above the content when
   `DriftScanner` found reverted tweaks (`MainWindow.RefreshDriftBanner`, and
   `Tabs/Shared/DriftBanner.cs`, which is still uncompiled). The WinUI shell has a
   **documented no-op `RefreshDriftBanner`** so the Verify tab's existing call
   site stays wired; landing the banner is a one-place change there.
6. **Cosmetic pass (single, AFTER all tabs migrate; confirmed by isleap):** ✅ DONE.
   Card shadows + glows restored via Composition (`Helpers/AkariShadow.cs`,
   `TweakHelpers.ShadowWrapCard`). See the "Cosmetic pass" section above for the full
   per-item table and the two flagged 1:1 gaps (nav-pill glow held as colour-only;
   rectangular card-shadow corners). NB: the "star/win-blue quick-set icons" and
   "accent underlines" mentioned here were **icon/foreground colouring, not
   DropShadowEffects** — they migrated with their brushes and were never part of the
   Effect inventory, so nothing to restore there.

---

