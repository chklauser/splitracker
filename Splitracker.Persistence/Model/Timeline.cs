using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Riok.Mapperly.Abstractions;

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
    public int? StartedAt { get; set; }
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
    public static Splitracker.Domain.Timeline ToDomain(
        this Timeline timeline,
        Group group,
        IEnumerable<CharacterModel> characters
    )
    {
        var charactersById = characters.ToImmutableDictionary(c => c.Id, c => c.ToDomain());
        var effectsById = timeline.Effects.ToImmutableDictionary(e => e.Id, e => e.toDomain(charactersById));
        return new(group.Id!, group.Name, charactersById, effectsById,
            timeline.ReadyCharacterIds.Select(cid => charactersById[cid]).ToImmutableArray(),
            timeline.Ticks.Select(t => t switch {
                { Type: TickType.Recovers, CharacterId: { } cid, At: var at } =>
                    (Domain.Tick)new Splitracker.Domain.Tick.Recovers(charactersById[cid], at),
                {
                    Type: TickType.ActionEnds, CharacterId: { } cid, At: var at, StartedAt: { } startedAt,
                    Description: var description
                } => new Domain.Tick.ActionEnds(charactersById[cid], at, startedAt, description),
                {
                    Type: TickType.EffectEnds, EffectId: { } eid, At: var at
                } => new Domain.Tick.EffectEnds(effectsById[eid], at),
                {
                    Type: TickType.EffectTicks, EffectId: { } eid, At: var at
                } => new Domain.Tick.EffectTicks(effectsById[eid], at),
                _ => throw new("Unexpected tick type")
            }).ToImmutableArray());
    }

    static Domain.Effect toDomain(
        this Effect effect,
        IReadOnlyDictionary<string, Splitracker.Domain.Character> charactersById
    )
    {
        return new(
            effect.Id,
            effect.Description,
            effect.StartsAt,
            effect.TotalDuration,
            effect.AffectedCharacterIds.Select(cid => charactersById[cid]).ToImmutableArray(),
            effect.TickInterval);
    }
}