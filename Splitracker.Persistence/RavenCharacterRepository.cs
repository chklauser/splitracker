using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Splitracker.Domain;

namespace Splitracker.Persistence;

class RavenCharacterRepository : ICharacterRepository
{
    internal const string CollectionName = "Characters";
    const string OidClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    readonly IDocumentStore store;
    readonly ILogger<RavenCharacterRepository> log;

    readonly ConcurrentDictionary<string, Task<RavenCharacterRepositorySubscription>> handles = new();

    public RavenCharacterRepository(IDocumentStore store, ILogger<RavenCharacterRepository> log)
    {
        this.store = store;
        this.log = log;
    }

    public async Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        var oid = principal.Claims.FirstOrDefault(c => c.Type == OidClaimType)?.Value ??
            throw new ArgumentException("Principal does not have an oid claim.", nameof(principal));

        var remainingTries = 5;
        while (remainingTries-- > 0)
        {
            var ourTask = new TaskCompletionSource<RavenCharacterRepositorySubscription>();
            var ourSubscription = ourTask.Task;
            var installedSubscription = handles.GetOrAdd(oid, ourSubscription);
            if (installedSubscription == ourSubscription)
            {
                try
                {
                    var subscription = await RavenCharacterRepositorySubscription.OpenAsync(store, oid, log);
                    ourTask.SetResult(subscription);
                }
                catch (Exception ex)
                {
                    ourTask.SetException(ex);
                    handles.TryRemove(oid, out _);
                    throw;
                }
            }
            else
            {
                log.Log(LogLevel.Debug, "Trying to join existing subscription for {Oid}", oid);
            }

            if ((await installedSubscription).TryGetHandle() is { } handle)
            {
                return handle;
            }
            else
            {
                // The subscription has been disposed of. Clear it from the dictionary (but only if it matches)
                // and try again.
                log.Log(LogLevel.Information, "Subscription for {Oid} was disposed of. Retrying.", oid);
                handles.TryRemove(new(oid, installedSubscription));
            }
        }
        
        throw new InvalidOperationException("Failed to open a handle.");
    }
    
    
}