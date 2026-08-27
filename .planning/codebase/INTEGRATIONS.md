# External Integrations

**Analysis Date:** 2026-08-27

## APIs & External Services

**GitHub Releases (Update Distribution):**
- Service: GitHub API v3 (`https://api.github.com/repos/isleap9/Akari-Tool`)
  - SDK/Client: `System.Net.Http.HttpClient` (built-in)
  - Purpose: Version checking (`CheckAsync`), release notes retrieval, installer download
  - Endpoints:
    - `GET /repos/isleap9/Akari-Tool/releases/latest` — Latest release + asset download URL
    - `GET /repos/isleap9/Akari-Tool/releases?per_page=10` — Release history for changelog
  - Auth: None (unauthenticated, 60 req/hr per IP limit)
  - User-Agent: `AkariTool/{CurrentVersion}` (custom header)
  - Timeout: 5 minutes (for large installer downloads)
  - Implementation: `src/AkariTool.Infrastructure/Services/UpdateService.cs`

**jsDelivr CDN (Icon Delivery):**
- Service: https://cdn.jsdelivr.net/ (GitHub CDN)
  - SDK/Client: `System.Net.Http.HttpClient` (built-in)
  - Purpose: App icons for Software tab (cached locally after first download)
  - Endpoints:
    - `https://cdn.jsdelivr.net/gh/isleap9/package-icons@main/` (base URL for icon repo)
  - Auth: None (public CDN)
  - Cache: `%ProgramData%\AkariTool\IconCache\` (persistent across sessions; missing icons cached in-memory for session)
  - Max size: 10 MB per icon
  - Parallel downloads: 6 concurrent (semaphore throttle)
  - Implementation: `src/AkariTool.App/Features/Software/AppIconService.cs`

**Raw GitHub Content (Autounattend Templates):**
- Service: https://raw.githubusercontent.com/isleap9/Akari-Tool-Autounattend/main/autounattend.xml
  - Purpose: Fetch Windows autounattend.xml template for AkariOS post-install scripts
  - Auth: None (public repo)
  - Usage: `AutounattendService.cs` loads template, injects user preferences
  - Implementation: `src/AkariTool.App/Services/AutounattendService.cs`

## Data Storage

**Databases:**
- Not detected (no SQL/relational database used)

**File Storage:**
- Local filesystem only
  - **Configuration:** `%LOCALAPPDATA%\WinUI.Framework\` — App theme (`AppTheme` key), UI preferences (JSON format via `ISettingsService`)
  - **Icon Cache:** `%ProgramData%\AkariTool\IconCache\` — Downloaded app icons (bitmap cache, named by app ID)
  - **Logs:** `%ProgramData%\AkariTool\Logs\` — Execution logs from tweaks, PowerShell scripts, AutoUnattend system phase
  - **Scripts:** `%ProgramData%\AkariTool\Scripts\` — Generated PowerShell scripts (BloatRemoval.ps1, merged on refresh)
  - **Backup/Import:** User-selected JSON files (legacy TweakRegistry format, re-ported to SettingBackupService)

**Caching:**
- In-process session cache (AppIconService)
  - `Dictionary<string, BitmapImage> Loaded` — Downloaded and decoded icons
  - `HashSet<string> Missing` — Icons confirmed absent on CDN (negative cache to avoid repeated 404 queries)

## Authentication & Identity

**Auth Provider:**
- Custom / Windows Built-in
  - Windows credentials required (UAC `requireAdministrator` manifest forces elevation at launch)
  - No remote authentication — all access is local administrator
  - Account detection via WMI (`AccountService.cs`) for system info display only

**GitHub OAuth:**
- Not detected (no token/authentication used for GitHub API; unauthenticated rate limit sufficient for update checks)

## Monitoring & Observability

**Error Tracking:**
- Not detected (no external error tracking service like Sentry/AppCenter)

**Logs:**
- Local file logging only
  - Framework: `ILogService` interface (WinUI.Framework)
  - File path: `%ProgramData%\AkariTool\Logs\AkariTool.log`
  - Implementation: `FileLogService` (WinUI.Framework) + `AkariUiLogService` (App wrapper for UI events)
  - Format: Plain text line-by-line
  - Rotation: None (single file, no size limit enforced)
  - Log levels: Debug, Info, Warning, Error
  - Usage: PowerShell script execution, service operations, UI actions logged via `ToolService.Log()`

## CI/CD & Deployment

**Hosting:**
- GitHub Releases (installer distribution)
  - Binary: `AkariTool-Setup-{version}.exe` (MSIX installer)
  - Published via `build-installer.ps1` and GitHub Actions (inferred from release flow)

**CI Pipeline:**
- Not detected in source (no `.github/workflows`, no CI config files)
- Build command (local): `msbuild AkariTool.sln /t:Build /p:Configuration=Debug /p:Platform=x64`
- Publish command (manual): `build-installer.ps1` (PowerShell script, generates MSIX installer for GitHub Releases)
- Installer script: `build-installer.ps1` packages self-contained runtime + app EXE
- De-elevated test script: `build-deelevated.ps1` (for asInvoker UAC level testing)

## Environment Configuration

**Required env vars:**
- None explicitly checked in code (Windows system drive assumed as default `C:`)
- `SystemDrive` queried (fallback to `C:` if absent)
- `PATH` used to locate WinGet CLI

**Secrets location:**
- No secrets stored in code or config files
- Windows Registry used for configuration storage (registry ACLs provide access control)
- DefenderService embeds cab + PowerShell scripts as encrypted resources in executable
- No .env files or credential files present in repo

## Webhooks & Callbacks

**Incoming:**
- None detected

**Outgoing:**
- None detected (app is unidirectional — pulls updates from GitHub, does not post back)

## Windows System Integrations

**Registry:**
- Root: `HKLM\SOFTWARE\AkariTool` (defined in CLAUDE.md)
- Primary read/write client: `WindowsRegistryService` (`src/AkariTool.Infrastructure/Features/Common/Services/`)
- Operations: REG_DWORD, REG_SZ, REG_BINARY bit-mask/byte manipulation for game settings, power profiles, privacy toggles, accessibility flags
- Scope: User (HKCU) and Machine (HKLM) hives

**Scheduled Tasks:**
- Read/write via COM (TaskScheduler)
- Operations: Enable/disable Windows system tasks (maintenance, updates, telemetry)
- Implementation: `ScheduledTaskService` (`src/AkariTool.Infrastructure/Features/Common/Services/ScheduledTaskService.cs`)

**Windows Management Instrumentation (WMI):**
- WMI Class Queries:
  - `Win32_PhysicalMemory` — RAM capacity, type, speed
  - `Win32_DiskDrive` — Disk model, size, media type
  - `Win32_VideoController` — GPU detection, PNP device ID (NVIDIA/AMD/Intel drivers)
  - `Win32_SystemRestore` — Restore point creation and status
  - `Win32_ComputerSystem` — Battery status (for power plan gating)
  - `Win32_UserAccount` — Current user account info
- Implementation: `SystemInfoService`, `RestorePointHelper`, `GpuTweaks`, `SystemBackupService` (all in Infrastructure)

**Power Management:**
- `powercfg.exe` CLI wrapper (via PowerShell)
  - Operations: Query power schemes, modify power plan settings (AC/DC groups and subgroups)
  - Execution: PowerShell required (MSYS bash mangles `/QUERY` syntax)
  - Implementation: `PowerCfgApplier` (`src/AkariTool.Infrastructure/Features/Common/Services/PowerCfgApplier.cs`)
- Native Power API:
  - `SetPowerPlan()` (Win32 DLL calls via P/Invoke, via CsWin32)
  - Battery detection via `GetSystemPowerStatus()` (to gate battery-only tweaks)

**Service Control:**
- ServiceController API (System.ServiceProcess)
  - Operations: Start, stop, query status of Windows services
  - Used for: Service presets, Competitive Mode resource suspension
  - Implementation: Scattered across `CompetitiveService`, `ElevationService`, `ProcessRestartManager`

**PowerShell Execution:**
- Host: Windows PowerShell 5.1 (not PowerShell Core)
- Embedded scripts executed via `Get-Content -Path (resolved from embedded resource)` + `Invoke-Expression`
- Implementation: `PowerShellRunner` (`src/AkariTool.Infrastructure/Features/Common/Services/PowerShellRunner.cs`)
- Script types:
  - Registry tweaks (PowerShell + .reg files)
  - Feature enable/disable (DISM, Remove-WindowsCapability)
  - Software uninstall (WinGet CLI, MSI, custom scripts)
  - System reboot scheduling
  - Playbook tweaks (AkariOS-specific)

**Windows Package Manager (WinGet):**
- Access: Windows Package Manager COM API (via `WinGet.Interop` C# projection)
- Operations:
  - Bootstrap WinGet (ensure availability on Windows 10)
  - Query installed packages
  - Install/uninstall external apps
- Implementation: `WingetDetectionService`, `SoftwareAppService` (in Infrastructure/App)
- Scope: User-level + Machine-level package enumeration

**Steam Integration:**
- Local filesystem scan (no Steam API)
- Purpose: Shader cache cleaning, game detection
- Paths: `%ProgramFiles(x86)%\Steam\steamapps\`, per-game cache dirs
- Implementation: `ShaderCacheService` (in Infrastructure)
- Feature: Auto-detect Steam installation + library paths (user-created subdirectories)

**NVIDIA Driver Control:**
- NVIDIA Settings Profile (`.nip` file)
  - Embedded in executable: `src/AkariTool.App/Nvidia/Settings.nip`
  - Injected into user's NVIDIA profile at runtime (if NVIDIA drivers detected)
- Implementation: `NvidiaProfileService` (in Infrastructure)

**System Restore Points:**
- WMI interface: `SystemRestore` class (enable/disable, query max size)
- Native API: `SRSetRestorePointW()` (P/Invoke via CsWin32)
- Vssadmin.exe: Query shadow storage, resize allocation (20 GB default, clamped 1–64 GB)
- Implementation: `SystemBackupService` + `RestorePointHelper` (in Infrastructure)

**DirectX Shader Cache:**
- Local paths: `%LOCALAPPDATA%\D3DSCache\`, per-GPU driver vendor caches
- Cleanup: Direct filesystem deletion (safe, rebuilt on next DirectX app launch)
- Implementation: `ShaderCacheService`

---

*Integration audit: 2026-08-27*
