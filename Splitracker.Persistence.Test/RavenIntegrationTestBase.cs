using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Raven.Client.Documents;
using Raven.TestDriver;
using Splitracker.Domain;
using Splitracker.Persistence.Users;

namespace Splitracker.Persistence.Test;

[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class RavenIntegrationTestBase : RavenTestDriver
{
    protected override void PreInitialize(IDocumentStore documentStore)
    {
        base.PreInitialize(documentStore);
        PersistenceServiceProviderConfig.CustomizeStore(documentStore);
    }

    protected IUserRepository UserRepository = null!;
    protected IDocumentStore CurrentStore = null!;
    
    [OneTimeSetUp]
    public async Task SetUpUserRepository()
    {
        CurrentStore = GetDocumentStore();
        var ravenUserRepository = new RavenUserRepository(CurrentStore, NullLogger<RavenUserRepository>.Instance);
        await ravenUserRepository.StartAsync(default);
        UserRepository = ravenUserRepository;
    }
    
    [OneTimeTearDown]
    public void TearDownUserRepository()
    {
        CurrentStore.Dispose();
    }

    protected static ClaimsPrincipal FakeUserPrincipal(string oid, string? name = null) => new(new[]
        { new ClaimsIdentity(new[] {
            new Claim(RavenUserRepository.OidClaimType, oid),
            new Claim("name", name ?? IdGenerator.RandomId()),
        }) });

    protected async Task WipeCollectionAsync<T>()
    {
        using var session = CurrentStore.OpenAsyncSession();
        var allDocs = await session.Query<T>()
            .Customize(x => x.WaitForNonStaleResults())
            .ToArrayAsync();
        foreach (var doc in allDocs)
        {
            session.Delete(doc);
        }
        await session.SaveChangesAsync();
    }

    protected void WaitForIndexing()
    {
        WaitForIndexing(CurrentStore);
    }
}