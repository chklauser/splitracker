using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Changes;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Generic;
using Splitracker.Persistence.Model;
using Tag = Splitracker.Domain.Tag;

namespace Splitracker.Persistence.Tags;

class RavenTagRepository(IDocumentStore store, ILogger<RavenTagRepository> log, IUserRepository repository)
    : ITagRepository
{
    const string CollectionName = "Tags";
    readonly ConcurrentDictionary<string, Task<RavenTagRepositorySubscription>> handles = new();

    public async Task<ITagRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        var userId = await repository.GetUserIdAsync(principal);

        return await handles.TryCreateSubscription(userId,
                createSubscription: async () =>
                {
                    var prefix = tagDocIdPrefix(userId);
                    var tags = await RavenTagRepositorySubscription.FetchInitialAsync(store, prefix);
                    return new RavenTagRepositorySubscription(store, prefix, tags, log);
                },
                onExistingSubscription: () =>
                {
                    log.Log(LogLevel.Debug, "Trying to join existing tag subscription for {UserId}", userId);
                },
                tryGetHandle: s => s.TryGetHandle(),
                onRetry: () =>
                    log.Log(LogLevel.Information,
                        "Tag subscription for {UserId} was disposed of. Retrying.",
                        userId)) ??
            throw new InvalidOperationException("Failed to open a tag repository handle.");
    }

    public async Task ApplyAsync(ClaimsPrincipal principal, ITagCommand tagCommand)
    {
        var userId = await repository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession();
        Model.Tag? model;
        var id = tagCommand.TagId;
        if (id != null)
        {
            if (!isOwner(id, userId))
            {
                throw new ArgumentException($"Tag {id} does not belong to user {userId}.");
            }

            model = await session.LoadAsync<Model.Tag>(id);
            if (model == null)
            {
                log.Log(LogLevel.Warning, "Tag {Id} not found.", id);
                return;
            }
        }
        else
        {
            model = null;
        }

        switch (tagCommand)
        {
            case DeleteTag deleteTag:
                session.Delete(model);
                // TODO: remove tag from characters
                break;
            case CreateTag create:
                await session.StoreAsync(new Model.Tag(TagDocIdPrefix(userId), create.Name));
                break;
            case EditTag editTag:
                model!.Name = editTag.Name;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(tagCommand));
        }

        await session.SaveChangesAsync();
    }

    internal static string TagDocIdPrefix(string userId)
    {
        return $"{CollectionName}/{userId["Users/".Length..]}/";
    }
    
    bool isOwner(string tagId, string userId) =>
        tagId.StartsWith(TagDocIdPrefix(userId), StringComparison.Ordinal);

    static string tagDocIdPrefix(string userId)
    {
        return $"{CollectionName}/{userId["Users/".Length..]}/";
    }
}

class RavenTagRepositorySubscription(
    IDocumentStore store,
    string docIdPrefix,
    IEnumerable<RavenTagHandle> initialHandles,
    ILogger log
) : RepositorySubscriptionBase<RavenTagRepositorySubscription, Tag, RavenTagHandle, Model.Tag,
    RavenTagRepositoryHandle>(store, docIdPrefix, initialHandles, log), IRepositorySubscriptionBase<Tag, Model.Tag>
{
    public static Tag ToDomain(Model.Tag model) => model.ToDomain();
}

class RavenTagRepositoryHandle(RavenTagRepositorySubscription subscription)
    : PrefixRepositoryHandle<RavenTagRepositoryHandle, RavenTagRepositorySubscription>(subscription),
        IHandle<RavenTagRepositoryHandle, RavenTagRepositorySubscription>,
        ITagRepositoryHandle
{
    public static RavenTagRepositoryHandle Create(RavenTagRepositorySubscription subscription) => new(subscription);
    public IReadOnlyList<ITagHandle> Tags => Subscription.Handles;
}

class RavenTagHandle(Tag value) : PrefixHandleBase<RavenTagHandle, Tag>(value), IPrefixHandle<RavenTagHandle, Tag>, ITagHandle
{
    public static RavenTagHandle Create(Tag value) => new(value);

    public override string Id => Value.Id;
    public Tag Tag => Value;
}

interface IRepositorySubscriptionBase<TValue, TDbModel>
{
    public static abstract TValue ToDomain(TDbModel model);
}

interface IRepositorySubscription
{
    void Release();
    event EventHandler? Added;
    event EventHandler? Deleted;
}

abstract class PrefixHandleBase<TSelf, TValue>(TValue value) : IDisposable, IEquatable<TSelf>
    where TSelf : PrefixHandleBase<TSelf, TValue>
    where TValue : class
{
    volatile TValue value = value;

    public event EventHandler? Updated;

    public abstract string Id { get; }

    public TValue Value
    {
        get => value;
        set => this.value = value;
    }

    public void TriggerUpdated()
    {
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Updated = null;
    }

    public bool Equals(TSelf? other) => other?.Id == Id;

    public bool Equals(PrefixHandleBase<TSelf, TValue>? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PrefixHandleBase<TSelf, TValue>)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(PrefixHandleBase<TSelf, TValue>? left, PrefixHandleBase<TSelf, TValue>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PrefixHandleBase<TSelf, TValue>? left, PrefixHandleBase<TSelf, TValue>? right)
    {
        return !Equals(left, right);
    }
}

abstract class PrefixRepositoryHandle<TSelf, TSubscription> : IAsyncDisposable, IDisposable
    where TSubscription : IRepositorySubscription
    where TSelf : IHandle<TSelf, TSubscription>
{
    protected readonly TSubscription Subscription;

    public PrefixRepositoryHandle(TSubscription subscription)
    {
        Subscription = subscription;
        subscription.Added += OnAdded;
        subscription.Deleted += OnDeleted;
    }

    public event EventHandler? Added;
    public event EventHandler? Deleted;

    void OnAdded(object? sender, EventArgs e)
    {
        Added?.Invoke(sender, e);
    }

    void OnDeleted(object? sender, EventArgs e)
    {
        Deleted?.Invoke(sender, e);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        Subscription.Added -= OnAdded;
        Subscription.Deleted -= OnDeleted;
        Subscription.Release();
        Added = null;
        Deleted = null;
    }
}

class RepositorySubscriptionBase<TSelf, TValue, TValueHandle, TDbModel, TRepositoryHandle> : IObserver<DocumentChange>,
    IAsyncDisposable,
    IDisposable, IRepositorySubscription
    where TValueHandle : IPrefixHandle<TValueHandle, TValue>, IDisposable
    where TSelf : RepositorySubscriptionBase<TSelf, TValue, TValueHandle, TDbModel, TRepositoryHandle>,
    IRepositorySubscriptionBase<TValue, TDbModel>
    where TRepositoryHandle : PrefixRepositoryHandle<TRepositoryHandle, TSelf>, IHandle<TRepositoryHandle, TSelf>
{
    readonly ReaderWriterLockSlim @lock = new();
    readonly IDocumentStore store;
    readonly ILogger log;
    readonly IDisposable subscription;
    int referenceCount;
    bool lifetimeBoundToHandles;

    volatile IImmutableList<TValueHandle> handles;
    public IReadOnlyList<TValueHandle> Handles => handles;

    protected RepositorySubscriptionBase(
        IDocumentStore store,
        string docIdPrefix,
        IEnumerable<TValueHandle> initialHandles,
        ILogger log
    )
    {
        this.store = store;
        this.log = log;
        handles = initialHandles.ToImmutableList();
        subscription = store.Changes().ForDocumentsStartingWith(docIdPrefix).Subscribe(this);
    }


    public static async Task<IEnumerable<TValueHandle>> FetchInitialAsync(IDocumentStore store, string docIdPrefix)
    {
        using var session = store.OpenAsyncSession();
        var handles = new List<TValueHandle>();
        await using var valueEnumerator = await session.Advanced.StreamAsync<TDbModel>(docIdPrefix);
        while (await valueEnumerator.MoveNextAsync())
        {
            var value = TSelf.ToDomain(valueEnumerator.Current.Document);
            var handle = TValueHandle.Create(value);
            handles.Add(handle);
        }

        return handles;
    }

    public TRepositoryHandle? TryGetHandle()
    {
        @lock.EnterWriteLock();
        try
        {
            if (lifetimeBoundToHandles)
            {
                if (referenceCount == 0)
                {
                    return null;
                }
            }
            else
            {
                lifetimeBoundToHandles = true;
            }

            referenceCount += 1;
        }
        finally
        {
            @lock.ExitWriteLock();
        }

        return TRepositoryHandle.Create((TSelf)this);
    }

    public void Release()
    {
        @lock.EnterWriteLock();
        try
        {
            referenceCount -= 1;
            if (referenceCount == 0)
            {
                subscription.Dispose();
            }
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    #region IObserver

    public void OnCompleted()
    {
        // nothing to do
    }

    public void OnError(Exception error)
    {
        log.Log(LogLevel.Error, error, "Error while listing for changes of {Type}", typeof(TValue).Name);
    }

    public async void OnNext(DocumentChange value)
    {
        using var session = store.OpenAsyncSession();
        var handle = handles.FirstOrDefault(h => h.Id == value.Id);

        switch (value.Type)
        {
            case DocumentChangeTypes.Delete when handle == null:
                log.Log(LogLevel.Warning,
                    "{Type} {Id} was deleted but not found in the cached list of handles.",
                    typeof(TValue).Name,
                    value.Id);
                return;
            case DocumentChangeTypes.Delete:
                log.Log(LogLevel.Debug, "{Type} {Id} deleted.", typeof(TValue).Name, value.Id);
                handles = handles.Remove(handle);
                handle.Dispose();
                @lock.EnterReadLock();
                EventHandler? deletedHandler;
                try
                {
                    deletedHandler = deleted;
                }
                finally
                {
                    @lock.ExitReadLock();
                }

                deletedHandler?.Invoke(this, EventArgs.Empty);
                break;
            case DocumentChangeTypes.Put:
                log.Log(LogLevel.Debug, "{Type} {Id} created or updated.", typeof(TValue).Name, value.Id);
                var newValue = await session.LoadAsync<TDbModel>(value.Id) is { } dbModel
                    ? (TValue?)TSelf.ToDomain(dbModel)
                    : default;
                if (newValue != null)
                {
                    if (handle == null)
                    {
                        handles = handles.Add(TValueHandle.Create(newValue));
                        @lock.EnterReadLock();
                        EventHandler? addedHandler;
                        try
                        {
                            addedHandler = added;
                        }
                        finally
                        {
                            @lock.ExitReadLock();
                        }

                        addedHandler?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        handle.Value = newValue;
                        handle.TriggerUpdated();
                    }
                }
                else
                {
                    log.Log(LogLevel.Warning,
                        "{Type} {Id} was added or updated but not found in the database.",
                        typeof(TValue).Name,
                        value.Id);
                }

                break;
            case var otherType:
                log.Log(LogLevel.Warning, "Unknown document change type {Type}", otherType);
                break;
        }
    }

    #endregion

    #region EventHandler

    EventHandler? added;

    public event EventHandler? Added
    {
        add
        {
            @lock.EnterWriteLock();
            try
            {
                added += value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
        remove
        {
            @lock.EnterWriteLock();
            try
            {
                added -= value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
    }

    EventHandler? deleted;

    public event EventHandler? Deleted
    {
        add
        {
            @lock.EnterWriteLock();
            try
            {
                deleted += value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
        remove
        {
            @lock.EnterWriteLock();
            try
            {
                deleted -= value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
    }

    #endregion

    public void Dispose()
    {
        @lock.EnterWriteLock();
        IImmutableList<TValueHandle> hs;
        try
        {
            if (referenceCount > 0)
            {
                subscription.Dispose();
            }

            added = null;
            deleted = null;
            hs = Interlocked.Exchange(ref handles, ImmutableList<TValueHandle>.Empty);
        }
        finally
        {
            @lock.ExitWriteLock();
        }

        foreach (var handle in hs)
        {
            handle.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }
}