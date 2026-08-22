using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.Framework.Services;

namespace AkariTool.Services
{
    /// <summary>
    /// Thin wrapper over the framework <see cref="IDialogService"/> that serializes
    /// dialogs behind a semaphore.
    ///
    /// WHY: WinUI allows exactly ONE ContentDialog open at a time — a second
    /// ShowAsync while one is up throws. The net8 build's AkariDialogs solved this
    /// the same way. The framework DialogService has no such guard, and the tweak
    /// surface can genuinely queue dialogs (a warned row confirm racing a bulk
    /// confirm, or an import refresh landing mid-dialog).
    ///
    /// Also centralises the "no XamlRoot yet" case: the framework service THROWS
    /// when XamlRoot is unset, which would turn a cosmetic startup race into a
    /// crash mid-apply. Here an unavailable dialog is treated as DECLINED — the
    /// same fail-safe direction the net8 build chose (never apply unconfirmed).
    /// </summary>
    public sealed class TweakDialogs
    {
        private readonly IDialogService _dialogs;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public TweakDialogs(IDialogService dialogs) => _dialogs = dialogs;

        /// <summary>OK/Cancel confirmation. True = proceed.</summary>
        public async Task<bool> ConfirmAsync(string title, object content, string primaryText = "OK")
        {
            if (_dialogs.XamlRoot is null) return false;   // fail safe: treat as declined
            await _gate.WaitAsync();
            try { return await _dialogs.ConfirmAsync(title, content, primaryText, "Cancel"); }
            catch { return false; }
            finally { _gate.Release(); }
        }

    /// <summary>
    /// Yes/No confirmation. True = proceed. Exists because the Software tab's
    /// destructive prompts are Yes/No in net8 (AkariDialogs.ConfirmYesNoAsync), not
    /// OK/Cancel — the button pair is part of the copy being ported verbatim.
    /// </summary>
    public async Task<bool> ConfirmYesNoAsync(string title, object content)
    {
        if (_dialogs.XamlRoot is null) return false;   // fail safe: treat as declined
        await _gate.WaitAsync();
        try { return await _dialogs.ConfirmAsync(title, content, "Yes", "No"); }
        catch { return false; }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Confirmation with explicit primary AND secondary button text (the first-launch
    /// restore offer's "Create Restore Point" / "Skip" pair — the secondary button is
    /// not Cancel). True = primary pressed.
    /// </summary>
    public async Task<bool> ConfirmWithButtonsAsync(
        string title, object content, string primaryText, string secondaryText)
    {
        if (_dialogs.XamlRoot is null) return false;   // fail safe: treat as declined
        await _gate.WaitAsync();
        try { return await _dialogs.ConfirmAsync(title, content, primaryText, secondaryText); }
        catch { return false; }
        finally { _gate.Release(); }
    }

        /// <summary>Single-button informational dialog.</summary>
        public async Task InfoAsync(string title, object content)
        {
            if (_dialogs.XamlRoot is null) return;
            await _gate.WaitAsync();
            try { await _dialogs.ShowInfoAsync(title, content); }
            catch { /* informational only */ }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Confirmation with arbitrary XAML content (the bulk dialog's warning
        /// callout + "create a restore point" checkbox). True = primary pressed.
        /// </summary>
        public async Task<bool> ConfirmContentAsync(string title, object content, string primaryText)
        {
            if (_dialogs.XamlRoot is null) return false;
            await _gate.WaitAsync();
            try
            {
                var result = await _dialogs.ShowAsync(title, content, primaryText, "Cancel");
                return result == ContentDialogResult.Primary;
            }
            catch { return false; }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Shows a warned tweak's confirmation. Returns true when there is no
        /// warning (nothing to confirm) or the user confirmed.
        /// </summary>
        public Task<bool> ConfirmWarningAsync(string tweakName, string? warning)
            => string.IsNullOrEmpty(warning) ? Task.FromResult(true) : ConfirmAsync(tweakName, warning);

        /// <summary>
        /// Confirmation with a checkbox below the content (Winhance's
        /// ShowConfirmationAsync shape: message + "don't show again" + custom
        /// primary button). Returns the button result and the checkbox state —
        /// the CheckBox stays referenced after dismissal, so its IsChecked is
        /// readable once the dialog closes.
        /// </summary>
        public async Task<(bool Confirmed, bool CheckboxChecked)> ConfirmWithCheckboxAsync(
            string title, UIElement content, string checkboxText, string primaryText)
        {
            if (_dialogs.XamlRoot is null) return (false, false);   // fail safe: treat as declined
            await _gate.WaitAsync();
            try
            {
                var panel = new StackPanel { Spacing = 12 };
                panel.Children.Add(content);
                var checkbox = new CheckBox { Content = checkboxText };
                panel.Children.Add(checkbox);

                var result = await _dialogs.ShowAsync(title, panel, primaryText, "Cancel");
                return (result == ContentDialogResult.Primary, checkbox.IsChecked == true);
            }
            catch { return (false, false); }
            finally { _gate.Release(); }
        }
    }
}
