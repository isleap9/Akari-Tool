using System;
using System.Threading;
using AkariTool.Core.Features.Common.Interfaces;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Services;

/// <summary>
/// Singleton task-progress tracker (Winhance TaskProgressService parity). Pure state +
/// event; the TaskProgressControl subscribes and marshals to the UI thread itself.
/// </summary>
public sealed class TaskProgressService : ITaskProgressService
{
    private readonly object _gate = new();

    public bool IsTaskRunning { get; private set; }
    public int CurrentProgress { get; private set; }
    public string CurrentStatusText { get; private set; } = string.Empty;
    public bool IsIndeterminate { get; private set; }
    public CancellationTokenSource? CurrentTaskCancellationSource { get; private set; }
    public string CurrentTaskName { get; private set; } = string.Empty;

    public event EventHandler<TaskProgressDetail>? ProgressUpdated;

    public CancellationTokenSource StartTask(string taskName, bool isIndeterminate = false)
    {
        lock (_gate)
        {
            CurrentTaskCancellationSource?.Cancel();
            CurrentTaskCancellationSource = new CancellationTokenSource();
            IsTaskRunning = true;
            IsIndeterminate = isIndeterminate;
            CurrentProgress = 0;
            CurrentStatusText = string.Empty;
            CurrentTaskName = taskName;
        }

        ProgressUpdated?.Invoke(this, new TaskProgressDetail
        {
            StatusText = taskName,
            IsActive = true,
        });
        return CurrentTaskCancellationSource;
    }

    public void UpdateProgress(int progressPercentage, string? statusText = null) =>
        UpdateDetailedProgress(new TaskProgressDetail
        {
            Progress = Math.Clamp(progressPercentage, 0, 100),
            StatusText = statusText,
            IsActive = true,
        });

    public void UpdateDetailedProgress(TaskProgressDetail detail)
    {
        lock (_gate)
        {
            if (!IsTaskRunning) return;
            if (detail.Progress > 0 || detail.IsCompletion) CurrentProgress = detail.Progress;
            if (detail.StatusText != null) CurrentStatusText = detail.StatusText;
        }

        ProgressUpdated?.Invoke(this, detail);
    }

    public void CompleteTask()
    {
        lock (_gate)
        {
            IsTaskRunning = false;
            IsIndeterminate = false;
            CurrentProgress = 100;
            CurrentStatusText = string.Empty;
            CurrentTaskCancellationSource?.Dispose();
            CurrentTaskCancellationSource = null;
        }

        ProgressUpdated?.Invoke(this, new TaskProgressDetail
        {
            Progress = 100,
            IsActive = false,
            IsCompletion = true,
        });
    }

    public void CancelCurrentTask()
    {
        try { CurrentTaskCancellationSource?.Cancel(); }
        catch (ObjectDisposedException) { }

        // Immediate UI feedback: the loop can only observe cancellation between items,
        // so tell the user right away that the request landed.
        if (IsTaskRunning)
            ProgressUpdated?.Invoke(this, new TaskProgressDetail
            {
                StatusText = "Cancelling… finishing current item",
                IsActive = true,
            });
    }

    public IProgress<TaskProgressDetail> CreateDetailedProgress() =>
        new Progress<TaskProgressDetail>(UpdateDetailedProgress);
}
