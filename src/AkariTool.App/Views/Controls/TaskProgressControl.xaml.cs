using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Views.Controls;

/// <summary>
/// Bottom-docked task progress card (Winhance TaskProgressControl parity, single-task
/// slice): task name + status line + cancel + indeterminate bar. Subscribes to
/// ITaskProgressService and marshals updates onto the UI thread.
/// </summary>
public sealed partial class TaskProgressControl : UserControl
{
    private readonly ITaskProgressService _progress;
    private readonly IDispatcherService _dispatcher;

    public TaskProgressControl()
        : this(
            WinUI.Framework.IoC.ServiceLocator.GetService<ITaskProgressService>(),
            WinUI.Framework.IoC.ServiceLocator.GetService<IDispatcherService>())
    {
    }

    public TaskProgressControl(ITaskProgressService progress, IDispatcherService dispatcher)
    {
        InitializeComponent();
        _progress = progress;
        _dispatcher = dispatcher;
        _progress.ProgressUpdated += OnProgressUpdated;
        UpdateFrom(_progress.IsTaskRunning,
            _progress.CurrentStatusText.Length > 0 ? _progress.CurrentStatusText : "Working…",
            string.Empty);
    }

    private void OnProgressUpdated(object? sender, TaskProgressDetail detail) =>
        _dispatcher.RunOnUIThread(() => UpdateFrom(
            detail.IsCompletion ? false : detail.IsActive || true,
            detail.StatusText ?? string.Empty,
            detail.QueueTotal > 0 ? $"{detail.QueueCurrent}/{detail.QueueTotal}" : string.Empty));

    private void UpdateFrom(bool visible, string status, string queueSuffix)
    {
        Root.Visibility = visible ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        if (!visible) return;
        StatusText.Text = string.IsNullOrEmpty(queueSuffix)
            ? status
            : $"{status}  ({queueSuffix})";
        if (TaskNameText.Text.Length == 0 && !string.IsNullOrEmpty(status))
            TaskNameText.Text = status.Split(':')[0].Trim();
        CancelButton.IsEnabled = _progress.IsTaskRunning;
    }

    private void OnCancelClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _progress.CancelCurrentTask();
}
