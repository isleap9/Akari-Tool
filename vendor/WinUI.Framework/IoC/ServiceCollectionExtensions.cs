using Microsoft.Extensions.DependencyInjection;
using WinUI.Framework.Navigation;
using WinUI.Framework.Services;

namespace WinUI.Framework.IoC;

/// <summary>
/// Extension methods for registering the framework's core services with
/// <see cref="Microsoft.Extensions.DependencyInjection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the framework's core services: settings, theming, dialogs,
    /// navigation, logging, file pickers, info notifications, and localization.
    /// </summary>
    public static IServiceCollection AddWinUIFrameworkCore(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, FrameNavigationService>();
        services.AddSingleton<ILogService, FileLogService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IInfoBarService, InfoBarService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        return services;
    }
}
