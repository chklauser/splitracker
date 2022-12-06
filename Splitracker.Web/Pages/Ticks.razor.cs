using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Web.Shared;
using Splitracker.Web.Shared.Timelines;

namespace Splitracker.Web.Pages;

partial class Ticks : IAsyncDisposable, ITimelineDispatcher, ICharacterCommandRouter
{
    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationState { get; set; }

    [Inject]
    public required ITimelineRepository Repository { get; set; }
    
    [Inject]
    public required ICharacterRepository CharacterRepository { get; set; }

    [Inject]
    public required NavigationManager Nav { get; set; }

    ITimelineHandle? handle;

    Tick? selectedTick;

    protected override async Task OnParametersSetAsync()
    {
        if (handle != null)
        {
            await handle.DisposeAsync();
        }

        handle = null;
        StateHasChanged();

        await base.OnInitializedAsync();
        var auth = await AuthenticationState;
        var newHandle = await Repository.OpenSingleAsync(auth.User, groupId);
        if (newHandle == null)
        {
            Nav.NavigateTo("/not-found");
        }
        else
        {
            newHandle.Updated += (_, _) => InvokeAsync(StateHasChanged);
            handle = newHandle;
        }
    }

    bool addEffectPanelOpen;

    void toggleAddEffectPanel()
    {
        addEffectPanelOpen = !addEffectPanelOpen;
        addCharacterPanelOpen = false;
    }

    bool addCharacterPanelOpen;
    string groupId = "Groups/0000000000000000021-A";

    void toggleAddCharacterPanel()
    {
        addCharacterPanelOpen = !addCharacterPanelOpen;
        addEffectPanelOpen = false;
    }

    void tickSelected(Tick tick)
    {
        selectedTick = tick;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (handle != null)
        {
            await handle.DisposeAsync();
        }
    }

    public async Task<IEnumerable<Character>> SearchCharactersAsync(
        string searchTerm,
        CancellationToken cancellationToken
    )
    {
        var auth = await AuthenticationState;
        return await Repository.SearchCharactersAsync(searchTerm, groupId, auth.User, cancellationToken);
    }

    public async Task ApplyCommandAsync(TimelineCommand command)
    {
        var auth = await AuthenticationState;
        await Repository.ApplyAsync(auth.User,
            command switch {
                TimelineCommand.AddEffect addEffect => addEffect with { GroupId = groupId },
                TimelineCommand.BumpCharacter bumpCharacter => bumpCharacter with { GroupId = groupId },
                TimelineCommand.AddCharacter addCharacter => addCharacter with { GroupId = groupId },
                TimelineCommand.RemoveCharacter removeCharacter => removeCharacter with { GroupId = groupId },
                TimelineCommand.RemoveEffect removeEffect => removeEffect with { GroupId = groupId },
                TimelineCommand.RemoveEffectTick removeTick => removeTick with { GroupId = groupId },
                TimelineCommand.SetCharacterActionEnded setCharacterActionEnded => setCharacterActionEnded with {
                    GroupId = groupId,
                },
                TimelineCommand.SetCharacterReady setCharacterReady => setCharacterReady with { GroupId = groupId },
                TimelineCommand.SetCharacterRecovered setCharacterRecovered => setCharacterRecovered with {
                    GroupId = groupId,
                },
                _ => throw new ArgumentOutOfRangeException(nameof(command)),
            });
    }

    class NonSubscribingCharacterHandle : ICharacterHandle
    {
        public NonSubscribingCharacterHandle(Character character)
        {
            Character = character;
        }

        public Character Character { get; }
        public event EventHandler? CharacterUpdated
        {
            add { }
            remove { }
        }
    }

    #region Character editing support

    bool characterEditPanelOpen;

    public IEnumerable<Character> PlayerCharacters =>
        handle?.Timeline.Characters.Values ?? Enumerable.Empty<Character>();

    public async Task ApplyAsync(ICharacterCommand command)
    {
        var auth = await AuthenticationState;
        await CharacterRepository.ApplyAsync(auth.User, command);
    }
    
    #endregion
}