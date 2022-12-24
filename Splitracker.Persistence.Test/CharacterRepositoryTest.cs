using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Characters;
using Splitracker.Persistence.Model;
using ActionShorthand = Splitracker.Domain.ActionShorthand;
using ActionShorthandType = Splitracker.Domain.ActionShorthandType;

namespace Splitracker.Persistence.Test;

[TestFixture]
public class CharacterRepositoryTest : RavenIntegrationTestBase
{
    RavenCharacterRepository repository = null!;
    ClaimsPrincipal principal = null!;

    static readonly CreateCharacter CreateCharacterExample = new CreateCharacter("My Name is Test McTestington",
        5,
        6,
        "#112233",
        ImmutableDictionary<string, ActionShorthand>.Empty
            .Add("a1", new("a1", "Action 1", 2, ActionShorthandType.Melee))
            .Add("a2", new("a2", "Action 2", 3, ActionShorthandType.Ranged))
            .Add("a3", new("a3", "Action 3", 4, ActionShorthandType.Spell)),
        default);

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        var repo = new RavenCharacterRepository(CurrentStore, NullLogger<RavenCharacterRepository>.Instance, UserRepository);
        await repo.StartAsync(default);
    }
    [SetUp]
    public async Task SetUp()
    {
        repository = new(CurrentStore, NullLogger<RavenCharacterRepository>.Instance, UserRepository);
        principal = FakeUserPrincipal("u1", "CharUser");
        await WipeCollectionAsync<CharacterModel>();
        WaitForIndexing();
    }

    [Test]
    public async Task HostedService()
    {
        // Arrange
        
        // Act
        await repository.StartAsync(default);
        await repository.StopAsync(default);

        // Assert
    }

    [Test]
    public async Task SearchCharacters_OnEmptyDb_EmptyResult()
    {
        // Arrange
        
        // Act
        var result = await repository.SearchCharactersAsync(principal, "test", default);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task SearchCharacters_AfterCharacterCreation_ShouldReturnThatCharacter([Values] bool isOpponent)
    {
        // Arrange
        var create = CreateCharacterExample with { IsOpponent = isOpponent};
        
        // Act
        await repository.ApplyAsync(principal, create);
        WaitForIndexing();
        var result = await repository.SearchCharactersAsync(principal, "test", default);

        // Assert
        result.Should().BeEquivalentTo(new[] {
            new Character("ignore",
                create.Name,
                create.LpBaseCapacity,
                create.FoBaseCapacity,
                create.CustomColor,
                create.IsOpponent,
                create.ActionShorthands),
        }, opts => opts
            .Excluding(c => c.Id)
            .Excluding(c => c.UserId));
    }

    [Test]
    public async Task SearchCharacters_AfterCharacterCreation_ShouldOnlyReturnOwnCharacters([Values] bool isOpponent)
    {
        // Arrange
        var ownCreate = CreateCharacterExample with { IsOpponent = isOpponent};
        var otherPrincipal = FakeUserPrincipal(IdGenerator.RandomId(), "Other User");
        var otherCreate = CreateCharacterExample with { LpBaseCapacity = CreateCharacterExample.LpBaseCapacity + 1 };
        
        // Act
        await repository.ApplyAsync(principal, ownCreate);
        await repository.ApplyAsync(otherPrincipal, otherCreate);
        WaitForIndexing();
        var result = await repository.SearchCharactersAsync(principal, "test", default);

        // Assert
        result.Should().BeEquivalentTo(new[] {
            new Character("ignore",
                ownCreate.Name,
                ownCreate.LpBaseCapacity,
                ownCreate.FoBaseCapacity,
                ownCreate.CustomColor,
                ownCreate.IsOpponent,
                ownCreate.ActionShorthands),
        }, opts => opts
            .Excluding(c => c.Id)
            .Excluding(c => c.UserId));
    }

    [Test]
    public async Task ApplyCreateCharacter_ShouldBeOwnedByCreatingUser()
    {
        // Arrange
        var create = CreateCharacterExample;
        
        // Act
        var userId = await UserRepository.GetUserIdAsync(principal);
        await repository.ApplyAsync(principal, create);
        WaitForIndexing();
        var result = await repository.SearchCharactersAsync(principal, "test", default);

        // Assert
        result.Should().BeEquivalentTo(new[] {
            new Character("ignore",
                create.Name,
                create.LpBaseCapacity,
                create.FoBaseCapacity,
                create.CustomColor,
                create.IsOpponent,
                create.ActionShorthands),
        }, opts => opts
            .Excluding(c => c.Id)
            .Excluding(c => c.UserId));
        result[0].UserId.Should().Be(userId);
    }
}