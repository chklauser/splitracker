using Splitracker.Domain;

namespace Splitracker.Web.Shared.Timelines;

public record CharacterActionData(ActionTemplate? Template, int NumberOfTicks)
{
    public static CharacterActionData Default { get; } = new(null, 1);
}