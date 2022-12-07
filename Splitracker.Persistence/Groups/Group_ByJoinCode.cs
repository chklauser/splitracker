using System.Linq;
using Raven.Client.Documents.Indexes;

namespace Splitracker.Persistence.Groups;

class Group_ByJoinCode : AbstractIndexCreationTask<Model.Group>
{
    public class EntityRecord
    {
        public required string Id { get; set; }
        public required string JoinCode { get; set; }
    }

    public Group_ByJoinCode()
    {
        Map = groups => 
            from g in groups
            where g.JoinCode != null && g.JoinCode != ""
            select new EntityRecord {
                Id = g.Id!,
                JoinCode = g.JoinCode!,
            };
    }
}