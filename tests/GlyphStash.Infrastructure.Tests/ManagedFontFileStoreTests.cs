using GlyphStash.Domain.Fonts;
using GlyphStash.Infrastructure.Fonts;

namespace GlyphStash.Infrastructure.Tests;

public sealed class ManagedFontFileStoreTests
{
    [Fact]
    public async Task EnumerateManagedFontFilesAsync_ReturnsOnlySupportedTopLevelFonts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(directory, "Nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(directory, "BrandSans.ttf"), "fixture");
        await File.WriteAllTextAsync(Path.Combine(directory, "BrandSans.woff2"), "fixture");
        await File.WriteAllTextAsync(Path.Combine(directory, "notes.txt"), "fixture");
        await File.WriteAllTextAsync(Path.Combine(nested, "Nested.otf"), "fixture");

        var store = new ManagedFontFileStore();
        var result = await store.EnumerateManagedFontFilesAsync(new UserFontSettings(directory), CancellationToken.None);

        var path = Assert.Single(result);
        Assert.Equal(Path.Combine(directory, "BrandSans.ttf"), path);
    }
}
