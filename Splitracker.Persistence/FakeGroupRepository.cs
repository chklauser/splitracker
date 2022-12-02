using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Splitracker.Domain;
using Splitracker.Domain.Commands;

namespace Splitracker.Persistence;

class FakeGroupRepository : IGroupRepository
{
    public Task<IGroupRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        var char1 = new Character("x", "Alvin Buckmeyer", new LpPool(10), new(10));
        var char2 = new Character("y", "Brahm", new LpPool(12), new(8));
        var char3 = new Character("z", "Chuck Mistwater", new LpPool(6), new(25));
        var char4 = new Character("a", "Dorothy", new LpPool(8), new(20));
        var char5 = new Character("b", "Eugene", new LpPool(10), new(15));
        var char6 = new Character("c", "Felix", new LpPool(12), new(10));

        return Task.FromResult((IGroupRepositoryHandle)new FakeGroupRepositoryHandle(new[] {
            new FakeGroupHandle(new(
                "Sandkasten",
                ImmutableDictionary.CreateRange(
                    new[] { char1, char2, char3, char4, char5, char6 }.Select(c =>
                        new KeyValuePair<string, Character>(c.Id, c))))),
        }));
    }

    public async Task<IGroupHandle> OpenSingleAsync(ClaimsPrincipal principal, string groupId)
    {
        var handle = await OpenAsync(principal);
        return handle.Groups[0];
    }

    public Task ApplyAsync(ClaimsPrincipal principal, IGroupCommand groupCommand)
    {
        throw new NotImplementedException();
    }
}

class FakeGroupRepositoryHandle : IGroupRepositoryHandle
{
    public FakeGroupRepositoryHandle(IReadOnlyList<IGroupHandle> groups)
    {
        Groups = groups;
    }

    public ValueTask DisposeAsync()
    {
        GroupAdded = null;
        GroupRemoved = null;
        return default;
    }

    public IReadOnlyList<IGroupHandle> Groups { get; }
    public event EventHandler? GroupAdded;
    public event EventHandler? GroupRemoved;
}

class FakeGroupHandle : IGroupHandle
{
    public FakeGroupHandle(Group group)
    {
        Group = group;
    }

    public ValueTask DisposeAsync()
    {
        GroupUpdated = null;
        return default;
    }

    public Group Group { get; }
    public event EventHandler? GroupUpdated;
}