using Microsoft.Extensions.DependencyInjection;
using AkariTool.Core.Interfaces;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Infrastructure.Services;
using AkariTool.Infrastructure.Features.Common.Interfaces;
using AkariTool.Infrastructure.Features.Common.Services;

namespace AkariTool.Infrastructure.DI;

/// <summary>
/// DI registrations for the Infrastructure layer: interface wrappers over the
/// static OS services (Update / ToolFetch / SystemInfo / ShaderCache).
///
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

        // Declarative SettingDefinition stack (Track A).
        services.AddSingleton<ISettingStateReader, SettingStateReader>();
        services.AddSingleton<ISettingOperationExecutor, SettingOperationExecutor>();

        // SettingOperationExecutor's nine dependencies. These are STUB implementations
        // (every method throws NotImplementedException) — they satisfy DI resolution so
        // the declarative pages construct, but the apply path is not live yet (Track A
        // Phase 2 follow-up). The read/badge path uses SettingStateReader, not these.
        services.AddSingleton<IWindowsRegistryService, WindowsRegistryService>();
        services.AddSingleton<IComboBoxResolver, ComboBoxResolver>();
        services.AddSingleton<IProcessRestartManager, ProcessRestartManager>();
        services.AddSingleton<IPowerCfgApplier, PowerCfgApplier>();
        services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
        services.AddSingleton<IProcessExecutor, ProcessExecutor>();
        services.AddSingleton<IPowerShellRunner, PowerShellRunner>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IAkariLogService, AkariLogService>();

        return services;
    }
}
