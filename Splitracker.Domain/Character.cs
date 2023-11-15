using System;
using System.Collections.Immutable;

namespace Splitracker.Domain;

public record Character(
    string Id,
    string Name,
    string? CustomColor,
    LpPool Lp,
    FoPool Fo,
    SplinterPoints SplinterPoints,
    IImmutableDictionary<string, ActionShorthand> ActionShorthands,
    bool IsOpponent,
    IImmutableSet<string> TagIds
)
{
    public Character(
        string id,
        string name,
        int lpBaseCapacity,
        int foBaseCapacity,
        int splinterPointsMax = 3,
        string? customColor = null,
        bool isOpponent = false,
        IImmutableDictionary<string, ActionShorthand>? actionShorthands = null,
        IImmutableSet<string>? tagIds = null
    ) :
        this(id,
            name,
            customColor,
            new(lpBaseCapacity),
            new(foBaseCapacity),
            new(splinterPointsMax, 0),
            actionShorthands ?? ImmutableDictionary<string, ActionShorthand>.Empty,
            isOpponent,
            tagIds ?? [])
    {
    }

    public static int PenaltyDueToLowLp(PointsVec points, int baseCapacity) =>
        (int)Math.Min(Math.Floor(Math.Pow(2,
                Math.Ceiling((double)(points.Exhausted + points.Consumed + points.Channeled) / baseCapacity) -
                2)),
            8);

    public int Penalty => PenaltyDueToLowLp(Lp.Points, Lp.BaseCapacity);

    public string UserId { get; } = deriveUserId(Id);

    static string deriveUserId(string? id)
    {
        if (id == null)
        {
            return "";
        }

        var firstSlash = id.IndexOf('/');
        var lastSlash = id.LastIndexOf('/');
        if (firstSlash < 0 || lastSlash < 0)
        {
            return "";
        }

        return $"Users/{id.Substring(firstSlash + 1, lastSlash - firstSlash - 1)}";
    }
}