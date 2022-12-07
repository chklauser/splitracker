using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Splitracker.Domain;

namespace Splitracker.Persistence.Model;

class Group
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public string? JoinCode { get; set; }
    public required List<string> CharacterIds { get; set; } = new();
    public required List<GroupMember> Members { get; set; } = new();
}

static class GroupModelMapper
{
    public static Domain.Group ToDomain(Group dbGroup, IEnumerable<CharacterModel> dbCharacters, bool hasTimeline)
    {
        return new(
            dbGroup.Id!,
            dbGroup.Name,
            dbCharacters.ToImmutableDictionary(
                c => c.Id,
                c => c.ToDomain()
            ),
            hasTimeline,
            JoinCode: dbGroup.JoinCode,
            Members: dbGroup.Members.ToImmutableDictionary(m => m.UserId,
                m => new GroupMembership(m.UserId,
                    m.Role switch {
                        GroupRole.Member => Domain.GroupRole.Member,
                        GroupRole.GameMaster => Domain.GroupRole.GameMaster,
                        _ => throw new ArgumentOutOfRangeException(nameof(GroupMember.Role),
                            m.Role,
                            "Unknown group role"),
                    })));
    }
}