// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Utilities;

namespace Splitracker.UI.Shared.Timelines;

public sealed partial class MultiTimelineItem : MudComponentBase, IDisposable
{
    const int SkipColumns = 1;
    public const int SpanColumns = 7;
    
    string classnames =>
        new CssBuilder("multi-timeline-item")
            .AddClass($"multi-timeline-item-end")
            .AddClass(Class)
            .Build();

    string dotClassnames =>
        new CssBuilder("multi-timeline-item-dot")
            .AddClass($"multi-timeline-dot-size-{Size.ToDescriptionString()}")
            .AddClass($"multi-elevation-{Elevation.ToString(CultureInfo.InvariantCulture)}")
            .Build();

    string dotInnerClassnames =>
        new CssBuilder("multi-timeline-item-dot-inner")
            .AddClass($"multi-timeline-dot-fill", Variant == Variant.Filled)
            .AddClass($"multi-timeline-dot-{Color.ToDescriptionString()}")
            .Build();

    public static string ComputeGridPosition(int track, int offset, int span, int height) =>
        $"grid-column: {track + SkipColumns + 1} / {track + SkipColumns + 1 + span}; " + 
        $"grid-row: {offset + 1} / {offset + 1 + height}; z-index: {50 - track}";
    public string GridPosition => ComputeGridPosition(Track, Offset, SpanColumns, 1);

    [CascadingParameter] public MudBaseItemsControl<MultiTimelineItem>? Parent { get; set; }

    /// <summary>
    /// Dot Icon
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Dot)]
    public string? Icon { get; set; }

    /// <summary>
    /// Variant of the dot.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Dot)]
    public Variant Variant { get; set; } = Variant.Outlined;

    /// <summary>
    /// User styles, applied to the lineItem dot.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Dot)]
    public string? DotStyle { get; set; }

    /// <summary>
    /// Color of the dot.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Dot)]
    public Color Color { get; set; } = Color.Default;

    /// <summary>
    /// Size of the dot.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Dot)]
    public Size Size { get; set; } = Size.Small;

    /// <summary>
    /// Elevation of the dot. The higher the number, the heavier the drop-shadow.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Dot)]
    public int Elevation { set; get; } = 1;

    /// <summary>
    /// If true, dot will not be displayed.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Dot)]
    public bool HideDot { get; set; }

    /// <summary>
    /// If used renders child content of the ItemContent.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Behavior)]
    public RenderFragment? ItemContent { get; set; }

    /// <summary>
    /// If used renders child content of the ItemDot.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Dot)]
    public RenderFragment? ItemDot { get; set; }

    /// <summary>
    /// Optional child content if no other RenderFragments is used.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Timeline.Behavior)]
    public RenderFragment? ChildContent { get; set; }
    
    [Parameter]
    [EditorRequired]
    public required int Track { get; set; }
    
    [Parameter]
    [EditorRequired]
    public required int Offset { get; set; }

    protected override Task OnInitializedAsync()
    {
        Parent?.Items.Add(this);
        return Task.CompletedTask;
    }

    void selectItem()
    {
        var myIndex = Offset;
        Parent?.MoveTo(myIndex);
    }

    public void Dispose()
    {
        Parent?.Items.Remove(this);
    }
}