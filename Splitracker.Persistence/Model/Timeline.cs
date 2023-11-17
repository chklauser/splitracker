using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Splitracker.Domain;

namespace Splitracker.Persistence.Model;

class Timeline
{
    public string? Id { get; set; }
    public required string GroupId { get; set; }
    public required List<Effect> Effects { get; set; }
    public required List<string> ReadyCharacterIds { get; set; }
    public required List<Tick> Ticks { get; set; }
}

enum TickType
{
    Recovers,
    ActionEnds,
    EffectEnds,
    EffectTicks,
}

class Tick
{
    public required TickType Type { get; set; }
    public required int At { get; set; }
    public int? TotalDuration { get; set; }
    public string? CharacterId { get; set; }
    public string? EffectId { get; set; }
    public string? Description { get; set; }
}

class Effect
{
    public required string Id { get; set; }
    public required string Description { get; set; }
    public required int StartsAt { get; set; }
    public required int TotalDuration { get; set; }
    public int? TickInterval { get; set; }
    public required List<string> AffectedCharacterIds { get; set; }
}

static class TimelineModelMapper
{
    public static Domain.Timeline ToDomain(
        this Timeline timeline,
        Group group,
        IEnumerable<Character?> characters
    )
    {
        var charactersById = characters
            .Where(c => c != null)
            .ToImmutableDictionary(
            c => c!.Id,
            c => c!.ToDomain());
        var effectsById = timeline.Effects.ToImmutableDictionary(
            e => e.Id,
            e => e.toDomain(charactersById));
        var rolesByUserId = group.Members.ToImmutableDictionary(
            m => m.UserId,
            m => m.Role.ToDomain());
        return new(timeline.Id!,
            group.Id!,
            group.Name,
            rolesByUserId,
            charactersById,
            effectsById,
            timeline.ReadyCharacterIds
                .Select(cid => CollectionExtensions.GetValueOrDefault(charactersById, cid))
                .OfType<Domain.Character>()
                .ToImmutableArray(),
            timeline.Ticks.Select(t => t switch {
                { Type: TickType.Recovers, CharacterId: { } cid, At: var at } =>
                    charactersById.TryGetValue(cid, out var character) 
                        ? (Domain.Tick?)new Domain.Tick.Recovers(character, at) : null,
                {
                    Type: TickType.ActionEnds, CharacterId: { } cid, At: var at, TotalDuration: { } totalDuration,
                    Description: var description
                } => charactersById.TryGetValue(cid, out var character) 
                    ? new Domain.Tick.ActionEnds(character, at, totalDuration, description) : null,
                {
                    Type: TickType.EffectEnds, EffectId: { } eid, At: var at
                } => new Domain.Tick.EffectEnds(effectsById[eid], at),
                {
                    Type: TickType.EffectTicks, EffectId: { } eid, At: var at
                } => new Domain.Tick.EffectTicks(effectsById[eid], at),
                _ => throw new("Unexpected tick type")
            })
                .Where(x => x != null)
                .ToImmutableArray());
    }

    static Domain.Effect toDomain(
        this Effect effect,
        IReadOnlyDictionary<string, Domain.Character> charactersById
    )
    {
        return new(
            effect.Id,
            effect.Description,
            effect.StartsAt,
            effect.TotalDuration,
            effect.AffectedCharacterIds
                .Select(cid => charactersById.TryGetValue(cid, out var character) ? character : null)
                .Where(c => c != null)
                .ToImmutableArray(),
            effect.TickInterval);
    }
}