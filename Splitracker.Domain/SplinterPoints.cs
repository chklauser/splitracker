using System;

namespace Splitracker.Domain;

public record SplinterPoints(int Max, int Used)
{
    public int Remaining => Math.Max(0, Max - Used);
}