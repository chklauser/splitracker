using System;
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
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Generic;
using Splitracker.Persistence.Model;
using Character = Splitracker.Persistence.Model.Character;
using Pool = Splitracker.Persistence.Model.Pool;

namespace Splitracker.Persistence.Characters;

class RavenCharacterRepository(
    IDocumentStore store,
    ILogger<RavenCharacterRepository> log,
    IUserRepository repository,
    NameGenerationService nameGeneration
)
    : ICharacterRepository, IHostedService
{
    internal const string CollectionName = "Characters";

    readonly ConcurrentDictionary<string, Task<RavenCharacterRepositorySubscription>> handles = new();

    bool isOwner(string characterId, string userId) =>
        characterId.StartsWith(CharacterDocIdPrefix(userId), StringComparison.Ordinal);

    public async Task<string?> FullCharacterIdFromImplicitAsync(ClaimsPrincipal principal, string implicitId)
    {
        var userId = await repository.GetUserIdAsync(principal);
        return $"{CharacterDocIdPrefix(userId)}{implicitId}";
    }

    [SuppressMessage("ReSharper", "VariableHidesOuterVariable")]
    public async Task ApplyAsync(ClaimsPrincipal principal, ICharacterCommand characterCommand)
    {
        var userId = await repository.GetUserIdAsync(principal);

        log.LogInformation("Applying character {Command} to {Oid}", characterCommand, userId);
        using var session = store.OpenAsyncSession();
        Character? model;
        Character? template = null;
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

            if (model.TemplateId is { } templateId)
            {
                template = await session.LoadAsync<Character>(templateId);
                if (template == null)
                {
                    log.Log(LogLevel.Warning, "Character {Id} has template {TemplateId} which was not found.", id,
                        templateId);
                    return;
                }
            }
        }
        else
        {
            model = null;
        }

        static void applyPointsTo(Pool model, Domain.Pool domain, PointsVec points, string? description)
        {
            model.Points ??= new();
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
                model.Channelings ??= [];
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
            var channeling = model.Channelings?.FirstOrDefault(ch => ch.Id == channelingId);
            if (channeling == null)
            {
                log.Log(LogLevel.Warning, "Character {Id} doesn't have an Lp channeling with ID {ChannelingId}", id, channelingId);
            }
            else
            {
                model.Channelings?.Remove(channeling);
                var points = channeling.Value;
                applyPointsTo(model, domain, new(-points, points, 0), null);
            }
        }

        switch (characterCommand)
        {
            case ApplyPoints { Pool: PoolType.Lp, Points: var points, Description: var description }:
                applyPointsTo(model!.Lp, model.Lp.ToDomainLp(template?.Lp.BaseCapacity ?? 1), points, description);
                break;
            case ApplyPoints { Pool: PoolType.Fo, Points: var points, Description: var description }:
                applyPointsTo(model!.Fo, model.Fo.ToDomainFo(template?.Fo.BaseCapacity ?? 1), points, description);
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
                    TagIds = create.TagIds.ToList(),
                    InsertedAt = DateTimeOffset.UtcNow,
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
                model.TagIds = edit.TagIds.Order().ToList();
                break;
            case EditCharacterInstance edit:
                model!.Name = edit.Name;
                break;
            case CreateCharacterInstance create:
                if (!isOwner(create.TemplateId, userId))
                {
                    log.Log(LogLevel.Warning, "Template {TemplateId} does not belong to user {UserId}.", create.TemplateId, userId);
                    return;
                }

                if (create is not { Name: { } instanceName })
                {
                    var otherInstanceNames = await session.Query<Character, Character_ByTemplateId>()
                        .Customize(opt => opt.NoTracking())
                        .Where(c => c.TemplateId == create.TemplateId)
                        .Select(c => c.Name)
                        .ToListAsync();
                    var scheme = nameGeneration.InferNamingScheme(otherInstanceNames ?? []);
                    instanceName = scheme.GenerateNext();
                }
                
                await session.StoreAsync(new Character(CharacterDocIdPrefix(userId), instanceName, new(), new()) {
                    TemplateId = create.TemplateId,
                    InsertedAt = DateTimeOffset.UtcNow,
                });
                break;
            case UnlinkFromTemplate unlink:
                session.Advanced.Evict(model);
                await unlinkAsync(session, model, unlink.Name, template);
                break;
            case StopChanneling { Pool: PoolType.Lp, Id: var channelingId }:
                stopChanneling(model!.Lp, model.Lp.ToDomainLp(template?.Lp.BaseCapacity ?? 1), channelingId);
                break;
            case StopChanneling { Pool: PoolType.Fo, Id: var channelingId }:
                stopChanneling(model!.Fo, model.Fo.ToDomainFo(template?.Fo.BaseCapacity ?? 1), channelingId);
                break;
            case UseSplinterPoints { Amount: var amount }:
                model!.SplinterPoints.Used = Math.Max(
                    0, Math.Min(
                        model.SplinterPoints.Max ?? template?.SplinterPoints.Max ?? 0, 
                        model.SplinterPoints.Used + amount));
                break;
            case ResetSplinterPoints:
                model!.SplinterPoints.Used = 0;
                break;
            case ShortRest:
                model!.Lp.Points ??= new();
                model.Lp.Points.Exhausted = 0;
                model.Fo.Points ??= new();
                model.Fo.Points.Exhausted = 0;
                break;
            case DeleteCharacter:
                await foreach (var instance in session.Query<Character, Character_ByTemplateId>()
                    .Customize(opt => opt.NoTracking())
                    .Where(c => c.TemplateId == model!.Id)
                    .AsAsyncEnumerable())
                {
                    await unlinkAsync(session, instance, null, model);
                }
                session.Delete(model);
                break;
            case CloneCharacter:
                // Generate new name
                string newInstanceName;
                if (model!.TemplateId == null)
                {
                    var relatedNames = await byNameSearch(session, nameGeneration.InferTemplateName(model.Name), userId)
                        .SelectFields<string>(nameof(Character.Name))
                        .Take(100)
                        .ToListAsync();
                    var baseName = nameGeneration.InferTemplateName(model.Name);
                    var scheme = nameGeneration.InferNamingScheme(relatedNames ?? []);
                    newInstanceName = scheme.GenerateNext().Replace("\uFFFC", baseName);
                }
                else
                {
                    var existingNames = await session.Query<Character>()
                        .Customize(opt => opt.NoTracking())
                        .Where(c => c.Id.StartsWith(CharacterDocIdPrefix(userId)))
                        .Select(c => c.Name)
                        .ToListAsync();
                    var scheme = nameGeneration.InferNamingScheme(existingNames ?? []);
                    newInstanceName = scheme.GenerateNext();
                }
                
                await session.StoreAsync(
                    new Character(CharacterDocIdPrefix(userId), newInstanceName, 
                        new() {BaseCapacity = model.Lp.BaseCapacity},
                        new() {BaseCapacity = model.Fo.BaseCapacity}) {
                        CustomColor = model.CustomColor,
                        IsOpponent = model.IsOpponent,
                        ActionShorthands = model.ActionShorthands,
                        SplinterPoints = model.SplinterPoints,
                        TagIds = model.TagIds,
                        TemplateId = model.TemplateId,
                        InsertedAt = DateTimeOffset.UtcNow,
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(characterCommand));
        }

        await session.SaveChangesAsync();
    }

    static async Task unlinkAsync(
        IAsyncDocumentSession session,
        Character? model,
        string? newName,
        Character? template
    )
    {
        var flattened = model!.ToDomain(template!).ToDbModel();
        flattened.TemplateId = null;
        if (newName != null)
        {
            flattened.Name = newName;
        }

        await session.StoreAsync(flattened);
    }

    public async Task ApplyAsync(ClaimsPrincipal principal, DeleteTag deleteTagCommand)
    {
        var userId = await repository.GetUserIdAsync(principal);

        using var session = store.OpenAsyncSession(new SessionOptions()
            { TransactionMode = TransactionMode.ClusterWide });
        var affectedCharacters = await session.Query<Character>()
            .Where(c => 
                c.Id.StartsWith(CharacterDocIdPrefix(userId))
                && c.TagIds.Contains(deleteTagCommand.TagId))
            .ToListAsync();
        foreach (var character in affectedCharacters)
        {
            character.TagIds.Remove(deleteTagCommand.TagId);
        }
        await session.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Domain.Character>> SearchCharactersAsync(ClaimsPrincipal principal, string searchTerm, CancellationToken cancellationToken)
    {
        var userId = await repository.GetUserIdAsync(principal);
        
        using var session = store.OpenAsyncSession();
        var dbCharacters = await byNameSearch(session, searchTerm, userId)
            .ToListAsync(cancellationToken);
        if (dbCharacters == null)
        {
            log.Log(LogLevel.Warning, "Unexpectedly got `null` from search query for characters.");
            return ImmutableArray<Domain.Character>.Empty;
        }

        log.Log(LogLevel.Debug, "Searching for characters returned {Count} results. UserId={UserId}",
            dbCharacters.Count, userId);
        var templates = await FetchTemplatesAsync(session, dbCharacters);
        return dbCharacters.Select(c => c.ToDomain(templates)).OrderBy(c => c.Name).ToImmutableArray();
    }

    static IAsyncDocumentQuery<Character> byNameSearch(IAsyncDocumentSession session, string searchTerm, string userId)
    {
        return session.Advanced.AsyncDocumentQuery<Character, Character_ByName>()
            .WhereStartsWith(x => x.Id, CharacterDocIdPrefix(userId))
            .Search(c => c.Name, $"{searchTerm}*", SearchOperator.And)
            .Take(100);
    }

    internal static async Task<IReadOnlyDictionary<string, Character>> FetchTemplatesAsync(IAsyncDocumentSession session, IEnumerable<Character> characters)
    {
        var templateIds = characters.Select(c => c.TemplateId).OfType<string>().ToHashSet();
        if (templateIds.Count == 0)
        {
            return ImmutableDictionary<string, Character>.Empty;
        }
        return (await session.LoadAsync<Character>(templateIds))?.AsReadOnly()
            ?? (IReadOnlyDictionary<string, Character>)ImmutableDictionary<string, Character>.Empty;
    }

    public async Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        var userId = await repository.GetUserIdAsync(principal);

        return await handles.TryCreateSubscription(
            userId,
            createSubscription: async () =>
            {
                var docIdPrefix = RavenCharacterRepository.CharacterDocIdPrefix(userId);
                var characters = (await RepositorySubscriptionBase<RavenCharacterRepositorySubscription, Domain.Character, RavenCharacterHandle, Character, RavenCharacterRepositoryHandle>.FetchInitialAsync(store, docIdPrefix));

                log.Log(LogLevel.Information, 
                    "Initialized character repository subscription for {Oid}", 
                    userId);
                return new(store, docIdPrefix, characters, log);
            },
            onExistingSubscription: () =>
                log.Log(LogLevel.Debug, "Trying to join existing character subscription for {UserId}", userId),
            tryGetHandle: s => s.TryGetHandle(),
            onRetry: () => log.Log(LogLevel.Information, "Subscription for {UserId} was disposed of. Retrying.", userId)
        ) ?? throw new InvalidOperationException("Failed to open a handle.");
    }

    readonly ConcurrentDictionary<string, Task<RavenSingleCharacterSubscription>> singleHandles = new();

    public async Task<ICharacterHandle?> OpenSingleAsync(ClaimsPrincipal principal, string characterId)
    {
        var userId = await repository.GetUserIdAsync(principal);
        if (!characterId.StartsWith(CharacterDocIdPrefix(userId)))
        {
            return null;
        }

        using var session = store.OpenAsyncSession();
        return await singleHandles.TryCreateSubscription(characterId,
            async () => await RavenSingleCharacterSubscription.OpenAsync(store, characterId, log),
            onExistingSubscription: () => log.Log(LogLevel.Debug,
                "Trying to join existing single character subscription for {UserId}",
                userId),
            tryGetHandle: s => s.TryGetHandle(),
            onRetry: () => log.Log(LogLevel.Information,
                "Single character subscription for {UserId} was disposed of. Retrying.",
                userId)
        );
    }

    internal static string CharacterDocIdPrefix(string userId)
    {
        return $"{CollectionName}/{userId["Users/".Length..]}/";
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await new Character_ByName().ExecuteAsync(store, token: cancellationToken);
        await new Character_ByTemplateId().ExecuteAsync(store, token: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}