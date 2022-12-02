using System.Collections.Generic;

namespace Splitracker.Persistence.Model;

class User
{
    public string? Id { get; set; }
    public required List<string> Oids { get; set; }
    public required string DisplayName { get; set; }
}