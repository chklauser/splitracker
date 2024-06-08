using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Splitracker.Domain;

public record ActionShorthand(
    string Id,
    string Name,
    string? Description,
    int Ticks,
    ActionShorthandType Type,
    string? CostExpression,
    int Bonus,
    DiceExpression? Damage,
    int PerSuccessDamageBonus,
    int TargetValue
)
{
    public ActionTemplate ToTemplate()
    {
        return new(
            Id,
            Name,
            Type == ActionShorthandType.Melee ? ActionTemplateType.Immediate : ActionTemplateType.Continuous,
            Default: Ticks,
            Description: Type switch {
                ActionShorthandType.Ranged => $"Fernkampfangriff \"{Description ?? Name}\" vorbereiten",
                ActionShorthandType.Spell => $"Magie für \"{Description ?? Name}\" fokussieren{(CostExpression is {} expr ? $" ({expr})" : "")}",
                _ => null,
            },
            FollowUp: Type switch {
                ActionShorthandType.Ranged => CommonActionTemplates.Shoot,
                ActionShorthandType.Spell => CommonActionTemplates.CastSpell,
                _ => null,
            }
            );
    }
}

public enum ActionShorthandType
{
    Melee,
    Ranged,
    Spell,
}

public partial record DiceExpression(
    int NumberOfDice,
    int NumberOfSides,
    int Bonus = 0,
    int ClampMin = 0,
    int NumberOfBonusDice = 0,
    int PerCriticalBonus = 0
)
{
    public const string IncrementalExpressionPattern = @"([dDwW0-9+-]|\s)*";
    
    public int Roll(Random random)
    {
        var totalNumDice = NumberOfDice + NumberOfBonusDice;
        var rolls = totalNumDice < 25 ? stackalloc int[totalNumDice] : new int[totalNumDice];
        for (var i = 0; i < rolls.Length; i++)
        {
            rolls[i] = random.Next(1, NumberOfSides + 1);
        }

        if (NumberOfBonusDice > 0)
        {
            // Only keep the highest dice
            rolls.Sort();
            rolls = rolls[^NumberOfDice..];
        }

        var sum = 0;
        var totalCriticalBonus = 0;
        foreach (var roll in rolls)
        {
            sum += Math.Max(roll, ClampMin);
            totalCriticalBonus += roll == NumberOfSides ? PerCriticalBonus : 0;
        }

        return Bonus + sum + totalCriticalBonus;
    }

    [GeneratedRegex(
        @"^\s*((?<const>\d+)|((?<numd>\d*)\s*(?<w>[dDwW])\s*(?<nums>\d*))(\s*(?<sign>[+-])\s*(?<bonus>\d+))?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        500)]
    private static partial Regex exprPattern();
    
    public static DiceExpression? Parse(string raw)
    {
        var m = exprPattern().Match(raw);
        if (!m.Success)
        {
            return null;
        }

        if (m.Groups["const"] is { Success: true, ValueSpan: var rawConst } && int.TryParse(rawConst, provider: CultureInfo.InvariantCulture, out var @const))
        {
            return new(0, 0, @const);
        }

        var numDice =
            m.Groups["numd"] is { Success: true, ValueSpan: var rawNumDice } &&
            int.TryParse(rawNumDice, provider: CultureInfo.InvariantCulture, out var parsedNumDice)
                ? parsedNumDice
                : 1;
        var numSides =
            m.Groups["nums"] is { Success: true, ValueSpan: var rawNumSides } &&
            int.TryParse(rawNumSides, provider: CultureInfo.InvariantCulture, out var parsedNumSides)
                ? parsedNumSides
                : 6;
        var sign =
            m.Groups["sign"] is { Success: true, ValueSpan: ['-'] }
                ? -1
                : 1;
        var bonus =
            m.Groups["bonus"] is { Success: true, ValueSpan: var rawBonus } &&
            int.TryParse(rawBonus, provider: CultureInfo.InvariantCulture, out var parsedBonus)
                ? parsedBonus * sign
                : 0;

        return new(numDice, numSides, bonus);
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        if (NumberOfDice > 0)
        {
            sb.Append(NumberOfDice);
            sb.Append('W');
            sb.Append(NumberOfSides);
            if (Bonus > 0)
            {
                sb.Append('+');
                sb.Append(Bonus);
            }
            else if (Bonus < 0)
            {
                sb.Append('-');
                sb.Append(Bonus);
            }
        }
        else
        {
            sb.Append(Bonus);
        }

        return sb.ToString();
    }
}