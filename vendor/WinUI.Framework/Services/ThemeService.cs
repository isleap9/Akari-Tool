using Microsoft.UI.Xaml;

namespace WinUI.Framework.Services;

/// <summary>
/// <see cref="IThemeService"/> that applies the requested theme to a root
/// element and persists the choice via <see cref="ISettingsService"/>.
/// </summary>
public class ThemeService : IThemeService
{
    private const string ThemeSettingKey = "AppTheme";

    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
    }

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Default;

    public FrameworkElement? RootElement { get; set; }

    public event EventHandler<AppTheme>? ThemeChanged;

    public void Initialize()
    {
        CurrentTheme = _settings.Get(ThemeSettingKey, AppTheme.Default);
        ApplyToRoot(CurrentTheme);
    }

    public void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        _settings.Set(ThemeSettingKey, theme);
        ApplyToRoot(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    private void ApplyToRoot(AppTheme theme)
    {
        if (RootElement is null)
        {
            return;
        }

        RootElement.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
