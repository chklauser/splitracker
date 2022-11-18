using Splitracker.Domain;

namespace Splitracker.Persistence;

/// <summary>
/// A wrapper around a <see cref="RavenCharacterRepositorySubscription"/> implement
/// reference count decrease on <see cref="IAsyncDisposable.DisposeAsync"/>.
/// </summary>
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