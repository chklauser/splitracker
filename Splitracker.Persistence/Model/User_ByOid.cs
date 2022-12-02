using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Raven.Client.Documents.Indexes;

namespace Splitracker.Persistence.Model;

[SuppressMessage("ReSharper", "InconsistentNaming")]
class User_ByOid : AbstractIndexCreationTask<User>
{
    public class IndexEntry
    {
        public required string Id { get; set; }
        public required string Oid { get; set; }
    }

    public User_ByOid()
    {
        Map = users =>
            from user in users
            from oid in user.Oids
            select new IndexEntry { Oid = oid, Id = user.Id! };
        Store(u => u.Id, FieldStorage.Yes);
    }
}