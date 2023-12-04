using System;
using System.Collections.Generic;
using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Characters;

/// <summary>
/// A wrapper around a <see cref="RavenCharacterRepositorySubscription"/> implement
/// reference count decrease on <see cref="IAsyncDisposable.DisposeAsync"/>.
/// </summary>
class RavenCharacterRepositoryHandle(RavenCharacterRepositorySubscription subscription) 
    : PrefixRepositoryHandle<RavenCharacterRepositoryHandle, RavenCharacterRepositorySubscription>(subscription),
        IHandle<RavenCharacterRepositoryHandle, RavenCharacterRepositorySubscription>,
        ICharacterRepositoryHandle
{
    public IReadOnlyList<ICharacterHandle> Characters => Subscription.Handles;
    public event EventHandler? CharacterAdded
    {
        add => Added += value;
        remove => Added -= value;
    }

    public event EventHandler? CharacterDeleted
    {
        add => Deleted += value;
        remove => Deleted -= value;
    }
    public static RavenCharacterRepositoryHandle Create(RavenCharacterRepositorySubscription subscription) => new(subscription);
}