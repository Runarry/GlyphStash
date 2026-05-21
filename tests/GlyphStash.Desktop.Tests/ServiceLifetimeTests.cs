using System.Runtime.Versioning;
using GlyphStash.Application.Fonts;
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
}
