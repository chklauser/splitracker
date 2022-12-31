using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Changes;
using Splitracker.Domain;
using Splitracker.Persistence.Model;
using Character = Splitracker.Persistence.Model.Character;

namespace Splitracker.Persistence.Characters;

/// <summary>
/// A shared subscription for one user (object identifier, oid). Is not handed out directly to clients.
/// Instead, clients get a <see cref="RavenCharacterHandle"/> via the <see cref="TryGetHandle"/> method.
/// </summary>
[SuppressMessage("ReSharper", "ContextualLoggerProblem")]
class RavenCharacterRepositorySubscription : IObserver<DocumentChange>
{
    readonly ReaderWriterLockSlim @lock = new();
    readonly IDocumentStore store;
    readonly string oid;
    readonly ILogger<RavenCharacterRepository> log;
    readonly IDisposable characterSubscription;
    int referenceCount;
    bool lifetimeBoundToHandles;
    

    public static async ValueTask<RavenCharacterRepositorySubscription> OpenAsync(
        IDocumentStore store,
        string userId,
        ILogger<RavenCharacterRepository> log
    )
    {
        using var session = store.OpenAsyncSession();
        var docIdPrefix = RavenCharacterRepository.CharacterDocIdPrefix(userId);
        var characters = new List<RavenCharacterHandle>();
        await using var characterEnumerator = await session.Advanced.StreamAsync<Character>(docIdPrefix);
        while (await characterEnumerator.MoveNextAsync())
        {
            var character = characterEnumerator.Current.Document.ToDomain();
            var handle = new RavenCharacterHandle(character);
            characters.Add(handle);
        }

        log.Log(LogLevel.Information, 
            "Initialized character repository subscription for {Oid} with {Count} characters", 
            userId, characters.Count);
        
        return new(store, docIdPrefix, characters, log, userId);
    }

    RavenCharacterRepositorySubscription(
        IDocumentStore store,
        string docIdPrefix,
        IEnumerable<RavenCharacterHandle> characters,
        ILogger<RavenCharacterRepository> log,
        string oid
    )
    {
        this.store = store;
        this.log = log;
        this.oid = oid;

        this.characters = characters.ToImmutableList();
        characterSubscription = store.Changes()
            .ForDocumentsStartingWith(docIdPrefix)
            .Subscribe(this);
    }

    public RavenCharacterRepositoryHandle? TryGetHandle()
    {
        @lock.EnterWriteLock();
        try
        {
            if (lifetimeBoundToHandles)
            {
                if (referenceCount == 0)
                {
                    return null;
                }
            }
            else
            {
                lifetimeBoundToHandles = true;
            }

            referenceCount += 1;
        }
        finally
        {
            @lock.ExitWriteLock();
        }
        return new(this);
    }

    public void Release()
    {
        @lock.EnterWriteLock();
        try
        {
            referenceCount -= 1;
            if (referenceCount == 0)
            {
                characterSubscription.Dispose();
            }
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    public ValueTask DisposeAsync()
    {
        characterSubscription.Dispose();
        @lock.EnterWriteLock();
        IImmutableList<RavenCharacterHandle> cs;
        try
        {
            characterAdded = null;
            characterDeleted = null;
            cs = Interlocked.Exchange(ref characters, ImmutableList<RavenCharacterHandle>.Empty);
        }
        finally
        {
            @lock.ExitWriteLock();
        }

        foreach (var handle in cs)
        {
            handle.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    volatile IImmutableList<RavenCharacterHandle> characters;

    public IReadOnlyList<ICharacterHandle> Characters => characters;
    EventHandler? characterAdded;
    public event EventHandler? CharacterAdded
    {
        add
        {
            @lock.EnterWriteLock();
            try
            {
                characterAdded += value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
        remove
        {
            @lock.EnterWriteLock();
            try
            {
                characterAdded -= value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
    }

    EventHandler? characterDeleted;
    public event EventHandler? CharacterDeleted
    {
        add
        {
            @lock.EnterWriteLock();
            try
            {
                characterDeleted += value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
        remove
        {
            @lock.EnterWriteLock();
            try
            {
                characterDeleted -= value;
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
        log.Log(LogLevel.Error, error, "Error while listening for character changes of user {Oid}", oid);
    }

    public async void OnNext(DocumentChange value)
    {
        using var session = store.OpenAsyncSession();
        var handle = characters.FirstOrDefault(c => c.Character.Id == value.Id);

        switch (value.Type)
        {
            case DocumentChangeTypes.Delete when handle == null:
                log.Log(LogLevel.Warning,
                    "Character {Id} was deleted but not found in the list of characters for {Oid}", value.Id, oid);
                return;
            case DocumentChangeTypes.Delete:
                log.Log(LogLevel.Debug, "Character {Id} was deleted from {Oid}", value.Id, oid);
                characters = characters.Remove(handle);
                handle.Dispose();
                @lock.EnterReadLock();
                EventHandler? characterDeletedHandler;
                try
                {
                    characterDeletedHandler = characterDeleted;
                }
                finally
                {
                    @lock.ExitReadLock();
                }
                
                characterDeletedHandler?.Invoke(this, EventArgs.Empty);
                break;
            case DocumentChangeTypes.Put:
                log.Log(LogLevel.Debug, "Character {Id} was created or updated from {Oid}", value.Id, oid);
                var newChar = (await session.LoadAsync<Character>(value.Id))?.ToDomain();
                if (newChar != null)
                {
                    if (handle == null)
                    {
                        characters = characters.Add(new(newChar));
                        @lock.EnterReadLock();
                        EventHandler? characterAddedHandler;
                        try
                        {
                            characterAddedHandler = characterAdded;
                        }
                        finally
                        {
                            @lock.ExitReadLock();
                        }

                        characterAddedHandler?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        handle.Character = newChar;
                        handle.TriggerCharacterUpdated();
                    }
                }
                else
                {
                    log.Log(LogLevel.Warning, "Character {Id} was added or updated but not found in the database for {Oid}",
                        value.Id, oid);
                }

                break;
            case var otherType:
                log.Log(LogLevel.Warning, "Unknown document change type {Type} for {Oid}", otherType, oid);
                break;
        }
    }
}