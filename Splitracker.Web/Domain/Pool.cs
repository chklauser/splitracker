﻿using System.Collections.Immutable;

namespace Splitracker.Web.Domain;

public record Pool(int BaseCapacity, PointsVec Points, IImmutableList<int> Channelings)
{
    public Pool(int baseCapacity) : this(baseCapacity, new(), ImmutableArray<int>.Empty)
    {
    }
}