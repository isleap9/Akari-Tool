# Winhance — Technology Stack

**Analysis Date:** 2026-10-05
**Reference repo:** `C:/Users/isleap/Documents/GitHub/Winhance` (read-only reference material)

---

## Languages

**Primary:**
- C# — .NET 10, `LangVersion=preview` in the UI project (required for CommunityToolkit.Mvvm 8.4.0 partial-property source generators)
- XAML — WinUI 3 markup (compiled by the WindowsAppSDK XAML compiler)

**Secondary:**
- PowerShell — generated at runtime (`C:\ProgramData\Winhance\Scripts\*.ps1`), plus repo build scripts in `extras/`
- XML — `autounattend.xml` generation (`src/Winhance.UI/Features/Common/Resources/AdvancedTools/autounattend-template.xml`)
- Inno Setup Pascal Script — installer (`extras/Winhance.Installer.iss`)

## Runtime

**Environment:**
- .NET 10 (`net10.0-windows10.0.19041.0`) across **all** projects (Core, Infrastructure, UI, Interop, and all 4 test projects)
- `TargetPlatformMinVersion` = `10.0.17763.0` (UI) / `10.0.19041.0` (Interop)
- `WindowsSdkPackageVersion` = `10.0.26100.48` (UI + UI.Tests)
- `Nullable=enable`, `ImplicitUsings=enable` everywhere
- **Self-contained**: `SelfContained=true` **and** `WindowsAppSDKSelfContained=true` — bundles both the .NET runtime and the App SDK runtime
- `RuntimeIdentifiers` = `win-x64` only; `Platforms=x64` only (ARM64 runs via x64 emulation)
- `CETCompat=false` — deliberate workaround for dotnet/runtime#110920 (shadow-stack FailFast before `Main()` on CET CPUs)
- `EnableMsixTooling=false`, `GenerateAppxPackageOnBuild=false`, `AppxPackage=false`, `WindowsPackageType=None` → **unpackaged** deployment (no MSIX)
- Custom entry point: `DISABLE_XAML_GENERATED_MAIN` + `StartupObject=Winhance.UI.Program`

**Elevation:**
- `src/Winhance.UI/app.manifest` → `requestedExecutionLevel level="requireAdministrator"`. The app **always** runs elevated.
- DPI: `PerMonitorV2` + `longPathAware=true`; supportedOS declares Windows 10 + Windows 11.
- Icon: `Assets\AppIcons\winhance-rocket.ico`

**Package Manager:**
- NuGet (via MSBuild / `dotnet restore`). **No** central package management (`Directory.Packages.props`), **no** `nuget.config`.
- Lockfile: none committed.

## SDK Pin

`global.json`:
```json
{
  "sdk": {
    "version": "10.0.102",
    "rollForward": "latestPatch"
  }
}
```

## Frameworks

**Core (UI):**
- **Windows App SDK metapackage** `Microsoft.WindowsAppSDK` **1.8.260416003** — the metapackage is mandatory (transitive dependency of Material.Icons.WinUI3 / CommunityToolkit.WinUI / FluentIcons / Xaml.Behaviors). WinUI version is whatever 1.8.x ships; do not switch to the `.WinUI` component package (duplicate build props break the build — MSB4011 + MSIX `CustomBeforeMicrosoftCommonTargets` error). The metapackage's ~38 MB of Windows ML DLLs (onnxruntime.dll, DirectML.dll) are stripped at installer time.
- `Microsoft.Windows.SDK.BuildTools` 10.0.26100.4654
- `Microsoft.Windows.CsWinRT` 2.2.0 — WinRT projections (needed by CommunityToolkit.WinUI 8.2+)

**MVVM:**
- `CommunityToolkit.Mvvm` **8.4.0** (UI), **8.2.2** (Core, Infrastructure) — source generators (`[ObservableProperty]` with `partial` properties, `[RelayCommand]`)

**Icons:**
- `Material.Icons.WinUI3` 3.0.2
- `FluentIcons.WinUI` 2.1.326 (4000+ icons)

**WinUI controls (CommunityToolkit.WinUI 8.2.x, version-locked):**
- `CommunityToolkit.WinUI.Controls.SettingsControls` 8.2.251219 (`SettingsCard`, the primary card primitive)
- `CommunityToolkit.WinUI.Controls.Primitives` 8.2.251219 (`WrapPanel` for badge rows)
- `CommunityToolkit.WinUI.Behaviors` 8.2.251219
- `CommunityToolkit.WinUI.Collections` 8.2.251219
- `CommunityToolkit.WinUI.Triggers` 8.2.251219
- `CommunityToolkit.WinUI.Extensions` 8.2.251219
- `CommunityToolkit.WinUI.UI.Controls.DataGrid` **7.1.2** — last release; the 8.x reorg never ported it, so 7.x sits alongside 8.x packages (it has no toolkit dependency). Future successor: `WinUI.TableView`.

**XAML Behaviors:**
- `Microsoft.Xaml.Behaviors.WinUI.Managed` 3.0.1

**Interop:**
- `Microsoft.Windows.CsWin32` 0.3.183 (UI, `PrivateAssets=all`) — source-generated P/Invoke. Request list in `src/Winhance.UI/NativeMethods.txt`: `SetForegroundWindow`, `ShowWindow`, `IsIconic`, `FindWindow`, `GetWindowThreadProcessId`, `AllowSetForegroundWindow`, and the `IFileDialog`/`IFileOpenDialog`/`IFileSaveDialog`/`IShellItem`/`IShellItemArray`/`SHCreateItemFromParsingName` COM set (WinRT file pickers fail under admin elevation).

**DI:**
- `Microsoft.Extensions.DependencyInjection` 10.0.7 (UI, Infrastructure, IntegrationTests)
- `Microsoft.Extensions.Hosting` 10.0.7 (UI, Core)

**Security pin:**
- `System.Security.Cryptography.Xml` 10.0.7 in UI, Core, and Infrastructure — floors out the vulnerable transitive (NU1903 / GHSA-37gx-xxp4-5rgx CVE-2026-33116, GHSA-w3x6-4m5h-cxqf CVE-2026-26171).

**Testing:**
- `Microsoft.NET.Test.Sdk` 17.12.0
- `xunit` 2.9.3 + `xunit.runner.visualstudio` 2.8.2
- `Moq` 4.20.72
- `FluentAssertions` 7.0.0
- `coverlet.collector` 6.0.2

**WinGet interop (`WindowsPackageManager.Interop`):**
- `Microsoft.Windows.CsWinRT` 2.0.4
- `Microsoft.Windows.CsWin32` 0.3.49-beta
- `Microsoft.WindowsPackageManager.ComInterop` 1.9.25180 — `<IncludeAssets>none</IncludeAssets>`, `NoWarn=NU1701`, `GeneratePathProperty=true`. Only the `.winmd` + `winrtact.dll` are used; a custom MSBuild target `CopyWinmdToTargetDir` runs `BeforeBuild` to copy them into `$(TargetDir)`, then CsWinRT (`CsWinRTIncludes=Microsoft.Management.Deployment`, `CsWinRTWindowsMetadata=10.0.19041.0`) generates the projected `Microsoft.Management.Deployment` classes. `AllowUnsafeBlocks=true`, `NoWarn=CS0618;CS9191`.

## Complete PackageReference Inventory

| Project | Package | Version |
|---|---|---|
| **Winhance.UI** | Microsoft.WindowsAppSDK | 1.8.260416003 |
| | Microsoft.Windows.SDK.BuildTools | 10.0.26100.4654 |
| | Microsoft.Windows.CsWinRT | 2.2.0 |
| | CommunityToolkit.Mvvm | 8.4.0 |
| | Material.Icons.WinUI3 | 3.0.2 |
| | FluentIcons.WinUI | 2.1.326 |
| | CommunityToolkit.WinUI.Controls.SettingsControls | 8.2.251219 |
| | CommunityToolkit.WinUI.Controls.Primitives | 8.2.251219 |
| | CommunityToolkit.WinUI.Behaviors | 8.2.251219 |
| | Microsoft.Xaml.Behaviors.WinUI.Managed | 3.0.1 |
| | CommunityToolkit.WinUI.Collections | 8.2.251219 |
| | CommunityToolkit.WinUI.Triggers | 8.2.251219 |
| | CommunityToolkit.WinUI.Extensions | 8.2.251219 |
| | CommunityToolkit.WinUI.UI.Controls.DataGrid | 7.1.2 |
| | Microsoft.Windows.CsWin32 | 0.3.183 (PrivateAssets=all) |
| | Microsoft.Extensions.DependencyInjection | 10.0.7 |
| | Microsoft.Extensions.Hosting | 10.0.7 |
| | System.Security.Cryptography.Xml | 10.0.7 |
| **Winhance.Core** | Microsoft.Extensions.Hosting | 10.0.7 |
| | System.Data.SqlClient | 4.8.6 |
| | System.Drawing.Common | 9.0.0-preview.6.24327.6 |
| | System.DirectoryServices.Protocols | 8.0.0 |
| | System.IO.Packaging | 8.0.1 |
| | Microsoft.Windows.Compatibility | 8.0.10 |
| | System.Security.Cryptography.Xml | 10.0.7 |
| | CommunityToolkit.Mvvm | 8.2.2 |
| **Winhance.Infrastructure** | Microsoft.Extensions.DependencyInjection | 10.0.7 |
| | CommunityToolkit.Mvvm | 8.2.2 |
| | System.Management | 8.0.0 |
| | System.Data.SqlClient | 4.8.6 |
| | System.DirectoryServices.Protocols | 8.0.0 |
| | System.IO.Packaging | 8.0.1 |
| | Microsoft.Windows.Compatibility | 8.0.10 |
| | System.Security.Cryptography.Xml | 10.0.7 |
| **WindowsPackageManager.Interop** | Microsoft.Windows.CsWinRT | 2.0.4 |
| | Microsoft.Windows.CsWin32 | 0.3.49-beta |
| | Microsoft.WindowsPackageManager.ComInterop | 1.9.25180 |
| **tests/** (all 4) | Microsoft.NET.Test.Sdk | 17.12.0 |
| | xunit | 2.9.3 |
| | xunit.runner.visualstudio | 2.8.2 |
| | Moq | 4.20.72 |
| | FluentAssertions | 7.0.0 |
| | coverlet.collector | 6.0.2 |
| **Winhance.UI.Tests** | Microsoft.WindowsAppSDK | 1.8.260416003 |
| | Microsoft.Windows.CsWinRT | 2.2.0 |
| | CommunityToolkit.Mvvm | 8.4.0 |
| **Winhance.IntegrationTests** | Microsoft.Extensions.DependencyInjection | 10.0.7 |

> Note: `System.Data.SqlClient` and `System.Drawing.Common` are referenced but the app has no SQL database — they are legacy/transitive padding. `System.Drawing.Common` is only at `9.0.0-preview`, which is a supply-chain smell worth avoiding in Akari.

## Project-to-Project Reference Graph

```
WindowsPackageManager.Interop   (leaf — WinGet COM projections)
        ▲
Winhance.Core                    (models, interfaces, enums, events, Native/, Localization keys)
        ▲                    ▲
Winhance.Infrastructure ───────┘
        ▲
Winhance.UI   (WinExe, AssemblyName=Winhance, RootNamespace=Winhance.UI)
```

Actual declarations:
- `src/Winhance.UI` → `..\Winhance.Core\Winhance.Core.csproj`, `..\Winhance.Infrastructure\Winhance.Infrastructure.csproj`
- `src/Winhance.Infrastructure` → `..\Winhance.Core\...`, `..\WindowsPackageManager.Interop\...`
- `src/Winhance.Core` → none
- `src/WindowsPackageManager.Interop` → none

Test references:
- `Winhance.Core.Tests` → Core only
- `Winhance.Infrastructure.Tests` → Core + Infrastructure
- `Winhance.UI.Tests` → Core + Infrastructure + UI
- `Winhance.IntegrationTests` → Core + Infrastructure

**`InternalsVisibleTo`:** each of Core / Infrastructure / UI declares `<InternalsVisibleTo Include="Winhance.<Same>.Tests" />`.

## MSBuild / Build Tooling Beyond the Compiler

- `src/Directory.Build.props` + `tests/Directory.Build.props`: when `WINHANCE_LOCAL_BUILD_ROOT` env var is set, redirect `BaseIntermediateOutputPath` / `BaseOutputPath` to `%LOCALAPPDATA%`. Needed because MSBuild's MakeDir-then-write task races the SMB redirector's namespace cache on network shares, and Windows refuses to launch `testhost.exe` from a network share (Win32Exception 5).
- `src/Winhance.UI/Directory.Build.props`: explicitly `<Import Project="..\Directory.Build.props" />` (MSBuild only auto-imports the *first* one it finds walking up).
- `src/Winhance.UI/Directory.Build.targets`: placeholder for CLI-build overrides.
- Solution: `Winhance.sln` (VS 17.5.2 format) with solution folders `src` and `tests`. UI + UI.Tests only support `x64`.
- **No CI build pipeline.** `.github/workflows/` contains only `stale-issues.yml`.
- Scripts in `extras/`:
  - `build-and-package.ps1` — build + Inno Setup installer + optional Authenticode signing (`-SignApplication`, `-CertificateThumbprint`, `-CertificateSubject`), `-Beta`, `-SkipTests`. Requires VS 2022 (".NET desktop development" **and** "Desktop development with C++" for the XAML compiler), .NET 10 SDK, Inno Setup 6, Windows SDK (signtool).
  - `dev-build-and-run.ps1` — dev loop, sets `WINHANCE_LOCAL_BUILD_ROOT`
  - `run-winhance-tests.ps1` — runs all 4 suites; `-SkipUITests`, `-IntegrationOnly`
  - `dev-test-installer.ps1`, `Update-BundledWinGet.ps1`, `GitHub-Automations.ps1`
- `extras/Winhance.Installer.iss` — Inno Setup 6. `AppName=Winhance`, `OutputBaseFilename=Winhance.Installer`, `ArchitecturesAllowed=x64compatible`, `SolidCompression=yes`, `DefaultDirName={autopf}\Winhance`. `[Files]` excludes `nul,onnxruntime.dll,DirectML.dll`. Has a `portableinstall` task driven by `extras/portable.marker`. Registers the `.winhance` file association (`ChangesAssociations=yes`).
- Root launchers: `Winhance.ps1` / `Winhance-Beta.ps1` — download-and-run bootstrappers from GitHub releases.

**Bundled binary payload:** `src/Winhance.Infrastructure/Features/SoftwareApps/Services/WinGet/winget-cli/` ships a vendored WinGet CLI (`winget.exe`, `WindowsPackageManagerServer.exe`, `WindowsPackageManager.dll`, MSVC runtime DLLs, `Microsoft.Management.Configuration.dll`, WebView2 core, `libsmartscreenn.dll`, `winget-version.txt`), copied to output as `winget-cli\...`. Refreshed by `extras/Update-BundledWinGet.ps1`.

## Localization Mechanism

**Folder:** `src/Winhance.UI/Features/Common/Localization/` — **30 flat JSON files**, one per locale:

`en, af, ar, cs, de, el, es, fa, fi(implied), fr, he, hi, hu, it, ja, ko, lt, lv, nl, nl-BE, pl, pt, pt-BR, ru, sv, tr, uk, vi, zh-Hans, zh-Hant`

(Actual files present: `af, ar, cs, de, el, en, es, fa, fr, he, hi, hu, it, ja, ko, lt, lv, nl, nl-BE, pl, pt, pt-BR, ru, sv, tr, uk, vi, zh-Hans, zh-Hant` — 29 locales + `en` fallback.)

**How it works** (`src/Winhance.Infrastructure/Features/Common/Services/LocalizationService.cs`, implements `src/Winhance.Core/Features/Common/Interfaces/ILocalizationService.cs`):

1. **Format:** a flat `{ "Key": "value" }` JSON dictionary — no nesting, no `.resx`, no XAML `x:String`.
2. **Deployment:** `src/Winhance.UI/Winhance.UI.csproj` copies `Features\Common\Localization\*.json` to `$(OutDir)\Localization\` (`Link=Localization\%(Filename)%(Extension)`).
3. **Loading:** `LocalizationService` resolves `<AppDomain.BaseDirectory>\Localization` and loads **two** dictionaries — the current locale and `en` (fallback). Both are `volatile`.
4. **Lookup:** `GetString(key)` → current locale, then `en`, then the literal `"[key]"` sentinel. `GetString(key, params object[] args)` = `string.Format` with a swallow-all catch that returns the raw format on failure.
5. **Resolution of the initial locale:** `ResolveLanguageCode(culture)` tries exact name → parent name → `TwoLetterISOLanguageName` → `"en"`.
6. **Switching:** `SetLanguage(code)` sets `CultureInfo.CurrentUICulture` but deliberately keeps `CultureInfo.CurrentCulture` / `DefaultThreadCurrentCulture` at **InvariantCulture** so numbers (incl. WinUI `NumberBox`) stay locale-independent. Raises `LanguageChanged`.
7. **Display names:** each file can carry `_Meta_LanguageDisplayName`; otherwise `CultureInfo.GetCultureInfo(code).NativeName`. English is always sorted first, then the rest by display name (`InvariantCulture`).
8. **Key naming convention:** `Area_Subject` PascalCase-ish, underscore-separated:
   - Global chrome: `App_Title`, `App_By`, `Nav_Optimize`, `Mode_Switcher_Label`, `Tooltip_*`, `Menu_*`, `Button_*`, `Dialog_*`, `Progress_*`, `Loading_*`, `Theme_*`, `Review_Mode_*`, `Builder_Mode_*`, `Common_*`
   - Per-page: `Category_Optimize_Title`, `Category_Optimize_StatusText`, `Feature_Sound_Name`, `Settings_*`, `SoftwareApps_*`, `WIMUtil_*`, `AdvancedTools_*`, `WindowsApps_Section_*`
   - **Per-setting (auto-generated, see `src/Winhance.Core/Features/Common/Localization/SettingLocalizationKeys.cs`):**
     - `Setting_{LocalizationId ?? Id}_Name`
     - `Setting_{LocalizationId ?? Id}_Description`
     - `Setting_{LocalizationId ?? Id}_Option_{index}`, `_OptionTooltip_{index}`, `_OptionWarning_{index}`, `_Option_Custom`
     - `SettingGroup_{name-without-spaces-and-&}` (compact) and `SettingGroup_{snake_case}` variants
     - `Common_CustomState` = "Custom" (generic unmatched-state label)
   - `LocalizationId` on a `SettingDefinition` lets two OS-gated variants of the same feature (Win10 vs Win11 registry mechanics) share one set of text keys.
9. **Application:** `src/Winhance.UI/Features/Common/Services/SettingLocalizationService.cs` (`ISettingLocalizationService`) rewrites a `SettingDefinition` via `setting with { Name=…, Description=…, GroupName=…, ComboBox=…, NumericRange=… }` (records → non-destructive). `VersionCompatibilityMessage` may embed `Key|arg1|arg2` for `string.Format`.
10. **Live re-render:** every ViewModel subscribes to `ILocalizationService.LanguageChanged` and re-raises `OnPropertyChanged` for each localized string property (see `MainWindowViewModel.OnLanguageChanged`, `SettingsViewModel.OnLanguageChanged`).
11. **RTL:** `ILocalizationService.IsRightToLeft` drives `FlowDirection` (`MainWindow.InitializeFlowDirection`).
12. **Guardrails:** `tests/Winhance.IntegrationTests/Localization/LocalizationJsonValidityTests.cs` (JSON well-formedness) and `LocalizationKeyReferenceTests.cs` (every key a setting requests must exist in `en.json`).

## Test Framework & Organization

**Framework:** xUnit 2.9.3 + Moq 4.20.72 + FluentAssertions 7.0.0 + coverlet 6.0.2, `Microsoft.NET.Test.Sdk` 17.12.0.

**Four suites, four folders under `tests/`:**

| Suite | Project | Refs | Purpose |
|---|---|---|---|
| `Winhance.Core.Tests` | `tests/Winhance.Core.Tests` | Core | Models, enums/constants, helpers, `LogService`, `InitializationService`, `GlobalSettingsRegistry`, `DependencyManager`, **`SettingCatalogValidator`**, `IconCoverage` (uses a snapshot `Assets/package-icons-manifest.json`) |
| `Winhance.Infrastructure.Tests` | `tests/Winhance.Infrastructure.Tests` | Core + Infra | One file per service: registry, PowerCfg, PowerService, `CompatibleSettingsRegistry`, WinGet (`WinGetBootstrapper/DetectionService/PackageInstaller`, `WinGetProgressParser`, `WinGetExitCodes`), Chocolatey, AppX sources, icons, localization, config export/import/migration, script builders, WIM/ISO/oscdimg, event bus |
| `Winhance.UI.Tests` | `tests/Winhance.UI.Tests` | Core + Infra + UI (`UseWinUI=true`, x64) | ViewModels (one file per VM), services (`Config*Service`, `Setting*Service`, `ThemeService`, `NavBadgeService`, …), helpers, converters, `WindowSizeManager` |
| `Winhance.IntegrationTests` | `tests/Winhance.IntegrationTests` | Core + Infra | Config round-trip + schema validation, DI container smoke test, localization JSON/key integrity, registry/file-system against temp dirs (`TempDirectoryFixture`, `TestContext`, `TestSettingFactory`), script-generation |

**Conventions:**
- Mirrored namespace/folder layout (`tests/<Suite>/<Area>/<Type>Tests.cs`).
- xUnit `[Fact]`, class named `<Type>Tests`, test method names `Member_Condition_ExpectedResult` (e.g. `ModuleId_ReturnsSound`, `Constructor_WithValidDependencies_DoesNotThrow`).
- `// Act` / `// Assert` comment markers; FluentAssertions `.Should()` chains.
- Mocks set up in the constructor; localization mocked to **return the key itself**; `IDispatcherService` mocked to execute **synchronously** so assertions run inline.
- Test suites are Windows-only and (for UI) x64-only.

---

*Stack analysis: 2026-10-05*