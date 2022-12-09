namespace Splitracker.Domain;

public static class CommonActionTemplates
{
    public static readonly ActionTemplate Immediate = new("__immediate", "Sofort", ActionTemplateType.Immediate);
    public static readonly ActionTemplate Continuous = new("__continuous", "Kont.", ActionTemplateType.Continuous);

    public static readonly ActionTemplate Move = new(
        "__move",
        "Bewegen",
        ActionTemplateType.Continuous,
        Default: 5,
        Description: "Bewegen");

    public static readonly ActionTemplate Sprint = new(
        "__sprint",
        "Sprinten",
        ActionTemplateType.Continuous,
        Default: 10,
        Description: "Sprinten");

    public static readonly ActionTemplate Ready = new("__ready", "Abwarten", ActionTemplateType.Ready);

    public static readonly ActionTemplate CastSpell = new(
        "__cast_spell",
        "Zauber auslösen",
        ActionTemplateType.Immediate,
        Default: 3,
        AvoidRepetition: true);

    public static readonly ActionTemplate Focus = new(
        "__focus",
        "Fokus",
        ActionTemplateType.Continuous,
        Description: "Magie fokussieren",
        FollowUp: CastSpell);

    public static readonly ActionTemplate Shoot = new(
        "__aim",
        "Fernk. auslösen",
        ActionTemplateType.Immediate,
        Default: 3,
        AvoidRepetition: true);

    public static readonly ActionTemplate PrepareRanged = new(
        "__prepare_ranged",
        "Fernk. vorbereiten",
        ActionTemplateType.Continuous,
        Description: "Fernkampf vorbereiten",
        FollowUp: Shoot);

    public static readonly ActionTemplate Aim = new(
        "__aim",
        "Zielen",
        ActionTemplateType.Continuous,
        Description: "Zielen",
        Default: 1,
        Max: 3,
        Multiplier: 2,
        FollowUp: Shoot,
        AvoidRepetition: true);

    public static readonly ActionTemplate LookForGap = new(
        "__look_for_gap",
        "Lücke suchen",
        ActionTemplateType.Continuous,
        Description: "Lücke suchen",
        Default: 1,
        Max: 3,
        Multiplier: 2,
        AvoidRepetition: true);

    public static readonly ActionTemplate BumpForward = new(
        "__bump_forward",
        "Position",
        ActionTemplateType.Bump,
        Max: 0,
        Default: 0);

    public static readonly ActionTemplate BumpBackward = new(
        "__bump_backward",
        "Position",
        ActionTemplateType.Bump,
        Max: 0,
        Default: 0);

    public static readonly ActionTemplate Reaction = new(
        "__reaction",
        "Reaktion",
        ActionTemplateType.Reaction,
        AvoidRepetition: true);

    public static readonly ActionTemplate ActiveDefense = new(
        "__active_defense",
        "Aktive Abwehr",
        ActionTemplateType.Reaction,
        Default: 3);

    public static readonly ActionTemplate AbortContinuousAction = new(
        "__abort_action",
        "Kontinuierliche Aktion abbrechen",
        ActionTemplateType.Reset,
        CustomLabel: "Aktion abbrechen",
        AvoidRepetition: true);

    public static readonly ActionTemplate LeaveTimeline = new(
        "__leave_timeline",
        "Charakter Entfernen",
        ActionTemplateType.Leave,
        CustomLabel: "Entfernen");
}