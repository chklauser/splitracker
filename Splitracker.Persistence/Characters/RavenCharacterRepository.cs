﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Queries;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Model;
using Character = Splitracker.Persistence.Model.Character;
using Pool = Splitracker.Persistence.Model.Pool;

namespace Splitracker.Persistence.Characters;

class RavenCharacterRepository : ICharacterRepository, IHostedService
{
    internal const string CollectionName = "Characters";

    readonly IDocumentStore store;
    readonly ILogger<RavenCharacterRepository> log;
    readonly IUserRepository userRepository;

    readonly ConcurrentDictionary<string, Task<RavenCharacterRepositorySubscription>> handles = new();

    public RavenCharacterRepository(
        IDocumentStore store,
        ILogger<RavenCharacterRepository> log,
        IUserRepository userRepository
    )
    {
        this.store = store;
        this.log = log;
        this.userRepository = userRepository;
    }

    bool isOwner(string characterId, string userId) =>
        characterId.StartsWith(CharacterDocIdPrefix(userId), StringComparison.Ordinal);

    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    public async Task ApplyAsync(ClaimsPrincipal principal, ICharacterCommand characterCommand)
    {
        var userId = await userRepository.GetUserIdAsync(principal);

        log.LogInformation("Applying character {Command} to {Oid}", characterCommand, userId);
        using var session = store.OpenAsyncSession();
        Character? model;
        var id = characterCommand.CharacterId;
        if (id != null)
        {
            if (!isOwner(id, userId))
            {
                throw new ArgumentException($"Character {id} does not belong to user {userId}.");
            }

            model = await session.LoadAsync<Character>(id);
            if (model == null)
            {
                log.Log(LogLevel.Warning, "Character {Id} not found.", id);
                return;
            }
        }
        else
        {
            model = null;
        }

        static void applyPointsTo(Pool model, Domain.Pool domain, PointsVec points, string? description)
        {
            var channeledBefore = model.Points.Channeled;
            // First apply point removal ("healing")
            if (points.Channeled < 0)
            {
                model.Points.Channeled = Math.Max(0,
                    Math.Min(domain.TotalCapacity - model.Points.Exhausted - model.Points.Consumed,
                        model.Points.Channeled + points.Channeled));
            }

            if (points.Exhausted < 0)
            {
                model.Points.Exhausted = Math.Max(0,
                    Math.Min(domain.TotalCapacity - model.Points.Channeled - model.Points.Consumed,
                        model.Points.Exhausted + points.Exhausted));
            }

            // Only then apply point addition ("damage") and start with the "worst" type: consume.
            // working our way down the list until the capacity is filled.
            model.Points.Consumed = Math.Max(0,
                Math.Min(domain.TotalCapacity - model.Points.Exhausted - model.Points.Channeled,
                    model.Points.Consumed + points.Consumed));

            if (points.Exhausted > 0)
            {
                model.Points.Exhausted = Math.Max(0,
                    Math.Min(domain.TotalCapacity - model.Points.Channeled - model.Points.Consumed,
                        model.Points.Exhausted + points.Exhausted));
            }

            if (points.Channeled > 0)
            {
                model.Points.Channeled = Math.Max(0,
                    Math.Min(domain.TotalCapacity - model.Points.Exhausted - model.Points.Consumed,
                        model.Points.Channeled + points.Channeled));
            }

            // Note how much was channeled in this change
            var effectivelyChanneled = model.Points.Channeled - channeledBefore;
            if (effectivelyChanneled > 0)
            {
                model.Channelings.Add(new()
                {
                    Id = IdGenerator.RandomId(),
                    Value = effectivelyChanneled, 
                    Description = description,
                });
            }
        }

        void stopChanneling(Pool model, Domain.Pool domain, string channelingId)
        {
            var channeling = model.Channelings.FirstOrDefault(ch => ch.Id == channelingId);
            if (channeling == null)
            {
                log.Log(LogLevel.Warning, "Character {Id} doesn't have an Lp channeling with ID {ChannelingId}", id, channelingId);
            }
            else
            {
                model.Channelings.Remove(channeling);
                var points = channeling.Value;
                applyPointsTo(model, domain, new(-points, points, 0), null);
            }
        }

        switch (characterCommand)
        {
            case ApplyPoints { Pool: PoolType.Lp, Points: var points, Description: var description }:
                applyPointsTo(model!.Lp, model.Lp.ToDomainLp(), points, description);
                break;
            case ApplyPoints { Pool: PoolType.Fo, Points: var points, Description: var description }:
                applyPointsTo(model!.Fo, model.Fo.ToDomainFo(), points, description);
                break;
            case CreateCharacter create:
                var newCharacter = new Character(CharacterDocIdPrefix(userId), create.Name,
                    new() { BaseCapacity = create.LpBaseCapacity },
                    new() { BaseCapacity = create.FoBaseCapacity }) {
                    SplinterPoints = new() { Max = create.SplinterPointsMax, Used = 0 },
                    ActionShorthands = create.ActionShorthands.Values
                        .OrderBy(v => v.Id)
                        .Select(x => x.ToDbModel())
                        .ToList(),
                    CustomColor = create.CustomColor,
                    IsOpponent = create.IsOpponent,
                };
                await session.StoreAsync(newCharacter);
                break;
            case EditCharacter edit:
                model!.Name = edit.Name;
                model.Lp.BaseCapacity = edit.LpBaseCapacity;
                model.Fo.BaseCapacity = edit.FoBaseCapacity;
                model.SplinterPoints.Max = edit.SplinterPointsMax;
                model.CustomColor = edit.CustomColor;
                model.IsOpponent = edit.IsOpponent;
                model.ActionShorthands = edit.ActionShorthands
                    .Values
                    .OrderBy(x => x.Id)
                    .Select(x => x.ToDbModel())
                    .ToList();
                break;
            case StopChanneling { Pool: PoolType.Lp, Id: var channelingId }:
                stopChanneling(model!.Lp, model.Lp.ToDomainLp(), channelingId);
                break;
            case StopChanneling { Pool: PoolType.Fo, Id: var channelingId }:
                stopChanneling(model!.Fo, model.Fo.ToDomainFo(), channelingId);
                break;
            case UseSplinterPoints { Amount: var amount }:
                model!.SplinterPoints.Used = Math.Max(0, Math.Min(model.SplinterPoints.Max, model.SplinterPoints.Used + amount));
                break;
            case ResetSplinterPoints:
                model!.SplinterPoints.Used = 0;
                break;
            case ShortRest:
                model!.Lp.Points.Exhausted = 0;
                model.Fo.Points.Exhausted = 0;
                break;
            case DeleteCharacter:
                session.Delete(model);
                break;
            case CloneCharacter:
                await session.StoreAsync(new Domain.Character(
                    id: CharacterDocIdPrefix(userId),
                    name: model!.Name,
                    lpBaseCapacity: model.Lp.BaseCapacity,
                    foBaseCapacity: model.Fo.BaseCapacity,
                    customColor: model.CustomColor,
                    isOpponent: model.IsOpponent,
                    actionShorthands: model.ActionShorthands.ToImmutableDictionary(
                        s => s.Id, 
                        s => s.ToDomain()
                        )
                    ).ToDbModel());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(characterCommand));
        }

        await session.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Domain.Character>> SearchCharactersAsync(ClaimsPrincipal principal, string searchTerm, CancellationToken cancellationToken)
    {
        var userId = await userRepository.GetUserIdAsync(principal);
        
        using var session = store.OpenAsyncSession();
        var dbCharacters = await session.Advanced.AsyncDocumentQuery<Character, Character_ByName>()
            .WhereStartsWith(x => x.Id, RavenCharacterRepository.CharacterDocIdPrefix(userId))
            .Search(c => c.Name, $"{searchTerm}*", SearchOperator.And)
            .Take(100)
            .ToListAsync(cancellationToken);
        if (dbCharacters == null)
        {
            log.Log(LogLevel.Warning, "Unexpectedly got `null` from search query for characters.");
            return ImmutableArray<Domain.Character>.Empty;
        }

        log.Log(LogLevel.Debug, "Searching for characters returned {Count} results. UserId={UserId}",
            dbCharacters.Count, userId);
        return dbCharacters.Select(c => c.ToDomain()).OrderBy(c => c.Name).ToImmutableArray();
    }

    public async Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        var userId = await userRepository.GetUserIdAsync(principal);

        return await handles.TryCreateSubscription(
            userId,
            createSubscription: async () => await RavenCharacterRepositorySubscription.OpenAsync(store, userId, log),
            onExistingSubscription: () =>
                log.Log(LogLevel.Debug, "Trying to join existing character subscription for {UserId}", userId),
            tryGetHandle: s => s.TryGetHandle(),
            onRetry: () => log.Log(LogLevel.Information, "Subscription for {UserId} was disposed of. Retrying.", userId)
        ) ?? throw new InvalidOperationException("Failed to open a handle.");
    }

    internal static string CharacterDocIdPrefix(string userId)
    {
        return $"{CollectionName}/{userId["Users/".Length..]}/";
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await new Character_ByName().ExecuteAsync(store, token: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}