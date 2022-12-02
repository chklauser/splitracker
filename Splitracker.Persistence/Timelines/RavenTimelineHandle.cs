using System;
using System.Threading.Tasks;
using Splitracker.Domain;

namespace Splitracker.Persistence.Timelines;

class RavenTimelineHandle : ITimelineHandle
{
    readonly RavenTimelineSubscription subscription;
    
    public RavenTimelineHandle(RavenTimelineSubscription subscription)
    {
        this.subscription = subscription;
        subscription.Updated += OnUpdated;
    }

    void OnUpdated(object? sender, EventArgs e)
    {
        Updated?.Invoke(sender, e);
    }

    public ValueTask DisposeAsync()
    {
        subscription.Updated -= OnUpdated;
        subscription.Release();
        Updated = null;
        return ValueTask.CompletedTask;
    }

    public Timeline Timeline => subscription.Timeline;
    public event EventHandler? Updated;
}