# Stack Research

**Domain:** Windows desktop system-utility features (uninstaller leftover cleanup, disk-space/duplicate-file scanning, per-NIC registry tuning) inside an existing WinUI 3 / .NET 10 app
**Researched:** 2026-08-27
**Confidence:** HIGH overall (grounded in current Microsoft Learn docs + the app's own existing source, not just training memory)

## Recommended Stack

No new heavy frameworks are needed for any of the three features. Everything below is either
already a dependency of this repo, a BCL/.NET 10 API, or a built-in Windows component reachable
through the existing `IWindowsRegistryService` / `IPowerShellRunner` / `IProcessExecutor` /
`IScheduledTaskService` seams in `AkariTool.Infrastructure`.

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `Microsoft.Win32.Registry` (BCL, via `IWindowsRegistryService`) | .NET 10 built-in | Enumerate `...\Uninstall` keys for leftover detection, read/write NIC Ndi\Params keys if a raw-registry fallback is needed | Already the app's single registry seam (`WindowsRegistryService`); zero new surface, ACL-exception handling already implemented |
| `System.IO.Enumeration` / `Directory.EnumerateFiles` (BCL) | .NET 10 built-in | Streaming, lazy NTFS directory walk for the System Cleaner (junk scan, large-file finder, duplicate finder) | Backed by `FindFirstFileEx` with `FIND_FIRST_EX_LARGE_FETCH` since .NET Core 3.0 — no P/Invoke needed; `Directory.EnumerateFiles` streams results instead of materializing the whole tree (`Directory.GetFiles` blocks and allocates on large volumes) |
| `System.IO.Hashing` (NuGet, Microsoft-owned) | 10.0.x (latest stable **10.0.11**, targets net8.0+/net10.0) | Non-cryptographic content hashing (XxHash3/XxHash64) for the duplicate-file finder | Purpose-built, in-box-quality Microsoft package for exactly this use case — dramatically faster than `SHA256`/`MD5` for bulk file comparison; ships in the same `System.*` NuGet family already pinned in this repo (`System.Management` 10.0.0, `System.ServiceProcess.ServiceController` 10.0.0) so it fits the existing versioning convention |
| PowerShell `NetAdapter` module (`Get-/Set-/Reset-NetAdapterAdvancedProperty`) via existing `IPowerShellRunner` | Built into Windows 10/11 (no install) | Enumerate and write per-adapter NDIS driver advanced properties (interrupt moderation, RSS, offloads, buffer sizes) with real per-value revert | This is the **officially supported, validated** surface for exactly the `*`-prefixed registry keywords `network-apply.bat` already targets (`*RSS`, `*InterruptModeration`, `*LsoV2IPv4`, `*ReceiveBuffers`, etc.). It targets adapters by friendly `-Name`, so no NNNN-subkey correlation is needed; `Reset-NetAdapterAdvancedProperty` restores the driver's factory default per keyword — the real per-value revert this milestone asks for |
| Task Scheduler COM automation (`Schedule.Service` ProgID, late-bound `dynamic`) | Windows built-in (Task Scheduler 2.0 COM API, unchanged since Vista) | Enumerate all scheduled tasks (recursive folder walk) and inspect each task's Actions for leftover-uninstaller detection | Already the exact pattern used by `ScheduledTaskService.cs` (`Type.GetTypeFromProgID("Schedule.Service")` + `dynamic`) — extend it with a recursive `GetFolders()`/`GetTasks()` walk instead of adding a new dependency |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Net.NetworkInformation.NetworkInterface` (BCL) | .NET 10 built-in | Cross-reference adapter GUID (`NetworkInterface.Id`) against `NetCfgInstanceId` when a UI needs adapter metadata beyond what `Get-NetAdapter` returns | Already used elsewhere in the Infrastructure layer (`WindowsRegistryService`, DNS scripts) — reuse rather than add WMI-only code paths for simple adapter listing |
| `System.Management` (already a dependency, 10.0.0) | 10.0.0 (pin to match existing) | Optional direct-CIM path (`root\StandardCimv2` → `MSFT_NetAdapter`, `MSFT_NetAdapterAdvancedPropertySettingData`) if a future perf pass wants to avoid PowerShell process spin-up per call | Fallback/optimization only — see Alternatives below; not required for MVP |
| `Microsoft.Win32.Registry.GetSubKeyNames` (`IWindowsRegistryService.GetSubKeyNames`, existing) | Existing | Enumerate `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`, its `WOW6432Node` twin, and `HKCU\...\Uninstall` for leftover registry-orphan detection | Standard, unchanged-since-XP location for install/uninstall metadata (`DisplayName`, `Publisher`, `InstallLocation`, `UninstallString`, `EstimatedSize`) |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `IPowerShellRunner` (existing Infrastructure service) | Executes `Get-/Set-/Reset-NetAdapterAdvancedProperty` and returns `OperationResult` | Have it call with `-ErrorAction Stop | ConvertTo-Json -Depth 3` and parse the JSON into a small DTO — avoids brittle text-scraping of PowerShell's default table output |
| `IScheduledTaskService` (existing Infrastructure service, extend) | Add an `EnumerateTasksAsync()`/`GetTaskActionsAsync(taskPath)` method alongside the existing `EnableTaskAsync`/`DisableTaskAsync` | Same COM object (`Schedule.Service`), same `dynamic` + `ReleaseComObject` disposal pattern already proven in this file |
| `IWindowsRegistryService` (existing, extend) | Add read-only enumeration helpers for the Uninstall key tree if not already exposed generically enough | `GetSubKeyNames` + per-value read already exist; leftover scan just needs to walk 2–3 known roots, no new registry primitive required |

## Installation

```bash
# Only one new package needed across all three features
dotnet add src/AkariTool.Infrastructure/AkariTool.Infrastructure.csproj package System.IO.Hashing --version 10.0.0
```

Everything else (registry, PowerShell runner, Task Scheduler COM, `Directory.EnumerateFiles`,
`NetworkInterface`) is either BCL or already referenced in this solution.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| `NetAdapterAdvancedProperty` cmdlets via `IPowerShellRunner` | Direct WMI/CIM calls to `MSFT_NetAdapterAdvancedPropertySettingData` in `root\StandardCimv2` via `System.Management` | Only if profiling shows PowerShell process-spawn overhead matters (e.g. applying to many adapters in a tight loop); the CIM `Put()`/method-invoke path for this specific class is thinly documented for raw C# (`ManagementBaseObject.InvokeMethod`), so it's a real cost/benefit tradeoff, not a free upgrade |
| Content hashing with `System.IO.Hashing` (XxHash3/XxHash64) | `System.Security.Cryptography.SHA256`/`MD5` | Only if the duplicate-file finder needs a cryptographically-verifiable hash for compliance reasons — not the case here; XxHash3 is non-cryptographic but that's irrelevant for local duplicate detection, and it is materially faster |
| Recursive COM walk over `Schedule.Service` (extend existing `ScheduledTaskService`) | `Microsoft.Win32.TaskScheduler` NuGet (dahall/TaskScheduler, the de facto managed wrapper) | Only if the raw COM `dynamic` calls become unwieldy for the richer Actions/triggers introspection the leftover scanner needs; the NuGet wrapper is well-maintained and typed, but this repo already has a working raw-COM pattern and the guidance is to avoid new third-party libraries where an existing pattern fits |
| Registry-key enumeration under `...\Uninstall` for leftover detection | Full before/after filesystem+registry snapshot diffing (what tools like "Total Uninstall" do) | Only if this milestone captures state *before* the user triggers uninstall through a monitoring hook — out of scope here since Software ▸ External/Windows Apps already performs uninstall separately; this milestone is post-hoc scanning, so snapshot-diffing isn't available and isn't needed — capture-then-verify (see Architecture notes below) is sufficient |
| `Directory.EnumerateFiles` + size-bucket-then-hash | Raw NTFS `$MFT` parsing (what WizTree/TreeSize Free use for near-instant full-volume scans) | Only if scan speed on multi-TB volumes becomes a measured user complaint; MFT parsing requires a raw volume handle, undocumented on-disk record parsing, and materially higher fragility/maintenance cost — disproportionate for a correctness-critical delete tool where a few extra seconds of enumeration is an acceptable tradeoff |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| Writing directly into `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-...}\NNNN` (what `network-apply.bat` does today) | The `NNNN` subkey is an install-order ordinal, **not stable per adapter** — it can renumber across driver reinstalls/reboots, which is exactly why the existing `.bat` has to dynamically `reg query /s` for a sibling value on every run instead of hardcoding an index. It also has no per-adapter targeting (writes to every subkey matching the probe value) and a raw registry write does **not** reload the NDIS driver binding — the value often only takes effect after a manual adapter disable/enable or reboot, which is very likely the source of the current script's flakiness | `Set-NetAdapterAdvancedProperty -Name <adapter> -RegistryKeyword <key> -RegistryValue <value>` — targets one named adapter, validates against the driver's declared value range, and triggers the proper reconfiguration path |
| Reusing `RegistrySetting.ApplyPerNetworkInterface` (existing `WindowsRegistryService` feature) for the new NIC tweak UI | This flag already exists and works correctly today, but it targets `SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces` (TCP/IP stack settings, subkeyed by stable adapter GUID) — a completely different registry namespace from the NDIS driver advanced-property tree this milestone needs. It also has no concept of "apply to this one selected adapter" (it always expands to every subkey) | Keep `ApplyPerNetworkInterface` exactly as-is for genuine Tcpip-interface settings (e.g. `gaming-nagle-algorithm`, unrelated to this milestone); build the new per-adapter/per-value NIC UI on the `NetAdapterAdvancedProperty` cmdlet surface instead |
| `Microsoft.Deployment.WindowsInstaller` / raw `msi.dll` P/Invoke (`MsiEnumProducts`, `MsiGetProductInfo`) for leftover detection | Adds a whole second detection subsystem (MSI ProductCode registration state) for a shrinking slice of apps — most consumer software today installs via NSIS/Inno/Squirrel/WinGet, not raw MSI. Disproportionate complexity for first-pass leftover scanning | Registry `...\Uninstall` key enumeration + captured `InstallLocation` existence check covers the overwhelming majority of cases; flag MSI-specific orphan detection as a possible future enhancement, not MVP |
| Blind filesystem-wide substring search for "leftover" folders (e.g. searching all of `C:\` for a string match on app name) | High false-positive risk — exactly what this milestone's safety constraint (no false positives, no default-select-all) is trying to avoid; a generic name like "Update" or "Data" will match unrelated vendor folders | Restrict heuristic search to a fixed, well-known set of leftover roots (`%APPDATA%`, `%LOCALAPPDATA%`, `%ProgramData%`, `%ProgramFiles%`, `%ProgramFiles(x86)%`, `HKCU/HKLM\Software\<Publisher>`, Start Menu shortcut folders) and match whole path segments case-insensitively against the sanitized app/publisher name — never raw substring; prioritize the exact captured `InstallLocation` path (direct evidence) over name-heuristic search (weak evidence) |
| `Directory.GetFiles(path, "*", SearchOption.AllDirectories)` for the System Cleaner scan | Materializes the entire file list into memory before returning anything, and the whole call throws (aborting the scan) on the first `UnauthorizedAccessException` it hits anywhere in the tree | `Directory.EnumerateFiles`/`FileSystemEnumerable` — streams results as it walks, and a custom `FileSystemEnumerable` subclass can override `ContinueOnError`/`ShouldRecurseIntoEntry` to skip access-denied directories and reparse points/junctions instead of aborting or double-counting |
| Cryptographic hashing (`SHA256`) as the *first-pass* filter for duplicate detection | Needlessly slow for a bulk pre-filter — you're not defending against adversarial hash collisions, you're comparing files a user already owns | Size-bucket first (files with a unique size in the whole scan can't have a duplicate — free elimination), then XxHash3/XxHash64 on a fixed-size prefix, then full-file hash only on the reduced candidate set; optionally a final byte-for-byte compare before offering delete, matching the "explicit user review before delete" constraint |

## Stack Patterns by Variant

**Deep-clean app uninstaller (leftover scan):**
- Capture `DisplayName`, `Publisher`, `InstallLocation`, `UninstallString`, and the registry key path itself from the app's `...\Uninstall` entry **before** the existing Software ▸ External/Windows Apps uninstall flow runs (this data still exists pre-uninstall even though the curated `AppDefinition` model doesn't store it today — the leftover scanner needs its own registry read, separate from the curated app catalog).
- After uninstall completes, verify: (1) does the captured `InstallLocation` still exist on disk — highest-confidence signal; (2) does the `Uninstall` registry key itself still exist (orphaned entry from a partially-failed uninstaller); (3) do `HKCU`/`HKLM Software\<Publisher>\<AppName>` keys matching the captured identity still exist (check both `RegistryView.Registry64` and `Registry32`); (4) do any scheduled tasks have an Action pointing at the now-missing `InstallLocation`.
- Before flagging anything, build an exclusion set from every *still-installed* app's current `InstallLocation`/`Publisher` (re-enumerate `...\Uninstall` live, not a stale pre-uninstall snapshot) so shared/common vendor folders are never flagged.
- Reference architecture: **Bulk Crap Uninstaller** (Klocman/Bulk-Crap-Uninstaller, open-source C#/.NET, Apache 2.0) implements this exact "JunkScanner" pattern — worth studying its detection heuristics, not worth depending on as a library.

**System Cleaner (junk/temp, large-file, duplicate-file):**
- One shared streaming enumeration pass (`Directory.EnumerateFiles`/`FileSystemEnumerable`) feeds all three sub-tools; skip `C:\Windows`, `$Recycle.Bin`, `System Volume Information`, and reparse-point directories by default.
- Large-file finder: aggregate sizes bottom-up per directory; parallelize across top-level subdirectories with a capped degree of parallelism (I/O-bound — don't default to `Environment.ProcessorCount` on spinning disks; 4–8 is a safer default, made configurable if worth it).
- Duplicate finder: size-bucket → prefix-hash (XxHash3 on first 4–64 KB) → full-file hash (XxHash3/XxHash64) → optional byte-for-byte confirm, only within reduced candidate sets, per the "what NOT to use" table above.
- All three are **read-only discovery** producing a review list; nothing deletes until the user explicitly selects items (already a hard project constraint, not a stack decision, but it shapes the API shape — return `IReadOnlyList<ScanResult>` from scan methods, take an explicit `IReadOnlyList<string> selectedPaths` in the delete method).

**Granular per-value, per-adapter NIC tweak UI:**
- Enumerate physical adapters with `Get-NetAdapter -Physical` (backed by `MSFT_NetAdapter.ConnectorPresent`/`HardwareInterface` — the modern, non-deprecated replacement for `Win32_NetworkAdapter`) via `IPowerShellRunner`, not manual registry Class-GUID subkey walking.
- Enumerate current advanced-property values with `Get-NetAdapterAdvancedProperty -Name <adapter>` — returns `DisplayName`, `RegistryKeyword` (the `*RSS`/`*InterruptModeration`/etc. names `network-apply.bat` already writes), `RegistryValue`, `ValidRegistryValues`/`ValidDisplayValues` (the driver's declared valid range — use this to build the per-value UI's option set instead of hardcoding assumptions).
- Write one value at a time with `Set-NetAdapterAdvancedProperty -Name <adapter> -RegistryKeyword <key> -RegistryValue <value>`; capture the pre-change `RegistryValue` at scan time so "revert this value" is a plain `Set` back to the captured value (real per-value revert), and/or call `Reset-NetAdapterAdvancedProperty -Name <adapter> -DisplayName <name>` to restore the driver's factory default specifically.
- Because these are officially validated, per-adapter, per-value operations, this directly satisfies the milestone's "replace all-or-nothing batch script with per-value toggles + per-adapter selection + real per-value revert" requirement without inventing a new registry-diffing/backup mechanism.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|------------------|-------|
| `System.IO.Hashing` 10.0.0+ | `net10.0-windows10.0.26100.0` (this project's TFM) | Multi-targets net8.0/net9.0/net10.0/netstandard2.0/net462 — no compatibility risk; pin the same major/minor as the repo's other `System.*` packages (`System.Management` 10.0.0) for consistency, bump patch versions together later if desired |
| `NetAdapter` PowerShell module | Windows 8.1/Server 2012 R2+ (built into Windows 10/11, no install) | Already satisfies this app's stated minimum (Windows 10 1909+); no PowerShell 7 dependency — runs fine under the PowerShell 5.1 the app already targets via `PowerShellRunner` |
| Task Scheduler 2.0 COM API (`Schedule.Service`) | Windows Vista+ | Unchanged surface for 15+ years; already proven working in this codebase's `ScheduledTaskService.cs` |

## Sources

- [Set-NetAdapterAdvancedProperty (NetAdapter) — Microsoft Learn](https://learn.microsoft.com/en-us/powershell/module/netadapter/set-netadapteradvancedproperty) — HIGH confidence, verified cmdlet parameters and behavior
- [Reset-NetAdapterAdvancedProperty (NetAdapter) — Microsoft Learn](https://learn.microsoft.com/en-us/powershell/module/netadapter/reset-netadapteradvancedproperty) — HIGH confidence, verified factory-default reset semantics
- [MSFT_NetAdapterAdvancedPropertySettingData class — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/fwp/wmi/netadaptercimprov/msft-netadapteradvancedpropertysettingdata) — HIGH confidence, confirms `root\StandardCimv2` CIM backing and property surface (`RegistryKeyword`, `RegistryValue`, `ValidRegistryValues`)
- [MSFT_NetAdapter class — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/fwp/wmi/netadaptercimprov/msft-netadapter) — HIGH confidence, confirms `ConnectorPresent`/`HardwareInterface` as the physical-adapter filter and that this supersedes the deprecated `Win32_NetworkAdapter`
- [System.IO.Hashing NuGet package page](https://www.nuget.org/packages/System.IO.Hashing) — HIGH confidence, verified latest stable version (10.0.11) and target framework list (net8.0+, netstandard2.0, net462)
- Bulk Crap Uninstaller (Klocman/Bulk-Crap-Uninstaller, GitHub, open-source C#/.NET) — MEDIUM confidence (referenced via search results describing its "JunkScanner" leftover-detection architecture, not independently line-read); used as architecture-pattern precedent, not a dependency
- This repo's own source, read directly — HIGH confidence: `src/AkariTool.App/Scripts/Network/network-apply.bat` (current all-or-nothing NIC script, confirms the exact `*`-prefixed registry keywords in use), `src/AkariTool.Infrastructure/Features/Common/Services/WindowsRegistryService.cs` and `src/AkariTool.Core/Features/Common/Models/RegistrySetting.cs` (confirms `ApplyPerNetworkInterface` targets `Tcpip\Parameters\Interfaces`, not the NDIS Class-GUID tree), `src/AkariTool.Infrastructure/Features/Common/Services/ScheduledTaskService.cs` (confirms the existing raw-COM `Schedule.Service` pattern to extend rather than replace), `src/AkariTool.Core/Features/Software/Catalogs/AppModels.cs` (confirms `AppDefinition` does not currently capture `InstallLocation`/`Publisher`, informing the leftover-scanner's own capture step)

---
*Stack research for: Windows desktop system-utility rework (uninstaller leftovers, disk cleanup, per-NIC registry tuning)*
*Researched: 2026-08-27*
