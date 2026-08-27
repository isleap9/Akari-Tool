# Technology Stack

**Analysis Date:** 2026-08-27

## Languages

**Primary:**
- C# 12 (latest) - All production code, split across Core (models/interfaces), Infrastructure (OS-touching services), and App (WinUI 3 UI layer)

**Secondary:**
- PowerShell 5.1 - Embedded scripts for system tweaks, feature toggles, and uninstall operations (executed via `PowerShellRunner` in `AkariTool.Infrastructure`)
- Batch (.bat) - Network configuration scripts in `src/AkariTool.App/Scripts/Network/`
- XML - XAML UI definitions, WinUI 3 control templates, and autounattend.xml generation

## Runtime

**Environment:**
- .NET 10.0 (LTS)
- Target: Windows 10 (OS Version 10.0.26100.0)
- Minimum: Windows 10 (10.0.17763.0 for main App; WinGet.Interop requires 10.0.22621.0)
- Platform: x64 only (`win-x64` RuntimeIdentifier)

**Package Manager:**
- NuGet
- Lockfile: `obj/project.assets.json` (MSBuild-generated, not checked in)

## Frameworks

**Core UI:**
- Windows App SDK 2.3.1 - WinUI 3 desktop application framework; self-contained deployment (runtime ships with executable)
- WinUI.Framework (local vendored) - Custom MVVM framework (`vendor/WinUI.Framework/`), providing `IThemeService`, `ISettingsService`, `IDispatcherService`, `ITaskProgressService`, base VM/View patterns

**Application Pattern:**
- CommunityToolkit.Mvvm 8.4.2 - MVVM source generators (`RelayCommand`, `ObservableProperty`), used across App and Core ViewModels

**Dependency Injection:**
- Microsoft.Extensions.DependencyInjection 10.0.10 - Service registration and resolution in `App.xaml.cs` and extension methods (`InfrastructureServiceExtensions.cs`, `UIServiceExtensions.cs`)

**Testing:**
- xunit 2.9.3 - Test runner and assertions
- FluentAssertions 8.7.1 - Fluent assertion syntax
- NSubstitute 5.3.0 - Mock/substitution framework for service isolation
- Microsoft.NET.Test.Sdk 17.14.1 - Test discovery and execution

**Build/Dev:**
- MSBuild (Visual Studio 2022 Community via hardcoded path `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`)
- CsWinRT 2.0.4 - C# projection generator for WinRT APIs (WinGet.Interop COM projection)
- CsWin32 0.3.49-beta - P/Invoke stub generator for Win32 APIs

## Key Dependencies

**Critical:**
- Microsoft.WindowsAppSDK 2.3.1 - All WinUI 3 controls, Window management, dispatcher, theming; enables self-contained deployment
- CommunityToolkit.Mvvm 8.4.2 - Eliminates boilerplate in property/command definitions across entire MVVM codebase
- Microsoft.Extensions.DependencyInjection 10.0.10 - Service resolution at startup and runtime (SettingOperationExecutor, SettingStateReader, power/update services)

**System Integration:**
- System.Management 10.0.0 - WMI queries for hardware detection (RAM, disk, GPU, battery status, account info)
- System.ServiceProcess.ServiceController 10.0.0 - Windows service enumeration and control (for service presets, Competitive Mode, system resource tuning)
- Microsoft.WindowsPackageManager.ComInterop 1.9.25180 - COM interop for Windows Package Manager (WinGet) detection and installation

**UI Components:**
- Material.Icons.WinUI3 3.0.2 - Material Design icon set, resolved by `IconConverter` from row definitions
- FluentIcons.WinUI 2.1.326 - Fluent Design icon set (same versions as Winhance upstream for parity)

**Build-Time Only:**
- Microsoft.Windows.CsWinRT 2.0.4 - XAML compilation targets, resource PRI generation for self-contained deployment

## Configuration

**Environment:**
- No `.env` files or secrets management — all configuration is in-code (CLAUDE.md defines registry root `HKLM\SOFTWARE\AkariTool`, theme/settings persist via Windows Registry and `%LOCALAPPDATA%`)
- Required elevation: App manifest declares `requireAdministrator` (Windows 10+ built-in UAC prompt)
- Storage root: `%ProgramData%\AkariTool\` (Scripts, Logs, IconCache directories created on first use)

**Build:**
- `AkariTool.sln` - Solution file with 7 projects (3 main, 2 test, 2 vendor)
- `src/AkariTool.App/AkariTool.App.csproj` - WinUI 3 executable; embeds PowerShell scripts, DefenderService cab/ps1, NVIDIA NIP profiles
- `src/AkariTool.Core/AkariTool.Core.csproj` - Pure C# models (zero OS dependencies, zero UI dependencies)
- `src/AkariTool.Infrastructure/AkariTool.Infrastructure.csproj` - OS-touching services (registry, WMI, PowerShell, tasks, elevation)
- `tests/AkariTool.Core.Tests/AkariTool.Core.Tests.csproj` - 53 passing tests
- `tests/AkariTool.Infrastructure.Tests/AkariTool.Infrastructure.Tests.csproj` - 136 passing + 1 skipped tests
- `vendor/WinUI.Framework/WinUI.Framework.csproj` - Local framework (ProjectReference, not NuGet)
- `vendor/WinGet.Interop/WinGet.Interop.csproj` - WinGet COM projection (ProjectReference)

**Project Properties:**
- LangVersion: `latest` (C# 12.0)
- Nullable: `enable` (strict null-reference safety)
- ImplicitUsings: `enable` (top-level `using` directives auto-included)
- RuntimeIdentifier: `win-x64` (x64-only builds)
- TargetFramework: `net10.0-windows10.0.26100.0` (tied to Windows SDK version 26100)

## Platform Requirements

**Development:**
- Windows 10 or later (10.0.17763.0+ for app, 10.0.22621.0+ for WinGet features)
- Visual Studio 2022 Community or later (for MSBuild, XAML designer, WinUI workload)
- .NET 10 SDK (installed automatically with VS workload)
- Administrator privileges (to run tests and debug app)

**Production:**
- Windows 10 (1909 or later minimum, though optimized for Windows 11)
- No additional runtime installation required (Windows App SDK runtime bundled in executable)
- Administrator privileges required at launch (UAC prompt on startup)
- At least 100 MB free disk space (for extracted runtime and cache)

**Optional Runtime Features:**
- PowerShell 5.1 (built-in since Windows 10; PowerShell 7+ not required)
- WinGet (Windows Package Manager, bundled in Windows 11+; backported in GitHub repo for Windows 10)
- Steam (for shader cache cleaning, auto-detected at runtime)

---

*Stack analysis: 2026-08-27*
