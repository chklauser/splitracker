using System;
using System.Threading.Tasks;

namespace Splitracker.Persistence.Generic;

abstract class PrefixRepositoryHandle<TSelf, TSubscription> : IAsyncDisposable, IDisposable
    where TSubscription : IRepositorySubscription
    where TSelf : IHandle<TSelf, TSubscription>
{
    protected readonly TSubscription Subscription;

    public PrefixRepositoryHandle(TSubscription subscription)
    {
        Subscription = subscription;
        subscription.Added += OnAdded;
        subscription.Deleted += OnDeleted;
    }

    public event EventHandler? Added;
    public event EventHandler? Deleted;

    void OnAdded(object? sender, EventArgs e)
    {
        Added?.Invoke(sender, e);
    }

    void OnDeleted(object? sender, EventArgs e)
    {
        Deleted?.Invoke(sender, e);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        Subscription.Added -= OnAdded;
        Subscription.Deleted -= OnDeleted;
        Subscription.Release();
        Added = null;
        Deleted = null;
    }
}