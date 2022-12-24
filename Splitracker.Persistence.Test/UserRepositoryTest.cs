using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Raven.Client.Documents.Operations;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Characters;
using Splitracker.Persistence.Model;
using ActionShorthand = Splitracker.Domain.ActionShorthand;
using ActionShorthandType = Splitracker.Domain.ActionShorthandType;

namespace Splitracker.Persistence.Test;

[TestFixture]
public class UserRepositoryTest : RavenIntegrationTestBase
{
    ClaimsPrincipal principal = null!;
    
    [SetUp]
    public async Task SetUp()
    {
        principal = FakeUserPrincipal("u1", "CharUser");
        await WipeCollectionAsync<User>();
        WaitForIndexing(CurrentStore);
    }

    [Test]
    public async Task GetUserId_ReturnsSameIdForSameOid()
    {
        // Arrange
        var randomOid = IdGenerator.RandomId();
        var randomPrincipal1 = FakeUserPrincipal(randomOid, "Test User");
        var randomPrincipal2 = FakeUserPrincipal(randomOid, "Different Name");
        
        // Act
        var result1 = await UserRepository.GetUserIdAsync(randomPrincipal1);
        WaitForIndexing();
        var result2 = await UserRepository.GetUserIdAsync(randomPrincipal2);

        // Assert
        result1.Should().Be(result2);
    }
}