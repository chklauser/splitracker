using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Splitracker.Domain;
using Splitracker.Persistence.Characters;
using Splitracker.Persistence.Groups;
using Splitracker.Persistence.Timelines;
using Splitracker.Persistence.Users;

namespace Splitracker.Persistence;

[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
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
                Certificate = new(tolerateNestedWorkingDir(opts.CertificatePath), readCertificatePassword(opts)),
            };
            CustomizeStore(store);
            store.Initialize();
            logger.Log(LogLevel.Information, "Document store initialized");
            return store;
        });

        services.AddSingletonImplementation<RavenUserRepository>()
            .As<IUserRepository>().AsWellAnd()
            .AsHostedService();

        services.AddSingletonImplementation<RavenCharacterRepository>()
            .As<ICharacterRepository>().AsWellAnd()
            .AsHostedService();

        services.AddSingletonImplementation<RavenGroupRepository>()
            .As<IGroupRepository>().AsWellAnd()
            .AsHostedService();

        services.AddSingletonImplementation<RavenTimelineRepository>()
            .As<ITimelineRepository>().AsWellAnd()
            .AsHostedService();

        return services;
    }

    internal static void CustomizeStore(IDocumentStore store)
    {
        store.Conventions.FindCollectionName = type =>
        {
            var defaultName = DocumentConventions.DefaultGetCollectionName(type);
            return defaultName.EndsWith("Models") ? $"{defaultName[..^6]}s" : defaultName;
        };
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