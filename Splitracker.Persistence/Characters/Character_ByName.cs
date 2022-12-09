﻿using System.Linq;
using Raven.Client.Documents.Indexes;
using Splitracker.Persistence.Model;

namespace Splitracker.Persistence.Characters;

class Character_ByName : AbstractIndexCreationTask<CharacterModel>
{
    class IndexEntry
    {
        public required string Name { get; set; }
        public required bool IsOpponent { get; set; }
    }

    public Character_ByName()
    {
        Map = characters =>
            from character in characters
            select new IndexEntry {
                Name = character.Name,
                IsOpponent = character.IsOpponent,
            };
        
        // undocumented https://issues.hibernatingrhinos.com/issue/RDoc-1525, but available in the studio
        Analyzers.Add(x => x.Name, "LowerCaseWhitespaceAnalyzer");
    }
}