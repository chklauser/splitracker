using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Splitracker.Domain;

namespace Splitracker.UI.Shared.Timelines;

partial class TimelineCharacterActionCard
{
    [Parameter]
    [EditorRequired]
    public required Tick.CharacterTick Tick { get; set; }
    
    [Parameter]
    [EditorRequired]
    public required CharacterPermissions Permissions { get; set; }

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
    
    [Parameter]
    public EventCallback OnCloseButtonClicked { get; set; }

    internal ActionTemplate? SelectedActionTemplate => ActionData.Template;

    int numberOfTicks => ActionData.NumberOfTicks;

    internal EventCallback<ActionTemplate> ActionTemplateSelected;

    bool canInteract => Permissions.HasFlag(CharacterPermissions.InteractOnTimeline);

    static int minNumberOfTicks(ActionTemplate? selectedActionTemplate) => 0;

    static int maxNumberOfTicks(ActionTemplate? selectedActionTemplate) => 
        selectedActionTemplate is { Max: { } customMax } ? Math.Max(1, customMax) : 100;

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
    
    MudNumericField<int>? numberOfTicksField;
    
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
            if (hasTicksParameter && numberOfTicksField != null)
            {
                await numberOfTicksField.SelectAsync();
            }
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

    async Task descriptionChanged(string? newDescription)
    {
        await ActionDataChanged.InvokeAsync(ActionData with {
            Description = string.IsNullOrWhiteSpace(newDescription) ? null : newDescription
        });
    }

    async Task applyBump(ActionTemplate bump, int delta)
    {
        await OnApplyActionClicked.InvokeAsync(new(bump, delta, null));
    }
}