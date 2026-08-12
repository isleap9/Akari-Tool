using Microsoft.Extensions.DependencyInjection;

namespace WinUI.Framework.IoC;

/// <summary>
/// Minimal service locator used by pages (which are created by the Frame and
/// therefore cannot use constructor injection) to resolve their view models and
/// services. Initialize it once with the app's service provider at startup.
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    /// <summary>The registered service provider.</summary>
    public static IServiceProvider Provider
        => _provider ?? throw new InvalidOperationException("The ServiceLocator has not been initialized yet. Call Initialize(IServiceProvider) at app startup.");

    /// <summary>Registers the app-wide service provider. Call once at startup.</summary>
    public static void Initialize(IServiceProvider provider) => _provider = provider;

    /// <summary>Resolves a service from the container; throws if it is not registered.</summary>
    public static T GetService<T>() where T : notnull => Provider.GetRequiredService<T>();

    /// <summary>Resolves a service from the container; returns <c>null</c> if it is not registered.</summary>
    public static T? GetOptionalService<T>() where T : class => Provider.GetService<T>();
}
