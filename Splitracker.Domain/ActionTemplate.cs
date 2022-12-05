namespace Splitracker.Domain;

public record ActionTemplate(
    string Id,
    string Name,
    ActionTemplateType Type,
    string? Description = null,
    string? CustomLabel = null,
    int Min = 1,
    int? Max = null,
    int Multiplier = 1,
    int? Default = null
)
{
    public string Label => CustomLabel ?? Name;
}

public enum ActionTemplateType
{
    Immediate,
    Continuous,
    Reaction,
    Ready,
    Reset,
    Bump,
}