using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public interface ITagRepository
{
    Task<ITagRepositoryHandle> OpenAsync(ClaimsPrincipal principal);
    Task ApplyAsync(ClaimsPrincipal principal, ITagCommand tagCommand);
}

public interface ITagRepositoryHandle : IAsyncDisposable
{
    IReadOnlyList<ITagHandle> Tags { get; }
    event EventHandler Added;
    event EventHandler Deleted;
}

public interface ITagHandle
{
    Tag Tag { get; }
    event EventHandler Updated;
}
