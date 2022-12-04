using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Splitracker.Domain;

namespace Splitracker.Web.Pages;

partial class Ticks : IAsyncDisposable
{
    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationState { get; set; }
    
    [Inject]
    public required ITimelineRepository Repository { get; set; }
    
    [Inject]
    public required NavigationManager Nav { get; set; }

    ITimelineHandle? handle;

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
        var newHandle = await Repository.OpenSingleAsync(auth.User, "Groups/0000000000000000021-A");
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

    
    
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (handle != null)
        {
            await handle.DisposeAsync();
        }
    }
}