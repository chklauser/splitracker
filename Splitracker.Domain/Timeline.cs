using System.Collections.Immutable;

namespace Splitracker.Domain;

public record Timeline(string GroupName, 
    IImmutableDictionary<string, Character> Characters,
    IImmutableList<Character> Ready,
    IImmutableList<Tick> Ticks);

public abstract record Tick(int At)
{
    public record Recovers(Character Character, int At) : Tick(At)
    {
        public override string ToString() => $"{At}:recover:{Character.Id}";
    }
    public record ActionEnds(Character Character, int At, int StartedAt, string? Description = null) : Tick(At)
    {
        public override string ToString() => $"{At}:end:{Character.Id}";
    }

    public record EffectEnds(Effect Effect, int At) : Tick(At)
    {
        public override string ToString() => $"{At}:effect:end:{Effect.Id}";
    }
    public record EffectTicks(Effect Effect, int At) : Tick(At)
    {
        public override string ToString() => $"{At}:effect:tick:{Effect.Id}";
    }
}

public record Effect(
    string Id,
    string Name,
    int StartsAt,
    int TotalDuration,
    IImmutableList<Character> Affects,
    int? TickInterval = null
);