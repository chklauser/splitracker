using Microsoft.Extensions.DependencyInjection;
using Splitracker.Domain;

namespace Splitracker.Persistence;

public static class PersistenceServiceProviderConfig
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddSingleton<ICharacterRepository, FakeCharacterRepository>();
        return services;
    }
}