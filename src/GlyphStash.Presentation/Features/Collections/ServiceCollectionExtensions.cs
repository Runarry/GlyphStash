using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Presentation.Features.Collections;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCollectionsFeature(this IServiceCollection services)
    {
        services.AddScoped<CollectionsViewModel>();
        return services;
    }
}
