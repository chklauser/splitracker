using System.Collections.Generic;
using JetBrains.Annotations;

namespace Splitracker.Persistence.Model;

[UsedImplicitly]
class PoolModel
{
    public int BaseCapacity { get; set; } = 1;
    public PointsModel Points { get; set; } = new(0, 0, 0);
    public List<int> Channelings { get; set; } = new();
}