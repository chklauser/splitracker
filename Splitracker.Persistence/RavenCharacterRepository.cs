using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Changes;
using Splitracker.Domain;
using Splitracker.Persistence.Model;

namespace Splitracker.Persistence;

class RavenCharacterRepository : ICharacterRepository
{
    internal const string CollectionName = "Characters";
    const string OidClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    readonly IDocumentStore store;
    readonly ILogger<RavenCharacterRepository> log;

    readonly ConcurrentDictionary<string, Task<RavenCharacterRepositorySubscription>> handles = new();

    public RavenCharacterRepository(IDocumentStore store, ILogger<RavenCharacterRepository> log)
    {
        this.store = store;
        this.log = log;
    }

    public async Task<ICharacterRepositoryHandle> OpenAsync(ClaimsPrincipal principal)
    {
        var oid = principal.Claims.FirstOrDefault(c => c.Type == OidClaimType)?.Value ??
            throw new ArgumentException("Principal does not have an oid claim.", nameof(principal));

        var remainingTries = 5;
        while (remainingTries-- > 0)
        {
            var ourTask = new TaskCompletionSource<RavenCharacterRepositorySubscription>();
            var ourSubscription = ourTask.Task;
            var installedSubscription = handles.GetOrAdd(oid, ourSubscription);
            if (installedSubscription == ourSubscription)
            {
                try
                {
                    var subscription = await RavenCharacterRepositorySubscription.OpenAsync(store, oid, log);
                    ourTask.SetResult(subscription);
                }
                catch (Exception ex)
                {
                    ourTask.SetException(ex);
                    handles.TryRemove(oid, out _);
                    throw;
                }
            }
            else
            {
                log.Log(LogLevel.Debug, "Trying to join existing subscription for {Oid}", oid);
            }

            if ((await installedSubscription).TryGetHandle() is { } handle)
            {
                return handle;
            }
            else
            {
                // The subscription has been disposed of. Clear it from the dictionary (but only if it matches)
                // and try again.
                log.Log(LogLevel.Information, "Subscription for {Oid} was disposed of. Retrying.", oid);
                handles.TryRemove(new(oid, installedSubscription));
            }
        }
        
        throw new InvalidOperationException("Failed to open a handle.");
    }
}

class RavenCharacterRepositoryHandle : ICharacterRepositoryHandle
{
    readonly RavenCharacterRepositorySubscription subscription;

    public RavenCharacterRepositoryHandle(RavenCharacterRepositorySubscription subscription)
    {
        this.subscription = subscription;
        subscription.CharacterAdded += OnCharacterAdded;
        subscription.CharacterDeleted += OnCharacterDeleted;
    }

    void OnCharacterDeleted(object? sender, EventArgs e)
    {
        CharacterDeleted?.Invoke(sender, e);
    }

    void OnCharacterAdded(object? sender, EventArgs e)
    {
        CharacterAdded?.Invoke(sender, e);
    }

    public IReadOnlyList<ICharacterHandle> Characters => subscription.Characters;

    public event EventHandler? CharacterAdded;
    public event EventHandler? CharacterDeleted;

    public ValueTask DisposeAsync()
    {
        subscription.CharacterAdded -= OnCharacterAdded;
        subscription.CharacterDeleted -= OnCharacterDeleted;
        subscription.Release();
        CharacterAdded = null;
        CharacterDeleted = null;
        return ValueTask.CompletedTask;
    }
}

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

class RavenCharacterHandle : ICharacterHandle, IEquatable<RavenCharacterHandle>, IDisposable
{
    volatile Character character;

    public RavenCharacterHandle(Character character)
    {
        this.character = character;
    }

    public Character Character
    {
        get => character;
        internal set => character = value;
    }

    public event EventHandler? CharacterUpdated;

    public void TriggerCharacterUpdated()
    {
        CharacterUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        CharacterUpdated = null;
    }

    #region Equality

    public bool Equals(RavenCharacterHandle? other) => other?.Character.Id == Character.Id;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((RavenCharacterHandle)obj);
    }

    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode", Justification = "Id remains stable")]
    public override int GetHashCode() => Character.Id.GetHashCode();

    public static bool operator ==(RavenCharacterHandle? left, RavenCharacterHandle? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(RavenCharacterHandle? left, RavenCharacterHandle? right)
    {
        return !Equals(left, right);
    }

    #endregion
}