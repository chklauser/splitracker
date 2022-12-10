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
    public const Breakpoint PersistentActionPanelBreakpoint = Breakpoint.SmAndDown;
    
    [Parameter]
    public required Timeline Timeline { get; set; }

    [Parameter]
    [EditorRequired]
    public required IReadOnlyDictionary<string, CharacterPermissions> Permissions { get; set; }

    [Parameter]
    public EventCallback<Tick> OnTickSelected { get; set; }
    
    [CascadingParameter]
    public required ITimelineDispatcher Dispatcher { get; set; }
    
    [Inject]
    public required TimelineLogic Logic { get; set; }

    IReadOnlyList<(Tick Tick, int Track, int Offset)>? allocatedTimeline;

    bool actionCardOpen = false;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        allocatedTimeline = allocateTracks(Timeline.Ticks).ToImmutableArray();
        trackFocusedTick();
    }
    
    #region Selection Management

    int selectedIndex = 0;
    int lastIndexClicked = 0;
    Tick? focusLock = null;

    async Task changeSelectedIndex(int newIndex)
    {
        if (selectedIndex != newIndex)
        {
            actionCardOpen = true;
            if (allocatedTimeline != null 
                && newIndex >= 0 
                && newIndex < allocatedTimeline.Count)
            {
                await OnTickSelected.InvokeAsync(allocatedTimeline[newIndex].Tick);
            }
        }
        selectedIndex = newIndex;
        lockOn();
    }
    
    void timelineClicked()
    {
        if (lastIndexClicked == selectedIndex)
        {
            actionCardOpen = !actionCardOpen;
        }

        lastIndexClicked = selectedIndex;
    }

    void lockOn()
    {
        if (allocatedTimeline != null
            && selectedIndex >= 0 && selectedIndex < allocatedTimeline.Count
            && allocatedTimeline[selectedIndex] is var focusCandidate and not (Empty, _, _))
        {
            focusLock = focusCandidate.Tick;
        }
        else
        {
            breakFocusLock();
        }
    }

    void breakFocusLock()
    {
        focusLock = null;
    }
    
    void trackFocusedTick()
    {
        if (focusLock == null || allocatedTimeline == null)
        {
            return;
        }

        static bool matchesFocusLock(Tick tick, Tick focusLock) =>
            (tick, focusLock) is (Tick.CharacterTick { Character.Id: var candidateCharId }, Tick.CharacterTick {
                Character.Id: var focusCharId,
            }) && candidateCharId == focusCharId
            || (tick, focusLock) is (Tick.EffectTick { Effect.Id: var candidateEffectId }, Tick.EffectTick {
                Effect.Id: var focusEffectId,
            }) && candidateEffectId == focusEffectId;

        var lockedOnIndex = allocatedTimeline
            .RadialSearch(selectedIndex)
            .Where( t => matchesFocusLock(t.Tick, focusLock) )
            .Select(x => (int?)x.Offset)
            .FirstOrDefault();
        if (lockedOnIndex is {} newSelectedIndex)
        {
            selectedIndex = newSelectedIndex;
        }
        else
        {
            // lost lock
            breakFocusLock();
        }
    }

    #endregion

    #region Action Card Display

    bool characterIsReadyNow(int offset) => offset == 0;
    bool effectIsHappeningNow(int offset) => offset == 0;
    bool characterCanReact(int offset) => allocatedTimeline != null && offset - allocatedTimeline[0].Tick.At < 30;

    #endregion
    
    #region Action Card Data

    readonly Dictionary<string, CharacterActionData> characterActionData = new();

    CharacterActionData getCharacterActionData(Character character) =>
        characterActionData.TryGetValue(character.Id, out var data) ? data : CharacterActionData.Default;
    void storeCharacterActionData(Character character, CharacterActionData data) {
        characterActionData[character.Id] = data;
    }

    async Task characterActionApplyClicked(CharacterActionData data, Character character)
    {
        if (data.Template == null)
        {
            return;
        }

        var cmd = Logic.ApplyAction(Timeline,
            data.Template,
            character,
            data.NumberOfTicks,
            data.Description);

        // Depending on the type of action, we either want to "follow" the
        // character along the timeline (and issue another action) or 
        // we want to break the lock to focus on the next character in line instead.
        if (data.Template.Type is not (
            ActionTemplateType.Bump 
            or ActionTemplateType.Reset
            or ActionTemplateType.Reaction))
        {
            breakFocusLock();
        }

        await Dispatcher.ApplyCommandAsync(cmd);
    }

    #endregion
    
    #region Timeline Layout

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
    
    #endregion
} 