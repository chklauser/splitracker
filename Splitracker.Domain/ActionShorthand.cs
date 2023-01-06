namespace Splitracker.Domain;

public record ActionShorthand(
    string Id,
    string Name,
    string? Description,
    int Ticks,
    ActionShorthandType Type,
    string? CostExpression
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