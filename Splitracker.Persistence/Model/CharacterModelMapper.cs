using System;
using System.Collections.Immutable;
using System.Linq;
using Splitracker.Domain;

namespace Splitracker.Persistence.Model;

static class CharacterModelMapper
{
    public static Domain.Character ToDomain(this Character model)
    {
        return new(model.Id,
            model.Name,
            model.CustomColor,
            model.Lp.ToDomainLp(),
            model.Fo.ToDomainFo(),
            model.ActionShorthands.Select(s => s.ToDomain()).ToImmutableDictionary(s => s.Id),
            model.IsOpponent);
    }

    public static LpPool ToDomainLp(this Pool model)
    {
        return new(model.BaseCapacity, model.Points.toDomain(), model.Channelings.Select(toDomain).ToImmutableArray());
    }

    public static FoPool ToDomainFo(this Pool model)
    {
        return new(model.BaseCapacity, model.Points.toDomain(), model.Channelings.Select(toDomain).ToImmutableArray());
    }
    
    static Domain.Channeling toDomain(this Channeling model)
    {
        return new(model.Id, model.Value, model.Description);
    }

    static Channeling toDbModel(this Domain.Channeling channeling)
    {
        return new() { Id = channeling.Id, Description = channeling.Description, Value = channeling.Value };
    }

    public static Domain.ActionShorthand ToDomain(this ActionShorthand model)
    {
        return new(model.Id,
            model.Name,
            model.Description,
            model.Ticks,
            model.Type switch {
                ActionShorthandType.Melee => Domain.ActionShorthandType.Melee,
                ActionShorthandType.Ranged => Domain.ActionShorthandType.Ranged,
                ActionShorthandType.Spell => Domain.ActionShorthandType.Spell,
                _ => throw new ArgumentOutOfRangeException(),
            },
            model.CostExpression);
    }

    static PointsVec toDomain(this Points points)
    {
        return new() {
            Channeled = points.Channeled,
            Consumed = points.Consumed,
            Exhausted = points.Exhausted,
        };
    }

    public static Character ToDbModel(this Domain.Character character)
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
    
    static Pool toDbModel(this Domain.Pool pool)
    {
        return new() {
            BaseCapacity = pool.BaseCapacity,
            Channelings = pool.Channelings.Select(toDbModel).ToList(),
            Points = pool.Points.toDbModel(),
        };
    }
    
    static Points toDbModel(this PointsVec points)
    {
        return new(points.Channeled, points.Exhausted, points.Consumed);
    }

    public static ActionShorthand ToDbModel(this Domain.ActionShorthand shorthand)
    {
        return new() {
            Id = shorthand.Id,
            Name = shorthand.Name,
            Description = shorthand.Description,
            Ticks = shorthand.Ticks,
            Type = shorthand.Type switch {
                Domain.ActionShorthandType.Melee => ActionShorthandType.Melee,
                Domain.ActionShorthandType.Ranged => ActionShorthandType.Ranged,
                Domain.ActionShorthandType.Spell => ActionShorthandType.Spell,
                _ => throw new ArgumentOutOfRangeException(nameof(shorthand)),
            },
            CostExpression = shorthand.CostExpression,
        };
    }
}