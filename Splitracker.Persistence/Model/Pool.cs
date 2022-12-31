using System.Collections.Generic;
using JetBrains.Annotations;

namespace Splitracker.Persistence.Model;

[UsedImplicitly]
class Pool
{
    public int BaseCapacity { get; set; } = 1;
    public Points Points { get; set; } = new(0, 0, 0);
    public List<Channeling> Channelings { get; set; } = new();
}

class Channeling
{
    public required string Id { get; set; }
    public required int Value { get; set; }
    public string? Description { get; set; }
}