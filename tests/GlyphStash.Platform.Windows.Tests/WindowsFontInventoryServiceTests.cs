using GlyphStash.Platform.Windows.Fonts;
using System.Runtime.Versioning;

namespace GlyphStash.Platform.Windows.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsFontInventoryServiceTests
{
    [Fact]
    public async Task ScanInstalledFontsAsync_DoesNotThrow()
    {
        var service = new WindowsFontInventoryService();
        var fonts = await service.ScanInstalledFontsAsync(CancellationToken.None);

        Assert.NotNull(fonts);
        Assert.NotEmpty(fonts);
        Assert.All(fonts, font => Assert.False(string.IsNullOrWhiteSpace(font.PrimaryFilePath)));
    }
}
