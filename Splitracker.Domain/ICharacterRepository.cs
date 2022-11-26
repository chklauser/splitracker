using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public interface ICharacterRepository
{
    Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal);
    Task ApplyAsync(ClaimsPrincipal principal, ICharacterCommand characterCommand);
}

public interface ICharacterRepositoryHandle : IAsyncDisposable
{
    IReadOnlyList<ICharacterHandle> Characters { get; }
    event EventHandler CharacterAdded;
    event EventHandler CharacterDeleted;
}

public interface ICharacterHandle
{
    Character Character { get; }
    event EventHandler CharacterUpdated;
}