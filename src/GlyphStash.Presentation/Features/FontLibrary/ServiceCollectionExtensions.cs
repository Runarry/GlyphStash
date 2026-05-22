using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Presentation.Features.FontLibrary;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFontLibraryFeature(this IServiceCollection services)
    {
        services.AddScoped<FontLibraryViewModel>();
        return services;
    }
}
