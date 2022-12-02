using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Splitracker.Domain;
using Splitracker.Domain.Commands;

namespace Splitracker.Persistence.Timelines;

class FakeTimelineRepository : ITimelineRepository
{
    public Task<ITimelineHandle?> OpenSingleAsync(ClaimsPrincipal principal, string groupId)
    {
        var char1 = new Character("x", "Alvin Buckmeyer", new LpPool(10), new(10));
        var char2 = new Character("y", "Brahm", new LpPool(12), new(8));
        var char3 = new Character("z", "Chuck Mistwater", new LpPool(6), new(25));
        var char4 = new Character("a", "Dorothy", new LpPool(8), new(20));
        var char5 = new Character("b", "Eugene", new LpPool(10), new(15));
        var char6 = new Character("c", "Felix", new LpPool(12), new(10));

        var poison = new Effect("g", "Gift", 1, 30, ImmutableArray.Create(char6), 4);
        var dazed = new Effect("d", "Benommen", 3, 7, ImmutableArray.Create(char5));
        
        return Task.FromResult(
            (ITimelineHandle?)new FakeTimelineHandle(new Timeline(
                "x",
                "Sandkasten",
                ImmutableDictionary.CreateRange(
                    new[] { char1, char2, char3, char4, char5, char6 }.Select(c =>
                        new KeyValuePair<string, Character>(c.Id, c))),
                ImmutableDictionary.CreateRange(new[]{poison, dazed}.Select(e => new KeyValuePair<string, Effect>(e.Id, e))),
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
            )))
        );
    }

    public Task ApplyAsync(ClaimsPrincipal principal, ITimelineCommand groupCommand)
    {
        throw new NotImplementedException();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

class FakeTimelineHandle : ITimelineHandle
{
    public FakeTimelineHandle(Timeline timeline)
    {
        Timeline = timeline;
    }

    public ValueTask DisposeAsync()
    {
        Updated = null;
        return default;
    }

    public Timeline Timeline { get; }
    public event EventHandler? Updated;
    
    [PublicAPI]
    public void TriggerUpdate(Timeline timeline)
    {
        Updated?.Invoke(this, EventArgs.Empty);
    }
}