using System.Collections.Immutable;

namespace Splitracker.UI.Shared;

public record SessionContext
{
    public IImmutableList<string> FilterTags { get; set; } = ImmutableList<string>.Empty;
    
    public int RollTarget { get; set; }
}