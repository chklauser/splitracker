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
    public static async ValueTask<RavenSingleCharacterSubscription?> OpenAsync(
        IDocumentStore store,
        string characterId,
        ILogger<RavenCharacterRepository> log
    )
    {
        using var session = store.OpenAsyncSession();
        var character = await loadCharacter(session, characterId);
        if (character == null)
        {
            return null;
        }
        
        return new(store, characterId, character, log);
    }
    
    protected override IEnumerable<string> DocumentIdsToSubscribeToFor(Character value)
    {
        if (value.TemplateId is { } templateId)
        {
            yield return templateId;
        }
        
        yield return value.Id;
    }

    protected override async Task<Character> RefreshValueAsync()
    {
        var id = characterId;
        using var session = Store.OpenAsyncSession();
        return await loadCharacter(session, id) ?? throw new InvalidOperationException("Character " + id + " cannot be refreshed. Not found.");
    }

    static async Task<Character?> loadCharacter(IAsyncDocumentSession session, string id)
    {
        var model = await session.LoadAsync<Model.Character>(id);
        if (model == null)
        {
            return null;
        }

        Model.Character? templateCharacter;
        if (model.TemplateId is { } templateId)
        {
            templateCharacter = await session.LoadAsync<Model.Character>(templateId);
            if (templateCharacter == null)
            {
                throw new InvalidOperationException("Cannot find template character " + id);
            }
        }
        else
        {
            templateCharacter = null;
        }
        
        return model.ToDomain(templateCharacter);
    }
}