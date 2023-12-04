using System;
using System.Threading.Tasks;

namespace Splitracker.Persistence.Generic;

abstract class HandleBase<TSubscription, TValue> : IAsyncDisposable, IDisposable
where TSubscription : ISubscription<TValue>
{
    readonly TSubscription subscription;

    public HandleBase(TSubscription subscription)
    {
        this.subscription = subscription;
        subscription.Updated += OnUpdated;
    }
    
    void OnUpdated(object? sender, EventArgs e)
    {
        Updated?.Invoke(sender, e);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        subscription.Updated -= OnUpdated;
        subscription.Release();
        Updated = null;
    }

    internal TValue Value => subscription.CurrentValue;
    public event EventHandler? Updated;
}

interface ISubscription
{
    void Release();
    event EventHandler? Disposed;
}
interface ISubscription<out T> : ISubscription
{
    event EventHandler? Updated;
    T CurrentValue { get; }
}

interface IHandle<TSelf, TSubscription> where TSelf : IHandle<TSelf, TSubscription>
{
    static abstract TSelf Create(TSubscription subscription);
}

interface IPrefixHandle<TSelf, TValue> where TSelf : IPrefixHandle<TSelf, TValue>
{
    static abstract TSelf Create(TValue value);
    string Id { get; }
    TValue Value { get; set; }
    void TriggerUpdated();
}