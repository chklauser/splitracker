using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public interface ITimelineRepository
{
    Task<ITimelineHandle?> OpenSingleAsync(ClaimsPrincipal principal, string groupId);
    Task ApplyAsync(ClaimsPrincipal principal, ITimelineCommand groupCommand);
}

public interface ITimelineHandle : IAsyncDisposable
{
    Timeline Timeline { get; }
    event EventHandler Updated;
}