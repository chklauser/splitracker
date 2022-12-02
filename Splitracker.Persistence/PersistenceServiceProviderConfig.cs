using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Json.Serialization.NewtonsoftJson;
using Splitracker.Domain;
using Splitracker.Persistence.Characters;
using Splitracker.Persistence.Model;
using Splitracker.Persistence.Timelines;
using Splitracker.Persistence.Users;

namespace Splitracker.Persistence;

[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
public static class PersistenceServiceProviderConfig
{
    public static IServiceCollection AlsoAddAsHostedService<T>(this IServiceCollection services) where T : class, IHostedService
    {
        services.AddHostedService(p => p.GetRequiredService<T>());
        return services;
    }
    public static IServiceCollection AlsoAddAsSingleton<TInterface, TRegistration>(this IServiceCollection services) 
        where TRegistration : class, TInterface
        where TInterface : class
    {
        services.AddSingleton<TInterface>(p => p.GetRequiredService<TRegistration>());
        return services;
    }
    
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
                Certificate = new(tolerateNestedWorkingDir(opts.CertificatePath), readCertificatePassword(opts)),
            };
            store.Conventions.FindCollectionName = type =>
            {
                var defaultName = DocumentConventions.DefaultGetCollectionName(type);
                return defaultName.EndsWith("Models") ? $"{defaultName[..^6]}s" : defaultName;
            };
            store.Initialize();
            logger.Log(LogLevel.Information, "Document store initialized");
            return store;
        });

        services.AddSingleton<RavenUserRepository>()
            .AlsoAddAsSingleton<IUserRepository, RavenUserRepository>()
            .AlsoAddAsHostedService<RavenUserRepository>();

        services.AddSingleton<ICharacterRepository, RavenCharacterRepository>();
        services.AddSingleton<IGroupRepository, FakeGroupRepository>();

        services.AddSingleton<RavenTimelineRepository>()
            .AlsoAddAsHostedService<RavenTimelineRepository>()
            .AlsoAddAsSingleton<ITimelineRepository, RavenTimelineRepository>();
        return services;
    }

    static string readCertificatePassword(RavenOptions opts)
    {
        return opts.CertificatePassword ??
            (opts.CertificatePasswordFile is { } passwordFile
                ? File.ReadAllText(passwordFile)
                : throw new("Certificate password not configured."));
    }

    static string tolerateNestedWorkingDir(string path)
    {
        if (path == "" || Path.IsPathRooted(path) || File.Exists(path))
        {
            return path;
        }

        var candidate = Environment.CurrentDirectory;        
        while (Path.GetDirectoryName(candidate) is {} nextCandidate and not "")
        {
            var location = Path.Join(nextCandidate, path);
            if (File.Exists(location))
            {
                return location;
            }
            else
            {
                candidate = nextCandidate;
            }
        }

        // give up and return the original path for an error message that makes sense
        return path;
    }
}