using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
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
    public static Domain.Character ToDomain(Character model) => model.ToDomain();

    public static async ValueTask<RavenCharacterRepositorySubscription> OpenAsync(
        IDocumentStore store,
        string userId,
        ILogger<RavenCharacterRepository> log
    )
    {
        using var session = store.OpenAsyncSession();
        var docIdPrefix = RavenCharacterRepository.CharacterDocIdPrefix(userId);
        var characters = new List<RavenCharacterHandle>();
        await using var characterEnumerator = await session.Advanced.StreamAsync<Character>(docIdPrefix);
        while (await characterEnumerator.MoveNextAsync())
        {
            var character = characterEnumerator.Current.Document.ToDomain();
            var handle = new RavenCharacterHandle(character);
            characters.Add(handle);
        }

        log.Log(LogLevel.Information, 
            "Initialized character repository subscription for {Oid} with {Count} characters", 
            userId, characters.Count);
        
        return new(store, docIdPrefix, characters, log);
    }
}