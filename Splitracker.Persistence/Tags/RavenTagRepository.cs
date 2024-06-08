using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Generic;
using Splitracker.Persistence.Model;
using Tag = Splitracker.Domain.Tag;

namespace Splitracker.Persistence.Tags;

class RavenTagRepository(IDocumentStore store, ILogger<RavenTagRepository> log, IUserRepository repository, ICharacterRepository characterRepository)
    : ITagRepository
{
    const string CollectionName = "Tags";
    readonly ConcurrentDictionary<string, Task<RavenTagRepositorySubscription>> handles = new(StringComparer.Ordinal);

    public async Task<ITagRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        var userId = await repository.GetUserIdAsync(principal);

        return await handles.TryCreateSubscription(userId,
                createSubscription: async () =>
                {
                    var prefix = tagDocIdPrefix(userId);
                    var tags = await RavenTagRepositorySubscription.FetchInitialAsync(store, prefix);
                    return new(store, prefix, tags, log);
                },
                onExistingSubscription: () =>
                {
                    log.Log(LogLevel.Debug, "Trying to join existing tag subscription for {UserId}", userId);
                },
                tryGetHandle: s => s.TryGetHandle(),
                onRetry: () =>
                    log.Log(LogLevel.Information,
                        "Tag subscription for {UserId} was disposed of. Retrying.",
                        userId)) ??
            throw new InvalidOperationException("Failed to open a tag repository handle.");
    }

    public async Task ApplyAsync(ClaimsPrincipal principal, ITagCommand tagCommand)
    {
        var userId = await repository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession();
        Model.Tag? model;
        var id = tagCommand.TagId;
        if (id != null)
        {
            if (!isOwner(id, userId))
            {
                throw new ArgumentException($"Tag {id} does not belong to user {userId}.");
            }

            model = await session.LoadAsync<Model.Tag>(id);
            if (model == null)
            {
                log.Log(LogLevel.Warning, "Tag {Id} not found.", id);
                return;
            }
        }
        else
        {
            model = null;
        }

        switch (tagCommand)
        {
            case DeleteTag deleteTag:
                session.Delete(model);
                await characterRepository.ApplyAsync(principal, deleteTag);
                break;
            case CreateTag create:
                await session.StoreAsync(new Model.Tag(TagDocIdPrefix(userId), create.Name));
                break;
            case EditTag editTag:
                model!.Name = editTag.Name;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(tagCommand));
        }

        await session.SaveChangesAsync();
    }

    internal static string TagDocIdPrefix(string userId)
    {
        return $"{CollectionName}/{userId["Users/".Length..]}/";
    }
    
    bool isOwner(string tagId, string userId) =>
        tagId.StartsWith(TagDocIdPrefix(userId), StringComparison.Ordinal);

    static string tagDocIdPrefix(string userId)
    {
        return $"{CollectionName}/{userId["Users/".Length..]}/";
    }
}

class RavenTagRepositorySubscription(
    IDocumentStore store,
    string docIdPrefix,
    IEnumerable<RavenTagHandle> initialHandles,
    ILogger log
) : RepositorySubscriptionBase<RavenTagRepositorySubscription, Tag, RavenTagHandle, Model.Tag,
    RavenTagRepositoryHandle>(store, docIdPrefix, initialHandles, log), IRepositorySubscriptionBase<Tag, Model.Tag>
{
    public static Task<IEnumerable<Tag>> ToDomainAsync(IAsyncDocumentSession session, IReadOnlyList<Model.Tag> models)
        => Task.FromResult(models.ToImmutableArray().Select(m => m.ToDomain()));
}

class RavenTagRepositoryHandle(RavenTagRepositorySubscription subscription)
    : PrefixRepositoryHandle<RavenTagRepositoryHandle, RavenTagRepositorySubscription>(subscription),
        IHandle<RavenTagRepositoryHandle, RavenTagRepositorySubscription>,
        ITagRepositoryHandle
{
    public static RavenTagRepositoryHandle Create(RavenTagRepositorySubscription subscription) => new(subscription);
    public IReadOnlyList<ITagHandle> Tags => Subscription.Handles;
}

[SuppressMessage("Design", "MA0095:A class that implements IEquatable<T> should override Equals(object)")]
sealed class RavenTagHandle(Tag value) : PrefixHandleBase<RavenTagHandle, Tag>(value), IPrefixHandle<RavenTagHandle, Tag>, ITagHandle
{
    public static RavenTagHandle Create(Tag value) => new(value);

    public override string Id => Value.Id;
    public Tag Tag => Value;
}