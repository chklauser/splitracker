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
where THandle : class, IHandle<THandle, TSelf, TValue>
where TSelf : SubscriptionBase<TSelf, TValue, THandle>
{
    protected readonly ReaderWriterLockSlim @lock = new();
    protected ILogger log;
    protected readonly IDocumentStore Store;
    int referenceCount = 1;
    IImmutableDictionary<string, IDisposable> ravenSubscriptions;

    public SubscriptionBase(ILogger log, TValue initialValue, IDocumentStore store, IEnumerable<string> documentIdsToSubscribeTo)
    {
        this.log = log;
        this.Store = store;
        CurrentValue = initialValue;
        ravenSubscriptions = documentIdsToSubscribeTo
            .Distinct()
            .ToImmutableDictionary(
                id => id,
                id => store.Changes().ForDocument(id).Subscribe(this)
            );
    }
    
    public TValue CurrentValue { get; private set; }
    
    public THandle? TryGetHandle()
    {
        @lock.EnterWriteLock();
        try
        {
            if (referenceCount <= 0)
            {
                return null;
            }

            referenceCount += 1;
        }
        finally
        {
            @lock.ExitWriteLock();
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
            @lock.EnterWriteLock();
            try
            {
                updated += value;
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
                updated -= value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
    }

    public void OnCompleted()
    {
        // nothing to do
    }

    public void OnError(Exception error)
    {
        log.Log(LogLevel.Warning, "Error while listening for changes.");
    }

    public void OnNext(DocumentChange value)
    {
        switch (value.Type)
        {
            case DocumentChangeTypes.Put:
            case DocumentChangeTypes.Delete:
                _ = doRefreshAsync();
                break;
            case var otherType:
                log.Log(LogLevel.Warning, "Unknown document change type {Type}", otherType);
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
        @lock.EnterWriteLock();
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
                log.Log(LogLevel.Information,
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
            log.Log(LogLevel.Critical, e,
                "Failed to refresh subscriptions. Memory and/or connections might have been leaked!");
            throw;
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    
    #endregion

    #region Cleanup

    public void Dispose()
    {
        @lock.EnterWriteLock();
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
            @lock.ExitWriteLock();
        }
    }

    public void Release()
    {
        @lock.EnterWriteLock();
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
            @lock.ExitWriteLock();
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