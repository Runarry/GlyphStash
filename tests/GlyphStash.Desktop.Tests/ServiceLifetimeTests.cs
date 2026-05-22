using System.Runtime.Versioning;
using GlyphStash.Application.Fonts;
using GlyphStash.Presentation.Features.Collections;
using GlyphStash.Presentation.Features.FontLibrary;
using GlyphStash.Presentation.Features.GlyphBrowser;
using GlyphStash.Presentation.Features.MergeTool;
using GlyphStash.Presentation.Features.OnlineFonts;
using GlyphStash.Presentation.Features.Settings;
using GlyphStash.Presentation.Features.Shell;
using GlyphStash.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GlyphStash.Desktop.Tests;

public sealed class ServiceLifetimeTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public void UiDialogServicesAreScopedWhileTemporaryActivationCoreStaysRooted()
    {
        using var provider = App.BuildServices();
        var rootActivation = provider.GetRequiredService<FontActivationCoordinator>();

        DesktopStorageDialogService firstDialogService;
        using (var scope = provider.CreateScope())
        {
            firstDialogService = scope.ServiceProvider.GetRequiredService<DesktopStorageDialogService>();
            Assert.Same(rootActivation, scope.ServiceProvider.GetRequiredService<FontActivationCoordinator>());
        }

        using var secondScope = provider.CreateScope();
        var secondDialogService = secondScope.ServiceProvider.GetRequiredService<DesktopStorageDialogService>();

        Assert.Null(firstDialogService.TopLevel);
        Assert.NotSame(firstDialogService, secondDialogService);
        Assert.Same(rootActivation, provider.GetRequiredService<FontActivationCoordinator>());
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void PresentationFeatureViewModelsResolveFromUiScope()
    {
        using var provider = App.BuildServices();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<FontLibraryViewModel>());
        Assert.NotNull(services.GetRequiredService<CollectionsViewModel>());
        Assert.NotNull(services.GetRequiredService<OnlineFontsViewModel>());
        Assert.NotNull(services.GetRequiredService<MergeToolViewModel>());
        Assert.NotNull(services.GetRequiredService<GlyphBrowserViewModel>());
        Assert.NotNull(services.GetRequiredService<SettingsViewModel>());
        Assert.NotNull(services.GetRequiredService<PlaceholderPageViewModel>());
    }
}
