using System.Collections.Immutable;

namespace Splitracker.Domain.Commands;

public interface ICharacterCommand
{
    string? CharacterId { get; }
}

public record ApplyPoints(string CharacterId, PoolType Pool, PointsVec Points) : ICharacterCommand;

public record ShortRest(string CharacterId) : ICharacterCommand;

public record StopChanneling(string CharacterId, PoolType Pool, int Points) : ICharacterCommand;

public abstract record UpsertCharacter(
    string Name,
    int LpBaseCapacity,
    int FoBaseCapacity,
    string? CustomColor,
    IImmutableDictionary<string, ActionShorthand> ActionShorthands,
    bool IsOpponent
) : ICharacterCommand
{
    protected abstract string? OptionalCharacterId { get; }
    string? ICharacterCommand.CharacterId => OptionalCharacterId;
}

public record CreateCharacter(
        string Name,
        int LpBaseCapacity,
        int FoBaseCapacity,
        string? CustomColor,
        IImmutableDictionary<string, ActionShorthand> ActionShorthands,
        bool IsOpponent
    )
    : UpsertCharacter(Name,
        LpBaseCapacity,
        FoBaseCapacity,
        CustomColor,
        ActionShorthands,
        IsOpponent)
{
    protected override string? OptionalCharacterId => null;
}

public record EditCharacter(
        string CharacterId,
        string Name,
        int LpBaseCapacity,
        int FoBaseCapacity,
        string? CustomColor,
        IImmutableDictionary<string, ActionShorthand> ActionShorthands,
        bool IsOpponent
    )
    : UpsertCharacter(Name,
        LpBaseCapacity,
        FoBaseCapacity,
        CustomColor,
        ActionShorthands,
        IsOpponent)
{
    protected override string OptionalCharacterId => CharacterId;
}

public record DeleteCharacter(string CharacterId) : ICharacterCommand;

public record CloneCharacter(string CharacterId) : ICharacterCommand;