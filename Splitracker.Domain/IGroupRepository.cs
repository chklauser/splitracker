using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public interface IGroupRepository
{
    Task<IGroupRepositoryHandle> OpenAsync(ClaimsPrincipal principal);
    Task<IGroupHandle> OpenSingleAsync(ClaimsPrincipal principal, string groupId);
    Task ApplyAsync(ClaimsPrincipal principal, IGroupCommand groupCommand);
}

public interface IGroupRepositoryHandle : IAsyncDisposable
{
    IReadOnlyList<IGroupHandle> Groups { get; }
    event EventHandler GroupAdded;
    event EventHandler GroupRemoved;
}

public interface IGroupHandle : IAsyncDisposable
{
    Group Group { get; }
    event EventHandler GroupUpdated;
}