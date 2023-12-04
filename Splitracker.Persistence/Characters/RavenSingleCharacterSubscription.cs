using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Splitracker.Persistence.Generic;
using Splitracker.Persistence.Model;
using Character = Splitracker.Domain.Character;

namespace Splitracker.Persistence.Characters;

sealed class RavenSingleCharacterSubscription(IDocumentStore store, string characterId, Character character, ILogger<RavenCharacterRepository> log) 
    : SubscriptionBase<RavenSingleCharacterSubscription, Character,
        RavenSingleCharacterHandle>(log, character, store, [characterId])
{
    public static async ValueTask<RavenSingleCharacterSubscription> OpenAsync(
        IDocumentStore store,
        string characterId,
        ILogger<RavenCharacterRepository> log
    )
    {
        using var session = store.OpenAsyncSession();
        return new(store, characterId, await loadCharacter(session, characterId), log);
    }
    
    protected override IEnumerable<string> DocumentIdsToSubscribeToFor(Character value)
    {
        yield return value.Id;
    }

    protected override async Task<Character> RefreshValueAsync()
    {
        var id = characterId;
        using var session = Store.OpenAsyncSession();
        return await loadCharacter(session, id);
    }

    static async Task<Character> loadCharacter(IAsyncDocumentSession session, string id)
    {
        var model = await session.LoadAsync<Model.Character>(id);
        if (model == null)
        {
            throw new InvalidOperationException("Character with ID " + id + " cannot be refreshed.");
        }
        return model.ToDomain();
    }
}