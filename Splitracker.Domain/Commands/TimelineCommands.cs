using System.Collections.Immutable;

namespace Splitracker.Domain.Commands;

public abstract record TimelineCommand(string GroupId)
{
    public abstract record CharacterCommand(string GroupId, string CharacterId) : TimelineCommand(GroupId);

    public record SetCharacterReady(string GroupId, string CharacterId) : CharacterCommand(GroupId, CharacterId);

    public record AddCharacter(string GroupId, string CharacterId, int? At) : CharacterCommand(GroupId, CharacterId);

    public record RemoveCharacter(string GroupId, string CharacterId) : CharacterCommand(GroupId, CharacterId);

    public record SetCharacterRecovered(string GroupId, string CharacterId, int At, int? PreemptPosition = null) 
        : CharacterCommand(GroupId, CharacterId);
    
    public record SetCharacterActionEnded(string GroupId, string CharacterId, int At, int TotalDuration, string? Description = null) 
        : CharacterCommand(GroupId, CharacterId);
    
    public record BumpCharacter(string GroupId, string CharacterId, int Direction) 
        : CharacterCommand(GroupId, CharacterId);
    
    public abstract record EffectCommand(string GroupId, string EffectId) : TimelineCommand(GroupId);

    public record AddEffect(
        string GroupId,
        string EffectId,
        string Description,
        int StartsAt,
        int TotalDuration,
        int? TickInterval,
        IImmutableList<string> AffectedCharacterIds
    ) : EffectCommand(GroupId, EffectId);
    
    public record RemoveEffect(string GroupId, string EffectId) : EffectCommand(GroupId, EffectId);
    
    public record RemoveEffectTick(string GroupId, string EffectId, int At) : EffectCommand(GroupId, EffectId);
    
    public record ResetEntireTimeline(string GroupId) : TimelineCommand(GroupId);
}