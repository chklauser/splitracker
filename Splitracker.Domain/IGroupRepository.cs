using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public interface IGroupRepository
{
    Task<IGroupHandle?> OpenSingleAsync(ClaimsPrincipal principal, string groupId);
    Task ApplyAsync(ClaimsPrincipal principal, GroupCommand groupCommand);
    Task<JoinResult> GetByJoinCodeAsync(ClaimsPrincipal principal, string joinCode);
    Task<IReadOnlyList<GroupInfo>> ListGroupsAsync(ClaimsPrincipal principal);
    Task JoinWithExistingCharacterAsync(ClaimsPrincipal user, Group group, Character character);
    Task JoinWithNewCharacterAsync(ClaimsPrincipal user, Group group, string characterName);
}

public abstract record JoinResult
{
    public sealed record GroupExists(Group Group) : JoinResult;
    public sealed record GroupAlreadyJoined(Group Group) : JoinResult;

    public sealed record GroupNotFound : JoinResult;
}

public interface IGroupHandle : IAsyncDisposable
{
    Group Group { get; }
    event EventHandler Updated;
}