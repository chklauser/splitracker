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
using Splitracker.Persistence.Model;

namespace Splitracker.Persistence.Timelines;

[SuppressMessage("ReSharper", "ContextualLoggerProblem")]
class RavenTimelineSubscription : IObserver<DocumentChange>, IDisposable
{
    readonly ReaderWriterLockSlim @lock = new();
    readonly IDocumentStore store;
    readonly ILogger<RavenTimelineRepository> log;
    IImmutableDictionary<(SubscriptionType Type, string Id), IDisposable> timelineSubscriptions;
    readonly string timelineId;
    int referenceCount = 1;

    public static async ValueTask<RavenTimelineSubscription> OpenAsync(
        IDocumentStore store,
        string timelineId,
        ILogger<RavenTimelineRepository> log
    )
    {
        using var session = store.OpenAsyncSession();
        return new(store, timelineId, await RavenTimelineRepository.LoadTimelineAsync(session, timelineId), log);
    }

    RavenTimelineSubscription(
        IDocumentStore store,
        string timelineId,
        Domain.Timeline timeline,
        ILogger<RavenTimelineRepository> log
    )
    {
        this.store = store;
        this.log = log;
        this.timelineId = timelineId;

        Timeline = timeline;
        var subscriptions = ImmutableDictionary.CreateBuilder<(SubscriptionType Type, string Id), IDisposable>();
        subscriptions.Add((SubscriptionType.Timeline, timelineId),
            store.Changes().ForDocument(timelineId).Subscribe(this));
        subscriptions.Add((SubscriptionType.Group, timeline.GroupId),
            store.Changes().ForDocument(timeline.GroupId).Subscribe(this));
        subscriptions.AddRange(timeline.Characters.Keys.Select(cid =>
            new KeyValuePair<(SubscriptionType Type, string Id), IDisposable>(
                (SubscriptionType.Character, cid),
                store.Changes().ForDocument(cid).Subscribe(this)
            )));
        timelineSubscriptions = subscriptions.ToImmutable();
    }

    enum SubscriptionType
    {
        Group,
        Character,
        Timeline
    }

    async Task refreshTimelineAsync()
    {
        using var session = store.OpenAsyncSession();
        var timeline = await RavenTimelineRepository.LoadTimelineAsync(session, timelineId);
        Timeline = timeline;
        synchronizeSubscriptions(timeline);

        updated?.Invoke(this, EventArgs.Empty);
    }

    void synchronizeSubscriptions(Domain.Timeline timeline)
    {
        @lock.EnterWriteLock();
        try
        {
            var existingSubscriptions = timelineSubscriptions;
            var existingKeys = existingSubscriptions.Keys.ToHashSet();
            HashSet<(SubscriptionType Type, string Id)> requiredKeys = timeline.Characters.Keys
                .Select(cid => (SubscriptionType.Character, cid))
                .Append((SubscriptionType.Group, timeline.GroupId))
                .Append((SubscriptionType.Timeline, timelineId))
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
                    "Subscriptions for timeline {TimelineId} changed, removing {ExistingCount} and adding {RequiredCount}",
                    timelineId, existingKeys.Count, requiredKeys.Count);

                var subscriptions = ImmutableDictionary.CreateBuilder<(SubscriptionType Type, string Id), IDisposable>();

                // Add subscriptions that have not changed
                subscriptions.AddRange(existingSubscriptions.Where(s => !existingKeys.Contains(s.Key)));

                // Add new subscriptions
                subscriptions.AddRange(requiredKeys.Select(k =>
                    new KeyValuePair<(SubscriptionType Type, string Id), IDisposable>(
                        k,
                        store.Changes().ForDocument(k.Id).Subscribe(this)
                    )));

                // Remove old subscriptions
                foreach (var key in existingKeys)
                {
                    existingSubscriptions[key].Dispose();
                }

                timelineSubscriptions = subscriptions.ToImmutable();
            }
        }
        catch (Exception e)
        {
            log.Log(LogLevel.Critical, e,
                "Failed to refresh timeline {TimelineId} subscriptions. Memory and/or connections might have been leaked!",
                timelineId);
            throw;
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    public Domain.Timeline Timeline { get; set; }

    public void OnCompleted()
    {
        // nothing to do
    }

    public void OnError(Exception error)
    {
        log.Log(LogLevel.Error, error, "Error while listening for timeline changes for {TimelineId}", timelineId);
    }

    public void OnNext(DocumentChange value)
    {
        switch (value.Type)
        {
            case DocumentChangeTypes.Put:
            case DocumentChangeTypes.Delete:
                _ = refreshTimelineAsync();
                break;
            case var otherType:
                log.Log(LogLevel.Warning, "Unknown document change type {Type} for timeline {TimelineId}", otherType,
                    timelineId);
                break;
        }
    }

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

    public RavenTimelineHandle? TryGetHandle()
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

        return new(this);
    }

    public void Release()
    {
        @lock.EnterWriteLock();
        try
        {
            referenceCount -= 1;
            if (referenceCount <= 0)
            {
                disposeWhileAlreadyLocked();
            }
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

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
            disposeWhileAlreadyLocked();
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    void disposeWhileAlreadyLocked()
    {
        updated = null;
        var subscriptions = timelineSubscriptions;
        timelineSubscriptions = ImmutableDictionary<(SubscriptionType, string), IDisposable>.Empty;
        foreach (var s in subscriptions.Values)
        {
            s.Dispose();
        }
    }
}