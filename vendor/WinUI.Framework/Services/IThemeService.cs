using Microsoft.UI.Xaml;

namespace WinUI.Framework.Services;

/// <summary>Application theme options.</summary>
public enum AppTheme
{
    /// <summary>Follow the system setting.</summary>
    Default,

    /// <summary>Force light theme.</summary>
    Light,

    /// <summary>Force dark theme.</summary>
    Dark,
}

/// <summary>
/// Applies an <see cref="AppTheme"/> to the root element, persists the choice,
/// and notifies listeners of changes.
/// </summary>
public interface IThemeService
{
    /// <summary>The currently applied theme.</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>
    /// The root element the theme is applied to (usually the window content).
    /// Set it once at startup before calling <see cref="Initialize"/>.
    /// </summary>
    FrameworkElement? RootElement { get; set; }

    /// <summary>Raised after the theme has been applied.</summary>
    event EventHandler<AppTheme>? ThemeChanged;

    /// <summary>Reads the persisted theme and applies it to the root element without re-saving.</summary>
    void Initialize();

    /// <summary>Applies, persists, and raises <see cref="ThemeChanged"/> for the given theme.</summary>
    void ApplyTheme(AppTheme theme);
}
