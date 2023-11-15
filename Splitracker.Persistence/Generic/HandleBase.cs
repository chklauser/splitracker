using System;
using System.Threading.Tasks;

namespace Splitracker.Persistence.Generic;

abstract class HandleBase<TSubscription, TValue> : IAsyncDisposable
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
        subscription.Updated -= OnUpdated;
        subscription.Release();
        Updated = null;
        return ValueTask.CompletedTask;
    }

    internal TValue Value => subscription.CurrentValue;
    public event EventHandler? Updated;
}

interface ISubscription<out T>
{
    T CurrentValue { get; }
    void Release();
    event EventHandler? Updated;
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