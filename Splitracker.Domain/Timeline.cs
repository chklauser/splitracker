using System.Collections.Immutable;

namespace Splitracker.Domain;

public record Timeline(
    string GroupId,
    string GroupName, 
    IImmutableDictionary<string, Character> Characters,
    IImmutableDictionary<string, Effect> Effects,
    IImmutableList<Character> Ready,
    IImmutableList<Tick> Ticks);

public abstract record Tick(int At)
{
    public record Recovers(Character Character, int At) : Tick(At), IHasCharacter
    {
        public override string ToString() => $"{At}:recover:{Character.Id}";
    }
    public record ActionEnds(Character Character, int At, int TotalDuration, string? Description = null) : Tick(At), IHasCharacter
    {
        public override string ToString() => $"{At}:end:{Character.Id}";
    }

    public record EffectEnds(Effect Effect, int At) : Tick(At), IHasEffect
    {
        public override string ToString() => $"{At}:effect:end:{Effect.Id}";
    }
    public record EffectTicks(Effect Effect, int At) : Tick(At), IHasEffect
    {
        public override string ToString() => $"{At}:effect:tick:{Effect.Id}";
    }
    
    public interface IHasCharacter
    {
        Character Character { get; }
    }
    
    public interface IHasEffect
    {
        Effect Effect { get; }
    }
}

public record Effect(
    string Id,
    string Description,
    int StartsAt,
    int TotalDuration,
    IImmutableList<Character> Affects,
    int? TickInterval = null
);