using System;
using Microsoft.AspNetCore.Components;
using Splitracker.Domain;

namespace Splitracker.Web.Shared.Timelines;

partial class TimelineCharacterActionCard
{
    [Parameter]
    [EditorRequired]
    public required Tick.CharacterTick Tick { get; set; }
    
    [Parameter]
    [EditorRequired]
    public required bool IsReadyNow { get; set; }
    
    internal ActionTemplate? SelectedActionTemplate;

    internal EventCallback<ActionTemplate> ActionTemplateSelected;

    int numberOfTicks = 1;
    
    int minNumberOfTicks => SelectedActionTemplate is { Min: var customMin } ? customMin : 0;
    int maxNumberOfTicks => SelectedActionTemplate is { Max: { } customMax } ? customMax : 100;
    bool hasTicksParameter => SelectedActionTemplate is null or { Type: not ActionTemplateType.Ready };

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ActionTemplateSelected = new EventCallbackFactory().Create<ActionTemplate>(
            this, 
            actionTemplateSelectedHandler);
    }

    void actionTemplateSelectedHandler(ActionTemplate e)
    {
        // Clicking an already selected action should deselect it
        if (ReferenceEquals(e, SelectedActionTemplate))
        {
            SelectedActionTemplate = null;
        }
        else
        {
            SelectedActionTemplate = e;
            numberOfTicks = SelectedActionTemplate is { Default: { } defaultTicks }
                ? defaultTicks
                : Math.Clamp(numberOfTicks, minNumberOfTicks, maxNumberOfTicks);
        }
    }

    #region Pre-defined actions

    static readonly ActionTemplate Immediate = new("__immediate", "Sofort", ActionTemplateType.Immediate);
    static readonly ActionTemplate Continuous = new("__continuous", "Kont.", ActionTemplateType.Continuous);
    static readonly ActionTemplate Move = new("__move", "Bewegen", ActionTemplateType.Continuous, Default: 5);
    static readonly ActionTemplate Ready = new("__ready", "Abwarten", ActionTemplateType.Ready);

    static readonly ActionTemplate Focus = new(
        "__focus",
        "Fokus",
        ActionTemplateType.Continuous,
        Description: "Magie fokussieren");

    static readonly ActionTemplate Aim = new(
        "__aim",
        "Zielen",
        ActionTemplateType.Continuous,
        Default: 1,
        Max: 3,
        Multiplier: 2);
    
    static readonly ActionTemplate BumpForward = new(
        "__bump_forward", 
        "Position", 
        ActionTemplateType.Bump, 
        Min: 0, 
        Max: 0, 
        Default: 0);
    static readonly ActionTemplate BumpBackward = new(
        "__bump_backward", 
        "Position", 
        ActionTemplateType.Bump, 
        Min: 0, 
        Max: 0, 
        Default: 0);

    #endregion
}