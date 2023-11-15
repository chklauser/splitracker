using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Groups;

[SuppressMessage("ReSharper", "ContextualLoggerProblem")]
sealed class RavenGroupSubscription(
    IDocumentStore store,
    string groupId,
    Group group,
    ILogger<RavenGroupRepository> log
) : SubscriptionBase<RavenGroupSubscription, Group, RavenGroupHandle>(
    log,
    group,
    store,
    documentIdsToSubscribeToFor(group))
{
    public static async ValueTask<RavenGroupSubscription> OpenAsync(
        IDocumentStore store,
        string groupId,
        ILogger<RavenGroupRepository> log
    )
    {
        using var session = store.OpenAsyncSession();
        return new(store, groupId, await RavenGroupRepository.LoadGroupAsync(session, groupId), log);
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