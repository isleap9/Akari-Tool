using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AkariTool.Tabs
{
    /// <summary>
    /// Sync-feeling wrappers around the async Wpf.Ui.Controls.MessageBox so
    /// existing OK/Cancel control flow at call sites stays untouched.
    /// ShowDialogAsync blocks on Window.ShowDialog internally; the
    /// dispatcher-frame pump below covers the case where the returned task
    /// is still pending when the call returns.
    /// </summary>
    public static class AkariDialogs
    {
        /// <summary>OK / Cancel confirmation. True when the user pressed OK.</summary>
        public static bool ConfirmOkCancel(string message, string title) =>
            Show(message, title, primaryText: "OK", closeText: "Cancel")
                == Wpf.Ui.Controls.MessageBoxResult.Primary;

        /// <summary>Yes / No confirmation. True when the user pressed Yes.</summary>
        public static bool ConfirmYesNo(string message, string title) =>
            Show(message, title, primaryText: "Yes", closeText: "No")
                == Wpf.Ui.Controls.MessageBoxResult.Primary;

        /// <summary>Information box with a single OK button.</summary>
        public static void Info(string message, string title) =>
            Show(message, title, primaryText: null, closeText: "OK");

        private static Wpf.Ui.Controls.MessageBoxResult Show(
            string message, string title, string? primaryText, string closeText)
        {
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                CloseButtonText = closeText,
            };
            if (primaryText is not null)
                box.PrimaryButtonText = primaryText;

            var task = box.ShowDialogAsync();
            if (!task.IsCompleted)
            {
                var frame = new DispatcherFrame();
                task.ContinueWith(_ => frame.Continue = false,
                    TaskScheduler.FromCurrentSynchronizationContext());
                Dispatcher.PushFrame(frame);
            }
            return task.GetAwaiter().GetResult();
        }
    }
}
