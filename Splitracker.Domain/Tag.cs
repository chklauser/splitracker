using System;
using System.Collections.Generic;
using System.Linq;

namespace Splitracker.Domain;

public record Tag(string Id, string Name);

public static class TagExtensions
{
    public static IEnumerable<Tag> SearchTags(this IEnumerable<Tag>? tags, IEnumerable<Tag>? exclude, string? query)
    {
        if (tags == null)
        {
            return Enumerable.Empty<Tag>();
        }

        var availableTags = tags.Where(t => exclude?.All(n => n.Id != t.Id) ?? true);
        if (string.IsNullOrWhiteSpace(query))
        {
            return availableTags.OrderBy(t => t.Name);
        }

        return availableTags
            .Select(t => (t, t.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase)))
            .Where(x => x.Item2 >= 0)
            .OrderBy(x => x.Item2)
            .ThenBy(x => x.t.Name)
            .Select(x => x.t);
    }
}