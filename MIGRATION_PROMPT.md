# Akari Tool — WPF-UI → WinUI 3 Migration (Agent Brief)

You are migrating **Akari Tool**, a large Windows 11 optimization utility, from **WPF + WPF-UI 4.1.0 (.NET 8)** to **WinUI 3 (Windows App SDK, .NET 8)**. This is a real, working ~41,000-line app across ~191 C# files and 25 XAML files. Treat this as a careful, phased port — **NOT** a one-shot rewrite.

Read this entire brief before writing any code.

---

## 0. NON-NEGOTIABLE CONSTRAINTS

1. **Target: unpackaged WinUI 3**, Windows App SDK (latest stable 1.x), `net8.0-windows10.0.19041.0`, `win-x64`, self-contained = false, `WindowsPackageType=None`. **Do NOT produce an MSIX/packaged app** — this app requires `requireAdministrator` elevation and does TrustedInstaller impersonation, which the packaged sandbox breaks.
2. **Keep the manifest `requireAdministrator`.** Carry over `app.manifest`'s `requestedExecutionLevel level="requireAdministrator"`. Verify the unpackaged WinUI 3 app still auto-elevates on launch.
3. **Root namespace stays `AkariTool`.** Assembly name stays `AkariTool`. Do not rename anything for cosmetic reasons.
4. **DO NOT TOUCH BUSINESS LOGIC.** Everything under `Services/` and every non-UI helper (registry access, P/Invoke, service ACLs, `ElevationService.cs`, tweak catalogs, `SystemStateReader.*`, `ServicesPreset.*`, `TweakRegistry`, `TweakDefinition`, all `*Catalog*.cs`, all removal-script generators) is UI-framework-agnostic and **must be copied over byte-for-byte** except for the mechanical namespace/type changes in Section 3. If a file has no UI construction in it, it should compile unchanged. Flag any file where you believe logic must change — do not change it silently.
5. **Preserve the public contract of `BaseTab` and `TweakHelpers`.** Every tab derives from `BaseTab : UserControl` and builds itself through factory methods (`AddItem`, `PageHeader`, `AddSectionTitle`, etc.). Reimplement the *internals* of these against WinUI controls, but keep the method **signatures identical** so the ~13 tabs don't need per-call rewrites. This factory is the single highest-leverage file in the codebase — get it right and most tabs port for free.
6. **Build after every phase.** `dotnet build -c Debug`. A phase is not "done" until it compiles with zero errors. Never proceed to the next phase on a broken build.
7. **No invented APIs.** Use only real, documented WinUI 3 / Windows App SDK APIs. If you are unsure whether an API exists, say so in the migration log and leave a `// TODO(migration):` marker rather than guessing. Hallucinated APIs are worse than gaps.

---

## 1. GOLDEN RULE — SEPARATE THE TWO LAYERS

The codebase has two layers. Your job touches only one.

- **Framework layer (MIGRATE):** anything referencing `System.Windows.*`, XAML files, control construction (`new StackPanel()`, `new Border()`, `new TextBlock()`…), `Dispatcher`, resource dictionaries, `pack://` URIs, triggers, storyboards, effects.
- **Logic layer (PRESERVE):** registry, services, P/Invoke into advapi32/kernel32/ntdll, catalogs, elevation, update/download logic, process control. These use `Microsoft.Win32`, `System.ServiceProcess`, `System.Diagnostics`, `System.Management` — all of which work identically under WinUI 3.

When a file mixes both (e.g. `TweakHelpers.cs` opens registry keys **and** builds controls), migrate only the control-building parts and leave the registry/P-Invoke parts alone.

---

## 2. THE HARD LANDMINES (WPF → WinUI 3)

These are the constructs that do **not** exist in WinUI 3 and must be re-architected, not translated. The shell (`MainWindow.xaml`, `App.xaml`) is saturated with them.

| WPF / WPF-UI construct | WinUI 3 replacement |
|---|---|
| `Style.Triggers`, `ControlTemplate.Triggers`, `DataTrigger`, `Trigger` | **VisualStateManager** + `VisualState` / `AdaptiveTrigger`, or code-behind property changes. There is no trigger system. |
| `Storyboard`/`DoubleAnimation` inside triggers | `Storyboard` exists but is driven via VisualState or started from code; retarget accordingly. |
| WPF-UI `FluentWindow` | WinUI `Microsoft.UI.Xaml.Window` + `AppWindow`. Note: WinUI `Window` is **not** a `FrameworkElement`. |
| WPF-UI `ui:TitleBar` + `ExtendsContentIntoTitleBar` | `AppWindow.TitleBar` / `Window.ExtendsContentIntoTitleBar` + `SetTitleBar(element)`. |
| WPF-UI `ui:NavigationView` (heavily retemplated here) | WinUI native `Microsoft.UI.Xaml.Controls.NavigationView`. **Do not port the custom `PART_`-based retemplate.** Rebuild the rail with the native control + `NavigationViewItem` / `NavigationViewItemHeader` and restyle via lightweight resource overrides + VisualStates. |
| `DropShadowEffect`, `Effect=` | No `Effect` property. Use `Microsoft.UI.Composition` `DropShadow` or `ThemeShadow`, or drop the glow where it isn't load-bearing. Log each one dropped/deferred. |
| `pack://application:,,,/…` URIs | `ms-appx:///…`. Fonts/images become `Content` with `ms-appx` paths. |
| `RenderOptions.BitmapScalingMode`, `SnapsToDevicePixels`, `UseLayoutRounding`, `TextOptions.*` | Remove — mostly no equivalent; WinUI handles DPI differently. |
| `Dispatcher.Invoke` / `Dispatcher.BeginInvoke` | `DispatcherQueue.TryEnqueue(...)`. Get it via `this.DispatcherQueue` or `DispatcherQueue.GetForCurrentThread()`. |
| `Wpf.Ui.Controls.MessageBox` | `ContentDialog` (needs `XamlRoot` set). |
| `ui:SymbolIcon` / `SymbolRegular` | `FontIcon` (Segoe Fluent Icons) or WinUI `SymbolIcon`. |
| WPF-UI `ToggleSwitch` | WinUI native `ToggleSwitch`. |
| WPF-UI `ProgressRing` | WinUI native `ProgressRing`. |
| `ui:ThemesDictionary` / `ui:ControlsDictionary` | Delete. WinUI ships its own theme resources; use `XamlControlsResources` in `App.xaml` and your own `ResourceDictionary` for Akari tokens. |
| `Popup` (global search results) | WinUI `Popup` exists but differs; a `Flyout` or `TeachingTip` is usually the better port. |
| `RelativeSource={RelativeSource Self}` on attached-property paths (`local:Nav.IsCompact`) | Reimplement compact/expanded as VisualStates on the NavigationView, not attached-property DataTriggers. |
| `FocusVisualStyle="{x:Null}"` | Remove; use `IsTabStop`/focus-visual resources if needed. |
| `x:Type`, `BasedOn` with `{x:Null}` | `x:Type` unnecessary in WinUI (use `TargetType="Button"` directly). |
| Classic `Binding` with complex paths | Prefer `{x:Bind}`; classic `Binding` is more limited in WinUI. |

**Theme tokens:** the app's crimson accent is `#E0142A`. All Akari color tokens live in `Themes/Akari.Dark.xaml` / `Akari.Light.xaml` and `AkariFluentTheme.xaml`. Port these to WinUI `ResourceDictionary` files using `ThemeDictionaries` (`Default`/`Light`) for theme-awareness. Keep the token **key names identical** so `{DynamicResource AkariXxx}` → `{ThemeResource AkariXxx}` is a mechanical swap across all files.

---

## 3. MECHANICAL NAMESPACE / TYPE MAP (apply codebase-wide)

- `using System.Windows;` → `using Microsoft.UI.Xaml;`
- `using System.Windows.Controls;` → `using Microsoft.UI.Xaml.Controls;`
- `using System.Windows.Media;` → `using Microsoft.UI.Xaml.Media;` (+ `Windows.UI` for `Color`, `Microsoft.UI` for `Colors`)
- `using System.Windows.Media.Effects;` → remove (see shadow note)
- `System.Windows.Thickness` → `Microsoft.UI.Xaml.Thickness` (usually just the `using` swap)
- `Visibility.Collapsed/Visible` → same names, `Microsoft.UI.Xaml.Visibility`
- `UserControl`, `Grid`, `StackPanel`, `Border`, `TextBlock`, `Button`, `ComboBox`, `TextBox` → same names, new namespace, **verify property deltas** (e.g. `TextBlock.TextTrimming`, `Border.Effect` gone, `Button` default style differs).
- `Color.FromRgb/FromArgb` → `Windows.UI.Color` via `Microsoft.UI.ColorHelper` or `Color{ A,R,G,B }`.
- `SolidColorBrush` → `Microsoft.UI.Xaml.Media.SolidColorBrush`.
- `DispatcherTimer` → `Microsoft.UI.Xaml.DispatcherTimer` (exists) or `DispatcherQueueTimer`.

Do this as a careful pass per file, **not** a blind find-replace across the repo — several types collide in name but differ in members.

---

## 4. PHASED PLAN (STOP AND REPORT AT EACH GATE)

Work strictly in this order. **After each phase, STOP, ensure it builds, append to `MIGRATION_LOG.md`, and report back. Do not start the next phase.**

**Phase 0 — Scaffold & harness**
- Create the new WinUI 3 unpackaged project (`AkariTool.csproj`) targeting Windows App SDK, `net8.0-windows10.0.19041.0`, `win-x64`, `WindowsPackageType=None`, `EnableMsixTooling=false`, keep `requireAdministrator` manifest, `RootNamespace`/`AssemblyName` = `AkariTool`.
- Wire `App.xaml`/`App.xaml.cs` (WinUI lifecycle: `OnLaunched`, create `Window`), a blank `MainWindow`, and confirm `dotnet build` + launch as admin works. Bring across fonts/icons/resources as `Content` with `ms-appx` paths.
- Copy **all** of `Services/` and all logic-only files into the project **unchanged** and confirm they compile (they should, once namespaces resolve). Do not migrate any tabs yet.
- **Gate:** empty app launches elevated, all `Services/` compile. STOP + report.

**Phase 1 — Shell + theme tokens + factory + ONE pilot tab**
- Port theme token dictionaries (`Akari.Dark`/`Akari.Light`/`AkariFluentTheme`) to WinUI `ThemeDictionaries`, keeping key names.
- Rebuild `MainWindow` shell: title bar (`AppWindow.TitleBar`), native `NavigationView` rail with the group structure (SOFTWARE / OPTIMIZE / ADVANCED), footer items, status bar, log panel, theme toggle. Rebuild compact/expanded as VisualStates. Wire `NavItem_Click` tab-switching (keep the Visibility-toggled UserControl stack pattern).
- Reimplement `BaseTab` + `TweakHelpers` factory internals against WinUI controls, **preserving all public method signatures**.
- Migrate exactly **one** simple tab end-to-end as the pilot (suggest `AboutTab` or `HomeTab`) to prove the factory + shell + navigation path.
- **Gate:** app launches, rail navigates, pilot tab renders correctly, theme toggle works, `ContentDialog`/log panel functional. STOP + report with screenshots-worthy notes.

**Phases 2–N — Tabs in batches**
- After the pilot is signed off, migrate remaining tabs in small batches (2–3 tabs per phase), simplest → most complex. Rough order: Notifications/Sound/Update → Debloat/Software → Privacy/Gaming/OSTweaks/Customize/Power → AkariOS/AdvancedTools/Backup/Verify.
- Each batch: build, self-check against the mapping table, STOP + report.
- **Do not** attempt more than one batch per run.

**Final phase — Installer & polish**
- Update `build-installer.ps1` / Inno Setup for the WinAppSDK runtime dependency (framework-dependent unpackaged deployment needs the bootstrapper or the runtime present). Verify update-check flow, drift banner, global search.

---

## 5. MIGRATION LOG (mandatory)

Maintain `MIGRATION_LOG.md` at the repo root. After every phase append:
- Files created / migrated / left unchanged.
- Every WPF construct that had **no clean WinUI equivalent** and how you handled it (replaced / deferred / dropped), with a `// TODO(migration):` in-code marker for anything deferred.
- Any place you were forced to change **logic** (should be near-zero — flag loudly).
- Anything you were unsure about / could not verify.
- Exact build status (`dotnet build` output summary).

---

## 6. THIS RUN — DO ONLY PHASE 0 AND PHASE 1

For this run: complete **Phase 0 and Phase 1 only**, then STOP and report. Do not migrate tabs beyond the single pilot. Do not touch the installer. End your run with:
1. `dotnet build` result.
2. The `MIGRATION_LOG.md` contents for phases 0–1.
3. A short list of anything ambiguous you need a decision on before Phase 2.

Begin with Phase 0.
