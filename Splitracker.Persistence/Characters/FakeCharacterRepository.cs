using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Threading.Tasks;
using Splitracker.Domain;
using Splitracker.Domain.Commands;

namespace Splitracker.Persistence.Characters;

internal class FakeCharacterRepository : ICharacterRepository
{
    public Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        return Task.FromResult(
            (ICharacterRepositoryHandle)new FakeCharacterRepositoryHandle(ImmutableArray.Create(
                new ICharacterHandle[] {
                    new FakeCharacterHandle(new Character("1", "Linea", 7, 20)),
                    new FakeCharacterHandle(new Character("2", "Andrin", 8, 15)),
                }
            )));
    }

    public Task ApplyAsync(ClaimsPrincipal principal, ICharacterCommand characterCommand)
    {
        return Task.CompletedTask;
    }
}

internal record FakeCharacterRepositoryHandle(IReadOnlyList<ICharacterHandle> Characters) : ICharacterRepositoryHandle
{
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public event EventHandler? CharacterAdded
    {
        add { }
        remove { }
    }

    public event EventHandler? CharacterDeleted
    {
        add { }
        remove { }
    }
}

internal record FakeCharacterHandle(Character Character) : ICharacterHandle
{
    public event EventHandler? CharacterUpdated
    {
        add { }
        remove { }
    }
}