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
}