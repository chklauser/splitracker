using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Raven.Client.Documents;

namespace Splitracker.Persistence;

public sealed class RavenDataProtectionRepository(TimeProvider time, IDocumentStore store)
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var session = store.OpenSession();

        return session.Query<DataProtectionKey>()
            .Customize(c => c.NoTracking())
            .Where(rawKey => !string.IsNullOrEmpty(rawKey.Xml))
            .AsEnumerable()
            .Select(rawKey => XElement.Parse(rawKey.Xml))
            .ToList()
            .AsReadOnly();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var session = store.OpenSession();
        
        var key = new DataProtectionKey
        {
            Id = null!,
            FriendlyName = friendlyName,
            Xml = element.ToString(SaveOptions.DisableFormatting),
            CreatedAt = time.GetUtcNow(),
        };
        session.Store(key);
        session.SaveChanges();
    }
}

record DataProtectionKey
{
    public required string Id { get; set; }
    
    public required string FriendlyName { get; set; }
    
    public required string Xml { get; set; }
    
    public required DateTimeOffset CreatedAt { get; set; }
}