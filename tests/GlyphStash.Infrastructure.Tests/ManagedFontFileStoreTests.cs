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

    [Fact]
    public async Task EnumerateManagedFontFilesAsync_RecoversLegacyDownloads()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        var legacyDirectory = Path.Combine(directory, ".downloads", "google-fonts", "NotoSans");
        Directory.CreateDirectory(legacyDirectory);
        await File.WriteAllTextAsync(Path.Combine(legacyDirectory, "NotoSans-Regular.ttf"), "fixture");
        await File.WriteAllTextAsync(Path.Combine(legacyDirectory, "NotoSans-Regular.woff2"), "fixture");

        var store = new ManagedFontFileStore();
        var result = await store.EnumerateManagedFontFilesAsync(new UserFontSettings(directory), CancellationToken.None);

        var path = Assert.Single(result);
        Assert.Equal(directory, Path.GetDirectoryName(path));
        Assert.EndsWith("-NotoSans-Regular.ttf", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task CopyToManagedDirectoryAsync_RejectsUnsupportedWebFontFormats()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "BrandSans.woff2");
        await File.WriteAllTextAsync(source, "fixture");

        var store = new ManagedFontFileStore();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CopyToManagedDirectoryAsync(source, new UserFontSettings(directory), CancellationToken.None));

        Assert.Contains("仅支持 TTF、OTF、TTC、OTC", ex.Message, StringComparison.Ordinal);
    }
}
