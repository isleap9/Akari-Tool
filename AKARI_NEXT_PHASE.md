# Akari Tool — Next Phase Migration Plan

---

## Current state (as of this document)

Architecture migration (Sessions 0–7) is fully complete. The repo looks like this:

```
Akari-Tool/
├── AkariTool.sln
├── src/
│   ├── AkariTool.Core/          ← pure models, interfaces, compiler-enforced
│   ├── AkariTool.Infrastructure/ ← OS services, wrappers
│   └── AkariTool.App/           ← WinUI 3 shell, ViewModels, XAML
├── tests/
│   ├── AkariTool.Core.Tests/    ← 46 passing
│   └── AkariTool.Infrastructure.Tests/ ← 24 passing + 1 skipped
├── vendor/
├── build-installer.ps1
├── build-deelevated.ps1
└── CLAUDE.md / README / CHANGELOG
```

Build command: `msbuild AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64`
Output path: `src/AkariTool.App/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/`

---

## Decisions already made

- **SettingDefinition model**: adopted 1:1 from Winhance (declarative, data-only)
- **Badge computation**: adopted 1:1 from Winhance (`ComputeBadgeState`, `BadgePillState`, `SettingBadgeKind`)
- **AkariOS-specific tweaks**: Option 1 — AkariOS extension fields on `SettingDefinition` (stays declarative, Akari-specific)
- **Catalog files**: one file per tab (no more split partials — merged during Phase 4 rewrite)
- **Track B** (WinGet/Chocolatey parity): deferred until Track A is complete

---

## Full ordered plan

### Step 1 — Housekeeping (15 min, terminal)

```bash
# Delete the 7z archive that should not be in the repo
git rm "Akari-Tool.7z"

# Move architecture plan to docs/
git mv AKARI_ARCHITECTURE_PLAN.md docs/AKARI_ARCHITECTURE_PLAN.md

# Verify these are gitignored (should not appear in git status)
# bin/, obj/, installer-output/

git add -A
git commit -m "chore: housekeeping — remove 7z archive, move architecture plan to docs/"
```

---

### Step 2 — Project rename (1 Claude Code session)

Rename the main app project from `AkariTool` to `AkariTool.App` to match
Winhance's naming convention (`Winhance.UI` equivalent).

**What changes:**
- `AkariTool.csproj` → `AkariTool.App.csproj`
- Project name in `AkariTool.sln` → `AkariTool.App`
- Add `<AssemblyName>AkariTool</AssemblyName>` to keep exe name identical
- Add `<RootNamespace>AkariTool</RootNamespace>` unchanged
- Update build scripts if they reference the csproj by name
- Update `CLAUDE.md`

**What does NOT change:**
- Assembly name → stays `AkariTool.exe`
- Root namespace → stays `AkariTool`
- Any source file
- Any using directive

---

### Step 3 — Folder rename (1 Claude Code session)

Rename `Tabs/` → `Features/` inside `src/AkariTool.App/` to match
Winhance's folder convention.

**What changes:**
- `src/AkariTool.App/Tabs/` → `src/AkariTool.App/Features/`
- All namespace references to `AkariTool.Tabs` updated across the repo
- All using directives updated

**What does NOT change:**
- Any logic
- Any file content beyond namespace strings

---

### Step 4 — Track A: SettingDefinition Migration

This is the largest change in the project's history. The entire tweak system
is replaced from a delegate-based model to Winhance's declarative model.

**Total estimated effort: 10–13 Claude Code sessions across several weeks.**

The app will be partially broken during Phases 2–3. Create a git tag before
starting Phase 1 as a rollback anchor.

```bash
git tag pre-settingdefinition-migration
```

---

#### Track A — Phase 1: Core layer (2–3 sessions)

**Goal:** Create all new model types in `AkariTool.Core`.
No behavior changes. No catalog changes. Pure model creation.

**Old files to eventually replace (do NOT delete yet):**
- `src/AkariTool.Core/Tweaks/TweakDefinition.cs`
- `src/AkariTool.Core/Tweaks/TweakTargets.cs`

**New files to create in `src/AkariTool.Core/`:**

*Models (copied 1:1 from Winhance):*
- `Features/Common/Models/BaseDefinition.cs`
- `Features/Common/Models/SettingDefinition.cs`
- `Features/Common/Models/RegistrySetting.cs`
- `Features/Common/Models/SettingGroup.cs`
- `Features/Common/Models/BadgePillState.cs`
- `Features/Common/Models/FeatureBadgeSummary.cs`
- `Features/Common/Models/SettingDefinitionToggleState.cs`
- `Features/Common/Models/ScheduledTaskSetting.cs`
- `Features/Common/Models/PowerShellScriptSetting.cs`
- `Features/Common/Models/RegContentSetting.cs`
- `Features/Common/Models/PowerCfgSetting.cs`
- `Features/Common/Models/NativePowerApiSetting.cs`
- `Features/Common/Models/SettingDependency.cs`
- `Features/Common/Models/ComboBoxMetadata.cs`
- `Features/Common/Models/ComboBoxOption.cs`
- `Features/Common/Models/NumericRangeMetadata.cs`

*Enums (copied 1:1 from Winhance):*
- `Features/Common/Enums/InputType.cs`
- `Features/Common/Enums/DetectionType.cs`
- `Features/Common/Enums/SettingBadgeKind.cs` — `Recommended, Default, Custom, Preference`
- `Features/Common/Enums/SettingBadgeMode.cs` — `None, AC, DC`

*AkariOS extension types (new, not in Winhance):*
- `Features/AkariOS/Models/BcdOperation.cs`
- `Features/AkariOS/Models/PlaybookTweakAction.cs`
- `Features/AkariOS/Models/ServicePresetKind.cs`

These AkariOS types get added as optional list fields on `SettingDefinition`:
```csharp
public IReadOnlyList<BcdOperation>? BcdOperations { get; init; }
public IReadOnlyList<PlaybookTweakAction>? PlaybookActions { get; init; }
public ServicePresetKind? ServicePreset { get; init; }
```

**Sign-off:** All 6 projects build clean. No existing behavior changed.

---

#### Track A — Phase 2: Interpreter service (3–4 sessions)

**Goal:** Replace `TweakHelpers.Apply.cs` and `TweakHelpers.State.cs` with a
`SettingOperationExecutor` service in `AkariTool.Infrastructure` that reads
`SettingDefinition` declaratively and applies/reads values.

**What gets replaced:**
- `src/AkariTool.Infrastructure/Services/TweakHelpers.Apply.cs`
- `src/AkariTool.Infrastructure/Services/TweakHelpers.State.cs`

**What gets created:**
- `src/AkariTool.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs`
- `src/AkariTool.Core/Features/Common/Interfaces/ISettingOperationExecutor.cs`

The executor handles:
- `RegistrySettings[]` — read/write registry values declaratively
- `ScheduledTaskSettings[]` — enable/disable scheduled tasks
- `PowerShellScripts[]` — run PS scripts
- `BcdOperations[]` — AkariOS BCD operations
- `PlaybookActions[]` — AkariOS playbook tweaks
- `ServicePreset` — AkariOS service presets

**Sign-off:** All 6 projects build clean. Existing tweaks still work (old
TweakDefinition system still in place during this phase).

---

#### Track A — Phase 3: ViewModel adaptation (2–3 sessions)

**Goal:** Adapt the ViewModel layer to work with `SettingDefinition` instead
of `TweakDefinition`. Port `ComputeBadgeState()` from Winhance 1:1.

**What gets replaced/adapted:**
- `TweakItemViewModel` → `SettingItemViewModel`
- `ToggleTweakViewModel` → merged into `SettingItemViewModel`
- `DropdownTweakViewModel` → merged into `SettingItemViewModel`
- `TweakBadgeViewModel` → replaced by `BadgePillState` records
- `TweakPageViewModel` → adapted to use `SettingDefinition` + `SettingGroup`
- `TweakRegistry` → adapted to store `SettingDefinition`

**Badge computation ported 1:1 from Winhance:**
- `ComputeBadgeState()` — checks RegistrySettings, ScheduledTaskSettings, PowerCfgSettings
- `SettingDefinitionToggleState` — toggle state resolver
- `FeatureBadgeAggregator` — aggregates counts for overview cards
- `BadgePillState` records — what the view binds to

**Sign-off:** App launches. Navigation works. Badge pills render correctly.
Old catalog files still in place (not yet converted).

---

#### Track A — Phase 4: Catalog conversion (6–8 sessions)

**Goal:** Rewrite all tweak catalog files from delegate-based to declarative
`SettingDefinition` records. One tab at a time. Old partial files deleted,
replaced by single files per tab.

**Order (simplest first, most complex last):**

| Tab | Old files | New single file | Complexity |
|-----|-----------|-----------------|------------|
| Sound | `SoundTweaks.cs` | `SoundOptimizations.cs` | Low |
| Notifications | `NotificationsTweaks.cs` | `NotificationOptimizations.cs` | Low |
| Update | `UpdateTweaks.cs` | `UpdateOptimizations.cs` | Low |
| Privacy | `PrivacyTweaks*.cs` (multiple) | `PrivacyOptimizations.cs` | Medium |
| Customize | `CustomizeTweaks*.cs` (multiple) | `CustomizeOptimizations.cs` | Medium |
| Gaming | `GamingTweaks*.cs` (multiple) | `GamingOptimizations.cs` | High |
| AkariOS | `PlaybookTweaks*.cs`, `ServicesPreset*.cs` etc. | `AkariOSOptimizations.cs` | High |

**Per-tab process:**
1. Read old partial files in full
2. Write new single declarative file
3. Delete old partial files
4. Build + smoke test that tab
5. Verify `[WARMUP]` tile count stays at 439
6. Commit before next tab

**AkariOS-specific notes:**
- Service presets use `ServicePreset` extension field
- BCD operations use `BcdOperations[]` extension field
- Playbook tweaks use `PlaybookActions[]` extension field
- Defender tweaks: copy byte-for-byte, never change behavior

**Sign-off per tab:** `[WARMUP] OK: … tiling [0..439)` in startup log.
Final sign-off: all 439 tweaks working, backup export/import working.

---

#### Track A — Phase 5: Tests rewritten (1–2 sessions)

**Goal:** Rewrite the 70 existing tests for the new model. Add new tests
for `SettingOperationExecutor`.

**What gets deleted:**
- `TweakDefinitionTests.cs`
- `TweakTargetsTests.cs`
- `UpdateModelsTests.cs`
- `UpdateServiceTests.cs`

**What gets created:**
- `SettingDefinitionTests.cs` — badge computation, toggle state resolver
- `RegistrySettingTests.cs` — value matching, enabled/disabled arrays
- `SettingDefinitionToggleStateTests.cs` — recommended/default resolution
- `SettingOperationExecutorTests.cs` — mocked registry reads/writes
- `FeatureBadgeAggregatorTests.cs` — aggregation logic

**Sign-off:** `dotnet test` — all tests passing, 0 failures.

---

### Step 5 — Track B: Software tab WinGet/Chocolatey parity

**Deferred until Track A is complete.**

This adds proper WinGet COM API support and Chocolatey service matching
Winhance's `WindowsPackageManager.Interop` project and
`AppInstallationService` pattern.

New project: `src/AkariTool.WinGet.Interop/`
New services: `WinGetService`, `ChocolateyService`, `AppInstallationService`

Scope TBD — plan separately when Track A is done.

---

## What 1:1 with Winhance means for this migration

**WILL be 1:1:**
- `SettingDefinition` model — identical fields
- `RegistrySetting` — identical structure
- Badge computation — identical algorithm
- `BadgePillState`, `SettingBadgeKind`, `SettingBadgeMode` — identical
- `SettingDefinitionToggleState` — identical resolver
- `FeatureBadgeSummary` — identical
- Single catalog file per tab — identical pattern

**Will NOT be 1:1 (intentional differences):**
- Project named `AkariTool.App` not `Winhance.UI` — correct, different product
- No localization system — Akari uses inline strings
- Icon system — Akari uses Icons8 Fluency CDN not Material/Fluent icons
- AkariOS extension fields — Akari-specific, not in Winhance
- Tweak content — Akari has AkariOS tweaks, different recommendations
- No `WindowsPackageManager.Interop` yet — Track B

---

## Key rules — never violate during this migration

- **`[WARMUP]` tile count = 439** — verify after every catalog conversion
- **TweakDefinition IDs preserved byte-for-byte** — backup file compatibility
- **Defender code: never touch** without explicit sign-off
- **Build with VS MSBuild** — never bare `dotnet build` for the app
- **One tab at a time in Phase 4** — build + smoke test + commit before next
- **No behavioral improvements bundled** — migration steps only
- **Show source first, confirm scope, get sign-off** before any change
