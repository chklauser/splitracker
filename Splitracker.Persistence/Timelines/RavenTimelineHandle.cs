using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Timelines;

class RavenTimelineHandle : HandleBase<RavenTimelineSubscription, Timeline>, ITimelineHandle,
    IHandle<RavenTimelineHandle, RavenTimelineSubscription, Timeline>
{
    RavenTimelineHandle(RavenTimelineSubscription subscription) : base(subscription)
    {
    }

    public Timeline Timeline => Value;
    public static RavenTimelineHandle Create(RavenTimelineSubscription subscription) => new(subscription);
}