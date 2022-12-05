using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Splitracker.Domain;

namespace Splitracker.Web.Shared.Timelines;

public partial class TimelineReadyCharacters
{
    [Parameter]
    [EditorRequired]
    public required IImmutableList<Character> Characters { get; set; }
    
    [Parameter]
    public Tick? SelectedTick { get; set; }
    
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
    
    
    void addCharacterClicked()
    {
        selectedCharacter = null;
    }

}