using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Splitracker.Domain;
using Splitracker.Domain.Commands;

namespace Splitracker.Web.Shared.Timelines;

public partial class TimelineReadyCharacters
{
    [Parameter]
    [EditorRequired]
    public required IImmutableList<Character> Characters { get; set; }
    
    [Parameter]
    public Tick? SelectedTick { get; set; }
    
    [CascadingParameter]
    public required ITimelineDispatcher Dispatcher { get; set; }
    
    bool isExpanded;

    Character? selectedCharacter;

    int insertionTick = 1;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if(SelectedTick != null)
        {
            insertionTick = SelectedTick.At;
        }
    }

    void selectCharacter(Character c)
    {
        if (!ReferenceEquals(selectedCharacter, c))
        {
            selectedCharacter = c;
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