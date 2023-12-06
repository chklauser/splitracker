using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Splitracker.Persistence.Generic;
using Splitracker.Persistence.Model;
using Character = Splitracker.Persistence.Model.Character;

namespace Splitracker.Persistence.Characters;

/// <summary>
/// A shared subscription for one user (object identifier, oid). Is not handed out directly to clients.
/// Instead, clients get a <see cref="RavenCharacterHandle"/> via the
/// <see cref="RepositorySubscriptionBase{TSelf,TValue,TValueHandle,TDbModel,TRepositoryHandle}.TryGetHandle"/> method.
/// </summary>
[SuppressMessage("ReSharper", "ContextualLoggerProblem")]
class RavenCharacterRepositorySubscription(
    IDocumentStore store,
    string docIdPrefix,
    IEnumerable<RavenCharacterHandle> initialHandles,
    ILogger log
)
    : RepositorySubscriptionBase<RavenCharacterRepositorySubscription, Domain.Character, RavenCharacterHandle,
            Character, RavenCharacterRepositoryHandle>(store, docIdPrefix, initialHandles, log),
        IRepositorySubscriptionBase<Domain.Character, Character>
{
    public static async Task<IEnumerable<Domain.Character>> ToDomainAsync(
        IAsyncDocumentSession session,
        IReadOnlyList<Character> models
    )
    {
        var templates = await RavenCharacterRepository.FetchTemplatesAsync(session, models);
        return models.ToImmutableArray().Select(m => m.ToDomain(templates));
    }

    protected override async ValueTask OnPutUpdatesExistingAsync(IAsyncDocumentSession session, Character updated)
    {
        await base.OnPutUpdatesExistingAsync(session, updated);
        var affectedHandles = Handles.Where(c => c.Value.TemplateId == updated.Id).ToImmutableArray();
        if (affectedHandles.Length > 0)
        {
            var affectedCharacters = await session.LoadAsync<Character>(affectedHandles.Select(h => h.Id));
            foreach (var affectedHandle in affectedHandles)
            {
                if (!affectedCharacters.TryGetValue(affectedHandle.Id, out var affectedCharacter))
                {
                    continue;
                }

                affectedHandle.Value = affectedCharacter.ToDomain(updated);
                affectedHandle.TriggerUpdated();
            }
        }
    }
}