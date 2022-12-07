using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Timelines;

[SuppressMessage("ReSharper", "ContextualLoggerProblem")]
class RavenTimelineSubscription : SubscriptionBase<RavenTimelineSubscription, Timeline, RavenTimelineHandle>
{
    readonly string timelineId;

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
        Timeline timeline,
        ILogger<RavenTimelineRepository> log
    ) : base(log, timeline, store, documentIdsToSubscribeToFor(timeline))
    {
         this.log = log;
        this.timelineId = timelineId;

    }

    protected override IEnumerable<string> DocumentIdsToSubscribeToFor(Timeline value) 
        => documentIdsToSubscribeToFor(value);

    static IEnumerable<string> documentIdsToSubscribeToFor(Timeline timeline)
    {
        yield return timeline.Id;
        yield return timeline.GroupId;
        foreach (var characterId in timeline.Characters.Keys)
            yield return characterId;
    }

    protected override async Task<Timeline> RefreshValueAsync()
    {
        using var session = Store.OpenAsyncSession();
        return await RavenTimelineRepository.LoadTimelineAsync(session, timelineId);    
    }

}