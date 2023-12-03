using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Splitracker.Domain;
using Splitracker.Domain.Commands;

namespace Splitracker.UI.Shared.Timelines;

public partial class TimelineAddCharacterCard
{
    [Parameter]
    [EditorRequired]
    public required int SelectedTick { get; set; }
    [Parameter]
    public EventCallback OnCharacterAdded { get; set; }
    
    [Parameter]
    public EventCallback OnCloseButtonClicked { get; set; }

    [CascadingParameter]
    public required ITimelineDispatcher Dispatcher { get; set; }

    string addCharacterButtonTooltip => selectedCharacter == null ? "Bitte wähl unten einen Charakter aus" :
        directToReady ? $"{selectedCharacter.Name} zur Liste der abwartenden Charaktere hinzufügen" :
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
        if (selectedCharacter == null)
        {
            return;
        }

        await Dispatcher.ApplyCommandAsync(new TimelineCommand.AddCharacter(
            null!,
            selectedCharacter.Id,
            directToReady ? null : at));
        await OnCharacterAdded.InvokeAsync();
    }
    
    string renderCharacterForCompletion(Character c)
    {
        return c?.Name ?? string.Empty;
    }
}