using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Groups;

[SuppressMessage("ReSharper", "ContextualLoggerProblem")]
sealed class RavenGroupSubscription :  SubscriptionBase<RavenGroupSubscription, Group, RavenGroupHandle>
{
    readonly string groupId;
    
    public static async ValueTask<RavenGroupSubscription> OpenAsync(
        IDocumentStore store,
        string groupId,
        ILogger<RavenGroupRepository> log
    )
    {
        using var session = store.OpenAsyncSession();
        return new(store, groupId, await RavenGroupRepository.LoadGroupAsync(session, groupId), log);
    }

    RavenGroupSubscription(
        IDocumentStore store,
        string groupId,
        Group group,
        ILogger<RavenGroupRepository> log
    ) : base(log, group, store, documentIdsToSubscribeToFor(group))
    {
        this.log = log;
        this.groupId = groupId;
    }

    protected override async Task<Group> RefreshValueAsync()
    {
        using var session = Store.OpenAsyncSession();
        return await RavenGroupRepository.LoadGroupAsync(session, groupId);
    }

    protected override IEnumerable<string> DocumentIdsToSubscribeToFor(Group value) =>
        documentIdsToSubscribeToFor(value);

    static IEnumerable<string> documentIdsToSubscribeToFor(Group group)
    {
        yield return group.Id;
        foreach (var character in group.Characters.Values)
            yield return character.Id;
    }
}