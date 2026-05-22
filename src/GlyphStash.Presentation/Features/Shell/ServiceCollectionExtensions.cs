using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Presentation.Features.Shell;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShellFeature(this IServiceCollection services)
    {
        services.AddScoped<PlaceholderPageViewModel>();
        return services;
    }
}
