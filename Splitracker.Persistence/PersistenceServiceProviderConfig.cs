using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Splitracker.Domain;

namespace Splitracker.Persistence;

public static class PersistenceServiceProviderConfig
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RavenOptions>()
            .Bind(configuration.GetRequiredSection("Raven"))
            .ValidateDataAnnotations();
        services.AddSingleton<IDocumentStore>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Splitracker.Persistence");
            var opts = sp.GetRequiredService<IOptions<RavenOptions>>().Value;
            logger.Log(LogLevel.Debug, "Creating document store");
            var store = new DocumentStore {
                Urls = opts.Urls.ToArray(),
                Database = opts.Database,
            };
            store.Initialize();
            logger.Log(LogLevel.Information, "Document store initialized");
            return store;
        });
        services.AddSingleton<ICharacterRepository, RavenCharacterRepository>();
        return services;
    }
}