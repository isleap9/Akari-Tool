using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using AkariTool.Core.Interfaces;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Services;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Services;
using AkariTool.Infrastructure.Features.Optimize.Services;

namespace AkariTool.Infrastructure.DI;

/// <summary>
/// DI registrations for the Infrastructure layer: interface wrappers over the
/// static OS services (Update / ToolFetch / SystemInfo / ShaderCache).
/// NOTE: ToolService and TweakDialogs — although "infrastructure-ish" — live in
/// the main app project (ToolService is in the AkariTool.Services namespace and
/// TweakDialogs depends on WinUI/WinUI.Framework), which this project does not
/// reference. Their registrations stay in the main project (see AddAkariUI).
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddAkariInfrastructure(
        this IServiceCollection services)
    {
        // Interface wrappers over the static services (Infrastructure).
        services.AddSingleton<IUpdateService, UpdateServiceWrapper>();
        services.AddSingleton<IToolFetchService, ToolFetchServiceWrapper>();
        services.AddSingleton<ISystemInfoService, SystemInfoServiceWrapper>();
        services.AddSingleton<IShaderCacheService, ShaderCacheServiceWrapper>();

        // Special-setting handlers (composite apply/detect paths the generic
        // executor cannot express — e.g. the Windows Update policy dropdown).
        services.AddSingleton<WindowsUpdatePolicyHandler>();
        services.AddSingleton<ISpecialSettingHandlerRegistry>(sp => new SpecialSettingHandlerRegistry(
            new Dictionary<string, ISpecialSettingHandler>
            {
                ["updates-policy-mode"] = sp.GetRequiredService<WindowsUpdatePolicyHandler>()
            }));

        // Declarative SettingDefinition stack (Track A).
        services.AddSingleton<ISettingStateReader, SettingStateReader>();
        services.AddSingleton<ISettingOperationExecutor, SettingOperationExecutor>();
        services.AddSingleton<ISettingDependencyResolver, SettingDependencyResolver>();

        // SettingOperationExecutor's dependencies — all fully implemented (the write
        // path has been live since Track A Phase 2; FileSystemService was the last
        // throwing stub, implemented during the 4h-era ComboBox detection fix).
        services.AddSingleton<IWindowsRegistryService, WindowsRegistryService>();
        services.AddSingleton<IComboBoxResolver, ComboBoxResolver>();
        services.AddSingleton<IProcessRestartManager, ProcessRestartManager>();
        services.AddSingleton<IPowerCfgApplier, PowerCfgApplier>();
        services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<IPowerShellRunner, PowerShellRunner>();
        services.AddSingleton<IFileSystemService, FileSystemService>();

        // Power (Track A Power tab — Winhance 1:1 port). All implementations live.
        services.AddSingleton<IPowerSettingsQueryService, PowerSettingsQueryService>();
        services.AddSingleton<IPowerSchemeOperations, PowerSchemeOperations>();
        services.AddSingleton<IPowerPlanComboBoxService, PowerPlanComboBoxService>();
        services.AddSingleton<IHardwareDetectionService, HardwareDetectionService>();
        services.AddSingleton<IPowerSettingsValidationService, PowerSettingsValidationService>();
        services.AddSingleton<IPowerService, PowerService>();

        // System backup (4g — Winhance SystemBackupService 1:1 port): restore point
        // creation with native SrClient API, verification retries, shadow-storage checks.
        services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
        services.AddSingleton<ISystemBackupService, SystemBackupService>();

        // Compatibility gating pipeline (4h — Winhance 1:1 port): Windows-version +
        // hardware filters applied to every declarative page catalog at Build time
        // (filtered mode — incompatible rows are removed).
        services.AddSingleton<IWindowsVersionService, WindowsVersionService>();
        services.AddSingleton<IWindowsCompatibilityFilter, WindowsCompatibilityFilter>();
        services.AddSingleton<IHardwareCompatibilityFilter, HardwareCompatibilityFilter>();

        // Priority 4d: Technical Details / Tooltip infrastructure
        services.AddSingleton<IRegeditLauncher, RegeditLauncher>();
        services.AddSingleton<ITooltipDataService, TooltipDataService>();

        // Winhance 1:1 port for TechnicalDetailsManager dependencies
        services.AddSingleton<IDispatcherService, DispatcherService>();

        return services;
    }
}