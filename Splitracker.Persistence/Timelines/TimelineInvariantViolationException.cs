using System;
using System.Runtime.Serialization;

namespace Splitracker.Persistence.Timelines;

public class TimelineInvariantViolationException : Exception
{
    public string? TimelineId { get; }
    
    public TimelineInvariantViolationException(string? timelineId)
    {
        TimelineId = timelineId;
    }

    public TimelineInvariantViolationException(string? message, string? timelineId) : base(message)
    {
        TimelineId = timelineId;
    }

    public TimelineInvariantViolationException(string? message, Exception? innerException, string? timelineId) : base(message, innerException)
    {
        TimelineId = timelineId;
    }
}