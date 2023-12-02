using System.Collections.Immutable;

namespace Splitracker.Web.Shared;

public record SessionContext
{
    public IImmutableList<string> FilterTags { get; set; } = ImmutableList<string>.Empty;
}