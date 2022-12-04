using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Utilities;

namespace Splitracker.Web.Shared.Timelines;

public partial class MultiTimeline
{
    /// <summary>
    /// The position the timeline itself and how the timeline items should be displayed.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Behavior)]
    public TimelinePosition TimelinePosition { get; set; } = TimelinePosition.Alternate;

    /// <summary>
    /// Aligns the dot and any item modifiers is changed, in default mode they are centered to the item.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Behavior)]
    public TimelineAlign TimelineAlign { get; set; } = TimelineAlign.Default;

    /// <summary>
    /// Reverse the order of TimelineItems when TimelinePosition is set to Alternate.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Behavior)]
    public bool Reverse { get; set; } = false;

    /// <summary>
    /// If true, disables all TimelineItem modifiers, like adding a caret to a MudCard.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Behavior)]
    public bool DisableModifiers { get; set; } = false;
    
    [Parameter]
    [EditorRequired]
    public required IEnumerable<string?> Labels { get; set; }

    protected string Classnames =>
        new CssBuilder("multi-timeline")
            .AddClass($"multi-timeline-vertical")
            .AddClass($"multi-timeline-position-{convertTimelinePosition().ToDescriptionString()}")
            .AddClass($"multi-timeline-align-end")
            .AddClass($"multi-timeline-modifiers", !DisableModifiers)
            .AddClass(Class)
            .Build();

    TimelinePosition convertTimelinePosition() =>
        TimelinePosition switch {
            TimelinePosition.Left => TimelinePosition.Start,
            TimelinePosition.Right => TimelinePosition.End,
            TimelinePosition.Top => TimelinePosition.Alternate,
            TimelinePosition.Bottom => TimelinePosition.Alternate,
            _ => TimelinePosition,
        };
}