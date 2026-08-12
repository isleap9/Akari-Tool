using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUI.Framework.Services;

/// <summary>
/// <see cref="IDialogService"/> implementation using <see cref="ContentDialog"/>.
/// </summary>
public class DialogService : IDialogService
{
    public XamlRoot? XamlRoot { get; set; }

    public async Task<ContentDialogResult> ShowAsync(
        string title,
        object content,
        string primaryButtonText = "OK",
        string closeButtonText = "Cancel")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            XamlRoot = GetXamlRoot(),
        };

        return await dialog.ShowAsync();
    }

    public Task<bool> ConfirmAsync(string title, object content, string confirmText = "Yes", string cancelText = "No")
        => ConfirmCoreAsync(title, content, confirmText, cancelText);

    public async Task ShowInfoAsync(string title, object content, string closeText = "OK")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = closeText,
            XamlRoot = GetXamlRoot(),
        };

        await dialog.ShowAsync();
    }

    private async Task<bool> ConfirmCoreAsync(string title, object content, string confirmText, string cancelText)
    {
        var result = await ShowAsync(title, content, confirmText, cancelText);
        return result == ContentDialogResult.Primary;
    }

    private XamlRoot GetXamlRoot()
        => XamlRoot ?? throw new InvalidOperationException(
            "No XamlRoot is set on the dialog service. Assign the XamlRoot property once at startup.");
}
