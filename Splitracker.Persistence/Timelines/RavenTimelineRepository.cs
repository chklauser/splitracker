using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Model;

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
        log.LogInformation("Checking access to timeline of group {GroupId} for user {UserId}", groupId, userId);
        var timelineId = await session.Query<Timeline_ByGroup.IndexEntry, Timeline_ByGroup>()
            .Where(t => t.GroupId == groupId && t.MemberUserId == userId)
            .Select(t => t.Id)
            .SingleOrDefaultAsync();
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
}