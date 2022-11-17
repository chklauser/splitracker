using System.Security.Claims;

namespace Splitracker.Domain;

public interface ICharacterRepository
{
    Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal);
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