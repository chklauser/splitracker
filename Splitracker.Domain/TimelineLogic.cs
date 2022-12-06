using System;
using System.Linq;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public class TimelineLogic
{
    public TimelineCommand ApplyAction(
        Timeline timeline,
        ActionTemplate template,
        Character character,
        int ticks,
        string? description
    )
    {
        var now = timeline.Ticks.Count > 0 ? timeline.Ticks[0].At : 1;
        var characterId = character.Id;
        var currentTick =
            timeline.Ticks.FirstOrDefault(t => t is Tick.CharacterTick { Character: var c } && c.Id == characterId);
        var duration = ticks * template.Multiplier;
        var gid = timeline.GroupId;
        return template switch {
            { Type: ActionTemplateType.Bump } =>
                new TimelineCommand.BumpCharacter(gid, characterId, Math.Sign(ticks)),
            { Type: ActionTemplateType.Ready } =>
                new TimelineCommand.SetCharacterReady(gid, characterId),
            { Type: ActionTemplateType.Reset } =>
                new TimelineCommand.SetCharacterRecovered(gid, characterId, now, 1),
            { Type: ActionTemplateType.Reaction } when currentTick is null =>
                new TimelineCommand.SetCharacterRecovered(gid, characterId, now + ticks),
            { Type: ActionTemplateType.Reaction } =>
                new TimelineCommand.SetCharacterRecovered(gid, characterId, currentTick.At + ticks),
            { Type: ActionTemplateType.Immediate } =>
                new TimelineCommand.SetCharacterRecovered(gid, characterId, now + duration),
            { Type: ActionTemplateType.Continuous } =>
                new TimelineCommand.SetCharacterActionEnded(gid,
                    characterId,
                    now + duration,
                    duration,
                    description ?? template.Description),
            { Type: ActionTemplateType.Leave } =>
                new TimelineCommand.RemoveCharacter(gid, characterId),
            _ => throw new ArgumentOutOfRangeException(nameof(template), "Invalid template type " + template.Type),
        };
    }
}