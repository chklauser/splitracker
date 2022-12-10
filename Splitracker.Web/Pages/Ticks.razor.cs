using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using Splitracker.Domain;
using Splitracker.Domain.Commands;
using Splitracker.Web.Shared;
using Splitracker.Web.Shared.Timelines;

namespace Splitracker.Web.Pages;

partial class Ticks : IAsyncDisposable, ITimelineDispatcher, ICharacterCommandRouter
{
    public const Breakpoint PersistentCharacterCardBreakpoint = Breakpoint.MdAndDown;

    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationState { get; set; }
    
    [CascadingParameter]
    public required IPermissionService Permissions { get; set; }
    
    [CascadingParameter]
    public required FlagContext Flags { get; set; }

    [Parameter]
    public required string GroupIdRaw { get; set; }

    string groupId => GroupInfo.IdFor(GroupIdRaw);

    [Inject]
    public required ITimelineRepository Repository { get; set; }

    [Inject]
    public required ICharacterRepository CharacterRepository { get; set; }

    [Inject]
    public required NavigationManager Nav { get; set; }

    ITimelineHandle? handle;

    IImmutableDictionary<string, CharacterPermissions> characterPermissions= ImmutableDictionary<string, CharacterPermissions>.Empty;

    Tick? selectedTick;

    List<BreadcrumbItem> breadcrumbs
    {
        get
        {
            var bs = new List<BreadcrumbItem>();
            var groupIcon = Icons.Filled.People!;
            if (handle == null)
            {
                bs.Add(new("Gruppe", GroupInfo.UrlFor(GroupIdRaw), icon: groupIcon));
            }
            else
            {
                bs.Add(new(handle.Timeline.GroupName, GroupInfo.UrlFor(GroupIdRaw), icon: groupIcon));
                bs.Add(new("Tickleiste", Nav.Uri, icon: Icons.Filled.ViewTimeline));
            }
            return bs;
        }
    }

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
            Nav.NavigateTo("/not-found", replace: true);
        }
        else
        {
            newHandle.Updated += (_, _) => InvokeAsync(() =>
            {
                characterPermissions = permissionsForTimeline(newHandle.Timeline);    
                StateHasChanged();
            });
            characterPermissions = permissionsForTimeline(newHandle.Timeline);
            handle = newHandle;
        }
    }

    ImmutableDictionary<string, CharacterPermissions> permissionsForTimeline(Timeline timeline)
    {
        return timeline.Characters.Values.ToImmutableDictionary(
            c => c.Id,
            c => Permissions.InTheContextOf(c, timeline)
        );
    }

    bool addEffectPanelOpen;

    void toggleAddEffectPanel()
    {
        addEffectPanelOpen = !addEffectPanelOpen;
        addCharacterPanelOpen = false;
    }

    bool addCharacterPanelOpen;

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
        return await Repository.SearchCharactersAsync(searchTerm, !Flags.StageMode, groupId, auth.User, cancellationToken);
    }

    public async Task ApplyCommandAsync(TimelineCommand command)
    {
        var auth = await AuthenticationState;
        var groupId1 = groupId;
        await Repository.ApplyAsync(auth.User,
            command switch {
                TimelineCommand.AddEffect addEffect => addEffect with { GroupId = groupId1 },
                TimelineCommand.BumpCharacter bumpCharacter => bumpCharacter with { GroupId = groupId1 },
                TimelineCommand.AddCharacter addCharacter => addCharacter with { GroupId = groupId1 },
                TimelineCommand.RemoveCharacter removeCharacter => removeCharacter with { GroupId = groupId1 },
                TimelineCommand.RemoveEffect removeEffect => removeEffect with { GroupId = groupId1 },
                TimelineCommand.RemoveEffectTick removeTick => removeTick with { GroupId = groupId1 },
                TimelineCommand.SetCharacterActionEnded setCharacterActionEnded => setCharacterActionEnded with {
                    GroupId = groupId1,
                },
                TimelineCommand.SetCharacterReady setCharacterReady => setCharacterReady with { GroupId = groupId1 },
                TimelineCommand.SetCharacterRecovered setCharacterRecovered => setCharacterRecovered with {
                    GroupId = groupId1,
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
        handle?.Timeline.Characters.Values
            .Where(c => characterPermissions[c.Id].HasFlag(CharacterPermissions.EditStats)) 
        ?? Enumerable.Empty<Character>();

    public async Task ApplyAsync(ICharacterCommand command)
    {
        var auth = await AuthenticationState;
        await CharacterRepository.ApplyAsync(auth.User, command);
    }
    
    #endregion
}