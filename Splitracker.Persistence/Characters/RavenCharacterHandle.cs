using System.Diagnostics.CodeAnalysis;
using Splitracker.Domain;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence.Characters;

/// <summary>
/// Mutable container for a <see cref="Character"/>. Triggers the <c>Updated</c> event whenever
/// anything about the character changes.
/// </summary>
[SuppressMessage("Design", "MA0095:A class that implements IEquatable<T> should override Equals(object)")]
sealed class RavenCharacterHandle(Character character)
    : PrefixHandleBase<RavenCharacterHandle, Character>(character),
        IPrefixHandle<RavenCharacterHandle, Character>,
        ICharacterHandle
{
    public static RavenCharacterHandle Create(Character value) => new(value);

    public override string Id => Value.Id;

    public Character Character => Value;
}