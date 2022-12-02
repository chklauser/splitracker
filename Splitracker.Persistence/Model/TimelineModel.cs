using System.Collections.Generic;

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
    public string? CharacterId { get; set; }
    public string? EffectId { get; set; }
    public string? Description { get; set; }
}

class Effect
{
    public required string Id { get; set; }
    public string? Description { get; set; }
    public required int StartsAt { get; set; }
    public required int TotalDuration { get; set; }
    public int? TickInterval { get; set; }
    public required List<string> AffectedCharacterIds { get; set; }
}