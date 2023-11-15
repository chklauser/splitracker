using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Groups;

class RavenGroupHandle : HandleBase<RavenGroupSubscription, Group>, IGroupHandle,
    IHandle<RavenGroupHandle, RavenGroupSubscription>
{
    RavenGroupHandle(RavenGroupSubscription subscription)
        : base(subscription)
    {
    }

    public Group Group => Value;
    public static RavenGroupHandle Create(RavenGroupSubscription subscription) => new(subscription);
}