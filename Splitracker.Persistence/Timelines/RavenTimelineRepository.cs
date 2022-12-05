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
using Raven.Client.Documents.Session;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Characters;
using Splitracker.Persistence.Model;
using Group = Splitracker.Persistence.Model.Group;
using Timeline = Splitracker.Persistence.Model.Timeline;

namespace Splitracker.Persistence.Timelines;

class RavenTimelineRepository : ITimelineRepository, IHostedService
{
    internal const string CollectionName = "Timelines";
    readonly IDocumentStore store;
    readonly ILogger<RavenTimelineRepository> log;
    readonly IUserRepository userRepository;

    public RavenTimelineRepository(
        IDocumentStore store,
        ILogger<RavenTimelineRepository> log,
        IUserRepository userRepository
    )
    {
        this.store = store;
        this.log = log;
        this.userRepository = userRepository;
    }

    #region Reading

    readonly ConcurrentDictionary<string, Task<RavenTimelineSubscription>> handles = new();

    public async Task<ITimelineHandle?> OpenSingleAsync(ClaimsPrincipal principal, string groupId)
    {
        var userId = await userRepository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession();
        var timelineId = await accessTimelineAsync(groupId, userId, session);
        if (timelineId == null)
        {
            return null;
        }

        return await handles.TryCreateSubscription(
            timelineId,
            createSubscription: async () => await RavenTimelineSubscription.OpenAsync(store, timelineId, log),
            onExistingSubscription: () =>
                log.Log(LogLevel.Debug, "Trying to join existing timeline subscription {TimelineId}", timelineId),
            tryGetHandle: s => s.TryGetHandle(),
            onRetry: () =>
                log.Log(LogLevel.Information, "Timeline subscription for {TimelineId} was disposed of, retrying",
                    timelineId)
        ) ?? throw new InvalidOperationException("Failed to open a handle for the timeline.");
    }

    async Task<string?> accessTimelineAsync(string groupId, string userId, IAsyncDocumentSession session)
    {
        log.LogInformation("Checking access to timeline of group {GroupId} for user {UserId}", groupId, userId);
        return await session.Query<Timeline_ByGroup.IndexEntry, Timeline_ByGroup>()
            .Where(t => t.GroupId == groupId && t.MemberUserId == userId)
            .Select(t => t.Id)
            .SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<Character>> SearchCharactersAsync(
        string searchTerm,
        string groupId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken
    )
    {
        var userId = await userRepository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession();
        var timelineId = await accessTimelineAsync(groupId, userId, session);
        if (timelineId == null)
        {
            return Enumerable.Empty<Character>();
        }

        var timeline = await LoadTimelineAsync(session, timelineId);
        var dbCharacters = await session.Advanced.AsyncDocumentQuery<CharacterModel, Character_ByName>()
            .WhereStartsWith(x => x.Id, RavenCharacterRepository.CharacterDocIdPrefix(userId))
            .Not.WhereIn(c => c.Id, timeline.Characters.Keys)
            .Search(c => c.Name, $"{searchTerm}*")
            .Take(100)
            .ToListAsync(cancellationToken);
        if (dbCharacters == null)
        {
            log.Log(LogLevel.Warning, "Unexpectedly got `null` from search query for characters.");
            return Enumerable.Empty<Character>();
        }

        log.Log(LogLevel.Debug, "Searching for characters for timeline {TimelineId} returned {Count} results. UserId={UserId}",
            timelineId, dbCharacters.Count, userId);
        return dbCharacters.Select(c => c.ToDomain()).OrderBy(c => c.Name).ToImmutableArray();
    }
    
    #endregion

    #region Writing

    public Task ApplyAsync(ClaimsPrincipal principal, ITimelineCommand groupCommand)
    {
        throw new NotImplementedException();
    }

    #endregion

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await new Timeline_ByGroup().ExecuteAsync(store, token: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    internal static async Task<Domain.Timeline> LoadTimelineAsync(IAsyncDocumentSession session, string timelineId)
    {
        var dbTimeline = await session
            .LoadAsync<Timeline>(timelineId);
        var group = await session.LoadAsync<Group>(dbTimeline.GroupId);
        var characters = await session.LoadAsync<CharacterModel>(
            dbTimeline.Ticks
                .Select(k => k.CharacterId)
                .Where(cid => cid != null)
                .Concat(dbTimeline.ReadyCharacterIds)
                .Concat(group.CharacterIds));
        return TimelineModelMapper.ToDomain(dbTimeline, group, characters.Values);
    }
}