using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Raven.Client.Documents.Indexes;

namespace Splitracker.Persistence.Model;

[SuppressMessage("ReSharper", "InconsistentNaming")]
class Timeline_ByGroup : AbstractIndexCreationTask<Timeline>
{
    public class IndexEntry
    {
        public required string Id { get; set; }
        public required string GroupId { get; set; }
        public required string MemberUserId { get; set; }
        public required GroupRole MemberRole { get; set; }
    }

    public Timeline_ByGroup()
    {
        Map = ts => from timeline in ts
            let g = LoadDocument<Group>(timeline.GroupId)
            from m in g.Members
            select new IndexEntry {
                Id = timeline.Id!,
                GroupId = timeline.GroupId,
                MemberUserId = m.UserId,
                MemberRole = m.Role,
            };
        StoreAllFields(FieldStorage.Yes);
    }
}