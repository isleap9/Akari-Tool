using Microsoft.Extensions.DependencyInjection;
using AkariTool.Core.Interfaces;
using AkariTool.Infrastructure.Services;

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

        return services;
    }
}
