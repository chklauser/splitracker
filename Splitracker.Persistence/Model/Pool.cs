using System.Collections.Generic;
using JetBrains.Annotations;

namespace Splitracker.Persistence.Model;

[UsedImplicitly]
class Pool
{
    public int? BaseCapacity { get; set; }
    public Points? Points { get; set; }
    public List<Channeling>? Channelings { get; set; }
}

class Channeling
{
    public required string Id { get; set; }
    public required int Value { get; set; }
    public string? Description { get; set; }
}