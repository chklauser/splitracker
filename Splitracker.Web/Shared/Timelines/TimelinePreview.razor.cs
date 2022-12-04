using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Splitracker.Domain;

namespace Splitracker.Web.Shared.Timelines;

partial class TimelinePreview
{
    [Parameter]
    public required Timeline Timeline { get; set; }

    IReadOnlyList<(Tick Tick, int Track, int Offset)>? allocatedTimeline;

    bool actionCardOpen = false;

    int selectedIndex = 0;
    int lastIndexClicked = 0;

    void changeSelectedIndex(int newIndex)
    {
        if (selectedIndex != newIndex)
        {
            actionCardOpen = true;
        }
        selectedIndex = newIndex;
    }
    
    void timelineClicked()
    {
        if (lastIndexClicked == selectedIndex)
        {
            actionCardOpen = !actionCardOpen;
        }

        lastIndexClicked = selectedIndex;
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        allocatedTimeline = allocateTracks(Timeline.Ticks).ToImmutableArray();
    }

    IEnumerable<string?> timelineLabels()
    {
        if (allocatedTimeline == null)
        {
            yield break;
        }

        var currentTick = -1;
        foreach (var (tick, _, _) in allocatedTimeline)
        {
            if(tick.At != currentTick)
            {
                currentTick = tick.At;
                yield return currentTick.ToString();
            }
            else
            {
                yield return null;
            }
        }
    }

    static (int startTick, int endTick) allocateTicks(IReadOnlyList<Tick> timeline)
    {
        var startTick = timeline.Count > 0 ? timeline[0].At : 1;
        var endTick = Math.Max(timeline.Count > 0 ? timeline[^1].At : 1, startTick + 14);
        return (startTick, endTick);
    }

    IEnumerable<(Tick Tick, int Track, int Offset)> allocateTracks(IReadOnlyList<Tick> timeline)
    {
        var (startTick, endTick) = allocateTicks(timeline);
        var currentTick = startTick - 1;
        var nextOffset = 0;
        var nextTrack = 1;
        var effectTracks = new Dictionary<string, int>();
        foreach (var tick in timeline)
        {
            if (tick.At > currentTick + 1)
            {
                // we skipped some ticks, so we need to fill in the gaps
                foreach (var i in Enumerable.Range(currentTick + 1, tick.At - currentTick - 1))
                {
                    yield return (new Empty(i), 0, nextOffset);
                    nextOffset += 1;
                }
            }

            currentTick = tick.At;

            var track = tick switch {
                Tick.Recovers => 0,
                Tick.ActionEnds => nextTrack++,
                Tick.EffectTicks { Effect.Id: var id } when effectTracks.TryGetValue(id, out var t) => t,
                Tick.EffectTicks { Effect.Id: var id } => effectTracks[id] = nextTrack++,
                Tick.EffectEnds { Effect.Id: var id } when effectTracks.TryGetValue(id, out var t) => t,
                Tick.EffectEnds { Effect.Id: var id } => effectTracks[id] = nextTrack++,
                _ => throw new($"Unknown tick type {tick.GetType()}"),
            };
            yield return (tick, track, nextOffset);
            nextOffset += 1;
        }
        
        while (currentTick < endTick)
        {
            currentTick += 1;
            yield return (new Empty(currentTick), 0, nextOffset);
            nextOffset += 1;
        }
    }

    record Empty(int At) : Tick(At);
} 