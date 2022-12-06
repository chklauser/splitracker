using System;
using System.Threading.Tasks;
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

    [Parameter]
    [EditorRequired]
    public required bool CanReact { get; set; }

    [Parameter]
    public required CharacterActionData ActionData { get; set; }

    [Parameter]
    public EventCallback<CharacterActionData> ActionDataChanged { get; set; }

    [Parameter]
    public EventCallback<CharacterActionData> OnApplyActionClicked { get; set; }

    internal ActionTemplate? SelectedActionTemplate => ActionData.Template;

    int numberOfTicks => ActionData.NumberOfTicks;

    internal EventCallback<ActionTemplate> ActionTemplateSelected;

    static int minNumberOfTicks(ActionTemplate? selectedActionTemplate) => 
        selectedActionTemplate is { Min: var customMin } ? customMin : 0;

    static int maxNumberOfTicks(ActionTemplate? selectedActionTemplate) => 
        selectedActionTemplate is { Max: { } customMax } ? customMax : 100;

    bool hasTicksParameter =>
        SelectedActionTemplate is null or
            { Type: not (ActionTemplateType.Ready or ActionTemplateType.Reset or ActionTemplateType.Leave) };

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ActionTemplateSelected = new EventCallbackFactory().Create<ActionTemplate>(
            this,
            actionTemplateSelectedHandler);
    }

    async Task applyActionClicked()
    {
        var data = ActionData;
        if (data.Template?.FollowUp is { } followUp)
        {
            await ActionDataChanged.InvokeAsync(applyTemplateToActionData(followUp));
        }
        else if (data.Template?.AvoidRepetition is true)
        {
            await ActionDataChanged.InvokeAsync(ActionData with { Template = null });
        }

        await OnApplyActionClicked.InvokeAsync(data);
    }
    
    async Task changeNumberOfTicks(int newNumberOfTicks)
    {
        await ActionDataChanged.InvokeAsync(ActionData with { NumberOfTicks = newNumberOfTicks });
    }
    
    async Task actionTemplateSelectedHandler(ActionTemplate next)
    {
        // Clicking an already selected action should deselect it
        if (ReferenceEquals(next, SelectedActionTemplate))
        {
            await ActionDataChanged.InvokeAsync(ActionData with { Template = null });
        }
        else
        {
            await ActionDataChanged.InvokeAsync(applyTemplateToActionData(next));
        }
    }

    CharacterActionData applyTemplateToActionData(ActionTemplate next)
    {
        return ActionData with {
            Template = next,
            NumberOfTicks = next is { Default: { } defaultTicks }
                ? defaultTicks
                : Math.Clamp(ActionData.NumberOfTicks, minNumberOfTicks(next), maxNumberOfTicks(next)),
            Description = next is {Description: {} defaultDescription} 
                ? defaultDescription 
                : ActionData.Description,
        };
    }

    async Task descriptionChanged(string newDescription)
    {
        await ActionDataChanged.InvokeAsync(ActionData with {
            Description = string.IsNullOrWhiteSpace(newDescription) ? null : newDescription
        });
    }

    async Task applyBump(ActionTemplate bump, int delta)
    {
        await OnApplyActionClicked.InvokeAsync(new(bump, delta, null));
    }

    #region Pre-defined actions

    static readonly ActionTemplate Immediate = new("__immediate", "Sofort", ActionTemplateType.Immediate);
    static readonly ActionTemplate Continuous = new("__continuous", "Kont.", ActionTemplateType.Continuous);
    static readonly ActionTemplate Move = new(
        "__move",
        "Bewegen",
        ActionTemplateType.Continuous,
        Default: 5,
        Description: "Bewegen");
    static readonly ActionTemplate Sprint = new(
        "__sprint",
        "Sprinten",
        ActionTemplateType.Continuous,
        Default: 10,
        Description: "Sprinten");
    static readonly ActionTemplate Ready = new("__ready", "Abwarten", ActionTemplateType.Ready);

    static readonly ActionTemplate CastSpell = new(
        "__cast_spell",
        "Zauber auslösen",
        ActionTemplateType.Immediate,
        Default: 3,
        AvoidRepetition: true);

    static readonly ActionTemplate Focus = new(
        "__focus",
        "Fokus",
        ActionTemplateType.Continuous,
        Description: "Magie fokussieren",
        FollowUp: CastSpell);

    static readonly ActionTemplate Shoot = new(
        "__aim",
        "Fernk. auslösen",
        ActionTemplateType.Immediate,
        Default: 3,
        AvoidRepetition: true);
    
    static readonly ActionTemplate PrepareRanged = new(
        "__prepare_ranged",
        "Fernk. vorbereiten",
        ActionTemplateType.Continuous,
        Description: "Fernkampf vorbereiten",
        FollowUp: Shoot);

    static readonly ActionTemplate Aim = new(
        "__aim",
        "Zielen",
        ActionTemplateType.Continuous,
        Description: "Zielen",
        Default: 1,
        Max: 3,
        Multiplier: 2,
        FollowUp: Shoot,
        AvoidRepetition: true);

    static readonly ActionTemplate LookForGap = new(
        "__look_for_gap",
        "Lücke suchen",
        ActionTemplateType.Continuous,
        Description: "Lücke suchen",
        Default: 1,
        Max: 3,
        Multiplier: 2,
        AvoidRepetition: true);

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

    static readonly ActionTemplate Reaction = new(
        "__reaction",
        "Reaktion",
        ActionTemplateType.Reaction,
        AvoidRepetition: true);

    static readonly ActionTemplate ActiveDefense = new(
        "__active_defense",
        "Aktive Abwehr",
        ActionTemplateType.Reaction,
        Default: 3);

    static readonly ActionTemplate AbortContinuousAction = new(
        "__abort_action",
        "Kontinuierliche Aktion abbrechen",
        ActionTemplateType.Reset,
        CustomLabel: "Aktion abbrechen",
        AvoidRepetition: true);


    static readonly ActionTemplate LeaveTimeline = new(
        "__leave_timeline",
        "Charakter Entfernen",
        ActionTemplateType.Leave,
        CustomLabel: "Entfernen");

    #endregion
}