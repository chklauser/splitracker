using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Changes;

namespace Splitracker.Persistence.Generic;

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

    EventHandler? disposed;
    
    public event EventHandler? Disposed
    {
        add
        {
            @lock.EnterWriteLock();
            try
            {
                disposed += value;
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
                disposed -= value;
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
        
        try
        {
            disposed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            disposed = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }
}