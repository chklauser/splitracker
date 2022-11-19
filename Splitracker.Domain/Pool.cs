using System.Collections.Immutable;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public abstract record Pool(int BaseCapacity, PointsVec Points, IImmutableList<int> Channelings)
{
    protected Pool(int baseCapacity) : this(baseCapacity, new(), ImmutableArray<int>.Empty)
    {
    }
    
    public abstract int TotalCapacity { get; }
}

public record LpPool(int BaseCapacity, PointsVec Points, IImmutableList<int> Channelings) : Pool(BaseCapacity, Points, Channelings)
{
    public LpPool(int baseCapacity) : this(baseCapacity, new(), ImmutableArray<int>.Empty)
    {
    }

    public override int TotalCapacity => 5 * BaseCapacity;
}

public record FoPool(int BaseCapacity, PointsVec Points, IImmutableList<int> Channelings) : Pool(BaseCapacity, Points, Channelings)
{
    public FoPool(int baseCapacity) : this(baseCapacity, new(), ImmutableArray<int>.Empty)
    {
    }

    public override int TotalCapacity => BaseCapacity;
}
