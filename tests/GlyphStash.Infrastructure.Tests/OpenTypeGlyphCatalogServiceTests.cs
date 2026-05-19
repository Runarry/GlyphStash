using GlyphStash.Domain.Fonts;
using GlyphStash.Infrastructure.Glyphs;

namespace GlyphStash.Infrastructure.Tests;

public sealed class OpenTypeGlyphCatalogServiceTests
{
    [Fact]
    public async Task GetGlyphsAsync_SupportsUnicodeSearchAndPaging()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat4Font(), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var page = await service.GetGlyphsAsync(new GlyphQuery(path, "Regular", "U+4F60"), CancellationToken.None);

        Assert.Single(page.Glyphs);
        Assert.Equal("你", page.Glyphs[0].Character);
        Assert.Equal("U+4F60", page.Glyphs[0].UnicodeLabel);
        Assert.Equal("uni4F60", page.Glyphs[0].GlyphName);
    }

    [Fact]
    public async Task GetGlyphsAsync_FiltersByUnicodeBlock()
    {
        var path = Path.Combine(Path.GetTempPath(), "GlyphStash.Tests", $"{Guid.NewGuid():N}.ttf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, CreateFormat4Font(), CancellationToken.None);
        var service = new OpenTypeGlyphCatalogService();

        var page = await service.GetGlyphsAsync(new GlyphQuery(path, "Regular", UnicodeBlockName: "Basic Latin"), CancellationToken.None);

        Assert.Single(page.Glyphs);
        Assert.Equal("A", page.Glyphs[0].Character);
    }

    private static byte[] CreateFormat4Font()
    {
        var cmap = new byte[52];
        WriteUInt16(cmap, 0, 0);
        WriteUInt16(cmap, 2, 1);
        WriteUInt16(cmap, 4, 3);
        WriteUInt16(cmap, 6, 1);
        WriteUInt32(cmap, 8, 12);
        var offset = 12;
        WriteUInt16(cmap, offset, 4);
        WriteUInt16(cmap, offset + 2, 40);
        WriteUInt16(cmap, offset + 4, 0);
        WriteUInt16(cmap, offset + 6, 6);
        WriteUInt16(cmap, offset + 8, 4);
        WriteUInt16(cmap, offset + 10, 1);
        WriteUInt16(cmap, offset + 12, 0);
        WriteUInt16(cmap, offset + 14, 0x0041);
        WriteUInt16(cmap, offset + 16, 0x4F60);
        WriteUInt16(cmap, offset + 18, 0xFFFF);
        WriteUInt16(cmap, offset + 20, 0);
        WriteUInt16(cmap, offset + 22, 0x0041);
        WriteUInt16(cmap, offset + 24, 0x4F60);
        WriteUInt16(cmap, offset + 26, 0xFFFF);
        WriteUInt16(cmap, offset + 28, unchecked((ushort)(1 - 0x0041)));
        WriteUInt16(cmap, offset + 30, unchecked((ushort)(2 - 0x4F60)));
        WriteUInt16(cmap, offset + 32, 1);
        WriteUInt16(cmap, offset + 34, 0);
        WriteUInt16(cmap, offset + 36, 0);
        WriteUInt16(cmap, offset + 38, 0);

        var font = new byte[28 + cmap.Length];
        WriteUInt32(font, 0, 0x00010000);
        WriteUInt16(font, 4, 1);
        WriteAscii(font, 12, "cmap");
        WriteUInt32(font, 20, 28);
        WriteUInt32(font, 24, (uint)cmap.Length);
        cmap.CopyTo(font, 28);
        return font;
    }

    private static void WriteAscii(byte[] bytes, int offset, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            bytes[offset + index] = (byte)value[index];
        }
    }

    private static void WriteUInt16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 1] = (byte)(value & 0xFF);
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)((value >> 24) & 0xFF);
        bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 3] = (byte)(value & 0xFF);
    }
}
