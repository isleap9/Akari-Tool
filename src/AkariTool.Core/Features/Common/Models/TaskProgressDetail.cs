using System;

namespace AkariTool.Core.Features.Common.Models;

/// <summary>
/// Detailed progress payload for long-running tasks (Winhance TaskProgressDetail parity).
/// </summary>
public class TaskProgressDetail
{
    /// <summary>Progress percentage (0-100).</summary>
    public int Progress { get; set; }

    /// <summary>Status text describing the current operation.</summary>
    public string? StatusText { get; set; }

    /// <summary>Name of the item currently being processed (queue "next" display).</summary>
    public string? QueueCurrentItemName { get; set; }

    /// <summary>1-based index of the current queue item.</summary>
    public int QueueCurrent { get; set; }

    /// <summary>Total number of items in the queue.</summary>
    public int QueueTotal { get; set; }

    /// <summary>Whether the task is actively running.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether this report completes the task.</summary>
    public bool IsCompletion { get; set; }
}
