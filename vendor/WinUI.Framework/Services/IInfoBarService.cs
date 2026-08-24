using Microsoft.UI.Xaml.Controls;

namespace WinUI.Framework.Services;

/// <summary>
/// Shows transient messages in a single, app-wide <see cref="InfoBar"/>
/// (typically hosted in the window shell). A good replacement for repeated
/// <see cref="ContentDialog"/> popups.
/// </summary>
public interface IInfoBarService
{
    /// <summary>The InfoBar the service controls. Attach once at startup.</summary>
    InfoBar? InfoBar { get; set; }

    /// <summary>Shows a message with an optional action button.</summary>
    void Show(
        string title,
        string? message = null,
        InfoBarSeverity severity = InfoBarSeverity.Informational,
        bool isPersistent = false,
        string? actionText = null,
        Action? action = null);

    /// <summary>Hides the currently shown message.</summary>
    void Hide();
}
