using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Splitracker.Persistence.Model;

[UsedImplicitly]
record Character(string Id, string Name, Pool Lp, Pool Fo)
{
    public string Id { get; set; } = Id;
    public string? TemplateId { get; set; }
    public string Name { get; set; } = Name;
    public Pool Lp { get; set; } = Lp;
    public Pool Fo { get; set; } = Fo;

    public SplinterPoints SplinterPoints { get; set; } = new() {
        Max = 3,
    };
    public List<ActionShorthand> ActionShorthands { get; set; } = [];
    public string? CustomColor;
    public bool? IsOpponent;
    
    public List<string> TagIds { get; set; } = [];
    
    public DateTimeOffset InsertedAt { get; set; } = default;
}

class SplinterPoints
{
    public int? Max { get; set; }
    public int Used { get; set; }
}

class ActionShorthand
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required int Ticks { get; set; } = 1;
    public required ActionShorthandType Type { get; set; }
    public string? CostExpression { get; set; }
    public int Bonus { get; set; }
    public  DiceExpression? Damage { get; set; }
    public int PerSuccessDamageBonus { get; set; }
}

class DiceExpression
{
    public required int NumberOfDice { get; set; }
    public required int NumberOfSides { get; set; }
    public int Bonus { get; set; }
    public int ClampMin { get; set; }
    public int NumberOfBonusDice { get; set; }
    public int PerCriticalBonus { get; set; }
}

enum ActionShorthandType
{
    Melee,
    Ranged,
    Spell,
}