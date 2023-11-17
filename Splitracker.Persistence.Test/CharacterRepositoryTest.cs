using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Persistence.Characters;

namespace Splitracker.Persistence.Test;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class CharacterRepositoryTest : RavenIntegrationTestBase
{
    RavenCharacterRepository repository = null!;
    ClaimsPrincipal principal = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        var repo = new RavenCharacterRepository(CurrentStore,
            NullLogger<RavenCharacterRepository>.Instance,
            UserRepository);
        await repo.StartAsync(default);
    }

    [SetUp]
    public async Task SetUp()
    {
        repository = new(CurrentStore, NullLogger<RavenCharacterRepository>.Instance, UserRepository);
        principal = FakeUserPrincipal("u1", "CharUser");
        await WipeCollectionAsync<Model.Character>();
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
        var create = CreateCharacterExample with { IsOpponent = isOpponent };

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
                    create.SplinterPointsMax,
                    create.CustomColor,
                    create.IsOpponent,
                    create.ActionShorthands),
            },
            opts => opts
                .Excluding(c => c.Id)
                .Excluding(c => c.ImplicitId)
                .Excluding(c => c.UserId)
                .Excluding(c => c.InsertedAt));
    }

    [Test]
    public async Task SearchCharacters_AfterCharacterCreation_ShouldOnlyReturnOwnCharacters([Values] bool isOpponent)
    {
        // Arrange
        var ownCreate = CreateCharacterExample with { IsOpponent = isOpponent };
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
                    ownCreate.SplinterPointsMax,
                    ownCreate.CustomColor,
                    ownCreate.IsOpponent,
                    ownCreate.ActionShorthands),
            },
            opts => opts
                .Excluding(c => c.Id)
                .Excluding(c => c.ImplicitId)
                .Excluding(c => c.UserId)
                .Excluding(c => c.InsertedAt));
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
                    create.SplinterPointsMax,
                    create.CustomColor,
                    create.IsOpponent,
                    create.ActionShorthands),
            },
            opts => opts
                .Excluding(c => c.Id)
                .Excluding(c => c.UserId)
                .Excluding(c => c.Lp.Points.Normalized)
                .Excluding(c => c.Fo.Points.Normalized)
                .Excluding(c => c.InsertedAt)
                .Excluding(c => c.ImplicitId)
                .IgnoringCyclicReferences());
        result[0].UserId.Should().Be(userId);
    }

    [Test]
    public async Task ApplyUpsertCharacter_UpdatesCharacter()
    {
        // Arrange
        var create = CreateCharacterExample;
        await repository.ApplyAsync(principal, create);
        WaitForIndexing();
        var created = await repository.SearchCharactersAsync(principal, create.Name, default);
        var edit = editCharacterExample(created[0].Id);
        var userId = await UserRepository.GetUserIdAsync(principal);
        await repository.ApplyAsync(principal, CreateCharacterExample);
        WaitForIndexing();
        var charactersBefore = await repository.SearchCharactersAsync(principal, create.Name, default);
        var otherCharacter = charactersBefore.First(c => c.Id != created[0].Id);

        // Act
        await repository.ApplyAsync(principal, edit);
        WaitForIndexing();

        // Assert
        var edited = await repository.SearchCharactersAsync(principal, edit.Name, default);
        edited.Should().BeEquivalentTo(new[] {
            new Character(created[0].Id,
                edit.Name,
                edit.LpBaseCapacity,
                edit.FoBaseCapacity,
                edit.SplinterPointsMax,
                edit.CustomColor,
                edit.IsOpponent,
                edit.ActionShorthands),
            new Character(otherCharacter.Id,
                otherCharacter.Name,
                otherCharacter.Lp.BaseCapacity,
                otherCharacter.Fo.BaseCapacity,
                otherCharacter.SplinterPoints.Max,
                otherCharacter.CustomColor,
                otherCharacter.IsOpponent,
                otherCharacter.ActionShorthands),
        }, opts => opts.Excluding(c => c.InsertedAt));
        edited[0].UserId.Should().Be(userId);
    }

    [Test]
    public async Task ApplyPoints_AddExhausted([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] { new(0, 5, 0) },
            new(0, 5, 0));
    }

    [Test]
    public async Task ApplyPoints_AddExhaustedToExisting([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(0, 5, 0),
                new(0, 3, 0),
            },
            new(0, 8, 0));
    }

    [Test]
    public async Task ApplyPoints_HealSomeExhausted([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(0, 5, 0),
                new(0, -3, 0),
            },
            new(0, 2, 0));
    }

    [Test]
    public async Task ApplyPoints_HealAllExhausted([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(0, 5, 0),
                new(0, -7, 0),
            },
            new(0, 0, 0));
    }

    [Test]
    public async Task ApplyPoints_ExhaustMaximum([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(0, 5, 0),
                new(0, 32, 0),
            },
            new(0, 30, 0));
    }

    [Test]
    public async Task ApplyPoints_AddConsumed([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] { new(0, 0, 5) },
            new(0, 0, 5));
    }

    [Test]
    public async Task ApplyPoints_AddConsumedToExisting([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(0, 0, 5),
                new(0, 0, 3),
            },
            new(0, 0, 8));
    }

    [Test]
    public async Task ApplyPoints_HealSomeConsumed([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(0, 0, 5),
                new(0, 0, -3),
            },
            new(0, 0, 2));
    }

    [Test]
    public async Task ApplyPoints_HealAllConsumed([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(0, 0, 5),
                new(0, 0, -7),
            },
            new(0, 0, 0));
    }

    [Test]
    public async Task ApplyPoints_ConsumeMaximum([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(0, 0, 5),
                new(0, 0, 32),
            },
            new(0, 0, 30));
    }

    [Test]
    public async Task ApplyPoints_AddChanneled([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] { new(5, 0, 0) },
            new(5, 0, 0),
            new[] { 5 });
    }

    [Test]
    public async Task ApplyPoints_AddChanneledToExisting([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(5, 0, 0),
                new(3, 0, 0),
            },
            new(8, 0, 0),
            new[] { 5, 3 });
    }

    [Test]
    public async Task ApplyPoints_ChannelMaximum([Values] PoolType poolType)
    {
        await testApplyPointsAsync(poolType,
            new PointsVec[] {
                new(5, 0, 0),
                new(32, 0, 0),
            },
            new(30, 0, 0),
            new[] { 5, 25 });
    }

    [Test]
    public async Task StopChanneling_Exists([Values] PoolType poolType)
    {
        // Arrange
        var (getPool, getOtherPool) = poolAccessorsFor(poolType);
        var original = await withCharacterCreated();
        await repository.ApplyAsync(principal, new ApplyPoints(original.Id, poolType, new(5, 0, 0), null));
        await repository.ApplyAsync(principal, new ApplyPoints(original.Id, poolType, new(2, 1, 3), null));
        await repository.ApplyAsync(principal, new ApplyPoints(original.Id, poolType, new(1, 0, 0), null));
        WaitForIndexing();
        var characterWithChannelings = await fetchCharacterAsync(original.Id);
        var ch5 = getPool(characterWithChannelings).Channelings.Single(ch => ch.Value == 5);
        var ch2 = getPool(characterWithChannelings).Channelings.Single(ch => ch.Value == 2);
        var ch1 = getPool(characterWithChannelings).Channelings.Single(ch => ch.Value == 1);

        // Act
        await repository.ApplyAsync(principal, new StopChanneling(original.Id, poolType, ch2.Id));
        WaitForIndexing();

        // Assert
        var updated = await fetchCharacterAsync(original.Id);
        getPool(updated).Should().BeEquivalentTo(new LpPool(
                getPool(original).BaseCapacity,
                new(6, 3, 3),
                ImmutableArray.Create(ch5, ch1)
            ),
            opts => opts.Excluding(b => b.TotalCapacity));
        getOtherPool(updated).Should().BeEquivalentTo(getOtherPool(original));
    }

    [Test]
    public async Task StopChanneling_DoesntExist([Values] PoolType poolType)
    {
        // Arrange
        var (getPool, getOtherPool) = poolAccessorsFor(poolType);
        var original = await withCharacterCreated();
        await repository.ApplyAsync(principal, new ApplyPoints(original.Id, poolType, new(5, 0, 0), null));
        await repository.ApplyAsync(principal, new ApplyPoints(original.Id, poolType, new(6, 1, 3), null));
        await repository.ApplyAsync(principal, new ApplyPoints(original.Id, poolType, new(1, 0, 0), null));
        WaitForIndexing();
        var characterWithChannelings = await fetchCharacterAsync(original.Id);
        var ch5 = getPool(characterWithChannelings).Channelings.Single(ch => ch.Value == 5);
        var ch6 = getPool(characterWithChannelings).Channelings.Single(ch => ch.Value == 6);
        var ch1 = getPool(characterWithChannelings).Channelings.Single(ch => ch.Value == 1);

        // Act
        await repository.ApplyAsync(principal, new StopChanneling(original.Id, poolType, "id that doesn't exist"));
        WaitForIndexing();

        // Assert
        var updated = await fetchCharacterAsync(original.Id);
        getPool(updated).Should().BeEquivalentTo(new LpPool(
                getPool(original).BaseCapacity,
                new(12, 1, 3),
                ImmutableArray.Create(
                    ch5,
                    ch6,
                    ch1
                )
            ),
            opts => opts
                .Excluding(b => b.TotalCapacity));
        getOtherPool(updated).Should().BeEquivalentTo(getOtherPool(original));
    }

    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(4, 3)]
    [TestCase(-1, 0)]
    [TestCase(0, 0)]
    public async Task ApplyUseSplinterPoints_ReducesPoints(int amount, int expectedUsed)
    {
        // Arrange
        var original = await withCharacterCreated();

        // Act
        await repository.ApplyAsync(principal, new UseSplinterPoints(original.Id, amount));
        
        // Assert
        var updated = await fetchCharacterAsync(original.Id);
        updated.Should().BeEquivalentTo(original with {
            SplinterPoints = original.SplinterPoints with { Used = expectedUsed },
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(-1)]
    [TestCase(0)]
    public async Task ApplyUseSplinterPoints_ReducesPoints(int amount)
    {
        // Arrange
        var original = await withCharacterCreated();
        await repository.ApplyAsync(principal, new UseSplinterPoints(original.Id, amount));

        // Act
        await repository.ApplyAsync(principal, new ResetSplinterPoints(original.Id));
        
        // Assert
        var updated = await fetchCharacterAsync(original.Id);
        updated.Should().BeEquivalentTo(original);
    }

    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(4, 3)]
    [TestCase(-1, 0)]
    [TestCase(0, 0)]
    public async Task ApplyUseSplinterPoints_RetainedThroughSplinterPointEdit(int amount, int expectedUsed)
    {
        // Arrange
        var original = await withCharacterCreated();
        var expectedSplinterPointMaximum = original.SplinterPoints.Max + 1;

        // Act
        await repository.ApplyAsync(principal, new UseSplinterPoints(original.Id, amount));
        await repository.ApplyAsync(principal,
            new EditCharacter(original.Id,
                original.Name,
                original.Lp.BaseCapacity,
                original.Fo.BaseCapacity,
                expectedSplinterPointMaximum, // <-- the edit
                original.CustomColor,
                original.ActionShorthands,
                original.IsOpponent,
                []));
        
        // Assert
        var updated = await fetchCharacterAsync(original.Id);
        updated.Should().BeEquivalentTo(original with {
            SplinterPoints = original.SplinterPoints with { Used = expectedUsed, Max = expectedSplinterPointMaximum },
        });
    }

    #region Test Support

    async Task testApplyPointsAsync(
        PoolType poolType,
        IEnumerable<PointsVec> inputPoints,
        PointsVec expectedPoints,
        IEnumerable<int>? expectedChannelings = null
    )
    {
        // Arrange
        var original = await withCharacterCreated();

        // Act
        foreach (var vec in inputPoints)
        {
            await repository.ApplyAsync(principal, new ApplyPoints(original.Id, poolType, vec, null));
        }

        WaitForIndexing();

        // Assert
        var updated = await fetchCharacterAsync(original.Id);

        var (getPool, getOtherPool) = poolAccessorsFor(poolType);

        getPool(updated).Should().BeEquivalentTo(new LpPool(
                getPool(original).BaseCapacity,
                expectedPoints,
                expectedChannelings?.Select(v => new Channeling("ignored", v)).ToImmutableArray()
                ?? ImmutableArray<Channeling>.Empty),
            opts => opts
                .Excluding(b => b.TotalCapacity)
                .For(b => b.Channelings).Exclude(b => b.Id)
        );

        getOtherPool(updated).Should().BeEquivalentTo(getOtherPool(original));
    }

    static (Func<Character, Pool> getPool, Func<Character, Pool> getOtherPool) poolAccessorsFor(
        PoolType poolType
    )
    {
        static Pool fo(Character c) => c.Fo;
        static Pool lp(Character c) => c.Lp;
        Func<Character, Pool> getPool = poolType switch {
            PoolType.Lp => lp,
            PoolType.Fo => fo,
            _ => throw new ArgumentOutOfRangeException(nameof(poolType), poolType, null),
        };
        Func<Character, Pool> getOtherPool = poolType switch {
            PoolType.Lp => fo,
            PoolType.Fo => lp,
            _ => throw new ArgumentOutOfRangeException(nameof(poolType), poolType, null),
        };
        return (getPool, getOtherPool);
    }

    async Task<Character> fetchCharacterAsync(string characterDocumentId)
    {
        Character updated;
        await using (var handle = await repository.OpenAsync(principal))
        {
            updated = handle.Characters.First(c => c.Character.Id == characterDocumentId).Character;
        }

        return updated;
    }

    async Task<Character> withCharacterCreated()
    {
        var create = CreateCharacterExample with {
            LpBaseCapacity = 6,
            FoBaseCapacity = 6 * 5,
        };
        await repository.ApplyAsync(principal, create);
        WaitForIndexing();
        await using var handle = await repository.OpenAsync(principal);

        return handle.Characters[0].Character;
    }

    static readonly CreateCharacter CreateCharacterExample = new("My Name is Test McTestington",
        5,
        6,
        3,
        "#112233",
        ImmutableDictionary<string, ActionShorthand>.Empty
            .Add("a1", new("a1", "Action 1", null, 2, ActionShorthandType.Melee, null))
            .Add("a2", new("a2", "Action 2", "Bogen", 3, ActionShorthandType.Ranged, null))
            .Add("a3", new("a3", "Action 3", "Feuerball", 4, ActionShorthandType.Spell, "K3v1")),
        default,
        []);

    static EditCharacter editCharacterExample(
        string docId,
        ClaimsPrincipal? customPrincipal = null
    ) => new(
        docId,
        "test",
        3,
        4,
        4,
        "#886633",
        CreateCharacterExample.ActionShorthands
            .SetItem("a2", new("a2", "Edited Action 2", null, 6, ActionShorthandType.Melee, null))
            .Add("a4", new("a4", "Action 4", "Armbrust", 5, ActionShorthandType.Ranged, null)),
        !CreateCharacterExample.IsOpponent,
        []);

    #endregion
}