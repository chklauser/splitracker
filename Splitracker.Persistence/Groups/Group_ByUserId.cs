using System.Linq;
using Raven.Client.Documents.Indexes;
using Splitracker.Persistence.Model;
using Timeline = Splitracker.Domain.Timeline;

namespace Splitracker.Persistence.Groups;

class Group_ByUserId : AbstractMultiMapIndexCreationTask<Group_ByUserId.IndexEntry>
{
    public class IndexEntry
    {
        public required string Id { get; set; }
        public required string? Name { get; set; }
        public required string? UserId { get; set; }
        public required GroupRole? Role { get; set; }
        public required bool? HasTimeline { get; set; }
    }

    public Group_ByUserId()
    {
        AddMap<Group>(
            groups =>
                from @group in groups
                from membership in @group.Members
                select new IndexEntry {
                    Id = @group.Id!,
                    UserId = membership.UserId,
                    Role = membership.Role,
                    Name = @group.Name,
                    HasTimeline = null,
                }
            );
        AddMap<Timeline>(timelines => 
            from timeline in timelines
            let @group = LoadDocument<Group>(timeline.GroupId)
            from membership in @group.Members
            select new IndexEntry {
                Id = timeline.GroupId,
                HasTimeline = true,
                Name = null,
                Role = null,
                UserId = membership.UserId,
            });

        Reduce = groupsOrTimelines =>
            from groupOrTimeline in groupsOrTimelines
            group groupOrTimeline by new { GroupId = groupOrTimeline.Id, groupOrTimeline.UserId }
            into g
            select new IndexEntry {
                Id = g.Key.GroupId,
                Name = g.Select(r => r.Name).FirstOrDefault(r => r != null),
                UserId = g.Key.UserId,
                Role = g.Select(r => r.Role).FirstOrDefault(r => r != null),
                HasTimeline = g.Select(r => r.HasTimeline).FirstOrDefault(r => r != null) ?? false,
            };
        
        StoreAllFields(FieldStorage.Yes);
    }
}