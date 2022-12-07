﻿using System;
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
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Characters;
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
        if (await accessTimelineAsync(groupId, userId, session) is not var (timelineId, _))
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

    async Task<(string TimelineId, GroupRole Role)?> accessTimelineAsync(string groupId, string userId, IAsyncDocumentSession session)
    {
        log.LogInformation("Checking access to timeline of group {GroupId} for user {UserId}", groupId, userId);
        var result = await session.Query<Timeline_ByGroup.IndexEntry, Timeline_ByGroup>()
            .Where(t => t.GroupId == groupId && t.MemberUserId == userId)
            .Select(t => new { t.Id, t.MemberRole })
            .SingleOrDefaultAsync();
        return result == null ? null : (result.Id, result.MemberRole);
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
        
        if (await accessTimelineAsync(groupId, userId, session) is not var (timelineId, _))
        {
            return Enumerable.Empty<Character>();
        }

        var timeline = await LoadTimelineAsync(session, timelineId);
        var dbCharacters = await session.Advanced.AsyncDocumentQuery<CharacterModel, Character_ByName>()
            .WhereStartsWith(x => x.Id, RavenCharacterRepository.CharacterDocIdPrefix(userId))
            .Not.WhereIn(c => c.Id, timeline.Characters.Keys)
            .Search(c => c.Name, $"{searchTerm}*", SearchOperator.And)
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

    public async Task ApplyAsync(ClaimsPrincipal principal, TimelineCommand command)
    {
        var userId = await userRepository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession();
        if (await accessTimelineAsync(command.GroupId, userId, session) is not var (timelineId, _)){
            throw new ArgumentException($"User {userId} does not have access to timeline of group {command.GroupId}");
        }

        var dbTimeline = await session.LoadAsync<Model.Timeline>(timelineId);
        if (dbTimeline == null)
        {
            throw new ArgumentException($"Timeline {timelineId} does not exist.");
        }

        switch (command)
        {
            case TimelineCommand.CharacterCommand cc:
                applyCharacterCommand(cc, dbTimeline);
                break;
            case TimelineCommand.EffectCommand ec:
                applyEffectCommand(ec, dbTimeline);
                break;
            default:
                throw new ArgumentException($"Unsupported command type {command.GetType().Name}");
        }
        
        enforceTimelineInvariants(dbTimeline);
        await session.SaveChangesAsync();
    }

    void applyEffectCommand(TimelineCommand.EffectCommand command, Model.Timeline dbTimeline)
    {
        var effectId = command.EffectId;

        if (command is TimelineCommand.RemoveEffectTick { At: var tickAt })
        {
            dbTimeline.Ticks.RemoveAll(t => t.EffectId == effectId && t.At == tickAt);
            return;
        }
        
        dbTimeline.Ticks.RemoveAll(t => t.EffectId == effectId);
        dbTimeline.Effects.RemoveAll(e => e.Id == effectId);

        switch (command)
        {
            case TimelineCommand.AddEffect e:
                var endsAt = e.StartsAt + e.TotalDuration;
                log.Log(LogLevel.Debug, "Add effect {EffectId} to timeline {TimelineId} at tick {Tick}", 
                    effectId,
                    dbTimeline.Id, endsAt);
                dbTimeline.Effects.Add(new() {
                    Id = effectId,
                    Description = e.Description,
                    AffectedCharacterIds = e.AffectedCharacterIds.ToList(),
                    StartsAt = e.StartsAt,
                    TotalDuration = e.TotalDuration,
                    TickInterval = e.TickInterval,
                });
                dbTimeline.Ticks.Insert(offsetOfTick(dbTimeline, endsAt), new() {
                    EffectId = effectId,
                    Type = TickType.EffectEnds,
                    At = endsAt,
                    TotalDuration = e.TotalDuration,
                });
                if (e.TickInterval is { } interval)
                {
                    var nextTickAt = e.StartsAt;
                    do
                    {
                        dbTimeline.Ticks.Insert(offsetOfTick(dbTimeline, nextTickAt),
                            new() {
                                EffectId = effectId,
                                Type = TickType.EffectTicks,
                                At = nextTickAt,
                            });
                        nextTickAt += interval;
                    } while (nextTickAt < endsAt);
                }
                break;
            case TimelineCommand.RemoveEffect:
                log.Log(LogLevel.Debug, "Remove effect {EffectId} from timeline {TimelineId}", 
                    effectId,
                    dbTimeline.Id);
                // Already done
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    void applyCharacterCommand(TimelineCommand.CharacterCommand command, Model.Timeline dbTimeline)
    {
        var characterId = command.CharacterId;
        var removedFromReadyCharacters = dbTimeline.ReadyCharacterIds.Remove(characterId);
        var (previousCharacterTick, previousCharacterPosition) = dbTimeline.Ticks
            .Zip(Enumerable.Range(0, dbTimeline.Ticks.Count))
            .FirstOrDefault(t => t.First.CharacterId == characterId);
        var removedFromTicks = dbTimeline.Ticks.RemoveAll(t => t.CharacterId == characterId);
        var characterWasAlreadyPartOfTimeline = removedFromTicks > 0 || removedFromReadyCharacters;
        if (!characterWasAlreadyPartOfTimeline && command is not TimelineCommand.AddCharacter)
        {
            throw new ArgumentException($"Character {characterId} is not part of timeline {dbTimeline.Id}.");
        }

        switch (command)
        {
            case TimelineCommand.BumpCharacter { Direction: var delta and < 0 }:
                log.Log(LogLevel.Debug, "Bump character {CharacterId} back by {Delta} positions/ticks", characterId, delta);
                delta = Math.Abs(delta);
                var destIndex = previousCharacterPosition;
                while (delta > 0)
                {
                    if (destIndex - 1 < 0 || dbTimeline.Ticks[destIndex - 1].At < previousCharacterTick.At)
                    {
                        previousCharacterTick.At -= 1;
                    }
                    else
                    {
                        destIndex -= 1;
                    }
                    delta -= 1;
                }
                dbTimeline.Ticks.Insert(destIndex, previousCharacterTick);
                break;
            case TimelineCommand.BumpCharacter { Direction: var delta and > 0}:
                log.Log(LogLevel.Debug, "Bump character {CharacterId} forward by {Delta} positions/ticks", characterId, delta);
                destIndex = previousCharacterPosition;
                while (delta > 0)
                {
                    if (destIndex >= dbTimeline.Ticks.Count || dbTimeline.Ticks[destIndex].At > previousCharacterTick.At)
                    {
                        previousCharacterTick.At += 1;
                    }
                    else
                    {
                        destIndex += 1;
                    }
                    delta -= 1;
                }
                dbTimeline.Ticks.Insert(destIndex, previousCharacterTick);
                break;
            case TimelineCommand.AddCharacter { At: { } newTick }:
                log.Log(LogLevel.Debug, "Newly added character {CharacterId} joins timeline {TimelineId} at tick {Tick}",
                    characterId, dbTimeline.Id, newTick);
                dbTimeline.Ticks.Insert(offsetOfTick(dbTimeline, newTick, 0),
                    new() {
                        Type = TickType.Recovers,
                        At = newTick,
                        CharacterId = characterId,
                    });
                break;
            case TimelineCommand.AddCharacter { At: null }:
                log.Log(LogLevel.Debug, "Newly added character {CharacterId} is now ready in timeline {TimelineId}",
                    characterId, dbTimeline.Id);
                dbTimeline.ReadyCharacterIds.Add(characterId);
                break;
            case TimelineCommand.RemoveCharacter:
                log.Log(LogLevel.Debug, "Character {CharacterId} gets removed from timeline {TimelineId}",
                    characterId, dbTimeline.Id);
                // already done
                break;
            case TimelineCommand.SetCharacterActionEnded { At: var newTick, Description: var desc, TotalDuration: var duration }:
                log.Log(LogLevel.Debug, "Character {CharacterId} ends action in timeline {TimelineId} at tick {Tick}",
                    characterId, dbTimeline.Id, newTick);
                dbTimeline.Ticks.Insert(offsetOfTick(dbTimeline, newTick),
                    new() {
                        Type = TickType.ActionEnds,
                        At = newTick,
                        CharacterId = characterId,
                        Description = desc,
                        TotalDuration = duration,
                    });
                break;
            case TimelineCommand.SetCharacterReady:
                log.Log(LogLevel.Debug, "Character {CharacterId} is now ready in timeline {TimelineId}",
                    characterId, dbTimeline.Id);
                dbTimeline.ReadyCharacterIds.Add(characterId);
                break;
            case TimelineCommand.SetCharacterRecovered { At: var newTick, PreemptPosition: var preempt }:
                log.Log(LogLevel.Debug, "Character {CharacterId} recovers in timeline {TimelineId} at tick {Tick}",
                    characterId, dbTimeline.Id, newTick);
                dbTimeline.Ticks.Insert(offsetOfTick(dbTimeline, newTick, preempt),
                    new() {
                        Type = TickType.Recovers,
                        At = newTick,
                        CharacterId = characterId,
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    int offsetOfTick(Model.Timeline dbTimeline, int tick, int? preempt = null)
    {
        var offset = 0;
        while (offset < dbTimeline.Ticks.Count && dbTimeline.Ticks[offset].At < tick)
        {
            offset+=1;
        }

        var lowerOffset = offset;
        while(offset < dbTimeline.Ticks.Count && dbTimeline.Ticks[offset].At == tick)
        {
            offset+=1;
        }
        var upperOffset = offset;

        return preempt switch {
            null => upperOffset,
            {} p => Math.Clamp(lowerOffset + p, lowerOffset, upperOffset),
        };
    }

    void enforceTimelineInvariants(Model.Timeline dbTimeline)
    {
        // Each character must only have a single action in the timeline
        var offendingCharacterIds = dbTimeline.Ticks
            .Where(t => t.CharacterId is not null)
            .Select(t => t.CharacterId)
            .Concat(dbTimeline.ReadyCharacterIds)
            .GroupBy(t => t)
            .Where(g => g.Count() > 1);
        foreach (var offendingCharacterId in offendingCharacterIds)
        {
            throw new TimelineInvariantViolationException(
                $"The character {offendingCharacterId} is part of the timeline multiple times.",
                dbTimeline.Id);
        }
        
        // Each effect must only have a single EffectEnds Tick in the timeline
        var offendingEffectTicks = dbTimeline.Ticks
            .Where(t => t.EffectId is not null)
            .GroupBy(t => t.EffectId)
            .Where(g => g.Count(t => t.Type == TickType.EffectEnds) > 1);
        foreach (var offendingEffectTick in offendingEffectTicks)
        {
            throw new TimelineInvariantViolationException(
                $"The effect {offendingEffectTick} has multiple EffectEnds ticks in the timeline.",
                dbTimeline.Id);
        }
        
        // Each effect must have an EffectEnds Tick in the timeline
        var offendingEffectIds = dbTimeline.Effects
            .Select(e => e.Id)
            .Where(e => !dbTimeline.Ticks.Any(t => t.EffectId == e && t.Type == TickType.EffectEnds));
        foreach (var offendingEffectId in offendingEffectIds)
        {
            throw new TimelineInvariantViolationException(
                $"The effect {offendingEffectId} has no EffectEnds tick in the timeline.",
                dbTimeline.Id);
        }
        
        // All EffectTicks must come before the EffectEnds Tick with the same EffectId
        var offendingEffectTickIds = dbTimeline.Ticks
            .Where(t => t.Type == TickType.EffectTicks 
                && dbTimeline.Ticks
                    .Where(ends => ends.Type == TickType.EffectEnds && ends.EffectId == t.EffectId)
                    .Any(ends => ends.At <= t.At)
                );
        foreach (var offendingEffectTickId in offendingEffectTickIds)
        {
            throw new TimelineInvariantViolationException(
                $"The EffectTicks tick {offendingEffectTickId} comes after the EffectEnds tick.",
                dbTimeline.Id);
        }

        // The ticks need to be sorted by `At`
        var previousTick = dbTimeline.Ticks.FirstOrDefault();
        foreach (var tick in dbTimeline.Ticks.Skip(1))
        {
            if (tick.At < previousTick!.At)
            {
                throw new TimelineInvariantViolationException(
                    $"The ticks of the timeline are not sorted by `At`.",
                    dbTimeline.Id);
            }
            previousTick = tick;
        }
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
            .LoadAsync<Model.Timeline>(timelineId);
        var group = await session.LoadAsync<Model.Group>(dbTimeline.GroupId);
        var characters = await session.LoadAsync<CharacterModel>(
            dbTimeline.Ticks
                .Select(k => k.CharacterId)
                .Where(cid => cid != null)
                .Concat(dbTimeline.ReadyCharacterIds)
                .Concat(group.CharacterIds));
        return TimelineModelMapper.ToDomain(dbTimeline, group, characters.Values);
    }
}