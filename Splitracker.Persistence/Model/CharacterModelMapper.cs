using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Splitracker.Domain;

namespace Splitracker.Persistence.Model;

static class CharacterModelMapper
{
    public static Character ToDomain(this CharacterModel model)
    {
        return new(model.Id,
            model.Name,
            model.CustomColor,
            model.Lp.ToDomainLp(),
            model.Fo.ToDomainFo(),
            model.ActionShorthands.Select(s => s.ToDomain()).ToImmutableDictionary(s => s.Id),
            model.IsOpponent);
    }

    public static LpPool ToDomainLp(this PoolModel model)
    {
        return new(model.BaseCapacity, model.Points.toDomain(), model.Channelings.ToImmutableArray());
    }

    public static FoPool ToDomainFo(this PoolModel model)
    {
        return new(model.BaseCapacity, model.Points.toDomain(), model.Channelings.ToImmutableArray());
    }

    public static Domain.ActionShorthand ToDomain(this ActionShorthand model)
    {
        return new(model.Id,
            model.Name,
            model.Ticks,
            model.Type switch {
                ActionShorthandType.Melee => Domain.ActionShorthandType.Melee,
                ActionShorthandType.Ranged => Domain.ActionShorthandType.Ranged,
                ActionShorthandType.Spell => Domain.ActionShorthandType.Spell,
                _ => throw new ArgumentOutOfRangeException(),
            });
    }

    static PointsVec toDomain(this PointsModel points)
    {
        return new() {
            Channeled = points.Channeled,
            Consumed = points.Consumed,
            Exhausted = points.Exhausted,
        };
    }

    public static CharacterModel ToDbModel(this Character character)
    {
        return new(character.Id, character.Name, character.Lp.toDbModel(), character.Fo.toDbModel()) {
            ActionShorthands = character.ActionShorthands.Values
                .OrderBy(x => x.Id)
                .Select(x => x.ToDbModel())
                .ToList(),
            CustomColor = character.CustomColor,
            IsOpponent = character.IsOpponent,
        };
    }
    
    static PoolModel toDbModel(this Domain.Pool pool)
    {
        return new() {
            BaseCapacity = pool.BaseCapacity,
            Channelings = pool.Channelings.ToList(),
            Points = pool.Points.toDbModel(),
        };
    }
    
    static PointsModel toDbModel(this PointsVec points)
    {
        return new(points.Channeled, points.Exhausted, points.Consumed);
    }

    [UsedImplicitly]
    static IImmutableList<int> toImmutableList(List<int> array)
    {
        return array.ToImmutableList();
    }

    public static ActionShorthand ToDbModel(this Domain.ActionShorthand shorthand)
    {
        return new() {
            Id = shorthand.Id,
            Name = shorthand.Name,
            Ticks = shorthand.Ticks,
            Type = shorthand.Type switch {
                Domain.ActionShorthandType.Melee => ActionShorthandType.Melee,
                Domain.ActionShorthandType.Ranged => ActionShorthandType.Ranged,
                Domain.ActionShorthandType.Spell => ActionShorthandType.Spell,
                _ => throw new ArgumentOutOfRangeException(nameof(shorthand)),
            },
        };
    }
}