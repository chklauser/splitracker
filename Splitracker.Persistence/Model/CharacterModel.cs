using System.Collections.Generic;
using JetBrains.Annotations;

namespace Splitracker.Persistence.Model;

[UsedImplicitly]
record CharacterModel(string Id, string Name, PoolModel Lp, PoolModel Fo)
{
    public string Id { get; set; } = Id;
    public string Name { get; set; } = Name;
    public PoolModel Lp { get; set; } = Lp;
    public PoolModel Fo { get; set; } = Fo;
    public List<ActionShorthand> ActionShorthands { get; set; } = new();
    public string? CustomColor;
    public bool IsOpponent;
}

class ActionShorthand
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required int Ticks { get; set; } = 1;
    public ActionShorthandType Type { get; set; }
}

enum ActionShorthandType
{
    Melee,
    Ranged,
    Spell
}