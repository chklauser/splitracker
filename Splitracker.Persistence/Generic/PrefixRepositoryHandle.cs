using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Splitracker.Persistence.Generic;

abstract class PrefixRepositoryHandle<TSelf, TSubscription> : IAsyncDisposable, IDisposable
    where TSubscription : IRepositorySubscription
    where TSelf : IHandle<TSelf, TSubscription>
{
    protected readonly TSubscription Subscription;

    protected PrefixRepositoryHandle(TSubscription subscription)
    {
        Subscription = subscription;
        subscription.Added += OnAdded;
        subscription.Deleted += OnDeleted;
    }

    public event EventHandler? Added;
    public event EventHandler? Deleted;

    [SuppressMessage("Usage", "MA0091:Sender should be \'this\' for instance events")]
    void OnAdded(object? sender, EventArgs e)
    {
        Added?.Invoke(sender, e);
    }

    [SuppressMessage("Usage", "MA0091:Sender should be \'this\' for instance events")]
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