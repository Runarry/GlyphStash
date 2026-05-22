using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Presentation.Features.OnlineFonts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOnlineFontsFeature(this IServiceCollection services)
    {
        services.AddScoped<OnlineFontsViewModel>();
        return services;
    }
}
