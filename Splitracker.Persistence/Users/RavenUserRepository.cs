using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Splitracker.Domain;
using Splitracker.Persistence.Model;

namespace Splitracker.Persistence.Users;

public class RavenUserRepository(IDocumentStore store, ILogger<RavenUserRepository> log)
    : IHostedService, IUserRepository
{
    const string CollectionName = "Users";
    internal const string OidClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    public async Task<string> GetUserIdAsync(ClaimsPrincipal principal)
    {
        var oid = oidOf(principal);
        using var session = store.OpenAsyncSession();

        var userId = await session.Query<User_ByOid.IndexEntry, User_ByOid>()
            .Where(u => u.Oid == oid)
            .Select(u => u.Id)
            .SingleOrDefaultAsync();

        if (userId == null)
        {
            log.LogInformation("Creating new user for {Oid}", oid);
            var newUserId = $"{CollectionName}/{oid}";
            await session.StoreAsync(new User {
                Id = newUserId,
                DisplayName = principal.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? principal.Identity?.Name ?? "Anonymous",
                Oids = [oid],
            });
            await session.SaveChangesAsync();
            return newUserId;
        }
        else
        {
            log.LogInformation("Login with {Oid} belongs to {UserId}", oid, userId);
            return userId;
        }
    }

    static string oidOf(ClaimsPrincipal principal)
    {
        var oid = principal.Claims.FirstOrDefault(c => c.Type == OidClaimType)?.Value ??
            throw new ArgumentException("Principal does not have an oid claim.", nameof(principal));
        return oid;
    }
    
    #region Index Creation

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await new User_ByOid().ExecuteAsync(store, token: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #endregion
}