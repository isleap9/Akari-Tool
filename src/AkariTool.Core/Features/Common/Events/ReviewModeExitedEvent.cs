using System;
using AkariTool.Core.Features.Common.Events;

namespace AkariTool.Core.Features.Common.Events;

public class ReviewModeExitedEvent : IDomainEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}