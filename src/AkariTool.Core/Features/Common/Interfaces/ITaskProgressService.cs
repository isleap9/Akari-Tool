using System;
using System.Collections.Generic;
using System.Threading;
using AkariTool.Core.Features.Common.Models;

namespace AkariTool.Core.Features.Common.Interfaces;

/// <summary>
/// Tracks progress of long-running tasks (Winhance ITaskProgressService parity).
/// Divergences: terminal-output accumulation (winget) and skip-next flag are not
/// ported — Akari has no winget/multi-script consumers yet.
/// </summary>
public interface ITaskProgressService
{
    bool IsTaskRunning { get; }
    int CurrentProgress { get; }
    string CurrentStatusText { get; }
    bool IsIndeterminate { get; }
    CancellationTokenSource? CurrentTaskCancellationSource { get; }

    /// <summary>Starts a new task and returns its cancellation source.</summary>
    CancellationTokenSource StartTask(string taskName, bool isIndeterminate = false);

    void UpdateProgress(int progressPercentage, string? statusText = null);

    void UpdateDetailedProgress(TaskProgressDetail detail);

    void CompleteTask();

    void CancelCurrentTask();

    IProgress<TaskProgressDetail> CreateDetailedProgress();

    event EventHandler<TaskProgressDetail>? ProgressUpdated;
}
