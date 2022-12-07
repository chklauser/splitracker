using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Splitracker.Domain;
using Splitracker.Domain.Commands;

namespace Splitracker.Web.Shared.Timelines;

public partial class TimelineReadyCharacters
{
    [Parameter]
    [EditorRequired]
    public required IImmutableList<Character> Characters { get; set; }
    
    [Parameter]
    [EditorRequired]
    public required IReadOnlyDictionary<string, CharacterPermissions> Permissions { get; set; }

    [Parameter]
    public Tick? SelectedTick { get; set; }
    
    [CascadingParameter]
    public required ITimelineDispatcher Dispatcher { get; set; }
    
    bool isExpanded;

    Character? selectedCharacter;

    int insertionTick = 1;

    MudNumericField<int>? insertionTickField;

    bool canInteractWith(Character character)
    {
        return Permissions[character.Id].HasFlag(CharacterPermissions.InteractOnTimeline);
    }
    
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if(SelectedTick != null)
        {
            insertionTick = SelectedTick.At;
        }
    }

    async Task selectCharacter(Character c)
    {
        if (!ReferenceEquals(selectedCharacter, c))
        {
            selectedCharacter = c;
            if (insertionTickField is { } field)
            {
                await field.SelectAsync();
            }
        }
        else
        {
            selectedCharacter = null;
        }
    }


    async Task addCharacterClicked()
    {
        if (selectedCharacter == null)
        {
            return;
        }

        await Dispatcher.ApplyCommandAsync(new TimelineCommand.SetCharacterRecovered(null!, selectedCharacter.Id, insertionTick, 1));
        selectedCharacter = null;
    }

}