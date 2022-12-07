using System;

namespace Splitracker.Domain;

public record Character(string Id, string Name, LpPool Lp, FoPool Fo)
{
    public Character(string id, string name, int lpBaseCapacity, int foBaseCapacity) :
        this(id, name, new(lpBaseCapacity), new FoPool(foBaseCapacity))
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