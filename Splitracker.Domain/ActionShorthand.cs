namespace Splitracker.Domain;

public record ActionShorthand(
    string Id,
    string Name,
    int Ticks,
    ActionShorthandType Type
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
                ActionShorthandType.Ranged => "Fernkampfangriff vorbereiten",
                ActionShorthandType.Spell => "Magie fokussieren",
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