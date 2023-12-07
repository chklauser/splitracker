using Microsoft.Extensions.DependencyInjection;
using Splitracker.Domain;
using Splitracker.UI.Shared;

namespace Splitracker.UI;

public static class UiServiceProviderConfig
{
    public static IServiceCollection AddSplitrackerUi(this IServiceCollection services) => services
        .AddScoped<FlagContextHolder>()
        .AddSingleton<TimelineLogic>()
        .AddScoped<ClipboardService>()
        .AddSingleton<NameGenerationService>()
        .AddCascadingValue(sp => sp.GetRequiredService<FlagContextHolder>().Source)
        .AddCascadingValue<SessionContext>(_ => new());
}