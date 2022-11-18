using System.Diagnostics.CodeAnalysis;
using Splitracker.Domain;

namespace Splitracker.Persistence;

/// <summary>
/// Mutable container for a <see cref="Character"/>. Triggers the <see cref="CharacterUpdated"/> event whenever
/// anything about the character changes.
/// </summary>
class RavenCharacterHandle : ICharacterHandle, IEquatable<RavenCharacterHandle>, IDisposable
{
    volatile Character character;

    public RavenCharacterHandle(Character character)
    {
        this.character = character;
    }

    public Character Character
    {
        get => character;
        internal set => character = value;
    }

    public event EventHandler? CharacterUpdated;

    public void TriggerCharacterUpdated()
    {
        CharacterUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        CharacterUpdated = null;
    }

    #region Equality

    public bool Equals(RavenCharacterHandle? other) => other?.Character.Id == Character.Id;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((RavenCharacterHandle)obj);
    }

    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode", Justification = "Id remains stable")]
    public override int GetHashCode() => Character.Id.GetHashCode();

    public static bool operator ==(RavenCharacterHandle? left, RavenCharacterHandle? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(RavenCharacterHandle? left, RavenCharacterHandle? right)
    {
        return !Equals(left, right);
    }

    #endregion
}