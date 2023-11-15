namespace Splitracker.Domain.Commands;

public interface ITagCommand
{
    string? TagId { get; }
}

public abstract record UpsertTag(string Name) : ITagCommand
{
    protected abstract string? ProtectedTagId { get; }
    string? ITagCommand.TagId => ProtectedTagId;
}

public record CreateTag(string Name) : UpsertTag(Name)
{
    protected override string? ProtectedTagId => null;
}

public record EditTag(string TagId, string Name) : UpsertTag(Name)
{
    protected override string ProtectedTagId => TagId;
}
public record DeleteTag(string TagId) : ITagCommand;