using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Changes;
using Splitracker.Domain;
using Splitracker.Persistence.Model;

namespace Splitracker.Persistence;

/// <summary>
/// A shared subscription for one user (object identifier, oid). Is not handed out directly to clients.
/// Instead, clients get a <see cref="RavenCharacterHandle"/> via the <see cref="TryGetHandle"/> method.
/// </summary>
[SuppressMessage("ReSharper", "ContextualLoggerProblem")]
class RavenCharacterRepositorySubscription : IObserver<DocumentChange>
{
    internal ReaderWriterLockSlim Lock { get; } = new();
    readonly IDocumentStore store;
    readonly string oid;
    readonly ILogger<RavenCharacterRepository> log;
    readonly IDisposable characterSubscription;
    int referenceCount = 1;

    public static async ValueTask<RavenCharacterRepositorySubscription> OpenAsync(
        IDocumentStore store,
        string oid,
        ILogger<RavenCharacterRepository> log
    )
    {
        using var session = store.OpenAsyncSession();
        var docIdPrefix = $"{RavenCharacterRepository.CollectionName}/{oid}/";
        var characters = new List<RavenCharacterHandle>();
        await using var characterEnumerator = await session.Advanced.StreamAsync<CharacterModel>(docIdPrefix);
        while (await characterEnumerator.MoveNextAsync())
        {
            var character = characterEnumerator.Current.Document.ToDomain();
            var handle = new RavenCharacterHandle(character);
            characters.Add(handle);
        }

        log.Log(LogLevel.Information, 
            "Initialized character repository subscription for {Oid} with {Count} characters", 
            oid, characters.Count);
        
        return new(store, docIdPrefix, characters, log, oid);
    }

    public RavenCharacterRepositorySubscription(
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
        Lock.EnterWriteLock();
        try
        {
            if (referenceCount == 0)
            {
                return null;
            }

            referenceCount += 1;
        }
        finally
        {
            Lock.ExitWriteLock();
        }
        return new(this);
    }

    public void Release()
    {
        Lock.EnterWriteLock();
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
            Lock.ExitWriteLock();
        }
    }

    public ValueTask DisposeAsync()
    {
        characterSubscription.Dispose();
        Lock.EnterWriteLock();
        try
        {
            characterAdded = null;
        }
        finally
        {
            Lock.ExitWriteLock();
        }

        var cs = Interlocked.Exchange(ref characters, ImmutableList<RavenCharacterHandle>.Empty);
        foreach (var handle in cs)
        {
            handle.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    volatile ImmutableList<RavenCharacterHandle> characters;

    public IReadOnlyList<ICharacterHandle> Characters => characters;
    EventHandler? characterAdded;
    public event EventHandler? CharacterAdded
    {
        add
        {
            Lock.EnterWriteLock();
            try
            {
                characterAdded += value;
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }
        remove
        {
            Lock.EnterWriteLock();
            try
            {
                characterAdded -= value;
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }
    }

    EventHandler? characterDeleted;
    public event EventHandler? CharacterDeleted
    {
        add
        {
            Lock.EnterWriteLock();
            try
            {
                characterDeleted += value;
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }
        remove
        {
            Lock.EnterWriteLock();
            try
            {
                characterDeleted -= value;
            }
            finally
            {
                Lock.ExitWriteLock();
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
                Lock.EnterReadLock();
                EventHandler? characterDeletedHandler;
                try
                {
                    characterDeletedHandler = characterDeleted;
                }
                finally
                {
                    Lock.ExitReadLock();
                }
                
                characterDeletedHandler?.Invoke(this, EventArgs.Empty);
                break;
            case DocumentChangeTypes.Put:
                log.Log(LogLevel.Debug, "Character {Id} was created or updated from {Oid}", value.Id, oid);
                var newChar = (await session.LoadAsync<CharacterModel>(value.Id))?.ToDomain();
                if (newChar != null)
                {
                    if (handle == null)
                    {
                        characters = characters.Add(new(newChar));
                        Lock.EnterReadLock();
                        EventHandler? characterAddedHandler;
                        try
                        {
                            characterAddedHandler = characterAdded;
                        }
                        finally
                        {
                            Lock.ExitReadLock();
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