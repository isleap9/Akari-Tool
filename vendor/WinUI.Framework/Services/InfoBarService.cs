using Microsoft.UI.Xaml.Controls;

namespace WinUI.Framework.Services;

/// <summary>
/// <see cref="IInfoBarService"/> implementation backed by an app-wide
/// <see cref="InfoBar"/> control.
/// </summary>
public class InfoBarService : IInfoBarService
{
    public InfoBar? InfoBar { get; set; }

    public void Show(
        string title,
        string? message = null,
        InfoBarSeverity severity = InfoBarSeverity.Informational,
        bool isPersistent = false,
        string? actionText = null,
        Action? action = null)
    {
        if (InfoBar is null)
        {
            return;
        }

        InfoBar.Title = title;
        InfoBar.Message = message;
        InfoBar.Severity = severity;
        InfoBar.IsOpen = true;
        InfoBar.IsClosable = !isPersistent;

        InfoBar.ActionButton = null;
        if (action is not null && actionText is not null)
        {
            var button = new Button { Content = actionText };
            button.Click += (_, _) => action();
            InfoBar.ActionButton = button;
        }
    }

    public void Hide()
    {
        if (InfoBar is not null)
        {
            InfoBar.IsOpen = false;
        }
    }
}
