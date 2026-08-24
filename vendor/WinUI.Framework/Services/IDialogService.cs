using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUI.Framework.Services;

/// <summary>
/// Displays <see cref="ContentDialog"/> based message boxes, dialogs and
/// confirmations on behalf of the app.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// The <see cref="XamlRoot"/> used to show dialogs. Set it once at startup
    /// (for example to <c>window.Content.XamlRoot</c>).
    /// </summary>
    XamlRoot? XamlRoot { get; set; }

    /// <summary>Shows a simple dialog with a primary and an optional close button.</summary>
    Task<ContentDialogResult> ShowAsync(string title, object content, string primaryButtonText = "OK", string closeButtonText = "Cancel");

    /// <summary>Shows a Yes/No style confirmation; returns <c>true</c> when confirmed.</summary>
    Task<bool> ConfirmAsync(string title, object content, string confirmText = "Yes", string cancelText = "No");

    /// <summary>Shows a single-button info dialog.</summary>
    Task ShowInfoAsync(string title, object content, string closeText = "OK");
}
