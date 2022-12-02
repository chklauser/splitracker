using System.Collections.Generic;

namespace Splitracker.Persistence.Model;

class Group
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public required List<string> CharacterIds { get; set; }
    public required List<GroupMember> Members { get; set; }
}