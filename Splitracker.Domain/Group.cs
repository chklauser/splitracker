﻿using System;
using System.Collections.Immutable;

namespace Splitracker.Domain;

public record Group(
    string Id,
    string Name,
    IImmutableDictionary<string, Character> Characters,
    bool HasTimeline,
    IImmutableDictionary<string, GroupMembership> Members,
string? JoinCode = null
) : GroupInfo(Id, Name, HasTimeline);

public record GroupMembership(string UserId, GroupRole Role);

#pragma warning disable MA0048
public enum GroupRole
#pragma warning restore MA0048
{
    Member,
    GameMaster,
}

public record GroupInfo(string Id, string Name, bool HasTimeline)
{
    public string Url => UrlFor(Id);
    
    public static string UrlFor(string groupId) => $"/Groups/{chopOffCollectionName(groupId)}";
    public static string IdFor(string rawId) => $"Groups/{rawId}";

    static string chopOffCollectionName(string groupId)
    {
        var slashIndex = groupId.LastIndexOf("/", StringComparison.Ordinal);
        return slashIndex >= 0 ? groupId[(slashIndex + 1)..] : groupId;
    }
}
