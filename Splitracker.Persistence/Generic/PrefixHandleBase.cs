using System;

namespace Splitracker.Persistence.Generic;

abstract class PrefixHandleBase<TSelf, TValue>(TValue value) : IDisposable, IEquatable<TSelf>
    where TSelf : PrefixHandleBase<TSelf, TValue>
    where TValue : class
{
    volatile TValue value = value;

    public event EventHandler? Updated;

    public abstract string Id { get; }

    public TValue Value
    {
        get => value;
        set => this.value = value;
    }

    public void TriggerUpdated()
    {
        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Updated = null;
    }

    public bool Equals(TSelf? other) => other?.Id == Id;

    public bool Equals(PrefixHandleBase<TSelf, TValue>? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PrefixHandleBase<TSelf, TValue>)obj);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(PrefixHandleBase<TSelf, TValue>? left, PrefixHandleBase<TSelf, TValue>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PrefixHandleBase<TSelf, TValue>? left, PrefixHandleBase<TSelf, TValue>? right)
    {
        return !Equals(left, right);
    }
}