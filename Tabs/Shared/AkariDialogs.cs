using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Async confirmation/info dialogs over WinUI <see cref="ContentDialog"/>.
    ///
    /// MIGRATION NOTE: the WPF version wrapped Wpf.Ui MessageBox in *synchronous*
    /// helpers via a Dispatcher.PushFrame nested pump. WinUI has no nested message
    /// pump and ContentDialog is async-only, so the sync API
    /// (ConfirmOkCancel / ConfirmYesNo / Info) is now
    /// ConfirmOkCancelAsync / ConfirmYesNoAsync / InfoAsync, and every call site is
    /// awaited. Call sites are converted batch-by-batch as tabs migrate.
    ///
    /// MainWindow must assign <see cref="XamlRoot"/> once its content is loaded —
    /// ContentDialog cannot show without it.
    /// </summary>
    public static class AkariDialogs
    {
        /// <summary>Set by MainWindow after its content loads; required to show dialogs.</summary>
        public static XamlRoot? XamlRoot { get; set; }

        // WinUI allows only ONE open ContentDialog per thread — serialize them.
        private static readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>OK / Cancel confirmation. True when the user pressed OK.</summary>
        public static Task<bool> ConfirmOkCancelAsync(string message, string title) =>
            ShowAsync(message, title, primaryText: "OK", closeText: "Cancel");

        /// <summary>Yes / No confirmation. True when the user pressed Yes.</summary>
        public static Task<bool> ConfirmYesNoAsync(string message, string title) =>
            ShowAsync(message, title, primaryText: "Yes", closeText: "No");

        /// <summary>Information box with a single OK button.</summary>
        public static Task InfoAsync(string message, string title) =>
            ShowAsync(message, title, primaryText: null, closeText: "OK");

        private static async Task<bool> ShowAsync(
            string message, string title, string? primaryText, string closeText)
        {
            var root = XamlRoot;
            if (root is null)
            {
                // No visual tree yet (headless path / very early startup): treat a
                // confirmation as declined rather than silently proceeding.
                System.Diagnostics.Debug.WriteLine("[AkariDialogs] No XamlRoot — dialog suppressed: " + title);
                return false;
            }

            await _gate.WaitAsync();
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 440,
                    },
                    CloseButtonText = closeText,
                    XamlRoot = root,
                };
                if (primaryText is not null)
                {
                    dialog.PrimaryButtonText = primaryText;
                    dialog.DefaultButton = ContentDialogButton.Primary;
                }

                var result = await dialog.ShowAsync();
                return result == ContentDialogResult.Primary;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Confirmation with arbitrary WinUI content (used by bulk-apply flows).
        /// True when the user pressed the primary button.
        /// </summary>
        public static async Task<bool> ConfirmContentAsync(
            object content, string title, string primaryText, string closeText = "Cancel")
        {
            var root = XamlRoot;
            if (root is null) return false;

            await _gate.WaitAsync();
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = content,
                    PrimaryButtonText = primaryText,
                    CloseButtonText = closeText,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = root,
                };
                return await dialog.ShowAsync() == ContentDialogResult.Primary;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
