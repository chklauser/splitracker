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

        var poison = new Effect("g", "Gift", 1, 30, ImmutableArray.Create(char6), 4);
        var dazed = new Effect("d", "Benommen", 3, 7, ImmutableArray.Create(char5));
        
        return Task.FromResult((IGroupRepositoryHandle)new FakeGroupRepositoryHandle(new[] {
            new FakeGroupHandle(new Group(
                ImmutableDictionary.CreateRange(
                    new[] { char1, char2, char3, char4, char5, char6 }.Select(c =>
                        new KeyValuePair<string, Character>(c.Id, c))),
                ImmutableList.Create(char3),
                ImmutableList.Create<Tick>(
                    new Tick.Recovers(char1, 3),
                    new Tick.ActionEnds(char2, 5, 1, "Fokus"),
                    new Tick.Recovers(char4, 5),
                    new Tick.EffectTicks(poison, 5),
                    new Tick.EffectTicks(poison, 9),
                    new Tick.ActionEnds(char5, 10, 2, "Bewegen"),
                    new Tick.EffectEnds(dazed, 10),
                    new Tick.EffectTicks(poison, 13),
                    new Tick.Recovers(char6, 13),
                    new Tick.EffectTicks(poison, 17),
                    new Tick.EffectTicks(poison, 21),
                    new Tick.EffectTicks(poison, 24),
                    new Tick.EffectTicks(poison, 27)
            ))),
        }));
    }

    public async Task<IGroupHandle> OpenSingleAsync(ClaimsPrincipal principal, string groupId)
    {
        var handle = await OpenAsync(principal);
        return handle.Groups.First();
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