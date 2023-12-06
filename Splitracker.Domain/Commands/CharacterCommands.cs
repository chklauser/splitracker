using System.Collections.Immutable;

namespace Splitracker.Domain.Commands;

public interface ICharacterCommand
{
    string? CharacterId { get; }
}

public record ApplyPoints(string CharacterId, PoolType Pool, PointsVec Points, string? Description) : ICharacterCommand;

public record ShortRest(string CharacterId) : ICharacterCommand;

public record StopChanneling(string CharacterId, PoolType Pool, string Id) : ICharacterCommand;

public record ResetSplinterPoints(string CharacterId) : ICharacterCommand;

public record UseSplinterPoints(string CharacterId, int Amount) : ICharacterCommand;

public abstract record UpsertCharacter(
    string Name,
    int LpBaseCapacity,
    int FoBaseCapacity,
    int SplinterPointsMax,
    string? CustomColor,
    IImmutableDictionary<string, ActionShorthand> ActionShorthands,
    bool? IsOpponent,
    IImmutableSet<string> TagIds
) : ICharacterCommand
{
    protected abstract string? OptionalCharacterId { get; }
    string? ICharacterCommand.CharacterId => OptionalCharacterId;
}

public record CreateCharacter(
    string Name,
    int LpBaseCapacity,
    int FoBaseCapacity,
    int SplinterPointsMax,
    string? CustomColor,
    IImmutableDictionary<string, ActionShorthand> ActionShorthands,
    bool? IsOpponent,
    IImmutableSet<string> TagIds
)
    : UpsertCharacter(Name,
        LpBaseCapacity,
        FoBaseCapacity,
        SplinterPointsMax,
        CustomColor,
        ActionShorthands,
        IsOpponent,
        TagIds)
{
    protected override string? OptionalCharacterId => null;
}

public record EditCharacter(
    string CharacterId,
    string Name,
    int LpBaseCapacity,
    int FoBaseCapacity,
    int SplinterPointsMax,
    string? CustomColor,
    IImmutableDictionary<string, ActionShorthand> ActionShorthands,
    bool? IsOpponent,
    IImmutableSet<string> TagIds
)
    : UpsertCharacter(Name,
        LpBaseCapacity,
        FoBaseCapacity,
        SplinterPointsMax,
        CustomColor,
        ActionShorthands,
        IsOpponent,
        TagIds)
{
    protected override string OptionalCharacterId => CharacterId;
}

public abstract record UpsertCharacterInstance(
    string Name
) : ICharacterCommand
{
    protected abstract string? OptionalCharacterId { get; }
    string? ICharacterCommand.CharacterId => OptionalCharacterId;
}

public record EditCharacterInstance(string CharacterId, string Name) : UpsertCharacterInstance(Name)
{
    protected override string OptionalCharacterId => CharacterId;
}

public record CreateCharacterInstance(string TemplateId, string Name) : UpsertCharacterInstance(Name)
{
    protected override string? OptionalCharacterId => null;
}

public record UnlinkFromTemplate(string CharacterId, string Name) : ICharacterCommand;

public record DeleteCharacter(string CharacterId) : ICharacterCommand;

public record CloneCharacter(string CharacterId) : ICharacterCommand;