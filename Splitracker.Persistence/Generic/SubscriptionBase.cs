using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Changes;

namespace Splitracker.Persistence.Generic;

[SuppressMessage("ReSharper", "ContextualLoggerProblem")]
abstract class SubscriptionBase<TSelf, TValue, THandle> : IDisposable, ISubscription<TValue>, IObserver<DocumentChange>
where THandle : class, IHandle<THandle, TSelf>
where TSelf : SubscriptionBase<TSelf, TValue, THandle>
{
    protected readonly ReaderWriterLockSlim Lock = new();
    protected ILogger Log;
    protected readonly IDocumentStore Store;
    int referenceCount;
    bool lifetimeBoundToHandles;
    IImmutableDictionary<string, IDisposable> ravenSubscriptions;

    public SubscriptionBase(ILogger log, TValue initialValue, IDocumentStore store, IEnumerable<string> documentIdsToSubscribeTo)
    {
        Log = log;
        Store = store;
        CurrentValue = initialValue;
        ravenSubscriptions = documentIdsToSubscribeTo
            .Distinct()
            .ToImmutableDictionary(
                id => id,
                id => store.Changes().ForDocument(id).Subscribe(this)
            );
    }
    
    public TValue CurrentValue { get; private set; }

    public event EventHandler? Disposed;

    protected void OnDisposed() => Disposed?.Invoke(this, EventArgs.Empty);

    public THandle? TryGetHandle()
    {
        Lock.EnterWriteLock();
        try
        {
            if (lifetimeBoundToHandles)
            {
                if (referenceCount <= 0)
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
            Lock.ExitWriteLock();
        }

        return THandle.Create((TSelf)this);
    }

    #region Update handling

    protected abstract IEnumerable<string> DocumentIdsToSubscribeToFor(TValue value);

    EventHandler? updated;

    public event EventHandler? Updated
    {
        add
        {
            Lock.EnterWriteLock();
            try
            {
                updated += value;
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }
        remove
        {
            Lock.EnterWriteLock();
            try
            {
                updated -= value;
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }
    }

    public void OnCompleted()
    {
        // nothing to do
    }

    public void OnError(Exception error)
    {
        Log.Log(LogLevel.Warning, "Error while listening for changes.");
    }

    public void OnNext(DocumentChange value)
    {
        if(lifetimeBoundToHandles && referenceCount <= 0)
        {
            return;
        }
        
        switch (value.Type)
        {
            case DocumentChangeTypes.Put:
            case DocumentChangeTypes.Delete:
                _ = doRefreshAsync();
                break;
            case var otherType:
                Log.Log(LogLevel.Warning, "Unknown document change type {Type}", otherType);
                break;
        }
    }

    async Task doRefreshAsync()
    {
        var newValue = await RefreshValueAsync();
        CurrentValue = newValue;
        synchronizeSubscriptions(newValue);
        updated?.Invoke(this, EventArgs.Empty);
    }

    protected abstract Task<TValue> RefreshValueAsync();

    void synchronizeSubscriptions(TValue group)
    {
        Lock.EnterWriteLock();
        try
        {
            var existingSubscriptions = ravenSubscriptions;
            var existingKeys = existingSubscriptions.Keys.ToHashSet();
            var requiredKeys = DocumentIdsToSubscribeToFor(group)
                .ToHashSet();
            foreach (var key in requiredKeys.ToList())
            {
                if (existingKeys.Remove(key))
                {
                    requiredKeys.Remove(key);
                }
            }

            if (requiredKeys.Count > 0 || existingKeys.Count > 0)
            {
                Log.Log(LogLevel.Information,
                    "Subscriptions changed, removing {ExistingCount} and adding {RequiredCount}",
                    existingKeys.Count, requiredKeys.Count);

                var subscriptions = ImmutableDictionary.CreateBuilder<string, IDisposable>();

                // Add subscriptions that have not changed
                subscriptions.AddRange(existingSubscriptions.Where(s => !existingKeys.Contains(s.Key)));

                // Add new subscriptions
                subscriptions.AddRange(requiredKeys.Select(k =>
                    new KeyValuePair<string, IDisposable>(
                        k,
                        Store.Changes().ForDocument(k).Subscribe(this)
                    )));

                // Remove old subscriptions
                foreach (var key in existingKeys)
                {
                    existingSubscriptions[key].Dispose();
                }

                ravenSubscriptions = subscriptions.ToImmutable();
            }
        }
        catch (Exception e)
        {
            Log.Log(LogLevel.Critical, e,
                "Failed to refresh subscriptions. Memory and/or connections might have been leaked!");
            throw;
        }
        finally
        {
            Lock.ExitWriteLock();
        }
    }

    
    #endregion

    #region Cleanup

    public void Dispose()
    {
        Lock.EnterWriteLock();
        if (referenceCount <= 0)
        {
            // already disposed
            return;
        }

        try
        {
            referenceCount = 0;
            DisposeWhileAlreadyLocked();
        }
        finally
        {
            Lock.ExitWriteLock();
        }

        try
        {
            OnDisposed();
        }
        finally
        {
            Disposed = null;
        }
    }

    public void Release()
    {
        Lock.EnterWriteLock();
        try
        {
            referenceCount -= 1;
            if (referenceCount <= 0)
            {
                DisposeWhileAlreadyLocked();
            }
        }
        finally
        {
            Lock.ExitWriteLock();
        }

        try
        {
            OnDisposed();
        }
        finally
        {
            Disposed = null;
        }
    }

    protected virtual void DisposeWhileAlreadyLocked()
    {
        updated = null;
        var subscriptions = ravenSubscriptions;
        ravenSubscriptions = ImmutableDictionary<string, IDisposable>.Empty;
        foreach (var s in subscriptions.Values)
        {
            s.Dispose();
        }
    }

    #endregion
}