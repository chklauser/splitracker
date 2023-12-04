using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Characters;

/// <summary>
/// Mutable container for a <see cref="Character"/>. Triggers the <see cref="Updated"/> event whenever
/// anything about the character changes.
/// </summary>
class RavenCharacterHandle(Character character)
    : PrefixHandleBase<RavenCharacterHandle, Character>(character),
        IPrefixHandle<RavenCharacterHandle, Character>,
        ICharacterHandle
{
    public static RavenCharacterHandle Create(Character value) => new(value);

    public override string Id => Value.Id;

    public Character Character => Value;
}