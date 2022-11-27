using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
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
        services.AddSingleton<ICharacterRepository, RavenCharacterRepository>();
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