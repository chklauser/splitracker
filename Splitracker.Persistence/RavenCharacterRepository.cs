using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Model;

namespace Splitracker.Persistence;

class RavenCharacterRepository : ICharacterRepository
{
    internal const string CollectionName = "Characters";
    const string OidClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    readonly IDocumentStore store;
    readonly ILogger<RavenCharacterRepository> log;

    readonly ConcurrentDictionary<string, Task<RavenCharacterRepositorySubscription>> handles = new();

    public RavenCharacterRepository(IDocumentStore store, ILogger<RavenCharacterRepository> log)
    {
        this.store = store;
        this.log = log;
    }

    bool isOwner(string characterId, string oid) => 
        characterId.StartsWith(CharacterDocIdPrefix(oid), StringComparison.Ordinal);

    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    public async Task ApplyAsync(ClaimsPrincipal principal, ICharacterCommand characterCommand)
    {
        var oid = oidOf(principal);

        log.LogInformation("Applying character {Command} to {Oid}", characterCommand, oid);
        using var session = store.OpenAsyncSession();
        CharacterModel? model;
        var id = characterCommand.CharacterId;
        if (id != null)
        {
            if (!isOwner(id, oid))
            {
                throw new ArgumentException($"Character {id} does not belong to user {oid}.");
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
            case ApplyPoints { Pool: PoolType.Fo, Points: var points}:
                applyPointsTo(model!.Fo, model.Fo.ToDomainFo(), points);
                break;
            case CreateCharacter create:
                var newCharacter = new CharacterModel(CharacterDocIdPrefix(oid), create.Name,
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
                await session.StoreAsync(new Character(CharacterDocIdPrefix(oid), model!.Name, model.Lp.BaseCapacity,
                    model.Fo.BaseCapacity).ToDbModel());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(characterCommand));
        }

        await session.SaveChangesAsync();
    }

    public async Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        var oid = oidOf(principal);

        var remainingTries = 5;
        while (remainingTries-- > 0)
        {
            var ourTask = new TaskCompletionSource<RavenCharacterRepositorySubscription>();
            var ourSubscription = ourTask.Task;
            var installedSubscription = handles.GetOrAdd(oid, ourSubscription);
            if (installedSubscription == ourSubscription)
            {
                try
                {
                    var subscription = await RavenCharacterRepositorySubscription.OpenAsync(store, oid, log);
                    ourTask.SetResult(subscription);
                }
                catch (Exception ex)
                {
                    ourTask.SetException(ex);
                    handles.TryRemove(oid, out _);
                    throw;
                }
            }
            else
            {
                log.Log(LogLevel.Debug, "Trying to join existing subscription for {Oid}", oid);
            }

            if ((await installedSubscription).TryGetHandle() is { } handle)
            {
                return handle;
            }
            else
            {
                // The subscription has been disposed of. Clear it from the dictionary (but only if it matches)
                // and try again.
                log.Log(LogLevel.Information, "Subscription for {Oid} was disposed of. Retrying.", oid);
                handles.TryRemove(new(oid, installedSubscription));
            }
        }
        
        throw new InvalidOperationException("Failed to open a handle.");
    }

    static string oidOf(ClaimsPrincipal principal)
    {
        var oid = principal.Claims.FirstOrDefault(c => c.Type == OidClaimType)?.Value ??
            throw new ArgumentException("Principal does not have an oid claim.", nameof(principal));
        return oid;
    }

    internal static string CharacterDocIdPrefix(string oid)
    {
        return $"{CollectionName}/{oid}/";
    }
}