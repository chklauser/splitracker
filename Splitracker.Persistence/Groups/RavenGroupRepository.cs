using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session;
using Splitracker.Domain;
using Splitracker.Persistence.Characters;
using Splitracker.Persistence.Model;
using Character = Splitracker.Persistence.Model.Character;

namespace Splitracker.Persistence.Groups;

class RavenGroupRepository : IGroupRepository, IHostedService
{
    internal const string CollectionName = "Groups";
    readonly IDocumentStore store;
    readonly ILogger<RavenGroupRepository> log;
    readonly IUserRepository userRepository;

    public RavenGroupRepository(
        IDocumentStore store,
        ILogger<RavenGroupRepository> log,
        IUserRepository userRepository
    )
    {
        this.store = store;
        this.log = log;
        this.userRepository = userRepository;
    }

    #region Reading

    readonly ConcurrentDictionary<string, Task<RavenGroupSubscription>> handles = new();

    public async Task<IGroupHandle?> OpenSingleAsync(ClaimsPrincipal principal, string groupId)
    {
        var userId = await userRepository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession();
        if (await accessGroupAsync(groupId, userId, session) is not { } role)
        {
            return null;
        }

        return await handles.TryCreateSubscription<string, RavenGroupSubscription, RavenGroupHandle, Domain.Group>(
            groupId,
            createSubscription: async () => await RavenGroupSubscription.OpenAsync(store, groupId, log),
            onExistingSubscription: () =>
                log.Log(LogLevel.Debug, "Trying to join existing group subscription {GroupId}", groupId),
            onRetry: () =>
                log.Log(LogLevel.Information,
                    "Group subscription for {GroupId} was disposed of, retrying",
                    groupId)
        ) ?? throw new InvalidOperationException("Failed to open a handle for the group.");
    }

    async Task<Model.GroupRole?> accessGroupAsync(string groupId, string userId, IAsyncDocumentSession session)
    {
        log.LogInformation("Checking access to group of group {GroupId} for user {UserId}", groupId, userId);
        var result = await session.LoadAsync<Model.Group>(groupId);
        return result.Members.FirstOrDefault(m => m.UserId == userId)?.Role;
    }

    public async Task<IEnumerable<Domain.Character>> SearchCharactersAsync(
        string searchTerm,
        string groupId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken
    )
    {
        var userId = await userRepository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession();

        if (await accessGroupAsync(groupId, userId, session) is not { } role)
        {
            return Enumerable.Empty<Domain.Character>();
        }

        var group = await LoadGroupAsync(session, groupId);
        var dbCharacters = await session.Advanced.AsyncDocumentQuery<Character, Character_ByName>()
            .WhereStartsWith(x => x.Id, RavenCharacterRepository.CharacterDocIdPrefix(userId))
            .Not.WhereIn(c => c.Id, group.Characters.Keys)
            .Search(c => c.Name, $"{searchTerm}*", SearchOperator.And)
            .Take(100)
            .ToListAsync(cancellationToken);
        if (dbCharacters == null)
        {
            log.Log(LogLevel.Warning, "Unexpectedly got `null` from search query for characters.");
            return Enumerable.Empty<Domain.Character>();
        }

        log.Log(LogLevel.Debug,
            "Searching for characters for group {GroupId} returned {Count} results. UserId={UserId}",
            groupId,
            dbCharacters.Count,
            userId);
        return dbCharacters.Select(c => c.ToDomain()).OrderBy(c => c.Name).ToImmutableArray();
    }

    public async Task<JoinResult> GetByJoinCodeAsync(ClaimsPrincipal principal, string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return new JoinResult.GroupNotFound();
        }

        var userId = await userRepository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession();
        var groupToJoin = await session.Query<Model.Group, Group_ByJoinCode>()
            .Where(g => g.JoinCode == joinCode)
            .SingleOrDefaultAsync();
        if (groupToJoin == null)
        {
            return new JoinResult.GroupNotFound();
        }

        var characters = await session.LoadAsync<Model.Character>(groupToJoin.CharacterIds);
        var domainGroup = GroupModelMapper.ToDomain(groupToJoin, characters.Values, false);

        if (groupToJoin.Members.Any(m => m.UserId == userId))
        {
            return new JoinResult.GroupAlreadyJoined(domainGroup);
        }
        else
        {
            return new JoinResult.GroupExists(domainGroup);
        }
    }

    public async Task<IReadOnlyList<GroupInfo>> ListGroupsAsync(ClaimsPrincipal principal)
    {
        var userId = await userRepository.GetUserIdAsync(principal);
        
        using var session = store.OpenAsyncSession();
        var groups = await session.Query<Group_ByUserId.IndexEntry, Group_ByUserId>()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Name)
            .ToListAsync();
        if (groups == null)
        {
            log.Log(LogLevel.Error, "RavenDB unexpectedly returned `null` for groups query for user {UserId}", userId);
            return ImmutableArray<Domain.Group>.Empty;
        }

        return groups.Select(db => new Domain.GroupInfo(db.Id!, db.Name!, db.HasTimeline ?? false)).ToImmutableArray();
    }

    #endregion

    #region Writing

    public async Task JoinWithExistingCharacterAsync(ClaimsPrincipal principal, Domain.Group group, Domain.Character character)
    {
        var userId = await userRepository.GetUserIdAsync(principal);

        if (!character.Id.StartsWith(RavenCharacterRepository.CharacterDocIdPrefix(userId)))
        {
            throw new DataAccessControlException(character.Id, userId);
        }

        using var session = store.OpenAsyncSession();
        await joinGroupAsync(group.Id, userId, character.Id, session);
        await session.SaveChangesAsync();
    }

    public async Task JoinWithNewCharacterAsync(ClaimsPrincipal principal, Domain.Group group, string characterName)
    {
        var userId = await userRepository.GetUserIdAsync(principal);
        var characterDocIdPrefix = RavenCharacterRepository.CharacterDocIdPrefix(userId);

        using var session = store.OpenAsyncSession();
        var newCharacter = new Domain.Character(null!, characterName, 10, 10).ToDbModel();
        await session.StoreAsync(newCharacter, characterDocIdPrefix);
        await session.SaveChangesAsync();
        try
        {
            await joinGroupAsync(group.Id, userId, newCharacter.Id, session);
            await session.SaveChangesAsync();
        }
        catch (Exception)
        {
            try
            {
                session.Delete(newCharacter.Id);
                await session.SaveChangesAsync();
            }
            catch (Exception followUp)
            {
                log.Log(LogLevel.Error,
                    followUp,
                    "Error while rolling back ad-hoc created character {CharacterId} for joining group {GroupId}",
                    newCharacter.Id,
                    group.Id);
            }

            throw;
        }
    }

    async Task joinGroupAsync(string groupId, string userId, string characterId, IAsyncDocumentSession session)
    {
        var group = await session.LoadAsync<Model.Group>(groupId);
        if (group == null)
        {
            throw new NotFoundException($"Cannot join group {groupId} because it does not exist.");
        }
        
        if (group.Members.Any(m => m.UserId == userId))
        {
            log.Log(LogLevel.Information, "User is already a member of group {GroupId}", groupId);
        }
        else
        {
            group.Members.Add(new() { UserId = userId, Role = Model.GroupRole.Member });
        }
        
        if (group.CharacterIds.Contains(characterId))
        {
            log.Log(LogLevel.Warning, "Character {CharacterId} is already a member of group {GroupId}", characterId, groupId);
        }
        else
        {
            group.CharacterIds.Add(characterId);
        }

        enforceGroupInvariants(group);
        log.Log(LogLevel.Information,
            "User {UserId} joined group {GroupId} with character {CharacterId}",
            userId,
            groupId,
            characterId);
    }

    public async Task LeaveGroupAsync(ClaimsPrincipal principal, Domain.Group group, Domain.Character character)
    {
        var userId = await userRepository.GetUserIdAsync(principal);
        var characterDocIdPrefix = RavenCharacterRepository.CharacterDocIdPrefix(userId);
        using var session = store.OpenAsyncSession();
        var role = await accessGroupAsync(group.Id, userId, session);
        if (role == null)
        {
            throw new DataAccessControlException("User is not a member of the group.", group.Id, userId);
        }

        if (role != Model.GroupRole.GameMaster && !character.Id.StartsWith(characterDocIdPrefix))
        {
            throw new DataAccessControlException($"User is not allowed to remove character {character.Id} from group.", group.Id, userId);
        }
        
        var dbGroup = await session.LoadAsync<Model.Group>(group.Id);
        if (dbGroup.CharacterIds.Remove(character.Id))
        {
            await session.SaveChangesAsync();
            log.Log(LogLevel.Information, "Character {CharacterId} left group {GroupId}", character.Id, group.Id);
        }
        else
        {
            log.Log(LogLevel.Warning,
                "Character {CharacterId} was not a member of the group {GroupId} in the first place",
                character.Id,
                group.Id);
        }
    }

    void enforceGroupInvariants(Model.Group dbGroup)
    {
        // List of character IDs does not contain duplicates
        var characterIds = dbGroup.CharacterIds;
        if (characterIds.Count != characterIds.Distinct().Count())
        {
            throw new InvalidOperationException(
                $"Group {dbGroup.Id} has duplicate character IDs in list of characters.");
        }
        
        // List of members does not contain duplicates
        var members = dbGroup.Members;
        if (members.Count != members.Distinct().Count())
        {
            throw new InvalidOperationException(
                $"Group {dbGroup.Id} has duplicate members in list of members.");
        }
    }

    #endregion

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await new Group_ByJoinCode().ExecuteAsync(store, token: cancellationToken);
        await new Group_ByUserId().ExecuteAsync(store, token: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal static async Task<Domain.Group> LoadGroupAsync(IAsyncDocumentSession session, string groupId)
    {
        var dbGroup = await session
            .LoadAsync<Model.Group>(groupId);
        var characters = await session.LoadAsync<Character>(dbGroup.CharacterIds);
        var timelineId = await session.Query<Timeline_ByGroup.IndexEntry, Timeline_ByGroup>()
            .Where(s => s.GroupId == groupId)
            .Select(s => s.Id)
            .Distinct()
            .SingleOrDefaultAsync();
        return GroupModelMapper.ToDomain(dbGroup, characters.Values, timelineId != null);
    }
}