using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Presentation.Features.MergeTool;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMergeToolFeature(this IServiceCollection services)
    {
        services.AddScoped<MergeToolViewModel>();
        return services;
    }
}
