﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Splitracker.Domain;

namespace Splitracker.Persistence.Characters;

/// <summary>
/// Mutable container for a <see cref="Character"/>. Triggers the <see cref="Updated"/> event whenever
/// anything about the character changes.
/// </summary>
class RavenCharacterHandle(Character character)
    : ICharacterHandle, IEquatable<RavenCharacterHandle>, IDisposable, IAsyncDisposable
{
    volatile Character character = character;

    public Character Character
    {
        get => character;
        internal set => character = value;
    }

    public event EventHandler? Updated;

    public void TriggerCharacterUpdated()
    {
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Updated = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
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