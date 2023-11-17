using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Characters;

class RavenSingleCharacterHandle(RavenSingleCharacterSubscription subscription)
    : HandleBase<RavenSingleCharacterSubscription, Character>(subscription),
        IHandle<RavenSingleCharacterHandle, RavenSingleCharacterSubscription>, ICharacterHandle
{
    public Character Character => Value;
    public static RavenSingleCharacterHandle Create(RavenSingleCharacterSubscription subscription) => new(subscription);
}