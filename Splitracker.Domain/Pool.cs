﻿using System.Collections.Immutable;
using Splitracker.Domain.Commands;

namespace Splitracker.Domain;

public abstract record Pool(int BaseCapacity, PointsVec Points, IImmutableList<Channeling> Channelings)
{
    protected Pool(int baseCapacity) : this(baseCapacity, new(), ImmutableArray<Channeling>.Empty)
    {
    }
    
    public abstract int TotalCapacity { get; }
}

public record LpPool(int BaseCapacity, PointsVec Points, IImmutableList<Channeling> Channelings) : Pool(BaseCapacity, Points, Channelings)
{
    public LpPool(int baseCapacity) : this(baseCapacity, new(), ImmutableArray<Channeling>.Empty)
    {
    }

    public override int TotalCapacity => 5 * BaseCapacity;
}

public record FoPool(int BaseCapacity, PointsVec Points, IImmutableList<Channeling> Channelings) : Pool(BaseCapacity, Points, Channelings)
{
    public FoPool(int baseCapacity) : this(baseCapacity, new(), ImmutableArray<Channeling>.Empty)
    {
    }

    public override int TotalCapacity => BaseCapacity;
}

public record Channeling(string Id, int Value, string? Description = null);
