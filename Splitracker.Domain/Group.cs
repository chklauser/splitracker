using System.Collections.Immutable;

namespace Splitracker.Domain;

public record Group(
    string Name,
    IImmutableDictionary<string, Character> Characters
);

