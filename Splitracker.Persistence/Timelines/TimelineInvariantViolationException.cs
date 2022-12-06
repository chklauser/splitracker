using System;
using System.Runtime.Serialization;

namespace Splitracker.Persistence.Timelines;

[Serializable]
public class TimelineInvariantViolationException : Exception
{
    public string? TimelineId { get; }
    
    public TimelineInvariantViolationException(string? timelineId)
    {
        TimelineId = timelineId;
    }

    protected TimelineInvariantViolationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        TimelineId = info.GetString("TimelineId") ?? null;
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