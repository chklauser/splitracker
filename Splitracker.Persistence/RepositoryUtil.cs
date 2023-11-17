using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Splitracker.Persistence.Generic;

namespace Splitracker.Persistence;

static class RepositoryUtil
{
    public static async Task<THandle?> TryCreateSubscription<TKey, TSubscription, THandle>(
        this ConcurrentDictionary<TKey, Task<TSubscription>> handles,
        TKey key,
        Func<Task<TSubscription>> createSubscription,
        Action onExistingSubscription,
        Func<TSubscription, THandle> tryGetHandle,
        Action onRetry
    ) where TKey : notnull where THandle : class? where TSubscription : ISubscription
    {
        var remainingTries = 5;
        while (remainingTries-- > 0)
        {
            var ourTask = new TaskCompletionSource<TSubscription>();
            var ourSubscription = ourTask.Task;
            var installedSubscription = handles.GetOrAdd(key, ourSubscription);
            if (installedSubscription == ourSubscription)
            {
                try
                {
                    var subscription = await createSubscription();
                    subscription.Disposed += (_, _) =>
                    {
                        _ = handles.TryRemove(new(key, ourTask.Task));
                    };
                    ourTask.SetResult(subscription);
                }
                catch (Exception ex)
                {
                    ourTask.SetException(ex);
                    handles.TryRemove(new(key, ourTask.Task));
                    throw;
                }
            }
            else
            {
                onExistingSubscription();
            }


            if (tryGetHandle(await installedSubscription) is { } handle)
            {
                return handle;
            }
            else
            {
                // The subscription has been disposed of. Clear it from the dictionary (but only if it matches)
                // and try again.
                onRetry();
                handles.TryRemove(new(key, installedSubscription));
            }
        }

        return null;
    }
    
    public static async Task<THandle?> TryCreateSubscription<TKey, TSubscription, THandle, TValue>(
        this ConcurrentDictionary<TKey, Task<TSubscription>> handles,
        TKey key,
        Func<Task<TSubscription>> createSubscription,
        Action onExistingSubscription,
        Action onRetry
    ) where TKey : notnull
        where THandle : class, IHandle<THandle, TSubscription>
    where TSubscription : SubscriptionBase<TSubscription, TValue, THandle>
    {
        var remainingTries = 5;
        while (remainingTries-- > 0)
        {
            var ourTask = new TaskCompletionSource<TSubscription>();
            var ourSubscription = ourTask.Task;
            var installedSubscription = handles.GetOrAdd(key, ourSubscription);
            if (installedSubscription == ourSubscription)
            {
                try
                {
                    var subscription = await createSubscription();
                    ourTask.SetResult(subscription);
                }
                catch (Exception ex)
                {
                    ourTask.SetException(ex);
                    handles.TryRemove(key, out _);
                    throw;
                }
            }
            else
            {
                onExistingSubscription();
            }


            if ((await installedSubscription).TryGetHandle() is { } handle)
            {
                return handle;
            }
            else
            {
                // The subscription has been disposed of. Clear it from the dictionary (but only if it matches)
                // and try again.
                onRetry();
                handles.TryRemove(new(key, installedSubscription));
            }
        }

        return null;
    }
}