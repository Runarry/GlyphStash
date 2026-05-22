using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Presentation.Features.GlyphBrowser;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGlyphBrowserFeature(this IServiceCollection services)
    {
        services.AddScoped<GlyphBrowserViewModel>();
        return services;
    }
}
