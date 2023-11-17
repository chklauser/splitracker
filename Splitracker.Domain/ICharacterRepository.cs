using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public interface ICharacterRepository
{
    Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal);
    Task<ICharacterHandle?> OpenSingleAsync(ClaimsPrincipal principal, string characterId);
    Task ApplyAsync(ClaimsPrincipal principal, ICharacterCommand characterCommand);
    Task<IReadOnlyList<Character>> SearchCharactersAsync(
        ClaimsPrincipal principal,
        string searchTerm,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Returns the full character ID based on a user-specific ID. The returned ID doesn't necessarily
    /// refer to an existing character.
    /// </summary>
    /// <returns><c>null</c> if the user is not logged in.</returns>
    Task<string?> FullCharacterIdFromImplicitAsync(ClaimsPrincipal principal, string implicitId);
}

public interface ICharacterRepositoryHandle : IAsyncDisposable
{
    IReadOnlyList<ICharacterHandle> Characters { get; }
    event EventHandler CharacterAdded;
    event EventHandler CharacterDeleted;
}

public interface ICharacterHandle : IAsyncDisposable
{
    Character Character { get; }
    event EventHandler Updated;
}