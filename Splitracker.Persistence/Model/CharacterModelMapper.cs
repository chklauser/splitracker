using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Splitracker.Domain;

namespace Splitracker.Persistence.Model;

static class CharacterModelMapper
{
    public static Domain.Character ToDomain(this Character model, IReadOnlyDictionary<string,Character> templates)
    {
        return model.ToDomain(model.TemplateId is { } templateId ? templates.GetValueOrDefault(templateId) : null);
    }

    static readonly Domain.Character Prototype = new(
        null!,
        string.Empty,
        null,
        new(5),
        new(5),
        new(0, 0),
        ImmutableDictionary<string, Domain.ActionShorthand>.Empty,
        false,
        ImmutableHashSet<string>.Empty,
        null,
        DateTimeOffset.MinValue);
    
    public static Domain.Character ToDomain(this Character model, Character? templateModel)
    {
        if(model.TemplateId is { } templateId && (templateModel is null || templateModel.Id != templateId))
        {
            throw new InvalidOperationException($"Character {model.Id} requires template id {templateId}.");
        }

        var template = templateModel?.ToDomain((Character?)null) ?? Prototype;
        var actionShorthands = model.ActionShorthands
            .Select(s => s.ToDomain())
            .ToDictionary(s => s.Id, comparer: StringComparer.Ordinal);
        foreach (var templateActionShorthand in template.ActionShorthands.Values)
        {
            actionShorthands.TryAdd(templateActionShorthand.Id, templateActionShorthand);
        }

        return new(model.Id,
            model.Name.Replace("\uFFFC", template.Name, StringComparison.Ordinal),
            model.CustomColor ?? template.CustomColor,
            model.Lp.ToDomainLp(template.Lp.BaseCapacity),
            model.Fo.ToDomainFo(template.Fo.BaseCapacity),
            model.SplinterPoints.toDomain(template.SplinterPoints.Max),
            actionShorthands.ToImmutableDictionary(),
            model.IsOpponent ?? template.IsOpponent,
            model.TagIds.Concat(template.TagIds).ToImmutableHashSet(),
            model.TemplateId,
            model.InsertedAt);
    }

    public static LpPool ToDomainLp(this Pool model, int templateBaseCapacity)
    {
        return new(
            model.BaseCapacity ?? templateBaseCapacity,
            model.Points?.toDomain() ?? new(),
            model.Channelings?.Select(toDomain).ToImmutableArray() ?? ImmutableArray<Domain.Channeling>.Empty);
    }

    public static FoPool ToDomainFo(this Pool model, int templateBaseCapacity)
    {
        return new(
            model.BaseCapacity ?? templateBaseCapacity,
            model.Points?.toDomain() ?? new(),
            model.Channelings?.Select(toDomain).ToImmutableArray() ?? ImmutableArray<Domain.Channeling>.Empty);
    }
    
    static Domain.Channeling toDomain(this Channeling model)
    {
        return new(model.Id, model.Value, model.Description);
    }

    static Domain.SplinterPoints toDomain(this SplinterPoints model, int templateMax)
    {
        return new(model.Max ?? templateMax, model.Used);
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
                _ => throw new InvalidOperationException($"Unknown action shorthand type {model.Type}."),
            },
            model.CostExpression,
            model.Bonus,
            model.Damage?.ToDomain(),
            model.PerSuccessDamageBonus,
            model.TargetValue);
    }

    public static Domain.DiceExpression ToDomain(this DiceExpression model)
    {
        return new(
            NumberOfDice: model.NumberOfDice,
            NumberOfSides: model.NumberOfSides,
            Bonus: model.Bonus,
            ClampMin: model.ClampMin,
            NumberOfBonusDice: model.NumberOfBonusDice,
            PerCriticalBonus: model.PerCriticalBonus);
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
            TagIds = character.TagIds.ToList(),
            InsertedAt = character.InsertedAt,
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
                _ => throw new InvalidOperationException($"Unknown action shorthand type {shorthand.Type}."),
            },
            CostExpression = shorthand.CostExpression,
            Bonus = shorthand.Bonus,
            Damage = shorthand.Damage?.toDbModel(),
            PerSuccessDamageBonus = shorthand.PerSuccessDamageBonus,
            TargetValue = shorthand.TargetValue,
        };
    }

    static DiceExpression toDbModel(this Domain.DiceExpression model)
    {
        return new() {
            NumberOfDice = model.NumberOfDice,
            NumberOfSides = model.NumberOfSides,
            Bonus = model.Bonus,
            ClampMin = model.ClampMin,
            NumberOfBonusDice = model.NumberOfBonusDice,
            PerCriticalBonus = model.PerCriticalBonus,
        };
    }
}