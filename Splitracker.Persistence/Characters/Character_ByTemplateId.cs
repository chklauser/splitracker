using System.Linq;
using Raven.Client.Documents.Indexes;
using Splitracker.Persistence.Model;

namespace Splitracker.Persistence.Characters;

class Character_ByTemplateId : AbstractIndexCreationTask<Character>
{
    class IndexEntry
    {
        public required string TemplateId { get; set; }
    }

    public Character_ByTemplateId()
    {
        Map = characters =>
            from character in characters
            where character.TemplateId != null
            select new IndexEntry {
                TemplateId = character.TemplateId!,
            };
    }
}