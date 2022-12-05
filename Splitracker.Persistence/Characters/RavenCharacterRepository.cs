using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Model;

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
        CharacterModel? model;
        var id = characterCommand.CharacterId;
        if (id != null)
        {
            if (!isOwner(id, userId))
            {
                throw new ArgumentException($"Character {id} does not belong to user {userId}.");
            }

            model = await session.LoadAsync<CharacterModel>(id);
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

        static void applyPointsTo(PoolModel model, Pool domain, PointsVec points)
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
                model.Channelings.Add(effectivelyChanneled);
            }
        }

        void stopChanneling(PoolModel model, Pool domain, int points)
        {
            var idx = model.Channelings.LastIndexOf(points);
            if (idx < 0)
            {
                log.Log(LogLevel.Warning, "Character {Id} doesn't have an Lp channeling valued {Points}", id, points);
            }

            model.Channelings.RemoveAt(idx);
            applyPointsTo(model, domain, new(-points, points, 0));
        }

        switch (characterCommand)
        {
            case ApplyPoints { Pool: PoolType.Lp, Points: var points }:
                applyPointsTo(model!.Lp, model.Lp.ToDomainLp(), points);
                break;
            case ApplyPoints { Pool: PoolType.Fo, Points: var points }:
                applyPointsTo(model!.Fo, model.Fo.ToDomainFo(), points);
                break;
            case CreateCharacter create:
                var newCharacter = new CharacterModel(CharacterDocIdPrefix(userId), create.Name,
                    new() { BaseCapacity = create.LpBaseCapacity },
                    new() { BaseCapacity = create.FoBaseCapacity });
                await session.StoreAsync(newCharacter);
                break;
            case EditCharacter edit:
                model!.Name = edit.Name;
                model.Lp.BaseCapacity = edit.LpBaseCapacity;
                model.Fo.BaseCapacity = edit.FoBaseCapacity;
                break;
            case StopChanneling { Pool: PoolType.Lp, Points: var points }:
                stopChanneling(model!.Lp, model.Lp.ToDomainLp(), points);
                break;
            case StopChanneling { Pool: PoolType.Fo, Points: var points }:
                stopChanneling(model!.Fo, model.Fo.ToDomainFo(), points);
                break;
            case ShortRest:
                model!.Lp.Points.Exhausted = 0;
                model.Fo.Points.Exhausted = 0;
                break;
            case DeleteCharacter:
                session.Delete(model);
                break;
            case CloneCharacter:
                await session.StoreAsync(new Character(CharacterDocIdPrefix(userId), model!.Name, model.Lp.BaseCapacity,
                    model.Fo.BaseCapacity).ToDbModel());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(characterCommand));
        }

        await session.SaveChangesAsync();
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