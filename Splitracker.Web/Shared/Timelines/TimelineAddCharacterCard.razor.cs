using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Splitracker.Domain;

namespace Splitracker.Web.Shared.Timelines;

public partial class TimelineAddCharacterCard
{
    [Parameter]
    [EditorRequired]
    public required int SelectedTick { get; set; }
    
    [Parameter]
    public EventCallback OnCharacterAdded { get; set; }
    
    [CascadingParameter]
    public required ITimelineDispatcher Dispatcher { get; set; }

    string addCharacterButtonTooltip => selectedCharacter == null ? "Bitte wähl unten einen Charakter aus" :
        directToReady ? $"{selectedCharacter} zur Liste der abwartenden Charaktere hinzufügen" :
        $"{selectedCharacter.Name} bei Tick {at} einreihen";

    Character? selectedCharacter;
    
    int at;

    bool directToReady;
    
    protected override void OnParametersSet()
    {
        at = SelectedTick;
    }
    
    async Task<IEnumerable<Character>> searchCharacters(string? search, CancellationToken cancellationToken)
    {
        return await Dispatcher.SearchCharactersAsync(search ?? "", cancellationToken);
    }

    async Task addCharacterButtonClicked()
    {
        await OnCharacterAdded.InvokeAsync();
    }
}