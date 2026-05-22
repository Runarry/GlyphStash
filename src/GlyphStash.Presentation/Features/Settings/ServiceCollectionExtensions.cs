using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Presentation.Features.Settings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsFeature(this IServiceCollection services)
    {
        services.AddScoped<SettingsViewModel>();
        return services;
    }
}
