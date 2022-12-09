using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public interface ITimelineRepository
{
    Task<ITimelineHandle?> OpenSingleAsync(ClaimsPrincipal principal, string groupId);
    Task ApplyAsync(ClaimsPrincipal principal, TimelineCommand command);
    Task<IEnumerable<Character>> SearchCharactersAsync(
        string searchTerm,
        bool includeOpponents,
        string groupId,
        ClaimsPrincipal authUser,
        CancellationToken cancellationToken
    );
}

public interface ITimelineHandle : IAsyncDisposable
{
    Timeline Timeline { get; }
    event EventHandler Updated;
}