using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Splitracker.Persistence;

namespace Splitracker.Web;

public static class RavenXmlRepositoryExtensions
{
    public static IDataProtectionBuilder PersistKeysToRavenDb(this IDataProtectionBuilder builder)
    {
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
        {
            var repo = services.GetRequiredService<RavenDataProtectionRepository>();
            return new ConfigureOptions<KeyManagementOptions>(options =>
            {
                options.XmlRepository = new RavenXmlRepository(repo);
            });
        });

        return builder;
    }

    class RavenXmlRepository(RavenDataProtectionRepository repo) : IXmlRepository
    {
        public IReadOnlyCollection<XElement> GetAllElements() => repo.GetAllElements();

        public void StoreElement(XElement element, string friendlyName) => repo.StoreElement(element, friendlyName);
    }
}