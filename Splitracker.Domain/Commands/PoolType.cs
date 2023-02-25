using System;
using System.Diagnostics;

namespace Splitracker.Domain.Commands;

public enum PoolType
{
    Fo,
    Lp,
}

public static class PoolTypeExtensions
{
    public static PointType DefaultPointType(this PoolType type) => type switch {
        PoolType.Fo => PointType.E,
        PoolType.Lp => PointType.V,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown pool type"),
    };
}
