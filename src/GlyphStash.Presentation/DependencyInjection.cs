using GlyphStash.Presentation.Features.Collections;
using GlyphStash.Presentation.Features.FontLibrary;
using GlyphStash.Presentation.Features.GlyphBrowser;
using GlyphStash.Presentation.Features.MergeTool;
using GlyphStash.Presentation.Features.OnlineFonts;
using GlyphStash.Presentation.Features.Settings;
using GlyphStash.Presentation.Features.Shell;
using GlyphStash.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddGlyphStashPresentation(this IServiceCollection services)
    {
        services
            .AddShellFeature()
            .AddFontLibraryFeature()
            .AddCollectionsFeature()
            .AddOnlineFontsFeature()
            .AddMergeToolFeature()
            .AddGlyphBrowserFeature()
            .AddSettingsFeature();

        services.AddScoped<ShellViewModel>();
        return services;
    }
}
