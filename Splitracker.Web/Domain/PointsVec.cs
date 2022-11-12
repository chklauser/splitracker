using System.Collections.Immutable;

namespace Splitracker.Web.Domain;

public readonly record struct PointsVec(int Channeled, int Exhausted, int Consumed);

public record Pool(int BaseCapacity, PointsVec Points, IImmutableList<int> Channelings)
{
    public Pool(int baseCapacity) : this(baseCapacity, new(), ImmutableArray<int>.Empty)
    {
    }
}

public record Character(string Id, string Name, Pool Lp, Pool Fo)
{
    public Character(string id, string name, int lpBaseCapacity, int foBaseCapacity) :
        this(id, name,
            new(lpBaseCapacity), new Pool(foBaseCapacity))
    {
    }
}