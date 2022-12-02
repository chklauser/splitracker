using System.Collections.Generic;

namespace Splitracker.Persistence.Model;

class Group
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public required List<string> Characters { get; set; }
}