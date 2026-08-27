# Codebase Concerns

**Analysis Date:** 2026-08-27

## Tech Debt

**Large ViewModel with Complex State Management:**
- Issue: `SettingItemViewModel` is 1462 lines with extensive property bindings, status banner management, technical details expansion, power plan selection, dependency resolution, and advanced unlock gating all in a single class.
- Files: `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs`
- Impact: Difficult to maintain, refactor, or test individual features; high cognitive load for changes; risk of unintended side effects in property change chains.
- Fix approach: Consider extracting status banner logic, technical details manager, and power plan handling into separate collaborative VMs or services; use composition to reduce single-class responsibility.

**Large Code-Behind View File:**
- Issue: `AkariOSPage.xaml.cs` is 2546 lines, combining declarative layout building with imperative UI construction for Post-Install banner, Service Presets, Competitive mode, gaming tweaks, GPU tools, and utilities. Multiple builder methods inline XAML control creation with complex color resources and styling.
- Files: `src/AkariTool.App/Views/AkariOSPage.xaml.cs`
- Impact: Code-behind should be minimal; this violates XAML separation of concerns; difficult to unit test; prone to visual regressions during refactoring; long load time during page instantiation.
- Fix approach: Progressively extract builder methods into separate UserControl definitions or a template factory service; leverage attached behaviors and value converters to move complex logic to XAML binding expressions; consider a ControlTemplate-based composition pattern.

**Massive Declarative Catalogs:**
- Issue: `GamingOptimizations.cs` (3427 lines), `PrivacyOptimizations.cs` (2934 lines), `ExplorerOptimizations.cs` (2026 lines), and `PowerOptimizations.cs` (1624 lines) are data-heavy catalog definitions that may slow compilation and increase assembly size.
- Files: `src/AkariTool.Core/Features/Gaming/Catalogs/GamingOptimizations.cs`, `src/AkariTool.Core/Features/Privacy/Catalogs/PrivacyOptimizations.cs`, `src/AkariTool.Core/Features/Customize/Catalogs/ExplorerOptimizations.cs`, `src/AkariTool.Core/Features/Power/Catalogs/PowerOptimizations.cs`
- Impact: Long compilation times; difficult to review changes; increased working-set memory; risk of accidental duplication of definitions across tabs.
- Fix approach: Consider extracting catalog definitions to separate partial files organized by setting type (registry, scheduled tasks, PowerCfg, scripts); investigate source generator or compile-time embedding for large static data to reduce in-memory overhead during development.

## Known Bugs

**Power Scheme Drift Detection:**
- Symptoms: When a user manually activates a different power plan outside of the app, the Power tab's UI does not reflect the change until the page is refreshed or reloaded; no indication that the system state has drifted from the persisted baseline.
- Files: `src/AkariTool.App/ViewModels/PowerViewModel.cs`, `src/AkariTool.Infrastructure/Features/Common/Services/SettingStateReader.cs`
- Trigger: Manually switch power plans via Windows Settings or `powercfg /setactive`, then navigate to the Power tab.
- Workaround: Refresh the page or navigate away and back; drift is detected and surfaced on next read. The design notes in CLAUDE.md state drift detection should surface via a persist indicator on read, but never reactivate from a read path — only on write.

**AkariOS Page Stub Sections Visible:**
- Symptoms: Three placeholder cards appear on the AkariOS tab stating "not yet ported" — "Gaming Tweaks", "Utilities Panel", "Useful Tools". These are intentional scaffolding visible to track remaining work (Phase 11), not missing functionality.
- Files: `src/AkariTool.App/Views/AkariOSPage.xaml.cs` (lines ~2478–2519 and throughout Build())
- Trigger: Launch app and navigate to AkariOS tab.
- Workaround: Not a bug — intentional by design. Replaced by real builders when their sub-sections are signed off.

## Security Considerations

**Process Launch Game Path Validation:**
- Risk: Game paths passed to `ProcessStartInfo` in `CompetitiveService.BuildLaunchStartInfo` and `GameDetection.SelectGameExe` rely on Steam library enumeration and filename heuristics for validation. A malformed or symlink-based path could theoretically execute an unintended binary or DLL.
- Files: `src/AkariTool.Infrastructure/Services/CompetitiveService.cs` (line 612–619), `src/AkariTool.Infrastructure/Services/GameDetection.cs` (line 90–150)
- Current mitigation: Game paths are discovered via Steam's standardized `steamapps/common` directory enumeration, with exclusion filters for known non-executable folders and suspicious filenames; hard exclusions prevent launching system services or utilities. Steam launches use the `steam://` protocol handler, not direct path execution.
- Recommendations: Add a post-discovery validation step to verify the resolved executable exists and is not a shortcut or symlink pointing outside the expected steam game directory; consider pinning to well-known game directories only; log all resolved paths for audit trails.

**Registry Write ACL Handling:**
- Risk: Registry writes via `WindowsRegistryService.SetValue` and `CreateSubKey` may throw `SecurityException` on ACL-locked keys (e.g., `HKLM\SYSTEM\ControlSet*\Services\*` keys for protected services), but the exception type differs from `UnauthorizedAccessException`. Callers expecting only `UnauthorizedAccessException` may not handle the security-related failure properly.
- Files: `src/AkariTool.Infrastructure/Features/Common/Services/WindowsRegistryService.cs` (line 185–200), `src/AkariTool.Infrastructure/Services/CompetitiveService.cs` (line 422–430)
- Current mitigation: Exceptions are caught broadly as `Exception` and logged; no distinction is made between permission denied and other failures. The catch-all logging approach masks the specific security event.
- Recommendations: Distinguish `SecurityException` from `UnauthorizedAccessException` in logging; surface ACL failures to the user as a specific elevation/permission issue rather than a generic error; add telemetry for ACL-denied writes to track policy-protected keys.

**Embedded Resource Extraction (Defender CAB/PS1):**
- Risk: `DefenderService.ExtractEmbeddedAsync` extracts binary payloads (NoDefender.cab, DisableDefender.ps1) to the temp folder with a random GUID name but still relies on temp folder access controls. A compromised temp directory or race condition could allow substitution or inspection of embedded payloads.
- Files: `src/AkariTool.App/Services/DefenderService.cs` (line 241–252)
- Current mitigation: Random file naming (`{Guid}`) reduces guessability; files are extracted to the default Windows temp directory (subject to OS ACLs); files are deleted in a finally block, though cleanup failure is silently ignored.
- Recommendations: Use isolated temp directories or `Path.GetTempFileName()` for improved isolation; explicitly verify file permissions after extraction; consider signing the embedded payloads and validating signatures after extraction; log extraction and cleanup success/failure; consider one-time download verification if updates are feasible.

## Performance Bottlenecks

**Warm-Up Blocking Build() Calls on Background Thread:**
- Problem: `SettingPageWarmUp.Run()` calls `Build()` on every `SettingPageViewModel` (Gaming, Sound, Notifications, Privacy, Update, Customize×5, Power) on a background thread. While this prevents blocking the UI, it loads the entire object graph (all SettingDefinitions, state readers, dependency resolvers) into memory at startup even if the user never navigates to those tabs.
- Files: `src/AkariTool.App/Services/SettingPageWarmUp.cs`, `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs` (Build method)
- Cause: Backup export/import and global search require all pages to be built so their rows are registered with `SettingBackupService`; delaying the build would leave those features incomplete for never-visited tabs.
- Improvement path: Implement lazy building on first-access with a fallback pre-build only for export/search contexts; cache built pages aggressively; profile warm-up time and consider incremental building over multiple time slices rather than sequentially.

**Catalog Definitions as Code (No Lazy Loading):**
- Problem: Large catalogs like `GamingOptimizations` (3427 lines) define thousands of `SettingDefinition` objects in static initializers that are loaded into memory for the entire app lifetime, even if only a fraction are used in a session.
- Files: `src/AkariTool.Core/Features/Gaming/Catalogs/GamingOptimizations.cs` and similar
- Cause: Catalogs are generated as C# static methods returning `IReadOnlyList<SettingGroup>`, so the entire list is materialized at reference time.
- Improvement path: Consider lazy/generator-based catalog loading; split catalogs by feature sub-category and load on-demand; investigate source generators to reduce generated code footprint in the binary.

## Fragile Areas

**CompetitiveService Static Mutable State Without Synchronization:**
- Files: `src/AkariTool.Infrastructure/Services/CompetitiveService.cs` (line 95–96, 261, 694–696)
- Why fragile: Static fields `_current` (CompetitiveSessionState) and `_watcherCts` (CancellationTokenSource) are accessed and modified from multiple contexts (UI thread calling `StartAsync`, background watcher thread calling `EndAsync`, event handlers) without locks. Concurrent reads/writes can cause torn state or race conditions if the UI calls `EndAsync` while the watcher thread is modifying the state or checking `IsSessionActive`.
- Safe modification: Always wrap access to `_current` and `_watcherCts` in lock statements; consider using Interlocked operations or a ReaderWriterLockSlim for high-contention scenarios; add explicit synchronization documentation; ensure the event `SessionEndedByGameExit` marshals to the UI dispatcher before invoking listeners.
- Test coverage: No unit tests for `CompetitiveService`; no concurrency/stress tests; critical to add tests that verify state consistency under concurrent start/end scenarios.

**SettingItemViewModel Dependency on Weak Dependencies:**
- Files: `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` (ctor, line 51–153)
- Why fragile: Constructor accepts optional dependencies (`IPowerPlanComboBoxService`, `IPowerService`, `ISettingDependencyResolver`, etc.) as nullable. If a dependency is expected by downstream code but not provided, null reference exceptions may occur deep in property getters or event handlers. Example: if `_dependencyResolver` is null, `ApplyWithDependencyPipelineAsync` still calls methods that assume it exists.
- Safe modification: Make optional dependencies explicit by splitting the constructor into required/optional factories; add null guards at the call site; document which methods require which dependencies; consider a builder pattern to enforce dependency completeness; add asserts in debug builds to catch missing dependencies early.
- Test coverage: Test both full initialization and minimal initialization scenarios; verify graceful degradation when optional dependencies are null.

**TweakDialogs XamlRoot Late Initialization:**
- Files: `src/AkariTool.App/Services/TweakDialogs.cs`, `src/AkariTool.App/MainWindow.xaml.cs` (line 501–520, XamlRoot assignment)
- Why fragile: `TweakDialogs` requires `XamlRoot` to be initialized before any dialog is shown, but `XamlRoot` is only assigned in `MainWindow.Loaded` (after `InitializeComponent`). If a VM is constructed and attempts to show a dialog before the root element is loaded, the dialog fails silently and renders blank.
- Safe modification: Add explicit guards in dialog methods to throw descriptively if `XamlRoot` is null; add a public initialization method that must be called before use; document the initialization requirement; consider a service factory that ensures initialization order.
- Test coverage: Add integration tests that verify dialogs fail safely if shown before initialization.

**ElevationService.RunAsSystem Impersonation Scope:**
- Files: `src/AkariTool.Infrastructure/Services/ElevationService.cs`, `src/AkariTool.App/Services/DefenderService.cs` (line 106–118, 149–230)
- Why fragile: `RunAsSystem` impersonates SYSTEM identity to execute code in a lambda. If the lambda throws or the impersonation fails mid-execution, the restoration of the original identity may be incomplete, leaving the process in an elevated state. Similarly, nested or re-entrant calls to `RunAsSystem` may interfere with identity restoration.
- Safe modification: Verify that `RunAsSystem` uses try/finally to guarantee identity restoration; add re-entrancy guards (detect and reject nested calls or use a stack-based identity tracker); log identity changes for audit purposes; consider a scoped identity context helper to simplify callers.
- Test coverage: Unit tests with permission failures; tests for proper restoration on exception; stress tests for concurrent/nested identity changes.

## Scaling Limits

**Backup Export Memory Overhead:**
- Current capacity: Entire SettingBackupService enumeration materializes all rows from all pages into memory at once (via `EnumerateItems()`), then iterates to build JSON payloads.
- Limit: For a catalog with 1000+ settings across 10+ pages, the combined serialization and JSON building could exceed available memory on constrained systems or stall the UI during export on slower hardware.
- Scaling path: Implement streaming JSON export to file in chunks; use generators to lazily enumerate rows; consider a paged export UI that exports/imports by tab rather than the full catalog at once.

**Warm-Up Thread Startup Time:**
- Current capacity: Building 8+ SettingPageViewModels sequentially on a background thread takes time proportional to the sum of all catalog sizes and state reader invocations.
- Limit: As catalogs grow (each new tab adds hundreds of settings), startup time increases linearly; this becomes noticeable on slower systems or with high CPU contention at launch.
- Scaling path: Parallelize the warm-up phase (build multiple pages concurrently on a thread pool, with care for shared state); implement incremental/lazy building triggered by tab navigation; measure and profile warm-up time continuously; set a target maximum (e.g., 2–3 seconds) and defer non-critical work.

## Dependencies at Risk

**WinUI Framework Local Vendoring:**
- Risk: `vendor/WinUI.Framework/` is vendored (checked into the repo as a ProjectReference). If the upstream WinUI.Framework diverges or receives critical bug fixes, Akari Tool must track updates manually or miss bug fixes and security patches.
- Impact: Breaks compatibility if the upstream API changes; accumulates technical debt as the vendored copy ages; increases maintenance burden.
- Migration plan: Evaluate if WinUI.Framework can be published to a NuGet feed (internal or public) and referenced as a package dependency instead; set up a process to cherry-pick upstream security fixes; or migrate directly to the latest WinUI 3 APIs if the framework dependency can be eliminated.

**Winhance Parity Maintenance:**
- Risk: Akari Tool is a port of Winhance (upstream reference repo at `C:\Users\isleap\Documents\GitHub\Winhance`). As Winhance evolves, Akari may diverge, especially in areas like the SettingDefinition model, catalog definitions, badge logic, and compatibility gating. A lack of systematic sync-up will cause feature/bug-fix misses.
- Impact: Akari may miss important bug fixes or new features ported upstream; inconsistent user experience across the two codebases; increased effort to debug issues that may have been resolved in Winhance.
- Migration plan: Establish a regular (e.g., quarterly) parity audit comparing key files (models, enums, badge helpers, gating pipeline) against Winhance; document known intentional divergences; consider a shared Common NuGet package for models and core logic; automate diff reports for major Winhance commits that affect ported areas.

## Missing Critical Features

**Stub Sections on AkariOS Tab (Intentional Deferred Work):**
- Problem: Three sections are intentionally not ported: "Competitive Mode B–D" (advanced tuning options), "Utilities Panel" (Account, Interface, System settings), "Useful Tools" (external tool launchers). These render as visible placeholder cards to track remaining work.
- Blocks: Users cannot configure those advanced options through the Akari GUI; must fall back to manual configuration or external tools.
- Planned: Marked as Phase 11+ work; sign-off by isleap required before implementation. This is intentional deferral, not a bug.

## Test Coverage Gaps

**No Tests for Large Infrastructure Services:**
- What's not tested: `CompetitiveService` (753 lines, critical session management), `PlaybookTweaks` (771 lines, AkariOS-specific service control), `NvidiaProfileService` (100+ lines, GPU profile management), `BcdBackup` (170+ lines, boot configuration editing), `DriftScanner` (registry drift detection), `PostInstallService`, `WimUtilService`, `GpuTweaks`.
- Files: `src/AkariTool.Infrastructure/Services/CompetitiveService.cs`, `src/AkariTool.Infrastructure/Services/PlaybookTweaks.cs` (and 7 partials), `src/AkariTool.Infrastructure/Services/NvidiaProfileService.cs`, `src/AkariTool.Infrastructure/Services/BcdBackup.cs`, `src/AkariTool.Infrastructure/Services/DriftScanner.cs`, and others
- Risk: These services execute system-level operations (process suspension, service state changes, boot configuration, WIM operations, drift detection) without unit test coverage. A regression in any of these could cause system instability, service mismanagement, or data loss. Integration tests on hardware are the only validation.
- Priority: **High** — CompetitiveService and PlaybookTweaks are particularly critical as they directly manipulate system services and power settings. BcdBackup and WimUtilService involve destructive operations that should be validated before production use.

**No Tests for App-Layer Services:**
- What's not tested: `SettingBackupService` (export/import, global search), `SettingPageWarmUp` (build order and initialization), `DefenderPhase2Scheduler` (post-reboot automation), `StartupNotificationService`, `StartupOrchestrator`, `NavBadgeService`, all ViewModels (Gaming, Power, etc.).
- Files: `src/AkariTool.App/Services/SettingBackupService.cs`, `src/AkariTool.App/Services/SettingPageWarmUp.cs`, `src/AkariTool.App/Services/DefenderPhase2Scheduler.cs`, `src/AkariTool.App/ViewModels/Tweaks/SettingPageViewModel.cs`, and others
- Risk: Backup/restore operations may corrupt or lose user preferences; startup initialization may fail silently, leaving the app in a degraded state; nav badges may be miscalculated, confusing users about the number of recommended changes.
- Priority: **High** — SettingBackupService directly affects data persistence; SettingPageWarmUp affects startup reliability.

**No Tests for Async Event Handling:**
- What's not tested: Async void event handlers in Views (e.g., `HomePage.ApplyAllButton_Click`, `HubView.ApplyRecommended_Click`) fire async operations that may throw. Unhandled exceptions in async void handlers crash the app or silently swallow errors.
- Files: `src/AkariTool.App/Views/HomePage.xaml.cs`, `src/AkariTool.App/Views/Controls/HubView.xaml.cs`, `src/AkariTool.App/Views/WindowsAppsPage.xaml.cs`, others
- Risk: User clicks a button, an async operation throws deep in a Task, but the exception is never observed, resulting in a silent failure or race condition.
- Priority: **Medium** — Add integration tests that verify click handlers complete successfully and error dialogs are shown on exception.

**No Tests for Power Plan ComboBox State Synchronization:**
- What's not tested: `PowerPlanComboBoxService`, plan option loading, selection persistence, refresh-after-plan-change behavior. The special handling for dynamic power plan options (loading from the system at runtime) and the sibling-row re-read logic on plan change are not validated.
- Files: `src/AkariTool.Infrastructure/Features/Common/Services/PowerPlanComboBoxService.cs`, `src/AkariTool.App/ViewModels/Tweaks/SettingItemViewModel.cs` (Power Plan selection path)
- Risk: A change to power plan loading or the plan-change event may break the synchronization, leaving rows displaying stale plan names or indices mismatched to system state.
- Priority: **Medium** — Power-related settings are frequently used and affect performance; validation is important.

---

*Concerns audit: 2026-08-27*
